using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>风险评级与判定原因生成。规则保守：可疑即升级风险，宁可不删。</summary>
public static class RiskEvaluator
{
    /// <summary>
    /// 评分规则：
    /// - 高风险：路径命中系统/用户关键目录（Windows/System32/ProgramData/AppData/Users/Recovery/Boot/EFI/Drivers...），
    ///           或位于系统盘且评分很高，或关联索引不可用时；
    /// - 中风险：包含用户数据/配置/存档/数据库，或近期仍有修改，或缺少卸载程序；
    /// - 低风险：非系统盘、像软件目录、有卸载器、无系统引用、无进程占用、最后修改较久。
    /// </summary>
    public static OrphanItem Evaluate(
        SoftwareDirectoryInfo info,
        IReadOnlyList<AssociationHit> hits,
        ScanOptions options,
        int associationIndexCount)
    {
        var reasons = new List<string>();
        int score = 0;

        var criticalKeyword = PathUtils.GetCriticalKeyword(info.Path);
        var systemDriveRoot = PathUtils.GetDriveRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        bool onSystemDrive = systemDriveRoot != null &&
                             string.Equals(PathUtils.GetDriveRoot(info.Path), systemDriveRoot, StringComparison.OrdinalIgnoreCase);

        bool isRunning = hits.Any(h => h.Source == AssociationSource.RunningProcess);

        // —— 高风险因子 ——
        if (criticalKeyword != null)
        {
            score += 90;
            reasons.Add($"路径包含系统/用户关键目录（{criticalKeyword}），禁止自动处理");
        }

        if (onSystemDrive)
        {
            score += 40;
            reasons.Add("位于系统盘");
        }

        if (info.HasReparsePoint)
        {
            score += 15;
            reasons.Add("包含符号链接/挂载点，可能指向其它位置");
        }

        if (associationIndexCount == 0)
        {
            score += 60;
            reasons.Add("系统关联索引为空（可能采集失败），判定不可靠，必须人工确认");
        }

        if (isRunning)
        {
            score += 50;
            reasons.Add("检测到有进程正在使用该目录");
        }

        // —— 中风险因子 ——
        if (info.HasUserData)
        {
            score += 45;
            var samples = info.UserDataSamples.Take(3).ToList();
            reasons.Add("包含可能重要的用户数据/配置/存档：" + string.Join("、", samples));
        }

        var days = (DateTime.Now - info.LastModified).TotalDays;
        if (days < 90)
        {
            // 近期仍有修改 —— 保守起见至少升级到中风险
            score += 40;
            reasons.Add($"最近 {(int)days} 天内有修改，可能仍在使用");
        }
        else if (days < 180)
        {
            score += 10;
            reasons.Add($"最近 {(int)days} 天内有修改");
        }
        else
        {
            reasons.Add($"已 {(int)days} 天未修改");
        }

        if (!info.HasUninstaller)
        {
            score += 10;
            reasons.Add("未发现卸载程序，无法通过标准卸载流程移除");
        }
        else
        {
            reasons.Add("存在卸载程序，建议优先使用其自身卸载程序");
        }

        // —— 系统引用情况 ——
        if (hits.Count == 0)
        {
            reasons.Add("无任何系统引用（注册表卸载项 / App Paths / 快捷方式 / 服务 / 计划任务 / 进程 / 文件关联 / 启动项均未命中）");
        }
        else
        {
            foreach (var group in hits.GroupBy(h => h.Source))
            {
                reasons.Add($"仍被{SourceText(group.Key)}引用：{string.Join("、", group.Take(2).Select(g => g.Name))}");
            }
        }

        // —— 白名单 ——
        string? excludeReason = null;
        var excludedPath = options.ExcludedPaths.FirstOrDefault(p => PathUtils.IsUnder(info.Path, p));
        if (excludedPath != null) excludeReason = "命中排除路径：" + excludedPath;

        var keyword = options.KeepKeywords.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k) && PathUtils.ContainsKeyword(info.Path, new[] { k }));
        if (keyword != null) excludeReason = "命中保留关键词：" + keyword;

        if (criticalKeyword != null) excludeReason ??= "系统关键目录，默认不参与删除";

        // 关联索引为空说明采集失败，判定不可靠 —— 直接升级为高风险，要求人工确认
        bool unreliable = associationIndexCount == 0;

        var risk = criticalKeyword != null || unreliable || score >= 80 ? RiskLevel.High
            : score >= 35 ? RiskLevel.Medium
            : RiskLevel.Low;

        var displayName = !string.IsNullOrWhiteSpace(info.ProductName)
            ? info.ProductName!
            : !string.IsNullOrWhiteSpace(info.Name)
                ? info.Name
                : System.IO.Path.GetFileName(info.Path);

        return new OrphanItem
        {
            Path = info.Path,
            DisplayName = displayName,
            Version = info.Version,
            Publisher = info.Publisher,
            ProductName = info.ProductName,
            SizeBytes = info.TotalSizeBytes,
            FileCount = info.FileCount,
            DirectoryCount = info.DirectoryCount,
            LastModified = info.LastModified,
            MainExecutables = info.ExecutablePaths,
            HasUninstaller = info.HasUninstaller,
            UninstallerPath = info.UninstallerPath,
            HasUserData = info.HasUserData,
            UserDataSamples = info.UserDataSamples,
            IsRunning = isRunning,
            SoftwareScore = info.SoftwareScore,
            IsLikelySoftwareDirectory = info.IsLikelySoftwareDirectory,
            Risk = risk,
            RiskScore = Math.Clamp(score, 0, 100),
            Reasons = reasons,
            Associations = hits.ToList(),
            IsExcluded = excludeReason != null,
            ExcludeReason = excludeReason,
        };
    }

    public static string SourceText(AssociationSource source) => source switch
    {
        AssociationSource.RegistryUninstall => "注册表卸载项",
        AssociationSource.AppPaths => "App Paths",
        AssociationSource.StartMenuShortcut => "开始菜单快捷方式",
        AssociationSource.DesktopShortcut => "桌面快捷方式",
        AssociationSource.StartupEntry => "启动项",
        AssociationSource.WindowsService => "Windows 服务",
        AssociationSource.ScheduledTask => "计划任务",
        AssociationSource.RunningProcess => "运行中的进程",
        AssociationSource.AppxPackage => "Microsoft Store 应用",
        AssociationSource.FileAssociation => "文件关联/右键菜单",
        _ => "其它",
    };
}

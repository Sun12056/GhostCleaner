using WinCleaner.Core.Models;

namespace WinCleaner.Scanner;

/// <summary>证据收集结果。</summary>
public sealed class EvidenceCollection
{
    public List<Evidence> Evidence { get; } = new();

    public List<string> Warnings { get; } = new();

    /// <summary>目录的"安装痕迹"系数（0~1）。越低说明越不像"被安装出来的软件"。</summary>
    public double InstallationFootprint { get; set; }

    public void Add(EvidenceType type, int score, string title, string description, bool isPositive, EvidenceSeverity severity, AssociationSource? source = null)
        => Evidence.Add(new Evidence
        {
            Type = type,
            Score = score,
            Title = title,
            Description = description,
            IsPositive = isPositive,
            Severity = severity,
            Source = source,
        });

    public void Warn(string message)
    {
        if (!Warnings.Contains(message)) Warnings.Add(message);
    }
}

/// <summary>
/// 证据引擎：把"文件系统特征 + 系统关联 + 使用状况 + 目录性质 + 扫描健康度"
/// 转成一组可解释的 <see cref="Evidence"/>。
///
/// 核心约束：<b>缺少关联只有在对应扫描源成功完成时才是证据</b>；
/// 扫描失败只能产生 <see cref="EvidenceType.AssociationSourceFailed"/> 与 Warning。
/// </summary>
public static class EvidenceCollector
{
    public static EvidenceCollection Collect(
        SoftwareDirectoryInfo info,
        DirectoryFacts facts,
        IReadOnlyList<AssociationHit> hits,
        IReadOnlyList<AssociationSourceHealth> health,
        EvidenceWeights weights)
    {
        var result = new EvidenceCollection();
        result.InstallationFootprint = ComputeFootprint(info, weights);

        CollectSoftwareTraitEvidence(info, weights, result);
        CollectAssociationEvidence(info, hits, health, weights, result);
        CollectUsageEvidence(info, hits, health, weights, result);
        CollectProtectionEvidence(info, facts, weights, result);
        CollectHealthEvidence(health, weights, result);

        return result;
    }

    /// <summary>
    /// 安装痕迹系数：目录有多像"被安装出来的软件"。
    /// 绿色软件 / 游戏 / 开发项目这个值很低，因此"缺少系统关联"对它们几乎没有说服力。
    /// </summary>
    public static double ComputeFootprint(SoftwareDirectoryInfo info, EvidenceWeights w)
    {
        double footprint = 0;

        if (info.HasUninstaller) footprint += w.FootprintUninstaller;
        if (info.HasVersionResource) footprint += w.FootprintVersionResource;
        if (!string.IsNullOrWhiteSpace(info.Publisher)) footprint += w.FootprintPublisher;
        if (info.LibraryCount >= w.FootprintLibraryThreshold) footprint += w.FootprintLibraries;
        if (info.FileCount >= w.FootprintFileCountThreshold) footprint += w.FootprintFileCount;
        if (info.TotalSizeBytes >= w.FootprintSizeThresholdBytes) footprint += w.FootprintSize;

        return Math.Clamp(Math.Max(footprint, w.MinInstallationFootprint), 0, 1);
    }

    // ---------------- 软件特征（正向） ----------------

    private static void CollectSoftwareTraitEvidence(SoftwareDirectoryInfo info, EvidenceWeights w, EvidenceCollection result)
    {
        if (info.IsLikelySoftwareDirectory)
        {
            result.Add(EvidenceType.SoftwareDirectory, w.SoftwareDirectory,
                "具备软件安装目录特征",
                $"包含 {info.ExecutableCount} 个可执行文件、{info.LibraryCount} 个动态库、{info.FileCount} 个文件",
                true, EvidenceSeverity.Info);
        }

        if (info.ExecutableCount >= 1)
        {
            result.Add(EvidenceType.Executable, w.Executable,
                "包含可执行文件",
                info.ExecutableCount > 1 ? $"共 {info.ExecutableCount} 个 exe" : "包含 1 个 exe",
                true, EvidenceSeverity.Info);
        }

        if (info.LibraryCount >= 1)
        {
            result.Add(EvidenceType.Library, w.Library,
                "包含动态库/组件",
                $"共 {info.LibraryCount} 个 dll/组件",
                true, EvidenceSeverity.Info);
        }

        if (info.HasUninstaller)
        {
            result.Add(EvidenceType.Uninstaller, w.Uninstaller,
                "发现卸载程序",
                "目录包含 " + System.IO.Path.GetFileName(info.UninstallerPath ?? string.Empty),
                true, EvidenceSeverity.Notice);
        }

        if (info.HasVersionResource)
        {
            result.Add(EvidenceType.VersionResource, w.VersionResource,
                "读取到版本信息",
                string.IsNullOrWhiteSpace(info.Version) ? "存在版本资源" : "版本：" + info.Version,
                true, EvidenceSeverity.Info);
        }

        if (!string.IsNullOrWhiteSpace(info.Publisher))
        {
            result.Add(EvidenceType.Publisher, w.Publisher,
                "读取到厂商信息",
                "厂商：" + info.Publisher,
                true, EvidenceSeverity.Info);
        }
    }

    // ---------------- 系统关联 ----------------

    private static void CollectAssociationEvidence(
        SoftwareDirectoryInfo info,
        IReadOnlyList<AssociationHit> hits,
        IReadOnlyList<AssociationSourceHealth> health,
        EvidenceWeights w,
        EvidenceCollection result)
    {
        bool hasProcess = hits.Any(h => h.Source == AssociationSource.RunningProcess);

        var found = hits.Where(h => h.Source != AssociationSource.RunningProcess)
                        .GroupBy(h => h.Source)
                        .Select(g => (Source: g.Key, Names: g.Select(x => x.Name).Distinct().Take(2).ToList()))
                        .ToList();

        if (found.Count > 0)
        {
            var primary = PrimarySource(found.Select(f => f.Source));
            var summary = string.Join("、", found.Select(f =>
                AssociationSourceText.ToText(f.Source) + "（" + string.Join("、", f.Names) + "）"));

            result.Add(PrimaryEvidenceType(primary), w.MaxAssociationPenalty,
                "仍被系统引用",
                "发现 " + summary + "，Windows 仍然识别该目录",
                false, EvidenceSeverity.Notice, primary);
        }

        var footprint = result.InstallationFootprint;
        string scaleNote = footprint < 1
            ? $"（该目录安装痕迹较弱，缺失类证据已按 {footprint:P0} 折算）"
            : string.Empty;

        AddMissing(hits, w, result, health, footprint, scaleNote,
            AssociationSource.RegistryUninstall,
            EvidenceType.MissingUninstallEntry, w.MissingUninstallEntry,
            "未发现对应的卸载注册表项",
            "注册表中没有指向该目录的卸载项");

        AddMissing(hits, w, result, health, footprint, scaleNote,
            AssociationSource.AppPaths,
            EvidenceType.MissingAppPath, w.MissingAppPath,
            "未发现 App Paths 注册",
            "App Paths 中没有指向该目录的程序");

        AddMissing(hits, w, result, health, footprint, scaleNote,
            new[] { AssociationSource.StartMenuShortcut, AssociationSource.DesktopShortcut },
            EvidenceType.MissingShortcut, w.MissingShortcut,
            "未发现快捷方式",
            "开始菜单与桌面快捷方式中均未指向该目录");

        AddMissing(hits, w, result, health, footprint, scaleNote,
            AssociationSource.WindowsService,
            EvidenceType.MissingService, w.MissingService,
            "未发现相关 Windows 服务",
            "没有服务的 ImagePath 指向该目录");

        AddMissing(hits, w, result, health, footprint, scaleNote,
            AssociationSource.ScheduledTask,
            EvidenceType.MissingScheduledTask, w.MissingScheduledTask,
            "未发现相关计划任务",
            "没有计划任务引用该目录");

        AddMissing(hits, w, result, health, footprint, scaleNote,
            AssociationSource.StartupEntry,
            EvidenceType.MissingStartupEntry, w.MissingStartupEntry,
            "未发现相关启动项",
            "Run / RunOnce / 启动目录中没有指向该目录的条目");

        if (hasProcess)
        {
            var names = hits.Where(h => h.Source == AssociationSource.RunningProcess)
                            .Select(h => h.Name).Distinct().Take(3).ToList();

            result.Add(EvidenceType.RunningProcess, w.RunningProcessFound,
                "检测到正在运行的进程",
                "进程：" + string.Join("、", names) + " —— 软件很可能仍在使用",
                false, EvidenceSeverity.Critical, AssociationSource.RunningProcess);
        }
    }

    private static void AddMissing(
        IReadOnlyList<AssociationHit> hits,
        EvidenceWeights w,
        EvidenceCollection result,
        IReadOnlyList<AssociationSourceHealth> health,
        double footprint,
        string scaleNote,
        AssociationSource source,
        EvidenceType type,
        int weight,
        string title,
        string description)
        => AddMissing(hits, w, result, health, footprint, scaleNote, new[] { source }, type, weight, title, description);

    private static void AddMissing(
        IReadOnlyList<AssociationHit> hits,
        EvidenceWeights w,
        EvidenceCollection result,
        IReadOnlyList<AssociationSourceHealth> health,
        double footprint,
        string scaleNote,
        AssociationSource[] sources,
        EvidenceType type,
        int weight,
        string title,
        string description)
    {
        // 已经命中了该来源的引用 —— 不能再生成"缺失"证据
        if (hits.Any(h => sources.Contains(h.Source))) return;

        // 关键：只有在该来源扫描成功时，"没有找到"才等于"确实没有"
        if (!IsSourceReliable(health, sources))
        {
            var label = string.Join("/", sources.Select(AssociationSourceText.ToText));
            result.Warn($"未能完成【{label}】关联检查，不能据此认为该目录没有引用");
            return;
        }

        var scaled = (int)Math.Round(weight * footprint);
        if (scaled == 0) return;

        result.Add(type, scaled, title, description + scaleNote, true, EvidenceSeverity.Info, sources[0]);
    }

    /// <summary>该来源是否至少有一个采集器成功完成。</summary>
    public static bool IsSourceReliable(IReadOnlyList<AssociationSourceHealth> health, params AssociationSource[] sources)
        => sources.Any(s => health.Any(h => h.Covers(s) && h.Status == AssociationSourceStatus.Success));

    private static AssociationSource PrimarySource(IEnumerable<AssociationSource> sources)
    {
        var set = sources.ToList();
        foreach (var preferred in new[]
                 {
                     AssociationSource.RegistryUninstall,
                     AssociationSource.AppPaths,
                     AssociationSource.StartMenuShortcut,
                     AssociationSource.DesktopShortcut,
                     AssociationSource.WindowsService,
                     AssociationSource.ScheduledTask,
                     AssociationSource.StartupEntry,
                     AssociationSource.FileAssociation,
                     AssociationSource.AppxPackage,
                 })
        {
            if (set.Contains(preferred)) return preferred;
        }

        return set.Count > 0 ? set[0] : AssociationSource.Other;
    }

    private static EvidenceType PrimaryEvidenceType(AssociationSource source) => source switch
    {
        AssociationSource.RegistryUninstall => EvidenceType.RegistryAssociation,
        AssociationSource.AppPaths => EvidenceType.AppPath,
        AssociationSource.StartMenuShortcut or AssociationSource.DesktopShortcut => EvidenceType.Shortcut,
        AssociationSource.WindowsService => EvidenceType.Service,
        AssociationSource.ScheduledTask => EvidenceType.ScheduledTask,
        AssociationSource.StartupEntry => EvidenceType.StartupEntry,
        AssociationSource.FileAssociation => EvidenceType.FileAssociation,
        AssociationSource.AppxPackage => EvidenceType.AppxPackage,
        _ => EvidenceType.RegistryAssociation,
    };

    // ---------------- 使用状况 ----------------

    private static void CollectUsageEvidence(
        SoftwareDirectoryInfo info,
        IReadOnlyList<AssociationHit> hits,
        IReadOnlyList<AssociationSourceHealth> health,
        EvidenceWeights w,
        EvidenceCollection result)
    {
        var days = (DateTime.Now - info.LastModified).TotalDays;

        if (days >= w.LongUnmodifiedDays)
        {
            result.Add(EvidenceType.LongUnmodified, w.LongUnmodified,
                "长期未修改",
                $"已 {(int)days} 天未修改",
                true, EvidenceSeverity.Info);
        }
        else if (days <= w.RecentModificationDays)
        {
            result.Add(EvidenceType.RecentModification, w.RecentModificationPenalty,
                "近期有修改",
                $"最近 {Math.Max((int)days, 0)} 天内被修改过，可能仍在使用",
                false, EvidenceSeverity.Warning);
        }
        else if (days <= w.RecentUsageDays)
        {
            result.Add(EvidenceType.RecentUsage, w.RecentUsagePenalty,
                "近期有活动",
                $"最近 {(int)days} 天内有修改",
                false, EvidenceSeverity.Notice);
        }

        bool hasProcess = hits.Any(h => h.Source == AssociationSource.RunningProcess);
        if (!hasProcess)
        {
            if (IsSourceReliable(health, AssociationSource.RunningProcess))
            {
                result.Add(EvidenceType.NoRunningProcess, w.NoRunningProcess,
                    "未检测到运行中的进程",
                    "当前没有进程在使用该目录（不代表将来不会被使用）",
                    true, EvidenceSeverity.Info);
            }
            else
            {
                result.Warn("未能完成【运行中的进程】检查，无法确认该目录是否正在被使用");
            }
        }
    }

    // ---------------- 保护性证据（负向） ----------------

    private static void CollectProtectionEvidence(SoftwareDirectoryInfo info, DirectoryFacts facts, EvidenceWeights w, EvidenceCollection result)
    {
        if (facts.IsDevelopmentProject)
        {
            result.Add(EvidenceType.DevelopmentProject, w.DevelopmentProjectPenalty,
                "疑似开发项目/工具链",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("开发")).Take(3)),
                false, EvidenceSeverity.Critical);
            result.Warn("该目录疑似开发项目或工具链，天然可能没有卸载注册表项，不应按软件残留处理");
        }

        if (facts.IsGameDirectory)
        {
            result.Add(EvidenceType.GameDirectory, w.GameDirectoryPenalty,
                "疑似游戏目录",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("游戏")).Take(3)),
                false, EvidenceSeverity.Critical);
            result.Warn("该目录疑似游戏/游戏库，默认只建议人工确认，不参与自动清理");
        }

        if (facts.IsPortableSoftware)
        {
            result.Add(EvidenceType.PortableSoftware, w.PortableSoftwarePenalty,
                "疑似绿色/Portable 软件",
                "命中：" + string.Join("、", facts.MatchedTags.Take(3)),
                false, EvidenceSeverity.Warning);
        }

        if (facts.HasSaveData)
        {
            result.Add(EvidenceType.SaveData, w.SaveDataPenalty,
                "检测到存档数据",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("存档")).Take(3)),
                false, EvidenceSeverity.Critical);
        }

        if (facts.HasProfileData)
        {
            result.Add(EvidenceType.ProfileData, w.ProfileDataPenalty,
                "检测到用户配置/个人资料",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("用户配置")).Take(3)),
                false, EvidenceSeverity.Critical);
        }

        if (facts.HasDocuments)
        {
            result.Add(EvidenceType.Documents, w.DocumentsPenalty,
                "检测到个人文档/媒体",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("个人资料")).Take(3)),
                false, EvidenceSeverity.Critical);
        }

        if (facts.HasBackupData)
        {
            result.Add(EvidenceType.BackupData, w.BackupDataPenalty,
                "检测到备份数据",
                "命中：" + string.Join("、", facts.MatchedTags.Where(t => t.StartsWith("备份")).Take(3)),
                false, EvidenceSeverity.Critical);
        }

        if (facts.HasConfiguration)
        {
            result.Add(EvidenceType.Configuration, w.ConfigurationPenalty,
                "检测到明确的配置目录",
                "目录内存在 config / settings 等配置目录",
                false, EvidenceSeverity.Notice);
        }

        bool strongUserData = facts.HasSaveData || facts.HasProfileData || facts.HasDocuments || facts.HasBackupData;
        if (facts.HasUserData && !strongUserData)
        {
            result.Add(EvidenceType.UserData, w.UserDataPenalty,
                "检测到可能的用户数据",
                info.UserDataSamples.Count > 0 ? "例如：" + string.Join("、", info.UserDataSamples.Take(3)) : "目录内存在配置/数据文件",
                false, EvidenceSeverity.Warning);
        }

        if (facts.IsCriticalPath)
        {
            result.Add(EvidenceType.CriticalPath, w.CriticalPathPenalty,
                "位于系统关键路径",
                "系统/用户关键目录禁止自动处理",
                false, EvidenceSeverity.Critical);
        }

        if (facts.IsSystemInstallRoot)
        {
            result.Add(EvidenceType.SystemDirectory, w.SystemInstallRootPenalty,
                "位于系统安装目录",
                "位于 Program Files / Windows 之下，误删代价高，需人工确认",
                false, EvidenceSeverity.Warning);
        }

        if (facts.IsOnSystemDrive)
        {
            result.Add(EvidenceType.SystemDirectory, w.SystemDrivePenalty,
                "位于系统盘",
                "系统盘上的目录误删代价更高",
                false, EvidenceSeverity.Notice);
        }

        if (info.HasReparsePoint)
        {
            result.Add(EvidenceType.ReparsePoint, w.ReparsePointPenalty,
                "包含符号链接/挂载点",
                "可能指向其它位置，删除结果难以预期",
                false, EvidenceSeverity.Warning);
        }
    }

    // ---------------- 扫描健康度 ----------------

    private static void CollectHealthEvidence(IReadOnlyList<AssociationSourceHealth> health, EvidenceWeights w, EvidenceCollection result)
    {
        var unhealthy = health
            .Where(h => h.Status is AssociationSourceStatus.Failed or AssociationSourceStatus.Partial or AssociationSourceStatus.NotRun)
            .ToList();

        foreach (var source in unhealthy)
        {
            result.Add(EvidenceType.AssociationSourceFailed, w.SourceFailurePenalty,
                $"关联检查未完成：{source.SourceName}",
                AssociationSourceStatusToReason(source),
                false, EvidenceSeverity.Warning, source.CoveredSources.FirstOrDefault());
        }

        var registry = health.FirstOrDefault(h => h.Covers(AssociationSource.RegistryUninstall));
        if (registry != null && registry.Status != AssociationSourceStatus.Success)
        {
            result.Add(EvidenceType.AssociationSourceFailed, w.RegistrySourceFailurePenalty,
                "注册表关联检查不可用",
                "这是最关键的关联来源；结果不完整时不能得出“没有注册表引用”的结论",
                false, EvidenceSeverity.Critical, AssociationSource.RegistryUninstall);

            result.Warn("注册表卸载项检查未完成，无法确认该目录是否存在卸载注册表项（这不等同于“没有引用”）");
        }

        if (unhealthy.Count > 0)
            result.Warn($"扫描结果可能不完整：{unhealthy.Count} 个关联来源未能完成检查（"
                        + string.Join("、", unhealthy.Select(u => u.SourceName)) + "）");
    }

    private static string AssociationSourceStatusToReason(AssociationSourceHealth source) => source.Status switch
    {
        AssociationSourceStatus.Failed => "采集失败：" + (source.ErrorMessage ?? "未知原因"),
        AssociationSourceStatus.Partial => "未能采集到任何记录，结果可能不完整",
        _ => "未执行",
    };
}

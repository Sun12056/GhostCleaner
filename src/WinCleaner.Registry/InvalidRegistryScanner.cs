using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Registry;

/// <summary>
/// 无效注册表项扫描与清理：无效卸载项 / 无效文件关联 / 无效服务 / 无效启动项。
///
/// ⚠ <b>状态：Beta / Experimental（v0.1）</b>
/// GhostCleaner v0.1 的核心目标是"孤儿软件目录检测"，注册表清理不作为稳定功能。
/// 使用约束：
/// 1. 所有删除操作都会先用 reg.exe 导出 .reg 备份，并生成 JSON 清单；
/// 2. 不允许批量无确认删除；
/// 3. "路径不存在"不再直接等同于"注册表项无效" —— 外置硬盘未连接、网络盘断开、
///    权限问题、按需安装组件都会造成路径临时不可达。
/// </summary>
public sealed class InvalidRegistryScanner : IRegistryCleaner
{
    /// <summary>Beta 提示文案（UI 必须展示）。</summary>
    public const string FeatureNotice =
        "注册表清理为 Beta / 实验性功能：路径不存在并不等于软件已卸载（可能是外置硬盘未连接或权限问题）。"
        + "删除前会自动 reg export 备份，且必须逐项人工确认，不支持批量无确认删除。";

    private readonly string _backupRoot;

    public InvalidRegistryScanner(string backupRoot)
    {
        _backupRoot = backupRoot;
    }

    public async Task<IReadOnlyList<RegistryIssue>> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => await Task.Run(() => ScanInternal(progress, cancellationToken), cancellationToken);

    private List<RegistryIssue> ScanInternal(IProgress<string>? progress, CancellationToken ct)
    {
        var issues = new List<RegistryIssue>();

        progress?.Report("扫描无效卸载项");
        ct.ThrowIfCancellationRequested();
        issues.AddRange(ScanInvalidUninstall());

        progress?.Report("扫描无效服务");
        ct.ThrowIfCancellationRequested();
        issues.AddRange(ScanInvalidServices());

        progress?.Report("扫描无效启动项");
        ct.ThrowIfCancellationRequested();
        issues.AddRange(ScanInvalidStartup());

        progress?.Report("扫描无效文件关联");
        ct.ThrowIfCancellationRequested();
        issues.AddRange(ScanInvalidFileAssociations());

        return issues;
    }

    private static List<RegistryIssue> ScanInvalidUninstall()
    {
        var list = new List<RegistryIssue>();

        foreach (var entry in UninstallEntryReader.ReadAll())
        {
            if (string.IsNullOrWhiteSpace(entry.DisplayName)) continue;

            var uninstallExe = CommandLinePathParser.ParseExecutablePath(entry.UninstallString);
            var installDir = CommandLinePathParser.ParseDirectoryPath(entry.InstallLocation);
            var icon = CommandLinePathParser.ParseIconPath(entry.DisplayIcon);

            // 关键：只有在"路径确实可达"的前提下，才能得出"已不存在"的结论。
            // 外置硬盘未连接 / 网络盘断开 / 权限不足都会造成路径临时不可达。
            if (uninstallExe != null && IsLocationVerifiable(uninstallExe) && !File.Exists(uninstallExe))
            {
                list.Add(Make(entry.RegistryPath, "无效卸载项", entry.DisplayName!, uninstallExe, "卸载程序已不存在"));
                continue;
            }

            if (installDir != null && IsLocationVerifiable(installDir) && !Directory.Exists(installDir))
            {
                list.Add(Make(entry.RegistryPath, "无效卸载项", entry.DisplayName!, installDir, "安装目录已不存在"));
                continue;
            }

            if (uninstallExe == null && installDir == null && icon == null)
            {
                list.Add(Make(entry.RegistryPath, "无效卸载项", entry.DisplayName!, null, "没有任何有效的路径信息"));
            }
        }

        return list;
    }

    private static List<RegistryIssue> ScanInvalidServices()
    {
        var list = new List<RegistryIssue>();
        const string servicesKey = @"SYSTEM\CurrentControlSet\Services";

        List<string> names = new();
        try
        {
            using var baseKey0 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key0 = baseKey0.OpenSubKey(servicesKey);
            if (key0 != null) names.AddRange(key0.GetSubKeyNames());
        }
        catch
        {
            return list;
        }

        foreach (var name in names)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(servicesKey);
                using var sub = key?.OpenSubKey(name);
                var imagePath = sub?.GetValue("ImagePath") as string;
                var exe = CommandLinePathParser.ParseExecutablePath(imagePath);
                if (exe == null) continue;                       // 驱动等非 exe 项跳过
                if (!IsLocationVerifiable(exe)) continue;        // 目标盘当前不可达，无法判定
                if (File.Exists(exe)) continue;

                list.Add(Make($@"HKLM\{servicesKey}\{name}", "无效服务", name, exe, "服务可执行文件已不存在"));
            }
            catch
            {
                // 忽略
            }
        }

        return list;
    }

    private static List<RegistryIssue> ScanInvalidStartup()
    {
        var list = new List<RegistryIssue>();

        string[] runKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
        };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            var hiveText = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
            foreach (var runKey in runKeys)
            {
                List<(string Value, string Name)> values = new();
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                    using var key = baseKey.OpenSubKey(runKey);
                    if (key != null)
                    {
                        foreach (var valueName in key.GetValueNames())
                        {
                            try
                            {
                                var v = key.GetValue(valueName) as string;
                                if (!string.IsNullOrWhiteSpace(v)) values.Add((v!, valueName));
                            }
                            catch
                            {
                                // 忽略
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }

                foreach (var (value, valueName) in values)
                {
                    try
                    {
                        var exe = CommandLinePathParser.ParseExecutablePath(value);
                        if (exe == null || !IsLocationVerifiable(exe) || File.Exists(exe)) continue;

                        list.Add(new RegistryIssue
                        {
                            KeyPath = $@"{hiveText}\{runKey}",
                            Hive = hiveText,
                            ValueName = valueName,
                            Category = "无效启动项",
                            DisplayName = valueName,
                            TargetPath = exe,
                            Reason = "启动项指向的程序已不存在",
                        });
                    }
                    catch
                    {
                        // 忽略
                    }
                }
            }
        }

        return list;
    }

    private static List<RegistryIssue> ScanInvalidFileAssociations()
    {
        var list = new List<RegistryIssue>();
        const string appsKey = @"SOFTWARE\Classes\Applications";

        List<string> appNames = new();
        try
        {
            using var baseKey0 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var apps0 = baseKey0.OpenSubKey(appsKey);
            if (apps0 != null) appNames.AddRange(apps0.GetSubKeyNames());
        }
        catch
        {
            return list;
        }

        foreach (var app in appNames)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var apps = baseKey.OpenSubKey(appsKey);
                using var appKey = apps?.OpenSubKey(app);
                using var shell = appKey?.OpenSubKey("shell");
                if (shell == null) continue;

                foreach (var verb in shell.GetSubKeyNames())
                {
                    try
                    {
                        using var verbKey = shell.OpenSubKey(verb);
                        using var cmd = verbKey?.OpenSubKey("command");
                        var value = cmd?.GetValue(null) as string;
                        var exe = CommandLinePathParser.ParseExecutablePath(value);
                        if (exe == null || !IsLocationVerifiable(exe) || File.Exists(exe)) continue;

                        list.Add(Make($@"HKLM\{appsKey}\{app}", "无效文件关联", app, exe, "关联的程序已不存在"));
                        break;
                    }
                    catch
                    {
                        // 忽略
                    }
                }
            }
            catch
            {
                // 忽略
            }
        }

        return list;
    }

    /// <summary>
    /// 该路径所在的位置当前是否可核验。
    /// 盘符根不存在（外置硬盘未连接 / 网络盘断开）时返回 false —— 此时"文件不存在"不能作为无效证据。
    /// </summary>
    private static bool IsLocationVerifiable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var root = PathUtils.GetDriveRoot(path);
        if (root == null) return true;   // UNC 等路径：交给 File/Directory.Exists 判断

        return Directory.Exists(root);
    }

    private static RegistryIssue Make(string registryPath, string category, string displayName, string? target, string reason)
    {
        var hive = registryPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) ? "HKCU" : "HKLM";
        return new RegistryIssue
        {
            KeyPath = registryPath,
            Hive = hive,
            Category = category,
            DisplayName = displayName,
            TargetPath = target,
            Reason = reason,
        };
    }

    /// <summary>备份：对每个待处理注册表项执行 reg export，并生成 JSON 清单。返回备份目录。</summary>
    public async Task<string> BackupAsync(IEnumerable<RegistryIssue> issues, CancellationToken cancellationToken = default)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dir = System.IO.Path.Combine(_backupRoot, "RegistryBackup", stamp);
        Directory.CreateDirectory(dir);

        var list = issues.ToList();
        var manifest = new List<object>();

        for (int i = 0; i < list.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var issue = list[i];
            var safe = PathUtils.SanitizeFileName($"{i:000}_{issue.Category}_{issue.DisplayName}");
            var regFile = System.IO.Path.Combine(dir, safe + ".reg");

            bool exported;
            try
            {
                exported = await ExportRegAsync(issue.KeyPath, regFile).ConfigureAwait(false);
            }
            catch
            {
                exported = false;
            }

            manifest.Add(new
            {
                issue.Id,
                issue.Category,
                issue.DisplayName,
                issue.KeyPath,
                issue.ValueName,
                issue.Hive,
                issue.TargetPath,
                issue.Reason,
                RegFile = exported ? System.IO.Path.GetFileName(regFile) : null,
            });
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir, "manifest.json"), json, cancellationToken).ConfigureAwait(false);

        return dir;
    }

    private static async Task<bool> ExportRegAsync(string keyPath, string outputFile)
    {
        var psi = new ProcessStartInfo("reg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("export");
        psi.ArgumentList.Add(keyPath);
        psi.ArgumentList.Add(outputFile);
        psi.ArgumentList.Add("/y");

        using var p = Process.Start(psi);
        if (p == null) return false;
        await p.WaitForExitAsync().ConfigureAwait(false);
        return p.ExitCode == 0 && File.Exists(outputFile);
    }

    /// <summary>删除前自动备份，然后删除。返回成功删除的数量。</summary>
    public async Task<int> RemoveAsync(IEnumerable<RegistryIssue> issues, CancellationToken cancellationToken = default)
    {
        var list = issues.Where(x => !x.IsFixed).ToList();
        if (list.Count == 0) return 0;

        await BackupAsync(list, cancellationToken).ConfigureAwait(false);

        int removed = 0;
        foreach (var issue in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var hive = issue.Hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
                    ? RegistryHive.CurrentUser
                    : RegistryHive.LocalMachine;

                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

                // KeyPath 形如 HKLM\SOFTWARE\... ，去掉 hive 前缀
                var relative = issue.KeyPath.Substring(issue.Hive.Length).TrimStart('\\');

                if (!string.IsNullOrEmpty(issue.ValueName))
                {
                    using var key = baseKey.OpenSubKey(relative, writable: true);
                    key?.DeleteValue(issue.ValueName, throwOnMissingValue: false);
                    removed++;
                    issue.IsFixed = true;
                    issue.OperationResult = "已删除注册表值";
                }
                else
                {
                    using var key = baseKey.OpenSubKey(relative);
                    if (key == null)
                    {
                        issue.IsFixed = true;
                        issue.OperationResult = "键已不存在";
                        continue;
                    }

                    var parts = relative.Split('\\');
                    var subName = parts.Last();
                    var parentPath = string.Join('\\', parts[..^1]);
                    using var parent = baseKey.OpenSubKey(parentPath, writable: true);
                    parent?.DeleteSubKeyTree(subName, throwOnMissingSubKey: false);
                    removed++;
                    issue.IsFixed = true;
                    issue.OperationResult = "已删除注册表项";
                }
            }
            catch (Exception ex)
            {
                issue.OperationResult = "删除失败：" + ex.Message;
            }
        }

        return removed;
    }
}

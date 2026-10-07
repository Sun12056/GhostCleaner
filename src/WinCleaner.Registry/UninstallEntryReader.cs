using Microsoft.Win32;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Registry;

/// <summary>注册表中的一个"已安装程序"条目。</summary>
public sealed class UninstallEntry
{
    public string KeyName { get; init; } = string.Empty;

    public string RegistryPath { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? Publisher { get; init; }

    public string? DisplayVersion { get; init; }

    public string? InstallLocation { get; init; }

    public string? UninstallString { get; init; }

    public string? DisplayIcon { get; init; }

    public string? EstimatedSizeKb { get; init; }

    public DateTime? InstallDate { get; init; }
}

/// <summary>读取 HKLM / HKCU（含 32 位与 64 位视图）下的卸载项。</summary>
public static class UninstallEntryReader
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string WowUninstallSubKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<UninstallEntry> ReadAll()
    {
        var results = new List<UninstallEntry>();

        void ReadHive(RegistryHive hive, RegistryView view, string subKey)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key == null) return;

                foreach (var name in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(name);
                        if (sub == null) continue;

                        results.Add(new UninstallEntry
                        {
                            KeyName = name,
                            RegistryPath = $@"{hive}\{view}\{subKey}\{name}",
                            DisplayName = sub.GetValue("DisplayName") as string,
                            Publisher = sub.GetValue("Publisher") as string,
                            DisplayVersion = sub.GetValue("DisplayVersion") as string,
                            InstallLocation = sub.GetValue("InstallLocation") as string,
                            UninstallString = sub.GetValue("UninstallString") as string,
                            DisplayIcon = sub.GetValue("DisplayIcon") as string,
                            EstimatedSizeKb = sub.GetValue("EstimatedSize")?.ToString(),
                            InstallDate = ParseInstallDate(sub.GetValue("InstallDate") as string),
                        });
                    }
                    catch
                    {
                        // 单个损坏的注册表项不影响整体
                    }
                }
            }
            catch
            {
                // 无权限或键不存在
            }
        }

        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, UninstallSubKey);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, WowUninstallSubKey);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Default, UninstallSubKey);

        return results;
    }

    /// <summary>卸载条目引用的所有路径（安装目录 / 卸载程序 / 图标）。</summary>
    public static IEnumerable<AssociationRecord> ToRecords(IEnumerable<UninstallEntry> entries)
    {
        foreach (var e in entries)
        {
            var label = e.DisplayName ?? e.KeyName;

            var dir = CommandLinePathParser.ParseDirectoryPath(e.InstallLocation);
            if (!string.IsNullOrEmpty(dir))
                yield return new AssociationRecord(dir!, AssociationSource.RegistryUninstall, label);

            var uninstallExe = CommandLinePathParser.ParseExecutablePath(e.UninstallString);
            if (!string.IsNullOrEmpty(uninstallExe))
                yield return new AssociationRecord(uninstallExe!, AssociationSource.RegistryUninstall, label);

            var icon = CommandLinePathParser.ParseIconPath(e.DisplayIcon);
            if (!string.IsNullOrEmpty(icon))
                yield return new AssociationRecord(icon!, AssociationSource.RegistryUninstall, label);
        }
    }

    private static DateTime? ParseInstallDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8) return null;
        if (!int.TryParse(value[..4], out int y) || !int.TryParse(value[4..6], out int m) || !int.TryParse(value[6..8], out int d))
            return null;
        try { return new DateTime(y, m, d); } catch { return null; }
    }
}

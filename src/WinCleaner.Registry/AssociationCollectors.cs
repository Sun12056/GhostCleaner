using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Win32;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Registry;

/// <summary>各类"系统仍在使用"证据的采集器。任一采集器失败都不影响其它来源。</summary>
internal static class AssociationCollectors
{
    /// <summary>注册表卸载项（HKLM/HKCU，32 位与 64 位）。</summary>
    public static List<AssociationRecord> RegistryUninstall()
    {
        var list = new List<AssociationRecord>();
        try
        {
            list.AddRange(UninstallEntryReader.ToRecords(UninstallEntryReader.ReadAll()));
        }
        catch
        {
            // 忽略
        }
        return list;
    }

    /// <summary>App Paths（很多程序通过它注册，但不写卸载项）。</summary>
    public static List<AssociationRecord> AppPaths()
    {
        var list = new List<AssociationRecord>();
        string[] paths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths",
        };

        foreach (var p in paths)
        {
            foreach (var (value, name) in ReadSubKeyDefaultValues(RegistryHive.LocalMachine, p, RegistryView.Registry64))
            {
                var exe = CommandLinePathParser.ParseExecutablePath(value);
                if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.AppPaths, name));
            }
        }

        return list;
    }

    /// <summary>快捷方式（开始菜单 / 桌面 / 启动目录）。</summary>
    public static List<AssociationRecord> Shortcuts()
    {
        var list = new List<AssociationRecord>();

        var specialDirs = new (string Dir, AssociationSource Source)[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), AssociationSource.StartMenuShortcut),
            (Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), AssociationSource.StartMenuShortcut),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AssociationSource.DesktopShortcut),
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AssociationSource.DesktopShortcut),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), AssociationSource.StartupEntry),
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), AssociationSource.StartupEntry),
        };

        foreach (var (dir, source) in specialDirs)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            List<string> files = new();
            try
            {
                files.AddRange(Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories));
            }
            catch
            {
                continue;
            }

            foreach (var lnk in files)
            {
                try
                {
                    var target = LnkParser.GetTargetPath(lnk);
                    if (string.IsNullOrEmpty(target)) continue;
                    list.Add(new AssociationRecord(target!, source, System.IO.Path.GetFileNameWithoutExtension(lnk)));
                }
                catch
                {
                    // 忽略单个快捷方式
                }
            }
        }

        return list;
    }

    /// <summary>Windows 服务的 ImagePath。</summary>
    public static List<AssociationRecord> Services()
    {
        var list = new List<AssociationRecord>();
        const string servicesKey = @"SYSTEM\CurrentControlSet\Services";

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            List<string> names = new();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(servicesKey);
                if (key == null) continue;
                names.AddRange(key.GetSubKeyNames());
            }
            catch
            {
                continue;
            }

            foreach (var name in names)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = baseKey.OpenSubKey(servicesKey);
                    using var sub = key?.OpenSubKey(name);
                    var imagePath = sub?.GetValue("ImagePath") as string;
                    var exe = CommandLinePathParser.ParseExecutablePath(imagePath);
                    if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.WindowsService, name));
                }
                catch
                {
                    // 忽略单个服务
                }
            }
        }

        return list;
    }

    /// <summary>计划任务（解析 %SystemRoot%\System32\Tasks 下的任务 XML）。</summary>
    public static List<AssociationRecord> ScheduledTasks()
    {
        var list = new List<AssociationRecord>();

        var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var tasksDir = System.IO.Path.Combine(systemDir, "Tasks");
        if (!Directory.Exists(tasksDir)) return list;

        List<string> files = new();
        try
        {
            files.AddRange(Directory.EnumerateFiles(tasksDir, "*", SearchOption.AllDirectories));
        }
        catch
        {
            return list;
        }

        foreach (var file in files)
        {
            try
            {
                var xml = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(xml) || !xml.Contains("<Task", StringComparison.OrdinalIgnoreCase)) continue;

                var doc = XDocument.Parse(xml);
                var taskName = System.IO.Path.GetFileName(file);

                foreach (var element in doc.Descendants())
                {
                    if (!element.Name.LocalName.Equals("Command", StringComparison.OrdinalIgnoreCase)) continue;
                    var exe = CommandLinePathParser.ParseExecutablePath(element.Value);
                    if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.ScheduledTask, taskName));
                }
            }
            catch
            {
                // 忽略无法解析的任务文件
            }
        }

        return list;
    }

    /// <summary>当前运行进程的可执行文件路径。</summary>
    public static List<AssociationRecord> RunningProcesses()
    {
        var list = new List<AssociationRecord>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                    list.Add(new AssociationRecord(PathUtils.Normalize(path!), AssociationSource.RunningProcess, p.ProcessName));
            }
            catch
            {
                // 无权限访问进程模块
            }
        }

        return list;
    }

    /// <summary>Microsoft Store / AppX 包的安装目录。</summary>
    public static List<AssociationRecord> AppxPackages()
    {
        var list = new List<AssociationRecord>();
        const string repoSubKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var (value, name) in ReadSubKeyStringValues(hive, RegistryView.Default, repoSubKey, "PackageRootFolder"))
            {
                var dir = CommandLinePathParser.ParseDirectoryPath(value);
                if (dir != null) list.Add(new AssociationRecord(dir, AssociationSource.AppxPackage, name));
            }
        }

        return list;
    }

    /// <summary>文件关联 / 右键菜单 / COM 注册。</summary>
    public static List<AssociationRecord> FileAssociations()
    {
        var list = new List<AssociationRecord>();

        string[] classesRoots =
        {
            @"SOFTWARE\Classes",
            @"SOFTWARE\Classes\WOW6432Node\Classes",
            @"SOFTWARE\WOW6432Node\Classes",
        };

        foreach (var root in classesRoots)
        {
            foreach (var shellRoot in new[] { "*", "Directory", "Directory\\Background", "Folder", "Drive", "AllFileSystemObjects" })
            {
                foreach (var (value, name) in ReadShellCommands(RegistryHive.LocalMachine, root, shellRoot, RegistryView.Registry64))
                {
                    var exe = CommandLinePathParser.ParseExecutablePath(value);
                    if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.FileAssociation, name));
                }
            }

            foreach (var (value, name) in ReadApplicationsCommands(RegistryHive.LocalMachine, root, RegistryView.Registry64))
            {
                var exe = CommandLinePathParser.ParseExecutablePath(value);
                if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.FileAssociation, name));
            }

            foreach (var (value, name) in ReadClsidServers(RegistryHive.LocalMachine, root, RegistryView.Registry64))
            {
                var exe = CommandLinePathParser.ParseExecutablePath(value);
                if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.FileAssociation, name));
            }
        }

        return list;
    }

    /// <summary>启动项（Run / RunOnce）。</summary>
    public static List<AssociationRecord> StartupEntries()
    {
        var list = new List<AssociationRecord>();

        string[] runKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
        };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var keyPath in runKeys)
            {
                foreach (var (value, name) in ReadKeyValueStrings(hive, RegistryView.Registry64, keyPath))
                {
                    var exe = CommandLinePathParser.ParseExecutablePath(value);
                    if (exe != null) list.Add(new AssociationRecord(exe, AssociationSource.StartupEntry, name));
                }
            }
        }

        return list;
    }

    // ---------- 注册表读取辅助（内部吞异常，返回已收集结果） ----------

    private static List<(string Value, string Name)> ReadSubKeyDefaultValues(RegistryHive hive, string keyPath, RegistryView view)
    {
        var result = new List<(string, string)>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            if (key == null) return result;

            foreach (var name in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(name);
                    var value = sub?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, name));
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
        return result;
    }

    private static List<(string Value, string Name)> ReadSubKeyStringValues(RegistryHive hive, RegistryView view, string keyPath, string valueName)
    {
        var result = new List<(string, string)>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            if (key == null) return result;

            foreach (var name in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(name);
                    var value = sub?.GetValue(valueName) as string;
                    if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, name));
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
        return result;
    }

    private static List<(string Value, string Name)> ReadKeyValueStrings(RegistryHive hive, RegistryView view, string keyPath)
    {
        var result = new List<(string, string)>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            if (key == null) return result;

            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var value = key.GetValue(name) as string;
                    if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, name));
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
        return result;
    }

    private static List<(string Value, string Name)> ReadShellCommands(RegistryHive hive, string classesRoot, string shellRoot, RegistryView view)
    {
        var result = new List<(string, string)>();
        var shellPath = $"{classesRoot}\\{shellRoot}\\shell";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var shellKey = baseKey.OpenSubKey(shellPath);
            if (shellKey == null) return result;

            foreach (var verb in shellKey.GetSubKeyNames())
            {
                try
                {
                    using var verbKey = shellKey.OpenSubKey(verb);
                    using var cmdKey = verbKey?.OpenSubKey("command");
                    var value = cmdKey?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, $"{shellRoot}::{verb}"));
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
        return result;
    }

    private static List<(string Value, string Name)> ReadApplicationsCommands(RegistryHive hive, string classesRoot, RegistryView view)
    {
        var result = new List<(string, string)>();
        var appsPath = $"{classesRoot}\\Applications";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var appsKey = baseKey.OpenSubKey(appsPath);
            if (appsKey == null) return result;

            foreach (var app in appsKey.GetSubKeyNames())
            {
                try
                {
                    using var appKey = appsKey.OpenSubKey(app);
                    using var shellKey = appKey?.OpenSubKey("shell");
                    if (shellKey == null) continue;

                    foreach (var verb in shellKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var verbKey = shellKey.OpenSubKey(verb);
                            using var cmdKey = verbKey?.OpenSubKey("command");
                            var value = cmdKey?.GetValue(null) as string;
                            if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, $"{app}::{verb}"));
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
        }
        catch
        {
            // 忽略
        }
        return result;
    }

    private static List<(string Value, string Name)> ReadClsidServers(RegistryHive hive, string classesRoot, RegistryView view)
    {
        var result = new List<(string, string)>();
        var clsidPath = $"{classesRoot}\\CLSID";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var clsidKey = baseKey.OpenSubKey(clsidPath);
            if (clsidKey == null) return result;

            foreach (var clsid in clsidKey.GetSubKeyNames())
            {
                try
                {
                    using var clsidSub = clsidKey.OpenSubKey(clsid);
                    if (clsidSub == null) continue;

                    foreach (var server in new[] { "InprocServer32", "LocalServer32" })
                    {
                        using var serverKey = clsidSub.OpenSubKey(server);
                        var value = serverKey?.GetValue(null) as string;
                        if (!string.IsNullOrWhiteSpace(value)) result.Add((value!, clsid));
                    }
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
        return result;
    }
}

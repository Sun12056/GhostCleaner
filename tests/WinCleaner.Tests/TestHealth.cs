using WinCleaner.Core.Models;

namespace WinCleaner.Tests;

/// <summary>构造 <see cref="AssociationSourceHealth"/> 的测试辅助工具。</summary>
internal static class TestHealth
{
    private static readonly (string Name, AssociationSource[] Covers)[] Sources =
    {
        ("注册表卸载项", new[] { AssociationSource.RegistryUninstall }),
        ("App Paths", new[] { AssociationSource.AppPaths }),
        ("开始菜单/桌面快捷方式", new[] { AssociationSource.StartMenuShortcut, AssociationSource.DesktopShortcut }),
        ("Windows 服务", new[] { AssociationSource.WindowsService }),
        ("计划任务", new[] { AssociationSource.ScheduledTask }),
        ("正在运行的进程", new[] { AssociationSource.RunningProcess }),
        ("Microsoft Store 应用", new[] { AssociationSource.AppxPackage }),
        ("文件关联", new[] { AssociationSource.FileAssociation }),
        ("启动项", new[] { AssociationSource.StartupEntry }),
    };

    /// <summary>全部来源成功（最理想的扫描状态）。</summary>
    public static IReadOnlyList<AssociationSourceHealth> AllSuccess()
        => Build(_ => AssociationSourceStatus.Success);

    /// <summary>全部来源失败（最差情况）。</summary>
    public static IReadOnlyList<AssociationSourceHealth> AllFailed()
        => Build(_ => AssociationSourceStatus.Failed);

    /// <summary>只让指定来源失败，其它成功。</summary>
    public static IReadOnlyList<AssociationSourceHealth> OnlyFail(params AssociationSource[] failed)
        => Build(s => failed.Contains(s) ? AssociationSourceStatus.Failed : AssociationSourceStatus.Success);

    public static IReadOnlyList<AssociationSourceHealth> Build(Func<AssociationSource, AssociationSourceStatus> selector)
        => Sources.Select(s =>
        {
            var status = selector(s.Covers[0]);
            return new AssociationSourceHealth
            {
                SourceName = s.Name,
                CoveredSources = s.Covers,
                Status = status,
                ItemCount = status == AssociationSourceStatus.Success ? 64 : 0,
                ErrorMessage = status == AssociationSourceStatus.Success ? null : "模拟采集失败",
            };
        }).ToList();
}

/// <summary>构造 <see cref="SoftwareDirectoryInfo"/> 的测试辅助工具。</summary>
internal static class TestInfo
{
    public static SoftwareDirectoryInfo Create(
        string path,
        int executables = 2,
        int libraries = 4,
        bool hasUninstaller = true,
        int fileCount = 120,
        long sizeBytes = 80L * 1024 * 1024,
        DateTime? lastModified = null,
        bool likelySoftware = true,
        bool hasUserData = false,
        params string[] topLevelDirectories)
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = path,
            Name = System.IO.Path.GetFileName(path),
            ProductName = "TestApp",
            Publisher = "TestVendor",
            Version = "1.2.3",
            ExecutableCount = executables,
            LibraryCount = libraries,
            FileCount = fileCount,
            TotalSizeBytes = sizeBytes,
            HasUninstaller = hasUninstaller,
            UninstallerPath = hasUninstaller ? System.IO.Path.Combine(path, "unins000.exe") : null,
            HasVersionResource = false,
            LastModified = lastModified ?? DateTime.Now.AddDays(-400),
            IsLikelySoftwareDirectory = likelySoftware,
            HasUserData = hasUserData,
        };

        foreach (var dir in topLevelDirectories) info.TopLevelDirectoryNames.Add(dir);
        foreach (var dir in topLevelDirectories) info.AllDirectoryNames.Add(dir);

        return info;
    }
}

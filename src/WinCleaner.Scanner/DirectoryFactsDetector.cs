using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>
/// 目录性质识别：区分"被安装出来的软件"与"天然没有系统关联"的目录
/// （绿色软件 / 游戏 / 开发项目 / 用户数据）。
///
/// 这是 v0.1 降低误判的第一道闸门：这些目录即使一条系统关联都没有，
/// 也不能被当成软件残留。
/// </summary>
public static class DirectoryFactsDetector
{
    /// <summary>绿色软件的结构性体积上限（超过该体积通常不是"解压即用"的小工具）。</summary>
    public const long PortableMaxSizeBytes = 500L * 1024 * 1024;

    /// <summary>绿色软件的可执行文件数量上限。</summary>
    public const int PortableMaxExecutables = 3;

    private static readonly string[] ConfigLikeExtensions =
    {
        ".ini", ".cfg", ".conf", ".config", ".json", ".xml", ".yml", ".yaml", ".toml", ".properties",
    };

    public static DirectoryFacts Detect(SoftwareDirectoryInfo info)
    {
        var path = info.Path;
        var segments = Heuristics.PathSegments(path);
        var dirNames = new HashSet<string>(
            info.TopLevelDirectoryNames.Concat(info.AllDirectoryNames).Select(n => n.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(
            info.TopLevelFileNames.Concat(info.FileNameSamples).Select(n => n.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var tags = new List<string>();

        // —— 游戏 ——
        var gameHits = segments.Where(Heuristics.MatchesGameIndicator).Distinct().ToList();
        gameHits.AddRange(dirNames.Where(Heuristics.MatchesGameIndicator));
        var gameFiles = fileNames.Where(Heuristics.IsGameFile).ToList();
        bool isGame = gameHits.Count > 0 || gameFiles.Count > 0;
        foreach (var hit in gameHits.Distinct().Take(3)) tags.Add("游戏特征：" + hit);
        foreach (var hit in gameFiles.Take(2)) tags.Add("游戏运行库：" + hit);

        // —— 开发项目 / 工具链 ——
        var devFiles = fileNames.Where(Heuristics.MatchesDevFileIndicator).ToList();
        var devDirs = dirNames.Where(Heuristics.MatchesDevDirectoryIndicator).ToList();
        bool isDev = devFiles.Count > 0 || devDirs.Count > 0;
        foreach (var hit in devFiles.Take(3)) tags.Add("开发文件：" + hit);
        foreach (var hit in devDirs.Take(3)) tags.Add("开发目录：" + hit);

        // —— 绿色 / Portable ——
        bool portableByName =
            Heuristics.MatchesPortableIndicator(info.Name) ||
            segments.Any(Heuristics.MatchesPortableIndicator) ||
            fileNames.Any(Heuristics.MatchesPortableIndicator) ||
            dirNames.Any(Heuristics.MatchesPortableIndicator);
        if (portableByName) tags.Add("命名含绿色/免安装特征");

        bool hasInstallerArtifact = fileNames.Any(Heuristics.IsInstallerFile);
        bool configNextToExecutable =
            info.TopLevelFileNames.Any(f => ConfigLikeExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())) ||
            info.TopLevelDirectoryNames.Any(d => Heuristics.IsConfigurationDirectory(d) || d.Equals("data", StringComparison.OrdinalIgnoreCase));

        bool portableByStructure =
            !info.HasUninstaller &&
            !hasInstallerArtifact &&
            configNextToExecutable &&
            info.ExecutableCount > 0 &&
            info.ExecutableCount <= PortableMaxExecutables &&
            info.TotalSizeBytes < PortableMaxSizeBytes;

        if (portableByStructure) tags.Add("配置文件与程序同目录，且无安装/卸载程序");

        bool isPortable = portableByName || portableByStructure;

        // —— 用户数据 ——
        bool hasSaveData = dirNames.Any(Heuristics.IsSaveDataDirectory);
        bool hasProfileData = dirNames.Any(Heuristics.IsProfileDirectory);
        bool hasDocuments = dirNames.Any(Heuristics.IsPersonalDataDirectory);
        bool hasBackupData = dirNames.Any(Heuristics.IsBackupDirectory);
        bool hasConfiguration = dirNames.Any(Heuristics.IsConfigurationDirectory);

        foreach (var d in dirNames.Where(Heuristics.IsSaveDataDirectory).Take(2)) tags.Add("存档目录：" + d);
        foreach (var d in dirNames.Where(Heuristics.IsProfileDirectory).Take(2)) tags.Add("用户配置目录：" + d);
        foreach (var d in dirNames.Where(Heuristics.IsPersonalDataDirectory).Take(2)) tags.Add("个人资料目录：" + d);
        foreach (var d in dirNames.Where(Heuristics.IsBackupDirectory).Take(2)) tags.Add("备份目录：" + d);

        bool hasUserData = info.HasUserData || hasSaveData || hasProfileData || hasDocuments || hasBackupData;

        // —— 路径安全 ——
        bool isCritical = PathUtils.IsCriticalPath(path);
        var systemDriveRoot = PathUtils.GetDriveRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        bool onSystemDrive = systemDriveRoot != null &&
                             string.Equals(PathUtils.GetDriveRoot(path), systemDriveRoot, StringComparison.OrdinalIgnoreCase);

        // 系统安装根：C:\Program Files / C:\Program Files (x86) / C:\Windows 下的一级目录
        bool isSystemInstallRoot = segments.Count >= 2
                                   && (segments[1] is "program files" or "program files (x86)" or "windows");

        string? primaryCategory =
            isDev ? "开发项目"
            : isGame ? "游戏"
            : hasSaveData || hasProfileData || hasDocuments || hasBackupData ? "用户数据"
            : isPortable ? "绿色软件"
            : null;

        var protections = new List<string>();
        if (isCritical) protections.Add("位于系统关键路径");
        if (isSystemInstallRoot) protections.Add("位于系统安装目录（Program Files / Windows）");
        if (isGame) protections.Add("疑似游戏目录");
        if (isDev) protections.Add("疑似开发项目/工具链");
        if (hasSaveData) protections.Add("包含存档数据");
        if (hasProfileData) protections.Add("包含用户配置/个人资料");
        if (hasDocuments) protections.Add("包含个人文档/媒体");
        if (hasBackupData) protections.Add("包含备份数据");

        return new DirectoryFacts
        {
            IsPortableSoftware = isPortable,
            IsGameDirectory = isGame,
            IsDevelopmentProject = isDev,
            HasSaveData = hasSaveData,
            HasProfileData = hasProfileData,
            HasDocuments = hasDocuments,
            HasBackupData = hasBackupData,
            HasConfiguration = hasConfiguration,
            HasUserData = hasUserData,
            HasInstallerArtifact = hasInstallerArtifact,
            IsCriticalPath = isCritical,
            IsOnSystemDrive = onSystemDrive,
            IsSystemInstallRoot = isSystemInstallRoot,
            MatchedTags = tags,
            PrimaryCategory = primaryCategory,
            IsProtected = protections.Count > 0,
            ProtectionReasons = protections,
        };
    }
}

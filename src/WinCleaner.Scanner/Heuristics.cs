namespace WinCleaner.Scanner;

/// <summary>"这个文件/目录看起来像软件吗"的知识库（全部为保守启发式）。</summary>
public static class Heuristics
{
    /// <summary>卸载程序命名特征。</summary>
    public static readonly string[] UninstallerPatterns =
    {
        "unins",        // unins000.exe（Inno Setup）
        "uninst",       // uninst.exe（NSIS / 其它）
        "uninstall",
        "uninstaller",
        "unwise",       // Wise Installer
        "uninstal",
    };

    /// <summary>被视为"资源目录"的子目录名。</summary>
    public static readonly string[] ResourceDirectoryNames =
    {
        "bin", "resources", "res", "lang", "languages", "locales", "locale",
        "plugins", "plugin", "modules", "lib", "libs", "help", "docs", "skins", "themes",
    };

    /// <summary>被视为"可能重要的用户数据"的目录名。</summary>
    public static readonly string[] UserDataDirectoryNames =
    {
        "config", "configs", "configuration", "settings", "prefs", "preferences",
        "save", "saves", "saved", "saveslot", "saveslots", "autosave",
        "profile", "profiles", "userdata", "user data", "users", "accounts",
        "data", "documents", "projects", "workspace", "backup", "backups", "cloud",
        "projects", "工程", "项目", "存档", "配置",
    };

    /// <summary>被视为"可能重要的用户数据"的扩展名。</summary>
    public static readonly string[] UserDataExtensions =
    {
        ".ini", ".cfg", ".conf", ".config", ".properties",
        ".db", ".sqlite", ".sqlite3", ".mdb", ".dat", ".sav", ".save", ".datx",
        ".json", ".xml", ".yml", ".yaml", ".toml",
        ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt",
        ".psd", ".aep", ".prproj", ".sketch", ".blend",
    };

    /// <summary>媒体/资料文件扩展名（占比过高说明这是媒体库而不是软件目录）。</summary>
    public static readonly string[] MediaExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".rmvb",
        ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic",
    };

    /// <summary>安装包 / 归档（出现说明可能是下载目录而非安装目录）。</summary>
    public static readonly string[] ArchiveExtensions = { ".iso", ".zip", ".rar", ".7z", ".tar", ".gz", ".msi" };

    /// <summary>容器目录名：这些目录本身是"软件集散地"，不应作为候选删除对象。</summary>
    public static readonly string[] ContainerDirectoryNames =
    {
        "program files", "program files (x86)", "software", "apps", "games", "tools",
        "greensoft", "portableapps", "programs", "我的软件", "软件",
    };

    /// <summary>始终跳过的目录名。</summary>
    public static readonly string[] AlwaysSkipDirectoryNames =
    {
        "$recycle.bin", "system volume information", "recovery", "windows",
        "winsxs", "programdata", "appdata", "perflogs", "onedrivetemp",
    };

    // ------------------------------------------------------------------
    // v0.1 新增：降低误判的特征库
    // ------------------------------------------------------------------

    /// <summary>绿色 / Portable 软件命名特征（目录名或文件名命中）。</summary>
    public static readonly string[] PortableIndicators =
    {
        "portable", "绿色", "绿色版", "免安装", "standalone", "launcher",
        "portableapps", "不需要安装", "解压即用", "单文件",
    };

    /// <summary>游戏平台 / 游戏库特征（路径片段或目录名命中）。</summary>
    public static readonly string[] GameDirectoryIndicators =
    {
        // 只保留明确的平台/发行商名称；"games"/"common" 这类通用容器名会导致
        // D:\Games\OldApp 这种普通软件目录被误判成游戏。
        "steam", "steamapps", "steamlibrary", "epic", "epicgames",
        "battle.net", "battlenet", "ubisoft", "uplay", "origin", "ea games",
        "gog", "gog galaxy", "galaxy", "xbox", "xboxgames",
        "rockstar games", "bethesda", "minecraft", "riot games",
    };

    /// <summary>游戏运行库 / 引擎文件名（命中即高度可疑为游戏目录）。</summary>
    public static readonly string[] GameFileIndicators =
    {
        "steam_api.dll", "steam_api64.dll", "steamclient.dll", "gameoverlayui.dll",
        "unityplayer.dll", "galaxy.dll", "galaxy64.dll", "eossdk-win64-shipping.dll",
        "eossdk.dll", "uplay_r1.dll", "goggalaxy.dll", "d3dx9_43.dll",
    };

    /// <summary>开发项目 / 工具链的目录名特征。</summary>
    public static readonly string[] DevDirectoryIndicators =
    {
        ".git", ".svn", ".hg", "node_modules", "venv", ".venv", "env",
        "packages", "obj", "target", "__pycache__", ".idea", ".vscode",
        ".gradle", ".mvn", "vendor", "site-packages", "sdk", "ndk",
        "jdk", "jre", "android sdk", "conda", "miniconda", "anaconda",
    };

    /// <summary>开发项目 / 工具链的文件名特征。</summary>
    public static readonly string[] DevFileIndicators =
    {
        "package.json", "package-lock.json", "pnpm-lock.yaml", "yarn.lock",
        "tsconfig.json", "pom.xml", "build.gradle", "build.gradle.kts",
        "settings.gradle", "requirements.txt", "pyproject.toml", "setup.py",
        "go.mod", "go.sum", "cargo.toml", "cmakelists.txt", "makefile",
        "dockerfile", "docker-compose.yml", "composer.json", "gemfile",
        "*.csproj", "*.sln", "*.fsproj", "*.vbproj", "*.vcxproj",
        "manage.py", "vcpkg.json", "conanfile.txt",
    };

    /// <summary>强保护的用户数据目录名（存档 / 配置 / 备份 / 项目）。</summary>
    public static readonly string[] StrongUserDataDirectoryNames =
    {
        "save", "saves", "saved", "saveslot", "saveslots", "autosave", "存档",
        "profile", "profiles", "userdata", "user data",
        "projects", "project", "workspace", "工程", "项目",
    };

    /// <summary>明显的用户资料目录名（比普通 config 更强的保护信号）。</summary>
    public static readonly string[] PersonalDataDirectoryNames =
    {
        "documents", "downloads", "pictures", "videos", "music", "我的文档",
        "我的图片", "我的音乐", "我的视频", "photos", "desktop",
    };

    /// <summary>备份类目录名。</summary>
    public static readonly string[] BackupDirectoryNames =
    {
        "backup", "backups", "备份", "snapshot", "snapshots", "archive", "archives",
    };

    /// <summary>明显配置目录名（配置与 exe 同目录往往意味着绿色软件）。</summary>
    public static readonly string[] ConfigurationDirectoryNames =
    {
        "config", "configs", "configuration", "settings", "prefs", "preferences", "配置",
    };

    /// <summary>安装程序特征（说明这是安装包目录，而不是已安装目录）。</summary>
    public static readonly string[] InstallerFileNamePatterns =
    {
        "setup", "install", "installer", "安装",
    };

    public static bool IsUninstaller(string fileName)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        return UninstallerPatterns.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase) || name.Contains(p));
    }

    public static bool IsResourceDirectory(string directoryName)
        => ResourceDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    public static bool IsUserDataDirectory(string directoryName)
        => UserDataDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    public static bool IsUserDataFile(string extension)
        => UserDataExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsMediaFile(string extension)
        => MediaExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsArchiveFile(string extension)
        => ArchiveExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsContainerDirectory(string directoryName)
        => ContainerDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    public static bool IsAlwaysSkipDirectory(string directoryName)
        => AlwaysSkipDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    // ------------------------------------------------------------------
    // v0.1 新增判定方法
    // ------------------------------------------------------------------

    /// <summary>是否为强保护的用户数据目录名（存档 / 配置 / 项目 / 工作区）。</summary>
    public static bool IsStrongUserDataDirectory(string directoryName)
        => StrongUserDataDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    /// <summary>是否为个人资料目录名（文档 / 图片 / 视频 / 下载 …）。</summary>
    public static bool IsPersonalDataDirectory(string directoryName)
        => PersonalDataDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    /// <summary>是否为备份目录名。</summary>
    public static bool IsBackupDirectory(string directoryName)
        => BackupDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    /// <summary>是否为明显的配置目录名。</summary>
    public static bool IsConfigurationDirectory(string directoryName)
        => ConfigurationDirectoryNames.Contains(directoryName.Trim().ToLowerInvariant());

    /// <summary>是否为存档类目录名。</summary>
    public static bool IsSaveDataDirectory(string directoryName)
    {
        var n = directoryName.Trim().ToLowerInvariant();
        return n is "save" or "saves" or "saved" or "saveslot" or "saveslots" or "autosave" or "存档";
    }

    /// <summary>是否为 profile / userdata 类目录名。</summary>
    public static bool IsProfileDirectory(string directoryName)
    {
        var n = directoryName.Trim().ToLowerInvariant();
        return n is "profile" or "profiles" or "userdata" or "user data" or "accounts";
    }

    /// <summary>是否命中绿色 / Portable 命名特征（按"包含"匹配，不区分大小写）。</summary>
    public static bool MatchesPortableIndicator(string name)
        => PortableIndicators.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>是否命中游戏平台命名特征（按目录片段精确匹配）。</summary>
    public static bool MatchesGameIndicator(string segment)
        => GameDirectoryIndicators.Contains(segment.Trim().ToLowerInvariant());

    /// <summary>是否命中游戏运行库 / 引擎文件名。</summary>
    public static bool IsGameFile(string fileName)
        => GameFileIndicators.Contains(fileName.Trim().ToLowerInvariant());

    /// <summary>是否命中开发项目目录名。</summary>
    public static bool MatchesDevDirectoryIndicator(string segment)
        => DevDirectoryIndicators.Contains(segment.Trim().ToLowerInvariant());

    /// <summary>是否命中开发项目文件名（支持 *.ext 通配）。</summary>
    public static bool MatchesDevFileIndicator(string fileName)
    {
        var name = fileName.Trim().ToLowerInvariant();

        foreach (var pattern in DevFileIndicators)
        {
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                if (name.EndsWith(pattern[1..], StringComparison.Ordinal)) return true;
            }
            else if (name.Equals(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>是否为安装程序文件（setup.exe / install.exe / *.msi）。</summary>
    public static bool IsInstallerFile(string fileName)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".msi" or ".msix" or ".appx") return true;
        return InstallerFileNamePatterns.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>拆出路径的目录片段（小写）。</summary>
    public static IReadOnlyList<string> PathSegments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();

        return path.Replace('/', '\\')
                   .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(s => s.ToLowerInvariant())
                   .ToArray();
    }
}

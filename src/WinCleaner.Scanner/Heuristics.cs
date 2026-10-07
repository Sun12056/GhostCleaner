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
}

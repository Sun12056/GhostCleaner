namespace WinCleaner.Core.Models;

/// <summary>对一个候选目录做文件系统层面的度量与"是否像软件安装目录"的打分结果。</summary>
public sealed class SoftwareDirectoryInfo
{
    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public long TotalSizeBytes { get; set; }

    public int FileCount { get; set; }

    public int DirectoryCount { get; set; }

    public int ExecutableCount { get; set; }

    public int LibraryCount { get; set; }

    public bool HasUninstaller { get; set; }

    public string? UninstallerPath { get; set; }

    /// <summary>主要可执行文件（按体积降序，最多 5 个）。</summary>
    public List<string> ExecutablePaths { get; } = new();

    /// <summary>典型资源子目录：bin / resources / lang / locales 等。</summary>
    public List<string> ResourceDirectories { get; } = new();

    public bool HasVersionResource { get; set; }

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string? ProductName { get; set; }

    public DateTime LastModified { get; set; }

    public DateTime Created { get; set; }

    /// <summary>是否包含可能重要的用户数据（配置/存档/数据库/文档）。</summary>
    public bool HasUserData { get; set; }

    public List<string> UserDataSamples { get; } = new();

    /// <summary>软件特征总分（0~100+）。</summary>
    public int SoftwareScore { get; set; }

    /// <summary>打分明细，用于 UI 展示判定原因。</summary>
    public List<string> ScoreDetails { get; } = new();

    public bool IsLikelySoftwareDirectory { get; set; }

    public bool HasReparsePoint { get; set; }
}

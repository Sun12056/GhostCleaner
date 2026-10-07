namespace WinCleaner.Core.Models;

/// <summary>
/// 目录性质判定结果：它到底是"被安装出来的软件"、绿色软件、游戏、开发项目，还是用户数据目录。
/// 这是 v0.1 降低误判的核心 —— 这些目录天然就可能没有系统关联。
/// </summary>
public sealed class DirectoryFacts
{
    /// <summary>疑似绿色 / Portable 软件（免安装、解压即用）。</summary>
    public bool IsPortableSoftware { get; init; }

    /// <summary>疑似游戏目录（Steam / Epic / Ubisoft / GOG …）。</summary>
    public bool IsGameDirectory { get; init; }

    /// <summary>疑似开发项目 / 工具链目录。</summary>
    public bool IsDevelopmentProject { get; init; }

    public bool HasSaveData { get; init; }
    public bool HasProfileData { get; init; }
    public bool HasDocuments { get; init; }
    public bool HasBackupData { get; init; }
    public bool HasConfiguration { get; init; }
    public bool HasUserData { get; init; }

    /// <summary>目录内存在安装程序（setup/install/msi），说明它是"安装包目录"而非安装目录。</summary>
    public bool HasInstallerArtifact { get; init; }

    public bool IsCriticalPath { get; init; }
    public bool IsOnSystemDrive { get; init; }

    /// <summary>是否位于系统安装根（Program Files / Program Files (x86) / Windows）。</summary>
    public bool IsSystemInstallRoot { get; init; }

    /// <summary>命中的特征标签（用于 UI 解释）。</summary>
    public IReadOnlyList<string> MatchedTags { get; init; } = Array.Empty<string>();

    /// <summary>主要分类：游戏 / 开发项目 / 绿色软件 / 用户数据 / null。</summary>
    public string? PrimaryCategory { get; init; }

    /// <summary>是否受保护（不应该被轻易判定为残留）。</summary>
    public bool IsProtected { get; init; }

    /// <summary>受保护的原因。</summary>
    public IReadOnlyList<string> ProtectionReasons { get; init; } = Array.Empty<string>();

    public string? CategoryText => PrimaryCategory;
}

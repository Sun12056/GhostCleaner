namespace WinCleaner.Core.Models;

/// <summary>扫描参数。</summary>
public sealed class ScanOptions
{
    /// <summary>扫描根路径（盘符或目录）。</summary>
    public IReadOnlyList<string> RootPaths { get; init; } = Array.Empty<string>();

    /// <summary>从根路径向下探测的最大层级（默认 2：D:\ 与 D:\Program Files\xxx 两层）。</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>是否包含隐藏目录（关闭时不会漏掉 $Recycle.Bin 这类系统目录，但要跳过它们）。</summary>
    public bool IncludeHiddenDirectories { get; init; }

    /// <summary>单个目录内最多统计多少文件，防止超大目录拖慢扫描。</summary>
    public int MaxFileCountPerDirectory { get; init; } = 20_000;

    /// <summary>判定为"像软件目录"的最低分值。</summary>
    public int MinSoftwareScore { get; init; } = 50;

    /// <summary>是否跳过符号链接/挂载点（避免循环与误判）。</summary>
    public bool SkipReparsePoints { get; init; } = true;

    /// <summary>排除路径（白名单，精确路径前缀匹配）。</summary>
    public IReadOnlyList<string> ExcludedPaths { get; init; } = Array.Empty<string>();

    /// <summary>保留关键词（路径任意层级包含即排除，例如 SteamLibrary、工作、项目）。</summary>
    public IReadOnlyList<string> KeepKeywords { get; init; } = Array.Empty<string>();

    /// <summary>额外扫描的常见安装目录名（相对盘符根）。</summary>
    public static readonly string[] DefaultInstallFolderNames =
    {
        "Program Files",
        "Program Files (x86)",
        "Software",
        "Apps",
        "Games",
        "Tools",
        "GreenSoft",
        "PortableApps",
        "Programs",
    };
}

/// <summary>扫描进度回调数据。</summary>
public sealed class ScanProgress
{
    public string CurrentPath { get; init; } = string.Empty;

    public int ScannedDirectories { get; init; }

    public int CandidateCount { get; init; }

    public int OrphanCount { get; init; }

    public long ScannedBytes { get; init; }

    public string Message { get; init; } = string.Empty;
}

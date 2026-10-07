using WinCleaner.Core.Models;

namespace WinCleaner.Core.Interfaces;

/// <summary>单个目录的文件系统层面探测器。</summary>
public interface ISoftwareDirectoryInspector
{
    Task<SoftwareDirectoryInfo?> InspectAsync(string directoryPath, ScanOptions options, CancellationToken cancellationToken = default);
}

/// <summary>孤儿软件扫描引擎。</summary>
public interface IOrphanScanner
{
    Task<IReadOnlyList<OrphanItem>> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

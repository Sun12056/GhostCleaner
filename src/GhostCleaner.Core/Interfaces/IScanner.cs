using GhostCleaner.Core.Models;

namespace GhostCleaner.Core.Interfaces;

/// <summary>单个目录的文件系统层面探测器。</summary>
public interface ISoftwareDirectoryInspector
{
    Task<SoftwareDirectoryInfo?> InspectAsync(string directoryPath, ScanOptions options, CancellationToken cancellationToken = default);
}

/// <summary>孤儿软件扫描引擎。</summary>
public interface IOrphanScanner
{
    /// <summary>
    /// 执行扫描，返回 <see cref="ScanOutcome"/>。
    ///
    /// <b>这是推荐入口</b>：结果条目与采集源健康度、置信度上限同批产生，
    /// 消费方无需（也不应）再去别处取健康度，也不会拿到"未构建"的空索引。
    /// </summary>
    Task<ScanOutcome> ScanWithOutcomeAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 兼容入口：只返回条目。默认实现转发到 <see cref="ScanWithOutcomeAsync"/>。
    /// 新代码请用 <see cref="ScanWithOutcomeAsync"/> —— 只拿 Items 会丢失健康度，
    /// 而健康度是解释置信度所必需的。
    /// </summary>
    async Task<IReadOnlyList<OrphanItem>> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => (await ScanWithOutcomeAsync(options, progress, cancellationToken).ConfigureAwait(false)).Items;
}

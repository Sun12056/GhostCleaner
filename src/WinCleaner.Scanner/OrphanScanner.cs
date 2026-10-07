using System.Collections.Concurrent;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>
/// 孤儿软件扫描引擎。
///
/// 流程：
/// 1. Discover Candidates → 2. Filter Critical/Excluded → 3. File Evidence
/// → 4. Association Evidence → 5. Usage Evidence → 6. User Data Evidence
/// → 7. Detect Portable/Game/Dev → 8. Check Source Health
/// → 9. Confidence → 10. Risk → 11. Recommendation → 12. Explanation → 13. Result
///
/// 扫描器<b>只输出判断与建议</b>，不执行任何删除；删除由 CleanupService 在人工确认后完成。
/// </summary>
public sealed class OrphanScanner : IOrphanScanner
{
    private readonly ISoftwareDirectoryInspector _inspector;
    private readonly IAssociationIndex _associationIndex;

    public OrphanScanner(ISoftwareDirectoryInspector inspector, IAssociationIndex associationIndex)
    {
        _inspector = inspector;
        _associationIndex = associationIndex;
    }

    public async Task<IReadOnlyList<OrphanItem>> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var roots = options.RootPaths
            .Select(PathUtils.Normalize)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (roots.Count == 0) return Array.Empty<OrphanItem>();

        // 1) 建立"系统仍在使用"的多源索引（同时记录每个来源的成功/失败状态）
        progress?.Report(new ScanProgress { Message = "正在建立系统关联索引…" });
        await _associationIndex.BuildAsync(
            new Progress<string>(s => progress?.Report(new ScanProgress { Message = s })),
            cancellationToken).ConfigureAwait(false);

        var health = _associationIndex.SourceHealth;
        var unhealthy = health.Where(h => h.Status != AssociationSourceStatus.Success).ToList();

        progress?.Report(new ScanProgress
        {
            Message = unhealthy.Count == 0
                ? $"关联索引建立完成：{health.Count} 个来源全部成功，共采集 {_associationIndex.Count} 条系统引用"
                : $"关联索引建立完成：{health.Count - unhealthy.Count}/{health.Count} 个来源成功，"
                  + $"共采集 {_associationIndex.Count} 条系统引用（{unhealthy.Count} 个来源未完成，结果可能不完整）",
        });

        // 2) 枚举候选目录（已过滤系统关键路径 / 白名单 / 隐藏与重解析点）
        progress?.Report(new ScanProgress { Message = "正在枚举候选目录…" });
        var candidates = await Task.Run(
            () => ScanTargetResolver.Resolve(options, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new ScanProgress
        {
            Message = $"发现 {candidates.Count} 个候选目录，开始逐个分析…",
            ScannedDirectories = 0,
        });

        // 3) 并行度量 + 证据收集 + 置信度/风险/建议
        var results = new ConcurrentBag<OrphanItem>();
        int processed = 0;
        long scannedBytes = 0;

        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
                CancellationToken = cancellationToken,
            },
            async (dir, token) =>
            {
                var info = await _inspector.InspectAsync(dir, options, token).ConfigureAwait(false);
                if (info == null || !info.IsLikelySoftwareDirectory) return;

                var hits = _associationIndex.FindHits(dir);
                var item = RiskEvaluator.Evaluate(info, hits, options, health);
                results.Add(item);

                var count = Interlocked.Increment(ref processed);
                Interlocked.Add(ref scannedBytes, info.TotalSizeBytes);

                if (count % 10 == 0 || count == candidates.Count)
                {
                    progress?.Report(new ScanProgress
                    {
                        CurrentPath = dir,
                        ScannedDirectories = count,
                        CandidateCount = candidates.Count,
                        OrphanCount = results.Count,
                        ScannedBytes = Interlocked.Read(ref scannedBytes),
                        Message = $"已分析 {count}/{candidates.Count} 个目录",
                    });
                }
            }).ConfigureAwait(false);

        // 4) 排序：先看置信度（越像残留越靠前），再看体积
        var ordered = results
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenByDescending(x => x.SizeBytes)
            .ToList();

        var highProbability = ordered.Count(x => x.ConfidenceLevel >= OrphanConfidenceLevel.HighProbability);
        var protectedCount = ordered.Count(x => x.IsProtected || x.IsExcluded);

        progress?.Report(new ScanProgress
        {
            ScannedDirectories = processed,
            CandidateCount = candidates.Count,
            OrphanCount = ordered.Count,
            ScannedBytes = Interlocked.Read(ref scannedBytes),
            Message = $"扫描完成：共分析 {processed} 个目录，发现 {ordered.Count} 个疑似软件残留"
                      + $"（其中 {highProbability} 个为高概率残留，{protectedCount} 个受保护/已排除）"
                      + (unhealthy.Count > 0 ? "；注意：部分关联来源未完成检查，结果可能不完整" : string.Empty),
        });

        return ordered;
    }
}

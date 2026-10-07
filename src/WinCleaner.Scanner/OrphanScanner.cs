using System.Collections.Concurrent;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>孤儿软件扫描引擎：建立关联索引 → 枚举候选目录 → 逐个度量 → 判定孤儿 → 风险评级。</summary>
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

        // 1) 建立"系统仍在使用"的多源索引
        progress?.Report(new ScanProgress { Message = "正在建立系统关联索引…" });
        await _associationIndex.BuildAsync(
            new Progress<string>(s => progress?.Report(new ScanProgress { Message = s })),
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new ScanProgress
        {
            Message = $"关联索引建立完成，共采集 {_associationIndex.Count} 条系统引用",
        });

        // 2) 枚举候选目录
        progress?.Report(new ScanProgress { Message = "正在枚举候选目录…" });
        var candidates = await Task.Run(
            () => ScanTargetResolver.Resolve(options, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new ScanProgress
        {
            Message = $"发现 {candidates.Count} 个候选目录，开始逐个分析…",
            ScannedDirectories = 0,
        });

        // 3) 并行度量 + 关联判定
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
                var item = RiskEvaluator.Evaluate(info, hits, options, _associationIndex.Count);
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

        var ordered = results
            .OrderBy(x => x.Risk == RiskLevel.High ? 0 : x.Risk == RiskLevel.Medium ? 1 : 2)
            .ThenByDescending(x => x.SizeBytes)
            .ToList();

        progress?.Report(new ScanProgress
        {
            ScannedDirectories = processed,
            CandidateCount = candidates.Count,
            OrphanCount = ordered.Count,
            ScannedBytes = Interlocked.Read(ref scannedBytes),
            Message = $"扫描完成：共分析 {processed} 个目录，发现 {ordered.Count} 个疑似孤儿软件残留",
        });

        return ordered;
    }
}

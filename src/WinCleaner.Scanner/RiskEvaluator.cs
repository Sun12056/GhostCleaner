using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>
/// 评估引擎总入口。
///
/// 流程：Candidate → Evidence Collection → Orphan Confidence → Cleanup Risk → Recommendation。
///
/// 关键原则（v0.1）：
/// 1. <b>没有系统关联 ≠ 软件残留</b>：缺少关联只有在对应扫描源成功时才是证据；
/// 2. <b>Confidence ≠ Risk</b>：前者是"多大把握认为它是残留"，后者是"清理它有多危险"；
/// 3. <b>扫描失败绝不能被当成"没有引用"</b>，只会产生 Warning 并压低置信度；
/// 4. Scanner 只输出 <see cref="CleanupRecommendation"/>，永远不输出"删除"。
/// </summary>
public static class RiskEvaluator
{
    public static OrphanItem Evaluate(
        SoftwareDirectoryInfo info,
        IReadOnlyList<AssociationHit> hits,
        ScanOptions options,
        IReadOnlyList<AssociationSourceHealth> sourceHealth,
        IEnumerable<IPathExclusionProvider>? exclusionProviders = null,
        EvidenceWeights? weights = null)
    {
        var w = weights ?? EvidenceWeights.Default;
        hits ??= Array.Empty<AssociationHit>();
        sourceHealth ??= Array.Empty<AssociationSourceHealth>();

        // 1) 目录性质（绿色软件 / 游戏 / 开发项目 / 用户数据）
        var facts = DirectoryFactsDetector.Detect(info);

        // 2) 收集证据
        var collection = EvidenceCollector.Collect(info, facts, hits, sourceHealth, w);

        // 3) 置信度（与风险完全独立）
        var confidenceScore = OrphanConfidenceEvaluator.Compute(collection, sourceHealth, w);
        var confidenceLevel = OrphanConfidenceThresholds.FromScore(confidenceScore);

        // 4) 清理风险
        var risk = EvaluateCleanupRisk(info, facts, hits, sourceHealth, w);

        // 5) 白名单 / 排除
        var providers = BuildProviders(options, exclusionProviders);
        string? excludeReason = null;
        bool isExcluded = false;
        foreach (var provider in providers)
        {
            if (provider.IsExcluded(info.Path, out var r))
            {
                isExcluded = true;
                excludeReason = r;
                break;
            }
        }

        // 6) 建议（永远不是"删除"）
        bool isRunning = hits.Any(h => h.Source == AssociationSource.RunningProcess);
        var recommendation = Recommend(confidenceLevel, risk.Level, facts, isExcluded, isRunning);

        // 7) 人类可读解释
        var reasons = BuildReasons(collection, risk.Reasons);

        var displayName = !string.IsNullOrWhiteSpace(info.ProductName)
            ? info.ProductName!
            : !string.IsNullOrWhiteSpace(info.Name)
                ? info.Name
                : System.IO.Path.GetFileName(info.Path);

        return new OrphanItem
        {
            Path = info.Path,
            DisplayName = displayName,
            Version = info.Version,
            Publisher = info.Publisher,
            ProductName = info.ProductName,
            SizeBytes = info.TotalSizeBytes,
            FileCount = info.FileCount,
            DirectoryCount = info.DirectoryCount,
            LastModified = info.LastModified,
            MainExecutables = info.ExecutablePaths,
            HasUninstaller = info.HasUninstaller,
            UninstallerPath = info.UninstallerPath,
            HasUserData = facts.HasUserData,
            UserDataSamples = info.UserDataSamples,
            IsRunning = hits.Any(h => h.Source == AssociationSource.RunningProcess),
            SoftwareScore = info.SoftwareScore,
            IsLikelySoftwareDirectory = info.IsLikelySoftwareDirectory,

            ConfidenceScore = confidenceScore,
            ConfidenceLevel = confidenceLevel,
            Risk = risk.Level,
            RiskScore = risk.Score,
            Recommendation = recommendation,
            Evidence = collection.Evidence,
            Warnings = collection.Warnings,
            IsProtected = facts.IsProtected,
            ProtectionReason = facts.ProtectionReasons.Count == 0
                ? null
                : string.Join("、", facts.ProtectionReasons),
            DirectoryCategory = facts.PrimaryCategory,

            Reasons = reasons,
            Associations = hits.ToList(),
            IsExcluded = isExcluded,
            ExcludeReason = excludeReason,
        };
    }

    /// <summary>
    /// 清理风险：回答"如果清理它，风险有多大"。
    /// 注意与置信度无关 —— 一个很像残留的目录也可能因为包含用户数据而风险很高。
    /// </summary>
    public static CleanupRisk EvaluateCleanupRisk(
        SoftwareDirectoryInfo info,
        DirectoryFacts facts,
        IReadOnlyList<AssociationHit> hits,
        IReadOnlyList<AssociationSourceHealth> sourceHealth,
        EvidenceWeights? weights = null)
    {
        var w = weights ?? EvidenceWeights.Default;
        var reasons = new List<string>();
        int score = 0;

        if (facts.IsCriticalPath)
        {
            score += w.RiskCriticalPath;
            reasons.Add("路径位于系统/用户关键目录，禁止自动处理");
        }

        if (facts.IsOnSystemDrive)
        {
            score += w.RiskSystemDrive;
            reasons.Add("位于系统盘");
        }

        if (info.HasReparsePoint)
        {
            score += w.RiskReparsePoint;
            reasons.Add("包含符号链接/挂载点，可能指向其它位置");
        }

        if (hits.Any(h => h.Source == AssociationSource.RunningProcess))
        {
            score += w.RiskRunningProcess;
            reasons.Add("检测到有进程正在使用该目录");
        }

        if (facts.HasUserData)
        {
            score += w.RiskUserData;
            var samples = info.UserDataSamples.Take(3).ToList();
            reasons.Add("包含可能重要的用户数据/配置/存档" + (samples.Count > 0 ? "：" + string.Join("、", samples) : string.Empty));
        }

        var days = (DateTime.Now - info.LastModified).TotalDays;
        if (days < w.RiskRecentlyModifiedDays)
        {
            score += w.RiskRecentlyModified;
            reasons.Add($"最近 {(int)days} 天内有修改，可能仍在使用");
        }

        if (!info.HasUninstaller)
        {
            score += w.RiskNoUninstaller;
            reasons.Add("未发现卸载程序，无法通过标准卸载流程移除");
        }
        else
        {
            reasons.Add("存在卸载程序，建议优先使用其自身卸载程序");
        }

        if (facts.IsProtected)
        {
            score += w.RiskProtectedContent;
            reasons.Add("受保护内容：" + string.Join("、", facts.ProtectionReasons));
        }

        if (OrphanConfidenceEvaluator.IsAssociationDataUnreliable(sourceHealth, out var unreliableReason))
        {
            score += w.RiskUnreliableAssociationData;
            reasons.Add("关联检查不完整（" + unreliableReason + "），判定不可靠，必须人工确认");
        }

        var level = score >= w.RiskHighThreshold ? RiskLevel.High
            : score >= w.RiskMediumThreshold ? RiskLevel.Medium
            : RiskLevel.Low;

        return new CleanupRisk(level, Math.Clamp(score, 0, 100), reasons);
    }

    /// <summary>生成建议。永远不会输出"删除"。</summary>
    public static CleanupRecommendation Recommend(
        OrphanConfidenceLevel confidenceLevel,
        RiskLevel risk,
        DirectoryFacts facts,
        bool isExcluded,
        bool isRunning = false)
    {
        if (isExcluded || facts.IsCriticalPath) return CleanupRecommendation.Keep;
        if (risk == RiskLevel.High) return CleanupRecommendation.HighRiskReview;
        if (isRunning) return CleanupRecommendation.HighRiskReview;   // 正在被使用，绝不建议处理
        if (facts.IsProtected) return CleanupRecommendation.Review;

        return confidenceLevel switch
        {
            OrphanConfidenceLevel.VeryHighProbability when risk == RiskLevel.Low => CleanupRecommendation.Quarantine,
            OrphanConfidenceLevel.VeryHighProbability => CleanupRecommendation.Review,
            OrphanConfidenceLevel.HighProbability => CleanupRecommendation.Review,
            OrphanConfidenceLevel.Suspicious => CleanupRecommendation.Review,
            _ => CleanupRecommendation.Keep,
        };
    }

    private static List<IPathExclusionProvider> BuildProviders(
        ScanOptions options,
        IEnumerable<IPathExclusionProvider>? extra)
    {
        var providers = new List<IPathExclusionProvider>
        {
            new SystemProtectedPathProvider(),
            new OptionsExclusionProvider(options),
            new DefaultWhitelistProvider(),
        };

        if (extra != null) providers.AddRange(extra);

        return providers;
    }

    private static List<string> BuildReasons(EvidenceCollection collection, IReadOnlyList<string> riskReasons)
    {
        var reasons = new List<string>();

        foreach (var e in collection.Evidence.Where(x => x.IsPositive).OrderByDescending(x => x.Score))
            reasons.Add($"疑似残留依据：{e.Title}（{e.ScoreText}）—— {e.Description}");

        foreach (var e in collection.Evidence.Where(x => !x.IsPositive).OrderBy(x => x.Score))
            reasons.Add($"保护/否定依据：{e.Title}（{e.ScoreText}）—— {e.Description}");

        foreach (var warning in collection.Warnings)
            reasons.Add("⚠ " + warning);

        foreach (var r in riskReasons)
            if (!reasons.Contains(r)) reasons.Add(r);

        return reasons;
    }

    public static string SourceText(AssociationSource source) => AssociationSourceText.ToText(source);
}

/// <summary>清理风险评估结果（与置信度分离）。</summary>
public sealed record CleanupRisk(RiskLevel Level, int Score, IReadOnlyList<string> Reasons);

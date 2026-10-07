namespace WinCleaner.Core.Models;

/// <summary>
/// 扫描器给出的建议动作。
/// 注意：扫描器<b>永远不输出"删除"</b>，只输出建议；真正的删除由 CleanupService 在人工确认后执行。
/// </summary>
public enum CleanupRecommendation
{
    /// <summary>建议保留（普通目录 / 受保护 / 已排除）。</summary>
    Keep = 0,

    /// <summary>建议人工确认后再决定。</summary>
    Review = 1,

    /// <summary>高概率残留且清理风险低，建议人工确认后移入隔离区。</summary>
    Quarantine = 2,

    /// <summary>清理风险高，必须人工复核（不允许自动处理）。</summary>
    HighRiskReview = 3,
}

public static class CleanupRecommendationText
{
    public static string ToText(CleanupRecommendation recommendation) => recommendation switch
    {
        CleanupRecommendation.Quarantine => "建议人工确认后隔离",
        CleanupRecommendation.Review => "建议人工确认",
        CleanupRecommendation.HighRiskReview => "高风险，必须人工复核",
        _ => "建议保留",
    };
}

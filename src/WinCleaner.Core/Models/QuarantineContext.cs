using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>
/// 隔离时随条目一起写入 manifest 的判定上下文，
/// 让用户在隔离区里也能看到"当时为什么怀疑它、有多大把握、风险多高"。
/// </summary>
public sealed class QuarantineContext
{
    public int ConfidenceScore { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrphanConfidenceLevel ConfidenceLevel { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RiskLevel Risk { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanupRecommendation Recommendation { get; init; }

    /// <summary>判定依据摘要（扫描器 ReasonText）。</summary>
    public string? Reason { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static QuarantineContext FromItem(OrphanItem item) => new()
    {
        ConfidenceScore = item.ConfidenceScore,
        ConfidenceLevel = item.ConfidenceLevel,
        Risk = item.Risk,
        Recommendation = item.Recommendation,
        Reason = item.ReasonText,
        Warnings = item.Warnings,
    };
}

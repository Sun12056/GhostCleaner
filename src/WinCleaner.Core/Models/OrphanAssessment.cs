using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>
/// 一个候选目录的完整评估结论。Scanner 直接输出它，UI 不需要再做任何计算。
/// 流程：Candidate → Evidence Collection → Orphan Confidence → Cleanup Risk → Recommendation。
/// </summary>
public sealed class OrphanAssessment
{
    /// <summary>疑似残留置信度（0~100）。回答问题："我们有多大把握认为它是残留？"</summary>
    public int ConfidenceScore { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrphanConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>清理风险（0~100）与等级。回答问题："如果清理它，风险有多大？"</summary>
    public int RiskScore { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RiskLevel RiskLevel { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanupRecommendation Recommendation { get; init; }

    /// <summary>全部证据（含正向与负向）。回答问题："为什么这么判断？"</summary>
    public IReadOnlyList<Evidence> Evidence { get; init; } = Array.Empty<Evidence>();

    /// <summary>警告：扫描不完整、受保护、判定不可靠等。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>是否受保护（用户数据 / 游戏 / 开发项目 / 系统关键路径）。</summary>
    public bool IsProtected { get; init; }

    /// <summary>是否命中白名单（不参与任何删除）。</summary>
    public bool IsExcluded { get; init; }

    public string? ExcludeReason { get; init; }

    /// <summary>仅保留正向证据（支持"是残留"）。</summary>
    public IReadOnlyList<Evidence> PositiveEvidence => Evidence.Where(e => e.IsPositive).ToList();

    /// <summary>仅保留负向证据（保护 / 否定）。</summary>
    public IReadOnlyList<Evidence> NegativeEvidence => Evidence.Where(e => !e.IsPositive).ToList();

    public string ConfidenceText => $"{ConfidenceScore}%";

    public string ConfidenceLevelText => OrphanConfidenceThresholds.ToText(ConfidenceLevel);

    public string RecommendationText => CleanupRecommendationText.ToText(Recommendation);
}

using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>隔离区条目（每个条目对应一个 manifest.json）。</summary>
public sealed class QuarantineEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>隔离前原始路径。</summary>
    public string OriginalPath { get; init; } = string.Empty;

    /// <summary>隔离区中的路径。</summary>
    public string QuarantinePath { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public int FileCount { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>过期时间 = CreatedAt + 保留天数。</summary>
    public DateTime ExpireAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuarantineStatus Status { get; set; } = QuarantineStatus.Quarantined;

    public string? DisplayName { get; init; }

    public string? Note { get; init; }

    // ---------------- v0.1：可解释的隔离清单 ----------------

    /// <summary>隔离原因（扫描器给出的判定依据摘要）。</summary>
    public string? Reason { get; init; }

    /// <summary>隔离时的疑似残留置信度。</summary>
    public int ConfidenceScore { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrphanConfidenceLevel ConfidenceLevel { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RiskLevel Risk { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanupRecommendation Recommendation { get; init; }

    /// <summary>隔离时的警告（例如扫描结果不完整）。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public string? ConfidenceText => ConfidenceScore > 0 || ConfidenceLevel != OrphanConfidenceLevel.Normal
        ? $"{ConfidenceScore}%（{OrphanConfidenceThresholds.ToText(ConfidenceLevel)}）"
        : null;

    public string SizeText => Utils.FileSizeFormatter.Format(SizeBytes);

    public string StatusText => Status switch
    {
        QuarantineStatus.Quarantined => "已隔离",
        QuarantineStatus.Restored => "已恢复",
        QuarantineStatus.Purged => "已清除",
        QuarantineStatus.Failed => "异常",
        _ => "未知",
    };

    public bool IsExpired => DateTime.Now >= ExpireAt;
}

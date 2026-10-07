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

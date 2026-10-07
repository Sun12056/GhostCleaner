using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>操作日志条目。</summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel Level { get; init; } = LogLevel.Info;

    /// <summary>分类：Scan / Clean / Quarantine / Registry / Settings / System。</summary>
    public string Category { get; init; } = "System";

    /// <summary>操作类型：MoveToQuarantine / Restore / RecycleBin / PermanentDelete / Scan / Export ...</summary>
    public string Action { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    /// <summary>结果：Success / Failed / Skipped。</summary>
    public string Result { get; init; } = "Success";

    public string Message { get; init; } = string.Empty;

    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    public string SizeText => SizeBytes > 0 ? Utils.FileSizeFormatter.Format(SizeBytes) : "-";
}

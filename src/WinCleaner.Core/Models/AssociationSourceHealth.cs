using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>
/// 一个关联采集器的健康状态。AssociationIndex 必须能告诉 Scanner：
/// 哪些来源成功了、哪些失败了、各采集到多少条记录。
/// </summary>
public sealed class AssociationSourceHealth
{
    /// <summary>采集器名称（中文）。</summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>该采集器负责的关联来源。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IReadOnlyList<AssociationSource> CoveredSources { get; init; } = Array.Empty<AssociationSource>();

    /// <summary>扫描结果。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssociationSourceStatus Status { get; init; } = AssociationSourceStatus.NotRun;

    /// <summary>采集到的记录数。</summary>
    public int ItemCount { get; init; }

    /// <summary>失败/不完整原因（可为 null）。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>结果是否可用于"缺失即缺失"的推理（Partial 仅在明确提示不完整的前提下可用）。</summary>
    public bool IsUsable => Status is AssociationSourceStatus.Success or AssociationSourceStatus.Partial;

    public bool Covers(AssociationSource source) => CoveredSources.Contains(source);

    public string StatusText => AssociationSourceText.ToText(Status);

    public override string ToString()
        => $"{SourceName}\t{StatusText}\t{ItemCount}{(ErrorMessage == null ? string.Empty : "\t" + ErrorMessage)}";
}

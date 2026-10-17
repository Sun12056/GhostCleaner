using System.Text.Json.Serialization;

namespace GhostCleaner.Core.Models;

/// <summary>
/// 一次完整扫描的结果：条目 + 采集源健康度 + 本次置信度上限。
///
/// 存在的理由（v0.1）：
/// 置信度上限由采集源健康度决定，两者必须成对出现。
/// 如果只返回 <see cref="Items"/>，消费方（UI）为了解释"为什么这里只有 65 分"
/// 就不得不自己再取一次健康度 —— 而在 Transient 生命周期下它拿到的可能是
/// 从未 Build 过的空索引，从而把一次健康扫描误报成"严重不完整（上限 45）"。
/// 这正是"把没观测当成观测到失败"，必须消灭。
///
/// 消费方请直接读 <see cref="ConfidenceCap"/> / <see cref="ConfidenceCapNote"/>，
/// 不要自行调用判定逻辑重算。
/// </summary>
public sealed class ScanOutcome
{
    /// <summary>扫描结果条目（已按置信度、体积排序）。</summary>
    public IReadOnlyList<OrphanItem> Items { get; init; } = Array.Empty<OrphanItem>();

    /// <summary>本次扫描各关联采集源的健康度。与 <see cref="Items"/> 同批产生，绝不为空洞数据。</summary>
    public IReadOnlyList<AssociationSourceHealth> SourceHealth { get; init; } = Array.Empty<AssociationSourceHealth>();

    /// <summary>
    /// 本次扫描的置信度上限（0~100）。由 Core 依据 <see cref="SourceHealth"/> 计算，
    /// 已施加到 <see cref="Items"/> 的每一个 ConfidenceScore 上。
    /// </summary>
    public int ConfidenceCap { get; init; } = 100;

    /// <summary>上限的人类可读原因；全部来源成功时为 null。</summary>
    public string? ConfidenceCapNote { get; init; }

    /// <summary>置信度是否被上限约束（false 表示未受限，可用满 100）。</summary>
    public bool IsConfidenceCapped => ConfidenceCap < 100;

    // ---- 以下为纯派生展示属性，不含任何判定逻辑 ----

    /// <summary>完成的采集源数量。</summary>
    public int HealthySourceCount => SourceHealth.Count(h => h.Status == AssociationSourceStatus.Success);

    /// <summary>失败/不完整/未运行的采集源数量。</summary>
    public int UnhealthySourceCount => SourceHealth.Count - HealthySourceCount;

    /// <summary>全部采集源是否都成功完成。</summary>
    public bool IsAssociationDataFullyHealthy => SourceHealth.Count > 0 && UnhealthySourceCount == 0;

    /// <summary>采集到的系统引用总条数。</summary>
    public long TotalAssociationRecords => SourceHealth.Sum(h => (long)h.ItemCount);

    /// <summary>未完成检查的采集源名称（用于展示，不用于判定）。</summary>
    [JsonIgnore]
    public IReadOnlyList<string> UnhealthySourceNames =>
        SourceHealth.Where(h => h.Status != AssociationSourceStatus.Success)
                    .Select(h => h.SourceName)
                    .ToList();
}

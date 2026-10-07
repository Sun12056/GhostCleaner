using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>
/// 一条证据：回答"为什么认为它可能是残留"或"为什么应该保护它"。
/// <see cref="Score"/> 带符号，正值提高残留置信度，负值降低置信度。
/// </summary>
public sealed class Evidence
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EvidenceType Type { get; init; }

    /// <summary>带符号权重。正=支持"是残留"，负=保护性/否定性证据。</summary>
    public int Score { get; init; }

    /// <summary>简短标题（UI 列表展示）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>人类可读的解释。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否为"支持残留假设"的证据。</summary>
    public bool IsPositive { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EvidenceSeverity Severity { get; init; }

    /// <summary>该证据来自哪个关联扫描源（仅关联类证据有值）。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssociationSource? Source { get; init; }

    public string SignText => IsPositive ? "+" : "-";

    public string ScoreText => Score > 0 ? "+" + Score : Score.ToString();

    public string IconText => IsPositive ? "✓" : "⚠";

    public override string ToString() => $"{IconText} {Title}（{ScoreText}）：{Description}";
}

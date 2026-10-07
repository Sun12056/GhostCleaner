using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>一条"系统仍在使用该路径"的证据。</summary>
public sealed class AssociationHit
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssociationSource Source { get; init; }

    /// <summary>证据摘要，例如注册表项名、快捷方式路径、服务名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>证据指向的具体路径（可执行文件 / 目录）。</summary>
    public string TargetPath { get; init; } = string.Empty;

    public override string ToString() => $"{Source}: {Name} -> {TargetPath}";
}

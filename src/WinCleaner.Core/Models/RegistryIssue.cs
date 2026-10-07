namespace WinCleaner.Core.Models;

/// <summary>无效注册表项（附加注册表清理功能）。</summary>
public sealed class RegistryIssue
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>类型：无效卸载项 / 无效文件关联 / 无效服务 / 无效启动项。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>注册表项路径（形如 HKLM\SOFTWARE\...\Uninstall\xxx）。</summary>
    public string KeyPath { get; init; } = string.Empty;

    /// <summary>若不为 null 表示这是键下的某个值（删除值时用它），否则删除整个键。</summary>
    public string? ValueName { get; init; }

    /// <summary>根 hive 文本（HKLM / HKCU）。</summary>
    public string Hive { get; init; } = "HKLM";

    /// <summary>显示名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>该注册表项引用的目标路径（文件/目录），用于判断是否存在。</summary>
    public string? TargetPath { get; init; }

    /// <summary>无效原因：目标不存在 / 路径为空 / 卸载程序已丢失。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>是否勾选待清理（默认 false）。</summary>
    public bool IsSelected { get; set; }

    public bool IsFixed { get; set; }

    public string? OperationResult { get; set; }
}

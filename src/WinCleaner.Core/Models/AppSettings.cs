using System.Text.Json.Serialization;

namespace WinCleaner.Core.Models;

/// <summary>应用配置（System.Text.Json 持久化到 settings.json）。</summary>
public sealed class AppSettings
{
    /// <summary>隔离区根目录。默认放在非系统盘。</summary>
    public string QuarantineRoot { get; set; } = string.Empty;

    /// <summary>隔离区保留天数（7~30）。</summary>
    public int RetentionDays { get; set; } = 15;

    /// <summary>删除前是否尝试创建系统还原点。</summary>
    public bool CreateRestorePoint { get; set; } = true;

    /// <summary>默认清理动作（永远不自动执行，只是默认值）。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanActionType DefaultCleanAction { get; set; } = CleanActionType.Quarantine;

    /// <summary>是否允许把"高风险"目录加入清理队列（默认 false）。</summary>
    public bool AllowHighRiskClean { get; set; }

    /// <summary>扫描最大层级。</summary>
    public int MaxScanDepth { get; set; } = 2;

    /// <summary>是否扫描隐藏目录。</summary>
    public bool IncludeHiddenDirectories { get; set; }

    /// <summary>判定为软件目录的最低分值。</summary>
    public int MinSoftwareScore { get; set; } = 50;

    /// <summary>排除路径（白名单）：这些路径及其子目录永不参与删除。</summary>
    public List<string> ExcludedPaths { get; set; } = new();

    /// <summary>保留关键词：路径任意层级包含即排除。</summary>
    public List<string> KeepKeywords { get; set; } = new()
    {
        "SteamLibrary",
        "Steam",
        "Epic",
        "Epic Games",
        "Ubisoft",
        "工作",
        "项目",
        "Projects",
        "Backup",
        "我的文档",
    };

    /// <summary>用户自定义扫描目录。</summary>
    public List<string> CustomScanDirectories { get; set; } = new();

    /// <summary>上次选择的盘符。</summary>
    public List<string> LastSelectedDrives { get; set; } = new();

    /// <summary>重置为默认值（保留系统相关的动态推断）。</summary>
    public void Normalize()
    {
        RetentionDays = Math.Clamp(RetentionDays, 7, 30);
        MaxScanDepth = Math.Clamp(MaxScanDepth, 1, 4);
        MinSoftwareScore = Math.Clamp(MinSoftwareScore, 20, 100);
    }
}

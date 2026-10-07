using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinCleaner.Core.Models;

/// <summary>扫描结果条目：一个疑似孤儿软件残留目录。</summary>
public sealed class OrphanItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExcluded;
    private string? _excludeReason;
    private string? _operationResult;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Version { get; init; }

    public string? Publisher { get; init; }

    public string? ProductName { get; init; }

    public long SizeBytes { get; init; }

    public int FileCount { get; init; }

    public int DirectoryCount { get; init; }

    public DateTime LastModified { get; init; }

    public List<string> MainExecutables { get; init; } = new();

    public bool HasUninstaller { get; init; }

    public string? UninstallerPath { get; init; }

    public bool HasUserData { get; init; }

    public List<string> UserDataSamples { get; init; } = new();

    public bool IsRunning { get; init; }

    public int SoftwareScore { get; init; }

    public bool IsLikelySoftwareDirectory { get; init; }

    public RiskLevel Risk { get; init; }

    public int RiskScore { get; init; }

    // ---------------- v0.1：Evidence / Confidence / Recommendation ----------------

    /// <summary>疑似残留置信度（0~100）。这不是风险分，也不等于"可以删除"。</summary>
    public int ConfidenceScore { get; init; }

    public OrphanConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>扫描器给出的建议（永远不是"删除"）。</summary>
    public CleanupRecommendation Recommendation { get; init; } = CleanupRecommendation.Keep;

    /// <summary>全部证据（含正向残留依据与负向保护依据）。</summary>
    public IReadOnlyList<Evidence> Evidence { get; init; } = Array.Empty<WinCleaner.Core.Models.Evidence>();

    /// <summary>警告：扫描不完整 / 受保护 / 判定不可靠等。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>是否受保护（用户数据 / 游戏 / 开发项目 / 系统关键路径）。</summary>
    public bool IsProtected { get; init; }

    /// <summary>受保护原因。</summary>
    public string? ProtectionReason { get; init; }

    /// <summary>目录性质分类：游戏 / 开发项目 / 绿色软件 / 用户数据 / null。</summary>
    public string? DirectoryCategory { get; init; }

    /// <summary>判定原因（人类可读，兼容旧 UI）。</summary>
    public List<string> Reasons { get; init; } = new();

    /// <summary>检测到的系统关联证据（正常情况下应为空；非空说明"仍有关联"，不会被判为孤儿）。</summary>
    public List<AssociationHit> Associations { get; init; } = new();

    /// <summary>是否命中白名单（排除路径 / 保留关键词）。</summary>
    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (_isExcluded == value) return;
            _isExcluded = value;
            OnPropertyChanged();
        }
    }

    public string? ExcludeReason
    {
        get => _excludeReason;
        set
        {
            if (_excludeReason == value) return;
            _excludeReason = value;
            OnPropertyChanged();
        }
    }

    /// <summary>UI 勾选状态。默认 false —— 默认不删除任何东西。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>清理操作结果说明。</summary>
    public string? OperationResult
    {
        get => _operationResult;
        set
        {
            if (_operationResult == value) return;
            _operationResult = value;
            OnPropertyChanged();
        }
    }

    public string SizeText => Utils.FileSizeFormatter.Format(SizeBytes);

    public string LastModifiedText => LastModified == default ? "-" : LastModified.ToString("yyyy-MM-dd HH:mm");

    public string MainExecutableText => MainExecutables.Count == 0 ? "-" : string.Join(", ", MainExecutables.Select(System.IO.Path.GetFileName));

    public string RiskText => Risk switch
    {
        RiskLevel.Low => "低",
        RiskLevel.Medium => "中",
        RiskLevel.High => "高",
        _ => "-",
    };

    public string ReasonText => Reasons.Count == 0 ? "-" : string.Join("；", Reasons);

    // ---------------- 展示用文本（UI 不需要自己计算） ----------------

    public string ConfidenceText => ConfidenceScore + "%";

    public string ConfidenceLevelText => OrphanConfidenceThresholds.ToText(ConfidenceLevel);

    public string RecommendationText => CleanupRecommendationText.ToText(Recommendation);

    public IReadOnlyList<WinCleaner.Core.Models.Evidence> PositiveEvidence =>
        Evidence.Where(e => e.IsPositive).OrderByDescending(e => e.Score).ToList();

    public IReadOnlyList<WinCleaner.Core.Models.Evidence> NegativeEvidence =>
        Evidence.Where(e => !e.IsPositive).OrderBy(e => e.Score).ToList();

    public string EvidenceText => Evidence.Count == 0
        ? "-"
        : string.Join(Environment.NewLine, Evidence
            .OrderByDescending(e => e.IsPositive)
            .ThenByDescending(e => Math.Abs(e.Score))
            .Select(e => e.ToString()));

    public string WarningsText => Warnings.Count == 0 ? "-" : string.Join(Environment.NewLine, Warnings.Select(x => "⚠ " + x));

    public string ProtectionText => IsProtected ? (ProtectionReason ?? "受保护") : "无";

    public string CategoryText => DirectoryCategory ?? "-";

    public bool HasWarnings => Warnings.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

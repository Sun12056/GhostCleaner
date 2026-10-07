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

    /// <summary>判定原因（人类可读）。</summary>
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

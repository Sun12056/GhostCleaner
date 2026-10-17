using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostCleaner.App.Services;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Services;

namespace GhostCleaner.App.ViewModels;

/// <summary>设置页（UI 0.2：分区导航 + 即时保存，没有"保存"按钮）。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;

    private string _quarantineRoot;
    private int _retentionDays;
    private bool _createRestorePoint;
    private bool _allowHighRiskClean;
    private int _maxScanDepth;
    private bool _includeHiddenDirectories;
    private int _minSoftwareScore;
    private CleanActionType _defaultCleanAction;
    private string _newExcludedPath = string.Empty;
    private string _newKeyword = string.Empty;
    private string _statusText = string.Empty;
    private string _selectedSection = "常规";

    public SettingsViewModel(
        ISettingsService settings,
        ILogService log,
        IDialogService dialogs,
        IThemeService theme)
    {
        _settings = settings;
        _log = log;
        _dialogs = dialogs;
        _theme = theme;

        var current = settings.Current;
        _quarantineRoot = current.QuarantineRoot;
        _retentionDays = current.RetentionDays;
        _createRestorePoint = current.CreateRestorePoint;
        _allowHighRiskClean = current.AllowHighRiskClean;
        _maxScanDepth = current.MaxScanDepth;
        _includeHiddenDirectories = current.IncludeHiddenDirectories;
        _minSoftwareScore = current.MinSoftwareScore;
        _defaultCleanAction = current.DefaultCleanAction;

        ExcludedPaths = new ObservableCollection<string>(current.ExcludedPaths);
        KeepKeywords = new ObservableCollection<string>(current.KeepKeywords);
    }

    // ---------- 分区导航 ----------

    public ObservableCollection<string> Sections { get; } = new()
    {
        "常规", "扫描", "保护与白名单", "隔离区", "外观", "高级",
    };

    public string SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    // ---------- 集合 ----------

    public ObservableCollection<string> ExcludedPaths { get; }

    public ObservableCollection<string> KeepKeywords { get; }

    // ---------- 常规 ----------

    public CleanActionType DefaultCleanAction
    {
        get => _defaultCleanAction;
        set => SetAndPersist(ref _defaultCleanAction, value);
    }

    public int DefaultCleanActionIndex
    {
        get => (int)_defaultCleanAction;
        set => DefaultCleanAction = (CleanActionType)value;
    }

    public string[] CleanActionNames => new[] { "移到隔离区", "删除到回收站", "永久删除" };

    public bool CreateRestorePoint
    {
        get => _createRestorePoint;
        set => SetAndPersist(ref _createRestorePoint, value);
    }

    // ---------- 扫描 ----------

    public int MaxScanDepth
    {
        get => _maxScanDepth;
        set => SetAndPersist(ref _maxScanDepth, value);
    }

    public int MaxScanDepthIndex
    {
        get => Math.Clamp(_maxScanDepth - 1, 0, 3);
        set => MaxScanDepth = value + 1;
    }

    public bool IncludeHiddenDirectories
    {
        get => _includeHiddenDirectories;
        set => SetAndPersist(ref _includeHiddenDirectories, value);
    }

    public int MinSoftwareScore
    {
        get => _minSoftwareScore;
        set => SetAndPersist(ref _minSoftwareScore, value);
    }

    public int MinSoftwareScoreIndex
    {
        get => _minSoftwareScore switch
        {
            <= 20 => 0,
            <= 35 => 1,
            <= 50 => 2,
            _ => 3,
        };
        set => MinSoftwareScore = value switch
        {
            0 => 20,
            1 => 35,
            2 => 50,
            _ => 70,
        };
    }

    // ---------- 保护与白名单 ----------

    public bool AllowHighRiskClean
    {
        get => _allowHighRiskClean;
        set => SetAndPersist(ref _allowHighRiskClean, value);
    }

    public string NewExcludedPath
    {
        get => _newExcludedPath;
        set => SetProperty(ref _newExcludedPath, value);
    }

    public string NewKeyword
    {
        get => _newKeyword;
        set => SetProperty(ref _newKeyword, value);
    }

    // ---------- 隔离区 ----------

    public string QuarantineRoot
    {
        get => _quarantineRoot;
        set => SetAndPersist(ref _quarantineRoot, value);
    }

    public int RetentionDays
    {
        get => _retentionDays;
        set => SetAndPersist(ref _retentionDays, value);
    }

    public int RetentionDaysIndex
    {
        get => _retentionDays switch
        {
            <= 7 => 0,
            <= 15 => 1,
            _ => 2,
        };
        set => RetentionDays = value switch
        {
            0 => 7,
            1 => 15,
            _ => 30,
        };
    }

    // ---------- 外观 ----------

    /// <summary>当前主题模式（写入即生效）。</summary>
    public Services.ThemeMode CurrentThemeMode
    {
        get => _theme.Mode;
        set
        {
            _theme.Apply(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeModeSystem));
            OnPropertyChanged(nameof(ThemeModeLight));
            OnPropertyChanged(nameof(ThemeModeDark));
        }
    }

    public bool ThemeModeSystem
    {
        get => _theme.Mode == Services.ThemeMode.System;
        set { if (value) CurrentThemeMode = Services.ThemeMode.System; }
    }

    public bool ThemeModeLight
    {
        get => _theme.Mode == Services.ThemeMode.Light;
        set { if (value) CurrentThemeMode = Services.ThemeMode.Light; }
    }

    public bool ThemeModeDark
    {
        get => _theme.Mode == Services.ThemeMode.Dark;
        set { if (value) CurrentThemeMode = Services.ThemeMode.Dark; }
    }

    // ---------- 状态 ----------

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SettingsFilePath => _settings.SettingsFilePath;

    // ---------- 命令 ----------

    [RelayCommand]
    private void BrowseQuarantine()
    {
        var folder = _dialogs.PickFolder(QuarantineRoot);
        if (!string.IsNullOrWhiteSpace(folder)) QuarantineRoot = folder;
    }

    [RelayCommand]
    private void BrowseExcludedPath()
    {
        var folder = _dialogs.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            NewExcludedPath = folder;
            AddExcludedPath();
        }
    }

    [RelayCommand]
    private void AddExcludedPath()
    {
        var path = NewExcludedPath.Trim();
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!ExcludedPaths.Contains(path)) ExcludedPaths.Add(path);
        NewExcludedPath = string.Empty;
        _ = PersistAsync();
    }

    [RelayCommand]
    private void RemoveExcludedPath(string? path)
    {
        if (path == null) return;
        ExcludedPaths.Remove(path);
        _ = PersistAsync();
    }

    [RelayCommand]
    private void AddKeyword()
    {
        var keyword = NewKeyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword)) return;
        if (!KeepKeywords.Contains(keyword)) KeepKeywords.Add(keyword);
        NewKeyword = string.Empty;
        _ = PersistAsync();
    }

    [RelayCommand]
    private void RemoveKeyword(string? keyword)
    {
        if (keyword == null) return;
        KeepKeywords.Remove(keyword);
        _ = PersistAsync();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        RetentionDays = 15;
        CreateRestorePoint = true;
        AllowHighRiskClean = false;
        MaxScanDepth = 2;
        IncludeHiddenDirectories = false;
        MinSoftwareScore = 50;
        DefaultCleanAction = CleanActionType.Quarantine;
        QuarantineRoot = DriveHelper.SuggestQuarantineRoot();
        StatusText = "已恢复默认设置。";
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        var dir = Path.GetDirectoryName(_settings.SettingsFilePath);
        if (string.IsNullOrEmpty(dir)) return;
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }
        catch
        {
            // 忽略
        }
    }

    // ---------- 内部 ----------

    /// <summary>写入并静默保存（UI 0.2：设置即时生效，没有保存按钮）。</summary>
    private bool SetAndPersist<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        if (!SetProperty(ref field, value, propertyName)) return false;

        // 联动的 Index 属性也要刷新
        OnPropertyChanged(nameof(MaxScanDepthIndex));
        OnPropertyChanged(nameof(MinSoftwareScoreIndex));
        OnPropertyChanged(nameof(RetentionDaysIndex));
        OnPropertyChanged(nameof(DefaultCleanActionIndex));

        _ = PersistAsync();
        return true;
    }

    private async Task PersistAsync()
    {
        var current = _settings.Current;
        current.QuarantineRoot = QuarantineRoot;
        current.RetentionDays = RetentionDays;
        current.CreateRestorePoint = CreateRestorePoint;
        current.AllowHighRiskClean = AllowHighRiskClean;
        current.MaxScanDepth = MaxScanDepth;
        current.IncludeHiddenDirectories = IncludeHiddenDirectories;
        current.MinSoftwareScore = MinSoftwareScore;
        current.DefaultCleanAction = DefaultCleanAction;
        current.ExcludedPaths = ExcludedPaths.ToList();
        current.KeepKeywords = KeepKeywords.ToList();
        current.Normalize();

        await _settings.SaveAsync();
        _log.Info("Settings", "Save", "配置已保存");
        StatusText = $"配置已保存到：{_settings.SettingsFilePath}";
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Services;

namespace WinCleaner.App.ViewModels;

/// <summary>设置页面：隔离区、白名单、扫描参数。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;

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

    public SettingsViewModel(ISettingsService settings, ILogService log, IDialogService dialogs)
    {
        _settings = settings;
        _log = log;
        _dialogs = dialogs;

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

    public ObservableCollection<string> ExcludedPaths { get; }

    public ObservableCollection<string> KeepKeywords { get; }

    public string QuarantineRoot
    {
        get => _quarantineRoot;
        set => SetProperty(ref _quarantineRoot, value);
    }

    public int RetentionDays
    {
        get => _retentionDays;
        set => SetProperty(ref _retentionDays, value);
    }

    public bool CreateRestorePoint
    {
        get => _createRestorePoint;
        set => SetProperty(ref _createRestorePoint, value);
    }

    public bool AllowHighRiskClean
    {
        get => _allowHighRiskClean;
        set => SetProperty(ref _allowHighRiskClean, value);
    }

    public int MaxScanDepth
    {
        get => _maxScanDepth;
        set => SetProperty(ref _maxScanDepth, value);
    }

    public bool IncludeHiddenDirectories
    {
        get => _includeHiddenDirectories;
        set => SetProperty(ref _includeHiddenDirectories, value);
    }

    public int MinSoftwareScore
    {
        get => _minSoftwareScore;
        set => SetProperty(ref _minSoftwareScore, value);
    }

    public CleanActionType DefaultCleanAction
    {
        get => _defaultCleanAction;
        set => SetProperty(ref _defaultCleanAction, value);
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

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SettingsFilePath => _settings.SettingsFilePath;

    public string[] CleanActionNames => new[] { "移到隔离区", "删除到回收站", "永久删除" };

    public int DefaultCleanActionIndex
    {
        get => (int)_defaultCleanAction;
        set => DefaultCleanAction = (CleanActionType)value;
    }

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
    }

    [RelayCommand]
    private void RemoveExcludedPath(string? path)
    {
        if (path != null) ExcludedPaths.Remove(path);
    }

    [RelayCommand]
    private void AddKeyword()
    {
        var keyword = NewKeyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword)) return;
        if (!KeepKeywords.Contains(keyword)) KeepKeywords.Add(keyword);
        NewKeyword = string.Empty;
    }

    [RelayCommand]
    private void RemoveKeyword(string? keyword)
    {
        if (keyword != null) KeepKeywords.Remove(keyword);
    }

    [RelayCommand]
    private async Task SaveAsync()
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
        _dialogs.Info("配置已保存。");
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
        StatusText = "已恢复默认设置（仍需点击保存）。";
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
}

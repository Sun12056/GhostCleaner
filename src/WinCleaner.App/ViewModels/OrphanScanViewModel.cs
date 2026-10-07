using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Helpers;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;
using WinCleaner.Services.Cleanup;

namespace WinCleaner.App.ViewModels;

public sealed class DriveOption : ObservableObject
{
    private bool _isSelected;

    public string Root { get; init; } = string.Empty;

    public long FreeBytes { get; init; }

    public long TotalBytes { get; init; }

    public bool IsSystemDrive { get; init; }

    public string Display =>
        $"{Root}  可用 {FileSizeFormatter.Format(FreeBytes)} / 共 {FileSizeFormatter.Format(TotalBytes)}"
        + (IsSystemDrive ? "（系统盘）" : string.Empty);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public enum RiskFilterOption
{
    All,
    Low,
    Medium,
    High,
}

/// <summary>孤儿软件扫描页 ViewModel。</summary>
public sealed partial class OrphanScanViewModel : ObservableObject
{
    private readonly IOrphanScanner _scanner;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IReportExporter _exporter;
    private readonly CleanupService _cleanup;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _cts;
    private OrphanItem? _selectedItem;
    private string _statusText = "请选择要扫描的盘符或目录，然后点击“开始扫描”。默认不会删除任何东西。";
    private string _searchText = string.Empty;
    private RiskFilterOption _riskFilter = RiskFilterOption.All;
    private bool _showExcluded = true;
    private double _progressValue;
    private bool _isScanning;
    private bool _isBusy;
    private string _customDirectoryInput = string.Empty;
    private string _orphanCountText = "0";

    public OrphanScanViewModel(
        IOrphanScanner scanner,
        ISettingsService settings,
        ILogService log,
        IReportExporter exporter,
        CleanupService cleanup,
        IDialogService dialogs)
    {
        _scanner = scanner;
        _settings = settings;
        _log = log;
        _exporter = exporter;
        _cleanup = cleanup;
        _dialogs = dialogs;

        Results = new ObservableCollection<OrphanItem>();
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = FilterItem;

        Drives = new ObservableCollection<DriveOption>();
        CustomDirectories = new ObservableCollection<string>();

        LoadDrives();
        foreach (var dir in _settings.Current.CustomScanDirectories) CustomDirectories.Add(dir);
    }

    // ---------- 集合与绑定属性 ----------

    public ObservableCollection<DriveOption> Drives { get; }

    public ObservableCollection<string> CustomDirectories { get; }

    public ObservableCollection<OrphanItem> Results { get; }

    public ICollectionView ResultsView { get; }

    public OrphanItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public RiskFilterOption RiskFilter
    {
        get => _riskFilter;
        set
        {
            if (SetProperty(ref _riskFilter, value)) ResultsView.Refresh();
        }
    }

    public bool ShowExcluded
    {
        get => _showExcluded;
        set
        {
            if (SetProperty(ref _showExcluded, value)) ResultsView.Refresh();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) ResultsView.Refresh();
        }
    }

    public string CustomDirectoryInput
    {
        get => _customDirectoryInput;
        set => SetProperty(ref _customDirectoryInput, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                ScanCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                NotifyCleanCommands();
            }
        }
    }

    public bool IsIdle => !IsScanning && !IsBusy;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string OrphanCountText
    {
        get => _orphanCountText;
        set => SetProperty(ref _orphanCountText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public int ResultCount => Results.Count;

    public int SelectedCount => Results.Count(x => x.IsSelected && !x.IsExcluded);

    public long SelectedSize => Results.Where(x => x.IsSelected && !x.IsExcluded).Sum(x => x.SizeBytes);

    public string SelectedSummary =>
        $"已勾选 {SelectedCount} 项，共 {FileSizeFormatter.Format(SelectedSize)}";

    public int HighRiskCount => Results.Count(x => x.Risk == RiskLevel.High && !x.IsExcluded);

    public int MediumRiskCount => Results.Count(x => x.Risk == RiskLevel.Medium && !x.IsExcluded);

    public int LowRiskCount => Results.Count(x => x.Risk == RiskLevel.Low && !x.IsExcluded);

    // ---------- 命令 ----------

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task ScanAsync()
    {
        var roots = BuildRootPaths();
        if (roots.Count == 0)
        {
            _dialogs.Warning("请至少选择一个盘符，或添加一个自定义目录。");
            return;
        }

        if (!AdminHelper.IsAdministrator)
            _dialogs.Warning("当前未以管理员身份运行，注册表与部分系统信息可能读取不完整，判定结果会偏保守。");

        _cts = new CancellationTokenSource();
        IsScanning = true;
        ProgressValue = 0;
        Results.Clear();
        SelectedItem = null;
        StatusText = "准备扫描…";

        var options = new ScanOptions
        {
            RootPaths = roots,
            MaxDepth = _settings.Current.MaxScanDepth,
            IncludeHiddenDirectories = _settings.Current.IncludeHiddenDirectories,
            MinSoftwareScore = _settings.Current.MinSoftwareScore,
            ExcludedPaths = _settings.Current.ExcludedPaths.ToList(),
            KeepKeywords = _settings.Current.KeepKeywords.ToList(),
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.Message)) StatusText = p.Message;
                else if (!string.IsNullOrWhiteSpace(p.CurrentPath)) StatusText = p.CurrentPath;

                if (p.CandidateCount > 0)
                    ProgressValue = Math.Clamp(p.ScannedDirectories * 100d / p.CandidateCount, 0, 100);

                OrphanCountText = p.OrphanCount.ToString();
            });

            var items = await _scanner.ScanAsync(options, progress, _cts.Token);

            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                Results.Add(item);
            }

            OnPropertyChanged(nameof(ResultCount));
            UpdateSelection();

            StatusText = $"扫描完成：发现 {items.Count} 个疑似孤儿软件残留（耗时 {sw.Elapsed.TotalSeconds:F1} 秒）。"
                       + " 默认未勾选任何条目，请人工确认后再处理。";
            _log.Info("Scan", "Scan",
                $"扫描完成：根路径 {roots.Count} 个，发现 {items.Count} 个疑似孤儿软件残留，耗时 {sw.Elapsed.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消。";
            _log.Warning("Scan", "Scan", "用户取消了扫描");
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
            _log.Error("Scan", "Scan", ex.Message);
            _dialogs.Error("扫描失败：" + ex.Message);
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectAll()
    {
        foreach (var item in Results.Where(x => !x.IsExcluded)) item.IsSelected = true;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectNone()
    {
        foreach (var item in Results) item.IsSelected = false;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void InvertSelection()
    {
        foreach (var item in Results.Where(x => !x.IsExcluded)) item.IsSelected = !item.IsSelected;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectLowRiskOnly()
    {
        foreach (var item in Results)
            item.IsSelected = !item.IsExcluded && item.Risk == RiskLevel.Low;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task MoveToQuarantineAsync() => await CleanAsync(CleanActionType.Quarantine);

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task DeleteToRecycleBinAsync() => await CleanAsync(CleanActionType.RecycleBin);

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task PermanentDeleteAsync() => await CleanAsync(CleanActionType.PermanentDelete);

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Results.Count == 0)
        {
            _dialogs.Warning("没有可导出的结果，请先扫描。");
            return;
        }

        var path = _dialogs.SaveFile(
            "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
            $"WinCleaner-Scan-{DateTime.Now:yyyyMMdd_HHmmss}.json");

        if (path == null) return;

        try
        {
            await _exporter.ExportScanReportAsync(Results, path);
            _log.Info("Scan", "Export", "扫描报告已导出：" + path);
            _dialogs.Info("报告已导出：\n" + path);
        }
        catch (Exception ex)
        {
            _log.Error("Scan", "Export", ex.Message);
            _dialogs.Error("导出失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private void OpenSelectedFolder()
    {
        if (SelectedItem == null || !Directory.Exists(SelectedItem.Path)) return;
        try
        {
            Process.Start("explorer.exe", SelectedItem.Path);
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private void RunUninstaller()
    {
        if (SelectedItem?.UninstallerPath == null || !File.Exists(SelectedItem.UninstallerPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(SelectedItem.UninstallerPath) { UseShellExecute = true });
            _log.Info("Clean", "RunUninstaller", "启动卸载程序：" + SelectedItem.UninstallerPath, SelectedItem.Path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("无法启动卸载程序：" + ex.Message);
        }
    }

    [RelayCommand]
    private void AddCustomDirectory()
    {
        var folder = _dialogs.PickFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;
        AddDirectory(folder);
    }

    [RelayCommand]
    private void AddInputDirectory()
    {
        if (string.IsNullOrWhiteSpace(CustomDirectoryInput)) return;
        AddDirectory(CustomDirectoryInput);
        CustomDirectoryInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveCustomDirectory(string? path)
    {
        if (path == null) return;
        CustomDirectories.Remove(path);
        _settings.Current.CustomScanDirectories.Remove(path);
        _ = _settings.SaveAsync();
    }

    [RelayCommand]
    private void AddCommonDirectories()
    {
        int added = 0;
        foreach (var drive in Drives.Where(d => d.IsSelected))
        {
            foreach (var name in ScanOptions.DefaultInstallFolderNames)
            {
                var candidate = System.IO.Path.Combine(drive.Root, name);
                if (Directory.Exists(candidate) && !CustomDirectories.Contains(candidate))
                {
                    AddDirectory(candidate, persist: true);
                    added++;
                }
            }
        }

        _dialogs.Info(added > 0 ? $"已添加 {added} 个常见安装目录。" : "未发现额外的常见安装目录。");
    }

    [RelayCommand]
    private async Task AddSelectedToWhitelistAsync()
    {
        if (SelectedItem == null) return;

        _settings.Current.ExcludedPaths.Add(SelectedItem.Path);
        await _settings.SaveAsync();

        SelectedItem.IsExcluded = true;
        SelectedItem.ExcludeReason = "已加入排除路径白名单";
        SelectedItem.IsSelected = false;
        UpdateSelection();
        ResultsView.Refresh();

        _log.Info("Settings", "AddWhitelist", "加入排除路径：" + SelectedItem.Path, SelectedItem.Path);
        _dialogs.Info("已加入排除路径，该目录不会再参与删除。");
    }

    // ---------- 内部实现 ----------

    private void AddDirectory(string path, bool persist = true)
    {
        var normalized = PathUtils.Normalize(path);
        if (!Directory.Exists(normalized))
        {
            _dialogs.Warning("目录不存在：" + normalized);
            return;
        }

        if (!CustomDirectories.Contains(normalized)) CustomDirectories.Add(normalized);

        if (persist && !_settings.Current.CustomScanDirectories.Contains(normalized))
        {
            _settings.Current.CustomScanDirectories.Add(normalized);
            _ = _settings.SaveAsync();
        }
    }

    private List<string> BuildRootPaths()
    {
        var roots = new List<string>();
        roots.AddRange(Drives.Where(d => d.IsSelected).Select(d => d.Root));
        roots.AddRange(CustomDirectories);
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task CleanAsync(CleanActionType action)
    {
        var targets = Results.Where(x => x.IsSelected && !x.IsExcluded).ToList();
        if (targets.Count == 0)
        {
            _dialogs.Warning("请先勾选要处理的条目。默认不会自动勾选任何条目。");
            return;
        }

        var totalSize = targets.Sum(x => x.SizeBytes);
        var highRisk = targets.Count(x => x.Risk == RiskLevel.High);
        bool allowHighRisk = false;

        if (highRisk > 0)
        {
            if (!_settings.Current.AllowHighRiskClean)
            {
                _dialogs.Warning($"所选条目中有 {highRisk} 个高风险条目，将自动跳过。\n"
                               + "高风险条目位于系统或用户关键目录，默认不参与删除。");
            }
            else
            {
                allowHighRisk = _dialogs.Confirm(
                    $"所选条目中有 {highRisk} 个高风险条目。\n高风险条目可能涉及系统或用户关键目录，确定继续吗？",
                    "高风险确认",
                    danger: true);
            }
        }

        var message = $"即将对 {targets.Count} 个目录执行【{CleanupService.ActionText(action)}】。\n"
                    + $"总大小：{FileSizeFormatter.Format(totalSize)}\n\n"
                    + (action == CleanActionType.Quarantine ? "目录会被移动到隔离区，可在隔离区页面恢复。\n" : string.Empty)
                    + (action == CleanActionType.RecycleBin ? "目录会被删除到回收站，可从回收站还原。\n" : string.Empty)
                    + (action == CleanActionType.PermanentDelete ? "永久删除不可恢复！请再次确认这些目录不再需要。\n" : string.Empty)
                    + "\n是否继续？";

        string? confirmText = null;

        if (action == CleanActionType.PermanentDelete)
        {
            if (!_dialogs.ConfirmWithText(message, CleanupService.PermanentDeleteConfirmText, "永久删除确认"))
                return;
            confirmText = CleanupService.PermanentDeleteConfirmText;
        }
        else if (!_dialogs.Confirm(message, "清理确认", danger: action == CleanActionType.RecycleBin))
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在处理…";

        try
        {
            var result = await _cleanup.ExecuteAsync(
                targets,
                action,
                allowHighRisk,
                confirmText,
                new Progress<string>(s => StatusText = s));

            _dialogs.Info(result.Summary + "\n\n完整记录请查看【日志】页面。", "处理完成");
            StatusText = result.Summary;

            var succeeded = targets
                .Where(x => x.OperationResult != null
                            && !x.OperationResult.Contains("跳过")
                            && !x.OperationResult.Contains("失败"))
                .ToList();

            foreach (var item in succeeded)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                Results.Remove(item);
            }

            OnPropertyChanged(nameof(ResultCount));
            UpdateSelection();
        }
        catch (Exception ex)
        {
            _log.Error("Clean", "Clean", ex.Message);
            _dialogs.Error("处理失败：" + ex.Message);
            StatusText = "处理失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool FilterItem(object obj)
    {
        if (obj is not OrphanItem item) return false;

        if (!ShowExcluded && item.IsExcluded) return false;

        if (RiskFilter != RiskFilterOption.All)
        {
            var expected = RiskFilter switch
            {
                RiskFilterOption.Low => RiskLevel.Low,
                RiskFilterOption.Medium => RiskLevel.Medium,
                RiskFilterOption.High => RiskLevel.High,
                _ => RiskLevel.Low,
            };
            if (item.Risk != expected) return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            if (!item.Path.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !item.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void LoadDrives()
    {
        Drives.Clear();
        var systemRoot = DriveHelperSystemRoot();

        foreach (var drive in System.IO.DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

                var root = drive.RootDirectory.FullName;
                Drives.Add(new DriveOption
                {
                    Root = root,
                    FreeBytes = drive.AvailableFreeSpace,
                    TotalBytes = drive.TotalSize,
                    IsSystemDrive = root.Equals(systemRoot, StringComparison.OrdinalIgnoreCase),
                    IsSelected = !root.Equals(systemRoot, StringComparison.OrdinalIgnoreCase),
                });
            }
            catch
            {
                // 忽略不可读的盘
            }
        }
    }

    private static string DriveHelperSystemRoot()
        => System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrphanItem.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(HighRiskCount));
        OnPropertyChanged(nameof(MediumRiskCount));
        OnPropertyChanged(nameof(LowRiskCount));
        NotifyCleanCommands();
    }

    private void NotifyCleanCommands()
    {
        MoveToQuarantineCommand.NotifyCanExecuteChanged();
        DeleteToRecycleBinCommand.NotifyCanExecuteChanged();
        PermanentDeleteCommand.NotifyCanExecuteChanged();
    }
}

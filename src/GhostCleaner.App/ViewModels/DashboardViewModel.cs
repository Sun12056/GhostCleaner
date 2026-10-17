using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostCleaner.App.Helpers;
using GhostCleaner.App.Services;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Core.Utils;

namespace GhostCleaner.App.ViewModels;

public sealed record DriveStatus(string Name, long FreeBytes, long TotalBytes, double UsedPercent)
{
    public string FreeText => FileSizeFormatter.Format(FreeBytes);

    public string TotalText => FileSizeFormatter.Format(TotalBytes);

    public string Summary => $"{Name}  已用 {UsedPercent:0.#}%   可用 {FreeText} / {TotalText}";
}

/// <summary>概览：一句话说清它是干什么的，以及现在能做什么。</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IQuarantineManager _quarantine;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly UiStateService _uiState;

    private string _quarantineSummary = "正在读取…";
    private string _scanHint = string.Empty;

    public DashboardViewModel(
        IQuarantineManager quarantine,
        ISettingsService settings,
        ILogService log,
        IDialogService dialogs,
        INavigationService navigation,
        UiStateService uiState)
    {
        _quarantine = quarantine;
        _settings = settings;
        _log = log;
        _dialogs = dialogs;
        _navigation = navigation;
        _uiState = uiState;

        Drives = new ObservableCollection<DriveStatus>();
        RecentLogs = new ObservableCollection<LogEntry>();

        _ = RefreshAsync();
    }

    public ObservableCollection<DriveStatus> Drives { get; }

    public ObservableCollection<LogEntry> RecentLogs { get; }

    public string QuarantineSummary
    {
        get => _quarantineSummary;
        set => SetProperty(ref _quarantineSummary, value);
    }

    public string ScanHint
    {
        get => _scanHint;
        set => SetProperty(ref _scanHint, value);
    }

    public bool IsAdministrator => AdminHelper.IsAdministrator;

    /// <summary>同上：未提权会让"正在使用的软件更容易被判成残留"，不是"偏保守"。</summary>
    public bool HasElevationRisk => !IsAdministrator;

    public string AdminText => AdminHelper.IsAdministrator
        ? "当前已以管理员身份运行，可完整读取注册表与系统信息。"
        : "当前未以管理员身份运行：运行中的进程与计划任务采集受限，正在使用的软件可能被误判为残留。";

    public string ElevationWarningText =>
        "未以管理员身份运行：运行中的进程采集不完整，计划任务基本采集不到。"
      + "正在使用但未被采集到的软件会缺少「运行中进程」保护信号，可能被误判为残留。";

    /// <summary>以管理员身份重启本程序。</summary>
    [RelayCommand]
    private void RestartAsAdmin() => AdminRestart.Request(_dialogs, _log);

    public string QuarantineRootText => _quarantine.QuarantineRoot;

    // ---------- 度量块 ----------

    public bool HasPreviousScan => _uiState.UiState.LastScan != null;

    public string LastScanText => HasPreviousScan
        ? RelativeTime(_uiState.UiState.LastScan!.FinishedAt)
        : "从未扫描";

    public string LastScanSubText => HasPreviousScan
        ? $"{_uiState.UiState.LastScan!.FinishedAt:yyyy-MM-dd HH:mm} · 耗时 {_uiState.UiState.LastScan.DurationSeconds:F1} 秒"
        : "完成一次扫描后，这里会显示时间";

    public string OrphanCountText => HasPreviousScan
        ? _uiState.UiState.LastScan!.OrphanCount.ToString()
        : "—";

    public string OrphanSubText => HasPreviousScan
        ? $"其中 {_uiState.UiState.LastScan!.HighConfidenceCount} 个几乎未检测到使用迹象"
        : "完成一次扫描后显示";

    public string TotalSizeText => HasPreviousScan
        ? FileSizeFormatter.Format(_uiState.UiState.LastScan!.TotalSize)
        : "—";

    public string TotalSizeSubText => HasPreviousScan
        ? "仅统计未排除条目"
        : "完成一次扫描后显示";

    public string QuarantineCountText { get; private set; } = "—";

    public string QuarantineSizeText { get; private set; } = "—";

    public string QuarantineSubText { get; private set; } = "正在读取…";

    public string RetentionDaysText => _settings.Current.RetentionDays.ToString();

    // ---------- 最近一次扫描卡片 ----------

    public string LastScanRootsText => HasPreviousScan
        ? _uiState.UiState.LastScan!.RootPathsText
        : "—";

    public string LastScanSummaryText => HasPreviousScan
        ? $"检测到 {_uiState.UiState.LastScan!.OrphanCount} 个未发现系统关联的软件目录，"
          + $"共 {FileSizeFormatter.Format(_uiState.UiState.LastScan.TotalSize)}。"
          + $"其中 {_uiState.UiState.LastScan.HighConfidenceCount} 个几乎未检测到使用迹象，"
          + $"{_uiState.UiState.LastScan.ProtectedCount} 个受保护或已排除。"
          + "「未发现关联」不等于「没人用」，处理前请逐项查看检测依据。"
        : "还没有扫描过。开始第一次扫描，看看 Windows 忘记了什么。";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Drives.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                var used = drive.TotalSize == 0 ? 0 : (drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize;
                Drives.Add(new DriveStatus(drive.Name, drive.AvailableFreeSpace, drive.TotalSize, used));
            }
            catch
            {
                // 忽略
            }
        }

        try
        {
            var entries = await _quarantine.ListAsync();
            var size = entries.Sum(e => e.SizeBytes);
            var retentionDays = _settings.Current.RetentionDays;

            QuarantineCountText = entries.Count.ToString();
            QuarantineSizeText = FileSizeFormatter.Format(size);

            var active = entries.Where(e => e.Status == QuarantineStatus.Quarantined).ToList();
            if (active.Count == 0)
            {
                QuarantineSubText = $"空 · 保留 {retentionDays} 天";
            }
            else
            {
                var earliest = active.Min(e => e.ExpireAt);
                var days = (int)Math.Ceiling((earliest - DateTime.Now).TotalDays);
                QuarantineSubText = days > 0
                    ? $"保留 {retentionDays} 天 · 最早 {days} 天后到期"
                    : $"保留 {retentionDays} 天 · 已有条目到期";
            }

            QuarantineSummary = $"隔离区共 {entries.Count} 项，占用 {FileSizeFormatter.Format(size)}，保留 {retentionDays} 天";
        }
        catch (Exception ex)
        {
            QuarantineSummary = "读取隔离区失败：" + ex.Message;
            QuarantineSubText = "读取失败";
        }

        try
        {
            var logs = await _log.LoadAsync();
            RecentLogs.Clear();
            foreach (var entry in logs.Take(10)) RecentLogs.Add(entry);
        }
        catch
        {
            // 忽略
        }

        ScanHint = "提示：扫描只呈现检测依据，不做自动判定。「未发现系统关联」不等于「没人用」。不会删除任何文件，是否处理由你逐项决定。";

        OnPropertyChanged(nameof(HasPreviousScan));
        OnPropertyChanged(nameof(LastScanText));
        OnPropertyChanged(nameof(LastScanSubText));
        OnPropertyChanged(nameof(OrphanCountText));
        OnPropertyChanged(nameof(OrphanSubText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(TotalSizeSubText));
        OnPropertyChanged(nameof(QuarantineCountText));
        OnPropertyChanged(nameof(QuarantineSizeText));
        OnPropertyChanged(nameof(QuarantineSubText));
        OnPropertyChanged(nameof(LastScanRootsText));
        OnPropertyChanged(nameof(LastScanSummaryText));
    }

    [RelayCommand]
    private void GoToScan() => _navigation.Navigate("Orphan");

    [RelayCommand]
    private void GoToResults() => _navigation.Navigate("Results");

    [RelayCommand]
    private void GoToQuarantine() => _navigation.Navigate("Quarantine");

    [RelayCommand]
    private void GoToSettings() => _navigation.Navigate("Settings");

    private static string RelativeTime(DateTime time)
    {
        var span = DateTime.Now - time;
        if (span.TotalMinutes < 1) return "刚刚";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} 天前";
        return time.ToString("yyyy-MM-dd");
    }
}

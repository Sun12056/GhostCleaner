using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Helpers;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.App.ViewModels;

public sealed record DriveStatus(string Name, long FreeBytes, long TotalBytes, double UsedPercent)
{
    public string FreeText => WinCleaner.Core.Utils.FileSizeFormatter.Format(FreeBytes);

    public string TotalText => WinCleaner.Core.Utils.FileSizeFormatter.Format(TotalBytes);

    public string Summary => $"{Name}  已用 {UsedPercent:0.#}%   可用 {FreeText} / {TotalText}";
}

/// <summary>仪表盘：磁盘概况、隔离区概况、最近日志。</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IQuarantineManager _quarantine;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly INavigationService _navigation;

    private string _quarantineSummary = "正在读取…";
    private string _scanHint = string.Empty;

    public DashboardViewModel(
        IQuarantineManager quarantine,
        ISettingsService settings,
        ILogService log,
        INavigationService navigation)
    {
        _quarantine = quarantine;
        _settings = settings;
        _log = log;
        _navigation = navigation;

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

    public string AdminText => AdminHelper.IsAdministrator
        ? "当前已以管理员身份运行，可完整读取注册表与系统信息。"
        : "当前未以管理员身份运行，建议右键以管理员身份启动本程序。";

    public string QuarantineRootText => _quarantine.QuarantineRoot;

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
            QuarantineSummary = $"隔离区共 {entries.Count} 项，占用 {FileSizeFormatter.Format(size)}，"
                              + $"保留 {_settings.Current.RetentionDays} 天（到期可自动清理）";
        }
        catch (Exception ex)
        {
            QuarantineSummary = "读取隔离区失败：" + ex.Message;
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

        ScanHint = "提示：孤儿软件扫描只做检测，不会删除任何文件；判定结果需要人工确认后再处理。";
    }

    [RelayCommand]
    private void GoToScan() => _navigation.Navigate("Orphan");

    [RelayCommand]
    private void GoToQuarantine() => _navigation.Navigate("Quarantine");

    [RelayCommand]
    private void GoToSettings() => _navigation.Navigate("Settings");
}

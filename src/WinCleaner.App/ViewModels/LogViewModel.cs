using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;

namespace WinCleaner.App.ViewModels;

/// <summary>日志页面：查看与导出操作日志。</summary>
public sealed partial class LogViewModel : ObservableObject
{
    private readonly ILogService _log;
    private readonly IReportExporter _exporter;
    private readonly IDialogService _dialogs;

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusText = string.Empty;

    public LogViewModel(ILogService log, IReportExporter exporter, IDialogService dialogs)
    {
        _log = log;
        _exporter = exporter;
        _dialogs = dialogs;

        Entries = new ObservableCollection<LogEntry>();
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = FilterEntry;

        _ = LoadAsync();
    }

    public ObservableCollection<LogEntry> Entries { get; }

    public ICollectionView EntriesView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) EntriesView.Refresh();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LogDirectory => _log.LogDirectory;

    public int EntryCount => Entries.Count;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var entries = await _log.LoadAsync();
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
            OnPropertyChanged(nameof(EntryCount));
            StatusText = $"共 {Entries.Count} 条日志，目录：{_log.LogDirectory}";
        }
        catch (Exception ex)
        {
            StatusText = "读取日志失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Entries.Count == 0)
        {
            _dialogs.Warning("没有可导出的日志。");
            return;
        }

        var path = _dialogs.SaveFile(
            "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
            $"WinCleaner-Log-{DateTime.Now:yyyyMMdd_HHmmss}.json");

        if (path == null) return;

        try
        {
            await _exporter.ExportCleanReportAsync(Entries, path);
            _dialogs.Info("已导出：" + path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("导出失败：" + ex.Message);
        }
    }

    private bool FilterEntry(object obj)
    {
        if (obj is not LogEntry entry) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var text = SearchText.Trim();
        return entry.Path.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.Action.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}

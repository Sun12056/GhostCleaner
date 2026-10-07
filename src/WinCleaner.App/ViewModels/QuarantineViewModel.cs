using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.App.ViewModels;

/// <summary>隔离区页面：查看、恢复、彻底删除、清空。</summary>
public sealed partial class QuarantineViewModel : ObservableObject
{
    private readonly IQuarantineManager _quarantine;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IReportExporter _exporter;
    private readonly IDialogService _dialogs;

    private QuarantineEntry? _selectedEntry;
    private bool _isBusy;
    private string _statusText = "隔离区中的条目可以恢复到原始位置，超过保留天数后可自动清理。";

    public QuarantineViewModel(
        IQuarantineManager quarantine,
        ISettingsService settings,
        ILogService log,
        IReportExporter exporter,
        IDialogService dialogs)
    {
        _quarantine = quarantine;
        _settings = settings;
        _log = log;
        _exporter = exporter;
        _dialogs = dialogs;

        Entries = new ObservableCollection<QuarantineEntry>();
        _ = RefreshAsync();
    }

    public ObservableCollection<QuarantineEntry> Entries { get; }

    public QuarantineEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
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

    public string QuarantineRootText => _quarantine.QuarantineRoot;

    public int EntryCount => Entries.Count;

    public long TotalSize => Entries.Sum(e => e.SizeBytes);

    public string Summary =>
        $"共 {Entries.Count} 项，占用 {FileSizeFormatter.Format(TotalSize)}，保留 {_settings.Current.RetentionDays} 天";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var entries = await _quarantine.ListAsync();
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(QuarantineRootText));
            StatusText = Summary;
        }
        catch (Exception ex)
        {
            StatusText = "读取隔离区失败：" + ex.Message;
            _log.Error("Quarantine", "List", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedEntry == null)
        {
            _dialogs.Warning("请先选择要恢复的条目。");
            return;
        }

        if (!_dialogs.Confirm($"确认把该条目恢复到原始位置？\n\n{SelectedEntry.OriginalPath}\n\n如果原位置已存在同名目录，恢复会失败。", "恢复确认"))
            return;

        IsBusy = true;
        try
        {
            var ok = await _quarantine.RestoreAsync(
                SelectedEntry.Id,
                new Progress<string>(s => StatusText = s));

            if (ok)
            {
                _log.Info("Quarantine", "Restore", "已恢复：" + SelectedEntry.OriginalPath, SelectedEntry.OriginalPath, SelectedEntry.SizeBytes);
                _dialogs.Info("恢复成功。");
            }
            else
            {
                _log.Warning("Quarantine", "Restore", "恢复失败：" + SelectedEntry.OriginalPath, SelectedEntry.OriginalPath);
                _dialogs.Warning("恢复失败，请检查原始位置是否已被占用。");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Quarantine", "Restore", ex.Message, SelectedEntry?.OriginalPath ?? "");
            _dialogs.Error("恢复失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedEntry == null)
        {
            _dialogs.Warning("请先选择要删除的条目。");
            return;
        }

        var entry = SelectedEntry;
        if (!_dialogs.ConfirmWithText(
                $"即将彻底删除隔离区中的条目（不可恢复）：\n\n{entry.OriginalPath}\n{FileSizeFormatter.Format(entry.SizeBytes)}",
                "彻底删除",
                "彻底删除"))
            return;

        IsBusy = true;
        try
        {
            var ok = await _quarantine.DeleteAsync(entry.Id);
            if (ok)
            {
                _log.Info("Quarantine", "Purge", "已彻底删除隔离条目：" + entry.OriginalPath, entry.OriginalPath, entry.SizeBytes);
                StatusText = "已彻底删除。";
            }
            else
            {
                _log.Warning("Quarantine", "Purge", "彻底删除失败：" + entry.OriginalPath, entry.OriginalPath);
                _dialogs.Warning("删除失败，可能有文件被占用。");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Quarantine", "Purge", ex.Message, entry.OriginalPath);
            _dialogs.Error("删除失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task PurgeExpiredAsync()
    {
        if (!_dialogs.Confirm($"确认清理已过保留期限（{_settings.Current.RetentionDays} 天）的隔离条目？", "清理过期条目", danger: true))
            return;

        IsBusy = true;
        try
        {
            var count = await _quarantine.PurgeExpiredAsync();
            _log.Info("Quarantine", "PurgeExpired", $"已清理 {count} 个过期隔离条目");
            _dialogs.Info($"已清理 {count} 个过期条目。");
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (Entries.Count == 0) return;

        if (!_dialogs.ConfirmWithText(
                $"即将清空整个隔离区（{Entries.Count} 项，{FileSizeFormatter.Format(TotalSize)}），此操作不可恢复！",
                "清空隔离区",
                "清空隔离区"))
            return;

        IsBusy = true;
        try
        {
            await _quarantine.ClearAsync();
            _log.Info("Quarantine", "Clear", "已清空隔离区");
        }
        catch (Exception ex)
        {
            _log.Error("Quarantine", "Clear", ex.Message);
            _dialogs.Error("清空失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private void OpenQuarantineFolder()
    {
        try
        {
            var root = _quarantine.QuarantineRoot;
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", root);
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Entries.Count == 0)
        {
            _dialogs.Warning("隔离区为空。");
            return;
        }

        var path = _dialogs.SaveFile(
            "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
            $"WinCleaner-Quarantine-{DateTime.Now:yyyyMMdd_HHmmss}.json");

        if (path == null) return;

        await _exporter.ExportQuarantineReportAsync(Entries, path);
        _dialogs.Info("已导出：" + path);
    }
}

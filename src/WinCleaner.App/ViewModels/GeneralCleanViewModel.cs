using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Utils;
using WinCleaner.Services.Cleanup;

namespace WinCleaner.App.ViewModels;

public sealed class JunkItem : ObservableObject
{
    private bool _isSelected;

    public JunkScanResult Result { get; init; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>常规清理页（临时文件 / 更新缓存 / 缩略图缓存 / 崩溃转储 等）。</summary>
public sealed partial class GeneralCleanViewModel : ObservableObject
{
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;

    private bool _isBusy;
    private string _statusText = "点击“开始扫描”统计各类垃圾文件的大小。默认不会删除任何东西。";

    public GeneralCleanViewModel(ILogService log, IDialogService dialogs)
    {
        _log = log;
        _dialogs = dialogs;
        Items = new ObservableCollection<JunkItem>();
    }

    public ObservableCollection<JunkItem> Items { get; }

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

    public long SelectedSize => Items.Where(i => i.IsSelected).Sum(i => i.Result.SizeBytes);

    public string SelectedSummary =>
        $"已勾选 {Items.Count(i => i.IsSelected)} 项，共 {FileSizeFormatter.Format(SelectedSize)}";

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "正在扫描…";
        Items.Clear();

        try
        {
            var results = await JunkScanner.ScanAsync(
                JunkScanner.Categories,
                new Progress<string>(s => StatusText = s));

            foreach (var result in results) Items.Add(new JunkItem { Result = result });

            var total = results.Sum(r => r.SizeBytes);
            StatusText = $"扫描完成：共 {FileSizeFormatter.Format(total)}。勾选需要清理的项目后再执行清理。";
            _log.Info("General", "Scan", "常规清理扫描完成，共 " + FileSizeFormatter.Format(total));
            OnPropertyChanged(nameof(SelectedSummary));
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
            _log.Error("General", "Scan", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Warning("请先勾选要清理的项目。");
            return;
        }

        var message = $"即将清理 {selected.Count} 类垃圾文件，预计释放 {FileSizeFormatter.Format(SelectedSize)}。\n"
                    + "被占用的文件会自动跳过。\n\n是否继续？";

        if (!_dialogs.Confirm(message, "常规清理确认", danger: true)) return;

        IsBusy = true;
        int files = 0;
        long bytes = 0;

        try
        {
            foreach (var item in selected)
            {
                StatusText = "正在清理：" + item.Result.Name;
                var (f, b) = await JunkScanner.CleanAsync(
                    item.Result,
                    new Progress<string>(s => StatusText = s));

                files += f;
                bytes += b;
                _log.Info("General", "Clean", $"{item.Result.Name}：删除 {f} 个文件，释放 {FileSizeFormatter.Format(b)}",
                    item.Result.Path, b);
            }

            StatusText = $"清理完成：删除 {files} 个文件，释放 {FileSizeFormatter.Format(bytes)}。";
            _dialogs.Info($"清理完成：删除 {files} 个文件，释放 {FileSizeFormatter.Format(bytes)}。");
            await ScanAsync();
        }
        catch (Exception ex)
        {
            StatusText = "清理失败：" + ex.Message;
            _log.Error("General", "Clean", ex.Message);
            _dialogs.Error("清理失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFolder(JunkItem? item)
    {
        if (item == null || !Directory.Exists(item.Result.Path)) return;
        try
        {
            Process.Start("explorer.exe", item.Result.Path);
        }
        catch
        {
            // 忽略
        }
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Utils;
using WinCleaner.Services.Analysis;

namespace WinCleaner.App.ViewModels;

public sealed class DriveItem
{
    public string Root { get; init; } = string.Empty;

    public string Display { get; init; } = string.Empty;

    public override string ToString() => Display;
}

/// <summary>磁盘分析页：大文件扫描（后续可扩展重复文件、空间占用分析）。</summary>
public sealed partial class DiskAnalysisViewModel : ObservableObject
{
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    private DriveItem? _selectedDrive;
    private long _minSizeBytes = 100L * 1024 * 1024;
    private bool _isBusy;
    private string _statusText = "选择磁盘后点击“扫描大文件”。扫描只读取信息，不会删除任何文件。";
    private LargeFileEntry? _selectedFile;

    public DiskAnalysisViewModel(ILogService log, IDialogService dialogs, ISettingsService settings)
    {
        _log = log;
        _dialogs = dialogs;
        _settings = settings;

        Files = new ObservableCollection<LargeFileEntry>();
        Drives = new ObservableCollection<DriveItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                Drives.Add(new DriveItem
                {
                    Root = drive.RootDirectory.FullName,
                    Display = $"{drive.RootDirectory.FullName} 可用 {FileSizeFormatter.Format(drive.AvailableFreeSpace)} / {FileSizeFormatter.Format(drive.TotalSize)}",
                });
            }
            catch
            {
                // 忽略
            }
        }

        _selectedDrive = Drives.FirstOrDefault();
    }

    public ObservableCollection<DriveItem> Drives { get; }

    public ObservableCollection<LargeFileEntry> Files { get; }

    public DriveItem? SelectedDrive
    {
        get => _selectedDrive;
        set => SetProperty(ref _selectedDrive, value);
    }

    public LargeFileEntry? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public long MinSizeBytes
    {
        get => _minSizeBytes;
        set => SetProperty(ref _minSizeBytes, value);
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

    public string TotalText => FileSizeFormatter.Format(Files.Sum(f => f.SizeBytes));

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (SelectedDrive == null)
        {
            _dialogs.Warning("请选择要分析的磁盘。");
            return;
        }

        IsBusy = true;
        Files.Clear();
        StatusText = "正在扫描大文件…";

        try
        {
            var files = await LargeFileScanner.ScanAsync(
                SelectedDrive.Root,
                topCount: 200,
                minSizeBytes: MinSizeBytes,
                progress: new Progress<string>(s => StatusText = s));

            foreach (var file in files) Files.Add(file);

            OnPropertyChanged(nameof(TotalText));
            StatusText = $"扫描完成：找到 {Files.Count} 个大于 {FileSizeFormatter.Format(MinSizeBytes)} 的文件，合计 {TotalText}。";
            _log.Info("Disk", "Scan", $"大文件扫描完成：{Files.Count} 个，合计 {TotalText}", SelectedDrive.Root);
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消。";
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
            _log.Error("Disk", "Scan", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (SelectedFile == null) return;
        try
        {
            Process.Start("explorer.exe", $"/select,\"{SelectedFile.Path}\"");
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private void DeleteToRecycleBin()
    {
        if (SelectedFile == null) return;

        var file = SelectedFile;
        if (!_dialogs.Confirm(
                $"确认把该文件删除到回收站？\n\n{file.Path}\n{file.SizeText}",
                "删除确认",
                danger: true))
            return;

        if (RecycleBin.Send(file.Path))
        {
            Files.Remove(file);
            OnPropertyChanged(nameof(TotalText));
            _log.Info("Disk", "RecycleBin", "已删除到回收站：" + file.Path, file.Path, file.SizeBytes);
            StatusText = "已删除到回收站：" + file.Path;
        }
        else
        {
            _log.Error("Disk", "RecycleBin", "删除到回收站失败：" + file.Path, file.Path);
            _dialogs.Error("删除失败，文件可能被占用。");
        }
    }
}

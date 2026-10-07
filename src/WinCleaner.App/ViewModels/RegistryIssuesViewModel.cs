using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinCleaner.App.Services;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Registry;

namespace WinCleaner.App.ViewModels;

/// <summary>启动项 / 无效注册表项：扫描并在备份后删除。</summary>
public sealed partial class RegistryIssuesViewModel : ObservableObject
{
    private readonly IRegistryCleaner _cleaner;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;

    private bool _isBusy;
    private string _statusText = "点击“扫描无效项”。扫描只读取注册表，不会修改任何内容。";

    public RegistryIssuesViewModel(IRegistryCleaner cleaner, ILogService log, IDialogService dialogs)
    {
        _cleaner = cleaner;
        _log = log;
        _dialogs = dialogs;
        Issues = new ObservableCollection<RegistryIssue>();
    }

    public ObservableCollection<RegistryIssue> Issues { get; }

    /// <summary>Beta 提示：注册表清理在 v0.1 不是稳定功能。</summary>
    public string FeatureNotice => InvalidRegistryScanner.FeatureNotice;

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

    public int SelectedCount => Issues.Count(i => i.IsSelected);

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsBusy = true;
        Issues.Clear();
        StatusText = "正在扫描注册表…";

        try
        {
            var issues = await _cleaner.ScanAsync(new Progress<string>(s => StatusText = s));
            foreach (var issue in issues) Issues.Add(issue);

            StatusText = $"扫描完成：发现 {Issues.Count} 个可疑/无效注册表项。默认未勾选，取消前请先人工确认。";
            _log.Info("Registry", "Scan", $"注册表扫描完成：{Issues.Count} 项");
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
            _log.Error("Registry", "Scan", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = Issues.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Warning("请先勾选要清理的注册表项。");
            return;
        }

        if (!_dialogs.ConfirmWithText(
                $"即将删除 {selected.Count} 个注册表项。\n删除前会自动用 reg export 备份到数据目录。\n\n"
                + "修改注册表有风险，请确保已理解每一项的用途。",
                "删除注册表项",
                "删除注册表项"))
            return;

        IsBusy = true;
        StatusText = "正在备份并删除…";

        try
        {
            var removed = await _cleaner.RemoveAsync(selected);
            _log.Info("Registry", "Remove", $"已删除 {removed} 个注册表项");
            _dialogs.Info($"已删除 {removed} 个注册表项，备份已保存到数据目录。");
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Registry", "Remove", ex.Message);
            _dialogs.Error("删除失败：" + ex.Message);
            StatusText = "删除失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackupSelectedAsync()
    {
        var selected = Issues.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Warning("请先勾选要备份的注册表项。");
            return;
        }

        IsBusy = true;
        try
        {
            var dir = await _cleaner.BackupAsync(selected);
            _log.Info("Registry", "Backup", "注册表备份完成：" + dir);
            _dialogs.Info("备份已保存到：\n" + dir);
            StatusText = "备份目录：" + dir;
        }
        catch (Exception ex)
        {
            _log.Error("Registry", "Backup", ex.Message);
            _dialogs.Error("备份失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

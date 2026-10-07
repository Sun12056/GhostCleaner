using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using WinCleaner.App.Helpers;
using WinCleaner.App.Services;

namespace WinCleaner.App.ViewModels;

public sealed record NavItem(string Key, string Name);

/// <summary>主窗口 ViewModel：左侧导航 + 内容切换。</summary>
public sealed class MainViewModel : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Func<ObservableObject>> _factories;
    private NavItem? _selectedItem;
    private ObservableObject? _currentView;

    public MainViewModel(IServiceProvider services)
    {
        _services = services;

        NavItems = new ObservableCollection<NavItem>
        {
            new("Dashboard", "仪表盘"),
            new("Orphan", "孤儿软件扫描"),
            new("General", "常规清理"),
            new("Disk", "磁盘分析"),
            new("Startup", "启动项 / 注册表"),
            new("Quarantine", "隔离区"),
            new("Logs", "日志"),
            new("Settings", "设置"),
        };

        _factories = new Dictionary<string, Func<ObservableObject>>
        {
            ["Dashboard"] = () => _services.GetRequiredService<DashboardViewModel>(),
            ["Orphan"] = () => _services.GetRequiredService<OrphanScanViewModel>(),
            ["General"] = () => _services.GetRequiredService<GeneralCleanViewModel>(),
            ["Disk"] = () => _services.GetRequiredService<DiskAnalysisViewModel>(),
            ["Startup"] = () => _services.GetRequiredService<RegistryIssuesViewModel>(),
            ["Quarantine"] = () => _services.GetRequiredService<QuarantineViewModel>(),
            ["Logs"] = () => _services.GetRequiredService<LogViewModel>(),
            ["Settings"] = () => _services.GetRequiredService<SettingsViewModel>(),
        };

        _selectedItem = NavItems[0];
        _currentView = _factories[_selectedItem.Key]();
    }

    public ObservableCollection<NavItem> NavItems { get; }

    public NavItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem == value || value == null) return;
            _selectedItem = value;
            OnPropertyChanged();
            CurrentView = _factories[value.Key]();
        }
    }

    public ObservableObject? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public string AdminText => AdminHelper.IsAdministrator
        ? "已以管理员身份运行"
        : "未以管理员身份运行，部分操作可能失败";

    public void Navigate(string pageKey)
    {
        var item = NavItems.FirstOrDefault(x => x.Key == pageKey);
        if (item != null) SelectedItem = item;
    }
}

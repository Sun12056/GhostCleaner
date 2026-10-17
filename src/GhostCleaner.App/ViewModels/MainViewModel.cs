using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using GhostCleaner.App.Helpers;
using GhostCleaner.App.Services;

namespace GhostCleaner.App.ViewModels;

/// <summary>导航项。IconKey 指向 Icons/IconSet.xaml 中的资源键。</summary>
public sealed record NavItem(string Key, string Name, string IconKey);

/// <summary>主窗口 ViewModel：侧栏导航 + 内容切换（UI 0.2 信息架构）。</summary>
public sealed class MainViewModel : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Func<ObservableObject>> _factories;

    // 扫描页在导航切换时保留实例，避免切换页面丢失扫描结果与进度
    private OrphanScanViewModel? _orphanViewModel;

    private NavItem? _selectedItem;
    private ObservableObject? _currentView;

    public MainViewModel(IServiceProvider services)
    {
        _services = services;

        NavItems = new ObservableCollection<NavItem>
        {
            new("Dashboard", "概览", "Icon.Dashboard"),
            new("Orphan", "残留扫描", "Icon.Scan"),
            new("Results", "扫描结果", "Icon.Results"),
            new("Quarantine", "隔离区", "Icon.Quarantine"),
            new("History", "扫描记录", "Icon.History"),
            new("Settings", "设置", "Icon.Settings"),
            new("About", "关于", "Icon.Info"),
        };

        _factories = new Dictionary<string, Func<ObservableObject>>
        {
            ["Dashboard"] = () => _services.GetRequiredService<DashboardViewModel>(),
            ["Orphan"] = () => { var vm = GetOrphan(); vm.Stage = ScanStage.Setup; return vm; },
            ["Results"] = () => { var vm = GetOrphan(); vm.Stage = ScanStage.Results; return vm; },
            ["Quarantine"] = () => _services.GetRequiredService<QuarantineViewModel>(),
            ["History"] = () => _services.GetRequiredService<LogViewModel>(),
            ["Settings"] = () => _services.GetRequiredService<SettingsViewModel>(),
            ["About"] = () => _services.GetRequiredService<AboutViewModel>(),
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
        : "未以管理员身份运行";

    public bool IsAdministrator => AdminHelper.IsAdministrator;

    public void Navigate(string pageKey)
    {
        var item = NavItems.FirstOrDefault(x => x.Key == pageKey);
        if (item != null) SelectedItem = item;
    }

    private OrphanScanViewModel GetOrphan()
        => _orphanViewModel ??= _services.GetRequiredService<OrphanScanViewModel>();
}

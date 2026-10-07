using WinCleaner.App.Services;

namespace WinCleaner.App.ViewModels;

/// <summary>
/// 导航服务：把 MainViewModel 的导航能力对外暴露。
/// 独立成类是为了避免 MainViewModel → 页面 VM → INavigationService → MainViewModel 的构造循环。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private MainViewModel? _main;

    public void Attach(MainViewModel main) => _main = main;

    public void Navigate(string pageKey) => _main?.Navigate(pageKey);
}

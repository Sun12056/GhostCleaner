using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using GhostCleaner.App.Services;

namespace GhostCleaner.App.Views;

/// <summary>
/// Toast 的呈现宿主。全局单例服务负责产生 Toast，本控件只负责显示。
/// </summary>
public partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();

        // Toast 是全局服务，不参与页面导航的 DataContext 流转，因此宿主自行取服务。
        DataContext = App.Services.GetService<IToastService>();
    }
}

using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WinCleaner.App.Services;
using WinCleaner.App.ViewModels;
using WinCleaner.Core.Interfaces;
using WinCleaner.Registry;
using WinCleaner.Scanner;
using WinCleaner.Services.Cleanup;
using WinCleaner.Services.Logging;
using WinCleaner.Services.Quarantine;
using WinCleaner.Services.Reports;
using WinCleaner.Services.Settings;
using WinCleaner.Services.SystemTools;

namespace WinCleaner.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            var log = Services.GetRequiredService<ILogService>();
            log.Info("System", "Startup", $"WinCleaner 启动；管理员权限：{Helpers.AdminHelper.IsAdministrator}");

            DispatcherUnhandledException += (_, args) =>
            {
                log.Error("System", "UnhandledException", args.Exception.Message);
                MessageBox.Show(
                    "发生未处理异常：\n" + args.Exception,
                    "WinCleaner",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            // 先把 MainViewModel 挂到导航服务上（必须在解析页面 ViewModel 之前）
            Services.GetRequiredService<NavigationService>()
                    .Attach(Services.GetRequiredService<MainViewModel>());

            var window = Services.GetRequiredService<MainWindow>();
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            try { Services?.GetService<ILogService>()?.Error("System", "StartupFailed", ex.ToString()); } catch { }
            MessageBox.Show("启动失败：\n" + ex, "WinCleaner", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // —— 基础服务（单例）——
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IQuarantineManager, QuarantineManager>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<IReportExporter, ReportExporter>();
        services.AddSingleton<IRegistryCleaner>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var baseDir = Path.GetDirectoryName(settings.SettingsFilePath) ?? AppContext.BaseDirectory;
            return new InvalidRegistryScanner(Path.Combine(baseDir, "Backups"));
        });
        services.AddSingleton<CleanupService>();

        // —— 扫描相关（每次扫描用新实例，保证关联索引重建）——
        services.AddTransient<ISoftwareDirectoryInspector, SoftwareDirectoryInspector>();
        services.AddTransient<IAssociationIndex, AssociationIndex>();
        services.AddTransient<IOrphanScanner, OrphanScanner>();

        // —— UI ——
        services.AddSingleton<IDialogService, DialogService>();

        // 导航服务独立注册，打破 MainViewModel 与页面 ViewModel 的构造循环
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<OrphanScanViewModel>();
        services.AddTransient<GeneralCleanViewModel>();
        services.AddTransient<DiskAnalysisViewModel>();
        services.AddTransient<RegistryIssuesViewModel>();
        services.AddTransient<QuarantineViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}

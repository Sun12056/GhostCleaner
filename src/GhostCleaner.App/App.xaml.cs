using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using GhostCleaner.App.Services;
using GhostCleaner.App.ViewModels;
using GhostCleaner.App.Views;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Registry;
using GhostCleaner.Scanner;
using GhostCleaner.Services.Cleanup;
using GhostCleaner.Services.Logging;
using GhostCleaner.Services.Quarantine;
using GhostCleaner.Services.Reports;
using GhostCleaner.Services.Settings;
using GhostCleaner.Services.SystemTools;

namespace GhostCleaner.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>显示名。UDR-11：命名空间重命名前，UI 层统一使用 GhostCleaner。</summary>
    public const string DisplayName = "GhostCleaner";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // 主题必须在创建窗口前应用（颜色资源全部走 DynamicResource）
            var theme = Services.GetRequiredService<IThemeService>();
            theme.Apply(theme.Mode);

            var log = Services.GetRequiredService<ILogService>();
            log.Info("System", "Startup", $"{DisplayName} 启动；管理员权限：{Helpers.AdminHelper.IsAdministrator}");

            DispatcherUnhandledException += (_, args) =>
            {
                log.Error("System", "UnhandledException", args.Exception.Message);
                Services.GetRequiredService<IDialogService>()
                        .Error("发生未处理异常：\n" + args.Exception, DisplayName);
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

            // WinExe 没有控制台，启动崩溃时异常文本只存在于那个对话框里，
            // 用户没法复制给我们。落盘一份，便于事后取证。
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "startup-error.txt"),
                    ex.ToString());
            }
            catch { }
            // 走到这里时 Services 多半还没构建好，不能依赖 DI —— 直接构造对话框。
            new MessageDialogWindow("启动失败：\n" + ex, DisplayName, MessageDialogKind.Error, false)
                .ShowDialog();
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

        // —— 扫描相关 ——
        services.AddTransient<ISoftwareDirectoryInspector, SoftwareDirectoryInspector>();

        // 单例：构建关联索引要读取注册表/服务/进程/文件关联等 9 类来源，
        // 成本不低（真机实测 13k+ 条、约 7 秒），单例可避免同一会话内重复构建。
        // OrphanScanner 每次扫描都会 BuildAsync() 先清空再重建，SourceHealth 有 lock，复用安全。
        // 注意：这不为"UI 要读同一个实例的健康度"—— 健康度已由 ScanOutcome 随扫描结果一起返回，
        // UI 侧（OrphanScanViewModel）已不再依赖 IAssociationIndex。
        services.AddSingleton<IAssociationIndex, AssociationIndex>();

        services.AddTransient<IOrphanScanner, OrphanScanner>();

        // —— UI ——
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IThemeService, ThemeService>();
        // UI 层轻量状态（最近一次扫描摘要）。UDR-02 落地后改由 Core 提供。
        services.AddSingleton<UiStateService>();

        // 导航服务独立注册，打破 MainViewModel 与页面 ViewModel 的构造循环
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<OrphanScanViewModel>();
        services.AddTransient<QuarantineViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        // 以下页面已在 UI 0.1 中移出信息架构（代码保留，不再注册到导航）
        services.AddTransient<GeneralCleanViewModel>();
        services.AddTransient<DiskAnalysisViewModel>();
        services.AddTransient<RegistryIssuesViewModel>();
    }
}

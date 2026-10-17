using System.Windows;
using GhostCleaner.App.Services;
using GhostCleaner.Core.Interfaces;

namespace GhostCleaner.App.Helpers;

/// <summary>
/// 以管理员身份重启本程序（统一入口，供概览 / 残留扫描 / 扫描结果等页面复用）。
///
/// 存在的理由（Core 真机数据）：未提权时运行中的进程采集不全（193 → 356，+84%）、
/// 计划任务基本采集不到（0 → 101）。后果不只是"结果不完整"，而是
/// 正在使用的软件会缺少「运行中进程」这条保护信号，从而更容易被判成残留 —— 真实的误删风险。
/// </summary>
public static class AdminRestart
{
    public static void Request(IDialogService dialogs, ILogService log)
    {
        if (AdminHelper.IsAdministrator) return;

        if (!dialogs.Confirm(
                "将以管理员身份重新启动 GhostCleaner。\n\n"
              + "提权后可完整读取运行中的进程与计划任务，正在使用的软件不会被误判为残留。\n\n"
              + "当前窗口会关闭，需要重新扫描一次。\n\n是否立即重启？",
                "以管理员身份重启"))
        {
            return;
        }

        log.Info("System", "RestartAsAdmin", "用户请求以管理员身份重启");

        if (!AdminHelper.TryRestartAsAdministrator())
        {
            dialogs.Warning("未能以管理员身份重启。\n请右键点击程序图标，选择「以管理员身份运行」。");
            return;
        }

        Application.Current.Shutdown();
    }
}

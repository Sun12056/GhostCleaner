using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace GhostCleaner.App.Helpers;

/// <summary>管理员权限检测与提权重启。</summary>
public static class AdminHelper
{
    public static bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 以管理员权限重新启动本程序（触发 UAC 弹窗）。
    /// 用户在 UAC 上点"否"会抛 Win32Exception，返回 false —— 这是正常取消，不是错误。
    /// </summary>
    public static bool TryRestartAsAdministrator()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
            });

            return true;
        }
        catch (Win32Exception)
        {
            // 用户取消了 UAC 提权
            return false;
        }
        catch
        {
            return false;
        }
    }
}

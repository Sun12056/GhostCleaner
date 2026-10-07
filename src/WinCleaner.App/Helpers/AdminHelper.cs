using System.Security.Principal;

namespace WinCleaner.App.Helpers;

/// <summary>管理员权限检测。</summary>
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
}

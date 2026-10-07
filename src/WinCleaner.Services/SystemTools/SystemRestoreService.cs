using System.Management;
using WinCleaner.Core.Interfaces;

namespace WinCleaner.Services.SystemTools;

/// <summary>通过 WMI 创建系统还原点（失败不影响主流程）。</summary>
public sealed class SystemRestoreService : ISystemRestoreService
{
    public async Task<bool> TryCreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                var path = new ManagementPath("SystemRestore");
                using var restoreClass = new ManagementClass(scope, path, new ObjectGetOptions());

                var parameters = restoreClass.GetMethodParameters("CreateRestorePoint");
                parameters["Description"] = description;
                parameters["RestorePointType"] = 12;   // MODIFY_SETTINGS
                parameters["EventType"] = 100;         // BEGIN_SYSTEM_CHANGE

                var result = restoreClass.InvokeMethod("CreateRestorePoint", parameters, null);
                return result != null && Convert.ToInt32(result["ReturnValue"]) == 0;
            }
            catch
            {
                // 系统保护未开启 / 无权限 / WMI 不可用时静默失败
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}

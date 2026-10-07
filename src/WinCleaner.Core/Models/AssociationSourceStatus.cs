namespace WinCleaner.Core.Models;

/// <summary>
/// 单个关联扫描源的运行结果。
/// 关键语义：<b>Failed / Partial 绝不能被当成"没有引用"</b>。
/// </summary>
public enum AssociationSourceStatus
{
    /// <summary>尚未运行。</summary>
    NotRun = 0,

    /// <summary>扫描完成，结果可信。</summary>
    Success = 1,

    /// <summary>扫描失败（抛出异常），该来源的"缺失"结论不可用。</summary>
    Failed = 2,

    /// <summary>扫描完成但结果可能不完整（采集到 0 条记录或中途出错）。</summary>
    Partial = 3,
}

/// <summary>扫描源的中文名（集中管理，避免 UI 与算法各写一份）。</summary>
public static class AssociationSourceText
{
    public static string ToText(AssociationSource source) => source switch
    {
        AssociationSource.RegistryUninstall => "注册表卸载项",
        AssociationSource.AppPaths => "App Paths",
        AssociationSource.StartMenuShortcut => "开始菜单快捷方式",
        AssociationSource.DesktopShortcut => "桌面快捷方式",
        AssociationSource.StartupEntry => "启动项",
        AssociationSource.WindowsService => "Windows 服务",
        AssociationSource.ScheduledTask => "计划任务",
        AssociationSource.RunningProcess => "运行中的进程",
        AssociationSource.AppxPackage => "Microsoft Store 应用",
        AssociationSource.FileAssociation => "文件关联/右键菜单",
        _ => "其它",
    };

    public static string ToText(AssociationSourceStatus status) => status switch
    {
        AssociationSourceStatus.Success => "成功",
        AssociationSourceStatus.Failed => "失败",
        AssociationSourceStatus.Partial => "不完整",
        _ => "未运行",
    };
}

namespace WinCleaner.Core.Models;

/// <summary>风险等级。判定规则保守：宁可标高风险，也不误删。</summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
}

/// <summary>清理动作类型。</summary>
public enum CleanActionType
{
    /// <summary>移动到隔离区（默认，可恢复）。</summary>
    Quarantine = 0,

    /// <summary>删除到回收站。</summary>
    RecycleBin = 1,

    /// <summary>强制永久删除（需二次确认并输入确认文字）。</summary>
    PermanentDelete = 2,
}

/// <summary>"系统当前仍有关联"的证据来源。</summary>
public enum AssociationSource
{
    RegistryUninstall,   // 注册表卸载项（HKLM/HKCU，32/64 位）
    AppPaths,            // HKLM\...\App Paths
    StartMenuShortcut,   // 开始菜单快捷方式
    DesktopShortcut,     // 桌面快捷方式
    StartupEntry,        // 启动项（Run/RunOnce/启动目录）
    WindowsService,      // Windows 服务
    ScheduledTask,       // 计划任务
    RunningProcess,      // 正在运行的进程
    AppxPackage,         // Microsoft Store / AppX 包
    FileAssociation,     // 文件关联 / 右键菜单 / COM 注册
    Other,
}

/// <summary>日志级别。</summary>
public enum LogLevel
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Success = 3,
}

/// <summary>隔离区条目状态。</summary>
public enum QuarantineStatus
{
    Quarantined = 0,
    Restored = 1,
    Purged = 2,
    Failed = 3,
}

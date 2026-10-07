namespace WinCleaner.Core.Models;

/// <summary>
/// 证据类型。
/// - "正向证据" 支持"它可能是软件残留"这一假设；
/// - "负向证据" 是否定该假设的保护性信号（绿色软件 / 游戏 / 开发项目 / 用户数据 / 正在使用 …）。
/// 注意：<see cref="MissingUninstallEntry"/> 这类"缺少关联"的证据只有在对应扫描源
/// <b>成功完成</b>时才会生成，扫描失败只能产生 <see cref="AssociationSourceFailed"/> 与 Warning。
/// </summary>
public enum EvidenceType
{
    // —— 软件特征（正向：它确实像一个被"安装"出来的软件目录）——
    SoftwareDirectory,
    Executable,
    Library,
    Uninstaller,
    VersionResource,
    Publisher,

    // —— 系统关联仍在（负向：Windows 还记得它）——
    RegistryAssociation,
    AppPath,
    Shortcut,
    Service,
    ScheduledTask,
    StartupEntry,
    FileAssociation,
    AppxPackage,
    RunningProcess,

    // —— 系统关联缺失（正向，但只有在对应来源扫描成功时才成立）——
    MissingUninstallEntry,
    MissingAppPath,
    MissingShortcut,
    MissingService,
    MissingScheduledTask,
    MissingStartupEntry,

    // —— 使用状况（正向）——
    LongUnmodified,
    NoRunningProcess,

    // —— 目录性质（负向：这些目录天然就可能没有系统关联）——
    PortableSoftware,
    GameDirectory,
    DevelopmentProject,

    // —— 用户数据（负向，强保护）——
    UserData,
    SaveData,
    ProfileData,
    Documents,
    BackupData,
    Configuration,

    // —— 近期活动（负向）——
    RecentModification,
    RecentUsage,

    // —— 路径安全（负向，强保护）——
    SystemDirectory,
    CriticalPath,
    ReparsePoint,

    // —— 扫描健康度（既降低置信度，也提高清理风险）——
    AssociationSourceFailed,
}

namespace WinCleaner.Core.Models;

/// <summary>
/// 全部评分权重集中在此。禁止在算法中散落魔法数字。
/// 约定：正值 = 支持"这是软件残留"；负值 = 保护性/否定性证据。
/// 这是 v0.1 的初始模型，允许直接调参。
/// </summary>
public sealed class EvidenceWeights
{
    public static EvidenceWeights Default { get; } = new();

    // ================= 正向：软件特征 =================

    /// <summary>目录具备"软件安装目录"的整体特征。</summary>
    public int SoftwareDirectory { get; init; } = 8;

    public int Executable { get; init; } = 10;

    public int Library { get; init; } = 5;

    public int Uninstaller { get; init; } = 25;

    public int VersionResource { get; init; } = 10;

    public int Publisher { get; init; } = 10;

    // ================= 正向：系统关联缺失（仅在来源扫描成功时计入） =================

    public int MissingUninstallEntry { get; init; } = 20;
    public int MissingAppPath { get; init; } = 8;
    public int MissingShortcut { get; init; } = 8;
    public int MissingService { get; init; } = 8;
    public int MissingScheduledTask { get; init; } = 6;
    public int MissingStartupEntry { get; init; } = 6;

    // ================= 正向：使用状况 =================

    public int LongUnmodified { get; init; } = 12;
    public int NoRunningProcess { get; init; } = 12;

    /// <summary>超过该天数未修改才算"长期未修改"。</summary>
    public int LongUnmodifiedDays { get; init; } = 180;

    // ================= 负向：系统关联仍在 =================

    /// <summary>发现任一系统引用时会追加的否定权重（按来源累加，但会被 <see cref="MaxAssociationPenalty"/> 截断）。</summary>
    public int AssociationFound { get; init; } = -40;

    public int MaxAssociationPenalty { get; init; } = -60;

    public int RunningProcessFound { get; init; } = -80;

    // ================= 负向：目录性质保护 =================

    public int PortableSoftwarePenalty { get; init; } = -45;
    public int GameDirectoryPenalty { get; init; } = -50;
    public int DevelopmentProjectPenalty { get; init; } = -50;

    // ================= 负向：用户数据保护 =================

    public int UserDataPenalty { get; init; } = -30;
    public int SaveDataPenalty { get; init; } = -50;
    public int ProfileDataPenalty { get; init; } = -40;
    public int DocumentsPenalty { get; init; } = -50;
    public int BackupDataPenalty { get; init; } = -40;
    public int ConfigurationPenalty { get; init; } = -30;

    // ================= 负向：近期活动 =================

    public int RecentModificationPenalty { get; init; } = -30;
    public int RecentUsagePenalty { get; init; } = -15;

    /// <summary>7 天内修改过 = 近期修改。</summary>
    public int RecentModificationDays { get; init; } = 7;

    /// <summary>30 天内修改过 = 近期使用。</summary>
    public int RecentUsageDays { get; init; } = 30;

    // ================= 负向：路径安全 =================

    public int CriticalPathPenalty { get; init; } = -100;
    public int SystemInstallRootPenalty { get; init; } = -30;
    public int SystemDrivePenalty { get; init; } = -10;
    public int ReparsePointPenalty { get; init; } = -15;

    // ================= 扫描健康度 =================

    /// <summary>每个失败/不完整的来源对置信度的惩罚。</summary>
    public int SourceFailurePenalty { get; init; } = -8;

    /// <summary>注册表卸载项失败时的额外惩罚（它是最关键的来源）。</summary>
    public int RegistrySourceFailurePenalty { get; init; } = -30;

    /// <summary>任一来源失败时，置信度上限。</summary>
    public int MaxConfidenceWhenAnySourceFailed { get; init; } = 65;

    /// <summary>注册表来源不可用时的置信度上限。</summary>
    public int MaxConfidenceWhenRegistryUnavailable { get; init; } = 55;

    /// <summary>大量来源失败时的置信度上限。</summary>
    public int MaxConfidenceWhenManySourcesFailed { get; init; } = 45;

    /// <summary>达到该数量即视为"大量失败"。</summary>
    public int ManySourcesFailedThreshold { get; init; } = 3;

    // ================= "安装痕迹"系数 =================
    // 缺少系统关联只有在目录确实像"被安装出来的软件"时才有说服力。
    // 绿色软件 / 游戏 / 开发项目天然没有安装痕迹，不应因此被加分。

    public double FootprintUninstaller { get; init; } = 0.35;
    public double FootprintVersionResource { get; init; } = 0.15;
    public double FootprintPublisher { get; init; } = 0.10;
    public double FootprintLibraries { get; init; } = 0.15;
    public double FootprintFileCount { get; init; } = 0.10;
    public double FootprintSize { get; init; } = 0.15;

    /// <summary>安装痕迹系数下限（保证"缺失关联"永远只是一小部分证据，而不是结论）。</summary>
    public double MinInstallationFootprint { get; init; } = 0.15;

    public int FootprintLibraryThreshold { get; init; } = 3;
    public int FootprintFileCountThreshold { get; init; } = 20;
    public long FootprintSizeThresholdBytes { get; init; } = 10L * 1024 * 1024;

    // ================= 清理风险评分 =================
    // 与置信度完全独立：回答"如果清理它，风险有多大"。

    public int RiskCriticalPath { get; init; } = 90;
    public int RiskSystemDrive { get; init; } = 40;
    public int RiskReparsePoint { get; init; } = 15;
    /// <summary>目录正在被使用时删除风险极高（文件被占用、状态不可预期）。</summary>
    public int RiskRunningProcess { get; init; } = 80;
    public int RiskUserData { get; init; } = 45;
    public int RiskRecentlyModifiedDays { get; init; } = 90;
    public int RiskRecentlyModified { get; init; } = 40;
    public int RiskNoUninstaller { get; init; } = 10;
    public int RiskProtectedContent { get; init; } = 40;
    public int RiskUnreliableAssociationData { get; init; } = 80;

    public int RiskHighThreshold { get; init; } = 80;
    public int RiskMediumThreshold { get; init; } = 35;
}

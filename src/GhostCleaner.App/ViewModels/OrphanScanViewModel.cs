using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostCleaner.App.Helpers;
using GhostCleaner.App.Services;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Core.Utils;
using GhostCleaner.Services.Cleanup;

namespace GhostCleaner.App.ViewModels;

public sealed class DriveOption : ObservableObject
{
    private bool _isSelected;

    public string Root { get; init; } = string.Empty;

    public long FreeBytes { get; init; }

    public long TotalBytes { get; init; }

    public bool IsSystemDrive { get; init; }

    public string Display =>
        $"{Root}  可用 {FileSizeFormatter.Format(FreeBytes)} / 共 {FileSizeFormatter.Format(TotalBytes)}"
        + (IsSystemDrive ? "（系统盘）" : string.Empty);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public enum RiskFilterOption
{
    All,
    Low,
    Medium,
    High,
}

/// <summary>
/// 扫描流程阶段。UI 0.1 把"配置 / 进度 / 结果 / 详情"拆成四个页面（04-信息架构.md）。
/// </summary>
public enum ScanStage
{
    /// <summary>残留扫描：选择范围与选项。</summary>
    Setup = 0,

    /// <summary>扫描中。</summary>
    Progress = 1,

    /// <summary>扫描完成摘要。</summary>
    Complete = 2,

    /// <summary>扫描结果 + 详情面板。</summary>
    Results = 3,
}

/// <summary>孤儿软件扫描页 ViewModel。</summary>
public sealed partial class OrphanScanViewModel : ObservableObject
{
    /// <summary>受保护条目二次确认里最多列出的路径条数，避免对话框过长。</summary>
    private const int MaxProtectedPreview = 8;

    private readonly IOrphanScanner _scanner;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IReportExporter _exporter;
    private readonly CleanupService _cleanup;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;
    private readonly INavigationService _navigation;
    private readonly UiStateService _uiState;

    /// <summary>本次扫描结束时各关联采集源的健康度（导出报告与顶部提示都用它）。</summary>
    private IReadOnlyList<AssociationSourceHealth> _lastScanHealth = Array.Empty<AssociationSourceHealth>();

    private string _associationHealthText = string.Empty;
    private bool _hasAssociationHealthIssue;
    private string _associationHealthDetail = string.Empty;

    private CancellationTokenSource? _cts;
    private OrphanItem? _selectedItem;
    private IReadOnlyList<string> _globalUncertaintyMessages = Array.Empty<string>();
    private string _statusText = "请选择要扫描的盘符或目录，然后点击“开始扫描”。默认不会删除任何东西。";
    private string _searchText = string.Empty;
    private RiskFilterOption _riskFilter = RiskFilterOption.All;
    private bool _showExcluded = true;
    private double _progressValue;
    private bool _isScanning;
    private bool _isBusy;
    private string _customDirectoryInput = string.Empty;
    private string _orphanCountText = "0";
    private ScanStage _stage = ScanStage.Setup;
    private int _scannedDirectories;
    private int _candidateCount;
    private string _currentPath = string.Empty;
    private string _elapsedText = "00:00";
    private double _elapsedSeconds;

    public OrphanScanViewModel(
        IOrphanScanner scanner,
        ISettingsService settings,
        ILogService log,
        IReportExporter exporter,
        CleanupService cleanup,
        IDialogService dialogs,
        IToastService toasts,
        INavigationService navigation,
        UiStateService uiState)
    {
        _scanner = scanner;
        _settings = settings;
        _log = log;
        _exporter = exporter;
        _cleanup = cleanup;
        _dialogs = dialogs;
        _toasts = toasts;
        _navigation = navigation;
        _uiState = uiState;

        Results = new ObservableCollection<OrphanItem>();
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = FilterItem;

        Drives = new ObservableCollection<DriveOption>();
        CustomDirectories = new ObservableCollection<string>();

        LoadDrives();
        foreach (var dir in _settings.Current.CustomScanDirectories) CustomDirectories.Add(dir);
    }

    // ---------- 集合与绑定属性 ----------

    public ObservableCollection<DriveOption> Drives { get; }

    public ObservableCollection<string> CustomDirectories { get; }

    public ObservableCollection<OrphanItem> Results { get; }

    public ICollectionView ResultsView { get; }

    public OrphanItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(SelectedHasUninstaller));
                OnPropertyChanged(nameof(SelectedRecentActivityText));
                OnPropertyChanged(nameof(SelectedConflictWarnings));
                OnPropertyChanged(nameof(HasSelectedConflictWarnings));
                OnPropertyChanged(nameof(SelectedUncertaintyWarnings));
                OnPropertyChanged(nameof(SelectedOtherWarnings));
                OnPropertyChanged(nameof(HasSelectedUncertaintyWarnings));
                OnPropertyChanged(nameof(HasSelectedOtherWarnings));
            }
        }
    }

    public RiskFilterOption RiskFilter
    {
        get => _riskFilter;
        set
        {
            if (SetProperty(ref _riskFilter, value)) ResultsView.Refresh();
        }
    }

    public bool ShowExcluded
    {
        get => _showExcluded;
        set
        {
            if (SetProperty(ref _showExcluded, value)) ResultsView.Refresh();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) ResultsView.Refresh();
        }
    }

    public string CustomDirectoryInput
    {
        get => _customDirectoryInput;
        set => SetProperty(ref _customDirectoryInput, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                ScanCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                NotifyCleanCommands();
            }
        }
    }

    public bool IsIdle => !IsScanning && !IsBusy;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string OrphanCountText
    {
        get => _orphanCountText;
        set => SetProperty(ref _orphanCountText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    // ---------- UI 0.2：流程阶段与扫描进度 ----------

    /// <summary>当前处于扫描流程的哪个页面。</summary>
    public ScanStage Stage
    {
        get => _stage;
        set => SetProperty(ref _stage, value);
    }

    public int ScannedDirectories
    {
        get => _scannedDirectories;
        private set => SetProperty(ref _scannedDirectories, value);
    }

    public int CandidateCount
    {
        get => _candidateCount;
        private set => SetProperty(ref _candidateCount, value);
    }

    /// <summary>当前正在检查的目录（扫描中页面展示）。</summary>
    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    /// <summary>
    /// 进度是否不确定。
    /// UDR-03：Core 目前只提供 CandidateCount，没有扫描总量 TotalDirectories，
    /// 因此百分比不可信 —— 官方降级方案是不显示百分比，只显示已检查数量。
    /// Core 提供 TotalDirectories 后改回确定进度。
    /// </summary>
    public bool IsProgressIndeterminate => true;

    public bool IsAdministrator => AdminHelper.IsAdministrator;

    // ---------------- 未提权降级（Core 真机数据确认的风险） ----------------

    /// <summary>
    /// 存在"未提权导致的误判风险"。
    ///
    /// Core 两轮对比实测：非管理员下运行中的进程采集 193 条 vs 管理员 356 条（+84%），
    /// 计划任务 0 条（Partial）vs 101 条（Success），置信度上限 65 vs 100。
    /// 后果不是"结果不完整"，而是**正在使用的软件缺少「运行中进程」这条保护信号，
    /// 更容易被判成残留** —— 真实误删风险。因此必须显式警告，并对清理入口加限制。
    /// </summary>
    public bool HasElevationRisk => !IsAdministrator;

    /// <summary>未提权警告正文：说清具体受限的两个来源，以及真实后果。</summary>
    public string ElevationWarningText =>
        "未以管理员身份运行：运行中的进程采集不完整，计划任务基本采集不到。"
      + "正在使用但未被采集到的软件会缺少「运行中进程」这条保护信号，可能被误判为残留。";

    /// <summary>未提权时的处理限制说明。</summary>
    public string ElevationRestrictionText =>
        "因此：永久删除已禁用；移入隔离区与删除到回收站需要额外确认一次。建议以管理员身份重启后再处理。";

    /// <summary>未提权时禁止永久删除（不可恢复 + 误判风险 = 后果不可接受）。</summary>
    public bool CanPermanentDelete => IsAdministrator;

    /// <summary>最大扫描层级（直接读写 AppSettings，供扫描配置页使用）。</summary>
    public int MaxScanDepth
    {
        get => _settings.Current.MaxScanDepth;
        set
        {
            if (_settings.Current.MaxScanDepth == value) return;
            _settings.Current.MaxScanDepth = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaxScanDepthIndex));
        }
    }

    public int MaxScanDepthIndex
    {
        get => Math.Clamp(MaxScanDepth - 1, 0, 3);
        set => MaxScanDepth = value + 1;
    }

    public bool IncludeHiddenDirectories
    {
        get => _settings.Current.IncludeHiddenDirectories;
        set
        {
            if (_settings.Current.IncludeHiddenDirectories == value) return;
            _settings.Current.IncludeHiddenDirectories = value;
            OnPropertyChanged();
        }
    }

    public int MinSoftwareScore
    {
        get => _settings.Current.MinSoftwareScore;
        set
        {
            if (_settings.Current.MinSoftwareScore == value) return;
            _settings.Current.MinSoftwareScore = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MinSoftwareScoreIndex));
        }
    }

    public int MinSoftwareScoreIndex
    {
        get => MinSoftwareScore switch
        {
            <= 20 => 0,
            <= 35 => 1,
            <= 50 => 2,
            _ => 3,
        };
        set => MinSoftwareScore = value switch
        {
            0 => 20,
            1 => 35,
            2 => 50,
            _ => 70,
        };
    }

    public int ResultCount => Results.Count;

    public int SelectedCount => Results.Count(x => x.IsSelected && !x.IsExcluded);

    public long SelectedSize => Results.Where(x => x.IsSelected && !x.IsExcluded).Sum(x => x.SizeBytes);

    public string SelectedSummary =>
        $"已勾选 {SelectedCount} 项，共 {FileSizeFormatter.Format(SelectedSize)}";

    public int HighRiskCount => Results.Count(x => x.Risk == RiskLevel.High && !x.IsExcluded);

    public int MediumRiskCount => Results.Count(x => x.Risk == RiskLevel.Medium && !x.IsExcluded);

    public int LowRiskCount => Results.Count(x => x.Risk == RiskLevel.Low && !x.IsExcluded);

    // ---------------- v0.1：可解释性汇总 ----------------

    /// <summary>
    /// 「几乎未检测到使用迹象」及以上的数量。
    /// 注意：这是证据强度的描述，**不代表这些确实是残留** —— Core 47 条人工回标实测
    /// Precision 0.333，96 分那条实际是天天在用的网易云音乐。
    /// </summary>
    public int HighConfidenceCount =>
        Results.Count(x => x.ConfidenceLevel >= OrphanConfidenceLevel.HighProbability && !x.IsExcluded);

    /// <summary>「未检测到任何使用迹象」（最高一档，≥90 分）的数量。</summary>
    public int VeryHighProbabilityCount =>
        Results.Count(x => x.ConfidenceLevel >= OrphanConfidenceLevel.VeryHighProbability && !x.IsExcluded);

    /// <summary>Core 建议保留复核的数量（Recommendation == Review）。</summary>
    public int ReviewCount => Results.Count(x => x.Recommendation == CleanupRecommendation.Review && !x.IsExcluded);

    /// <summary>受保护 / 已排除的数量。</summary>
    public int ProtectedCount => Results.Count(x => x.IsProtected || x.IsExcluded);

    /// <summary>带警告（扫描不完整等）的数量。</summary>
    public int WarningCount => Results.Count(x => x.HasWarnings);

    public long TotalSize => Results.Where(x => !x.IsExcluded).Sum(x => x.SizeBytes);

    // ---- Core 2026-10-16：把"仍在使用"与"不确定"信号聚合成用户可见的事实 ----
    // 这三组计数在逻辑上优先于置信度：它们是真的观测，置信度只是推断。

    /// <summary>
    /// 近 90 天内有文件活动的条目数 —— 很可能仍在使用。
    /// 直接读 Core 新增的 <see cref="OrphanItem.RecentModifiedFileCount90"/>，
    /// 不再绕道匹配 EvidenceType（枚举一旦调整就会失效，且拿不到文件数量级）。
    /// </summary>
    public int RecentActivityCount => Results.Count(x => x.RecentModifiedFileCount90 > 0);

    /// <summary>
    /// 近 90 天被修改文件数的最大值。
    /// 「100 个文件」和「1 个」含义完全不同 —— 布尔值丢了这个量级信息（Core 已确认）。
    /// </summary>
    public int MaxRecentFileCount90 =>
        Results.Count == 0 ? 0 : Results.Max(x => x.RecentModifiedFileCount90);

    /// <summary>未能完成「运行中的进程」检查的条目数 —— 不确定信号，不是可清理信号。</summary>
    public int UncertainProcessCount => Results.Count(HasUncertainProcessWarning);

    /// <summary>自带卸载程序的条目数 —— 应优先走正规卸载，而不是删目录。</summary>
    public int UninstallerCount => Results.Count(x => x.HasUninstaller);

    /// <summary>是否存在需要优先提示的不确定性 / 仍在使用信号。</summary>
    public bool HasUncertaintySignals =>
        RecentActivityCount > 0 || UncertainProcessCount > 0 || UninstallerCount > 0
        || GlobalUncertaintyMessages.Count > 0 || HasStrongDetectionGroup;

    /// <summary>
    /// 全部条目都带有的「不确定」警告文案（例如 47/47 都有的关联检查未完成）。
    /// 这类信息放到每一项里重复展示只会稀释注意力，统一在页面级展示一次。
    /// </summary>
    public IReadOnlyList<string> GlobalUncertaintyMessages
    {
        get => _globalUncertaintyMessages;
        private set
        {
            _globalUncertaintyMessages = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGlobalUncertaintyMessages));
        }
    }

    public bool HasGlobalUncertaintyMessages => GlobalUncertaintyMessages.Count > 0;

    /// <summary>不太可能仍在使用的上限估计（近 90 天无文件活动且进程检查完成）。</summary>
    public int LikelyUnusedEstimate =>
        Results.Count(x => !x.IsExcluded
                           && x.RecentModifiedFileCount90 == 0
                           && !HasUncertainProcessWarning(x));

    // ---- Core 2026-10-17 提议的聚合提示：把"看起来能清理"和"有更安全方式"摆在一起 ----

    /// <summary>
    /// Core 标出「观测冲突」的条数（<see cref="ScanWarningCode.ObservationsConflict"/>）。
    ///
    /// 数量直接数 Core 的警告，不在 UI 里另设阈值 —— 否则就会出现在聚合卡上写"2 项冲突"、
    /// 逐项展开却找不到这条警告的错位。
    /// </summary>
    public int ConflictCount => Results.Count(HasConflictWarning);

    /// <summary>最高一档里自带卸载器的条数 —— 这些有更安全的选择。</summary>
    public int StrongDetectionUninstallerCount =>
        Results.Count(x => !x.IsExcluded
                           && x.ConfidenceLevel >= OrphanConfidenceLevel.VeryHighProbability
                           && x.HasUninstaller);

    public bool HasStrongDetectionGroup => Results.Count > 0 && VeryHighProbabilityCount > 0;

    /// <summary>Core 提议的那句聚合提示。</summary>
    public string StrongDetectionGroupText
    {
        get
        {
            if (!HasStrongDetectionGroup) return string.Empty;

            var sb = new StringBuilder();
            sb.Append($"本次 {ResultCount} 项中，{VeryHighProbabilityCount} 项未检测到任何使用迹象（最高一档）");

            if (StrongDetectionUninstallerCount > 0)
                sb.Append($"，其中 {StrongDetectionUninstallerCount} 项自带卸载器");

            if (ConflictCount > 0)
                sb.Append($"，{ConflictCount} 项 Core 标出「观测冲突」");

            return sb.Append("。").ToString();
        }
    }

    /// <summary>
    /// 「没有冲突提示 ≠ 安全」的说明。
    ///
    /// Core 2026-10-17 实测：≥40 分的 8 条里只有 2 条能被观测冲突检出，
    /// 漏掉的是"主程序常年在 AppData 写数据、安装目录不动"的软件 —— 包括那条最危险的 100 分。
    /// 因此这句话必须**无条件显示**：只在有冲突时才让这一栏出现，
    /// 等于告诉用户"没黄字就可以删"，比不加提示更危险。
    /// </summary>
    public string ConflictCoverageNote =>
        "「观测冲突」只能发现把活动写在安装目录里的软件。" +
        "很多软件把数据写在 AppData 等其它位置，安装目录常年不变 —— " +
        "这类目录看不到冲突提示，同样可能有人在用。";

    // ---- 详情面板派生（把字符串分类放在 VM，XAML 不写业务逻辑） ----

    /// <summary>选中项是否可运行自带卸载器（比删除目录安全得多）。</summary>
    public bool SelectedHasUninstaller =>
        SelectedItem is { HasUninstaller: true } item
        && !string.IsNullOrEmpty(item.UninstallerPath)
        && File.Exists(item.UninstallerPath);

    /// <summary>
    /// 选中项身上「不确定」类的警告。
    /// 按 <see cref="ScanWarning.Code"/> 分类 —— 不按文案匹配，
    /// 否则 Core 改措辞会让分类静默失效（提示消失且不报错）。
    ///
    /// 会剔除「全部条目都有的」那几条：Core 实测 AssociationCheckIncomplete / ScanIncomplete
    /// 是 47/47，逐条重复等于没有信息量，还会把真正有区分度的警告淹没掉（"狼来了"效应）。
    /// 它们改由页面级的 <see cref="GlobalUncertaintyMessages"/> 统一展示一次。
    /// </summary>
    public IReadOnlyList<ScanWarning> SelectedUncertaintyWarnings =>
        SelectedItem == null
            ? Array.Empty<ScanWarning>()
            : SelectedItem.Warnings
                .Where(w => w.IsUncertainty && !GlobalUncertaintyMessages.Contains(w.Message))
                .ToList();

    /// <summary>
    /// 选中项身上 Core 标出的「观测冲突」（<see cref="ScanWarningCode.ObservationsConflict"/>）。
    /// 单独列出：Core 明确它比 IsUncertainty 更强 —— 不是"没查清楚"，
    /// 而是"查出来了但自相矛盾"。因此不混进「其它提示」里稀释掉。
    /// </summary>
    public IReadOnlyList<ScanWarning> SelectedConflictWarnings =>
        SelectedItem == null
            ? Array.Empty<ScanWarning>()
            : SelectedItem.Warnings.Where(HasConflictWarning).ToList();

    public bool HasSelectedConflictWarnings => SelectedConflictWarnings.Count > 0;

    /// <summary>选中项身上其它提示（自带卸载器、游戏目录、开发项目等）。</summary>
    public IReadOnlyList<ScanWarning> SelectedOtherWarnings =>
        SelectedItem == null
            ? Array.Empty<ScanWarning>()
            : SelectedItem.Warnings.Where(w => !w.IsUncertainty && !HasConflictWarning(w)).ToList();

    public bool HasSelectedUncertaintyWarnings => SelectedUncertaintyWarnings.Count > 0;

    public bool HasSelectedOtherWarnings => SelectedOtherWarnings.Count > 0;

    /// <summary>
    /// 选中项近期文件活动的完整描述。
    /// 用 Core 给的量级而不是"有/没有" —— 用户判断"还在用吗"时，文件数比布尔值有用得多。
    /// </summary>
    public string SelectedRecentActivityText => SelectedItem switch
    {
        null => string.Empty,
        { RecentModifiedFileCount90: > 0 } i =>
            $"近 90 天有 {i.RecentModifiedFileCount90} 个文件被修改 —— 很可能仍在使用",
        { RecentModifiedFileCount365: > 0 } i =>
            $"近 90 天无活动；近一年有 {i.RecentModifiedFileCount365} 个文件被修改",
        _ => "近一年没有检测到文件被修改",
    };

    /// <summary>
    /// 面向用户的总结。
    /// 措辞原则：说"检测到 / 没检测到"，不说"它是残留"；同时把"仍在使用"的信号放在最前面。
    /// </summary>
    public string ResultSummaryText
    {
        get
        {
            if (Results.Count == 0) return "未检测到疑似软件残留的目录。";

            var sb = new StringBuilder();
            sb.Append($"检测到 {Results.Count} 个「未发现系统关联」的软件目录，共 {FileSizeFormatter.Format(TotalSize)}。");

            var caveats = new List<string>();
            if (RecentActivityCount > 0) caveats.Add($"{RecentActivityCount} 个近 90 天内有文件活动");
            if (UninstallerCount > 0) caveats.Add($"{UninstallerCount} 个自带卸载器");
            if (UncertainProcessCount > 0) caveats.Add($"{UncertainProcessCount} 个未能确认是否正在运行");
            if (ProtectedCount > 0) caveats.Add($"{ProtectedCount} 个受保护或已排除");

            if (caveats.Count > 0)
                sb.Append("其中 " + string.Join("，", caveats) + "。");

            sb.Append("本工具只呈现检测依据，不做自动判定 —— 是否处理请逐项人工确认。");
            return sb.ToString();
        }
    }

    /// <summary>本次扫描的全局警告（例如"关联来源未完成检查"）。</summary>
    public ObservableCollection<string> ScanWarnings { get; } = new();

    public bool HasScanWarnings => ScanWarnings.Count > 0;

    // ---------------- v0.1：关联采集源健康度 ----------------

    /// <summary>
    /// 顶部一行健康度摘要，例如
    /// 「关联检查：8/9 个来源成功 · 未完成：计划任务 · 结果可能不完整 · 置信度上限 65」。
    /// 上限直接读 <see cref="ScanOutcome.ConfidenceCap"/>，UI 不重算、不另设阈值。
    /// </summary>
    public string AssociationHealthText
    {
        get => _associationHealthText;
        private set
        {
            if (SetProperty(ref _associationHealthText, value))
                OnPropertyChanged(nameof(HasAssociationHealth));
        }
    }

    /// <summary>是否已产出健康度摘要（决定顶部提示是否显示）。</summary>
    public bool HasAssociationHealth => !string.IsNullOrEmpty(_associationHealthText);

    /// <summary>是否存在未完成/未运行的采集源。为 true 时必须显式提示"结果可能不完整"。</summary>
    public bool HasAssociationHealthIssue
    {
        get => _hasAssociationHealthIssue;
        private set => SetProperty(ref _hasAssociationHealthIssue, value);
    }

    /// <summary>逐来源明细（名称 · 状态 · 条数），供展开查看。</summary>
    public string AssociationHealthDetail
    {
        get => _associationHealthDetail;
        private set => SetProperty(ref _associationHealthDetail, value);
    }

    // ---------- 命令 ----------

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task ScanAsync()
    {
        var roots = BuildRootPaths();
        if (roots.Count == 0)
        {
            _dialogs.Warning("请至少选择一个盘符，或添加一个自定义目录。");
            return;
        }

        if (!AdminHelper.IsAdministrator)
        {
            // 注意措辞：不是"偏保守"。Core 真机数据确认未提权会让"正在使用的软件更容易被判成残留"，
            // 那是偏向误删的方向，必须说清楚，不能用"偏保守"让用户以为更安全。
            _dialogs.Warning(
                "未以管理员身份运行：\n\n"
              + "· 运行中的进程采集不全，计划任务基本采集不到\n"
              + "· 正在使用但未被采集到的软件，会缺少「运行中进程」保护信号，可能被误判为残留\n"
              + "· 未提权时永久删除已禁用，其余操作需额外确认\n\n"
              + "建议以管理员身份重启本程序。");
        }

        _cts = new CancellationTokenSource();
        IsScanning = true;
        Stage = ScanStage.Progress;
        ProgressValue = 0;
        ScannedDirectories = 0;
        CandidateCount = 0;
        CurrentPath = string.Empty;
        Results.Clear();
        ScanWarnings.Clear();
        SelectedItem = null;
        _lastScanHealth = Array.Empty<AssociationSourceHealth>();
        AssociationHealthText = string.Empty;
        HasAssociationHealthIssue = false;
        AssociationHealthDetail = string.Empty;
        StatusText = "准备扫描…";

        var options = new ScanOptions
        {
            RootPaths = roots,
            MaxDepth = _settings.Current.MaxScanDepth,
            IncludeHiddenDirectories = _settings.Current.IncludeHiddenDirectories,
            MinSoftwareScore = _settings.Current.MinSoftwareScore,
            ExcludedPaths = _settings.Current.ExcludedPaths.ToList(),
            KeepKeywords = _settings.Current.KeepKeywords.ToList(),
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.Message)) StatusText = p.Message;
                else if (!string.IsNullOrWhiteSpace(p.CurrentPath)) StatusText = p.CurrentPath;

                ScannedDirectories = p.ScannedDirectories;
                CandidateCount = p.CandidateCount;
                if (!string.IsNullOrWhiteSpace(p.CurrentPath)) CurrentPath = p.CurrentPath;

                // UDR-03：Core 尚未提供 TotalDirectories，百分比不可信 —— 不显示百分比
                ProgressValue = 0;
                _elapsedSeconds = sw.Elapsed.TotalSeconds;
                ElapsedText = FormatElapsed(sw.Elapsed);

                OrphanCountText = p.OrphanCount.ToString();
            });

            // 推荐入口：条目、采集源健康度、置信度上限同批产生。
            // 不再自行去 AssociationIndex 取 SourceHealth —— 那样在别的生命周期下
            // 可能拿到"从未构建过"的空索引，把一次健康扫描误报成"严重不完整"。
            var outcome = await _scanner.ScanWithOutcomeAsync(options, progress, _cts.Token);
            var items = outcome.Items;

            // 与 items 同批，绝不会是空索引
            _lastScanHealth = outcome.SourceHealth;
            RefreshAssociationHealth(outcome);

            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                Results.Add(item);
            }

            OnPropertyChanged(nameof(ResultCount));
            UpdateSelection();

            // 汇总本次扫描的全局警告（例如某个关联来源未完成检查）
            foreach (var warning in items.SelectMany(i => i.Warnings)
                                         .Select(w => w.Message)
                                         .Distinct())
                ScanWarnings.Add(warning);

            OnPropertyChanged(nameof(HasScanWarnings));

            Stage = ScanStage.Complete;

            // 过渡方案：把本次扫描摘要写到 UI 层状态（UDR-02 落地后改由 Core 提供）
            _uiState.UpdateAndSave(state => state.LastScan = new LastScanSummary
            {
                FinishedAt = DateTime.Now,
                DurationSeconds = sw.Elapsed.TotalSeconds,
                RootPathCount = roots.Count,
                RootPathsText = string.Join(" · ", roots),
                OrphanCount = items.Count,
                TotalSize = items.Where(i => !i.IsExcluded).Sum(i => i.SizeBytes),
                HighConfidenceCount = HighConfidenceCount,
                ReviewCount = ReviewCount,
                ProtectedCount = ProtectedCount,
            });

            StatusText = $"扫描完成：检测到 {items.Count} 个未发现系统关联的软件目录"
                         + $"（其中 {RecentActivityCount} 个近 90 天仍有文件活动，"
                         + $"{UninstallerCount} 个自带卸载器，{ProtectedCount} 个受保护或已排除），"
                         + $"耗时 {sw.Elapsed.TotalSeconds:F1} 秒。默认未勾选任何条目。";
            _log.Info("Scan", "Scan",
                $"扫描完成：根路径 {roots.Count} 个，检出 {items.Count} 个未发现系统关联的目录"
                + $"（近 90 天有活动 {RecentActivityCount}，自带卸载器 {UninstallerCount}，受保护 {ProtectedCount}），"
                + $"耗时 {sw.Elapsed.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消。";
            Stage = ScanStage.Setup;
            _log.Warning("Scan", "Scan", "用户取消了扫描");
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
            Stage = ScanStage.Setup;
            _log.Error("Scan", "Scan", ex.Message);
            _dialogs.Error("扫描失败：" + ex.Message);
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Stop() => _cts?.Cancel();

    /// <summary>从「扫描完成」进入「扫描结果」。</summary>
    [RelayCommand]
    private void ViewResults() => Stage = ScanStage.Results;

    /// <summary>从「扫描中 / 完成」返回「残留扫描」配置页。</summary>
    [RelayCommand]
    private void BackToSetup() => Stage = ScanStage.Setup;

    /// <summary>以管理员身份重启本程序（引导用户消除未提权导致的误判风险）。</summary>
    [RelayCommand]
    private void RestartAsAdmin() => AdminRestart.Request(_dialogs, _log);

    /// <summary>重新扫描：清空结果，回到配置页。</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void RestartScan()
    {
        Results.Clear();
        ScanWarnings.Clear();
        SelectedItem = null;
        _lastScanHealth = Array.Empty<AssociationSourceHealth>();
        AssociationHealthText = string.Empty;
        HasAssociationHealthIssue = false;
        AssociationHealthDetail = string.Empty;
        UpdateSelection();
        OnPropertyChanged(nameof(ResultCount));
        OnPropertyChanged(nameof(HasScanWarnings));
        Stage = ScanStage.Setup;
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectAll()
    {
        foreach (var item in Results.Where(x => !x.IsExcluded)) item.IsSelected = true;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectNone()
    {
        foreach (var item in Results) item.IsSelected = false;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void InvertSelection()
    {
        foreach (var item in Results.Where(x => !x.IsExcluded)) item.IsSelected = !item.IsSelected;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void SelectLowRiskOnly()
    {
        foreach (var item in Results)
            item.IsSelected = !item.IsExcluded && item.Risk == RiskLevel.Low;
        UpdateSelection();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task MoveToQuarantineAsync() => await CleanAsync(CleanActionType.Quarantine);

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task DeleteToRecycleBinAsync() => await CleanAsync(CleanActionType.RecycleBin);

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task PermanentDeleteAsync() => await CleanAsync(CleanActionType.PermanentDelete);

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Results.Count == 0)
        {
            _dialogs.Warning("没有可导出的结果，请先扫描。");
            return;
        }

        var path = _dialogs.SaveFile(
            "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
            $"GhostCleaner-Scan-{DateTime.Now:yyyyMMdd_HHmmss}.json");

        if (path == null) return;

        try
        {
            // 必须带上采集源健康度，否则报告无法解释"为什么某个目录的置信度只有 55/65/45"
            await _exporter.ExportScanReportAsync(Results, path, _lastScanHealth);
            _log.Info("Scan", "Export", "扫描报告已导出：" + path);
            _dialogs.Info("报告已导出：\n" + path);
        }
        catch (Exception ex)
        {
            _log.Error("Scan", "Export", ex.Message);
            _dialogs.Error("导出失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private void OpenSelectedFolder()
    {
        if (SelectedItem == null || !Directory.Exists(SelectedItem.Path)) return;
        try
        {
            Process.Start("explorer.exe", SelectedItem.Path);
        }
        catch
        {
            // 忽略
        }
    }

    // ---- Core 文档 16 第一步：AppData 导航 ----
    //
    // 性质是**导航，不是判定**。Core 实测 AppData 目录名/exe 厂商匹配的 AUC 是 0.327（反向），
    // 所以这里的按钮绝不参与打分、也不参与任何提示 —— 它只是把用户带到他自己能一眼看明白的地方。
    // 第二步（Core 提供 AppDataCandidatePaths）落地后，再加精确导航那时的样子还不确定，
    // 但第一步这两颗按钮不会失效：Core 明确写了"候选全不存在则回退到这里"。

    /// <summary>%APPDATA%（Roaming，用户漫游数据）。</summary>
    public string AppDataRoamingPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>%LOCALAPPDATA%（本地数据，游戏/缓存/大型状态多在这里）。</summary>
    public string AppDataLocalPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public bool HasAppDataRoaming => Directory.Exists(AppDataRoamingPath);

    public bool HasAppDataLocal => Directory.Exists(AppDataLocalPath);

    [RelayCommand]
    private void OpenAppDataRoaming() => OpenFolder(AppDataRoamingPath);

    [RelayCommand]
    private void OpenAppDataLocal() => OpenFolder(AppDataLocalPath);

    private void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            _dialogs.Warning("目录不存在：" + path);
            return;
        }

        try
        {
            Process.Start("explorer.exe", path);
            _log.Info("Nav", "OpenFolder", "打开目录：" + path, path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("无法打开目录：" + ex.Message);
        }
    }

    [RelayCommand]
    private void RunUninstaller()
    {
        if (SelectedItem?.UninstallerPath == null || !File.Exists(SelectedItem.UninstallerPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(SelectedItem.UninstallerPath) { UseShellExecute = true });
            _log.Info("Clean", "RunUninstaller", "启动卸载程序：" + SelectedItem.UninstallerPath, SelectedItem.Path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("无法启动卸载程序：" + ex.Message);
        }
    }

    [RelayCommand]
    private void AddCustomDirectory()
    {
        var folder = _dialogs.PickFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;
        AddDirectory(folder);
    }

    [RelayCommand]
    private void AddInputDirectory()
    {
        if (string.IsNullOrWhiteSpace(CustomDirectoryInput)) return;
        AddDirectory(CustomDirectoryInput);
        CustomDirectoryInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveCustomDirectory(string? path)
    {
        if (path == null) return;
        CustomDirectories.Remove(path);
        _settings.Current.CustomScanDirectories.Remove(path);
        _ = _settings.SaveAsync();
    }

    [RelayCommand]
    private void AddCommonDirectories()
    {
        int added = 0;
        foreach (var drive in Drives.Where(d => d.IsSelected))
        {
            foreach (var name in ScanOptions.DefaultInstallFolderNames)
            {
                var candidate = System.IO.Path.Combine(drive.Root, name);
                if (Directory.Exists(candidate) && !CustomDirectories.Contains(candidate))
                {
                    AddDirectory(candidate, persist: true);
                    added++;
                }
            }
        }

        _dialogs.Info(added > 0 ? $"已添加 {added} 个常见安装目录。" : "未发现额外的常见安装目录。");
    }

    [RelayCommand]
    private async Task AddSelectedToWhitelistAsync()
    {
        if (SelectedItem == null) return;

        _settings.Current.ExcludedPaths.Add(SelectedItem.Path);
        await _settings.SaveAsync();

        SelectedItem.IsExcluded = true;
        SelectedItem.ExcludeReason = "已加入排除路径白名单";
        SelectedItem.IsSelected = false;
        UpdateSelection();
        ResultsView.Refresh();

        _log.Info("Settings", "AddWhitelist", "加入排除路径：" + SelectedItem.Path, SelectedItem.Path);
        _dialogs.Info("已加入排除路径，该目录不会再参与删除。");
    }

    // ---------- 内部实现 ----------

    private static string FormatElapsed(TimeSpan elapsed)
        => $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

    /// <summary>
    /// 找出「全部条目都有」的不确定警告。
    /// Core 实测这类警告常常是 47/47（AssociationCheckIncomplete / ScanIncomplete），
    /// 逐条重复没有信息量，还会把真正有区分度的警告淹没掉 —— 统一提到页面级展示一次。
    /// </summary>
    private void RecomputeGlobalUncertaintyMessages()
    {
        if (Results.Count == 0)
        {
            GlobalUncertaintyMessages = Array.Empty<string>();
            return;
        }

        GlobalUncertaintyMessages = Results
            .SelectMany(x => x.Warnings)
            .Where(w => w.IsUncertainty)
            .GroupBy(w => w.Message)
            .Where(g => g.Count() == Results.Count)
            .Select(g => g.Key)
            .ToList();
    }

    /// <summary>该项是否带有 Core 标出的「观测冲突」（两者互相矛盾的观测同时成立）。</summary>
    private static bool HasConflictWarning(OrphanItem item)
        => item.Warnings.Any(HasConflictWarning);

    /// <summary>该警告是否为「观测冲突」。写代码不用文案匹配。</summary>
    private static bool HasConflictWarning(ScanWarning warning)
        => warning.Code == ScanWarningCode.ObservationsConflict;

    /// <summary>
    /// 该项是否带有「未能完成运行中的进程检查」的不确定信号。
    /// 按 <see cref="ScanWarningCode"/> 判定 —— 不按文案匹配，
    /// 否则 Core 改措辞会让判定静默失效。
    /// </summary>
    private static bool HasUncertainProcessWarning(OrphanItem item)
        => item.Warnings.Any(w => w.Code == ScanWarningCode.ProcessCheckIncomplete);

    private void AddDirectory(string path, bool persist = true)
    {
        var normalized = PathUtils.Normalize(path);
        if (!Directory.Exists(normalized))
        {
            _dialogs.Warning("目录不存在：" + normalized);
            return;
        }

        if (!CustomDirectories.Contains(normalized)) CustomDirectories.Add(normalized);

        if (persist && !_settings.Current.CustomScanDirectories.Contains(normalized))
        {
            _settings.Current.CustomScanDirectories.Add(normalized);
            _ = _settings.SaveAsync();
        }
    }

    private List<string> BuildRootPaths()
    {
        var roots = new List<string>();
        roots.AddRange(Drives.Where(d => d.IsSelected).Select(d => d.Root));
        roots.AddRange(CustomDirectories);
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task CleanAsync(CleanActionType action)
    {
        var targets = Results.Where(x => x.IsSelected && !x.IsExcluded).ToList();
        if (targets.Count == 0)
        {
            _dialogs.Warning("请先勾选要处理的条目。默认不会自动勾选任何条目。");
            return;
        }

        // 未提权闸门：先于高风险/受保护确认执行。
        // 这里的风险不是"结果不完整"，而是"正在使用的软件更容易被判成残留"（Core 真机数据确认）。
        if (!IsAdministrator)
        {
            if (action == CleanActionType.PermanentDelete)
            {
                _dialogs.Warning(
                    "未以管理员身份运行时，运行中的进程与计划任务采集受限，\n"
                  + "正在使用的软件可能被误判为残留。\n\n"
                  + "永久删除不可恢复，因此未提权时已禁用。\n"
                  + "请改选「移入隔离区」或「删除到回收站」，或以管理员身份重启本程序。",
                    "未提权：永久删除已禁用");
                return;
            }

            if (!_dialogs.ConfirmWithCheckbox(
                    "未以管理员身份运行，关联采集不完整：\n\n"
                  + "· 运行中的进程采集不全，计划任务基本采集不到\n"
                  + "· 正在使用但未被采集到的软件，会缺少「运行中进程」这条保护信号\n"
                  + "· 这类软件因此更容易被判定为残留 —— 这是真实的误删风险\n\n"
                  + "建议以管理员身份重启后再处理。\n"
                  + $"即将对 {targets.Count} 个目录执行【{CleanupService.ActionText(action)}】。",
                    "我已知悉采集不完整可能导致正在使用的软件被误判为残留，仍要继续",
                    "未提权确认"))
            {
                return;
            }
        }

        var totalSize = targets.Sum(x => x.SizeBytes);
        var highRisk = targets.Count(x => x.Risk == RiskLevel.High);
        bool allowHighRisk = false;

        // 「仍在使用」信号（Core 2026-10-16 指定的最高性价比兜底）。
        // Core 判定不可靠时，这是比置信度更可信的观测：近 90 天有文件活动 ≈ 很可能还在用。
        // 说清楚文件数量级 —— "100 个文件"和"1 个"对用户判断的含义完全不同。
        var recentActive = targets.Where(x => x.RecentModifiedFileCount90 > 0).ToList();
        var recentFiles = recentActive.Sum(x => x.RecentModifiedFileCount90);

        if (recentActive.Count > 0)
        {
            var top = recentActive.OrderByDescending(x => x.RecentModifiedFileCount90).First();

            if (!_dialogs.ConfirmWithCheckbox(
                    $"本次选中的 {targets.Count} 项中，有 {recentActive.Count} 项近 90 天内有文件被修改"
                  + $"（共 {recentFiles} 个文件）。\n\n"
                  + $"其中最活跃的是「{top.DisplayName}」—— {top.RecentModifiedFileCount90} 个文件。\n\n"
                  + "这些目录很可能仍在使用。删除前请确认它们确实不再需要，\n"
                  + "尤其是系统重装后 D 盘遗留的目录 —— 它们往往正是还在用的软件。\n\n"
                  + $"即将执行【{CleanupService.ActionText(action)}】。",
                    "我已知悉其中有目录近期仍有文件活动，仍要对全部选中项继续",
                    "近期仍有活动"))
            {
                return;
            }
        }

        // 自带卸载器：Core 对这类目录**从不输出 Quarantine**（RiskEvaluator.Recommend 硬编码）。
        // 用户手动勾选等于绕过 Core 判定，所以这里必须拦一次并给出更安全的替代路径。
        var withUninstaller = targets.Count(x => x.HasUninstaller);

        if (withUninstaller > 0)
        {
            var preview = string.Join(
                "\n",
                targets.Where(x => x.HasUninstaller)
                       .Take(MaxProtectedPreview)
                       .Select(x => "· " + x.Path));

            if (!_dialogs.ConfirmWithCheckbox(
                    $"本次选中的 {targets.Count} 项中，有 {withUninstaller} 项自带卸载程序：\n\n{preview}\n\n"
                  + "Core 不会对这些目录建议隔离。直接删除目录会绕过正规卸载流程，\n"
                  + "残留注册表项、服务与系统关联。\n\n"
                  + "更安全的做法：取消勾选它们，然后在详情面板点「运行自带卸载器」。",
                    "我已知悉存在更安全的卸载方式，仍要直接处理整个目录",
                    "建议改用自带卸载器"))
            {
                return;
            }
        }

        if (highRisk > 0)
        {
            if (!_settings.Current.AllowHighRiskClean)
            {
                _dialogs.Warning($"所选条目中有 {highRisk} 个高风险条目，将自动跳过。\n"
                               + "高风险条目位于系统或用户关键目录，默认不参与删除。");
            }
            else
            {
                allowHighRisk = _dialogs.Confirm(
                    $"所选条目中有 {highRisk} 个高风险条目。\n高风险条目可能涉及系统或用户关键目录，确定继续吗？",
                    "高风险确认",
                    danger: true);
            }
        }

        // 受保护内容：产品决策允许在人工明确确认后隔离，
        // 但必须先展示 ProtectionReason，并要求用户勾选知悉后才允许提交。
        var protectedTargets = targets.Where(x => x.IsProtected).ToList();

        if (protectedTargets.Count > 0)
        {
            if (action == CleanActionType.PermanentDelete)
            {
                _dialogs.Warning($"所选条目中有 {protectedTargets.Count} 个受保护目录，"
                               + "Core 会拒绝永久删除并自动跳过。\n"
                               + "受保护内容只允许可恢复的隔离或回收站。");
            }
            else
            {
                var preview = string.Join(
                    "\n",
                    protectedTargets
                        .Take(MaxProtectedPreview)
                        .Select(x => "· " + x.Path + "（" + (x.ProtectionReason ?? "受保护") + "）"));

                if (protectedTargets.Count > MaxProtectedPreview)
                    preview += $"\n… 等共 {protectedTargets.Count} 个";

                var reasons = protectedTargets
                    .Select(x => string.IsNullOrWhiteSpace(x.ProtectionReason) ? "受保护内容" : x.ProtectionReason!)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var checkboxText = "我已知悉这些目录包含 " + string.Join(" / ", reasons)
                                   + "，并理解隔离是可恢复的，仍要继续";

                if (!_dialogs.ConfirmWithCheckbox(
                        $"所选条目中有 {protectedTargets.Count} 个受保护目录：\n\n{preview}\n\n"
                      + "受保护目录可能包含用户存档、游戏数据、开发项目或配置文件。\n"
                      + $"即将执行【{CleanupService.ActionText(action)}】，请先确认。",
                        checkboxText,
                        "受保护内容确认"))
                {
                    return;
                }
            }
        }

        var message = $"即将对 {targets.Count} 个目录执行【{CleanupService.ActionText(action)}】。\n"
                    + $"总大小：{FileSizeFormatter.Format(totalSize)}\n\n"
                    + (action == CleanActionType.Quarantine ? "目录会被移动到隔离区，可在隔离区页面恢复。\n" : string.Empty)
                    + (action == CleanActionType.RecycleBin ? "目录会被删除到回收站，可从回收站还原。\n" : string.Empty)
                    + (action == CleanActionType.PermanentDelete ? "永久删除不可恢复！请再次确认这些目录不再需要。\n" : string.Empty)
                    + "\n是否继续？";

        string? confirmText = null;

        if (action == CleanActionType.PermanentDelete)
        {
            if (!_dialogs.ConfirmWithText(message, CleanupService.PermanentDeleteConfirmText, "永久删除确认"))
                return;
            confirmText = CleanupService.PermanentDeleteConfirmText;
        }
        else if (!_dialogs.Confirm(message, "清理确认", danger: action == CleanActionType.RecycleBin))
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在处理…";

        try
        {
            var result = await _cleanup.ExecuteAsync(
                targets,
                action,
                allowHighRisk,
                confirmText,
                new Progress<string>(s => StatusText = s));

            // 隔离是可恢复操作，成功后不该用模态对话框打断用户。
            // 改用 Toast，并给出「查看隔离区」直达入口（验收标准要求这条路径存在）。
            if (action == CleanActionType.Quarantine && result.SuccessCount > 0)
            {
                _toasts.Success(
                    $"已隔离 {result.SuccessCount} 项，可在隔离区恢复。",
                    "查看隔离区",
                    () => _navigation.Navigate("Quarantine"));
            }
            else
            {
                _dialogs.Info(result.Summary + "\n\n完整记录请查看【日志】页面。", "处理完成");
            }
            StatusText = result.Summary;

            var succeeded = targets
                .Where(x => x.OperationResult != null
                            && !x.OperationResult.Contains("跳过")
                            && !x.OperationResult.Contains("失败"))
                .ToList();

            foreach (var item in succeeded)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                Results.Remove(item);
            }

            OnPropertyChanged(nameof(ResultCount));
            UpdateSelection();
        }
        catch (Exception ex)
        {
            _log.Error("Clean", "Clean", ex.Message);
            _dialogs.Error("处理失败：" + ex.Message);
            StatusText = "处理失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 根据本次扫描的 <see cref="ScanOutcome"/> 刷新顶部提示。
    /// 置信度上限直接读 <see cref="ScanOutcome.ConfidenceCap"/>，
    /// UI 不调用判定逻辑重算（那正是"把没观测当成观测到失败"的来源）。
    /// </summary>
    private void RefreshAssociationHealth(ScanOutcome outcome)
    {
        var health = outcome.SourceHealth;

        if (health.Count == 0)
        {
            HasAssociationHealthIssue = true;
            AssociationHealthText = "关联检查：未获取到采集源状态，结果可能不完整";
            AssociationHealthDetail = "扫描未产出采集源健康度，无法判断置信度是否被限制。";
            return;
        }

        // 逐来源明细仍用 SourceHealth 直接渲染（语义/配色逻辑不变）
        AssociationHealthDetail = string.Join(
            Environment.NewLine,
            health.Select(h => $"{h.SourceName} · {h.StatusText} · {h.ItemCount} 条"
                               + (h.ErrorMessage == null ? string.Empty : " · " + h.ErrorMessage)));

        HasAssociationHealthIssue = !outcome.IsAssociationDataFullyHealthy;

        AssociationHealthText = outcome.IsAssociationDataFullyHealthy
            ? $"关联检查：{outcome.HealthySourceCount} 个来源全部成功 · 引用 {outcome.TotalAssociationRecords} 条"
            : $"关联检查：{outcome.HealthySourceCount}/{health.Count} 个来源成功"
              + $" · 未完成：{string.Join("、", outcome.UnhealthySourceNames)}"
              + $" · 结果可能不完整 · 置信度上限 {outcome.ConfidenceCap}";

        // 顶部提示之外，也并入全局警告，避免用户只看结果列表时漏掉
        var note = outcome.ConfidenceCapNote;
        if (!string.IsNullOrWhiteSpace(note) && !ScanWarnings.Contains(note))
        {
            ScanWarnings.Add(note);
            OnPropertyChanged(nameof(HasScanWarnings));
        }
    }

    private bool FilterItem(object obj)
    {
        if (obj is not OrphanItem item) return false;

        if (!ShowExcluded && item.IsExcluded) return false;

        if (RiskFilter != RiskFilterOption.All)
        {
            var expected = RiskFilter switch
            {
                RiskFilterOption.Low => RiskLevel.Low,
                RiskFilterOption.Medium => RiskLevel.Medium,
                RiskFilterOption.High => RiskLevel.High,
                _ => RiskLevel.Low,
            };
            if (item.Risk != expected) return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            if (!item.Path.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !item.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void LoadDrives()
    {
        Drives.Clear();
        var systemRoot = DriveHelperSystemRoot();

        foreach (var drive in System.IO.DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

                var root = drive.RootDirectory.FullName;
                Drives.Add(new DriveOption
                {
                    Root = root,
                    FreeBytes = drive.AvailableFreeSpace,
                    TotalBytes = drive.TotalSize,
                    IsSystemDrive = root.Equals(systemRoot, StringComparison.OrdinalIgnoreCase),
                    IsSelected = !root.Equals(systemRoot, StringComparison.OrdinalIgnoreCase),
                });
            }
            catch
            {
                // 忽略不可读的盘
            }
        }
    }

    private static string DriveHelperSystemRoot()
        => System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrphanItem.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(HighRiskCount));
        OnPropertyChanged(nameof(MediumRiskCount));
        OnPropertyChanged(nameof(LowRiskCount));
        OnPropertyChanged(nameof(HighConfidenceCount));
        OnPropertyChanged(nameof(ReviewCount));
        OnPropertyChanged(nameof(ProtectedCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(TotalSize));
        OnPropertyChanged(nameof(RecentActivityCount));
        OnPropertyChanged(nameof(MaxRecentFileCount90));
        RecomputeGlobalUncertaintyMessages();
        OnPropertyChanged(nameof(UncertainProcessCount));
        OnPropertyChanged(nameof(UninstallerCount));
        OnPropertyChanged(nameof(HasUncertaintySignals));
        OnPropertyChanged(nameof(LikelyUnusedEstimate));
        OnPropertyChanged(nameof(VeryHighProbabilityCount));
        OnPropertyChanged(nameof(ConflictCount));
        OnPropertyChanged(nameof(StrongDetectionUninstallerCount));
        OnPropertyChanged(nameof(HasStrongDetectionGroup));
        OnPropertyChanged(nameof(StrongDetectionGroupText));
        OnPropertyChanged(nameof(ResultSummaryText));
        NotifyCleanCommands();
    }

    private void NotifyCleanCommands()
    {
        MoveToQuarantineCommand.NotifyCanExecuteChanged();
        DeleteToRecycleBinCommand.NotifyCanExecuteChanged();
        PermanentDeleteCommand.NotifyCanExecuteChanged();
    }
}

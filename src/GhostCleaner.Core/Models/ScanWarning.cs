namespace GhostCleaner.Core.Models;

/// <summary>
/// 警告的分类代码。
///
/// <para>存在的理由：此前警告是裸 <c>string</c>，消费方（UI）只能按文案匹配来分类
/// （例如 <c>Contains("未能完成")</c>）。这有两个问题：</para>
/// <list type="bullet">
/// <item>Core 改一个措辞，UI 的分类就静默失效 —— 而且是"提示消失"，不是报错，极难发现</item>
/// <item>分类逻辑与展示文案耦合，文案不能自由优化</item>
/// </list>
///
/// <para>故改为「代码 + 文案」：<see cref="ScanWarning.Code"/> 用于程序判定与分类，
/// <see cref="ScanWarning.Message"/> 用于展示，两者解耦。</para>
/// </summary>
public enum ScanWarningCode
{
    /// <summary>未归类（保底，不应出现）。</summary>
    Other = 0,

    /// <summary>
    /// 关联检查未完成 —— 不能据此认为"没有引用"。
    /// 属「不确定信号」：没查清楚，不代表没人用。
    /// </summary>
    AssociationCheckIncomplete,

    /// <summary>
    /// 运行中进程检查未完成 —— 无法确认该目录是否正在被使用。
    /// 属「不确定信号」。
    /// </summary>
    ProcessCheckIncomplete,

    /// <summary>注册表卸载项检查未完成。属「不确定信号」。</summary>
    RegistryUninstallCheckIncomplete,

    /// <summary>整体扫描结果可能不完整（多个来源未完成）。属「不确定信号」。</summary>
    ScanIncomplete,

    /// <summary>自带卸载器 —— 应引导用自带卸载器，而非直接删目录。属「操作建议」。</summary>
    HasUninstaller,

    /// <summary>疑似开发项目或工具链。属「类别提示」。</summary>
    DevelopmentProject,

    /// <summary>疑似游戏 / 游戏库。属「类别提示」。</summary>
    GameDirectory,

    /// <summary>
    /// 观测冲突：Core 未检测到使用迹象，但文件系统显示近期仍有写入。
    /// 两条独立观测互相矛盾，应以文件系统为准。
    ///
    /// <para>属「明确警示」而非「不确定信号」—— 它不是"没查清楚"，
    /// 而是"查出来了但自相矛盾"，性质比 <see cref="AssociationCheckIncomplete"/> 那类更强。</para>
    ///
    /// <para>⚠️ <b>覆盖有限，绝不能当作安全清单</b>：47 条回标中高分样本只命中 2/8
    /// （命中者真值全为"在用"，零假警）。未命中的高分样本
    /// （wangyiyunyinyue 100 / IROK 68 / VS Code 56）真值同样是"在用"，
    /// 只是目录内没留下活动痕迹（数据写在 AppData）。</para>
    /// </summary>
    ObservationsConflict,

    /// <summary>
    /// 该目录早于当前系统安装时间 —— 重装场景。
    /// 重装会清除软件的注册表登记，使「缺失系统关联」类证据在所有目录上都成立、
    /// 失去区分度。此警告用于向用户诚实说明"这条判定为什么不可信"。
    /// 属「不确定信号」。
    /// </summary>
    PredatesSystemInstall,
}

/// <summary>
/// 一条结构化警告：<see cref="Code"/> 供程序判定，<see cref="Message"/> 供展示。
/// </summary>
public sealed class ScanWarning
{
    public ScanWarningCode Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public ScanWarning()
    {
    }

    public ScanWarning(ScanWarningCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public override string ToString() => Message;

    /// <summary>属于「不确定信号」的类别：没查清楚，不代表没人用。</summary>
    public bool IsUncertainty => Code is ScanWarningCode.AssociationCheckIncomplete
        or ScanWarningCode.ProcessCheckIncomplete
        or ScanWarningCode.RegistryUninstallCheckIncomplete
        or ScanWarningCode.ScanIncomplete
        or ScanWarningCode.PredatesSystemInstall;
}

namespace GhostCleaner.Core.Models;

/// <summary>
/// 采集器的子项诊断计数：扫描了多少项、多少项因无权限等原因失败。
///
/// 存在的理由（v0.1 核心语义）：
/// 采集器内部普遍用 <c>catch { continue; }</c> 跳过无权限的子项。
/// 若跳过之后仍向 <see cref="AssociationSourceHealth"/> 报告 Success，
/// 那么"缺失关联"类证据就会在采集不完整的前提下错误成立 ——
/// 即"把没观测当成观测到空"，这正是 v0.1 要消灭的错误。
///
/// 典型实例：非管理员下 <c>RunningProcesses</c> 只能采到 193/356 个进程，
/// 却报告 Success，导致正在运行但无权限访问的进程被判成"未检测到运行中进程"。
/// </summary>
public sealed class CollectionDiagnostics
{
    /// <summary>尝试采集的子项总数（无论成败）。</summary>
    public int ItemsScanned { get; private set; }

    /// <summary>因无权限或读取失败而被跳过的子项数。</summary>
    public int ItemsFailed { get; private set; }

    /// <summary>第一条失败原因（用于展示，不用于判定）。</summary>
    public string? Error { get; private set; }

    /// <summary>是否存在失败（无论程度）。</summary>
    public bool HasFailures => ItemsFailed > 0;

    /// <summary>失败率（0~1）。</summary>
    public double FailureRatio => ItemsScanned == 0 ? 0 : (double)ItemsFailed / ItemsScanned;

    /// <summary>
    /// 可容忍的失败比例：失败率低于此值仍视为可靠。
    ///
    /// <para>为什么不是"只要有失败就不可靠"：真机实测管理员下进程采集
    /// 375 项中 18 项失败（4.8%）—— 这些是受保护系统进程（System / csrss / smss 等），
    /// 即使管理员也永远无法访问 MainModule，与权限无关，也不可能是用户软件进程。
    /// 若因此把来源判定为不可靠，管理员下置信度上限会从 100 掉到 65，属于明显倒退。</para>
    ///
    /// <para>非管理员下失败率 43%（163/375），远超此阈值，仍会正确降级。</para>
    ///
    /// <para>取值依据：落在管理员 4.8% 与非管理员 43% 之间，两侧各有 2 倍以上余量。
    /// ⚠️ 该阈值由真机数据选定，仍需在更多样本上校准。</para>
    /// </summary>
    public double MaxFailureRatio { get; init; } = 0.10;

    /// <summary>
    /// 是否严重到应把该来源判定为不可靠。
    /// 与 <see cref="HasFailures"/> 的区别：少量失败（受保护项，非权限问题）不算不可靠；
    /// 但"一项都没扫到"必然不可靠。
    /// </summary>
    public bool IsDegraded => HasFailures
        && (ItemsScanned == 0 || FailureRatio >= MaxFailureRatio);

    /// <summary>记录一次采集尝试。</summary>
    public void RecordScanned() => ItemsScanned++;

    /// <summary>记录一次失败，并保留第一条原因。</summary>
    public void RecordFailure(string? error = null)
    {
        ItemsFailed++;
        Error ??= error;
    }

    /// <summary>直接记录一条来源级错误（不增加失败计数，例如目录枚举整体被拒绝）。</summary>
    public void RecordError(string error) => Error ??= error;

    /// <summary>供 UI 展示的人类可读摘要。</summary>
    public string Describe()
    {
        var text = $"共扫描 {ItemsScanned} 项，{ItemsFailed} 项失败";
        return Error == null ? text : text + "：" + Error;
    }
}

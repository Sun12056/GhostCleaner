using GhostCleaner.Core.Models;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// <see cref="CollectionDiagnostics"/> 的降级判定测试。
///
/// 它要守住的是实测踩到的一个坑：
/// <b>"只要有失败就判定来源不可靠"会造成倒退。</b>
///
/// 真机数据（同一台机器）：
/// - 管理员下进程采集 375 项 / 18 项失败（4.8%）—— 受保护系统进程，
///   即使管理员也永远无法访问 MainModule，与权限无关
/// - 非管理员下 375 项 / 163 项失败（43%）—— 真正的权限不足
///
/// 若按"有失败即降级"处理，管理员下置信度上限会从 100 掉到 65，
/// 反而比修复前更保守。降级必须看失败率。
/// </summary>
public class CollectionDiagnosticsTests
{
    private static CollectionDiagnostics Build(int scanned, int failed)
    {
        var d = new CollectionDiagnostics();
        for (var i = 0; i < scanned; i++) d.RecordScanned();
        for (var i = 0; i < failed; i++) d.RecordFailure("无法访问");
        return d;
    }

    /// <summary>
    /// 管理员场景：少量受保护进程失败，不应降级。
    /// 这条是防倒退的关键断言 —— 一旦它失败，管理员下的置信度上限会被压到 65。
    /// </summary>
    [Fact]
    public void Admin_ProtectedProcessesOnly_NotDegraded()
    {
        var d = Build(scanned: 375, failed: 18);

        Assert.True(d.HasFailures, "确实存在失败项，应被记录");
        Assert.False(d.IsDegraded, "4.8% 的失败率属受保护系统进程，不应判定来源不可靠");
    }

    /// <summary>非管理员场景：近半数进程无权限，必须降级。</summary>
    [Fact]
    public void NonAdmin_MassivePermissionFailures_Degraded()
    {
        var d = Build(scanned: 375, failed: 163);

        Assert.True(d.HasFailures);
        Assert.True(d.IsDegraded, "43% 的失败率必须判定来源不可靠");
    }

    /// <summary>
    /// 来源整体被拒绝（一项都没扫到）：必然降级，
    /// 且不能因为 ItemsScanned 为 0 导致失败率算成 0 而漏判。
    /// </summary>
    [Fact]
    public void SourceTotallyRejected_Degraded()
    {
        var d = new CollectionDiagnostics();
        d.RecordFailure("权限不足，无法枚举 C:\\WINDOWS\\system32\\Tasks");

        Assert.Equal(0, d.ItemsScanned);
        Assert.True(d.HasFailures);
        Assert.True(d.IsDegraded, "一项都没扫到时，失败率无意义，必须直接判定不可靠");
    }

    /// <summary>无失败时不得降级（回归保护）。</summary>
    [Fact]
    public void NoFailures_NotDegraded()
    {
        var d = Build(scanned: 356, failed: 0);

        Assert.False(d.HasFailures);
        Assert.False(d.IsDegraded);
    }

    /// <summary>失败率恰好等于阈值时按"降级"处理（保守取向）。</summary>
    [Fact]
    public void FailureRatioAtThreshold_Degraded()
    {
        var d = Build(scanned: 100, failed: 10);

        Assert.Equal(0.10, d.FailureRatio, 6);
        Assert.True(d.IsDegraded);
    }
}

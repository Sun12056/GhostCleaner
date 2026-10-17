using GhostCleaner.Core.Models;
using GhostCleaner.Scanner;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// 契约测试：证据文案的产出规则，以及「观测冲突」警告的触发条件。
/// </summary>
public class WarningAndEvidenceContractTests
{
    private static OrphanItem Evaluate(SoftwareDirectoryInfo info)
        => RiskEvaluator.Evaluate(info, Array.Empty<AssociationHit>(), new ScanOptions(), TestHealth.AllSuccess());

    /// <summary>
    /// 卸载器证据必须用<b>完整路径</b>，不能只用文件名。
    ///
    /// 实测 <c>UNINSTALL.EXE</c> 这类通用名在本机跨了 5 个目录
    /// （wangyiyunyinyue / IROK / aDrive / CloudMusic / ZWCAD2026），
    /// 只写文件名会让用户以为这几条是同一个东西 —— UI 侧反馈指出 Core 自己还在产出它。
    /// </summary>
    [Fact]
    public void UninstallerEvidence_DescriptionUsesFullPath()
    {
        const string path = @"D:\wangyiyunyinyue";
        var item = Evaluate(TestInfo.Create(path));

        var e = item.Evidence.FirstOrDefault(x => x.Type == EvidenceType.Uninstaller);

        Assert.NotNull(e);
        Assert.Contains(path, e!.Description);
        Assert.Contains("unins000.exe", e.Description);
    }

    /// <summary>路径缺失时不得产出「目录包含 」这种残缺文案。</summary>
    [Fact]
    public void UninstallerEvidence_NoBrokenTextWhenPathMissing()
    {
        var info = TestInfo.Create(@"D:\X");
        info.UninstallerPath = null;

        var item = Evaluate(info);
        var e = item.Evidence.FirstOrDefault(x => x.Type == EvidenceType.Uninstaller);

        if (e != null)
            Assert.False(e.Description.EndsWith("目录包含 "),
                "路径缺失时不能产出以『目录包含 』结尾的残缺文案");
    }

    /// <summary>
    /// 观测冲突：Core 说"未检测到使用迹象"，但文件系统显示近期还在写。
    ///
    /// 依据（47 条回标）：置信度≥40 且近 90 天有文件活动的样本，真值全部是"在用"（零假警）。
    /// 所以这条警告出现时，应引导用户相信文件系统而不是分数。
    /// </summary>
    [Fact]
    public void HighConfidenceWithRecentFileActivity_ProducesObservationsConflict()
    {
        var info = TestInfo.Create(@"D:\Weixin");
        info.RecentModifiedFileCount90 = 19;

        var item = Evaluate(info);

        Assert.True(item.ConfidenceScore >= OrphanConfidenceThresholds.Suspicious,
            "构造前提：该样本应达到『使用迹象较少』及以上");
        Assert.Contains(item.Warnings, w => w.Code == ScanWarningCode.ObservationsConflict);
    }

    /// <summary>没有近期文件活动时不该报冲突 —— 否则提示泛滥就会失效。</summary>
    [Fact]
    public void NoRecentFileActivity_NoObservationsConflict()
    {
        var info = TestInfo.Create(@"D:\X");
        info.RecentModifiedFileCount90 = 0;

        var item = Evaluate(info);

        Assert.DoesNotContain(item.Warnings, w => w.Code == ScanWarningCode.ObservationsConflict);
    }

    /// <summary>
    /// 锁住「观测冲突」的触发条件与<b>名义阈值</b>一致，让漂移从"静默不一致"变成"显式失败"。
    ///
    /// <para><b>能防</b>：Core 内部实现与 <see cref="OrphanConfidenceThresholds.Suspicious"/>
    /// 的漂移（例如有人把判定改成 <c>&gt;= HighProbability</c> 却没同步说明）。</para>
    ///
    /// <para><b>不能防</b>：UI 另设一套阈值 —— 那是 UI 侧的事，由协作约定检查清单第 4 条
    /// 覆盖（<c>docs/协作约定-猜测与显示.md</c>）。**Core 的测试管不到 UI。**</para>
    ///
    /// <para>只断言"判定 == 声明的阈值"，不写死 40 这个数字 ——
    /// 阈值本身允许整体调整，调整后本测试仍应通过。</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    public void ObservationsConflict_MatchesSuspiciousThreshold(int recent90)
    {
        var info = TestInfo.Create(@"D:\X");
        info.RecentModifiedFileCount90 = recent90;

        var item = Evaluate(info);

        var hasConflict = item.Warnings.Any(w => w.Code == ScanWarningCode.ObservationsConflict);
        var expect = recent90 > 0
                     && item.ConfidenceScore >= OrphanConfidenceThresholds.Suspicious;

        Assert.Equal(expect, hasConflict);
    }

    /// <summary>
    /// 观测冲突属「明确警示」，不是「不确定信号」——
    /// 它不是"没查清楚"，而是"查出来了但自相矛盾"，性质更强。
    /// </summary>
    [Fact]
    public void ObservationsConflict_IsNotClassifiedAsUncertainty()
    {
        var warning = new ScanWarning(ScanWarningCode.ObservationsConflict, "测试");

        Assert.False(warning.IsUncertainty);
    }
}

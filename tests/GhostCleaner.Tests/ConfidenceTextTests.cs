using GhostCleaner.Core.Models;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// 置信度等级文案的契约测试。
///
/// 它要守住的是一条由真实数据换来的措辞原则：
/// <b>等级文案必须说"没检测到什么"，不能说"它是残留"。</b>
///
/// 依据：47 条人工回标中，置信度 96 的样本真值是"日常在用"
/// （重装系统场景下"缺失系统关联"证据失去区分度）。
/// 若文案写成"非常高概率残留"，会直接诱导用户删除仍在使用的软件。
///
/// 导出报告 <c>ReportExporter</c> 直接取用 <see cref="OrphanConfidenceThresholds.ToText"/>，
/// 所以这里锁住的也是报告与 UI 说法的一致性。
/// </summary>
public class ConfidenceTextTests
{
    [Fact]
    public void VeryHighProbability_SaysNoUsageDetected()
    {
        var text = OrphanConfidenceThresholds.ToText(OrphanConfidenceLevel.VeryHighProbability);

        Assert.Contains("未检测到", text);
        Assert.DoesNotContain("残留", text);
    }

    [Fact]
    public void HighProbability_SaysAlmostNoUsageDetected()
    {
        var text = OrphanConfidenceThresholds.ToText(OrphanConfidenceLevel.HighProbability);

        Assert.Contains("未检测到", text);
        Assert.DoesNotContain("残留", text);
    }

    [Fact]
    public void Suspicious_SaysLessUsageDetected()
    {
        var text = OrphanConfidenceThresholds.ToText(OrphanConfidenceLevel.Suspicious);

        Assert.Contains("使用迹象", text);
        Assert.DoesNotContain("残留", text);
    }

    /// <summary>
    /// 全部等级都不得出现"残留"字样 —— 这是本测试存在的核心理由。
    /// 一旦有人把它改回"高概率残留"，这条会失败。
    /// </summary>
    [Theory]
    [InlineData(OrphanConfidenceLevel.VeryHighProbability)]
    [InlineData(OrphanConfidenceLevel.HighProbability)]
    [InlineData(OrphanConfidenceLevel.Suspicious)]
    public void NoLevelTextClaimsItIsOrphan(OrphanConfidenceLevel level)
    {
        Assert.DoesNotContain("残留", OrphanConfidenceThresholds.ToText(level));
    }

    /// <summary>阈值本身不得被改动（改动需同步回标口径）。</summary>
    [Fact]
    public void Thresholds_Unchanged()
    {
        Assert.Equal(40, OrphanConfidenceThresholds.Suspicious);
        Assert.Equal(70, OrphanConfidenceThresholds.HighProbability);
        Assert.Equal(90, OrphanConfidenceThresholds.VeryHighProbability);
    }
}

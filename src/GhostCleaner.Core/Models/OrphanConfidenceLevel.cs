namespace GhostCleaner.Core.Models;

/// <summary>疑似残留置信度等级（注意：这是"多大把握认为它是残留"，不是"删除风险"）。</summary>
public enum OrphanConfidenceLevel
{
    /// <summary>普通目录，没有足够的残留特征。</summary>
    Normal = 0,

    /// <summary>可疑，需要人工确认。</summary>
    Suspicious = 1,

    /// <summary>高概率残留。</summary>
    HighProbability = 2,

    /// <summary>非常高概率残留（仍需人工确认后才处理）。</summary>
    VeryHighProbability = 3,
}

/// <summary>
/// 置信度分级阈值。所有阈值集中在此，禁止在算法中散落魔法数字。
/// 这些是 v0.1 的初始模型，允许后续整体调整。
/// </summary>
public static class OrphanConfidenceThresholds
{
    public const int Suspicious = 40;
    public const int HighProbability = 70;
    public const int VeryHighProbability = 90;

    public static OrphanConfidenceLevel FromScore(int score) => score switch
    {
        >= VeryHighProbability => OrphanConfidenceLevel.VeryHighProbability,
        >= HighProbability => OrphanConfidenceLevel.HighProbability,
        >= Suspicious => OrphanConfidenceLevel.Suspicious,
        _ => OrphanConfidenceLevel.Normal,
    };

    /// <summary>
    /// 等级文案。
    ///
    /// <b>措辞按"没检测到什么"来写，不写成"它是什么残留"。</b>
    /// 依据：47 条人工回标实测，置信度 96 的样本真值是"日常在用"
    /// （重装系统场景下"缺失系统关联"失去区分度，见
    /// <c>docs/core-0.1/09-…指标结果.md</c> §4.2）。
    /// 写成"非常高概率残留"会直接诱导用户删除仍在使用的软件。
    /// </summary>
    public static string ToText(OrphanConfidenceLevel level) => level switch
    {
        OrphanConfidenceLevel.VeryHighProbability => "未检测到任何使用迹象",
        OrphanConfidenceLevel.HighProbability => "几乎未检测到使用迹象",
        OrphanConfidenceLevel.Suspicious => "使用迹象较少",
        _ => "普通",
    };
}

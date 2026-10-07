namespace WinCleaner.Core.Models;

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

    public static string ToText(OrphanConfidenceLevel level) => level switch
    {
        OrphanConfidenceLevel.VeryHighProbability => "非常高概率残留",
        OrphanConfidenceLevel.HighProbability => "高概率残留",
        OrphanConfidenceLevel.Suspicious => "可疑",
        _ => "普通",
    };
}

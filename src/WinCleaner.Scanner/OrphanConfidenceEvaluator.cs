using WinCleaner.Core.Models;

namespace WinCleaner.Scanner;

/// <summary>
/// 置信度模型：回答"我们有多大把握认为它是残留"。
///
/// 与 <see cref="RiskEvaluator"/>（清理风险）完全独立：
/// 一个目录可以"非常像残留"但"清理风险很高"，也可以反过来。
/// </summary>
public static class OrphanConfidenceEvaluator
{
    /// <summary>由证据集合计算 0~100 的置信度，并施加扫描健康度上限。</summary>
    public static int Compute(
        EvidenceCollection collection,
        IReadOnlyList<AssociationSourceHealth> health,
        EvidenceWeights weights)
    {
        var raw = collection.Evidence.Sum(e => e.Score);
        var capped = Math.Clamp(raw, 0, 100);

        return Math.Min(capped, ComputeHealthCap(health, weights, out _));
    }

    /// <summary>
    /// 扫描健康度上限：来源失败时不允许给出高置信度结论。
    /// 7 个来源全成功 → 不限制；少量失败 → 降低上限；大量失败 → 严格压低。
    /// </summary>
    public static int ComputeHealthCap(
        IReadOnlyList<AssociationSourceHealth> health,
        EvidenceWeights weights,
        out string? note)
    {
        note = null;

        if (health.Count == 0)
        {
            note = "关联索引未构建，判定不可靠";
            return weights.MaxConfidenceWhenManySourcesFailed;
        }

        var unhealthy = health
            .Where(h => h.Status is AssociationSourceStatus.Failed
                                  or AssociationSourceStatus.Partial
                                  or AssociationSourceStatus.NotRun)
            .ToList();

        if (unhealthy.Count == 0) return 100;

        int cap = 100;

        var registry = health.FirstOrDefault(h => h.Covers(AssociationSource.RegistryUninstall));
        bool registryUnavailable = registry != null && registry.Status != AssociationSourceStatus.Success;

        if (registryUnavailable)
        {
            cap = Math.Min(cap, weights.MaxConfidenceWhenRegistryUnavailable);
            note = "注册表关联检查未完成，置信度受限";
        }
        else
        {
            cap = Math.Min(cap, weights.MaxConfidenceWhenAnySourceFailed);
            note = "有 " + unhealthy.Count + " 个关联来源未完成检查，置信度受限";
        }

        if (unhealthy.Count >= weights.ManySourcesFailedThreshold)
        {
            cap = Math.Min(cap, weights.MaxConfidenceWhenManySourcesFailed);
            note = unhealthy.Count + " 个关联来源未完成检查，扫描结果可能严重不完整";
        }

        return cap;
    }

    /// <summary>关联数据是否整体不可靠（用于风险评级）。</summary>
    public static bool IsAssociationDataUnreliable(IReadOnlyList<AssociationSourceHealth> health, out string? reason)
    {
        reason = null;

        if (health.Count == 0)
        {
            reason = "关联索引未构建";
            return true;
        }

        var failed = health.Where(h => h.Status == AssociationSourceStatus.Failed).ToList();
        if (failed.Count >= 2)
        {
            reason = failed.Count + " 个关联来源采集失败";
            return true;
        }

        var registry = health.FirstOrDefault(h => h.Covers(AssociationSource.RegistryUninstall));
        if (registry != null && registry.Status != AssociationSourceStatus.Success)
        {
            reason = "注册表卸载项检查未完成";
            return true;
        }

        if (health.Sum(h => h.ItemCount) == 0)
        {
            reason = "关联索引为空，采集可能整体失败";
            return true;
        }

        return false;
    }
}

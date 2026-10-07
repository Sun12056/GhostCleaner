using WinCleaner.Core.Models;

namespace WinCleaner.Core.Interfaces;

/// <summary>"系统当前仍有关联"的多源索引。</summary>
public interface IAssociationIndex
{
    /// <summary>构建索引（注册表 / 快捷方式 / 服务 / 计划任务 / 进程 / AppX / 文件关联 / 启动项）。</summary>
    Task BuildAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>查询某个目录是否被系统引用，返回全部证据。</summary>
    IReadOnlyList<AssociationHit> FindHits(string directoryPath);

    /// <summary>是否存在任意引用。</summary>
    bool HasAny(string directoryPath) => FindHits(directoryPath).Count > 0;

    /// <summary>索引中的路径总数（诊断用）。</summary>
    int Count { get; }

    /// <summary>
    /// 各采集源的健康状态。Scanner 必须据此区分"确认没有引用"与"未能完成检查"。
    /// </summary>
    IReadOnlyList<AssociationSourceHealth> SourceHealth { get; }

    /// <summary>某个关联来源的结果是否可信（至少有一个覆盖它的采集器成功且可用）。</summary>
    bool IsSourceUsable(AssociationSource source)
    {
        var covered = SourceHealth.Where(h => h.Covers(source)).ToList();
        if (covered.Count == 0) return false;
        return covered.Any(h => h.Status == AssociationSourceStatus.Success);
    }

    /// <summary>所有采集源是否都成功完成。</summary>
    bool IsFullyHealthy => SourceHealth.Count > 0 && SourceHealth.All(h => h.Status == AssociationSourceStatus.Success);

    /// <summary>失败或未完成的来源。</summary>
    IReadOnlyList<AssociationSourceHealth> UnhealthySources =>
        SourceHealth.Where(h => h.Status is AssociationSourceStatus.Failed or AssociationSourceStatus.Partial or AssociationSourceStatus.NotRun)
                    .ToList();
}

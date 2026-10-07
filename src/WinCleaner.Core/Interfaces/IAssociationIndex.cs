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
}

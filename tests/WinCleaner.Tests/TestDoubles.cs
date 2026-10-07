using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;

namespace WinCleaner.Tests;

/// <summary>
/// 关联索引测试替身：不读取真实系统状态，可精确控制每个来源的成功/失败，
/// 让扫描结果断言不受运行环境（是否管理员、装了哪些软件）影响。
/// </summary>
internal sealed class StubAssociationIndex : IAssociationIndex
{
    private readonly Func<AssociationSource, AssociationSourceStatus> _selector;
    private readonly IReadOnlyList<AssociationHit> _hits;
    private readonly List<AssociationSourceHealth> _health = new();

    /// <param name="selector">返回每个来源的扫描状态，默认全部成功。</param>
    /// <param name="hits">强制返回的关联命中。</param>
    public StubAssociationIndex(
        Func<AssociationSource, AssociationSourceStatus>? selector = null,
        IReadOnlyList<AssociationHit>? hits = null)
    {
        _selector = selector ?? (_ => AssociationSourceStatus.Success);
        _hits = hits ?? Array.Empty<AssociationHit>();
    }

    public int Count => _hits.Count;

    public IReadOnlyList<AssociationSourceHealth> SourceHealth => _health;

    public Task BuildAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        _health.Clear();

        foreach (var source in Enum.GetValues<AssociationSource>())
        {
            if (source == AssociationSource.Other) continue;

            var status = _selector(source);
            _health.Add(new AssociationSourceHealth
            {
                SourceName = AssociationSourceText.ToText(source),
                CoveredSources = new[] { source },
                Status = status,
                ItemCount = status == AssociationSourceStatus.Success ? 64 : 0,
                ErrorMessage = status == AssociationSourceStatus.Success ? null : "模拟采集失败",
            });
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<AssociationHit> FindHits(string directoryPath) => _hits;

    public bool HasAny(string directoryPath) => _hits.Count > 0;
}

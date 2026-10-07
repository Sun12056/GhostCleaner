using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Registry;

/// <summary>
/// "系统当前仍有关联"的多源索引实现。
///
/// 重要语义：某个来源采集失败时，<b>不能</b>把它解释成"该目录没有这个来源的引用"。
/// 因此每个采集器都会产出 <see cref="AssociationSourceHealth"/>，Scanner 必须据此
/// 决定"缺失关联"这条证据是否可以成立。
/// </summary>
public sealed class AssociationIndex : IAssociationIndex
{
    private readonly List<AssociationRecord> _records = new();
    private readonly List<AssociationSourceHealth> _health = new();
    private readonly object _sync = new();

    public int Count
    {
        get { lock (_sync) return _records.Count; }
    }

    public IReadOnlyList<AssociationRecord> Records
    {
        get { lock (_sync) return _records.ToList(); }
    }

    public IReadOnlyList<AssociationSourceHealth> SourceHealth
    {
        get { lock (_sync) return _health.ToList(); }
    }

    public async Task BuildAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _records.Clear();
            _health.Clear();
        }

        var sources = new (string Label, AssociationSource[] Covers, Func<IEnumerable<AssociationRecord>> Collect)[]
        {
            ("注册表卸载项", new[] { AssociationSource.RegistryUninstall }, AssociationCollectors.RegistryUninstall),
            ("App Paths", new[] { AssociationSource.AppPaths }, AssociationCollectors.AppPaths),
            ("开始菜单/桌面快捷方式", new[] { AssociationSource.StartMenuShortcut, AssociationSource.DesktopShortcut }, AssociationCollectors.Shortcuts),
            ("Windows 服务", new[] { AssociationSource.WindowsService }, AssociationCollectors.Services),
            ("计划任务", new[] { AssociationSource.ScheduledTask }, AssociationCollectors.ScheduledTasks),
            ("正在运行的进程", new[] { AssociationSource.RunningProcess }, AssociationCollectors.RunningProcesses),
            ("Microsoft Store 应用", new[] { AssociationSource.AppxPackage }, AssociationCollectors.AppxPackages),
            ("文件关联/右键菜单/COM", new[] { AssociationSource.FileAssociation }, AssociationCollectors.FileAssociations),
            ("启动项", new[] { AssociationSource.StartupEntry }, AssociationCollectors.StartupEntries),
        };

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"正在建立关联索引：{source.Label}");

            var buffer = new List<AssociationRecord>();
            string? error = null;
            var status = AssociationSourceStatus.Success;

            await Task.Run(() =>
            {
                try
                {
                    foreach (var record in source.Collect())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(record.Path)) continue;
                        buffer.Add(new AssociationRecord(PathUtils.Normalize(record.Path), record.Source, record.Name));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 采集器整体失败：必须记录下来，绝不能静默当成"没有引用"
                    status = AssociationSourceStatus.Failed;
                    error = ex.Message;
                }
            }, cancellationToken).ConfigureAwait(false);

            // 采集器内部吞掉了异常并返回 0 条 —— 同样不能当成"确实没有引用"
            if (status == AssociationSourceStatus.Success && buffer.Count == 0)
            {
                status = AssociationSourceStatus.Partial;
                error = "未采集到任何记录，该来源的关联检查可能不完整";
            }

            lock (_sync)
            {
                _records.AddRange(buffer);
                _health.Add(new AssociationSourceHealth
                {
                    SourceName = source.Label,
                    CoveredSources = source.Covers,
                    Status = status,
                    ItemCount = buffer.Count,
                    ErrorMessage = error,
                });
            }

            progress?.Report($"关联索引：{source.Label}（{AssociationSourceText.ToText(status)}，{buffer.Count} 条）");
        }
    }

    public IReadOnlyList<AssociationHit> FindHits(string directoryPath)
    {
        var dir = PathUtils.Normalize(directoryPath);
        if (string.IsNullOrEmpty(dir)) return Array.Empty<AssociationHit>();

        var hits = new List<AssociationHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_sync)
        {
            foreach (var record in _records)
            {
                if (!PathUtils.IsUnder(record.Path, dir)) continue;

                var key = $"{(int)record.Source}|{record.Path.ToLowerInvariant()}";
                if (!seen.Add(key)) continue;

                hits.Add(new AssociationHit
                {
                    Source = record.Source,
                    Name = record.Name,
                    TargetPath = record.Path,
                });
            }
        }

        return hits;
    }

    public bool HasAny(string directoryPath) => FindHits(directoryPath).Count > 0;
}

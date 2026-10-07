using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Registry;

/// <summary>
/// "系统当前仍有关联"的多源索引实现。
/// 只有当一个目录没有任何来源引用时，才会被判定为疑似孤儿软件残留。
/// </summary>
public sealed class AssociationIndex : IAssociationIndex
{
    private readonly List<AssociationRecord> _records = new();
    private readonly object _sync = new();

    public int Count => _records.Count;

    public IReadOnlyList<AssociationRecord> Records => _records;

    public async Task BuildAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _records.Clear();
        }

        var sources = new (string Label, Func<IEnumerable<AssociationRecord>> Collect)[]
        {
            ("注册表卸载项", AssociationCollectors.RegistryUninstall),
            ("App Paths", AssociationCollectors.AppPaths),
            ("开始菜单/桌面快捷方式", AssociationCollectors.Shortcuts),
            ("Windows 服务", AssociationCollectors.Services),
            ("计划任务", AssociationCollectors.ScheduledTasks),
            ("正在运行的进程", AssociationCollectors.RunningProcesses),
            ("Microsoft Store 应用", AssociationCollectors.AppxPackages),
            ("文件关联/右键菜单/COM", AssociationCollectors.FileAssociations),
            ("启动项", AssociationCollectors.StartupEntries),
        };

        foreach (var (label, collect) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"正在建立关联索引：{label}");

            await Task.Run(() =>
            {
                try
                {
                    var buffer = new List<AssociationRecord>();
                    foreach (var record in collect())
                    {
                        if (string.IsNullOrWhiteSpace(record.Path)) continue;
                        buffer.Add(new AssociationRecord(PathUtils.Normalize(record.Path), record.Source, record.Name));
                    }

                    lock (_sync)
                    {
                        _records.AddRange(buffer);
                    }
                }
                catch
                {
                    // 单个来源失败不阻断整体扫描（后续判定会因此更保守）
                }
            }, cancellationToken);
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

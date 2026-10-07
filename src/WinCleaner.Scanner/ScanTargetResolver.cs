using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>根据扫描根路径推导出候选目录集合（只做目录枚举，不计算体积，速度快）。</summary>
public static class ScanTargetResolver
{
    /// <summary>
    /// 规则：
    /// 1. 盘符根目录（D:\）本身是容器，只把它的一级子目录作为候选；
    /// 2. 常见安装目录（Program Files / Software / Games ...）也是容器，只把它们的子目录作为候选；
    /// 3. 自定义目录：自身作为候选，同时按 MaxDepth 向下探测；
    /// 4. 命中白名单、系统目录、隐藏目录（未开启时）、重解析点的目录直接跳过。
    /// </summary>
    public static List<string> Resolve(ScanOptions options, Action<string>? onVisited = null, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawRoot in options.RootPaths)
        {
            var root = PathUtils.Normalize(rawRoot);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            bool rootIsDrive = PathUtils.IsDriveRoot(root);

            if (rootIsDrive)
            {
                // 盘符根：一级子目录为候选起点（深度 1）
                foreach (var sub in SafeEnumerateDirectories(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    onVisited?.Invoke(sub);
                    if (!IsInspectable(sub, options, isContainer: false)) continue;

                    var name = System.IO.Path.GetFileName(sub);
                    bool isContainer = Heuristics.IsContainerDirectory(name);

                    if (isContainer)
                    {
                        foreach (var child in SafeEnumerateDirectories(sub))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            onVisited?.Invoke(child);
                            if (IsInspectable(child, options, isContainer: false) && seen.Add(child)) results.Add(child);
                        }
                    }
                    else if (seen.Add(sub))
                    {
                        results.Add(sub);
                    }
                }
            }
            else
            {
                // 自定义目录：自身 + 逐级向下，遇到"看起来像软件目录"的在扫描阶段停止下钻（由 OrphanScanner 处理）
                var queue = new Queue<(string Path, int Depth)>();
                queue.Enqueue((root, 0));

                while (queue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (current, depth) = queue.Dequeue();
                    onVisited?.Invoke(current);

                    if (IsInspectable(current, options, isContainer: depth == 0 && Heuristics.IsContainerDirectory(System.IO.Path.GetFileName(current))) && seen.Add(current))
                        results.Add(current);

                    if (depth >= options.MaxDepth) continue;

                    foreach (var sub in SafeEnumerateDirectories(current))
                        queue.Enqueue((sub, depth + 1));
                }
            }
        }

        return results;
    }

    private static bool IsInspectable(string dir, ScanOptions options, bool isContainer)
    {
        var name = System.IO.Path.GetFileName(dir);
        if (string.IsNullOrEmpty(name)) return false;
        if (Heuristics.IsAlwaysSkipDirectory(name)) return false;
        if (isContainer) return false;
        if (PathUtils.IsCriticalPath(dir)) return false;

        // 白名单：精确路径前缀
        if (options.ExcludedPaths.Any(p => PathUtils.IsUnder(dir, p))) return false;

        // 保留关键词
        if (PathUtils.ContainsKeyword(dir, options.KeepKeywords)) return false;

        try
        {
            var info = new DirectoryInfo(dir);
            var attrs = info.Attributes;

            if (options.SkipReparsePoints && (attrs & FileAttributes.ReparsePoint) != 0) return false;
            if (!options.IncludeHiddenDirectories && (attrs & FileAttributes.Hidden) != 0) return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string dir)
    {
        try
        {
            return new DirectoryInfo(dir)
                .EnumerateDirectories("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    AttributesToSkip = FileAttributes.System,
                })
                .Select(d => d.FullName)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

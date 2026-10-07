namespace WinCleaner.Core.Utils;

/// <summary>目录复制 / 删除 / 度量（带重试与属性清理）。</summary>
public static class DirectoryHelper
{
    /// <summary>统计目录体积与文件数（忽略不可访问项）。</summary>
    public static (long Size, int FileCount) Measure(string path, int maxFiles = 500_000)
    {
        long size = 0;
        int count = 0;
        try
        {
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
            }))
            {
                if (count >= maxFiles) break;
                try { size += file.Length; } catch { }
                count++;
            }
        }
        catch
        {
            // 忽略
        }
        return (size, count);
    }

    /// <summary>复制目录（跨卷移动时使用）。</summary>
    public static void Copy(string source, string destination, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destination);

        var stack = new Stack<(string Src, string Dst)>();
        stack.Push((source, destination));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (src, dst) = stack.Pop();

            foreach (var dir in SafeEnumerate(src, d => d.EnumerateDirectories()))
            {
                var target = System.IO.Path.Combine(dst, dir.Name);
                Directory.CreateDirectory(target);
                stack.Push((dir.FullName, target));
            }

            foreach (var file in SafeEnumerate(src, d => d.EnumerateFiles()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = System.IO.Path.Combine(dst, file.Name);
                progress?.Report(target);

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        File.Copy(file.FullName, target, overwrite: true);
                        break;
                    }
                    catch
                    {
                        if (attempt == 2) throw;
                        Thread.Sleep(100);
                    }
                }
            }
        }
    }

    /// <summary>删除目录（先清理只读属性，失败重试）。</summary>
    public static bool Delete(string path)
    {
        if (!Directory.Exists(path)) return true;

        try
        {
            ClearAttributes(path);
        }
        catch
        {
            // 继续尝试删除
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch
            {
                Thread.Sleep(150);
            }
        }

        // 最后手段：逐文件删除
        try
        {
            foreach (var file in SafeEnumerate(path, d => d.EnumerateFiles("*", SearchOption.AllDirectories)))
            {
                try
                {
                    File.SetAttributes(file.FullName, FileAttributes.Normal);
                    File.Delete(file.FullName);
                }
                catch
                {
                    // 忽略被占用的文件
                }
            }

            Directory.Delete(path, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ClearAttributes(string path)
    {
        foreach (var dir in SafeEnumerate(path, d => d.EnumerateDirectories("*", SearchOption.AllDirectories)))
        {
            try { dir.Attributes = FileAttributes.Normal; } catch { }
        }

        foreach (var file in SafeEnumerate(path, d => d.EnumerateFiles("*", SearchOption.AllDirectories)))
        {
            try { File.SetAttributes(file.FullName, FileAttributes.Normal); } catch { }
        }
    }

    private static IEnumerable<T> SafeEnumerate<T>(string path, Func<DirectoryInfo, IEnumerable<T>> selector)
    {
        try
        {
            return selector(new DirectoryInfo(path)).ToList();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }
}

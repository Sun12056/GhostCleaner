using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Cleanup;

public sealed record JunkCategory(string Key, string Name, string Path, string Description);

public sealed class JunkScanResult
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
    public bool PathExists { get; init; }
    public string SizeText => FileSizeFormatter.Format(SizeBytes);
}

/// <summary>常规垃圾清理：临时文件、更新缓存、缩略图缓存、崩溃转储等（可后续扩展）。</summary>
public static class JunkScanner
{
    public static IReadOnlyList<JunkCategory> Categories { get; } = new List<JunkCategory>
    {
        new("UserTemp", "用户临时文件", Path.GetTempPath(), "当前用户的 %TEMP% 目录"),
        new("WindowsTemp", "Windows 临时文件", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "C:\\Windows\\Temp"),
        new("UpdateCache", "Windows 更新缓存", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"), "已下载的更新包，删除后系统会重新下载"),
        new("ThumbnailCache", "缩略图缓存", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer"), "thumbcache_*.db，系统会自动重建"),
        new("CrashDumps", "崩溃转储", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"), "应用程序崩溃时生成的 dump 文件"),
        new("ErrorReports", "Windows 错误报告", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER"), "WER 报告文件"),
    };

    public static async Task<IReadOnlyList<JunkScanResult>> ScanAsync(
        IEnumerable<JunkCategory> categories,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<JunkScanResult>();

            foreach (var category in categories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("正在扫描：" + category.Name);

                if (!Directory.Exists(category.Path))
                {
                    results.Add(new JunkScanResult
                    {
                        Key = category.Key,
                        Name = category.Name,
                        Path = category.Path,
                        Description = category.Description,
                        PathExists = false,
                    });
                    continue;
                }

                long size = 0;
                int count = 0;

                try
                {
                    foreach (var file in new DirectoryInfo(category.Path).EnumerateFiles("*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = 0,
                    }))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            size += file.Length;
                            count++;
                        }
                        catch
                        {
                            // 忽略不可访问文件
                        }
                    }
                }
                catch
                {
                    // 目录不可访问
                }

                results.Add(new JunkScanResult
                {
                    Key = category.Key,
                    Name = category.Name,
                    Path = category.Path,
                    Description = category.Description,
                    SizeBytes = size,
                    FileCount = count,
                    PathExists = true,
                });
            }

            return results;
        }, cancellationToken);
    }

    /// <summary>逐文件删除（被占用的文件会自动跳过）；返回删除的文件数与释放字节数。</summary>
    public static async Task<(int Files, long Bytes)> CleanAsync(
        JunkScanResult result,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(result.Path)) return (0, 0);

            int files = 0;
            long bytes = 0;

            var directory = new DirectoryInfo(result.Path);

            foreach (var file in directory.EnumerateFiles("*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
            }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var length = file.Length;
                    File.SetAttributes(file.FullName, FileAttributes.Normal);
                    file.Delete();
                    files++;
                    bytes += length;
                    if (files % 50 == 0) progress?.Report($"已删除 {files} 个文件：{result.Name}");
                }
                catch
                {
                    // 被占用或无权限，跳过
                }
            }

            // 尝试删除空的子目录
            try
            {
                foreach (var dir in directory.EnumerateDirectories("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (!dir.EnumerateFileSystemInfos().Any()) dir.Delete();
                    }
                    catch
                    {
                        // 忽略
                    }
                }
            }
            catch
            {
                // 忽略
            }

            return (files, bytes);
        }, cancellationToken);
    }
}

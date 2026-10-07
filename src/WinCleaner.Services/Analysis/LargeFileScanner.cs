using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Analysis;

public sealed class LargeFileEntry
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime LastModified { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string SizeText => FileSizeFormatter.Format(SizeBytes);
    public string LastModifiedText => LastModified.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>大文件扫描（磁盘空间分析的轻量实现）。</summary>
public static class LargeFileScanner
{
    public static async Task<IReadOnlyList<LargeFileEntry>> ScanAsync(
        string root,
        int topCount = 200,
        long minSizeBytes = 100L * 1024 * 1024,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var top = new PriorityQueue<LargeFileEntry, long>();
            int visited = 0;

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
            };

            try
            {
                foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    long length;
                    DateTime writeTime;
                    try
                    {
                        length = file.Length;
                        writeTime = file.LastWriteTime;
                    }
                    catch
                    {
                        continue;
                    }

                    if (length < minSizeBytes) continue;

                    var entry = new LargeFileEntry
                    {
                        Path = file.FullName,
                        SizeBytes = length,
                        LastModified = writeTime,
                        Extension = file.Extension,
                    };

                    top.Enqueue(entry, length);
                    if (top.Count > topCount) top.Dequeue();

                    if (++visited % 2000 == 0) progress?.Report($"已扫描 {visited} 个文件…");
                }
            }
            catch
            {
                // 忽略不可访问目录
            }

            var list = new List<LargeFileEntry>();
            while (top.Count > 0) list.Add(top.Dequeue());

            list.Reverse();
            return list;
        }, cancellationToken);
    }
}

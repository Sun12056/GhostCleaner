using System.Text.Json;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;

namespace GhostCleaner.Services.Logging;

/// <summary>操作日志：按天写入 JSONL 文件，同时保留内存缓存供 UI 展示。</summary>
public sealed class LogService : ILogService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly List<LogEntry> _cache = new();
    private readonly object _sync = new();

    public string LogDirectory { get; }

    public LogService(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Data");
        LogDirectory = System.IO.Path.Combine(dir, "Logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public void Log(LogLevel level, string category, string action, string path, long sizeBytes, string result, string message)
    {
        var entry = new LogEntry
        {
            Level = level,
            Category = category,
            Action = action,
            Path = path,
            SizeBytes = sizeBytes,
            Result = result,
            Message = message,
        };

        lock (_sync)
        {
            _cache.Add(entry);
            if (_cache.Count > 5000) _cache.RemoveRange(0, _cache.Count - 5000);
        }

        try
        {
            // 品牌统一为 GhostCleaner（UDR-11）。旧版本写的是 GhostCleaner- 前缀，
            // 读取时两种前缀都会纳入（见 LoadAsync），否则升级后历史日志会「凭空消失」。
            var file = System.IO.Path.Combine(LogDirectory, $"ghostcleaner-{DateTime.Now:yyyy-MM-dd}.jsonl");
            File.AppendAllText(file, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        }
        catch
        {
            // 日志写入失败不影响主流程
        }
    }

    public void Info(string category, string action, string message, string path = "", long sizeBytes = 0)
        => Log(LogLevel.Info, category, action, path, sizeBytes, "Success", message);

    public void Warning(string category, string action, string message, string path = "", long sizeBytes = 0)
        => Log(LogLevel.Warning, category, action, path, sizeBytes, "Skipped", message);

    public void Error(string category, string action, string message, string path = "", long sizeBytes = 0)
        => Log(LogLevel.Error, category, action, path, sizeBytes, "Failed", message);

    public async Task<IReadOnlyList<LogEntry>> LoadAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var entries = new List<LogEntry>();

        IEnumerable<string> files;
        try
        {
            // 兼容旧前缀 GhostCleaner-：升级前的日志不能因为改名就看不见。
            files = Directory.EnumerateFiles(LogDirectory, "*.jsonl")
                .Where(f =>
                {
                    var name = System.IO.Path.GetFileName(f);
                    return name.StartsWith("ghostcleaner-", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("GhostCleaner-", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(f => f);
        }
        catch
        {
            files = Array.Empty<string>();
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var line in await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogEntry>(line, JsonOptions);
                        if (entry == null) continue;
                        if (from.HasValue && entry.Timestamp < from.Value) continue;
                        if (to.HasValue && entry.Timestamp > to.Value) continue;
                        entries.Add(entry);
                    }
                    catch
                    {
                        // 跳过损坏行
                    }
                }
            }
            catch
            {
                // 忽略无法读取的文件
            }
        }

        lock (_sync)
        {
            foreach (var cached in _cache)
            {
                if (from.HasValue && cached.Timestamp < from.Value) continue;
                if (to.HasValue && cached.Timestamp > to.Value) continue;
                entries.Add(cached);
            }
        }

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }
}

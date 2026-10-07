using System.Text.Json;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Quarantine;

/// <summary>
/// 隔离区：默认动作是把目录"移动"到隔离区（可恢复），而不是直接删除。
/// 隔离区条目包含 manifest.json，记录原始路径、体积、时间与过期时间。
/// </summary>
public sealed class QuarantineManager : IQuarantineManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISettingsService _settings;

    public QuarantineManager(ISettingsService settings)
    {
        _settings = settings;
    }

    public string QuarantineRoot
    {
        get
        {
            var root = _settings.Current.QuarantineRoot;
            if (string.IsNullOrWhiteSpace(root)) root = DriveHelper.SuggestQuarantineRoot();
            root = PathUtils.Normalize(root);
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var root = QuarantineRoot;
        var entries = new List<QuarantineEntry>();

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root);
        }
        catch
        {
            return entries;
        }

        foreach (var dir in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = System.IO.Path.GetFileName(dir);
            if (name.StartsWith("_", StringComparison.Ordinal)) continue;

            var entry = await ReadManifestAsync(dir, cancellationToken).ConfigureAwait(false);
            if (entry != null) entries.Add(entry);
        }

        return entries.OrderByDescending(e => e.CreatedAt).ToList();
    }

    public async Task<QuarantineEntry> MoveToQuarantineAsync(
        string sourcePath,
        string? displayName = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var source = PathUtils.Normalize(sourcePath);
        if (!Directory.Exists(source) && !File.Exists(source))
            throw new DirectoryNotFoundException($"源路径不存在：{source}");

        if (PathUtils.IsCriticalPath(source))
            throw new InvalidOperationException($"拒绝处理系统关键路径：{source}");

        var root = QuarantineRoot;
        if (PathUtils.IsUnder(root, source) || PathUtils.IsUnder(source, root))
            throw new InvalidOperationException("不能把隔离区自身放入隔离区");

        var leaf = System.IO.Path.GetFileName(source.TrimEnd('\\'));
        var id = $"{DateTime.Now:yyyyMMdd_HHmmss}_{PathUtils.SanitizeFileName(leaf)}_{Guid.NewGuid().ToString("N")[..8]}";
        var entryDir = System.IO.Path.Combine(root, id);
        Directory.CreateDirectory(entryDir);

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report($"正在统计：{source}");
        var (size, count) = Directory.Exists(source)
            ? DirectoryHelper.Measure(source)
            : (new FileInfo(source).Length, 1);

        var target = System.IO.Path.Combine(entryDir, PathUtils.SanitizeFileName(leaf));

        progress?.Report($"正在移动：{source}");
        MovePath(source, target, progress, cancellationToken);

        var retention = Math.Clamp(_settings.Current.RetentionDays, 7, 30);
        var entry = new QuarantineEntry
        {
            Id = id,
            OriginalPath = source,
            QuarantinePath = target,
            SizeBytes = size,
            FileCount = count,
            CreatedAt = DateTime.Now,
            ExpireAt = DateTime.Now.AddDays(retention),
            DisplayName = displayName ?? leaf,
        };

        await WriteManifestAsync(entryDir, entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<bool> RestoreAsync(string entryId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var root = QuarantineRoot;
        var entryDir = System.IO.Path.Combine(root, entryId);

        var entry = await ReadManifestAsync(entryDir, cancellationToken).ConfigureAwait(false);
        if (entry == null) return false;

        if (Directory.Exists(entry.OriginalPath) || File.Exists(entry.OriginalPath))
            throw new IOException($"原始路径已存在，无法恢复：{entry.OriginalPath}");

        var parent = System.IO.Path.GetDirectoryName(entry.OriginalPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        progress?.Report($"正在恢复：{entry.OriginalPath}");
        MovePath(entry.QuarantinePath, entry.OriginalPath, progress, cancellationToken);

        entry.Status = QuarantineStatus.Restored;
        await MoveManifestToHistoryAsync(entryDir, entry, cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(entryDir)) DirectoryHelper.Delete(entryDir);
        return true;
    }

    public async Task<bool> DeleteAsync(string entryId, CancellationToken cancellationToken = default)
    {
        var entryDir = System.IO.Path.Combine(QuarantineRoot, entryId);
        if (!Directory.Exists(entryDir)) return false;

        var entry = await ReadManifestAsync(entryDir, cancellationToken).ConfigureAwait(false);
        if (entry != null)
        {
            entry.Status = QuarantineStatus.Purged;
            await MoveManifestToHistoryAsync(entryDir, entry, cancellationToken).ConfigureAwait(false);
        }

        return DirectoryHelper.Delete(entryDir);
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var entries = await ListAsync(cancellationToken).ConfigureAwait(false);
        int removed = 0;

        foreach (var entry in entries.Where(e => e.Status == QuarantineStatus.Quarantined && e.IsExpired))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DeleteAsync(entry.Id, cancellationToken).ConfigureAwait(false)) removed++;
        }

        return removed;
    }

    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        var entries = await ListAsync(cancellationToken).ConfigureAwait(false);
        bool allOk = true;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await DeleteAsync(entry.Id, cancellationToken).ConfigureAwait(false)) allOk = false;
        }

        return allOk;
    }

    // ---------- 内部实现 ----------

    private static void MovePath(string source, string target, IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sameVolume = string.Equals(
            System.IO.Path.GetPathRoot(source),
            System.IO.Path.GetPathRoot(target),
            StringComparison.OrdinalIgnoreCase);

        if (sameVolume)
        {
            try
            {
                if (Directory.Exists(source))
                {
                    Directory.Move(source, target);
                    return;
                }

                File.Move(source, target);
                return;
            }
            catch (IOException)
            {
                // 同卷移动失败（例如被占用）时退回复制+删除
            }
        }

        if (Directory.Exists(source))
        {
            DirectoryHelper.Copy(source, target, progress, ct);
            if (!DirectoryHelper.Delete(source))
                throw new IOException($"移动后无法删除源目录：{source}");
        }
        else
        {
            File.Copy(source, target, overwrite: true);
            File.Delete(source);
        }
    }

    private static async Task WriteManifestAsync(string entryDir, QuarantineEntry entry, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await File.WriteAllTextAsync(System.IO.Path.Combine(entryDir, "manifest.json"), json, ct).ConfigureAwait(false);
    }

    private static async Task<QuarantineEntry?> ReadManifestAsync(string entryDir, CancellationToken ct)
    {
        var file = System.IO.Path.Combine(entryDir, "manifest.json");
        if (!File.Exists(file)) return null;

        try
        {
            return JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false));
        }
        catch
        {
            return null;
        }
    }

    private async Task MoveManifestToHistoryAsync(string entryDir, QuarantineEntry entry, CancellationToken ct)
    {
        try
        {
            var historyDir = System.IO.Path.Combine(QuarantineRoot, "_history");
            Directory.CreateDirectory(historyDir);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(System.IO.Path.Combine(historyDir, entry.Id + ".json"), json, ct).ConfigureAwait(false);
        }
        catch
        {
            // 历史记录写入失败不影响主流程
        }
    }
}

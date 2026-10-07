using WinCleaner.Core.Models;

namespace WinCleaner.Core.Interfaces;

/// <summary>隔离区管理：移入 / 恢复 / 彻底删除 / 过期清理。</summary>
public interface IQuarantineManager
{
    string QuarantineRoot { get; }

    Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<QuarantineEntry> MoveToQuarantineAsync(
        string sourcePath,
        string? displayName = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(string entryId, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string entryId, CancellationToken cancellationToken = default);

    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);

    Task<bool> ClearAsync(CancellationToken cancellationToken = default);
}

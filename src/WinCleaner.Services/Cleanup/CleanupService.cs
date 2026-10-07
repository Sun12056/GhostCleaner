using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Cleanup;

public sealed class CleanupResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public long TotalBytes { get; set; }
    public List<string> Messages { get; } = new();

    public string Summary =>
        $"成功 {SuccessCount} 项，失败 {FailedCount} 项，跳过 {SkippedCount} 项，共释放 {FileSizeFormatter.Format(TotalBytes)}";
}

/// <summary>
/// 清理执行器。安全原则：
/// 1. 只处理用户显式勾选（IsSelected）的条目，默认什么都不做；
/// 2. 系统关键路径一律拒绝；
/// 3. 高风险条目需显式允许才处理；
/// 4. 默认动作是"移到隔离区"；回收站失败不会退化为永久删除；
/// 5. 永久删除必须传入确认文字；
/// 6. 每一步都写日志。
/// </summary>
public sealed class CleanupService
{
    public const string PermanentDeleteConfirmText = "永久删除";

    private readonly IQuarantineManager _quarantine;
    private readonly ILogService _log;
    private readonly ISettingsService _settings;
    private readonly ISystemRestoreService _restore;

    public CleanupService(
        IQuarantineManager quarantine,
        ILogService log,
        ISettingsService settings,
        ISystemRestoreService restore)
    {
        _quarantine = quarantine;
        _log = log;
        _settings = settings;
        _restore = restore;
    }

    public async Task<CleanupResult> ExecuteAsync(
        IEnumerable<OrphanItem> items,
        CleanActionType action,
        bool allowHighRisk,
        string? confirmationText = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult();
        var targets = items.Where(i => i.IsSelected).ToList();

        if (targets.Count == 0)
        {
            result.Messages.Add("没有勾选任何条目，未执行任何操作。");
            return result;
        }

        if (action == CleanActionType.PermanentDelete && confirmationText != PermanentDeleteConfirmText)
            throw new InvalidOperationException($"永久删除必须输入确认文字“{PermanentDeleteConfirmText}”。");

        _log.Info("Clean", "StartClean", $"开始清理：{targets.Count} 项，动作：{ActionText(action)}");

        if (_settings.Current.CreateRestorePoint)
        {
            progress?.Report("正在创建系统还原点…");
            var ok = await _restore.TryCreateRestorePointAsync(
                $"WinCleaner 清理前还原点 {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                cancellationToken).ConfigureAwait(false);

            _log.Info("System", "CreateRestorePoint", ok ? "系统还原点创建成功" : "系统还原点创建失败（可能未开启系统保护）");
            result.Messages.Add(ok ? "已创建系统还原点。" : "未能创建系统还原点（可能系统保护未开启）。");
        }

        foreach (var item in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.IsExcluded)
            {
                result.SkippedCount++;
                item.OperationResult = "已跳过（白名单）";
                _log.Warning("Clean", ActionText(action), "命中白名单，跳过", item.Path, item.SizeBytes);
                continue;
            }

            if (PathUtils.IsCriticalPath(item.Path))
            {
                result.SkippedCount++;
                item.OperationResult = "已跳过（系统关键路径）";
                _log.Warning("Clean", ActionText(action), "系统关键路径，拒绝处理", item.Path, item.SizeBytes);
                continue;
            }

            if (item.Risk == RiskLevel.High && !allowHighRisk)
            {
                result.SkippedCount++;
                item.OperationResult = "已跳过（高风险未授权）";
                _log.Warning("Clean", ActionText(action), "高风险条目，未授权处理", item.Path, item.SizeBytes);
                continue;
            }

            // 受保护内容（用户数据 / 游戏 / 开发项目 / 系统关键路径）：
            // 只允许可恢复的隔离与回收站，永久删除一律拒绝。
            if (item.IsProtected && action == CleanActionType.PermanentDelete)
            {
                result.SkippedCount++;
                item.OperationResult = "已跳过（受保护内容，禁止永久删除）";
                _log.Warning("Clean", ActionText(action),
                    "受保护内容，禁止永久删除：" + (item.ProtectionReason ?? ""), item.Path, item.SizeBytes);
                continue;
            }

            if (item.IsProtected)
            {
                _log.Warning("Clean", ActionText(action),
                    "受保护内容仍被处理（可恢复动作）：" + (item.ProtectionReason ?? ""), item.Path, item.SizeBytes);
            }

            try
            {
                progress?.Report($"正在处理：{item.Path}");

                switch (action)
                {
                    case CleanActionType.Quarantine:
                        var entry = await _quarantine.MoveToQuarantineAsync(
                            item.Path, item.DisplayName, progress, cancellationToken, QuarantineContext.FromItem(item))
                            .ConfigureAwait(false);
                        item.OperationResult = $"已移到隔离区（{entry.Id}）";
                        break;

                    case CleanActionType.RecycleBin:
                        var success = await Task.Run(() => RecycleBin.Send(item.Path), cancellationToken).ConfigureAwait(false);
                        if (!success)
                        {
                            result.FailedCount++;
                            item.OperationResult = "删除到回收站失败（未做任何改动）";
                            _log.Error("Clean", ActionText(action), "删除到回收站失败", item.Path, item.SizeBytes);
                            continue;
                        }
                        item.OperationResult = "已删除到回收站";
                        break;

                    case CleanActionType.PermanentDelete:
                        if (!DirectoryHelper.Delete(item.Path))
                        {
                            result.FailedCount++;
                            item.OperationResult = "永久删除失败";
                            _log.Error("Clean", ActionText(action), "永久删除失败（可能有文件被占用）", item.Path, item.SizeBytes);
                            continue;
                        }
                        item.OperationResult = "已永久删除";
                        break;
                }

                result.SuccessCount++;
                result.TotalBytes += item.SizeBytes;
                _log.Log(LogLevel.Success, "Clean", ActionText(action), item.Path, item.SizeBytes, "Success", item.OperationResult ?? string.Empty);
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                item.OperationResult = "处理失败：" + ex.Message;
                _log.Error("Clean", ActionText(action), ex.Message, item.Path, item.SizeBytes);
            }
        }

        _log.Info("Clean", "FinishClean", result.Summary);
        result.Messages.Add(result.Summary);
        return result;
    }

    public static string ActionText(CleanActionType action) => action switch
    {
        CleanActionType.Quarantine => "移到隔离区",
        CleanActionType.RecycleBin => "删除到回收站",
        CleanActionType.PermanentDelete => "永久删除",
        _ => "未知操作",
    };
}

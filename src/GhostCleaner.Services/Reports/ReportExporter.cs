using System.Text;
using System.Text.Json;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Core.Utils;

namespace GhostCleaner.Services.Reports;

/// <summary>扫描报告 / 清理报告导出（JSON 或 CSV，按扩展名自动判断）。</summary>
public sealed class ReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<string> ExportScanReportAsync(
        IEnumerable<OrphanItem> items,
        string filePath,
        IReadOnlyList<AssociationSourceHealth>? sourceHealth = null,
        CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        var health = sourceHealth ?? Array.Empty<AssociationSourceHealth>();
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 采集源健康度：决定置信度上限，必须与结果一起导出，否则报告无法解释"置信度为何被压低"
        var healthSummary = BuildHealthSummary(health);
        var unhealthyText = health.Count == 0
            ? "未提供"
            : healthSummary.UnhealthyCount == 0
                ? "全部成功"
                : string.Join(" | ", health
                    .Where(h => h.Status != AssociationSourceStatus.Success)
                    .Select(h => $"{h.SourceName}={h.StatusText}"));

        if (IsCsv(filePath))
        {
            var lines = new List<string>
            {
                Csv("路径", "推测软件名", "版本", "厂商", "总大小", "大小(字节)", "文件数", "最后修改", "主要exe", "有卸载程序",
                    "疑似残留度", "置信等级", "风险", "建议", "目录性质", "是否受保护", "判定原因", "是否排除", "排除原因", "用户数据", "警告",
                    "关联源健康度"),
            };

            foreach (var item in list)
            {
                lines.Add(Csv(
                    item.Path,
                    item.DisplayName,
                    item.Version ?? "",
                    item.Publisher ?? "",
                    item.SizeText,
                    item.SizeBytes.ToString(),
                    item.FileCount.ToString(),
                    item.LastModifiedText,
                    string.Join(" | ", item.MainExecutables),
                    item.HasUninstaller ? "是" : "否",
                    item.ConfidenceText,
                    item.ConfidenceLevelText,
                    item.RiskText,
                    item.RecommendationText,
                    item.CategoryText,
                    item.IsProtected ? "是" : "否",
                    item.ReasonText,
                    item.IsExcluded ? "是" : "否",
                    item.ExcludeReason ?? "",
                    item.HasUserData ? "是" : "否",
                    string.Join(" | ", item.Warnings.Select(w => w.Message)),
                    unhealthyText));
            }

            await File.WriteAllLinesAsync(filePath, lines, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
            return filePath;
        }

        var payload = new
        {
            GeneratedAt = DateTime.Now,
            TotalCount = list.Count,
            TotalSizeBytes = list.Sum(x => x.SizeBytes),

            // v0.1：采集源健康度。置信度上限由它决定，必须与结果一起导出，
            // 否则报告无法解释"为什么某个目录的置信度被压到 55/65/45"。
            AssociationSourceHealth = health.Select(h => new
            {
                h.SourceName,
                CoveredSources = h.CoveredSources.Select(s => s.ToString()),
                Status = h.Status.ToString(),
                StatusText = h.StatusText,
                h.ItemCount,
                h.ErrorMessage,
            }),
            AssociationHealth = healthSummary,

            Items = list.Select(x => new
            {
                x.Path,
                x.DisplayName,
                x.Version,
                x.Publisher,
                x.ProductName,
                x.SizeBytes,
                SizeText = x.SizeText,
                x.FileCount,
                LastModified = x.LastModified,
                MainExecutables = x.MainExecutables,
                x.HasUninstaller,
                x.UninstallerPath,
                Risk = x.RiskText,
                x.RiskScore,

                // v0.1：可解释性
                x.ConfidenceScore,
                ConfidenceLevel = x.ConfidenceLevelText,
                Recommendation = x.RecommendationText,
                DirectoryCategory = x.CategoryText,
                x.IsProtected,
                x.ProtectionReason,
                Evidence = x.Evidence.Select(e => new
                {
                    Type = e.Type.ToString(),
                    e.Title,
                    e.Description,
                    e.Score,
                    e.IsPositive,
                    Severity = e.Severity.ToString(),
                }),
                Warnings = x.Warnings,

                Reasons = x.Reasons,
                x.HasUserData,
                UserDataSamples = x.UserDataSamples,
                x.IsExcluded,
                x.ExcludeReason,
                Associations = x.Associations.Select(a => new { Source = a.Source.ToString(), a.Name, a.TargetPath }),
            }),
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(payload, JsonOptions), cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    public async Task<string> ExportCleanReportAsync(IEnumerable<LogEntry> entries, string filePath, CancellationToken cancellationToken = default)
    {
        var list = entries.ToList();
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (IsCsv(filePath))
        {
            var lines = new List<string> { Csv("时间", "级别", "分类", "操作", "路径", "大小", "结果", "说明") };
            foreach (var e in list)
                lines.Add(Csv(e.TimestampText, e.Level.ToString(), e.Category, e.Action, e.Path, e.SizeText, e.Result, e.Message));

            await File.WriteAllLinesAsync(filePath, lines, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
            return filePath;
        }

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, JsonOptions), cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    public async Task<string> ExportQuarantineReportAsync(IEnumerable<QuarantineEntry> entries, string filePath, CancellationToken cancellationToken = default)
    {
        var list = entries.ToList();
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (IsCsv(filePath))
        {
            var lines = new List<string> { Csv("ID", "原始路径", "隔离路径", "大小", "文件数", "隔离时间", "过期时间", "状态") };
            foreach (var e in list)
                lines.Add(Csv(e.Id, e.OriginalPath, e.QuarantinePath, e.SizeText, e.FileCount.ToString(),
                    e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), e.ExpireAt.ToString("yyyy-MM-dd HH:mm:ss"), e.StatusText));

            await File.WriteAllLinesAsync(filePath, lines, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
            return filePath;
        }

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, JsonOptions), cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    /// <summary>
    /// 采集源健康度概览。只做统计，不复算置信度上限
    /// （上限逻辑在 GhostCleaner.Scanner 的 OrphanConfidenceEvaluator 中，Services 不依赖 Scanner）。
    /// </summary>
    private static HealthSummary BuildHealthSummary(IReadOnlyList<AssociationSourceHealth> health)
    {
        var unhealthy = health
            .Where(h => h.Status is AssociationSourceStatus.Failed
                                  or AssociationSourceStatus.Partial
                                  or AssociationSourceStatus.NotRun)
            .ToList();

        return new HealthSummary(
            health.Count,
            unhealthy.Count,
            health.Count > 0 && unhealthy.Count == 0,
            health.Count > 0,
            health.Sum(h => h.ItemCount),
            unhealthy.Select(h => h.SourceName).ToList(),
            health.Count == 0
                ? "未提供采集源健康度，无法判断置信度是否被限制"
                : unhealthy.Count == 0
                    ? "全部采集源均成功完成"
                    : $"{unhealthy.Count}/{health.Count} 个采集源未完成，置信度已被上限约束，结果可能不完整");
    }

    /// <summary>采集源健康度概览（随扫描报告一同导出）。</summary>
    private sealed record HealthSummary(
        int SourceCount,
        int UnhealthyCount,
        bool IsFullyHealthy,
        bool IsHealthDataProvided,
        long TotalRecords,
        IReadOnlyList<string> UnhealthySourceNames,
        string Note);

    private static bool IsCsv(string filePath)
        => System.IO.Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    private static string Csv(params string[] values)
        => string.Join(",", values.Select(v =>
        {
            var s = (v ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            if (s.Contains(',') || s.Contains('"')) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }));
}

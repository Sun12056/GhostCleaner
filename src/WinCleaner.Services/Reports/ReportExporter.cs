using System.Text;
using System.Text.Json;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Reports;

/// <summary>扫描报告 / 清理报告导出（JSON 或 CSV，按扩展名自动判断）。</summary>
public sealed class ReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<string> ExportScanReportAsync(IEnumerable<OrphanItem> items, string filePath, CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (IsCsv(filePath))
        {
            var lines = new List<string>
            {
                Csv("路径", "推测软件名", "版本", "厂商", "总大小", "大小(字节)", "文件数", "最后修改", "主要exe", "有卸载程序",
                    "疑似残留度", "置信等级", "风险", "建议", "目录性质", "是否受保护", "判定原因", "是否排除", "排除原因", "用户数据", "警告"),
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
                    string.Join(" | ", item.Warnings)));
            }

            await File.WriteAllLinesAsync(filePath, lines, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
            return filePath;
        }

        var payload = new
        {
            GeneratedAt = DateTime.Now,
            TotalCount = list.Count,
            TotalSizeBytes = list.Sum(x => x.SizeBytes),
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

using WinCleaner.Core.Models;
using WinCleaner.Services.Logging;
using WinCleaner.Services.Reports;
using Xunit;

namespace WinCleaner.Tests;

public class ReportAndLogTests : IDisposable
{
    private readonly string _root;

    public ReportAndLogTests()
    {
        _root = System.IO.Path.Combine(AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 忽略清理失败 */ }
        GC.SuppressFinalize(this);
    }

    private static List<OrphanItem> SampleItems() => new()
    {
        new OrphanItem
        {
            Path = @"D:\Games\OldApp",
            DisplayName = "OldApp",
            Publisher = "Vendor",
            Version = "1.0",
            SizeBytes = 1024 * 1024 * 512,
            FileCount = 321,
            LastModified = DateTime.Now.AddDays(-400),
            HasUninstaller = true,
            Risk = RiskLevel.Low,
            Reasons = new List<string> { "无任何系统引用" },
        },
    };

    [Fact]
    public async Task ExportScanReport_Csv_ContainsHeaderAndPath()
    {
        var exporter = new ReportExporter();
        var file = System.IO.Path.Combine(_root, "scan.csv");

        await exporter.ExportScanReportAsync(SampleItems(), file);

        var text = File.ReadAllText(file);
        Assert.Contains("推测软件名", text);
        Assert.Contains(@"D:\Games\OldApp", text);
        Assert.Contains("OldApp", text);
    }

    [Fact]
    public async Task ExportScanReport_Json_IsValidJson()
    {
        var exporter = new ReportExporter();
        var file = System.IO.Path.Combine(_root, "scan.json");

        await exporter.ExportScanReportAsync(SampleItems(), file);

        var json = File.ReadAllText(file);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("Items", out var items));
        Assert.Equal(1, items.GetArrayLength());
    }

    [Fact]
    public async Task ExportCleanReport_Csv_Works()
    {
        var exporter = new ReportExporter();
        var file = System.IO.Path.Combine(_root, "clean.csv");

        var logs = new List<LogEntry>
        {
            new() { Category = "Clean", Action = "移到隔离区", Path = @"D:\Games\OldApp", SizeBytes = 1024, Result = "Success", Message = "ok" },
        };

        await exporter.ExportCleanReportAsync(logs, file);

        var text = File.ReadAllText(file);
        Assert.Contains("移到隔离区", text);
        Assert.Contains(@"D:\Games\OldApp", text);
    }

    [Fact]
    public async Task LogService_WritesAndLoadsEntries()
    {
        var log = new LogService(_root);

        log.Info("Scan", "Scan", "测试日志", @"D:\Games\OldApp", 2048);
        log.Error("Clean", "Delete", "失败测试");

        var entries = await log.LoadAsync();

        Assert.Contains(entries, e => e.Message == "测试日志" && e.Category == "Scan");
        Assert.Contains(entries, e => e.Level == LogLevel.Error);
        Assert.True(File.Exists(System.IO.Path.Combine(log.LogDirectory, $"wincleaner-{DateTime.Now:yyyy-MM-dd}.jsonl")));
    }

    [Fact]
    public async Task SettingsService_RoundTrip()
    {
        var settingsService = new WinCleaner.Services.Settings.SettingsService(_root);
        settingsService.Current.QuarantineRoot = System.IO.Path.Combine(_root, "MyQuarantine");
        settingsService.Current.KeepKeywords.Add("我的项目");
        await settingsService.SaveAsync();

        var reloaded = new WinCleaner.Services.Settings.SettingsService(_root);

        Assert.Equal(System.IO.Path.Combine(_root, "MyQuarantine"), reloaded.Current.QuarantineRoot);
        Assert.Contains("我的项目", reloaded.Current.KeepKeywords);
    }
}

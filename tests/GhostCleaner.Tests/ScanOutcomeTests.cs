using System.Text;
using GhostCleaner.Core.Models;
using GhostCleaner.Scanner;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// ScanOutcome 契约测试。
///
/// 它要守住的是一条 v0.1 核心原则：
/// <b>结果条目、采集源健康度、置信度上限必须同批产生，不可分离。</b>
///
/// 分离会直接导致"把没观测当成观测到失败"：消费方另取健康度时若拿到未构建的空索引，
/// 一次完全健康的扫描会被误报成"严重不完整（上限 45）"。
/// </summary>
public class ScanOutcomeTests : IDisposable
{
    private readonly string _root;

    public ScanOutcomeTests()
    {
        _root = System.IO.Path.Combine(AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 忽略清理失败 */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HealthyScan_OutcomeCarriesHealthAndFullCap()
    {
        var appDir = CreateAbandonedTool();

        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), new StubAssociationIndex());

        var outcome = await scanner.ScanWithOutcomeAsync(
            new ScanOptions { RootPaths = new[] { _root }, MaxDepth = 1 });

        // 健康度必须与结果同批产生，不能为空
        Assert.NotEmpty(outcome.SourceHealth);
        Assert.True(outcome.IsAssociationDataFullyHealthy);
        Assert.Equal(0, outcome.UnhealthySourceCount);

        // 全部成功时不应施加任何上限
        Assert.Equal(100, outcome.ConfidenceCap);
        Assert.False(outcome.IsConfidenceCapped);
        Assert.Null(outcome.ConfidenceCapNote);

        // 条目仍然正常产出，且兼容入口与推荐入口一致
        Assert.Contains(outcome.Items, i => i.Path.Equals(appDir, StringComparison.OrdinalIgnoreCase));
        var legacy = await scanner.ScanAsync(new ScanOptions { RootPaths = new[] { _root }, MaxDepth = 1 });
        Assert.Equal(outcome.Items.Count, legacy.Count);
    }

    [Fact]
    public async Task RegistryFailed_OutcomeCarriesCappedConfidence()
    {
        CreateAbandonedTool();

        var index = new StubAssociationIndex(s => s == AssociationSource.RegistryUninstall
            ? AssociationSourceStatus.Failed
            : AssociationSourceStatus.Success);
        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), index);

        var outcome = await scanner.ScanWithOutcomeAsync(
            new ScanOptions { RootPaths = new[] { _root }, MaxDepth = 1 });

        Assert.NotEmpty(outcome.SourceHealth);
        Assert.False(outcome.IsAssociationDataFullyHealthy);
        Assert.True(outcome.UnhealthySourceCount >= 1);
        Assert.Contains(outcome.UnhealthySourceNames, n => n.Contains("注册表"));

        // 上限必须被施加，并且每一个条目的置信度都不能越过它
        var expectedCap = EvidenceWeights.Default.MaxConfidenceWhenRegistryUnavailable;
        Assert.Equal(expectedCap, outcome.ConfidenceCap);
        Assert.True(outcome.IsConfidenceCapped);
        Assert.NotNull(outcome.ConfidenceCapNote);
        Assert.All(outcome.Items, i => Assert.True(i.ConfidenceScore <= outcome.ConfidenceCap));
    }

    [Fact]
    public async Task NoRoots_OutcomeIsEmptyButWellFormed()
    {
        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), new StubAssociationIndex());

        var outcome = await scanner.ScanWithOutcomeAsync(new ScanOptions());

        Assert.Empty(outcome.Items);
        Assert.NotNull(outcome.SourceHealth);
        Assert.NotNull(outcome.ConfidenceCapNote); // 必须给出原因，不能静默返回
    }

    private string CreateAbandonedTool()
    {
        var appDir = System.IO.Path.Combine(_root, "AbandonedTool");
        Directory.CreateDirectory(appDir);

        var oldTime = DateTime.Now.AddYears(-2);
        WriteFile(System.IO.Path.Combine(appDir, "AbandonedTool.exe"), 4096, oldTime);
        WriteFile(System.IO.Path.Combine(appDir, "core.dll"), 2048, oldTime);
        WriteFile(System.IO.Path.Combine(appDir, "unins000.exe"), 1024, oldTime);
        Directory.SetLastWriteTime(appDir, oldTime);

        return appDir;
    }

    private static void WriteFile(string path, int size, DateTime? lastWriteTime = null)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', size), Encoding.ASCII);
        if (lastWriteTime.HasValue) File.SetLastWriteTime(path, lastWriteTime.Value);
    }
}

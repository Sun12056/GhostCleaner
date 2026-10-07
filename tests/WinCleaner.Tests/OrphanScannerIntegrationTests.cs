using System.Text;
using WinCleaner.Core.Models;
using WinCleaner.Registry;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>
/// 端到端扫描测试：在临时目录中构造"像软件安装目录"的样本，
/// 用真实的关联索引（注册表/进程/服务等）验证它会被判为孤儿残留。
/// </summary>
public class OrphanScannerIntegrationTests : IDisposable
{
    private readonly string _root;

    public OrphanScannerIntegrationTests()
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
    public async Task ScanAsync_FindsOrphanSoftwareDirectory()
    {
        // 构造一个"老软件目录"：主程序 + dll + 卸载器 + 资源目录，最后修改时间很久以前
        var appDir = System.IO.Path.Combine(_root, "AbandonedTool");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(System.IO.Path.Combine(appDir, "resources"));

        WriteFile(System.IO.Path.Combine(appDir, "AbandonedTool.exe"), 4096);
        WriteFile(System.IO.Path.Combine(appDir, "core.dll"), 2048);
        WriteFile(System.IO.Path.Combine(appDir, "unins000.exe"), 1024);
        WriteFile(System.IO.Path.Combine(appDir, "resources", "icon.png"), 512);

        Directory.SetLastWriteTime(appDir, DateTime.Now.AddYears(-2));

        // 构造一个"不像软件"的目录：只有文档
        var docsDir = System.IO.Path.Combine(_root, "MyDocuments");
        Directory.CreateDirectory(docsDir);
        WriteFile(System.IO.Path.Combine(docsDir, "notes.txt"), 128);

        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), new AssociationIndex());

        var options = new ScanOptions
        {
            RootPaths = new[] { _root },
            MaxDepth = 1,
        };

        var items = await scanner.ScanAsync(options);

        Assert.Contains(items, i => i.Path.Equals(appDir, StringComparison.OrdinalIgnoreCase));

        var found = items.First(i => i.Path.Equals(appDir, StringComparison.OrdinalIgnoreCase));
        Assert.True(found.HasUninstaller);
        Assert.NotEmpty(found.Reasons);
        Assert.Contains(found.Reasons, r => r.Contains("无任何系统引用") || r.Contains("仍被"));
        Assert.NotEqual(RiskLevel.High, found.Risk); // 非系统盘、非关键目录，不应是高风险

        // 纯文档目录不应被识别为软件残留
        Assert.DoesNotContain(items, i => i.Path.Equals(docsDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanAsync_RespectsKeepKeywords()
    {
        var appDir = System.IO.Path.Combine(_root, "SteamLibrary");
        Directory.CreateDirectory(appDir);
        WriteFile(System.IO.Path.Combine(appDir, "game.exe"), 2048);
        WriteFile(System.IO.Path.Combine(appDir, "engine.dll"), 2048);

        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), new AssociationIndex());

        var options = new ScanOptions
        {
            RootPaths = new[] { _root },
            MaxDepth = 1,
            KeepKeywords = new[] { "SteamLibrary" },
        };

        var items = await scanner.ScanAsync(options);

        Assert.DoesNotContain(items, i => i.Path.Equals(appDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanAsync_CanBeCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var scanner = new OrphanScanner(new SoftwareDirectoryInspector(), new AssociationIndex());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(new ScanOptions { RootPaths = new[] { _root } }, null, cts.Token));
    }

    private static void WriteFile(string path, int size)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', size), Encoding.ASCII);
    }
}

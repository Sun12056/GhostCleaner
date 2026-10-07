using System.Text;
using WinCleaner.Core.Models;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>用真实的临时目录验证"像不像软件安装目录"的判定。</summary>
public class SoftwareDirectoryInspectorTests : IDisposable
{
    private readonly string _root;
    private readonly SoftwareDirectoryInspector _inspector = new();

    public SoftwareDirectoryInspectorTests()
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
    public async Task TypicalSoftwareDirectory_IsDetected()
    {
        var dir = CreateDirectory("MyApp");
        CreateFile(dir, "MyApp.exe", 2048);
        CreateFile(dir, "core.dll", 4096);
        CreateFile(dir, "helper.dll", 1024);
        CreateFile(dir, "unins000.exe", 512);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "resources"));
        CreateFile(System.IO.Path.Combine(dir, "resources"), "icon.png", 128);

        var info = await _inspector.InspectAsync(dir, new ScanOptions());

        Assert.NotNull(info);
        Assert.True(info!.IsLikelySoftwareDirectory, "典型的软件目录应被识别");
        Assert.True(info.HasUninstaller);
        Assert.Contains("unins000.exe", info.UninstallerPath!);
        Assert.Equal(2, info.ExecutableCount); // MyApp.exe + unins000.exe
        Assert.True(info.TotalSizeBytes > 0);
        Assert.Contains("resources", info.ResourceDirectories);
    }

    [Fact]
    public async Task MediaLibraryDirectory_IsNotDetectedAsSoftware()
    {
        var dir = CreateDirectory("Movies");
        for (int i = 0; i < 10; i++) CreateFile(dir, $"movie{i}.mkv", 1024);
        CreateFile(dir, "player.exe", 512);

        var info = await _inspector.InspectAsync(dir, new ScanOptions());

        Assert.NotNull(info);
        Assert.False(info!.IsLikelySoftwareDirectory, "媒体库不应被识别为软件目录");
    }

    [Fact]
    public async Task DirectoryWithoutExecutable_NeverTreatedAsSoftware()
    {
        var dir = CreateDirectory("DataFiles");
        CreateFile(dir, "notes.txt", 128);
        CreateFile(dir, "config.ini", 128);

        var info = await _inspector.InspectAsync(dir, new ScanOptions());

        Assert.NotNull(info);
        Assert.False(info!.IsLikelySoftwareDirectory, "没有 exe 的目录绝不视为软件目录");
    }

    [Fact]
    public async Task UserDataDirectory_IsMarked()
    {
        var dir = CreateDirectory("OldTool");
        CreateFile(dir, "tool.exe", 1024);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "config"));
        CreateFile(System.IO.Path.Combine(dir, "config"), "settings.ini", 64);

        var info = await _inspector.InspectAsync(dir, new ScanOptions());

        Assert.NotNull(info);
        Assert.True(info!.HasUserData);
    }

    private string CreateDirectory(string name)
    {
        var path = System.IO.Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string directory, string fileName, int size)
    {
        var path = System.IO.Path.Combine(directory, fileName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, new string('x', size), Encoding.ASCII);
    }
}

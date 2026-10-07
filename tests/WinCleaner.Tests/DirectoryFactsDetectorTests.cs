using System.Text;
using WinCleaner.Core.Models;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>用真实临时目录验证 Portable / 游戏 / 开发项目 / 用户数据的识别。</summary>
public class DirectoryFactsDetectorTests : IDisposable
{
    private readonly string _root;
    private readonly SoftwareDirectoryInspector _inspector = new();

    public DirectoryFactsDetectorTests()
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
    public async Task PortableToolLayout_IsDetectedAsPortable()
    {
        var dir = Create("Tool");
        CreateFile(dir, "Tool.exe", 1024);
        CreateFile(dir, "config.json", 128);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "data"));

        var facts = await Detect(dir);

        Assert.True(facts.IsPortableSoftware);
        Assert.Equal("绿色软件", facts.PrimaryCategory);
    }

    [Fact]
    public async Task PortableNameHint_IsDetectedAsPortable()
    {
        var dir = Create("MyTool-Portable");
        CreateFile(dir, "tool.exe", 1024);

        var facts = await Detect(dir);

        Assert.True(facts.IsPortableSoftware);
    }

    [Fact]
    public async Task SteamGameLayout_IsDetectedAsGame()
    {
        var dir = Create("steamapps");
        var common = Directory.CreateDirectory(System.IO.Path.Combine(dir, "common")).FullName;
        var game = Directory.CreateDirectory(System.IO.Path.Combine(common, "MyGame")).FullName;
        CreateFile(game, "steam_api64.dll", 512);
        CreateFile(game, "MyGame.exe", 2048);

        var facts = await Detect(game);

        Assert.True(facts.IsGameDirectory);
        Assert.Equal("游戏", facts.PrimaryCategory);
        Assert.True(facts.IsProtected);
    }

    [Fact]
    public async Task GitProject_IsDetectedAsDevelopmentProject()
    {
        var dir = Create("TestProject");
        Directory.CreateDirectory(System.IO.Path.Combine(dir, ".git"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "src"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "node_modules"));
        CreateFile(dir, "package.json", 128);

        var facts = await Detect(dir);

        Assert.True(facts.IsDevelopmentProject);
        Assert.Equal("开发项目", facts.PrimaryCategory);
        Assert.True(facts.IsProtected);
    }

    [Fact]
    public async Task DirectoryWithSaves_IsMarkedAsUserData()
    {
        var dir = Create("OldApp");
        CreateFile(dir, "app.exe", 1024);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "save"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "profile"));

        var facts = await Detect(dir);

        Assert.True(facts.HasSaveData);
        Assert.True(facts.HasProfileData);
        Assert.True(facts.HasUserData);
        Assert.True(facts.IsProtected);
    }

    [Fact]
    public async Task DirectoryWithBackups_IsMarkedAsUserData()
    {
        var dir = Create("OldApp");
        CreateFile(dir, "app.exe", 1024);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "backup"));

        var facts = await Detect(dir);

        Assert.True(facts.HasBackupData);
        Assert.True(facts.IsProtected);
    }

    [Fact]
    public async Task PlainSoftwareDirectory_IsNotProtected()
    {
        var dir = Create("NormalApp");
        CreateFile(dir, "NormalApp.exe", 2048);
        CreateFile(dir, "core.dll", 2048);
        CreateFile(dir, "unins000.exe", 1024);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "resources"));

        var facts = await Detect(dir);

        Assert.False(facts.IsPortableSoftware);
        Assert.False(facts.IsGameDirectory);
        Assert.False(facts.IsDevelopmentProject);
        Assert.False(facts.IsProtected);
    }

    [Theory]
    [InlineData(@"C:\ProgramData\SomeApp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\SomeApp")]
    public void CriticalPath_IsAlwaysProtected(string path)
    {
        var facts = DirectoryFactsDetector.Detect(new SoftwareDirectoryInfo { Path = path, Name = "SomeApp" });

        Assert.True(facts.IsCriticalPath);
        Assert.True(facts.IsProtected);
    }

    [Theory]
    [InlineData(@"C:\Program Files\SomeApp")]
    [InlineData(@"C:\Program Files (x86)\SomeApp")]
    [InlineData(@"C:\Windows\SomeApp")]
    public void SystemInstallRoot_IsProtected(string path)
    {
        var facts = DirectoryFactsDetector.Detect(new SoftwareDirectoryInfo { Path = path, Name = "SomeApp" });

        Assert.True(facts.IsSystemInstallRoot);
        Assert.True(facts.IsProtected, "系统安装目录必须受到保护，不参与自动清理");
    }

    [Fact]
    public void GamesContainerFolder_IsNotTreatedAsGameDirectory()
    {
        var facts = DirectoryFactsDetector.Detect(new SoftwareDirectoryInfo
        {
            Path = @"D:\Games\OldApp",
            Name = "OldApp",
        });

        Assert.False(facts.IsGameDirectory, "D:\\Games\\OldApp 这种普通容器下的软件不应被当成游戏目录");
        Assert.False(facts.IsProtected);
    }

    private async Task<DirectoryFacts> Detect(string dir)
    {
        var info = await _inspector.InspectAsync(dir, new ScanOptions());
        Assert.NotNull(info);
        return DirectoryFactsDetector.Detect(info!);
    }

    private string Create(string name)
    {
        var path = System.IO.Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string directory, string fileName, int size)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Combine(directory, fileName), new string('x', size), Encoding.ASCII);
    }
}

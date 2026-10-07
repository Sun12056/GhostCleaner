using System.Text;
using WinCleaner.Services.Quarantine;
using WinCleaner.Services.Settings;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>隔离区：移入 → 列出 → 恢复 → 彻底删除，以及安全拒绝逻辑。</summary>
public class QuarantineManagerTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsService _settings;
    private readonly QuarantineManager _manager;

    public QuarantineManagerTests()
    {
        _root = System.IO.Path.Combine(AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _settings = new SettingsService(_root);
        _settings.Current.QuarantineRoot = System.IO.Path.Combine(_root, "Quarantine");
        _settings.Current.RetentionDays = 7;

        _manager = new QuarantineManager(_settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 忽略清理失败 */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MoveToQuarantine_ThenRestore_RestoresOriginalDirectory()
    {
        var source = CreateSoftwareDirectory("TestApp");

        var entry = await _manager.MoveToQuarantineAsync(source, "TestApp");

        Assert.False(Directory.Exists(source));
        Assert.True(Directory.Exists(entry.QuarantinePath));
        Assert.True(File.Exists(System.IO.Path.Combine(entry.QuarantinePath, "app.exe")));
        Assert.Equal(source, entry.OriginalPath);
        Assert.True(entry.ExpireAt > entry.CreatedAt);

        var list = await _manager.ListAsync();
        Assert.Single(list);

        var restored = await _manager.RestoreAsync(entry.Id);
        Assert.True(restored);
        Assert.True(Directory.Exists(source));
        Assert.True(File.Exists(System.IO.Path.Combine(source, "app.exe")));
    }

    [Fact]
    public async Task Delete_RemovesQuarantinedDirectory()
    {
        var source = CreateSoftwareDirectory("ToDelete");
        var entry = await _manager.MoveToQuarantineAsync(source);

        var deleted = await _manager.DeleteAsync(entry.Id);

        Assert.True(deleted);
        Assert.False(Directory.Exists(entry.QuarantinePath));
        Assert.Empty(await _manager.ListAsync());
    }

    [Fact]
    public async Task MoveToQuarantine_RefusesCriticalPath()
    {
        var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _manager.MoveToQuarantineAsync(systemDir));
    }

    [Fact]
    public async Task MoveToQuarantine_ThrowsForMissingSource()
    {
        await Assert.ThrowsAsync<System.IO.DirectoryNotFoundException>(
            () => _manager.MoveToQuarantineAsync(System.IO.Path.Combine(_root, "NotExist")));
    }

    [Fact]
    public async Task RetentionDays_IsClampedBetween7And30()
    {
        _settings.Current.RetentionDays = 999;
        _settings.Current.Normalize();
        Assert.Equal(30, _settings.Current.RetentionDays);

        _settings.Current.RetentionDays = 1;
        _settings.Current.Normalize();
        Assert.Equal(7, _settings.Current.RetentionDays);

        await Task.CompletedTask;
    }

    private string CreateSoftwareDirectory(string name)
    {
        var path = System.IO.Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(System.IO.Path.Combine(path, "app.exe"), new string('a', 1024), Encoding.ASCII);
        Directory.CreateDirectory(System.IO.Path.Combine(path, "bin"));
        File.WriteAllText(System.IO.Path.Combine(path, "bin", "helper.dll"), new string('b', 512), Encoding.ASCII);
        return path;
    }
}

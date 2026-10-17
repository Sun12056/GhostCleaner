using System.Text.Json;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Core.Utils;

namespace GhostCleaner.Services.Settings;

/// <summary>配置持久化（System.Text.Json）。默认存放在程序目录下的 Data 子目录，避免写入系统目录。</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string SettingsFilePath { get; }

    public AppSettings Current { get; private set; }

    public SettingsService(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? DefaultBaseDirectory();
        Directory.CreateDirectory(dir);
        SettingsFilePath = System.IO.Path.Combine(dir, "settings.json");
        Current = LoadSync();
    }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Current = LoadSync();
        return Task.FromResult(Current);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Current.Normalize();
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        return File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
    }

    private AppSettings LoadSync()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    settings.Normalize();
                    if (string.IsNullOrWhiteSpace(settings.QuarantineRoot))
                        settings.QuarantineRoot = DriveHelper.SuggestQuarantineRoot();
                    return settings;
                }
            }
        }
        catch
        {
            // 配置文件损坏时回退默认值
        }

        return CreateDefault();
    }

    private static AppSettings CreateDefault()
    {
        var settings = new AppSettings
        {
            QuarantineRoot = DriveHelper.SuggestQuarantineRoot(),
        };
        settings.Normalize();
        return settings;
    }

    private static string DefaultBaseDirectory()
    {
        var baseDir = AppContext.BaseDirectory;

        // 程序装在 Program Files / Windows 目录时不写入程序目录，改用当前用户目录
        if (PathUtils.ContainsKeyword(baseDir, new[] { "program files", "windows" }))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 品牌统一为 GhostCleaner（UDR-11）。
            //
            // ⚠️ 这里不能直接改名：旧目录里可能存着用户的隔离区文件与扫描记录，
            // 一旦换成新目录，用户会以为数据被删了 —— 隔离区里放的是用户还没决定
            // 要不要永久删除的文件，丢不得。
            //
            // 所以：新目录优先；新目录不存在、而旧目录存在时，继续沿用旧目录。
            var branded = System.IO.Path.Combine(localAppData, "GhostCleaner");
            var legacy = System.IO.Path.Combine(localAppData, "GhostCleaner");

            baseDir = System.IO.Directory.Exists(System.IO.Path.Combine(branded, "Data"))
                      || !System.IO.Directory.Exists(System.IO.Path.Combine(legacy, "Data"))
                ? branded
                : legacy;
        }

        return System.IO.Path.Combine(baseDir, "Data");
    }
}

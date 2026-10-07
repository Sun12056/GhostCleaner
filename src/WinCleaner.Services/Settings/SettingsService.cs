using System.Text.Json;
using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Services.Settings;

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
            baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinCleaner");

        return System.IO.Path.Combine(baseDir, "Data");
    }
}

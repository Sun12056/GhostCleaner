using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Win32;

namespace GhostCleaner.App.Services;

/// <summary>
/// ThemeService —— UI 0.2。
/// 颜色 token 分两本字典（Theme.Light / Theme.Dark），键名完全一致，运行时整本替换。
/// 主题偏好写到 %LOCALAPPDATA%\GhostCleaner\ui.json（不占用 Core 的 AppSettings，
/// 避免与 Core 0.1 并行开发产生冲突；UDR-07 落地后可迁移到 AppSettings.ThemeMode）。
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly string _settingsPath;
    private int _themeDictionaryIndex = -1;
    private bool _hookedSystemEvents;

    public ThemeService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GhostCleaner");

        // 独立文件：避免与 UiStateService 的 ui.json 互相覆盖
        _settingsPath = Path.Combine(dir, "theme.json");
        Mode = Load();
    }

    public ThemeMode Mode { get; private set; }

    public ThemeMode EffectiveMode =>
        Mode == ThemeMode.System ? (IsSystemDark() ? ThemeMode.Dark : ThemeMode.Light) : Mode;

    public event EventHandler? Changed;

    /// <summary>应用主题（会替换 Application 级资源字典中的 Theme.*.xaml）。</summary>
    public void Apply(ThemeMode mode)
    {
        Mode = mode;
        Save();
        HookSystemEvents(mode == ThemeMode.System);
        ReplaceDictionary(EffectiveMode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceDictionary(ThemeMode effective)
    {
        var app = Application.Current;
        if (app == null) return;

        var file = effective == ThemeMode.Dark ? "Theme.Dark.xaml" : "Theme.Light.xaml";
        var uri = new Uri($"Styles/Tokens/{file}", UriKind.Relative);

        try
        {
            var dictionary = new ResourceDictionary { Source = uri };
            var merged = app.Resources.MergedDictionaries;

            if (_themeDictionaryIndex < 0) _themeDictionaryIndex = FindThemeIndex(merged);

            if (_themeDictionaryIndex >= 0 && _themeDictionaryIndex < merged.Count)
                merged[_themeDictionaryIndex] = dictionary;
            else
            {
                merged.Add(dictionary);
                _themeDictionaryIndex = merged.Count - 1;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("主题加载失败：" + ex.Message);
        }
    }

    private static int FindThemeIndex(System.Collections.ObjectModel.Collection<ResourceDictionary> merged)
    {
        for (int i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString;
            if (source != null
                && source.Contains("Theme.", StringComparison.OrdinalIgnoreCase)
                && source.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private void HookSystemEvents(bool enable)
    {
        if (enable && !_hookedSystemEvents)
        {
            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _hookedSystemEvents = true;
            }
            catch
            {
                // 某些环境下 SystemEvents 不可用；此时跟随系统退化为启动时读取一次
            }
        }
        else if (!enable && _hookedSystemEvents)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _hookedSystemEvents = false;
            }
            catch
            {
                // 忽略
            }
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;

        if (Mode != ThemeMode.System) return;

        var app = Application.Current;
        if (app == null) return;

        app.Dispatcher.Invoke(() =>
        {
            ReplaceDictionary(EffectiveMode);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private static bool IsSystemDark()
    {
        try
        {
            // 必须完全限定：GhostCleaner.Registry 命名空间会遮蔽 Microsoft.Win32.Registry
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value) return value == 0;
        }
        catch
        {
            // 忽略
        }

        return false;
    }

    // ---------- 持久化 ----------

    private ThemeMode Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var data = JsonSerializer.Deserialize<UiSettings>(json);
                if (data != null && Enum.TryParse<ThemeMode>(data.Theme, ignoreCase: true, out var mode))
                    return mode;
            }
        }
        catch
        {
            // 读不到就用默认值
        }

        return ThemeMode.System;
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new UiSettings { Theme = Mode.ToString() });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // 保存失败不影响使用
        }
    }

    public void Dispose() => HookSystemEvents(false);

    private sealed class UiSettings
    {
        [JsonPropertyName("theme")]
        public string? Theme { get; set; }
    }
}

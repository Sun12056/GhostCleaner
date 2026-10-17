using System;

namespace GhostCleaner.App.Services;

/// <summary>主题模式。</summary>
public enum ThemeMode
{
    /// <summary>跟随系统（默认）。</summary>
    System = 0,

    Light = 1,

    Dark = 2,
}

/// <summary>
/// 主题服务：在运行时整本替换 Theme.Light.xaml / Theme.Dark.xaml。
/// 所有颜色资源必须使用 DynamicResource 引用，切换才能立即生效。
/// </summary>
public interface IThemeService
{
    ThemeMode Mode { get; }

    /// <summary>Mode 为 System 时解析出的实际主题。</summary>
    ThemeMode EffectiveMode { get; }

    void Apply(ThemeMode mode);

    /// <summary>主题发生变化（含跟随系统时系统主题切换）。</summary>
    event EventHandler? Changed;
}

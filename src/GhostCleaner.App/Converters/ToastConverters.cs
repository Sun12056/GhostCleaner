using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GhostCleaner.App.Services;

namespace GhostCleaner.App.Converters;

/// <summary>ToastLevel → 语义色画刷。</summary>
public sealed class ToastLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Info 用品牌色：配色表里没有 Brush.Info，且 Info 不该抢语义色的注意力 ——
        // 语义色（Success/Warning/Danger）必须只在真正需要时点亮。
        var key = value switch
        {
            ToastLevel.Success => "Brush.Success",
            ToastLevel.Warning => "Brush.Warning",
            ToastLevel.Danger => "Brush.Danger",
            _ => "Brush.Accent",
        };

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>ToastLevel → 图标资源。</summary>
public sealed class ToastLevelToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ToastLevel.Success => "Icon.CircleCheck",
            ToastLevel.Warning => "Icon.AlertTriangle",
            ToastLevel.Danger => "Icon.AlertCircle",
            _ => "Icon.Info",
        };

        return Application.Current.TryFindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

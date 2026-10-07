using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WinCleaner.Core.Models;

namespace WinCleaner.App.Converters;

/// <summary>风险等级 → 颜色。</summary>
public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RiskLevel risk)
        {
            return risk switch
            {
                RiskLevel.High => (Brush)Application.Current.FindResource("Brush.RiskHigh"),
                RiskLevel.Medium => (Brush)Application.Current.FindResource("Brush.RiskMedium"),
                _ => (Brush)Application.Current.FindResource("Brush.RiskLow"),
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → Visibility。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (parameter is string s && s.Equals("inverse", StringComparison.OrdinalIgnoreCase)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null → Collapsed，非 null → Visible。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → 是否可选（用于高风险/白名单行）。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

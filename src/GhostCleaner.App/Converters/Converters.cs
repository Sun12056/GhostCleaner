using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GhostCleaner.Core.Models;

namespace GhostCleaner.App.Converters;

// ============================================================
//  UI 0.2：以下转换器全部指向新的 Design Token（Brush.*）
//  旧键名 Brush.RiskHigh 等仍保留在 Styles/Colors.xaml 中，仅供移出 IA 的旧页面使用。
// ============================================================

/// <summary>资源键 → 资源对象（用于按名字取 Geometry / Brush）。</summary>
public sealed class StringToResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrEmpty(key)) return null;
        try { return Application.Current.TryFindResource(key); }
        catch { return null; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>百分比 → StrokeDashArray（用于扫描进度环）。parameter = 周长。</summary>
public sealed class PercentToDashArrayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => 0d,
        };
        pct = Math.Clamp(pct, 0, 100);

        double circumference = 238.76;
        if (parameter != null
            && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            circumference = parsed;
        }

        double len = circumference * pct / 100.0;
        // 长度至少留一点，避免 0% 时渲染异常
        return new DoubleCollection(new[] { Math.Max(len, 0.0001), circumference });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>百分比 → 宽度（用于置信度条填充）。parameter = 轨道总宽。</summary>
public sealed class PercentToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => 0d,
        };
        pct = Math.Clamp(pct, 0, 100);

        double total = 96;
        if (parameter != null
            && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            total = parsed;
        }

        return total * pct / 100.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>风险等级 → 语义前景色（徽章文字 / 图标）。</summary>
public sealed class RiskToForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskLevel risk) return Brushes.Gray;
        string key = risk switch
        {
            RiskLevel.High => "Brush.Danger.Fg",
            RiskLevel.Medium => "Brush.Warning.Fg",
            _ => "Brush.Success.Fg",
        };
        return Find(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static object Find(string key)
    {
        try { return Application.Current.TryFindResource(key) ?? Brushes.Gray; }
        catch { return Brushes.Gray; }
    }
}

/// <summary>风险等级 → 语义背景色（徽章底色）。</summary>
public sealed class RiskToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskLevel risk) return Brushes.Transparent;
        string key = risk switch
        {
            RiskLevel.High => "Brush.Danger.Bg",
            RiskLevel.Medium => "Brush.Warning.Bg",
            _ => "Brush.Success.Bg",
        };
        return Find(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static object Find(string key)
    {
        try { return Application.Current.TryFindResource(key) ?? Brushes.Transparent; }
        catch { return Brushes.Transparent; }
    }
}

/// <summary>风险等级 → 语义边框色（徽章描边）。</summary>
public sealed class RiskToBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskLevel risk) return Brushes.Transparent;
        string key = risk switch
        {
            RiskLevel.High => "Brush.Danger.Border",
            RiskLevel.Medium => "Brush.Warning.Border",
            _ => "Brush.Success.Border",
        };
        return Find(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static object Find(string key)
    {
        try { return Application.Current.TryFindResource(key) ?? Brushes.Transparent; }
        catch { return Brushes.Transparent; }
    }
}

/// <summary>风险等级 → 颜色（保留旧类名，兼容 UI 0.1 遗留页面）。</summary>
public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskLevel risk) return Brushes.Gray;
        string key = risk switch
        {
            RiskLevel.High => "Brush.Danger",
            RiskLevel.Medium => "Brush.Warning",
            _ => "Brush.Success",
        };
        try { return Application.Current.TryFindResource(key) ?? Brushes.Gray; }
        catch { return Brushes.Gray; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>风险等级 → 图标（高=AlertCircle 中=ShieldAlert 低=ShieldCheck）。</summary>
public sealed class RiskToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskLevel risk) return null;
        string key = risk switch
        {
            RiskLevel.High => "Icon.AlertCircle",
            RiskLevel.Medium => "Icon.ShieldAlert",
            _ => "Icon.ShieldCheck",
        };
        try { return Application.Current.TryFindResource(key); }
        catch { return null; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>疑似残留度等级 → 靛蓝强度色（单一色相，绝不出现绿/红）。</summary>
public sealed class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not OrphanConfidenceLevel level) return Brushes.Gray;

        string key = level switch
        {
            OrphanConfidenceLevel.VeryHighProbability => "Brush.Confidence.VHP",
            OrphanConfidenceLevel.HighProbability => "Brush.Confidence.HP",
            OrphanConfidenceLevel.Suspicious => "Brush.Confidence.Sus",
            _ => "Brush.Confidence.Norm",
        };

        try { return Application.Current.TryFindResource(key) ?? Brushes.Gray; }
        catch { return Brushes.Gray; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → Visibility（parameter=inverse 时取反）。</summary>
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

/// <summary>null → Collapsed，非 null → Visible（parameter=inverse 时取反）。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        if (parameter is string s && s.Equals("inverse", StringComparison.OrdinalIgnoreCase)) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>0 → Collapsed，非 0 → Visible（列表为空时展示 EmptyState）。</summary>
public sealed class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool empty = value switch
        {
            int i => i == 0,
            long l => l == 0,
            null => true,
            _ => false,
        };
        if (parameter is string s && s.Equals("inverse", StringComparison.OrdinalIgnoreCase)) empty = !empty;
        return empty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>空字符串 / null → Collapsed。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → 取反。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// 枚举 ↔ bool（用于分段控件的 RadioButton 双向绑定）。
/// 必须在 XAML 中指定 EnumType，例如：
/// <c>&lt;conv:EnumToBooleanConverter EnumType="{x:Type vm:RiskFilterOption}" /&gt;</c>
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    /// <summary>目标枚举类型（XAML 中用 {x:Type} 指定）。</summary>
    public Type? EnumType { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null || EnumType == null) return false;
        return value.Equals(Enum.Parse(EnumType, parameter.ToString()!));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter == null || EnumType == null) return Binding.DoNothing;
        return Enum.Parse(EnumType, parameter.ToString()!);
    }
}

/// <summary>日志级别 → 中文文本 / 语义前景色（parameter=text 或 fg）。</summary>
public sealed class LogLevelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string name = value?.ToString() ?? string.Empty;
        string part = parameter as string ?? "text";

        if (part == "text")
        {
            return name switch
            {
                "Error" => "错误",
                "Warning" => "警告",
                "Success" => "成功",
                "Debug" => "调试",
                _ => "信息",
            };
        }

        string key = name switch
        {
            "Error" => "Brush.Danger.Fg",
            "Warning" => "Brush.Warning.Fg",
            "Success" => "Brush.Success.Fg",
            _ => "Brush.Text.Secondary",
        };

        try { return Application.Current.TryFindResource(key) ?? Brushes.Gray; }
        catch { return Brushes.Gray; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 置信度等级 → 「检测到什么 / 没检测到什么」的表述。
///
/// 关键前提（Core 2026-10-16 真机数据）：47 条人工回标下 Precision 0.333、Recall 0.333、
/// AUC 0.648，置信度最高的正类与负类都是 96 分（完全重叠）。
/// 因此置信度**不是结论**，UI 只用它描述"证据强度"，绝不用它说"它是残留"。
///
/// parameter="short" 时返回列表行用的短文案。
/// </summary>
public sealed class DetectionLevelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isShort = parameter is string s && s.Equals("short", StringComparison.OrdinalIgnoreCase);

        if (isShort)
        {
            return value switch
            {
                OrphanConfidenceLevel.VeryHighProbability => "未检测到使用迹象",
                OrphanConfidenceLevel.HighProbability => "极少使用迹象",
                OrphanConfidenceLevel.Suspicious => "较少使用迹象",
                _ => "有使用迹象",
            };
        }

        // 长文案不在这里写死：直接复用 Core 的 ToText。
        // 导出报告（ReportExporter）读的也是它 —— 保证 UI 与报告用同一套措辞，不会漂移。
        return value is OrphanConfidenceLevel level
            ? OrphanConfidenceThresholds.ToText(level)
            : "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字节数 → 人类可读体积（48 MB / 1.42 GB）。</summary>
public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long bytes = value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => 0L,
        };
        return GhostCleaner.Core.Utils.FileSizeFormatter.Format(bytes);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>隔离状态 → 中文文本 + 语义色（parameter=fg / bg / border）。</summary>
public sealed class QuarantineStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not QuarantineStatus status) return "-";

        string key = status switch
        {
            QuarantineStatus.Quarantined => "Brush.Accent",
            QuarantineStatus.Restored => "Brush.Success",
            // Core 0.1 的 QuarantineStatus 只有 Quarantined / Restored / Purged / Failed。
            // Purged = 隔离内容已被永久清除（不可恢复），语义上等价于"已删除"。
            QuarantineStatus.Purged => "Brush.Danger",
            _ => "Brush.Warning",
        };

        string part = parameter as string ?? "text";
        if (part == "text")
        {
            return status switch
            {
                QuarantineStatus.Quarantined => "已隔离",
                QuarantineStatus.Restored => "已恢复",
                QuarantineStatus.Purged => "已清除",
                _ => status.ToString(),
            };
        }

        string resourceKey = part switch
        {
            "bg" => key.Replace("Brush.Accent", "Brush.Accent.Bg").Replace("Brush.Success", "Brush.Success.Bg").Replace("Brush.Danger", "Brush.Danger.Bg").Replace("Brush.Warning", "Brush.Warning.Bg"),
            "border" => key.Replace("Brush.Accent", "Brush.Accent.Border").Replace("Brush.Success", "Brush.Success.Border").Replace("Brush.Danger", "Brush.Danger.Border").Replace("Brush.Warning", "Brush.Warning.Border"),
            _ => key.Replace("Brush.Accent", "Brush.Accent.Fg").Replace("Brush.Success", "Brush.Success.Fg").Replace("Brush.Danger", "Brush.Danger.Fg").Replace("Brush.Warning", "Brush.Warning.Fg"),
        };

        try { return Application.Current.TryFindResource(resourceKey) ?? Brushes.Gray; }
        catch { return Brushes.Gray; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>完整路径 → 末级目录名（隔离区"名称"列）。</summary>
public sealed class PathToFileNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return "-";
        try
        {
            var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var name = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }
        catch
        {
            return path;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>枚举 → 与参数相等时 Visible（用于按 Stage 切换面板）。</summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        bool equal = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        return equal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

namespace WinCleaner.Core.Utils;

/// <summary>体积格式化（1024 进制，中文单位）。</summary>
public static class FileSizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 0) return "-";
        if (bytes == 0) return "0 B";

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {Units[unit]}" : $"{value:0.##} {Units[unit]}";
    }
}

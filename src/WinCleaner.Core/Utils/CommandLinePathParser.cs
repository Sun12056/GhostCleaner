using WinCleaner.Core.Models;

namespace WinCleaner.Core.Utils;

/// <summary>从命令行/注册表值中解析出可执行文件或目录路径。</summary>
public static class CommandLinePathParser
{
    /// <summary>解析出第一个可执行文件路径（去引号、去参数、展开环境变量）。解析不出返回 null。</summary>
    public static string? ParseExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;

        var cmd = commandLine.Trim().TrimStart('"');
        if (cmd.StartsWith(@"\??\", StringComparison.Ordinal)) cmd = cmd[4..];

        string? raw;

        int quoteEnd = cmd.IndexOf('"');
        if (quoteEnd > 0)
        {
            raw = cmd[..quoteEnd];
        }
        else
        {
            // 未加引号：优先在 .exe/.dll 处截断，否则按空格截断
            int idx = IndexOfExtension(cmd, ".exe");
            if (idx < 0) idx = IndexOfExtension(cmd, ".dll");
            if (idx >= 0) raw = cmd[..(idx + 4)];
            else
            {
                int space = cmd.IndexOf(' ');
                raw = space > 0 ? cmd[..space] : cmd;
            }
        }

        raw = raw.Trim();
        if (raw.Length == 0) return null;

        try
        {
            raw = Environment.ExpandEnvironmentVariables(raw);
        }
        catch
        {
            // 忽略环境变量展开失败
        }

        if (raw.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase)) return null;
        if (raw.StartsWith("rundll32", StringComparison.OrdinalIgnoreCase)) return null;

        if (!System.IO.Path.IsPathRooted(raw)) return null;
        if (!raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !raw.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return null;

        return PathUtils.Normalize(raw);
    }

    /// <summary>解析目录路径（InstallLocation 之类）。</summary>
    public static string? ParseDirectoryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var p = value.Trim().Trim('"');
        try { p = Environment.ExpandEnvironmentVariables(p); } catch { }
        if (!System.IO.Path.IsPathRooted(p)) return null;
        return PathUtils.Normalize(p);
    }

    /// <summary>DisplayIcon 形如 "C:\a\b.exe,0"。</summary>
    public static string? ParseIconPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim('"');
        int comma = v.LastIndexOf(',');
        if (comma > 0) v = v[..comma];
        return ParseExecutablePath(v) ?? (System.IO.Path.IsPathRooted(v.Trim()) ? PathUtils.Normalize(v.Trim()) : null);
    }

    private static int IndexOfExtension(string text, string ext)
        => text.IndexOf(ext, StringComparison.OrdinalIgnoreCase);
}

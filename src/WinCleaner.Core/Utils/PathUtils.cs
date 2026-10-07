namespace WinCleaner.Core.Utils;

/// <summary>路径规范化、包含关系判断与系统关键路径识别。</summary>
public static class PathUtils
{
    private static readonly char[] Separators = { '\\', '/' };

    /// <summary>系统 / 用户关键目录关键词（命中即视为高风险，默认禁止删除）。</summary>
    public static readonly string[] CriticalPathKeywords =
    {
        "windows",
        "system32",
        "syswow64",
        "winsxs",
        "programdata",
        "appdata",
        "users",
        "recovery",
        "boot",
        "efi",
        "drivers",
        "$recycle.bin",
        "system volume information",
        "perflogs",
        "onedrive",
        "documents",
        "desktop",
        "downloads",
        "pictures",
    };

    /// <summary>规范化：统一分隔符、去掉末尾分隔符（保留根目录的 D:\）、小写比较用。</summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var p = path.Trim().Trim('"').Replace('/', '\\');
        while (p.EndsWith('\\') && p.Length > 3) p = p[..^1];
        return p;
    }

    /// <summary>用于比较的键（小写、规范化）。</summary>
    public static string CompareKey(string? path) => Normalize(path).ToLowerInvariant();

    /// <summary>确保目录路径末尾带分隔符，用于前缀判断（D:\ 除外，保持原样）。</summary>
    public static string WithTrailingSeparator(string path)
    {
        var p = Normalize(path);
        if (p.EndsWith('\\')) return p;
        return p + "\\";
    }

    /// <summary>child 是否位于 parent 内部或等于 parent（大小写不敏感，按目录边界判断）。</summary>
    public static bool IsUnder(string? child, string? parent)
    {
        if (string.IsNullOrWhiteSpace(child) || string.IsNullOrWhiteSpace(parent)) return false;
        var c = Normalize(child);
        var p = Normalize(parent);
        if (c.Equals(p, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = p.EndsWith('\\') ? p : p + "\\";
        return c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>路径中是否包含给定关键词（按目录片段匹配，大小写不敏感）。</summary>
    public static bool ContainsKeyword(string? path, IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = Normalize(path).ToLowerInvariant();
        foreach (var kw in keywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            var k = kw.Trim().ToLowerInvariant();
            if (p.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>是否为系统 / 用户关键路径（高风险）。</summary>
    public static bool IsCriticalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = Normalize(path).ToLowerInvariant();
        var segments = p.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            foreach (var kw in CriticalPathKeywords)
            {
                if (seg.Equals(kw, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    /// <summary>命中系统关键路径时返回命中的关键词，否则 null。</summary>
    public static string? GetCriticalKeyword(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var segments = Normalize(path).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            foreach (var kw in CriticalPathKeywords)
            {
                if (seg.Equals(kw, StringComparison.OrdinalIgnoreCase)) return kw;
            }
        }
        return null;
    }

    /// <summary>取盘符根，例如 D:\Games\Foo -> D:\；UNC 路径返回原样。</summary>
    public static string? GetDriveRoot(string? path)
    {
        var p = Normalize(path);
        if (p.Length >= 2 && p[1] == ':') return p[..2] + "\\";
        return null;
    }

    /// <summary>是否为盘符根目录。</summary>
    public static bool IsDriveRoot(string? path)
    {
        var p = Normalize(path);
        return p.Length == 2 && p[1] == ':' || p.Length == 3 && p[1] == ':' && p[2] == '\\';
    }

    /// <summary>把路径转成扩展长度路径（支持 >260 字符）。</summary>
    public static string ToExtendedPath(string path)
    {
        var full = System.IO.Path.GetFullPath(Normalize(path));
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) return full;
        if (full.StartsWith("\\\\", StringComparison.Ordinal)) return "\\\\?\\UNC\\" + full[2..];
        return @"\\?\" + full;
    }

    /// <summary>把路径中非法文件名字符替换为下划线，用于隔离区目录命名。</summary>
    public static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }
}

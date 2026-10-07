using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>基于 <see cref="ScanOptions"/> 的用户白名单（排除路径 + 保留关键词）。</summary>
public sealed class OptionsExclusionProvider : IPathExclusionProvider
{
    private readonly ScanOptions _options;

    public OptionsExclusionProvider(ScanOptions options) => _options = options;

    public bool IsExcluded(string path, out string? reason)
    {
        var excludedPath = _options.ExcludedPaths.FirstOrDefault(p => PathUtils.IsUnder(path, p));
        if (excludedPath != null)
        {
            reason = "命中排除路径：" + excludedPath;
            return true;
        }

        var keyword = _options.KeepKeywords
            .FirstOrDefault(k => !string.IsNullOrWhiteSpace(k) && PathUtils.ContainsKeyword(path, new[] { k }));
        if (keyword != null)
        {
            reason = "命中保留关键词：" + keyword;
            return true;
        }

        reason = null;
        return false;
    }
}

/// <summary>
/// 内置白名单：知名厂商 / 游戏平台 / 开发工具链。
/// 设计为可扩展 —— 后续可拆成 DefaultWhitelist / UserWhitelist / SystemProtectedPaths。
/// 这里只放"误删代价明显大于收益"的少量规则，不做大量硬编码。
/// </summary>
public sealed class DefaultWhitelistProvider : IPathExclusionProvider
{
    public static readonly string[] DefaultNames =
    {
        "steam", "steamlibrary", "steamapps",
        "epic games", "epicgames", "ubisoft", "uplay", "origin", "gog", "gog galaxy",
        "microsoft", "microsoft visual studio", "visual studio", "jetbrains",
        "intellij", "pycharm", "webstorm", "android studio",
        "python", "anaconda", "miniconda", "conda",
        "nodejs", "git", "jenkins", "docker", "maven", "gradle",
        "unity", "unreal engine", "unrealengine",
    };

    private readonly IReadOnlyList<string> _names;

    public DefaultWhitelistProvider(IEnumerable<string>? extraNames = null)
    {
        var list = DefaultNames.ToList();
        if (extraNames != null) list.AddRange(extraNames.Where(n => !string.IsNullOrWhiteSpace(n)));
        _names = list.Select(n => n.Trim().ToLowerInvariant()).Distinct().ToArray();
    }

    /// <summary>
    /// 按目录片段<b>精确</b>匹配。
    /// 不做前缀匹配 —— 否则 "git" 会命中 "github"、把整个代码仓库目录都排除掉。
    /// 需要覆盖的变体（steamlibrary / epicgames / xboxgames …）已在名单中显式列出。
    /// </summary>
    public bool IsExcluded(string path, out string? reason)
    {
        var segments = Heuristics.PathSegments(path);

        foreach (var name in _names)
        {
            if (!segments.Contains(name, StringComparer.Ordinal)) continue;

            reason = $"命中内置白名单（{name}）：{path}";
            return true;
        }

        reason = null;
        return false;
    }
}

/// <summary>系统关键路径保护：无论置信度多高，都不允许绕过。</summary>
public sealed class SystemProtectedPathProvider : IPathExclusionProvider
{
    public bool IsExcluded(string path, out string? reason)
    {
        var keyword = PathUtils.GetCriticalKeyword(path);
        if (keyword != null)
        {
            reason = "系统/用户关键目录（" + keyword + "），默认不参与删除";
            return true;
        }

        reason = null;
        return false;
    }
}

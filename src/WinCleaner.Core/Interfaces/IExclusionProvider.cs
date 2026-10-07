namespace WinCleaner.Core.Interfaces;

/// <summary>
/// 路径排除（白名单）规则。设计为可扩展：
/// 内置规则 / 用户白名单 / 系统保护路径各自实现一份，再由组合器串起来。
/// </summary>
public interface IPathExclusionProvider
{
    /// <summary>判断路径是否被排除。</summary>
    /// <param name="path">待判断的目录路径。</param>
    /// <param name="reason">被排除时给出人类可读原因。</param>
    bool IsExcluded(string path, out string? reason);
}

/// <summary>把多个排除规则按顺序组合：任一命中即排除。</summary>
public sealed class CompositeExclusionProvider : IPathExclusionProvider
{
    private readonly IReadOnlyList<IPathExclusionProvider> _providers;

    public CompositeExclusionProvider(params IPathExclusionProvider[] providers)
        => _providers = providers ?? Array.Empty<IPathExclusionProvider>();

    public IReadOnlyList<IPathExclusionProvider> Providers => _providers;

    public bool IsExcluded(string path, out string? reason)
    {
        foreach (var provider in _providers)
        {
            if (provider.IsExcluded(path, out reason)) return true;
        }

        reason = null;
        return false;
    }
}

using System.Diagnostics;

namespace WinCleaner.Core.Utils;

/// <summary>读取 PE 文件版本资源（厂商 / 产品名 / 版本）。</summary>
public static class VersionInfoReader
{
    public static VersionResource? Read(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var vi = FileVersionInfo.GetVersionInfo(filePath);
            if (vi == null) return null;

            var version = !string.IsNullOrWhiteSpace(vi.ProductVersion)
                ? vi.ProductVersion.Trim()
                : vi.FileVersion?.Trim();

            return new VersionResource
            {
                Version = string.IsNullOrWhiteSpace(version) ? null : version,
                Publisher = string.IsNullOrWhiteSpace(vi.CompanyName) ? null : vi.CompanyName.Trim(),
                ProductName = string.IsNullOrWhiteSpace(vi.ProductName) ? null : vi.ProductName.Trim(),
                FileDescription = string.IsNullOrWhiteSpace(vi.FileDescription) ? null : vi.FileDescription.Trim(),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>依次尝试多个文件，返回第一个非空的完整信息。</summary>
    public static VersionResource? ReadAny(IEnumerable<string> filePaths)
    {
        VersionResource? best = null;
        foreach (var p in filePaths)
        {
            var r = Read(p);
            if (r == null) continue;
            if (best == null) best = r;
            if (!string.IsNullOrEmpty(best.Version) && !string.IsNullOrEmpty(best.Publisher) && !string.IsNullOrEmpty(best.ProductName))
                return best;
            if (!string.IsNullOrEmpty(r.Version)) best.Version ??= r.Version;
            if (!string.IsNullOrEmpty(r.Publisher)) best.Publisher ??= r.Publisher;
            if (!string.IsNullOrEmpty(r.ProductName)) best.ProductName ??= r.ProductName;
        }
        return best;
    }
}

public sealed class VersionResource
{
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? ProductName { get; set; }
    public string? FileDescription { get; set; }

    public bool HasAny => !string.IsNullOrEmpty(Version) || !string.IsNullOrEmpty(Publisher) || !string.IsNullOrEmpty(ProductName);
}

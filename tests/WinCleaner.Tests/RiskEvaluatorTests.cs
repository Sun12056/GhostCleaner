using WinCleaner.Core.Models;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

public class RiskEvaluatorTests
{
    private static SoftwareDirectoryInfo CreateInfo(string path) => new()
    {
        Path = path,
        Name = System.IO.Path.GetFileName(path),
        ProductName = "TestApp",
        Publisher = "TestVendor",
        Version = "1.2.3",
        ExecutableCount = 2,
        LibraryCount = 4,
        FileCount = 120,
        TotalSizeBytes = 80L * 1024 * 1024,
        HasUninstaller = true,
        UninstallerPath = System.IO.Path.Combine(path, "unins000.exe"),
        LastModified = DateTime.Now.AddDays(-400),
        IsLikelySoftwareDirectory = true,
    };

    private static OrphanItem Evaluate(
        string path,
        bool hasUserData = false,
        DateTime? lastModified = null,
        int indexCount = 100,
        ScanOptions? options = null,
        IReadOnlyList<AssociationHit>? hits = null)
    {
        var info = CreateInfo(path);
        info.HasUserData = hasUserData;
        if (lastModified.HasValue) info.LastModified = lastModified.Value;

        return RiskEvaluator.Evaluate(info, hits ?? Array.Empty<AssociationHit>(), options ?? new ScanOptions(), indexCount);
    }

    [Fact]
    public void CleanOldSoftwareOnNonSystemDrive_IsLowRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp");
        Assert.Equal(RiskLevel.Low, item.Risk);
        Assert.Contains(item.Reasons, r => r.Contains("无任何系统引用"));
    }

    [Fact]
    public void DirectoryWithUserData_IsMediumRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp", hasUserData: true);
        Assert.Equal(RiskLevel.Medium, item.Risk);
        Assert.Contains(item.Reasons, r => r.Contains("用户数据"));
    }

    [Theory]
    [InlineData(@"C:\ProgramData\OldApp")]
    [InlineData(@"D:\Recovery\OldApp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\OldApp")]
    public void SystemOrUserCriticalPath_IsHighRiskAndExcluded(string path)
    {
        var item = Evaluate(path);
        Assert.Equal(RiskLevel.High, item.Risk);
        Assert.True(item.IsExcluded, "系统关键目录必须被排除出删除范围");
    }

    [Fact]
    public void EmptyAssociationIndex_MakesResultHighRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp", indexCount: 0);
        Assert.Equal(RiskLevel.High, item.Risk);
        Assert.Contains(item.Reasons, r => r.Contains("关联索引为空"));
    }

    [Fact]
    public void RecentlyModifiedDirectory_IsAtLeastMediumRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp", lastModified: DateTime.Now.AddDays(-10));
        Assert.NotEqual(RiskLevel.Low, item.Risk);
    }

    [Fact]
    public void WhitelistedPath_IsExcluded()
    {
        var options = new ScanOptions { ExcludedPaths = new[] { @"D:\Games\KeepMe" } };
        var item = Evaluate(@"D:\Games\KeepMe", options: options);
        Assert.True(item.IsExcluded);
        Assert.NotNull(item.ExcludeReason);
        Assert.Contains("排除路径", item.ExcludeReason!);
    }

    [Fact]
    public void KeepKeyword_IsExcluded()
    {
        var options = new ScanOptions { KeepKeywords = new[] { "SteamLibrary" } };
        var item = Evaluate(@"D:\SteamLibrary\common\Game", options: options);
        Assert.True(item.IsExcluded);
        Assert.Contains("SteamLibrary", item.ExcludeReason!);
    }

    [Fact]
    public void ExistingAssociation_ProducesReasonInsteadOfOrphanClaim()
    {
        var hits = new List<AssociationHit>
        {
            new() { Source = AssociationSource.WindowsService, Name = "TestService", TargetPath = @"D:\Apps\Tool\tool.exe" },
        };

        var item = Evaluate(@"D:\Apps\Tool", hits: hits);

        Assert.Contains(item.Reasons, r => r.Contains("仍被"));
        Assert.DoesNotContain(item.Reasons, r => r.Contains("无任何系统引用"));
    }
}

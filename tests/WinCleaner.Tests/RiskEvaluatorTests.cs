using WinCleaner.Core.Models;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>
/// 评估引擎测试。
/// 注意：v0.1 起 Risk 不再等于 Orphan Confidence —— 两者分别断言。
/// </summary>
public class RiskEvaluatorTests
{
    private static OrphanItem Evaluate(
        string path,
        SoftwareDirectoryInfo? info = null,
        IReadOnlyList<AssociationHit>? hits = null,
        ScanOptions? options = null,
        IReadOnlyList<AssociationSourceHealth>? health = null)
        => RiskEvaluator.Evaluate(
            info ?? TestInfo.Create(path),
            hits ?? Array.Empty<AssociationHit>(),
            options ?? new ScanOptions(),
            health ?? TestHealth.AllSuccess());

    [Fact]
    public void CleanOldSoftwareOnNonSystemDrive_IsLowRiskButHighConfidence()
    {
        var item = Evaluate(@"D:\Games\OldApp");

        Assert.Equal(RiskLevel.Low, item.Risk);
        Assert.True(item.ConfidenceScore >= OrphanConfidenceThresholds.HighProbability,
            "真正的软件残留应获得较高置信度");
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.MissingUninstallEntry);
    }

    [Fact]
    public void DirectoryWithUserData_IsMediumRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp",
            TestInfo.Create(@"D:\Games\OldApp", hasUserData: true, topLevelDirectories: new[] { "config" }));

        Assert.Equal(RiskLevel.Medium, item.Risk);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.UserData && !e.IsPositive);
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
        Assert.True(item.IsProtected);
        Assert.Equal(CleanupRecommendation.Keep, item.Recommendation);
    }

    [Fact]
    public void EmptyAssociationIndex_MakesResultHighRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp", health: TestHealth.AllFailed());

        Assert.Equal(RiskLevel.High, item.Risk);
        Assert.Contains(item.Warnings, w => w.Contains("扫描结果可能不完整"));
        Assert.DoesNotContain(item.Evidence, e => e.Type == EvidenceType.MissingUninstallEntry);
    }

    [Fact]
    public void FailedSources_NeverProduceMissingAssociationEvidence()
    {
        var item = Evaluate(@"D:\Games\OldApp", health: TestHealth.OnlyFail(AssociationSource.RegistryUninstall));

        // 注册表来源失败：不能出现"未发现卸载注册表项"这种结论
        Assert.DoesNotContain(item.Evidence, e => e.Type == EvidenceType.MissingUninstallEntry);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.AssociationSourceFailed);
        Assert.Contains(item.Warnings, w => w.Contains("注册表卸载项检查未完成"));
        Assert.True(item.ConfidenceScore <= EvidenceWeights.Default.MaxConfidenceWhenRegistryUnavailable,
            "注册表不可用时置信度必须被压低");
    }

    [Fact]
    public void RecentlyModifiedDirectory_IsAtLeastMediumRisk()
    {
        var item = Evaluate(@"D:\Games\OldApp",
            TestInfo.Create(@"D:\Games\OldApp", lastModified: DateTime.Now.AddDays(-3)));

        Assert.NotEqual(RiskLevel.Low, item.Risk);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.RecentModification && !e.IsPositive);
    }

    [Fact]
    public void WhitelistedPath_IsExcluded()
    {
        var options = new ScanOptions { ExcludedPaths = new[] { @"D:\Games\KeepMe" } };
        var item = Evaluate(@"D:\Games\KeepMe", options: options);

        Assert.True(item.IsExcluded);
        Assert.NotNull(item.ExcludeReason);
        Assert.Contains("排除路径", item.ExcludeReason!);
        Assert.Equal(CleanupRecommendation.Keep, item.Recommendation);
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

        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.Service && !e.IsPositive);
        Assert.True(item.IsRunning == false);
        Assert.DoesNotContain(item.Evidence, e => e.Type == EvidenceType.MissingService);
    }

    [Fact]
    public void RunningProcess_StronglyLowersConfidence()
    {
        var hits = new List<AssociationHit>
        {
            new() { Source = AssociationSource.RunningProcess, Name = "tool", TargetPath = @"D:\Apps\Tool\tool.exe" },
        };

        var withProcess = Evaluate(@"D:\Apps\Tool", hits: hits);
        var without = Evaluate(@"D:\Apps\Tool");

        Assert.True(withProcess.ConfidenceScore < without.ConfidenceScore - 50,
            "正在运行的软件置信度必须大幅下降");
        Assert.Contains(withProcess.Evidence, e => e.Type == EvidenceType.RunningProcess && !e.IsPositive);
    }

    [Fact]
    public void Recommendation_IsNeverDelete()
    {
        foreach (var recommendation in Enum.GetValues<CleanupRecommendation>())
        {
            Assert.NotEqual("Delete", recommendation.ToString());
            Assert.DoesNotContain("删除", CleanupRecommendationText.ToText(recommendation));
        }
    }
}

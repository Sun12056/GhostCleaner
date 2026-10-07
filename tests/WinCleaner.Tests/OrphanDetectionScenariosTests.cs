using WinCleaner.Core.Models;
using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

/// <summary>
/// v0.1 验收场景测试：
/// 真正的残留要能被发现；绿色软件 / 游戏 / 开发项目 / 用户数据不能轻易被误判；
/// 扫描失败不能伪装成"没有引用"。
/// </summary>
public class OrphanDetectionScenariosTests
{
    private static readonly IReadOnlyList<AssociationSourceHealth> Healthy = TestHealth.AllSuccess();

    private static OrphanItem Assess(SoftwareDirectoryInfo info, IReadOnlyList<AssociationHit>? hits = null, IReadOnlyList<AssociationSourceHealth>? health = null)
        => RiskEvaluator.Evaluate(info, hits ?? Array.Empty<AssociationHit>(), new ScanOptions(), health ?? Healthy);

    // ---------------- Case 1：真正的软件残留 ----------------

    [Fact]
    public void Case1_RealOrphanSoftware_HighConfidence()
    {
        var info = TestInfo.Create(
            @"D:\OldSoftware\AdobeReader",
            executables: 3,
            libraries: 12,
            hasUninstaller: true,
            fileCount: 420,
            sizeBytes: 240L * 1024 * 1024,
            lastModified: DateTime.Now.AddDays(-900));

        var item = Assess(info);

        Assert.True(item.ConfidenceScore >= OrphanConfidenceThresholds.HighProbability,
            $"真正的软件残留应达到高概率，实际 {item.ConfidenceScore}");
        Assert.True(item.ConfidenceLevel is OrphanConfidenceLevel.HighProbability or OrphanConfidenceLevel.VeryHighProbability);

        // 关键正向证据都在
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.Uninstaller);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.MissingUninstallEntry);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.LongUnmodified);

        // 没有保护性证据
        Assert.False(item.IsProtected);
        Assert.Equal(RiskLevel.Low, item.Risk);
    }

    // ---------------- Case 2：Portable 软件 ----------------

    [Fact]
    public void Case2_PortableSoftware_NotFlaggedAsOrphan()
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\Portable\Tool",
            Name = "Tool",
            ExecutableCount = 1,
            LibraryCount = 0,
            FileCount = 3,
            TotalSizeBytes = 5L * 1024 * 1024,
            HasUninstaller = false,
            LastModified = DateTime.Now.AddDays(-2),
            IsLikelySoftwareDirectory = false,
        };
        info.TopLevelFileNames.Add("Tool.exe");
        info.TopLevelFileNames.Add("config.json");
        info.TopLevelDirectoryNames.Add("data");
        info.AllDirectoryNames.Add("data");
        info.TopLevelDirectoryNames.Add("config");
        info.AllDirectoryNames.Add("config");

        var item = Assess(info);

        Assert.Equal(OrphanConfidenceLevel.Normal, item.ConfidenceLevel);
        Assert.True(item.ConfidenceScore < OrphanConfidenceThresholds.Suspicious,
            $"绿色软件不应被判为可疑残留，实际 {item.ConfidenceScore}");
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.PortableSoftware && !e.IsPositive);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.RecentModification && !e.IsPositive);
        Assert.NotEqual(CleanupRecommendation.Quarantine, item.Recommendation);
        Assert.Equal("绿色软件", item.DirectoryCategory);
    }

    // ---------------- Case 3：游戏目录 ----------------

    [Fact]
    public void Case3_SteamGameDirectory_KeepOrReviewOnly()
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\SteamLibrary\steamapps\common\Game",
            Name = "Game",
            ExecutableCount = 1,
            LibraryCount = 6,
            FileCount = 800,
            TotalSizeBytes = 8L * 1024 * 1024 * 1024,
            HasUninstaller = false,
            LastModified = DateTime.Now.AddDays(-400),
            IsLikelySoftwareDirectory = true,
        };
        info.FileNameSamples.Add("steam_api64.dll");
        info.AllDirectoryNames.Add("bin");

        var item = Assess(info);

        Assert.Equal("游戏", item.DirectoryCategory);
        Assert.True(item.IsProtected, "游戏目录必须受到保护");
        Assert.Equal(CleanupRecommendation.Keep, item.Recommendation);
        Assert.True(item.IsExcluded, "SteamLibrary 应命中内置白名单");
        Assert.Contains("白名单", item.ExcludeReason!);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.GameDirectory && !e.IsPositive);
    }

    [Fact]
    public void Case3b_GameDirectoryOutsideWhitelist_StillProtected()
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\MyGames\SomeGame",
            Name = "SomeGame",
            ExecutableCount = 1,
            LibraryCount = 4,
            FileCount = 300,
            TotalSizeBytes = 4L * 1024 * 1024 * 1024,
            HasUninstaller = false,
            LastModified = DateTime.Now.AddDays(-400),
            IsLikelySoftwareDirectory = true,
        };
        info.FileNameSamples.Add("steam_api64.dll");
        info.FileNameSamples.Add("UnityPlayer.dll");
        info.AllDirectoryNames.Add("MonoBleedingEdge");

        var item = Assess(info);

        Assert.Equal("游戏", item.DirectoryCategory);
        Assert.True(item.IsProtected);
        Assert.True(item.ConfidenceScore < OrphanConfidenceThresholds.HighProbability);
        Assert.DoesNotContain(CleanupRecommendation.Quarantine, new[] { item.Recommendation });
    }

    // ---------------- Case 4：开发项目 ----------------

    [Fact]
    public void Case4_DevelopmentProject_Protected()
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\Projects\TestProject",
            Name = "TestProject",
            ExecutableCount = 1,
            LibraryCount = 0,
            FileCount = 1200,
            TotalSizeBytes = 320L * 1024 * 1024,
            HasUninstaller = false,
            LastModified = DateTime.Now.AddDays(-300),
            IsLikelySoftwareDirectory = true,
        };
        info.TopLevelDirectoryNames.Add(".git");
        info.TopLevelDirectoryNames.Add("src");
        info.TopLevelDirectoryNames.Add("node_modules");
        info.AllDirectoryNames.Add(".git");
        info.AllDirectoryNames.Add("src");
        info.AllDirectoryNames.Add("node_modules");
        info.TopLevelFileNames.Add("package.json");
        info.FileNameSamples.Add("package.json");

        var item = Assess(info);

        Assert.Equal("开发项目", item.DirectoryCategory);
        Assert.True(item.IsProtected, "开发项目必须受到保护");
        Assert.True(item.ConfidenceScore < OrphanConfidenceThresholds.HighProbability);
        Assert.Equal(CleanupRecommendation.Review, item.Recommendation);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.DevelopmentProject && !e.IsPositive);
        Assert.Contains(item.Warnings, w => w.Contains("开发项目"));
    }

    // ---------------- Case 5：用户数据 ----------------

    [Fact]
    public void Case5_DirectoryWithSaveData_NeverRecommendedForCleanup()
    {
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\OldApp",
            Name = "OldApp",
            ExecutableCount = 2,
            LibraryCount = 5,
            FileCount = 150,
            TotalSizeBytes = 500L * 1024 * 1024,
            HasUninstaller = true,
            LastModified = DateTime.Now.AddDays(-500),
            IsLikelySoftwareDirectory = true,
            HasUserData = true,
        };
        foreach (var dir in new[] { "save", "profile", "userdata" })
        {
            info.TopLevelDirectoryNames.Add(dir);
            info.AllDirectoryNames.Add(dir);
        }

        var item = Assess(info);

        Assert.True(item.ConfidenceScore < OrphanConfidenceThresholds.HighProbability,
            $"含用户存档的目录置信度必须明显降低，实际 {item.ConfidenceScore}");
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.SaveData && !e.IsPositive);
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.ProfileData && !e.IsPositive);
        Assert.True(item.IsProtected);
        Assert.NotEqual(CleanupRecommendation.Quarantine, item.Recommendation);
    }

    // ---------------- Case 6：扫描器失败 ----------------

    [Fact]
    public void Case6_RegistryCollectorFailed_NeverClaimsNoRegistryReference()
    {
        var info = TestInfo.Create(@"D:\OldSoftware\AdobeReader", lastModified: DateTime.Now.AddDays(-900));
        var health = TestHealth.OnlyFail(AssociationSource.RegistryUninstall);

        var item = Assess(info, health: health);

        // 不能出现"未发现卸载注册表项"这种结论
        Assert.DoesNotContain(item.Evidence, e => e.Type == EvidenceType.MissingUninstallEntry);

        // 必须有明确的失败证据与警告
        Assert.Contains(item.Evidence, e => e.Type == EvidenceType.AssociationSourceFailed);
        Assert.Contains(item.Warnings, w => w.Contains("注册表卸载项检查未完成"));
        Assert.Contains(item.Warnings, w => w.Contains("不能据此认为该目录没有引用"));

        // 并且置信度必须被压低
        Assert.True(item.ConfidenceScore <= EvidenceWeights.Default.MaxConfidenceWhenRegistryUnavailable);

        // UI 文案中不能出现"确定没有引用"这类绝对表述
        Assert.DoesNotContain(item.Reasons, r => r.Contains("未发现对应的卸载注册表项"));
    }

    [Fact]
    public void Case6b_ManySourcesFailed_StrictlyCapsConfidence()
    {
        var info = TestInfo.Create(@"D:\OldSoftware\AdobeReader", lastModified: DateTime.Now.AddDays(-900));
        var health = TestHealth.OnlyFail(
            AssociationSource.RegistryUninstall,
            AssociationSource.AppPaths,
            AssociationSource.ScheduledTask);

        var item = Assess(info, health: health);

        Assert.True(item.ConfidenceScore <= EvidenceWeights.Default.MaxConfidenceWhenManySourcesFailed);
        Assert.Contains(item.Warnings, w => w.Contains("扫描结果可能不完整"));
    }

    // ---------------- Case 7：正在运行的软件 ----------------

    [Fact]
    public void Case7_RunningSoftware_ConfidenceCollapses()
    {
        var info = TestInfo.Create(@"D:\Apps\Tool", lastModified: DateTime.Now.AddDays(-900));

        var idle = Assess(info);
        var running = Assess(info, hits: new List<AssociationHit>
        {
            new() { Source = AssociationSource.RunningProcess, Name = "Tool", TargetPath = @"D:\Apps\Tool\Tool.exe" },
        });

        Assert.True(idle.ConfidenceScore >= OrphanConfidenceThresholds.HighProbability);
        Assert.True(running.ConfidenceScore < OrphanConfidenceThresholds.Suspicious,
            $"正在运行的软件置信度必须明显降低，实际 {running.ConfidenceScore}");
        Assert.True(running.IsRunning);
        Assert.Equal(CleanupRecommendation.HighRiskReview, running.Recommendation);
    }

    // ---------------- Case 8：最近修改 ----------------

    [Fact]
    public void Case8_RecentlyModified_ConfidenceLowered()
    {
        var old = Assess(TestInfo.Create(@"D:\Apps\Tool", lastModified: DateTime.Now.AddDays(-900)));
        var recent = Assess(TestInfo.Create(@"D:\Apps\Tool", lastModified: DateTime.Now.AddDays(-3)));

        Assert.True(recent.ConfidenceScore < old.ConfidenceScore);
        Assert.Contains(recent.Evidence, e => e.Type == EvidenceType.RecentModification && !e.IsPositive);
        Assert.NotEqual(CleanupRecommendation.Quarantine, recent.Recommendation);
    }

    // ---------------- 通用保证 ----------------

    [Fact]
    public void MissingAssociation_IsNeverTheWholeVerdict()
    {
        // 一个完全没有"安装痕迹"的目录（没有卸载器/版本信息/大量文件），
        // 即使所有来源都确认没有引用，也不应得到高置信度
        var info = new SoftwareDirectoryInfo
        {
            Path = @"D:\Temp\SomeFolder",
            Name = "SomeFolder",
            ExecutableCount = 1,
            FileCount = 2,
            TotalSizeBytes = 2L * 1024 * 1024,
            HasUninstaller = false,
            LastModified = DateTime.Now.AddDays(-900),
            IsLikelySoftwareDirectory = true,
        };

        var item = Assess(info);

        Assert.True(item.ConfidenceScore < OrphanConfidenceThresholds.HighProbability,
            "“没有系统关联”本身不能构成高置信度结论");
        Assert.NotEmpty(item.Evidence.Where(e => e.Type == EvidenceType.MissingUninstallEntry));
    }

    [Fact]
    public void EveryResult_IsExplainable()
    {
        var item = Assess(TestInfo.Create(@"D:\OldSoftware\AdobeReader"));

        Assert.NotEmpty(item.Evidence);
        Assert.All(item.Evidence, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Title));
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
            Assert.Equal(e.Score > 0, e.IsPositive);
        });
        Assert.NotEmpty(item.Reasons);
    }
}

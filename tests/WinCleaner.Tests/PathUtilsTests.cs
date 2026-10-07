using WinCleaner.Core.Utils;
using Xunit;

namespace WinCleaner.Tests;

public class PathUtilsTests
{
    [Theory]
    [InlineData(@"D:\Games\Foo\", @"D:\Games\Foo")]
    [InlineData("D:/Games/Foo", @"D:\Games\Foo")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData(@"  ""D:\Apps\Bar""  ", @"D:\Apps\Bar")]
    public void Normalize_WorksAsExpected(string input, string expected)
        => Assert.Equal(expected, PathUtils.Normalize(input));

    [Theory]
    [InlineData(@"D:\Games\Foo\bin", @"D:\Games\Foo", true)]
    [InlineData(@"D:\Games\Foo", @"D:\Games\Foo", true)]
    [InlineData(@"d:\games\foo", @"D:\Games\Foo", true)]
    [InlineData(@"D:\Games\FooBar", @"D:\Games\Foo", false)]
    [InlineData(@"D:\Games\Foo", @"D:\Games\Foo\bin", false)]
    public void IsUnder_ComparesByDirectoryBoundary(string child, string parent, bool expected)
        => Assert.Equal(expected, PathUtils.IsUnder(child, parent));

    [Fact]
    public void ContainsKeyword_MatchesCaseInsensitive()
    {
        Assert.True(PathUtils.ContainsKeyword(@"D:\SteamLibrary\common\Game", new[] { "SteamLibrary" }));
        Assert.True(PathUtils.ContainsKeyword(@"D:\工作\项目A", new[] { "项目" }));
        Assert.False(PathUtils.ContainsKeyword(@"D:\Games\Foo", new[] { "SteamLibrary" }));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32", true)]
    [InlineData(@"C:\ProgramData\Foo", true)]
    [InlineData(@"C:\Users\Alice\AppData\Local\Foo", true)]
    [InlineData(@"D:\Recovery\Foo", true)]
    [InlineData(@"D:\Games\OldApp", false)]
    public void IsCriticalPath_DetectsSystemAndUserFolders(string path, bool expected)
        => Assert.Equal(expected, PathUtils.IsCriticalPath(path));

    [Fact]
    public void GetCriticalKeyword_ReturnsMatchedKeyword()
        => Assert.Equal("windows", PathUtils.GetCriticalKeyword(@"C:\Windows\Temp\Foo"));

    [Fact]
    public void GetDriveRoot_And_IsDriveRoot()
    {
        Assert.Equal(@"D:\", PathUtils.GetDriveRoot(@"D:\Games\Foo"));
        Assert.True(PathUtils.IsDriveRoot("D:\\"));
        Assert.False(PathUtils.IsDriveRoot(@"D:\Games"));
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidChars()
    {
        var result = PathUtils.SanitizeFileName("a:b/c*?");
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("/", result);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}

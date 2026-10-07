using WinCleaner.Core.Utils;
using Xunit;

namespace WinCleaner.Tests;

public class CommandLinePathParserTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\Foo\uninstall.exe"" /S", @"C:\Program Files\Foo\uninstall.exe")]
    [InlineData(@"D:\Apps\Bar\app.exe", @"D:\Apps\Bar\app.exe")]
    [InlineData(@"D:\Apps\Bar\app.exe --silent", @"D:\Apps\Bar\app.exe")]
    public void ParseExecutablePath_ExtractsPath(string input, string expected)
        => Assert.Equal(expected, CommandLinePathParser.ParseExecutablePath(input));

    [Fact]
    public void ParseExecutablePath_IgnoresMsiExecAndRundll()
    {
        Assert.Null(CommandLinePathParser.ParseExecutablePath("MsiExec.exe /X{1234-5678}"));
        Assert.Null(CommandLinePathParser.ParseExecutablePath(@"rundll32.exe C:\a\b.dll,Entry"));
    }

    [Fact]
    public void ParseIconPath_StripsIndex()
        => Assert.Equal(@"D:\Apps\Bar\app.exe",
            CommandLinePathParser.ParseIconPath(@"""D:\Apps\Bar\app.exe"",0"));

    [Fact]
    public void ParseDirectoryPath_ExpandsEnvironmentVariables()
    {
        var result = CommandLinePathParser.ParseDirectoryPath(@"%SystemDrive%\Games\Foo");
        Assert.NotNull(result);
        Assert.EndsWith(@"Games\Foo", result!);
    }

    [Fact]
    public void ParseExecutablePath_ReturnsNullForRelativeOrEmpty()
    {
        Assert.Null(CommandLinePathParser.ParseExecutablePath("app.exe"));
        Assert.Null(CommandLinePathParser.ParseExecutablePath(null));
        Assert.Null(CommandLinePathParser.ParseExecutablePath("   "));
    }
}

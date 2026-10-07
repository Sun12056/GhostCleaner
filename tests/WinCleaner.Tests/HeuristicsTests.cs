using WinCleaner.Scanner;
using Xunit;

namespace WinCleaner.Tests;

public class HeuristicsTests
{
    [Theory]
    [InlineData("unins000.exe", true)]
    [InlineData("uninstall.exe", true)]
    [InlineData("uninst.exe", true)]
    [InlineData("Unwise.exe", true)]
    [InlineData("setup.exe", false)]
    [InlineData("game.exe", false)]
    public void IsUninstaller_DetectsUninstallers(string fileName, bool expected)
        => Assert.Equal(expected, Heuristics.IsUninstaller(fileName));

    [Theory]
    [InlineData("bin", true)]
    [InlineData("resources", true)]
    [InlineData("locales", true)]
    [InlineData("save", false)]
    public void IsResourceDirectory(string name, bool expected)
        => Assert.Equal(expected, Heuristics.IsResourceDirectory(name));

    [Theory]
    [InlineData("config", true)]
    [InlineData("saves", true)]
    [InlineData("项目", true)]
    [InlineData("bin", false)]
    public void IsUserDataDirectory(string name, bool expected)
        => Assert.Equal(expected, Heuristics.IsUserDataDirectory(name));

    [Theory]
    [InlineData(".sqlite", true)]
    [InlineData(".sav", true)]
    [InlineData(".exe", false)]
    public void IsUserDataFile(string extension, bool expected)
        => Assert.Equal(expected, Heuristics.IsUserDataFile(extension));

    [Theory]
    [InlineData("Program Files", true)]
    [InlineData("Games", true)]
    [InlineData("GreenSoft", true)]
    [InlineData("OldApp", false)]
    public void IsContainerDirectory(string name, bool expected)
        => Assert.Equal(expected, Heuristics.IsContainerDirectory(name));

    [Theory]
    [InlineData(".mkv", true)]
    [InlineData(".mp3", true)]
    [InlineData(".dll", false)]
    public void IsMediaFile(string extension, bool expected)
        => Assert.Equal(expected, Heuristics.IsMediaFile(extension));
}

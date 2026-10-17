using GhostCleaner.Core.Models;
using GhostCleaner.Scanner;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// 「开发项目」判定的契约测试。
///
/// <para>背景：原判定是"命中 1 个特征即判开发项目"，且判据纳入了<b>递归子目录</b>与
/// sdk / vendor / obj 等<b>弱特征</b>，实测 48 条里 <b>23 条（48%）</b> 被误判，
/// 其中含 bilibili、桌面、文档、下载（见 <c>docs/core-0.1/18</c>）。</para>
///
/// <para>修复后：<b>分类</b>只认顶层的强特征，<b>保护</b>仍用宽松判据 ——
/// 修分类的 bug 绝不顺带削弱安全网。</para>
/// </summary>
public class DirectoryFactsTests
{
    /// <summary>
    /// 只有弱特征（sdk / vendor / obj，普通软件里同样常见）⇒ 不得判为开发项目。
    /// </summary>
    [Fact]
    public void WeakDevIndicatorsOnly_NotDevelopmentProject()
    {
        // params 在可选参数之后，必须走命名数组，否则字符串会被绑定到 int executables
        var info = TestInfo.Create(@"D:\SomeApp", topLevelDirectories: new[] { "sdk", "vendor", "obj" });

        var facts = DirectoryFactsDetector.Detect(info);

        Assert.False(facts.IsDevelopmentProject);
    }

    /// <summary>
    /// 弱特征<b>仍应触发保护</b> —— 修分类不得削弱安全网。
    /// 这条是本轮修复的硬约束：绝不因为修 bug 让更多目录变得可删除。
    /// </summary>
    [Fact]
    public void WeakDevIndicatorsOnly_StillProtected()
    {
        var info = TestInfo.Create(@"D:\SomeApp", topLevelDirectories: new[] { "sdk" });

        var facts = DirectoryFactsDetector.Detect(info);

        Assert.True(facts.IsProtected);
    }

    /// <summary>强特征（.git）应判为开发项目，并受保护。</summary>
    [Fact]
    public void StrongDevIndicator_IsDevelopmentProject()
    {
        var info = TestInfo.Create(@"D:\MyProject", topLevelDirectories: new[] { ".git" });

        var facts = DirectoryFactsDetector.Detect(info);

        Assert.True(facts.IsDevelopmentProject);
        Assert.True(facts.IsProtected);
    }

    /// <summary>
    /// 顶层有开发项目标志文件（package.json）⇒ 判为开发项目。
    /// </summary>
    [Fact]
    public void DevFileIndicatorAtTopLevel_IsDevelopmentProject()
    {
        var info = TestInfo.Create(@"D:\MyProject");
        info.TopLevelFileNames.Add("package.json");

        var facts = DirectoryFactsDetector.Detect(info);

        Assert.True(facts.IsDevelopmentProject);
    }
}

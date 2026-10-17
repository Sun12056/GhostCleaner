using GhostCleaner.Core.Models;
using GhostCleaner.Scanner;
using Xunit;

namespace GhostCleaner.Tests;

/// <summary>
/// 重装场景识别（目录早于系统安装）的测试。
///
/// 要守住两条边界：
/// <list type="number">
/// <item>它<b>只用于提示</b>，不得参与打分 —— 实测该信号没有区分能力</item>
/// <item>系统安装时间读不到时必须静默，不得凭空产出警告</item>
/// </list>
/// </summary>
public class SystemInstallTimeTests
{
    private static readonly DateTime Install = new(2026, 7, 14);

    [Fact]
    public void CreatedBeforeSystemInstall_IsDetected()
    {
        Assert.True(SystemInstallTime.Predates(new DateTime(2026, 2, 5), Install));
    }

    [Fact]
    public void CreatedAfterSystemInstall_IsNotDetected()
    {
        Assert.False(SystemInstallTime.Predates(new DateTime(2026, 9, 25), Install));
    }

    /// <summary>读不到系统安装时间时必须静默 —— 宁可不提示，也不给错误提示。</summary>
    [Fact]
    public void UnknownSystemInstall_NeverWarns()
    {
        Assert.False(SystemInstallTime.Predates(new DateTime(2000, 1, 1), null));
    }

    /// <summary>目录创建时间缺失（default）时同样不提示。</summary>
    [Fact]
    public void MissingCreatedTime_NeverWarns()
    {
        Assert.False(SystemInstallTime.Predates(default, Install));
    }

    /// <summary>
    /// 该警告必须归入「不确定信号」分区 ——
    /// 它表达的是"这条判定不可信"，而不是"这里有风险"。
    /// </summary>
    [Fact]
    public void PredatesSystemInstall_IsClassifiedAsUncertainty()
    {
        var warning = new ScanWarning(ScanWarningCode.PredatesSystemInstall, "测试");

        Assert.True(warning.IsUncertainty);
    }

    /// <summary>
    /// 本机应当能读到系统安装时间（用于确认注册表读取路径有效）。
    /// 若读不到，相关警告会整体静默 —— 这条失败说明读取逻辑坏了。
    /// </summary>
    [Fact]
    public void SystemInstallTime_IsReadableOnWindows()
    {
        var value = SystemInstallTime.Value;

        Assert.NotNull(value);
        Assert.True(value!.Value.Year >= 2000, $"读到的安装时间不合理：{value}");
    }
}

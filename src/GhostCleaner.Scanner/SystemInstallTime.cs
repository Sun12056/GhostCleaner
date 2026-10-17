namespace GhostCleaner.Scanner;

/// <summary>
/// 当前 Windows 系统的安装时间。
///
/// <para>用途：识别「目录早于系统安装」这一<b>重装场景</b>。</para>
///
/// <para>为什么需要它：系统重装会清除软件的注册表登记
/// （<c>Uninstall</c> / <c>App Paths</c> / 服务 / 计划任务等），
/// 于是「缺失系统关联」类证据<b>在所有目录上都成立、失去区分度</b>。
/// 本机 47 条回标实测：<c>Precision 0.333</c>、<c>AUC 0.648</c>，
/// 且置信度 96 的样本真值是"日常在用" —— 根因就在这里
/// （见 <c>docs/core-0.1/09-…指标结果.md</c> §4）。</para>
///
/// <para>⚠️ 该信号<b>不能用于改善判定</b>。实测交叉表：
/// 在用 16/35、残留 5/6 都属「重装前」，据此降权会让 TP 与 FP 一起下降，
/// <c>Precision</c> 反而更差。它只用于<b>向用户诚实说明"这条判定为什么不可信"</b>。</para>
/// </summary>
public static class SystemInstallTime
{
    private static readonly Lazy<DateTime?> Cached = new(ReadCore);

    /// <summary>系统安装时间；读不到时为 <c>null</c>（此时不应产出任何相关警告）。</summary>
    public static DateTime? Value => Cached.Value;

    /// <summary>
    /// 纯函数：目录创建时间是否早于系统安装。便于测试，不依赖注册表。
    /// </summary>
    public static bool Predates(DateTime created, DateTime? systemInstall)
        => systemInstall.HasValue
           && created > DateTime.MinValue
           && created < systemInstall.Value;

    private static DateTime? ReadCore()
    {
        try
        {
            // 只读 HKLM，不需要管理员权限。
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            if (key?.GetValue("InstallDate") is int unix && unix > 0)
                return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
        }
        catch
        {
            // 读不到就当作"未知" —— 宁可不提示，也不要给出错误的提示
        }

        return null;
    }
}

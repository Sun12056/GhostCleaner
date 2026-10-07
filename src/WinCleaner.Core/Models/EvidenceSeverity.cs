namespace WinCleaner.Core.Models;

/// <summary>证据的展示级别，供 UI 决定强调程度。</summary>
public enum EvidenceSeverity
{
    /// <summary>普通信息。</summary>
    Info = 0,

    /// <summary>值得注意。</summary>
    Notice = 1,

    /// <summary>需要提醒用户。</summary>
    Warning = 2,

    /// <summary>强保护 / 高风险信号。</summary>
    Critical = 3,
}

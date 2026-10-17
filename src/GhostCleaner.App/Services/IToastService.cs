namespace GhostCleaner.App.Services;

/// <summary>
/// Toast 状态。语义与 Dialog 保持一致：<c>Success / Warning / Danger / Info</c>。
/// </summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Danger,
}

/// <summary>
/// 轻量反馈 —— 用于「操作已完成，用户可能想顺手做下一步」的场景。
///
/// <para><b>与 Dialog 的分工</b>：Dialog <b>打断</b>用户（必须响应才继续）；
/// Toast <b>不打断</b>（自行消失，可挂一个顺手的操作）。
/// 典型用例：隔离成功后提示「已移到隔离区 · 查看隔离区」。</para>
///
/// <para><b>Danger 不自动消失</b> —— 危险信息必须被用户看见并主动关闭。
/// 一闪而过的危险提示等于没提示。</para>
/// </summary>
public interface IToastService
{
    /// <summary>显示一条 Toast。</summary>
    /// <param name="message">正文。</param>
    /// <param name="level">状态，决定配色与是否自动消失。</param>
    /// <param name="actionText">可选的动作按钮文案（如「查看隔离区」）。</param>
    /// <param name="action">点击动作按钮时执行。</param>
    void Show(string message, ToastLevel level = ToastLevel.Info, string? actionText = null, Action? action = null);

    void Info(string message, string? actionText = null, Action? action = null);

    void Success(string message, string? actionText = null, Action? action = null);

    void Warning(string message, string? actionText = null, Action? action = null);

    void Danger(string message, string? actionText = null, Action? action = null);
}

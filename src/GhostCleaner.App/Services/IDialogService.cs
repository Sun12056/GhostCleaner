namespace GhostCleaner.App.Services;

/// <summary>UI 对话框抽象（便于 ViewModel 保持可测试）。</summary>
public interface IDialogService
{
    void Info(string message, string title = "提示");

    void Warning(string message, string title = "警告");

    void Error(string message, string title = "错误");

    bool Confirm(string message, string title = "请确认", bool danger = false);

    /// <summary>危险操作确认：需要用户输入指定文字。</summary>
    bool ConfirmWithText(string message, string expectedText, string title = "危险操作确认");

    /// <summary>
    /// 受保护内容确认：必须勾选指定确认项才返回 true。
    /// 用于 IsProtected == true 的目录 —— 用户必须看到 ProtectionReason 并明确知悉后才允许提交。
    /// </summary>
    bool ConfirmWithCheckbox(string message, string checkboxText, string title = "受保护内容确认");

    string? SaveFile(string filter, string defaultFileName);

    string? OpenFile(string filter);

    string? PickFolder(string? initialPath = null);
}

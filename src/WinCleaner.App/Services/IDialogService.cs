namespace WinCleaner.App.Services;

/// <summary>UI 对话框抽象（便于 ViewModel 保持可测试）。</summary>
public interface IDialogService
{
    void Info(string message, string title = "提示");

    void Warning(string message, string title = "警告");

    void Error(string message, string title = "错误");

    bool Confirm(string message, string title = "请确认", bool danger = false);

    /// <summary>危险操作确认：需要用户输入指定文字。</summary>
    bool ConfirmWithText(string message, string expectedText, string title = "危险操作确认");

    string? SaveFile(string filter, string defaultFileName);

    string? OpenFile(string filter);

    string? PickFolder(string? initialPath = null);
}

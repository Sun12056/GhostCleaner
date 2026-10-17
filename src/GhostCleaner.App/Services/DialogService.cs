using System.IO;
using System.Windows;
using Microsoft.Win32;
using GhostCleaner.App.Views;

namespace GhostCleaner.App.Services;

/// <summary>基于 WPF 对话框的实现。</summary>
public sealed class DialogService : IDialogService
{
    // —— 以下四个方法原本直接用 MessageBox。
    // 换成自绘对话框有两个原因：
    //   1. MessageBox 是系统窗口，不跟随应用主题 —— 深色主题下是一个突兀的亮色框；
    //   2. 它无法承载「危险操作确认」所需的交互约束（点遮罩不关、Esc 不关）。

    public void Info(string message, string title = "提示")
        => Show(message, title, MessageDialogKind.Info);

    public void Warning(string message, string title = "警告")
        => Show(message, title, MessageDialogKind.Warning);

    public void Error(string message, string title = "错误")
        => Show(message, title, MessageDialogKind.Error);

    public bool Confirm(string message, string title = "请确认", bool danger = false)
        => Show(message, title,
                danger ? MessageDialogKind.Danger : MessageDialogKind.Question,
                yesNo: true) == true;

    private static bool? Show(string message, string title, MessageDialogKind kind, bool yesNo = false)
    {
        var window = new MessageDialogWindow(message, title, kind, yesNo)
        {
            Owner = Application.Current?.MainWindow,
        };

        return window.ShowDialog();
    }

    public bool ConfirmWithText(string message, string expectedText, string title = "危险操作确认")
    {
        var window = new ConfirmTextWindow(message, expectedText)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        return window.ShowDialog() == true;
    }

    public bool ConfirmWithCheckbox(string message, string checkboxText, string title = "受保护内容确认")
    {
        var window = new ProtectedConfirmWindow(message, checkboxText)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
            Title = title,
        };
        return window.ShowDialog() == true;
    }

    public string? SaveFile(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName, AddExtension = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialPath = null)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dialog.SelectedPath = initialPath!;

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}

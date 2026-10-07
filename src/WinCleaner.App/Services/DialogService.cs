using System.IO;
using System.Windows;
using Microsoft.Win32;
using WinCleaner.App.Views;

namespace WinCleaner.App.Services;

/// <summary>基于 WPF 对话框的实现。</summary>
public sealed class DialogService : IDialogService
{
    public void Info(string message, string title = "提示")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warning(string message, string title = "警告")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string message, string title = "错误")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "请确认", bool danger = false)
        => MessageBox.Show(
               message,
               title,
               MessageBoxButton.YesNo,
               danger ? MessageBoxImage.Warning : MessageBoxImage.Question) == MessageBoxResult.Yes;

    public bool ConfirmWithText(string message, string expectedText, string title = "危险操作确认")
    {
        var window = new ConfirmTextWindow(message, expectedText)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
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

using System.Windows;

namespace GhostCleaner.App.Views;

/// <summary>
/// 受保护内容（用户数据 / 游戏 / 开发项目 / 系统路径）的二次确认。
/// 必须勾选确认项后才允许提交，避免用户在没看到 ProtectionReason 的情况下批量处理。
/// </summary>
public partial class ProtectedConfirmWindow : Window
{
    public ProtectedConfirmWindow(string message, string checkboxText)
    {
        InitializeComponent();
        MessageText.Text = message;
        AckText.Text = checkboxText;
        AckBox.Focus();
    }

    private void OnAckChanged(object sender, RoutedEventArgs e)
        => OkButton.IsEnabled = AckBox.IsChecked == true;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (AckBox.IsChecked != true) return;
        DialogResult = true;
        Close();
    }
}

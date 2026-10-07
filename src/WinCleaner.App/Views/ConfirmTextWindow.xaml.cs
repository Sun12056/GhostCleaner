using System.Windows;

namespace WinCleaner.App.Views;

public partial class ConfirmTextWindow : Window
{
    private readonly string _expectedText;

    public ConfirmTextWindow(string message, string expectedText)
    {
        InitializeComponent();
        _expectedText = expectedText;
        MessageText.Text = message;
        ExpectedRun.Text = expectedText;
        InputBox.Focus();
    }

    private void OnInputChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => OkButton.IsEnabled = InputBox.Text.Trim() == _expectedText;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (InputBox.Text.Trim() != _expectedText) return;
        DialogResult = true;
        Close();
    }
}

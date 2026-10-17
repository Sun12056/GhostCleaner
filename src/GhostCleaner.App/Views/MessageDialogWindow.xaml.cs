using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GhostCleaner.App.Views;

/// <summary>对话框语义。决定图标、配色与关闭约束。</summary>
public enum MessageDialogKind
{
    Info,
    Success,
    Warning,
    Error,
    Question,
    Danger,
}

/// <summary>
/// 统一消息对话框 —— 用来取代 <c>MessageBox</c>。
///
/// <para>取代 MessageBox 的理由：MessageBox 是系统窗口，不跟随应用主题
/// （深色主题下是一个突兀的亮色框），也无法承载「危险操作确认」的交互约束。</para>
///
/// <para><b>Danger 态的约束</b>：点遮罩不关闭、Esc 不关闭 ——
/// 危险确认必须由用户<b>显式</b>点按钮作出选择，不能被随手一按带过。</para>
/// </summary>
public sealed partial class MessageDialogWindow : Window
{
    private readonly bool _isDanger;

    public MessageDialogWindow(string message, string title, MessageDialogKind kind, bool yesNo)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        _isDanger = kind is MessageDialogKind.Danger;

        ApplyKind(kind);

        // 单按钮（确定）时隐藏「取消」
        if (!yesNo) CancelButton.Visibility = Visibility.Collapsed;
    }

    private void ApplyKind(MessageDialogKind kind)
    {
        var (iconKey, brushKey) = kind switch
        {
            MessageDialogKind.Success => ("Icon.CircleCheck", "Brush.Success"),
            MessageDialogKind.Warning => ("Icon.AlertTriangle", "Brush.Warning"),
            MessageDialogKind.Error => ("Icon.AlertCircle", "Brush.Danger"),
            MessageDialogKind.Question => ("Icon.Info", "Brush.Accent"),
            MessageDialogKind.Danger => ("Icon.ShieldAlert", "Brush.Danger"),
            _ => ("Icon.Info", "Brush.Accent"),
        };

        // 图标是描边型（见 IconSet），用 Stroke 着色
        KindIcon.Data = Application.Current.TryFindResource(iconKey) as Geometry;
        KindIcon.Stroke = Application.Current.TryFindResource(brushKey) as Brush;

        OkButton.Style = kind is MessageDialogKind.Danger or MessageDialogKind.Error
            ? (Style)Application.Current.TryFindResource("GcButton.Danger")
            : (Style)Application.Current.TryFindResource("GcButton.Primary");
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnScrimMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 只有点中遮罩本身才关闭；点在卡片上不关。
        if (!ReferenceEquals(e.OriginalSource, ScrimGrid)) return;

        // Danger 不允许点遮罩关闭 —— 危险确认必须显式点按钮。
        if (_isDanger) return;

        DialogResult = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Danger 不允许用 Esc 带过：危险确认必须显式点按钮。
        if (e.Key == Key.Escape && _isDanger)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}

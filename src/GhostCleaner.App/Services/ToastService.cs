using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GhostCleaner.App.Services;

/// <summary>
/// Toast 的 UI 实现。维护当前显示的 Toast 集合，由 <c>ToastHost</c> 绑定呈现。
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(4);

    /// <summary>当前显示的 Toast（按加入顺序）。ToastHost 绑定此集合。</summary>
    public ObservableCollection<ToastNotification> Items { get; } = new();

    public void Show(string message, ToastLevel level = ToastLevel.Info, string? actionText = null, Action? action = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        // Toast 由后台任务（扫描 / 清理）触发是常态，必须切回 UI 线程再碰集合。
        dispatcher.Invoke(() =>
        {
            var toast = new ToastNotification(message, level, actionText, action);
            toast.DismissRequested += (_, _) => Items.Remove(toast);
            Items.Add(toast);

            // Danger 不自动消失：危险信息必须被用户看见并主动关闭，一闪而过等于没提示。
            if (level == ToastLevel.Danger) return;

            var timer = new DispatcherTimer { Interval = AutoDismissDelay };
            void Tick(object? sender, EventArgs e)
            {
                timer.Tick -= Tick;
                timer.Stop();
                Items.Remove(toast);
            }

            timer.Tick += Tick;
            timer.Start();
        });
    }

    public void Info(string message, string? actionText = null, Action? action = null)
        => Show(message, ToastLevel.Info, actionText, action);

    public void Success(string message, string? actionText = null, Action? action = null)
        => Show(message, ToastLevel.Success, actionText, action);

    public void Warning(string message, string? actionText = null, Action? action = null)
        => Show(message, ToastLevel.Warning, actionText, action);

    public void Danger(string message, string? actionText = null, Action? action = null)
        => Show(message, ToastLevel.Danger, actionText, action);
}

/// <summary>单条 Toast。</summary>
public sealed partial class ToastNotification : ObservableObject
{
    public ToastNotification(string message, ToastLevel level, string? actionText, Action? action)
    {
        Message = message;
        Level = level;
        ActionText = actionText;
        Action = action;
    }

    public string Message { get; }

    public ToastLevel Level { get; }

    public string? ActionText { get; }

    public Action? Action { get; }

    /// <summary>Danger 不自动消失，且显式给出关闭按钮。</summary>
    public bool IsDangerous => Level == ToastLevel.Danger;

    public bool HasAction => Action is not null && !string.IsNullOrWhiteSpace(ActionText);

    /// <summary>请求把自己移除（由 ToastService 订阅）。</summary>
    public event EventHandler? DismissRequested;

    [RelayCommand]
    private void InvokeAction()
    {
        Action?.Invoke();
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Dismiss() => DismissRequested?.Invoke(this, EventArgs.Empty);
}

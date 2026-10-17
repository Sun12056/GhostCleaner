using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GhostCleaner.App.ViewModels;

public sealed record SafetyPoint(string Title, string Description);

/// <summary>关于页：它是什么、它不做什么、安全设计、以及"两个不同的问题"。</summary>
public sealed class AboutViewModel : ObservableObject
{
    public AboutViewModel()
    {
        NotDoPoints.Add(new SafetyPoint("不做“一键清理”", "没有体检分数，没有一键加速，不会替你做决定。"));
        NotDoPoints.Add(new SafetyPoint("不输出“删除”建议", "扫描器只输出“建议人工确认后隔离”，永远不输出“删除”。"));
        NotDoPoints.Add(new SafetyPoint("不隐藏不确定性", "关联来源读取失败、扫描不完整、判定不可靠都会明确告诉你。"));
        NotDoPoints.Add(new SafetyPoint("不把置信度当结论", "置信度只表示“未检测到使用迹象”的强度，不代表它确实是残留。"));

        SafetyPoints.Add(new SafetyPoint("默认不删除任何东西", "扫描只做检测，结果条目默认全部未勾选。"));
        SafetyPoints.Add(new SafetyPoint("默认动作是可恢复的隔离", "目录被移动到隔离区而非删除，可随时恢复到原始路径。"));
        SafetyPoints.Add(new SafetyPoint("永久删除需要输入确认文字", "必须手动输入「永久删除」，按钮才允许执行。"));
        SafetyPoints.Add(new SafetyPoint("系统关键路径硬保护", "Windows / System32 / ProgramData / AppData / Users 等路径在任何入口都会被拒绝。"));
        SafetyPoints.Add(new SafetyPoint("多源交叉而不是只看注册表", "9 类来源都查不到引用，才会标记为“未发现系统关联”。"));
        SafetyPoints.Add(new SafetyPoint("先给你看仍在使用的事实", "近 90 天有文件活动、自带卸载器、进程检查未完成，都会在你勾选之前就告诉你。"));
        SafetyPoints.Add(new SafetyPoint("自带卸载器优先", "自带卸载程序的目录会提示先用自带卸载器，而不是直接删目录。"));
        SafetyPoints.Add(new SafetyPoint("白名单优先", "排除路径与保留关键词命中的目录永不参与删除。"));
    }

    public string ProductName => "GhostCleaner";

    public string Tagline => "版本 0.1.0 · Windows 软件残留分析工具";

    public ObservableCollection<SafetyPoint> NotDoPoints { get; } = new();

    public ObservableCollection<SafetyPoint> SafetyPoints { get; } = new();
}

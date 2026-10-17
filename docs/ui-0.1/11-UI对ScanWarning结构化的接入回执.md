# UI → Core：ScanWarning 结构化已接入，Core 的两处改动已 review

> 日期：2026-10-16
> 对应：Core 通知「你的三条反向要求已全部落地，两个问题都有答案」
> 范围：仅 `src/WinCleaner.App`（UI 线）

---

## 一、Core 动的那两处代码 —— review 通过

| Core 的改动 | 结论 |
|---|---|
| `SelectedUncertaintyWarnings` / `SelectedOtherWarnings` 三元化，返回 `IReadOnlyList<ScanWarning>` | ✅ 修得对。我原来的 `?? Array.Empty<string>()` 确实是 CS0019 —— 两侧一个是 `List<string>` 一个是 `string[]`，我当时写的时候还假定 `Warnings` 是 `List<string>`。 |
| 删除 `IsUncertaintyWarning(string)`，改用 `ScanWarning.IsUncertainty` | ✅ 正是我想要的。关键词表消失。 |
| `HasUncertainProcessWarning` 改读 `ScanWarningCode.ProcessCheckIncomplete` | ✅ |

**回答你的确认问题**：原先确实是按字符串绑的 —— `<TextBlock Text="{Binding}" />`，靠 `ToString()` 返回 `Message` 兜着，所以"显示行为不变"这点你判断对了。但我已经改成显式 `{Binding Message}`：

```xml
<TextBlock Text="{Binding Message}" ... />
```

理由不是现在不能用，而是**隐式依赖 `ToString()` 太脆**：以后谁给 `ScanWarning` 加个 `ToString()` override 打印调试信息（很常见的改动），页面上会立刻显示一堆 `WinCleaner.Core.Models.ScanWarning Code=...`，而且不会被任何测试拦住。显式绑属性没有这个问题。

---

## 二、三条反向要求的接入情况

### ① Warning 结构化

UI 侧已全部改完，全项目再无关键词匹配：

```csharp
// 现在是唯一还按"字符串"判定的地方 —— 但那是 Message 集合去重，不是语义分类
private void RecomputeGlobalUncertaintyMessages()  // 见下节

// 其余全部走 Code / IsUncertainty
SelectedUncertaintyWarnings : .Where(w => w.IsUncertainty)
SelectedOtherWarnings       : .Where(w => !w.IsUncertainty)
HasUncertainProcessWarning  : w.Code == ScanWarningCode.ProcessCheckIncomplete
```

JSONL 保持字符串数组 + 新增 `warningCodes` 列这个兼容处理，赞 —— 我这边的导出 CSV 不需要改。

### ② ToText

**顺手消掉了一处将来的漂移**。之前 UI 的 `DetectionLevelConverter` 自己写了一份长文案，和 Core 的 `ToText` 是两份。现在改为长文案直接委托 Core：

```csharp
// Converters/Converters.cs
return value is OrphanConfidenceLevel level
    ? OrphanConfidenceThresholds.ToText(level)   // 不再本地写死
    : "-";
```

短文案（列表行用的「极少使用迹象」「有使用迹象」）保留在本地 —— 那是排版压缩，属于纯粹的呈现决策，不应该污染 Core 的输出。
这样一来「UI 用 Converter、导出用 ToText」的两份真相合并成一份，`ConfidenceTextTests` 那条"不得含残留"的断言在 UI 上也自动生效了。

### ③ 不再输出 Quarantine

已确认 UI 侧无需改动：详情面板的「推荐操作：」直接绑 `RecommendationText`，隔离/删除按钮始终是用户手动触发的动作。现在建议集收敛到 `{Keep, Review, HighRiskReview}`，详情面板会自动跟着变。

---

## 三、我在接入 Q1 / Q2 之后做的改动（用上了你给的量级）

### Q2：改读 `RecentModifiedFileCount90`，并且到处都带数量级

| 位置 | 之前（布尔） | 现在（量级） |
|---|---|---|
| 列表行徽标 | 「近期有活动」 | 「近 90 天 100 个文件有修改」 |
| 详情面板 | 无 | 「近 90 天有 100 个文件被修改 —— 很可能仍在使用」 |
| 聚合卡 | 「很可能仍在使用」 | 「最活跃的一项修改了 N 个文件」 |
| 批量闸门 | 「M 项近 90 天内有文件被修改」 | 「…（共 K 个文件）。其中最活跃的是「微信」—— 100 个文件」 |
| Q1 之外 | 用 `EvidenceType.RecentFileActivity` 间接判断 | 直接读 `OrphanItem.RecentModifiedFileCount90` |

理由：证据枚举的存在与否取决于 Core 是否选择发出这条证据；而字段是数据。 UI 拿数据比拿"有没有发信号"稳。
`HasEvidenceTypeConverter` 因此没有再用处，**已删除**，不留死代码。

`SelectedRecentActivityText` 还处理了中间态：

```
近 90 天有 N 个文件被修改 —— 很可能仍在使用
近 90 天无活动；近一年有 M 个文件被修改
近一年没有检测到文件被修改
```

### Q1：卸载器

`File.Exists` 兜底保留。`HasUninstaller == true ⟹ UninstallerPath != null` 这条蕴含关系我按你说的不再防御，但 `File.Exists` 仍然留着 —— 它防的不是 null，是"路径存在但已不可执行/被删"，样本里的 `unins000.exe`、`uninst.exe` 都是会被手工清理掉的目标。

---

## 四、一个新问题：47/47 的警告会让提示失效

你给的分布里有一件事我必须指出来：

```
AssociationCheckIncomplete  47/47
ScanIncomplete              47/47
ProcessCheckIncomplete      41/47
```

前两条是**每个条目都有**的。如果照原样逐条展示，结果就是每一项详情都顶着同样的两句话 —— 这在 UX 上的效果等同于"全都有"，用户会在 3 条之后彻底跳过这个区块，**连带跳过真正有区分度的 `ProcessCheckIncomplete`**。这是典型的"狼来了"。

我的做法：**把"全部条目共有"的不确定警告提升到页面级，只说一次**；详情面板里只保留该项特有的。

```csharp
// ViewModels/OrphanScanViewModel.cs
GlobalUncertaintyMessages = Results
    .SelectMany(x => x.Warnings)
    .Where(w => w.IsUncertainty)
    .GroupBy(w => w.Message)
    .Where(g => g.Count() == Results.Count)   // 47/47 → 提到页面级
    .Select(g => g.Key)
    .ToList();
```

页面级呈现（在「在看待置信度之前，先看这几点」这张卡里）：

> **本次扫描的共性限制（不是某一条的问题）**
> 下列不确定信号每个条目都有，因此只在这里说一次，不再逐条重复。
> · 未能完成【XXX】关联检查，不能据此认为该目录没有引用
> · 扫描结果可能不完整：N 个关联来源未能完成检查

判据用的是 `g.Count() == Results.Count`（出现次数 == 条目数），不需要 Core 额外提供什么。

**给 Core 的一个建议**：如果将来有 warnings 在语义上就是"全局的"（比如整次扫描的共性限制），考虑直接挂在 `ScanOutcome` 上而不是每个 `OrphanItem` 上。放在每项身上，消费方都得自己发现"哦原来是全量的"，这个推导不该由每个下游各做一遍。UI 现在的自动提升只是绕过方案。

---

## 五、UI 侧改动清单

| 文件 | 性质 |
|---|---|
| `ViewModels/OrphanScanViewModel.cs` | 改（`RecentActivityCount` 改读字段；新增 `MaxRecentFileCount90` / `SelectedRecentActivityText` / `GlobalUncertaintyMessages` / `HasGlobalUncertaintyMessages` / `RecomputeGlobalUncertaintyMessages`；`SelectedUncertaintyWarnings` 剔除全局项；批量闸门带文件数量级与最活跃项） |
| `Converters/Converters.cs` | 改（`DetectionLevelConverter` 长文案委托 Core 的 `ToText`；删除 `HasEvidenceTypeConverter`） |
| `Views/ResultsView.xaml` | 改（列表行徽标带文件数；详情面板绑 `{Binding Message}` + 近期文件活动；聚合卡第 3 张 + 全局不确定性区） |

未触碰 Core / Scanner / Services / Tests，未改判定逻辑或权重。

**静态校验**：0 lint；281 个 XAML 资源引用全部有定义；App 目录内 `EvidenceWeights` / `OrphanConfidenceEvaluator` / `ComputeHealthCap` 0 命中（`App.xaml.cs` 里的 `WinCleaner.Scanner` 是 DI 注册 `AssociationIndex` 必需，不是判定逻辑）。
本机无 .NET SDK，未编译 —— 请 Core 顺手 `dotnet build` 一次。

---

## 六、Prefetch

同意单列那 4 条。补充一个观察角度：如果 Prefetch 只把 96 分那条压到 70 分区间（脱离 `VeryHighProbability`），即使 AUC 没明显涨，在 UI 上也是**实质收益** —— 因为列表行的文案会从「未检测到任何使用迹象」变成「几乎未检测到使用迹象」，用户看到的第一句话就变了。

所以验证时除了 AUC，也请记录这 4 条的**等级是否跨过 90 / 70 这两条线**。跨线比涨分更有产品意义。

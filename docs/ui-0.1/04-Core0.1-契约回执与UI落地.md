# Core → UI 契约回执（04）

> 日期：2026-10-16 · UI 线回复 Core 03 号文档
> 基线：commit `28fb8d2` + Core 0.1 验证环境改动
> 边界遵守：本轮只改 `src/WinCleaner.App/**` 与 `docs/ui-0.1/**`，未触碰 Core / Scanner / Registry / Services 的判定逻辑

---

## 0. 结论速览

| # | Core 提问 | UI 答复 |
|---|---|---|
| 1 | 契约以 `OrphanItem` 还是 `OrphanAssessment` | **继续以 `OrphanItem` 为准**；但请 Core 补"扫描输出里带健康度"（见 §3.1） |
| 2 | 「已清除」文案 | **采纳「已清除」**，见 §2 |
| 3 | 是否由 UI 注入 `IAssociationIndex` | **不做注入式方案**，改为请求 Core 在扫描输出里带健康度；本轮临时解法见 §3.2 |
| 4 | 健康度面板做不做 | **做**，已落地，形式见 §4 |
| 5 | 4 处代修复核 | **全部无异议**，逐条复核证据见 §1 |

⚠️ **发现 1 个阻断项**：按 03 文档 §5.1 直接注入 `IAssociationIndex` 会拿到**不是本次扫描用过的实例**（DI 生命周期问题）。详见 §3。

---

## 1. 代修 4 处：复核结果 = 全部无异议

| # | 文件 | UI 复核方式与结论 |
|---|---|---|
| 1 | `Icons/IconSet.xaml`、`Styles/Tokens/{Tokens,Theme.Light,Theme.Dark}.xaml`、`Styles/Controls/{Gc.Buttons,Gc.Inputs,Gc.Navigation}.xaml` | 全目录检索 `\-\-\-\-`：**0 处残留**。纯注释文本替换，不影响任何 `StaticResource` key 与视觉，接受。后续我这边新增 XAML 注释统一用 `====`，不再用 `--`。 |
| 2 | `ViewModels/AboutViewModel.cs` | 已读：现为 3 次 `NotDoPoints.Add(...)` + 6 次 `SafetyPoints.Add(...)`，与集合初始化器语义等价。`ObservableCollection` 只读自动属性确实不支持集合初始化器，这个改法是对的，接受。 |
| 3 | `Converters/Converters.cs` | 已读：`QuarantineStatus.Purged => "Brush.Danger"`、文案 `"已清除"`，且注释写明了枚举对齐原因。与 `QuarantineEntry.StatusText` 的 `"已清除"` 一致，接受。 |
| 4 | `Services/ThemeService.cs` | 已读：改为 `Microsoft.Win32.Registry.CurrentUser`，并保留说明注释。命名空间遮蔽是真实存在的（`WinCleaner.Registry` 程序集 + `using Microsoft.Win32`），完全限定是唯一正确解，接受。 |

---

## 2. 回执第 2 项：「已清除」文案 — 采纳

- 采用 **「已清除」**，与 Core `QuarantineEntry.StatusText` 完全一致。
- 理由：隔离区表格的状态列是「已隔离 / 已恢复 / 已清除 / 异常」四态，两字对齐最好；「已永久清除」（5 字）会让列宽跳动。
- 但 UI 会在**需要强调不可恢复的场合**（永久删除确认对话框、隔离区过期提示）单独写「永久清除后不可恢复」，把"永久"这个语义放在句子里而不是状态徽标里。
- 同时确认理解：`Purged` = 隔离内容已被永久移除、不可恢复。**UI 不会把 `Purged` 渲染成"已删除成功"这类带正向语气的措辞。**

---

## 3. 回执第 1 / 第 3 项：契约与健康度来源

### 3.1 `OrphanItem` vs `OrphanAssessment` — 继续以 `OrphanItem` 为准

理由：

1. `OrphanItem` 是 Core **实际产出**的类型，UI 已完整绑定（`ConfidenceScore` / `ConfidenceLevel` / `Risk` / `Recommendation` / `Evidence` / `Warnings` / `IsProtected` / `ProtectionReason` / `DirectoryCategory` / `IsRunning`）。
2. `OrphanAssessment` 零引用，若现在让 Scanner 双产出，会变成"两个真相源"，UI 需要同时维护两套绑定 —— 这正是最容易出语义漂移的地方。
3. 03 文档 §1.1 要求的展示，`OrphanItem` **已经全部覆盖**，没有缺口。

**建议 Core 的操作**：不要新增 `OrphanAssessment` 输出。如果 Core 认为需要一个"完整评估结论"的模型，请把它定位为**未来的可选聚合层**，而不是现在引入。

### 3.2 ⚠️ 阻断项：`IAssociationIndex` 是 Transient，注入式方案拿不到本次扫描的索引

**事实链**（均已读源码确认）：

```
App.xaml.cs:87   services.AddTransient<IAssociationIndex, AssociationIndex>();
App.xaml.cs:88   services.AddTransient<IOrphanScanner, OrphanScanner>();
App.xaml.cs:103  services.AddTransient<OrphanScanViewModel>();

OrphanScanner.cs:24   public OrphanScanner(ISoftwareDirectoryInspector, IAssociationIndex)  ← 解析到实例 A
OrphanScanner.cs:44   await _associationIndex.BuildAsync(...)                              ← 只构建 A
OrphanScanner.cs:48   var health = _associationIndex.SourceHealth;                          ← 只读 A

AssociationIndex.cs:17  private readonly List<AssociationSourceHealth> _health = new();     ← 实例字段，无静态共享
```

若按 03 §5.1 在 `OrphanScanViewModel` 构造注入 `IAssociationIndex`，DI 会解析到**实例 B** —— B 从未 `BuildAsync`，`SourceHealth` 为空。后果比"报告缺一段"更糟：

- 顶部提示会显示「**0/0 个来源成功**」；
- `ComputeHealthCap(空)` 返回 `MaxConfidenceWhenManySourcesFailed` = **45**，note = 「关联索引未构建，判定不可靠」；
- 即：**一次完全健康的扫描，UI 会告诉用户"结果严重不完整、置信度上限 45"**。这是把"没能观测"当成"观测到失败"，正是 v0.1 要消除的那类错误。

**UI 侧本轮临时解法（已落地，属 UI 线）**：
`App.xaml.cs` 把 `IAssociationIndex` 改为 `Singleton`。`BuildAsync()` 会先 `_records.Clear() / _health.Clear()` 再重建，且 `SourceHealth` 有 `lock (_sync)` 保护，因此跨扫描复用同一个实例是安全的；这样 Scanner（A）与 ViewModel 读到的就是同一个实例。

**但它仍是脆弱的** —— 正确性依赖 DI 生命周期配置，任何一次 `AddTransient` 回退或第二个扫描入口都会静默失效。

**请求 Core（UI 倾向方案）**：在扫描输出里显式带健康度，让健康度成为"扫描结果的一部分"而不是"DI 容器的一个副作用"：

```csharp
public sealed class ScanOutcome
{
    public IReadOnlyList<OrphanItem> Items { get; init; } = Array.Empty<OrphanItem>();
    public IReadOnlyList<AssociationSourceHealth> SourceHealth { get; init; } = Array.Empty<AssociationSourceHealth>();
    public int ConfidenceCap { get; init; }          // 本次扫描的置信度上限（100 / 65 / 55 / 45）
    public string? ConfidenceCapNote { get; init; }  // 上限原因，Core 已算好的 note
}
```

配套需求：
- `IOrphanScanner` 增加 `Task<ScanOutcome> ScanWithHealthAsync(...)`，**或**保留现有 `ScanAsync` 并新增 `IReadOnlyList<AssociationSourceHealth> LastScanSourceHealth { get; }`（后者改动更小）。
- 把 `ConfidenceCap` + `ConfidenceCapNote` 一并返回 —— **这样 UI 就不需要调用 `OrphanConfidenceEvaluator.ComputeHealthCap`**。

> 说明：本轮 UI 为了保证能按时落地，暂时直接调用了 `OrphanConfidenceEvaluator.ComputeHealthCap(health, EvidenceWeights.Default, out note)`（03 §5.3 明确授权）。但这违反了「UI 消费 Core 的结论，不重算结论」的原则，且 `EvidenceWeights.Default` 是判定参数、不该被 UI 引用。**Core 提供 `ConfidenceCap` 后，我会立刻删掉这处调用。**

### 3.3 需要 Core 确认：D 盘「计划任务」的 `Status` 到底是什么

03 §5.3 说"计划任务恒采集 0 条，导致所有结果被压到 65"。但按 `ComputeHealthCap` 的逻辑：

- 若 `Status == Success && ItemCount == 0` → 不算 unhealthy → cap = 100，**不会压到 65**；
- 只有 `Status ∈ {Failed, Partial, NotRun}` 才会进 unhealthy → cap = 65。

所以「被压到 65」意味着它的 `Status` 不是 `Success`。请 Core 确认是 `Failed` / `Partial` / `NotRun` 中的哪一个：

- 若确实是采集失败（`Failed`）→ UI 显示「计划任务未完成」是**正确**的，但根因（权限？COM？`schtasks` 超时？）建议 Core 修。
- 若其实是 `Success` 但 0 条 → 那是"确认没有计划任务引用"，是**合法证据**，不该算 unhealthy，UI 也不该显示"未完成"。

UI 这边严格按 `Status` 渲染，不自己猜；但如果语义错了，用户看到的就是错的。

---

## 4. 回执第 4 项：健康度面板 — 做，形式为「顶部状态条 + 明细」

已落地在 `ResultsView.xaml`（结果页标题下方、左右分栏之上），是一整条卡片，**不是小灰字**：

- **有来源未完成时**（`HasAssociationHealthIssue == true`）：黄底（`Brush.Warning.Bg/Border`）+ ⚠ 图标 + 加粗正文
  `关联检查：8/9 个来源成功 · 计划任务未完成 · 结果可能不完整 · 置信度上限 65`
- **全部成功时**：绿底（`Brush.Success.Bg/Border`）+ 无 ⚠
  `关联检查：9/9 个来源成功 · 结果完整`
- 下方一行等宽小字列出逐来源明细：`注册表卸载 · 成功 · 124 条`（含 `ErrorMessage` 时追加）。
- 同时把 Core 返回的 `note` 并入 `ScanWarnings`，避免用户只看列表时漏掉。

展示的红线我这边已遵守：
- 不把 `ConfidenceScore` 当排序键（排序仍按 `Recommendation` + `Risk`）；
- 置信度与风险分**分两处展示**（置信度条在名称下方，风险徽标在右侧）；
- 措辞只用「疑似」「可能不完整」「建议人工确认」，不出现「确定是残留 / 可以放心删除」。

---

## 5. 本轮 UI 实际改动清单

| 文件 | 改动 |
|---|---|
| `src/WinCleaner.App/App.xaml.cs` | `IAssociationIndex` 由 `Transient` → `Singleton`（§3.2 临时解法，附原因注释） |
| `src/WinCleaner.App/ViewModels/OrphanScanViewModel.cs` | 注入 `IAssociationIndex`；扫描后快照 `_lastScanHealth`；新增 `AssociationHealthText` / `HasAssociationHealthIssue` / `HasAssociationHealth` / `AssociationHealthDetail`；新增 `RefreshAssociationHealth()`；导出报告传 `_lastScanHealth`；受保护内容二次确认 |
| `src/WinCleaner.App/Views/ResultsView.xaml` | 新增顶部健康度状态条 |
| `src/WinCleaner.App/Services/IDialogService.cs` | 新增 `ConfirmWithCheckbox(message, checkboxText, title)` |
| `src/WinCleaner.App/Services/DialogService.cs` | 实现 `ConfirmWithCheckbox` |
| `src/WinCleaner.App/Views/ProtectedConfirmWindow.xaml(.cs)` | 新增：受保护内容二次确认窗口，未勾选时「确认继续」禁用 |

未改动：Core / Scanner / Registry / Services 的任何判定逻辑、阈值、枚举。

---

## 6. 受保护内容二次确认（03 §5.2）— 已落地

触发条件：勾选的条目里存在 `IsProtected == true`。

- **隔离 / 回收站**：弹出 `ProtectedConfirmWindow`，内容为
  - 最多 8 条路径预览：`· D:\OldApp（检测到用户存档）`
  - 说明「可能包含用户存档、游戏数据、开发项目或配置文件」
  - 复选框文案由 `ProtectionReason` 动态拼成：`我已知悉这些目录包含 用户存档 / 游戏存档，并理解隔离是可恢复的，仍要继续`
  - **未勾选时「确认继续」按钮禁用**（不是禁用后再点一次，是根本点不了）
- **永久删除**：不弹勾选框，直接 `Warning` 告知「Core 会拒绝永久删除并自动跳过，受保护内容只允许可恢复的隔离或回收站」，然后继续走原有的「输入『永久删除』」确认 —— 与 `CleanupService.cs:112` 的拒绝逻辑对齐。

---

## 7. 构建状态

```
dotnet build src/WinCleaner.App/WinCleaner.App.csproj -c Release
已成功生成。0 个警告，0 个错误
```

（Core / Registry / Scanner / Services 均被连带编译通过。）

---

## 8. 已知问题 / 需要 Core 跟进

1. **【P0】`IAssociationIndex` 生命周期语义** —— §3.2。临时解法在 UI 侧，长期请 Core 提供 `ScanOutcome` 或 `LastScanSourceHealth`。
2. **【P0】UI 目前直接调 `ComputeHealthCap`** —— §3.2 末尾。Core 给出 `ConfidenceCap` 后我会删掉。
3. **【P1】「计划任务」`Status` 语义** —— §3.3，需要 Core 给结论。
4. **【P2】`OrphanAssessment` 去留** —— 建议 Core 删除或不启用，避免双真相源（§3.1）。
5. **【P2】`OrphanScanViewModel` 是 `Transient`** —— 每次导航到扫描页会重建 VM，`_lastScanHealth` 随 VM 重建而清空。这与单例索引配合没问题，但如果 Core 后续加"扫描历史/结果持久化"（UDR-02），健康度也应一并持久化，否则历史记录无法解释当时的置信度。

---

## 9. UI 侧下一步（不阻塞 Core）

1. Core 给出 `ConfidenceCap` 后移除 `ComputeHealthCap` 调用。
2. 把健康度状态条复用到 `ScanCompleteView`（扫描完成摘要页），让用户在进结果页之前就看到"结果可能不完整"。
3. `UiStateService.LastScanSummary` 补健康度字段，供概览页展示（依赖 Core UDR-02 或 §8.5）。

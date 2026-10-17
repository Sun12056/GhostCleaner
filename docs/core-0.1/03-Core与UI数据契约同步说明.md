# Core ↔ UI 数据契约同步说明

> 日期：2026-10-16 · Core 线发出 · **请 UI 对话复核并回执**
> 基线：commit `28fb8d2` + Core 0.1 验证环境搭建改动

---

## 0. 一句话原则

**UI 消费 Core 的结论，不重算结论。**
尤其：不能把「没有发现系统关联」改写成「确认是残留」。这两者在 Core 0.1 里是严格不同的东西。

---

## 1. UI 实际消费的类型：`OrphanItem`

`IOrphanScanner.ScanAsync` 返回 `IReadOnlyList<OrphanItem>`。UI 应直接绑定它。

> ⚠️ `Core/Models/OrphanAssessment.cs` 虽然定义了「完整评估结论」的契约模型，但**目前全仓库零引用**，Scanner 并不产出它。
> **待决策（需 UI 回执）**：契约是继续以 `OrphanItem` 为准，还是让 Scanner 额外输出 `OrphanAssessment`？在决策前，UI 请按 `OrphanItem` 开发。

### 1.1 字段与展示要求

| 字段 | 含义 | UI 展示要求 |
|---|---|---|
| `Path` | 目录路径 | 原样展示 |
| `DisplayName` | 推测软件名 | 优先用它，缺失再退回 `Path` 末段 |
| `ConfidenceScore` (0–100) | **多大把握认为它是残留** | 展示为「疑似残留度 65%」 |
| `ConfidenceLevel` | Normal / Suspicious / HighProbability / VeryHighProbability | 阈值 40 / 70 / 90，**Core 已集中定义，UI 不要另设阈值** |
| `RiskScore` (0–100) / `Risk` | **清理它有多危险** | 与置信度**分开展示**，不要合并成一个"危险度" |
| `Recommendation` | Keep / Review / Quarantine / HighRiskReview | 见 §2 |
| `Evidence` | 证据列表（含正负向） | 双向列出；`IsPositive=false` 的是保护依据，不能隐藏 |
| `Warnings` | 扫描不完整 / 判定不可靠 | **必须显式展示**，不能折叠成小灰字 |
| `IsProtected` / `ProtectionReason` | 受保护（用户数据/游戏/开发项目/系统路径） | 明确徽标 + 原因 |
| `IsExcluded` / `ExcludeReason` | 命中白名单 | 明确徽标 + 原因 |
| `DirectoryCategory` | 开发项目 / 游戏 / 用户数据 / 绿色软件 / `-` | 原样展示 |
| `IsRunning` | 扫描时检测到占用进程 | 显著提示 |
| `Reasons` | 人类可读解释（含 `⚠` 前缀的警告） | 直接展示即可 |

### 1.2 语义红线（UI 不得违反）

1. **Confidence 与 Risk 是两件事**。高置信度 ≠ 可以清理；高置信度 + 高风险 → 必须人工复核。
2. **Recommendation 永远不会是 Delete**。Core 的 `CleanupRecommendation` 只有 `Keep / Review / Quarantine / HighRiskReview`。UI 不得自行新增"删除"按钮作为默认动作。
3. **Warnings 非空时，判定不可信**。UI 必须提示"结果可能不完整"，不能让用户在不知情的情况下批量操作。
4. 不要把 `ConfidenceScore` 翻译成"清理优先级"。优先级排序请用 `Recommendation` + `Risk`。

---

## 2. `Recommendation` 的准确含义

| 值 | 含义 | UI 建议措辞 |
|---|---|---|
| `Keep` | 不建议处理（命中白名单 / 关键路径 / 置信度不足） | 「保留」 |
| `Review` | 需要人工复核（受保护 / 高风险 / 高置信度但非低风险） | 「建议人工确认」 |
| `Quarantine` | 置信度非常高（≥90）**且**清理风险为 Low | 「建议隔离（可恢复）」 |
| `HighRiskReview` | 风险为 High，或检测到正在运行的进程 | 「高风险，必须人工确认」 |

注意触发条件（Core `RiskEvaluator.Recommend`）：
- `IsExcluded || IsCriticalPath` → `Keep`
- `Risk == High` → `HighRiskReview`
- `IsRunning` → `HighRiskReview`
- `IsProtected` → `Review`
- 仅当 `VeryHighProbability && Risk == Low` → `Quarantine`

---

## 3. `QuarantineStatus` 枚举：没有 `Deleted`

Core 实际定义（`Core/Models/Enums.cs`）：

```csharp
public enum QuarantineStatus
{
    Quarantined = 0,
    Restored    = 1,
    Purged      = 2,   // 隔离内容已被永久清除（不可恢复）
    Failed      = 3,
}
```

**UI 代码里曾引用 `QuarantineStatus.Deleted`（不存在，导致 `CS0117`）。** 本次构建修复时已代为对齐：

- `src/WinCleaner.App/Converters/Converters.cs`
  - `QuarantineStatus.Deleted => "Brush.Danger"` → `QuarantineStatus.Purged => "Brush.Danger"`
  - 文案 `"已删除"` → `"已清除"`

**请 UI 对话复核这个文案是否合意**（`Purged` 表示隔离内容已被永久移除、不可恢复；也可以改成"已永久清除"）。

---

## 4. 本次为恢复构建而代修的 UI 线文件（请 UI 对话复核）

以下文件**归属 UI 线**，因整解决方案构建被阻塞、经用户授权后由 Core 侧代修。改动均为语法/枚举对齐，未触碰 UI 设计意图：

| 文件 | 问题 | 改动 |
|---|---|---|
| `Icons/IconSet.xaml`、`Styles/Tokens/{Tokens,Theme.Light,Theme.Dark}.xaml`、`Styles/Controls/{Gc.Buttons,Gc.Inputs,Gc.Navigation}.xaml` | XML 注释里 `----------` 含连续 `--`，`MC3000` ×7 | 分隔线统一改为 `==========`（35 处，纯注释文本） |
| `ViewModels/AboutViewModel.cs` | `NotDoPoints = { ... };` 对只读属性用集合初始化器赋值，`CS1525/CS1002/CS1513` ×22 | 改为逐个 `Add(...)` |
| `Converters/Converters.cs` | `QuarantineStatus.Deleted` 不存在 | 对齐 `Purged`，文案「已清除」 |
| `Services/ThemeService.cs` | `Registry.CurrentUser` 被 `WinCleaner.Registry` 命名空间遮蔽，`CS0234` | 改为 `Microsoft.Win32.Registry.CurrentUser` |

---

## 5. 需要 UI 侧跟进的事项

### 5.1【建议】导出报告时带上采集源健康度

`IReportExporter.ExportScanReportAsync` 已新增可选参数：

```csharp
Task<string> ExportScanReportAsync(
    IEnumerable<OrphanItem> items,
    string filePath,
    IReadOnlyList<AssociationSourceHealth>? sourceHealth = null,   // 新增
    CancellationToken cancellationToken = default);
```

不传时，报告的 `associationHealth.note` 会是「未提供采集源健康度，无法判断置信度是否被限制」，用户无法解释"为什么这里只有 65%"。

`OrphanScanViewModel` 目前**没有注入 `IAssociationIndex`**，所以 `SourceHealth` 拿不到。需要 UI 侧：
1. 在构造函数注入 `IAssociationIndex`（或让 `IOrphanScanner` 暴露最后一次扫描的健康度）；
2. 调用处改为 `_exporter.ExportScanReportAsync(Results, path, _associationIndex.SourceHealth)`。

> 若 UI 更希望由 Core 提供"扫描结果 + 健康度"的合并返回类型，请提出需求，Core 侧评估（这也与 §1 的 `OrphanAssessment` 决策相关）。

### 5.2【待决策】受保护内容的隔离确认

已确认产品决策：**受保护内容（`IsProtected == true`）允许在人工明确确认后执行隔离。**
建议 UI 增加到二次确认：展示 `ProtectionReason`，要求用户勾选"我已知悉该目录包含 XXX"后才允许提交隔离。

### 5.3【待确认】是否需要 UI 展示采集源健康度面板

建议在扫描结果页顶部显示一行：
`关联检查：8/9 个来源成功 · 计划任务未完成 · 置信度上限 65`

数据已可由 Core 提供（`IAssociationIndex.SourceHealth`、`OrphanConfidenceEvaluator.ComputeHealthCap`）。真实环境下这个提示很重要 —— D 盘实测中"计划任务"恒为 0，导致所有结果被压到 65，若不提示用户会误以为工具失灵。

---

## 6. Core 侧暂未提供、UI 不要假设存在的东西

- 不存在 `CleanupRecommendation.Delete`
- 不存在 `QuarantineStatus.Deleted`
- 不存在"体检分数""一键优化"类聚合指标（Core 不产出，也不打算产出）
- `OrphanAssessment` 暂未被 Scanner 使用，不要按它建模

---

## 7. 回执请包含

1. §1 `OrphanItem` vs `OrphanAssessment` 的决策
2. §3 「已清除」文案是否采纳
3. §5.1 是否由 UI 注入 `IAssociationIndex`，或需要 Core 提供合并返回类型
4. §5.3 健康度面板是否需要，以及期望的展示形式
5. §4 代修文件是否已复核无异议

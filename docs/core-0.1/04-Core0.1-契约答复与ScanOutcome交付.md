# Core → UI：契约回执答复 + ScanOutcome 交付

> 日期：2026-10-16
> 对应：UI 回执 `docs/ui-0.1/04-Core0.1-契约回执与UI落地.md`
> 基线：commit `28fb8d2` + Core 0.1 验证环境改动 + 本次 ScanOutcome 改动（均未提交）

---

## 一、5 项回执：全部采纳

| # | UI 答复 | Core 结论 |
|---|---|---|
| 1 | 继续 `OrphanItem`，`OrphanAssessment` 暂不启用 | **采纳**。`OrphanAssessment` 保留在 `Core/Models/`，注释已明确它是"完整评估结论"模型；Core 不会在 0.1/0.2 启用它，避免双真相源 |
| 2 | 「已清除」文案采纳 | **采纳**。与 `QuarantineEntry.StatusText` 一致；"永久"语义留在句子里（永久删除确认框），不进状态徽标 |
| 3 | 不做注入式，请求 Core 在扫描输出里带健康度 | **采纳，已交付 `ScanOutcome`**（见第二节） |
| 4 | 健康度面板已落地（顶部状态条：未完成=黄底+⚠，全成功=绿底，下方等宽明细） | **采纳**。数据请改从 `ScanOutcome` 取，别再直调 `ComputeHealthCap` |
| 5 | 4 处代修全部无异议 | 收到 |

---

## 二、交付：`ScanOutcome`（解除阻断项）

### 2.1 新增

| 文件 | 内容 |
|---|---|
| `src/WinCleaner.Core/Models/ScanOutcome.cs` | `Items` + `SourceHealth` + `ConfidenceCap` + `ConfidenceCapNote`；另有纯派生展示属性 `IsConfidenceCapped` / `HealthySourceCount` / `UnhealthySourceCount` / `IsAssociationDataFullyHealthy` / `TotalAssociationRecords` / `UnhealthySourceNames` |
| `src/WinCleaner.Core/Interfaces/IScanner.cs` | `IOrphanScanner` 新增 `ScanWithOutcomeAsync`（**推荐入口**）；`ScanAsync` 保留，改为默认实现转发 |
| `src/WinCleaner.Scanner/OrphanScanner.cs` | 实现 `ScanWithOutcomeAsync`，内部算出 cap 后与条目一起返回；保留实例方法 `ScanAsync` 作兼容包装 |
| `tests/WinCleaner.Tests/ScanOutcomeTests.cs` | 3 个契约测试（新增，测试总数 105 → 108） |

设计要点：**结果与健康度不可分离**。这不只是 API 整洁问题 —— 分离会直接造成"把没观测当成观测到失败"，即 UI 回执里描述的那个 45 分误报。

### 2.2 UI 请改 3 处

```csharp
// 1. 扫描入口
var outcome = await _scanner.ScanWithOutcomeAsync(options, progress, ct);
var items   = outcome.Items;
_lastScanHealth = outcome.SourceHealth;      // 与 items 同批，绝不会是空索引

// 2. 顶部提示条：删掉 ComputeHealthCap 直调与 EvidenceWeights.Default 引用
AssociationHealthText  = outcome.IsAssociationDataFullyHealthy
    ? $"关联检查：{outcome.HealthySourceCount} 个来源全部成功 · 引用 {outcome.TotalAssociationRecords} 条"
    : $"关联检查：{outcome.HealthySourceCount}/{outcome.SourceHealth.Count} 个来源成功"
      + $" · 未完成：{string.Join("、", outcome.UnhealthySourceNames)}"
      + $" · 置信度上限 {outcome.ConfidenceCap}";
HasAssociationHealthIssue = !outcome.IsAssociationDataFullyHealthy;
// 明细行可用 outcome.SourceHealth 直接渲染（语义/配色逻辑不变）

// 3. 导出报告（不变，仍用 _lastScanHealth）
await _exporter.ExportScanReportAsync(Results, path, _lastScanHealth);
```

改完后 `OrphanScanViewModel` 里不应再出现 `OrphanConfidenceEvaluator` 与 `EvidenceWeights`。

### 2.3 关于 `App.xaml.cs` 的 Singleton 改动

**不必回退，建议保留**，但注释理由要更新：

- 现在的正确理由：构建关联索引要读取注册表/服务/进程/文件关联等 9 类来源，成本不低（真机实测 13k+ 条、约 7 秒），单例可避免同一会话内重复构建；`BuildAsync` 先清空再重建、`SourceHealth` 有 `lock`，复用安全。
- 原来的理由（"UI 要读同一个实例的健康度"）已由 `ScanOutcome` 取代，**请从注释里删掉**，否则后来者会误以为它是必需的耦合。
- `OrphanScanViewModel` 的 `IAssociationIndex` 构造依赖可以移除（`ScanOutcome` 已提供健康度）。移除与否由 UI 定；移除后 VM 与索引彻底解耦。

---

## 三、答复：D 盘「计划任务」的状态

### 3.1 结论：**`Partial`，不是 `Success` + 0 条**

证据链：

```
AssociationCollectors.cs:148-155   Directory.EnumerateFiles(tasksDir) 被 catch { return list; } 吞掉
        ↓  返回 0 条
AssociationIndex.cs:88-93          Success 且 buffer.Count == 0  →  改标 Partial
```

### 3.2 但更关键的是：**这个 `Partial` 是对的，不要改成 `Success`**

我在本机实测了根因：

```
C:\WINDOWS\System32\Tasks  存在 = True
Directory.EnumerateFiles(...) → UnauthorizedAccessException："访问被拒绝"
当前进程 runAsAdmin = False
```

即：**非管理员权限下根本读不到计划任务目录**，采集器把异常吞掉、返回空列表。这是一次真实的采集失败，**不是**"确认没有任何计划任务引用"。

如果按 UI 提的"若是 Success+0 条，那是合法的确认无引用"去改，后果是：UI 显示"9/9 全部成功"，而实际上计划任务**一条都没检查** —— 这正是 v0.1 要消灭的"把没观测当成观测到失败"的镜像错误（把失败当成观测到空）。

所以：**保持 `Partial`。UI 现在的黄底 ⚠ 显示是正确的。**

### 3.3 真正要修的是采集器（Core 0.2 候选，不在此轮）

根因是 `AssociationCollectors.cs` 里 21 处 `catch { // 忽略 }` 吞掉了子项异常 —— 即核验报告「已知问题 2」。正确修法不是改状态码，而是让采集器**上报计数**：

```csharp
// 目标形态（Core 0.2）
Source: 计划任务
Status: Failed
ItemsScanned: 0   ItemsFailed: 1   ItemsCollected: 0
Error: 权限不足，无法枚举 C:\WINDOWS\System32\Tasks（建议以管理员身份运行）
```

那时 UI 就能显示「计划任务：失败 —— 权限不足，建议以管理员身份运行」，比笼统的"未完成"准确得多，也让用户知道该做什么。

**在此之前，UI 文案建议**：不要把"未完成"说成"失败"，也不要说成"检查过但没找到"。中性表述如「N 个来源未能完成检查，结果可能不完整」是安全的。

### 3.4 顺带：请以管理员身份跑一轮验证

非管理员下进程模块采集也可能不全。真实验证建议跑两轮对比（普通 / 管理员），这正是 `runAsAdmin` 字段进 JSONL 的原因。

---

## 四、确认 UI 提到的另外两点

1. **「那 2 个 M 是 Core 0.1 预存在的 `IReportExporter` 改动，非我所为」** —— 确认，是我改的：
   `src/WinCleaner.Core/Interfaces/IServices.cs`、`src/WinCleaner.Services/Reports/ReportExporter.cs`（新增可选参数 `sourceHealth`）。UI 线零改动，已按你要求核对。
2. **永久删除路径改为 Warning** —— 正确，与 `CleanupService.cs:112-119` 对齐：受保护内容**硬拒绝**永久删除；但允许人工确认后**隔离**（本次已确认的产品决策）。你的 `ProtectedConfirmWindow` 未勾选即禁用「确认继续」的做法符合预期。

---

## 五、本轮 Core 侧改动与验证

改动（仅 Core / Scanner / Tests / tools）：

| 文件 | 性质 |
|---|---|
| `Core/Models/ScanOutcome.cs` | 新增 |
| `Core/Interfaces/IScanner.cs` | 新增方法 + `ScanAsync` 转默认实现 |
| `Scanner/OrphanScanner.cs` | 实现 `ScanWithOutcomeAsync`，保留 `ScanAsync` 兼容包装 |
| `tests/WinCleaner.Tests/ScanOutcomeTests.cs` | 新增 3 个契约测试 |
| `tools/WinCleaner.Validate/Program.cs` | 改用 `ScanWithOutcomeAsync`，不再自己算 cap |

**验证**

```
dotnet build WinCleaner.sln -c Release   →  0 警告 0 错误
dotnet test                              →  108 通过 / 0 失败（105 → 108）
D 盘真机冒烟                              →  完成来源 8/9，引用总数 13257，置信度上限 65
                                            （与 SourceHealth 一致，ScanOutcome 生效）
```

未调整任何 `EvidenceWeights` 权重，未修改 `RiskEvaluator` / `EvidenceCollector` / `DirectoryFactsDetector` 的任何判定逻辑。

---

## 六、给 UI 的直接待办

1. 扫描入口改 `ScanWithOutcomeAsync`，删除 `ComputeHealthCap` 与 `EvidenceWeights.Default` 直调
2. 更新 `App.xaml.cs` 里 Singleton 的注释理由（见 §2.3）
3. 健康度文案保持中性"未完成"，不要写成"检查过但没找到"或"失败"（见 §3.3）
4. 可选：移除 `OrphanScanViewModel` 的 `IAssociationIndex` 依赖

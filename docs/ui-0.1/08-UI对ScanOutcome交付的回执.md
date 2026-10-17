# UI → Core：ScanOutcome 交付回执（4 项待办全部完成）

> 日期：2026-10-16
> 对应：Core 答复 `docs/core-0.1/04-Core0.1-契约答复与ScanOutcome交付.md` §2.2 / §2.3 / §3.3 / §六
> 范围：仅 `src/WinCleaner.App`（UI 线），未触碰 Core / Scanner / Services / Tests

---

## 一、4 项待办：全部完成

| # | Core 待办 | UI 处理 | 状态 |
|---|---|---|---|
| 1 | 扫描入口改 `ScanWithOutcomeAsync`，删 `ComputeHealthCap` / `EvidenceWeights.Default` 直调 | 已改，改用 `outcome.ConfidenceCap` / `ConfidenceCapNote` / `UnhealthySourceNames` | 完成 |
| 2 | `App.xaml.cs` Singleton 注释理由改为"索引构建成本高"，删掉"UI 要读同一个实例" | 已改，并注明"健康度已由 ScanOutcome 返回" | 完成 |
| 3 | 健康度文案保持中性"未完成" | 已确认：全项目无"失败/检查过但没找到"表述 | 完成 |
| 4 | 可选：移除 `OrphanScanViewModel` 的 `IAssociationIndex` 依赖 | 已移除（构造参数 + 字段 + 唯一使用点） | 完成 |

---

## 二、具体改动

### 1. 扫描入口（`ViewModels/OrphanScanViewModel.cs`）

```csharp
// 之前
var items = await _scanner.ScanAsync(options, progress, _cts.Token);
_lastScanHealth = _associationIndex.SourceHealth;      // ← 生命周期假设，危险
RefreshAssociationHealth();                            // ← 内部直调 ComputeHealthCap

// 现在
var outcome = await _scanner.ScanWithOutcomeAsync(options, progress, _cts.Token);
var items   = outcome.Items;
_lastScanHealth = outcome.SourceHealth;                 // 与 items 同批
RefreshAssociationHealth(outcome);                      // 只读 outcome 上的派生属性
```

`RefreshAssociationHealth()` 的签名改为 `RefreshAssociationHealth(ScanOutcome outcome)`，内部不再出现任何判定逻辑：

```csharp
HasAssociationHealthIssue = !outcome.IsAssociationDataFullyHealthy;

AssociationHealthText = outcome.IsAssociationDataFullyHealthy
    ? $"关联检查：{outcome.HealthySourceCount} 个来源全部成功 · 引用 {outcome.TotalAssociationRecords} 条"
    : $"关联检查：{outcome.HealthySourceCount}/{health.Count} 个来源成功"
      + $" · 未完成：{string.Join("、", outcome.UnhealthySourceNames)}"
      + $" · 结果可能不完整 · 置信度上限 {outcome.ConfidenceCap}";
```

逐来源明细行仍用 `outcome.SourceHealth` 渲染（`SourceName · StatusText · ItemCount 条 · ErrorMessage`），语义与配色逻辑不变。
导出报告仍传 `_lastScanHealth`，按 Core §2.2 第 3 条保持不变。

验收：全 `WinCleaner.App` 目录搜 `ComputeHealthCap` / `EvidenceWeights` / `OrphanConfidenceEvaluator` → **0 命中**；`using WinCleaner.Scanner;` 随之变为无用，已删除。

### 2. `App.xaml.cs` 的 Singleton 注释

```csharp
// 单例：构建关联索引要读取注册表/服务/进程/文件关联等 9 类来源，
// 成本不低（真机实测 13k+ 条、约 7 秒），单例可避免同一会话内重复构建。
// OrphanScanner 每次扫描都会 BuildAsync() 先清空再重建，SourceHealth 有 lock，复用安全。
// 注意：这不为"UI 要读同一个实例的健康度"—— 健康度已由 ScanOutcome 随扫描结果一起返回，
// UI 侧（OrphanScanViewModel）已不再依赖 IAssociationIndex。
services.AddSingleton<IAssociationIndex, AssociationIndex>();
```

Singleton 本身按 Core 建议**保留未回退**。

### 3. 健康度文案

| 场景 | 文案 |
|---|---|
| 全部成功 | `关联检查：9 个来源全部成功 · 引用 13257 条`（绿底，无 ⚠） |
| 有来源未完成 | `关联检查：8/9 个来源成功 · 未完成：计划任务 · 结果可能不完整 · 置信度上限 65`（黄底 + ⚠） |
| 极端：健康度为空 | `关联检查：未获取到采集源状态，结果可能不完整` |

全项目检索确认**没有**"失败""检查过但没找到""已确认无引用"类表述。
明细行里的 `StatusText` 由 Core 的 `AssociationSourceText.ToText` 提供（`成功/失败/未完成/未运行`），UI 不另写一份 —— 这条也顺带确认了"集中管理，避免 UI 与算法各写一份"。

### 4. 移除 `IAssociationIndex` 依赖

- 删除字段 `private readonly IAssociationIndex _associationIndex;`
- 删除构造参数与赋值
- 全项目确认构造点只有 DI：`services.AddTransient<OrphanScanViewModel>()` + `_services.GetRequiredService<OrphanScanViewModel>()`，无手写 `new`，移除安全
- 现在 VM 与关联索引**彻底解耦**，健康度唯一来源是 `ScanOutcome`

---

## 三、对「计划任务 = Partial」结论的确认

接受，且**不改状态码**。补充一条 UI 侧的对应做法，请 Core 确认是否与 Core 0.2 的方向一致：

- UI 顶部提示对未完成来源只说"未完成 + 结果可能不完整"，不解释原因（原因在明细行由 `ErrorMessage` 呈现，当前为 null 所以不显示）。
- 因此现在用户看到的是「计划任务 · 未完成 · 0 条」，没有"建议以管理员身份运行"的提示。**这确实不够**（用户不知道该做什么），但按 §3.3 的判断，等采集器上报 `ItemsScanned/ItemsFailed/Error` 后再显示比现在编一句更好。
- 另有一处已在 UI 侧覆盖：扫描配置页在未提权时显示黄底横幅「未以管理员身份运行…判定会偏保守」。所以它并非完全无提示，只是没精确到"计划任务"这一个来源。

若 Core 0.2 让采集器上报 `Error`，UI 这边只需把明细行从「`ErrorMessage`」扩到「`ItemsScanned/ItemsFailed` + `Error`」，并把 `Error` 提升到顶部提示，改动很小。

---

## 四、UI 侧待 Core 确认的 0 项 / 待办

无新增对 Core 的请求。UDR-01/02/03/05 的降级状态不变，等在 Core 0.2。

---

## 五、本轮 UI 改动清单

| 文件 | 性质 |
|---|---|
| `src/WinCleaner.App/ViewModels/OrphanScanViewModel.cs` | 改（扫描入口、健康度刷新、移除 `IAssociationIndex`、删 `using WinCleaner.Scanner`） |
| `src/WinCleaner.App/App.xaml.cs` | 改（仅 Singleton 注释） |

UI 线未触碰任何判定逻辑、未调整权重、未修改 Core / Scanner / Services / Tests。
本机无 .NET SDK，无法编译验证；已做静态检索确认（`ComputeHealthCap`/`EvidenceWeights`/`OrphanConfidenceEvaluator`/`IAssociationIndex` 在 App 侧均为 0 命中，除注释）。

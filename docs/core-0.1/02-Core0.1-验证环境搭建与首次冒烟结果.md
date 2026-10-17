# Core 0.1 验证环境搭建记录 + D 盘首次冒烟结果

> 日期：2026-10-16
> 对应文档：`01-Core0.1-只读核验报告与真实环境验证计划.md` 的第 6 节「最小必要改进清单」阻塞项 B1 / B2 / B3
> 基线：commit `28fb8d2` + 本次改动（未提交）

---

## 1. 本次改动清单

### B1 恢复解决方案构建（UI 线文件，获授权后代修）

| 文件 | 问题 | 改动 |
|---|---|---|
| `src/WinCleaner.App/Icons/IconSet.xaml` | XML 注释含连续 `--`（`<!-- ---------- 导航 ---------- -->`），`MC3000` | 7 个文件中的 `----------` 分隔线全部改为 `==========`（共 35 处，纯注释文本，零逻辑影响） |
| `src/WinCleaner.App/Styles/Tokens/Tokens.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/Styles/Tokens/Theme.Light.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/Styles/Tokens/Theme.Dark.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/Styles/Controls/Gc.Navigation.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/Styles/Controls/Gc.Buttons.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/Styles/Controls/Gc.Inputs.xaml` | 同上 | 同上 |
| `src/WinCleaner.App/ViewModels/AboutViewModel.cs` | `NotDoPoints = { ... };` / `SafetyPoints = { ... };` —— 对只读自动属性使用集合初始化器赋值，非法 C#（`CS1525/CS1002/CS1513`，22 个错误） | 改为逐个 `Add(...)`，行为完全等价 |
| `src/WinCleaner.App/Converters/Converters.cs` | 引用了不存在的 `QuarantineStatus.Deleted`（`CS0117` ×2） | 对齐到 Core 实际枚举 `QuarantineStatus.Purged`，状态文本改为「已清除」 |
| `src/WinCleaner.App/Services/ThemeService.cs` | `Registry.CurrentUser` 被 `WinCleaner.Registry` 命名空间遮蔽（`CS0234`） | 改为完全限定 `Microsoft.Win32.Registry.CurrentUser` |

> **说明**：以上 3 处 C# 问题在 XAML 修复前被 `MC3000` 掩盖，是修复后新暴露出来的。它们全部位于 UI 线文件中，Core 侧未做任何逻辑改动。已同步记录到 `03-Core与UI数据契约同步说明.md` 供 UI 对话复核。

### B2 新增只读验证入口

| 文件 | 说明 |
|---|---|
| `tools/WinCleaner.Validate/WinCleaner.Validate.csproj` | 新增控制台项目，`net8.0-windows`。**只引用 Core / Registry / Scanner，刻意不引用 `WinCleaner.Services`** —— 从工程结构上保证 `CleanupService` / `QuarantineManager` 编译期不可达 |
| `tools/WinCleaner.Validate/Program.cs` | 命令行入口，输出 `docs/core-0.1/validation/label-schema-v1.json` 定义的 JSONL |
| `WinCleaner.sln` | 已加入（`tools` 解决方案文件夹） |
| `.gitignore` | 追加 `*.path-map.local.json`、`validation/raw/` |

能力：
- `--roots <p;p>` 必填；`--out`、`--class`、`--max-depth`、`--include-all`、`--follow-reparse-points`、`--limit`、`--no-redact`、`--commit`
- `--include-all`：绕过 `OrphanScanner` 的 `IsLikelySoftwareDirectory` 过滤，让空目录 / 用户数据目录这类**负样本**也进入回标集合（TN 统计必需）
- 默认脱敏：用户名段替换为 `<USER>`，真实路径另存 `*.path-map.local.json`（已 gitignore）
- 控制台打印采集源健康度表 + 本次置信度上限 + 置信度 Top15
- 退出码：0 成功 / 1 失败 / 2 参数错误
- 启动即打印 `READ-ONLY MODE`

### B3 报告补 `AssociationSourceHealth`

| 文件 | 改动 |
|---|---|
| `src/WinCleaner.Core/Interfaces/IServices.cs` | `IReportExporter.ExportScanReportAsync` 增加可选参数 `IReadOnlyList<AssociationSourceHealth>? sourceHealth = null`（可选参数，现有 2 参调用点不受影响） |
| `src/WinCleaner.Services/Reports/ReportExporter.cs` | JSON 报告新增顶层 `associationSourceHealth`（逐来源明细）+ `associationHealth`（概览：来源数 / 未完成数 / 总记录数 / 未完成的来源名 / 人类可读 Note）；CSV 新增「关联源健康度」列 |

`Services` 不依赖 `Scanner`，所以概览只做统计、不复算置信度上限（上限逻辑仍唯一存在于 `OrphanConfidenceEvaluator.ComputeHealthCap`）。

### 文档

- `docs/core-0.1/validation/label-schema-v1.json`：`sampleId` 允许 `U-` 前缀、`sampleClass` 增加 `U`、`groundTruth` / `groundTruthConfidence` 允许 `null`（未回标状态）、`core` 增加 `isRunning`

---

## 2. 构建与测试结果

```
dotnet build WinCleaner.sln -c Release --nologo
  → 已成功生成。0 个警告，0 个错误      （含新增 WinCleaner.Validate）

dotnet test tests/WinCleaner.Tests/WinCleaner.Tests.csproj -c Release
  → 已通过!  失败 0，通过 105，已跳过 0，总计 105（2s）
```

> 环境提醒：`PATH` 里的 `C:\Program Files\dotnet\dotnet.exe` **没有 SDK**。必须在命令前加：
> ```powershell
> $env:DOTNET_ROOT = 'D:\DevTools\dotnet'
> $env:PATH = 'D:\DevTools\dotnet;' + $env:PATH
> ```

---

## 3. D 盘首次冒烟（只读，未修改任何被扫描内容）

```
WinCleaner.Validate --roots "D:\" --out validation\raw\smoke1.jsonl --limit 12
```

**采集源健康度（真实数据）**

| 来源 | 状态 | 条数 |
|---|---|---|
| 注册表卸载项 | 成功 | 93 |
| App Paths | 成功 | 54 |
| 开始菜单/桌面快捷方式 | 成功 | 18 |
| Windows 服务 | 成功 | 658 |
| **计划任务** | **不完整** | **0** |
| 正在运行的进程 | 成功 | 186 |
| Microsoft Store 应用 | 成功 | 151 |
| 文件关联/右键菜单/COM | 成功 | 12084 |
| 启动项 | 成功 | 4 |

合计 13248 条；8/9 来源成功，耗时 7.7s。候选目录 66 个，分析 47 个。

**置信度 Top 12**

| ID | 置信 | 等级 | 风险 | 建议 | 路径 |
|---|---|---|---|---|---|
| U-001 | 65 | Suspicious | High | HighRiskReview | `D:\Weixin` |
| U-002 | 65 | Suspicious | Medium | Review | `D:\wangyiyunyinyue` |
| U-003 | 65 | Suspicious | Medium | Review | `D:\Registry Repair` |
| U-004 | 61 | Suspicious | Medium | Review | `D:\T20` |
| U-005 | 61 | Suspicious | Medium | Review | `D:\4173-2025-03-28021420-...` |
| U-006 | 54 | Suspicious | High | HighRiskReview | `D:\IROK Software` |
| U-007 | 42 | Suspicious | High | HighRiskReview | `D:\Microsoft VS Code` |
| U-008 | 37 | Normal | High | HighRiskReview | `D:\Acrobat-PRO-DC-2026...-m0nkrus` |
| U-009 | 24 | Normal | High | HighRiskReview | `D:\draw.io` |
| U-010 | 21 | Normal | Low | Keep | `D:\UsbEAm Hosts Editor` |
| U-011 | 19 | Normal | High | HighRiskReview | `D:\Tools\jdk-21` |
| U-012 | 11 | Normal | High | HighRiskReview | `D:\keke` |

---

## 4. 冒烟暴露的真实环境问题（重要）

### 4.1 「计划任务」采集恒为 0 → 全局置信度被压到 65

真实机器上 `AssociationCollectors.ScheduledTasks` 返回 **0 条**，`AssociationIndex` 按 §已知规则把它标成 `Partial`，进而触发 `MaxConfidenceWhenAnySourceFailed = 65`。

直接后果：**本次扫描中没有任何一个目录能达到 `HighProbability`（≥70）**，即使它确实是残留。这与核验报告 §4.10 预判完全一致，且合成测试根本测不出来。

这是当前**最优先的真实环境问题**，它同时压低召回（FN 上升）。需先判断：
- 采集器是真的失败了（`schtasks` / 任务 XML 解析异常），还是这台机器确实没有可采集的计划任务？
- 若为后者，`0 条 → Partial` 的判定口径需要细化（Core 0.2 候选，优先级取决于 M9 指标）。

> 注意：压低置信度是**安全方向**的失败，不会造成误删。但它会让工具在真实环境下几乎不产生"建议隔离"的结论，可用性受损。

### 4.2 `MissingScheduledTask` 证据被抑制是正确的

`D:\Weixin` 的证据列表里有 `AssociationSourceFailed(-8)` + 2 条 Warning，而**没有** `MissingScheduledTask` 正向证据 —— 说明 §3 第 10 条（`AddMissing` 仅在来源 `Success` 时生成"缺失"证据）在真实环境下确实生效。这是核验中最关键的一条安全属性，已在真机得到验证。

### 4.3 白名单覆盖不足（已知问题 1 的真机确认）

- `D:\Microsoft VS Code` 未命中内置白名单（名单里有 `visual studio` / `microsoft visual studio`，但没有 `vs code`），置信度 42。虽然最终 `HighRiskReview` 不会误删，但说明白名单仍需补充。
- `D:\Tools\jdk-21` 同理（`java` / `jdk` 不在名单，名单只有 `python` / `nodejs` / `git` / `maven` / `gradle`）。
- `D:\Acrobat-PRO-DC-...-m0nkrus` 是**安装包/破解版目录**，置信度 37 / Risk High → `HighRiskReview`，行为合理。

### 4.4 未以管理员运行

`environment.runAsAdmin = false`。进程模块采集（186 条）在非管理员下可能漏掉部分进程，`G 类（运行中软件）`样本的判定会偏保守。建议正式验证时**用管理员权限跑一轮**，两轮对比。

---

## 5. 下一步：正式跑验证

### 5.1 全量扫描（建议先跑一轮不带 `--include-all`）

```powershell
cd d:\文档\GitHub\GhostCleaner
$env:DOTNET_ROOT = 'D:\DevTools\dotnet'
$env:PATH = 'D:\DevTools\dotnet;' + $env:PATH
dotnet build WinCleaner.sln -c Release

tools\WinCleaner.Validate\bin\Release\net8.0-windows\WinCleaner.Validate.exe `
  --roots "D:\" --out validation\raw\run-full.jsonl --commit (git rev-parse --short HEAD)
```

再以管理员身份重复一次，输出到 `run-full-admin.jsonl`，用于对比 §4.4。

### 5.2 负样本必须单独跑（`--include-all`）

`OrphanScanner` 默认只输出"像软件目录"的项，TN 无法统计。对 J 类（空目录/普通文件夹/下载目录）和部分 F 类，用：

```powershell
... --roots "D:\Downloads" --class J --include-all
... --roots "D:\SomeEmptyFolder" --class J --include-all
```

### 5.3 人工回标

打开 `validation/raw/*.jsonl`，为每条填 `groundTruth` / `groundTruthConfidence` / `isActuallyInUse` / `humanNote`，另存为 `validation/labels.jsonl`（可入库；原始 run 文件已在 `validation/raw/` 被 gitignore）。

### 5.4 统计

按 `01-...验证计划.md` §5.4 计算 TP/FP/TN/FN 与 M1–M9。

---

## 6. 已确认的产品决策（来自本次授权）

1. 受保护内容（`IsProtected == true`）**允许**在人工明确确认后执行隔离（对应核验报告 N8）。
2. 验证机器为 D 盘。
3. UI 契约与 UI 对话同步（见 `03-Core与UI数据契约同步说明.md`）。

## 7. 未做的事

- 未调整任何 `EvidenceWeights` 权重
- 未修改 Scanner / RiskEvaluator / AssociationIndex 的任何判定逻辑
- 未执行任何清理、隔离、删除、注册表写入
- 未提交任何 commit（工作区仍保留 UI 线的未提交改动）

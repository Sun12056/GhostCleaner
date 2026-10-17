# GhostCleaner Core 0.1 — 只读核验报告 + 真实 Windows 验证计划

> 生成时间：2026-10-16
> 执行范围：**只读核验 + 方案设计**，未修改任何业务代码
> 仓库：`d:\文档\GitHub\GhostCleaner`

---

## 0. 结论摘要

| 项目 | 结果 |
|---|---|
| 分支 / HEAD | `master` @ `28fb8d2`（Merge remote-tracking branch 'origin/master'，2026-10-07） |
| Core 0.1 落地位置 | 提交 `8e0c8e9`（feat(v0.1): 重构软件残留检测核心…），**已提交**，不在工作区 |
| 工作区改动 | 15 个 `M` + 49 个 `??`，**全部属于 UI 线**（`src/WinCleaner.App/**`、`docs/ui-0.1/**`）。Core / Scanner / Registry / Services 无任何未提交改动 |
| `dotnet build WinCleaner.sln -c Release` | **失败**：7 个 error，全部在未跟踪的 UI XAML 文件（XML 注释含 `--`） |
| Core 五项目编译 | **全绿，0 警告**（Core / Registry / Services / Scanner / Tests） |
| `dotnet test` | **105 passed / 0 failed / 0 skipped**，2s |
| 交接文档 ↔ 代码一致性 | **高**。模型、阈值、Footprint 权重、健康度上限、AddMissing 前置检查、Confidence/Risk 分离、Recommendation 不含 Delete，均与文档描述一致 |
| 真实样本集 | **不存在**。全部 105 个测试为合成样本（手工构造 `SoftwareDirectoryInfo`），无真实文件系统/注册表采集 |
| 新发现阻塞项 | 1 个（解决方案无法整体构建）+ 1 个（无可用验证入口：无任何 CLI/工具能脱离 WPF 跑一次真实扫描并导出报告） |

**关键判断**：交接文档记录的「0 warnings 0 errors」已**过期**——UI 线新增的 XAML 破坏了整解决方案构建。Core 自身代码健康，但**没有任何可执行路径能在真实 Windows 上产出一份带健康度的评估报告**，因此"真实环境验证"目前无法启动，需先补一个最小验证入口。

---

## 1. 仓库与工作区核验

### 1.1 Git 状态

```
* master  28fb8d2 [origin/master] Merge remote-tracking branch 'origin/master'
8e0c8e9 feat(v0.1): 重构软件残留检测核心，建立 Evidence/Confidence/Risk/Recommendation 模型
888414a chore: 项目整体迁移到 D:\文档\GitHub\GhostCleaner
```

- 已修改（15）：`App.xaml(.cs)`、`MainWindow.xaml(.cs)`、`Converters.cs`、6 个 ViewModel、5 个 View XAML —— **全部 UI 层**
- 未跟踪（49）：`docs/ui-0.1/**`（设计与原型，30 项）、`src/WinCleaner.App/Icons|Services|Styles|ViewModels|Views`（19 项）—— **全部 UI 层**

> 结论：Core 0.1 的工作已随 `8e0c8e9` 入库；本次核验的 Core 源码即 HEAD 状态，不存在"日志与 HEAD 不一致"的风险。**未覆盖任何用户未提交改动。**

### 1.2 目录结构

```
src/WinCleaner.Core      (38 .cs)   Interfaces / Models / Utils
src/WinCleaner.Scanner   (13 .cs)   OrphanScanner, RiskEvaluator, EvidenceCollector, ...
src/WinCleaner.Registry  ( 8 .cs)   AssociationIndex, AssociationCollectors, InvalidRegistryScanner
src/WinCleaner.Services  (13 .cs)   CleanupService, QuarantineManager, ReportExporter, ...
src/WinCleaner.App       (106 .cs)  ← UI 线专属
tests/WinCleaner.Tests   (11 .cs)   105 个测试
```

---

## 2. 构建与测试核验

### 2.1 环境差异（必须记录，否则复现会失败）

`PATH` 中第一个 `dotnet` 是 `C:\Program Files\dotnet\dotnet.exe`，它**只有 runtime（8.0.31），没有 SDK**，直接执行会报 `No .NET SDKs were found`。

真实 SDK 在 **`D:\DevTools\dotnet`**（SDK 8.0.425）。可复现命令：

```powershell
Set-Location -LiteralPath 'd:\文档\GitHub\GhostCleaner'
$env:DOTNET_ROOT = 'D:\DevTools\dotnet'
$env:PATH = 'D:\DevTools\dotnet;' + $env:PATH

dotnet build WinCleaner.sln -c Release --nologo
dotnet test tests/WinCleaner.Tests/WinCleaner.Tests.csproj -c Release --nologo
```

### 2.2 构建结果

```
WinCleaner.Core     -> bin/Release/net8.0-windows/WinCleaner.Core.dll
WinCleaner.Registry -> ...  成功
WinCleaner.Services -> ...  成功
WinCleaner.Scanner  -> ...  成功
WinCleaner.Tests    -> ...  成功
WinCleaner.App      -> error MC3000 × 7
    0 个警告    7 个错误
```

7 个错误全部形如：

```
src/WinCleaner.App/Icons/IconSet.xaml(16,10): error MC3000:
  “An XML comment cannot contain '--', and '-' cannot be the last character.”
```

涉及文件（**均为 UI 线未跟踪文件**）：
`Icons/IconSet.xaml:16`、`Styles/Controls/Gc.Buttons.xaml:11`、`Gc.Inputs.xaml:10`、`Gc.Navigation.xaml:21`、`Styles/Tokens/Theme.Dark.xaml:11`、`Theme.Light.xaml:9`、`Tokens.xaml:12`

根因示例（`IconSet.xaml:16`）：`<!-- ---------- 导航 ---------- -->` —— XML 注释中不允许出现连续 `--`。

> 这是**纯语法问题，非逻辑问题**。修复方式：把 `-----` 分隔线改成 `=====` 或其它非 `--` 字符。**但按 Core/UI 分工，这些文件归 UI 线所有，Core 侧不应擅自修改**，需 UI 对话修复或用户授权。

### 2.3 测试结果

```
已通过! - 失败: 0，通过: 105，已跳过: 0，总计: 105 (2s)
```

测试构成：`CommandLinePathParserTests`、`DirectoryFactsDetectorTests`、`HeuristicsTests`、`OrphanDetectionScenariosTests`（8 类验收场景）、`OrphanScannerIntegrationTests`、`PathUtilsTests`、`QuarantineManagerTests`、`ReportAndLogTests`、`RiskEvaluatorTests`、`SoftwareDirectoryInspectorTests`。

**全部为合成样本**：直接用对象初始化器构造 `SoftwareDirectoryInfo`（如 `Path = @"D:\SteamLibrary\steamapps\common\Game"`），配合 `StubAssociationIndex`（`TestDoubles.cs`）精确控制每个来源的 `Success/Partial/Failed`。**没有一处读取真实磁盘或真实注册表。** 这符合"单元/集成测试确定性"的设计目标，但也意味着：**105 个测试通过 ≠ 真实环境检测准确率达标。**

---

## 3. Core 0.1 交付项逐条核验

| # | 交接文档声明 | 核验结果 | 证据 |
|---|---|---|---|
| 1 | 11 个新模型文件 | ✅ 全部存在 | `src/WinCleaner.Core/Models/`：`Evidence/EvidenceType/EvidenceSeverity/OrphanConfidenceLevel/CleanupRecommendation/EvidenceWeights/OrphanAssessment/AssociationSourceStatus/AssociationSourceHealth/DirectoryFacts/QuarantineContext` |
| 2 | 阈值 40 / 70 / 90 | ✅ 一致 | `OrphanConfidenceLevel.cs:25-27` |
| 3 | Scanner 新增 4 个组件 | ✅ 存在 | `DirectoryFactsDetector.cs`、`EvidenceCollector.cs`、`OrphanConfidenceEvaluator.cs`、`ExclusionProviders.cs` |
| 4 | `IPathExclusionProvider` + `CompositeExclusionProvider` | ✅ 存在 | `Core/Interfaces/IExclusionProvider.cs:7,16` |
| 5 | 索引健康状态 `SourceHealth` / `IsSourceUsable` / `IsFullyHealthy` / `UnhealthySources` | ✅ 全部存在 | `Core/Interfaces/IAssociationIndex.cs:23,26,34,37` |
| 6 | 零条记录标记为 `Partial` 而非 `Success` | ✅ 已实现 | `Registry/AssociationIndex.cs:88-93` |
| 7 | RiskEvaluator 分离 Confidence / Risk / Recommendation | ✅ 已实现 | `Scanner/RiskEvaluator.cs:39-43`，`Risk` 评分入口 `EvaluateCleanupRisk:116` 与置信度完全独立 |
| 8 | OrphanScanner 多阶段流水线 | ✅ 已实现 | `Scanner/OrphanScanner.cs:30-130`；并行度仍是 `Math.Clamp(ProcessorCount/2,1,4)`（:80） |
| 9 | Footprint 权重 0.35/0.15/0.10/0.15/0.10/0.15，下限 0.15 | ✅ 完全一致 | `EvidenceWeights.cs:110-118`；`ComputeFootprint` 见 `EvidenceCollector.cs:65-77` |
| 10 | `AddMissing` 仅在来源 `Success` 时生成"缺失"证据 | ✅ 一致 | `EvidenceCollector.cs:243-249`：`IsSourceReliable` 失败 → 写 Warning 并 return，不生成确定性结论 |
| 11 | 健康度上限 55 / 65 / 45 | ✅ 一致 | `EvidenceWeights.cs:95-101`；`OrphanConfidenceEvaluator.ComputeHealthCap:29-73` |
| 12 | Recommendation 不含 Delete | ✅ 一致 | `Models/CleanupRecommendation.cs` 仅 Keep/Review/Quarantine/HighRiskReview；`RiskEvaluator.Recommend:194-215`，`risk==High` 或 `isRunning` 直接 `HighRiskReview` |
| 13 | Quarantine 清单记录判断上下文 | ✅ 一致 | `QuarantineEntry` 含 `Reason/ConfidenceScore/ConfidenceLevel/Risk/Recommendation/Warnings` |
| 14 | 受保护内容禁止永久删除 | ✅ 已实现 | `CleanupService.cs:112-119` |
| 15 | 回收站失败不退化为永久删除 | ✅ 已实现 | `CleanupService.cs:140-148`，失败即计 Failed 并 continue |
| 16 | 永久删除需确认文本 | ✅ 已实现 | `CleanupService.cs:30,66-67`，须等于 `"永久删除"` |
| 17 | `InvalidRegistryScanner.IsLocationVerifiable()` | ✅ 存在 | `Registry/InvalidRegistryScanner.cs:267-275`，用于卸载项/服务/启动项/文件关联四类 |
| 18 | 报告导出含 Evidence/Confidence/Recommendation | ✅ 存在（有缺口，见 §4.6） | `Services/Reports/ReportExporter.cs:58-105` |

**结论**：交接文档第"四"节描述的实现，与 HEAD 代码逐条吻合，未发现"文档写了但代码没有"的情况。

---

## 4. 核验中新发现的问题

按严重程度排序。**本节仅为事实陈述，未做任何修改。**

### 4.0 【阻塞】整解决方案无法构建
见 §2.2。UI 线 XAML 的 XML 注释语法错误导致 `WinCleaner.App` 编译失败。影响：无法启动真实扫描、无法产出真实报告 → **真实环境验证阶段被完全阻塞**。

### 4.1 【阻塞】没有脱离 WPF 的验证入口
仓库中能触发 `OrphanScanner.ScanAsync` 的唯一入口是 WPF 应用。而 WPF 应用当前编译失败；即使修好，它也耦合了 UI 状态、对话框与管理员权限。
**真实验证需要一个纯只读的命令行入口**：给定根目录 → 跑 `OrphanScanner` + `AssociationIndex` → 导出 JSONL（含 Evidence / Confidence / Risk / Recommendation / Warnings / SourceHealth / IsProtected / DirectoryCategory），不做任何删除。目前不存在。

### 4.2 【P0】隔离跨卷失败无回滚 → 数据半移动且不可见
`QuarantineManager.cs:217-227`：跨卷（或 `Directory.Move` 抛 `IOException`）时走「复制 + 删源」；若 `Delete(source)` 失败则直接抛异常，**已复制的副本留在隔离区，源目录也仍在**，而 manifest 在移动之后才写入（:116）→ 该条目在 UI 上不可见、`PurgeExpired/Clear` 也清理不到 → **空间泄漏 + 用户找不到自己的文件**。

### 4.3 【P0】隔离 manifest 无 Hash、无 schema 版本
`QuarantineEntry` 只有 `SizeBytes/FileCount`，无内容校验值、无清单版本号。无法发现"隔离期间文件被篡改"，跨版本升级反序列化风险未知。

### 4.4 【P1】JSON 报告缺 `AssociationSourceHealth`
`ReportExporter.cs:58-105` 导出了 Evidence/Confidence/Recommendation，**但没有导出采集源健康度**。这直接破坏 Core 0.1 最重要的可解释性承诺：看到 `ConfidenceScore=55` 却无法从报告里得知"是因为注册表采集失败被压到上限"。CSV 导出（:24-51）更缺 Evidence / ConfidenceScore / Associations。**真实验证阶段必须能拿这个字段做人工回标**（见 §5.3）。

### 4.5 【P1】注册表备份失败仍继续删除
`InvalidRegistryScanner.cs:311-316, 367-407`：`reg export` 失败仅记 `RegFile = null`，仍继续执行删除。违反"先备份后删除"的安全前提。该模块文档已声明为 Beta，但此行为属于**安全边界被突破**，不只是精度问题。

### 4.6 【P1】manifest 损坏/缺失条目被静默丢弃
`QuarantineManager.cs:236-249` 反序列化异常被吞掉返回 `null`；`ListAsync:56-57` 静默跳过 → 条目占空间但不可见、不可恢复、不可清理。

### 4.7 【P2】语义不一致：`IsUsable` vs `IsSourceReliable`
`AssociationSourceHealth.IsUsable` 认为 `Success || Partial` 可用（`AssociationSourceHealth.cs:29`）；而 `EvidenceCollector.IsSourceReliable` 只认 `Success`（`EvidenceCollector.cs:258-259`）。当前行为是"更保守"，是正确的；但两处语义定义冲突，后续维护容易误改成更激进的版本。**建议加注释锁定语义，或在真实验证后统一口径。**

### 4.8 【P2】`DirectoryHelper.Delete` 可能误报成功
`DirectoryHelper.cs:78`（路径不存在返回 true）、`:102-120`（"最后手段"逐文件删除时忽略被占用文件后仍返回 true）。配合 `CleanupService.cs:153`，永久删除可能报告成功但实际残留。

### 4.9 【P2】受保护内容在"可恢复动作"下仍被执行
`CleanupService.cs:121-125`：`IsProtected` 项在做 Quarantine 时**仍会执行**，只写一条警告日志，无二次确认。与"保护优先级高于覆盖率"的原则有张力。

### 4.10 【P2】零记录来源一律判 `Partial`，真机可能产生大量噪音 Warning
`AssociationIndex.cs:88-93` 把 `buffer.Count == 0` 全部标 `Partial`。在真实环境中"计划任务/启动项确实没有任何指向软件目录的记录"是可能的正常情况 → 会产生大量"未能完成检查"Warning，并触发 `MaxConfidenceWhenAnySourceFailed = 65` 的全局上限，可能**系统性压低所有真实残留的置信度**。这一点在合成测试中完全测不出来，必须在真实验证中重点观察（见 §5.4 指标 M5）。

### 4.11 【P3】`IsLocationVerifiable` 只校验盘符存在
`InvalidRegistryScanner.cs:267-275`：`Directory.Exists(driveRoot)` 为 false 才判不可验证。无法区分"盘不存在"与"权限不足（ACL 拒绝）"。

### 4.12 【P3】`OrphanAssessment` 是孤儿类型
`Core/Models/OrphanAssessment.cs` 定义了 UI 数据契约（含 `PositiveEvidence/NegativeEvidence/ConfidenceText` 等便捷属性），但**全仓库无任何引用**（grep 仅命中自身定义文件）。Scanner 实际返回的是 `OrphanItem`。这意味着 §9 要求的"围绕 OrphanAssessment 形成稳定契约"目前**并未真正落地**——UI 消费的是 `OrphanItem`。

### 4.13 【P3】白名单仍为路径段精确匹配
`ExclusionProviders.cs:68-82` 确认：按目录片段 `Ordinal` 精确匹配，`Steam` 不自动匹配 `SteamLibrary`（靠显式列出 `steamlibrary/steamapps` 覆盖）。与文档"已知问题 1"一致，未恶化。

---

## 5. 真实 Windows 验证计划（Core 0.1 Real-world Validation）

### 5.1 目标与安全边界

**目标**：回答一个问题 —— *GhostCleaner 能否安全地区分「残留软件」「正常软件」「用户数据」？*

**不是**目标：追求高召回率、追求扫描结果数量、追求 Core 0.2 新功能。

**硬边界（任何情况下不得跨越）**：
1. 验证过程**只扫描、只导出报告**，不执行任何清理/隔离/删除。
2. 不修改用户机器上的任何软件数据、注册表项。
3. 不在验证阶段调整权重去迎合结果；权重调整必须在回标统计完成后、且附带回归测试。
4. 无法确认真值的样本单列，不强行计入准确率分母。

### 5.2 测试样本分类（A–J）

每类给出建议**最小样本数**与采集方式。合计目标 **≥ 60 个样本**。

| 代号 | 类别 | 建议数量 | 采集方式 |
|---|---|---|---|
| A | Windows 重装/重置后遗留的软件目录（D/E 盘上的旧 `Program Files`、旧安装目录） | 10 | 找一台重装过的机器；或人工构造：在系统盘装小工具 → 删除卸载项与快捷方式 → 保留目录 |
| B | 正常安装且注册信息完整的软件 | 10 | `C:\Program Files\*`、开始菜单有快捷方式、注册表有卸载项 |
| C | Portable / 绿色软件 | 6 | 解压即用的工具（含/不含 `portable` 命名两种） |
| D | 游戏平台与游戏目录 | 6 | Steam / Epic / GOG / Ubisoft，含 `steamapps\common\*` 与非标准路径两种 |
| E | 开发工具与项目目录 | 8 | VS / Node.js / Python / Java / `node_modules` / `.git` 仓库 / Unity 工程 |
| F | 含用户文档/配置/存档的目录 | 6 | `save`、`profile`、`userdata`、文档与媒体目录 |
| G | 正在运行的软件 | 4 | 扫描时保持进程占用（如正在运行的 IDE） |
| H | 外置硬盘 / 网络共享 / 不可达路径 | 4 | 拔掉外置盘后扫描其盘符路径；UNC 离线共享 |
| I | 系统目录、关键路径、重解析点 | 4 | `C:\Windows\*`、`Program Files` 一级目录、junction / symlink |
| J | 空目录、普通文件夹、下载目录、安装包目录 | 6 | 普通数据夹、`Downloads`、含 `*.exe` 安装包的目录 |

**每个样本必须由人工给出真值标签**，标签来源可以是：用户自己确认、卸载项存在性、快捷方式存在性、最近使用记录。**不能只用 Core 自己的输出当真值。**

### 5.3 人工回标 Schema（JSONL，一行一个样本）

文件格式：`validation/labels.jsonl`，UTF-8，一行一个 JSON 对象。

```json
{
  "schemaVersion": 1,
  "sampleId": "A-003",
  "sampleClass": "A",
  "scannedPath": "D:/OldSoftware/AdobeReader",
  "pathRedacted": true,
  "groundTruth": "ConfirmedOrphan",
  "groundTruthConfidence": "Certain",
  "isActuallyInUse": false,
  "containsUserData": false,
  "humanNote": "重装前装的旧版本，卸载项已随系统重置丢失",

  "core": {
    "confidenceScore": 92,
    "confidenceLevel": "VeryHighProbability",
    "riskScore": 20,
    "risk": "Low",
    "recommendation": "Quarantine",
    "isProtected": false,
    "protectionReason": null,
    "isExcluded": false,
    "excludeReason": null,
    "directoryCategory": null
  },

  "evidence": [
    { "type": "Uninstaller",              "score": 25, "isPositive": true,  "severity": "Info" },
    { "type": "MissingUninstallEntry",    "score": 20, "isPositive": true,  "severity": "Info" },
    { "type": "LongUnmodified",           "score": 12, "isPositive": true,  "severity": "Info" }
  ],
  "warnings": [],

  "associationSourceHealth": [
    { "sourceName": "注册表卸载项", "status": "Success", "itemCount": 187, "errorMessage": null },
    { "sourceName": "计划任务",     "status": "Partial", "itemCount": 0,   "errorMessage": "未采集到任何记录…" }
  ],

  "environment": {
    "osVersion": "Windows 11 23H2",
    "coreVersion": "0.1",
    "gitCommit": "28fb8d2",
    "runAsAdmin": true,
    "scannedAt": "2026-10-16T14:03:00+08:00",
    "scanDurationMs": 41230
  }
}
```

字段约束：

- `sampleId`：`{类代号}-{三位序号}`，全局唯一
- `sampleClass`：`A`–`J`
- `groundTruth`（真值枚举）：
  - `ConfirmedOrphan` 确认残留
  - `ActiveSoftware` 正常使用的软件
  - `PortableSoftware` 绿色/便携软件
  - `GameLibrary` 游戏或游戏库
  - `DevelopmentData` 开发环境或项目
  - `UserData` 用户数据
  - `Unknown` 无法确认
- `groundTruthConfidence`：`Certain` / `Likely` / `Unsure`
- `pathRedacted`：是否对路径做了脱敏（见 §5.6）
- `evidence[].type`、`associationSourceHealth[].status` 使用与 Core 相同的枚举名（`EvidenceType`、`AssociationSourceStatus`），便于机器比对
- `Unknown` 与 `groundTruthConfidence == Unsure` 的样本**单列统计，不进入主指标分母**

### 5.4 指标定义

二分类（正类 = `ConfirmedOrphan`）：

| | Core 判为疑似残留（≥ Suspicious，即 ≥40） | Core 判为普通（< 40） |
|---|---|---|
| 真值 = 残留 | **TP** | **FN** |
| 真值 = 非残留 | **FP** | **TN** |

主指标：

- `Precision = TP / (TP + FP)` —— **误报成本极高，这是最重要的指标**
- `Recall = TP / (TP + FN)`
- `FPR = FP / (FP + TN)`

分层误报（比总 FP 更重要，必须单独报告）：

- **M1 正常软件被推荐 `Quarantine` 的样本数** —— 目标 **0**
- **M2 用户数据（F 类）被推荐 `Quarantine` 的样本数** —— 目标 **0**
- **M3 开发项目（E 类）被推荐 `Quarantine` 的样本数** —— 目标 **0**
- **M4 游戏库（D 类）被推荐 `Quarantine` 的样本数** —— 目标 **0**
- **M5 健康度约束正确性**：凡 `associationSourceHealth` 中存在 Failed/Partial 的样本，`confidenceScore` 必须 ≤ 对应上限（注册表不可用 ≤55、任一失败 ≤65、≥3 个失败 ≤45）—— 违反数目标 **0**
- **M6 无"确定性缺失结论"**：健康度非 Success 的来源，其 `evidence` 中不得出现对应 `Missing*` 类型 —— 违反数目标 **0**
- **M7 Confidence ≠ Risk 校验**：统计 `confidenceScore ≥ 70 且 risk == High` 的样本，其 `recommendation` 必须 ∈ `{Review, HighRiskReview}`，不得为 `Quarantine` —— 违反数目标 **0**
- **M8 保护规则一致性**：`isProtected == true` 的样本，`recommendation` 不得为 `Quarantine` —— 违反数目标 **0**
- **M9 噪音观察（非硬指标）**：统计因"零记录 → Partial"触发 Warning 的样本占比（对应 §4.10），若 > 30% 需评估是否调整 `Partial` 判定口径

**验收门槛建议（Core 0.1 → Core 0.2 的 gate）**：
- M1–M4、M6–M8 全部为 **0**
- M5 违反数为 **0**
- `Precision ≥ 0.90`（误报容忍度极低）
- `Recall ≥ 0.60`（准确率优先于覆盖率；召回不足可通过降低阈值**并同步补强保护规则**来提升，不得单独放宽保护）

未达门槛时，按 §5.5 步骤 6 迭代；**任何权重修改必须附带真实样本依据 + 新增回归测试**。

### 5.5 执行步骤

**步骤 0 — 解除阻塞（需用户确认）**
1. UI 线修复 7 处 XAML 注释（或授权 Core 侧代修），使 `WinCleaner.sln` 通过 Release 构建。
2. 新增只读验证入口 `tools/WinCleaner.Validate`（控制台，Core 线所有）：
   - 参数：`--roots <path;path>`、`--out <file.jsonl>`、`--redact`
   - 行为：构造真实 `AssociationIndex` + `SoftwareDirectoryInspector` + `OrphanScanner`，导出 JSONL
   - **不含任何清理/删除/隔离调用**，不引用 `CleanupService`
3. 补 `ReportExporter`：导出 `AssociationSourceHealth`（对应 §4.4）

**步骤 1 — 采集样本**
按 §5.2 在真实机器上定位样本目录，记录到样本清单（不含 Core 输出）。

**步骤 2 — 人工标注真值**
先标真值，再跑扫描。**顺序不可颠倒**，避免 Core 输出影响人工判断。

**步骤 3 — 跑扫描并导出**
`WinCleaner.Validate --roots ... --out run1.jsonl --redact`

**步骤 4 — 合并**
把 `run1.jsonl` 与人工真值按 `scannedPath` 合并成 `labels.jsonl`（§5.3 格式）。路径已脱敏时用 `sampleId` 关联。

**步骤 5 — 统计**
脚本（Python 或 C# 均可）计算 §5.4 全部指标，输出汇总表 + 每个 FP/FN 样本的 Evidence 明细。

**步骤 6 — 分析并迭代（只在需要时改代码）**
- 每个 FP 必须定位到**具体是哪条 Evidence 给了正分、哪条保护证据没生效**
- 修改优先顺序：**先补保护规则/目录识别 → 再调 Footprint → 最后才调 Evidence 权重**
- 每次修改必须补一条以该真实样本为原型的回归测试（可对路径脱敏）
- 重跑 `dotnet build` + `dotnet test` + 重跑验证

**步骤 7 — 产出验证报告**
包含：样本分布、主指标、M1–M9、所有 FP/FN 逐条分析、权重是否调整及依据、已知局限。

### 5.6 隐私与脱敏

报告中会出现用户名、安装路径、软件清单，属于敏感信息。

- `--redact` 默认开启：用户名段替换为 `<USER>`，盘符保留（盘符是判断必需），完整路径另存为**仅本地**的映射表 `path-map.local.json`（加入 `.gitignore`，不入库）
- 入库的 `labels.jsonl` 只含脱敏路径 + `sampleId`
- 不采集、不导出：文件内容、文件名以外的任何数据、环境变量值、注册表键值内容（只记录来源名称与条数）
- 用户文档/媒体目录（F 类）只记录目录名与类别，不导出具体文件名
- 验证报告对外分享前，必须人工复核一次 `scannedPath` 字段

### 5.7 安全边界（验证阶段禁止事项）

1. 禁止调用 `CleanupService` 的任何方法
2. 禁止调用 `QuarantineManager.MoveToQuarantineAsync`
3. 禁止调用 `InvalidRegistryScanner.RemoveAsync`
4. 禁止执行 `DirectoryHelper.Delete`
5. 禁止以提升权限写入 `Program Files`、`Windows`、注册表
6. 禁止对 H 类（外置/网络/不可达路径）执行任何写操作
7. 验证入口必须显式打印"READ-ONLY MODE"并以退出码区分成功/失败

---

## 6. 最小必要改进清单

### 6.1 阻塞项（不解决则验证无法开始）

| ID | 问题 | 归属 | 建议动作 |
|---|---|---|---|
| B1 | 7 个 UI XAML 的 `MC3000`，整解决方案构建失败 | **UI 线** | 把注释里 `-----` 改为 `=====`。Core 侧可在用户授权后代修 |
| B2 | 无只读验证入口 | Core 线 | 新增 `tools/WinCleaner.Validate` 控制台项目（只读，不引用 CleanupService） |
| B3 | 报告缺 `AssociationSourceHealth`，无法解释置信度上限 | Core 线 | `ReportExporter.cs` 补一个字段（约 5 行） |

### 6.2 非阻塞（Core 0.2 候选，按真实验证结果排优先级）

| ID | 问题 | 优先级 | 建议时机 |
|---|---|---|---|
| N1 | 跨卷隔离失败无回滚（§4.2） | P0 | 必须在**任何真实隔离操作**之前修复 |
| N2 | 隔离 manifest 无 Hash / 无 schema 版本（§4.3） | P0 | Core 0.2 |
| N3 | 注册表备份失败仍删除（§4.5） | P1 | Core 0.2；修复前该模块保持 Beta 且不暴露给 UI 默认路径 |
| N4 | manifest 损坏条目静默丢弃（§4.6） | P1 | Core 0.2 |
| N5 | 采集器子项异常无计数（§已知问题 2） | P1 | Core 0.2；按 `ItemsScanned / ItemsFailed / ItemsCollected` 三元组改造 |
| N6 | `IsUsable` vs `IsSourceReliable` 语义冲突（§4.7） | P2 | 真实验证后统一口径 |
| N7 | `DirectoryHelper.Delete` 误报成功（§4.8） | P2 | Core 0.2 |
| N8 | 受保护内容仍可被 Quarantine（§4.9） | P2 | 需产品决策：是否要求二次确认 |
| N9 | `OrphanAssessment` 孤儿类型，UI 契约未落地（§4.12） | P2 | 与 UI 线确认：是让 `OrphanItem` 承载契约，还是让 Scanner 输出 `OrphanAssessment` |
| N10 | 零记录一律 Partial 的噪音（§4.10） | P2 | 由 M9 指标结果决定 |
| N11 | 白名单匹配规则 / 用户自定义 / 持久化（已知问题 1） | P3 | Core 0.2 |

---

## 7. 待用户确认事项

1. **B1 授权**：是否允许 Core 侧代修 7 处 UI XAML 注释（改 `-----` → `=====`，纯语法、零逻辑影响），还是转交 UI 对话处理？
2. **B2 授权**：是否批准新增 `tools/WinCleaner.Validate` 只读控制台项目（新增文件，不改动任何现有业务代码）？
3. **B3 授权**：是否批准给 `ReportExporter` 补 `AssociationSourceHealth` 字段？
4. **验证机器**：在哪台真实 Windows 机器上跑？是否允许扫描 `C:\Program Files` 与外置盘？是否需要管理员权限运行（影响进程/服务采集完整性）？
5. **N9 契约决策**：UI 契约以 `OrphanItem` 为准，还是正式切换到 `OrphanAssessment`？需与 UI 对话同步。
6. **N8 产品决策**：受保护内容（`IsProtected == true`）是否允许在人工确认后执行隔离？

在以上确认前，**不修改任何业务代码**。

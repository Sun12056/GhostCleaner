# Core 侧对 UI 反向要求的落地（契约改动）

> 日期：2026-10-16
> 对应：`docs/ui-0.1/10-UI对判定能力边界的落地回执.md` 的「反过来三件事」与两个问题
> 依据：`docs/core-0.1/09-…指标结果.md`（47 条回标）、`10-…方法论审核.md`

---

## 0. 结论速览

| UI 要求 | 处理 |
|---|---|
| ① Warning 要结构化 | ✅ 已落地 `ScanWarning(Code, Message)` |
| ② `ToText` 文案改成新说法 | ✅ 已改，并加契约测试锁住 |
| ③ 索性不输出 `Quarantine` | ✅ 已采纳，建议集收敛为 `{Keep, Review, HighRiskReview}` |
| Q1 卸载器入口可行吗 | ✅ **确认可行**，路径真实存在（见 §4） |
| Q2 90 天活动提示 | ✅ 已把 `RecentModifiedFileCount90` 挂到 `OrphanItem` |
| Prefetch 验证建议 | ✅ 接受，改为单列 4 条样本看分数变化 |

附带修了 UI 的两处编译错误（见 §5.3）。

---

## 1. Warning 结构化

### 新增类型

`Core/Models/ScanWarning.cs`：

```csharp
public enum ScanWarningCode
{
    Other = 0,
    AssociationCheckIncomplete,      // 关联检查未完成
    ProcessCheckIncomplete,          // 运行中进程检查未完成
    RegistryUninstallCheckIncomplete,
    ScanIncomplete,
    HasUninstaller,                  // 自带卸载器（操作建议，非"不确定"）
    DevelopmentProject,
    GameDirectory,
}

public sealed class ScanWarning
{
    public ScanWarningCode Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsUncertainty => Code is AssociationCheckIncomplete or ProcessCheckIncomplete
                                      or RegistryUninstallCheckIncomplete or ScanIncomplete;
}
```

`IsUncertainty` 直接对应 UI 要的「不确定信号 —— 没查清楚，不代表没人用」分区。

### 改动链路

`EvidenceCollection.Warn(code, message)` → `OrphanItem.Warnings` →
`OrphanAssessment.Warnings` → `QuarantineContext.Warnings` → `QuarantineEntry.Warnings`
→ `ReportExporter` / `QuarantineManager`。

7 个产出点全部补上 code：

| 原文案 | code |
|---|---|
| 未能完成【X】关联检查… | `AssociationCheckIncomplete` |
| 未能完成【运行中的进程】检查… | `ProcessCheckIncomplete` |
| 注册表卸载项检查未完成… | `RegistryUninstallCheckIncomplete` |
| 扫描结果可能不完整：N 个来源… | `ScanIncomplete` |
| 该目录自带卸载程序… | `HasUninstaller` |
| 疑似开发项目或工具链… | `DevelopmentProject` |
| 疑似游戏/游戏库… | `GameDirectory` |

### JSONL 兼容处理

`warnings` 列**保持字符串数组**（既有分析脚本不失效），**新增 `warningCodes` 列**
与之一一对应。需要按类别统计时用后者。

### 实测分布（非管理员，47 条）

```
AssociationCheckIncomplete    47
ScanIncomplete                47
ProcessCheckIncomplete        41
DevelopmentProject            22
HasUninstaller                 2
GameDirectory                  1
```

---

## 2. `ToText` 文案改为「没检测到什么」

| 等级 | 旧 | 新 |
|---|---|---|
| ≥90 | 非常高概率残留 | **未检测到任何使用迹象** |
| ≥70 | 高概率残留 | **几乎未检测到使用迹象** |
| ≥40 | 可疑 | **使用迹象较少** |

字段名未动（UI 要求），只改文案。导出报告 `ReportExporter` 与 UI 说法因此自动一致。

新增 `tests/ConfidenceTextTests.cs`（5 个测试），其中一条断言
**任何等级的文案都不得含"残留"字样** —— 防止有人改回去。

---

## 3. 不再输出 `Quarantine`

`RiskEvaluator.Recommend` 移除 `VeryHighProbability && Low → Quarantine` 分支。

依据：

1. 实测 47 条 `Quarantine` 输出本来就是 **0**
   （自带卸载器分支拦了大半，高风险又被 `HighRiskReview` 拦掉）
2. `Precision 0.333` / `AUC 0.648` —— 置信度 96 的样本真值是"日常在用"。
   在此准确率下由 Core 主动建议"隔离"本身就是误删风险源

**建议集收敛为 `{Keep, Review, HighRiskReview}`。隔离从此永远只是用户自选动作。**

实测（非管理员 47 条）：`HighRiskReview 41` / `Review 3` / `Keep 3` / **`Quarantine 0`**。

---

## 4. 答复 UI 的两个问题

### Q1：卸载器入口 —— 可行，已确认路径真实存在

代码链路本来就是通的，无需新增字段：

```
SoftwareDirectoryInspector.cs:179-183   if (uninstaller.Path != null) { HasUninstaller = true; UninstallerPath = ... }
RiskEvaluator.cs:87                     UninstallerPath = info.UninstallerPath
OrphanItem.cs:38                        public string? UninstallerPath
```

`HasUninstaller == true` 蕴含 `UninstallerPath != null`（前者只在后者非空时置位），
所以 UI 的 `File.Exists` 兜底足够。

实测文件系统确认：

| 目录 | 卸载器 |
|---|---|
| `D:\Weixin` | `Uninstall.exe` ✅ |
| `D:\wangyiyunyinyue` | `UnInstall.exe` ✅ |
| `D:\Registry Repair` | `uninst.exe` ✅ |
| `D:\IROK Software` | `uninstall.exe` ✅ |
| `D:\Microsoft VS Code` | `unins000.exe` ✅ |
| `D:\Bandizip` | `Uninstall.exe` ✅ |

⚠️ 一个容易误判的点：**JSONL 报告里查不到 `UninstallerPath`**。
因为 JSONL 输出的是 `ScanRecord`（验证用结构），不是 `OrphanItem`。
不要拿 JSONL 判断这个字段有没有值 —— 应当看内存中的 `OrphanItem`。

另外按 UI 建议加了防御：`EvidenceCollector.cs:111` 原先在路径为空时会产出
「目录包含 」这种残缺文案，现已改为输出「目录包含卸载程序」。

### Q2：90 天活动 —— 已透出**量级**而非布尔

`OrphanItem` 新增 `RecentModifiedFileCount90` / `RecentModifiedFileCount365`，
由 `RiskEvaluator` 从 `SoftwareDirectoryInfo` 透出。

UI 的理由成立：「修改了 100 个文件」和「修改了 1 个」含义完全不同，
只给布尔会丢掉量级信息。

---

## 5. 顺带处理

### 5.1 接受 UI 关于 Prefetch 的验证建议

改为**单独列出 4 条 FP 样本**（`wangyiyunyinyue` 96 / `Weixin` 81 / `IROK Software` 58 /
`Microsoft VS Code` 46）看分数变化，比看 AUC 涨 0.05 有说服力。
UI 的提醒也记下了：废弃软件重装前跑过也可能留 Prefetch 记录，
它可能主要改善"仍在用"侧而非整体区分度。

### 5.2 移除了 UI 里的按文案匹配函数

`OrphanScanViewModel.IsUncertaintyWarning(string)` 已删除，
改由 `ScanWarning.IsUncertainty` 提供。`HasUncertainProcessWarning` 也改为读
`ScanWarningCode.ProcessCheckIncomplete`。

### 5.3 ⚠️ 修了 UI 的两处编译错误（请 UI review）

`OrphanScanViewModel.cs` 原代码：

```csharp
public IReadOnlyList<string> SelectedUncertaintyWarnings =>
    SelectedItem?.Warnings.Where(IsUncertaintyWarning).ToList() ?? Array.Empty<string>();
```

`??` 两侧分别是 `List<string>` 与 `string[]`，`CS0019` 编译不过。
已改为 `SelectedItem == null ? Array.Empty<ScanWarning>() : ...` 形式，
并把返回类型改为 `IReadOnlyList<ScanWarning>`（配合结构化）。

**XAML 若直接绑定这两个集合**，现在元素类型是 `ScanWarning`，
其 `ToString()` 返回 `Message`，显示行为不变；但若 XAML 里按字符串属性绑定则需调整。

---

## 6. 本轮状态

- 构建 **0 警告 0 错误**；测试 **120 通过 / 0 失败**（新增 7 个）
- 全程只读扫描，未修改任何被扫描内容
- 未改动任何权重值

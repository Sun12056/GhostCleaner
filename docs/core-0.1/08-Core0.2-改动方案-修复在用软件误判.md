# Core 0.2 改动方案：修复「在用软件被判 100% 残留」

> 日期：2026-10-16
> 触发：首批 3 条回标结果（U-001/U-002 为 FP，见 `07-…md`）
> 性质：**方案设计，尚未实施**。权重类改动须待回标完成后才能落地
> 验算数据：47 条目录的近期活动实测（`validation/raw/` 本地数据）

---

## 0. 一个必须先说的诚实结论

对 47 个目录实测「90 天内 / 365 天内被修改的文件数」后发现：

| 样本 | 真值 | 90 天内修改文件数 | 365 天内 |
|---|---|---|---|
| U-001 `D:\Weixin` | **在用** | **19** | 29 |
| U-002 `D:\wangyiyunyinyue` | **在用** | **0** | 54 |
| U-003 `D:\Registry Repair` | 真残留 | 0 | 37 |

**仅靠文件系统元数据，无法可靠区分「装了在用、但程序目录长期不变」与「真废弃」。**

U-002 网易云音乐：程序目录 125 天无写入（数据在 `AppData` 别处），这在正常使用的软件里极其常见。
U-003 真残留：365 天内还有 37 个文件被改（安装/更新残留），比 U-001 还多。

> 因此本方案**不追求「靠时间戳分辨在用与否」** —— 那是做不到的事。
> 核心策略改为两条：
> 1. **能救的救**（有活动信号的降分）
> 2. **救不了的就不给危险建议**（自带卸载器的目录，不推荐清理，让用户用正规卸载器）

---

## 1. 改动 1（P0，安全策略，无需校准）— 自带卸载器的目录不推荐清理

### 问题

三条 100 分样本**全部**命中 `Uninstaller(+25)`：目录里自带卸载程序。
一个自带卸载器的目录被判 100% 残留，而用户若按建议清理，等于**绕过正规卸载流程直接删目录**
（卸载器通常会清理注册表、服务、关联项，直接删目录会留下垃圾）。

47 条中有 **24 条**命中 `Uninstaller` 证据 —— 影响面不小。

### 改动

`src/WinCleaner.Scanner/RiskEvaluator.cs:200-206` 的 `DecideRecommendation`：

```csharp
// 现有
if (isExcluded || facts.IsCriticalPath) return CleanupRecommendation.Keep;
if (risk == RiskLevel.High) return CleanupRecommendation.HighRiskReview;
if (isRunning) return CleanupRecommendation.HighRiskReview;
if (facts.IsProtected) return CleanupRecommendation.Review;

// 新增（放在 risk 判定之后、Quarantine 之前）
if (info.HasUninstaller)
{
    warnings.Add("该目录自带卸载程序，建议优先使用其自带卸载器卸载，"
               + "直接删除目录会残留注册表项与系统关联");
    return CleanupRecommendation.Review;   // 永不返回 Quarantine
}
```

### 契约影响

- **过渡方案（零 UI 改动）**：返回 `Review` + 新增 Warning，UI 现有分支即可正确显示
- **完整方案（需 UI 同步）**：新增 `CleanupRecommendation.UseUninstaller`，UI 增加对应徽标与文案
- 建议先落地过渡方案，完整方案随下次契约同步一起给 UI

### 预期效果（真实数据验算）

三条 100 分样本全部从「高置信疑似残留」降级为「人工复核 + 提示用自带卸载器」，
**用户不会再被引导去直接删除仍在使用的微信/网易云音乐目录**。

---

## 2. 改动 2（P1，可解释性）— 消除 31~179 天时间盲区

### 问题

`EvidenceCollector.cs:305-327` 只有三段，31~179 天不产出任何时间证据：

```
≥180 天 → LongUnmodified +12
≤7 天   → RecentModification -30
≤30 天  → RecentUsage -15
其余     → 无证据（盲区）      ← U-001(34天) / U-002(125天) / U-003(124天) 全落在这里
```

UI 因此无法向用户解释「这个目录到底多久没动了」。

### 改动

改为连续分段，任何天数都产出证据：

| 天数 | 证据 | 建议权重 | 说明 |
|---|---|---|---|
| ≤7 | `RecentModification`（现有） | −30 | 刚用过 |
| ≤30 | `RecentUsage`（现有） | −15 | 近期用过 |
| **≤90** | **`RecentActivity`（新增）** | **−8** | 90 天内有活动 |
| **≤180** | **`StaleButNotAncient`（新增）** | **0** | 中性，但**必须产出证据**以便 UI 显示 |
| >180 | `LongUnmodified`（现有） | +12 | 长期未动 |

新增两个 `EvidenceType` 枚举成员（Core 侧），权重标注 **需回标校准**。

### 预期效果（真实数据验算）

| 样本 | 天数 | 现状 | 改后 |
|---|---|---|---|
| U-001 Weixin | 34 | 无证据 | `RecentActivity −8` → 106−8 = **98** |
| U-002 wangyiyunyinyue | 125 | 无证据 | `StaleButNotAncient 0` → **100** |
| U-003 Registry Repair | 124 | 无证据 | `StaleButNotAncient 0` → **100** |

⚠️ **注意**：这一项**几乎不改变分数**，它的价值在可解释性（UI 能说清"125 天没动过"），
**不能**指望它降低误报。降低误报靠改动 1 和改动 3。

---

## 3. 改动 3（P1，新增信号）— 统计「近期活动文件数」

### 问题

`info.LastModified` 是**单点**最大 mtime（`SoftwareDirectoryInspector.cs:25/58/163`），
脆弱：一个目录几万文件里只要有一个新文件，就掩盖了整体陈旧；反之亦然。

### 改动

`SoftwareDirectoryInspector` 遍历时（已有遍历，额外成本≈0）统计：

```csharp
info.RecentModifiedFileCount90  // 90 天内被修改的文件数
info.RecentModifiedFileCount365 // 365 天内
```

`EvidenceCollector` 新增证据：

| 条件 | 证据 | 建议权重 |
|---|---|---|
| `RecentModifiedFileCount90 >= 1` | `RecentFileActivity` | **−25** |
| 90 天内为 0 但 365 天内 ≥ 1 | `HistoricalFileActivity` | −10 |

### 预期效果（真实数据验算）

| 样本 | 90 天内文件数 | 改后分数 | 效果 |
|---|---|---|---|
| U-001 Weixin（**在用**） | **19** | 106−8−25 = **73** | ✅ 从 100 降到 73（脱离 VeryHighProbability） |
| U-002 wangyiyunyinyue（**在用**） | 0 | 100 | ❌ **仍然误报** |
| U-003 Registry Repair（真残留） | 0 | 100 | ✅ TP 保持 |

**只能救回 1/2 的 FP。** 这正是 §0 结论的实证：U-002 这类「程序目录不常变」的软件，
文件时间戳救不了，必须靠改动 1 的「有卸载器就不给危险建议」兜底。

### 附带收益

置信度 0 的样本里有一大批高活动目录（`D:\文档` 47553、`D:\桌面` 34590、`D:\下载` 5473、
`node.js` 1109、`WPS Office` 4224…）。加入该信号后，这些目录的"仍在活跃使用"会变成**显式证据**，
而不是靠"被识别为用户数据"间接压到 0 —— 判定更可解释。

---

## 4. 改动 4（P2）— 重装场景下重新评估 `MissingUninstallEntry` 区分度

用户明确说明本机是「重装系统后的 D 盘」。重装后 **D 盘所有软件目录都会缺失注册表卸载项**，
因此 `MissingUninstallEntry(+20)` 在该场景下**区分度接近于零**。

候选方向（**必须先有回标数据**）：

- 与 `HasUninstaller` 联合判定：有卸载器 + 缺注册表项 → 更可能是"重装导致登记丢失"而非"真废弃"
- 或引入"重装检测"信号：若检测到大量目录同时缺失注册表项（如 >50%），
  整体下调缺失类证据权重并显式提示用户「本次扫描疑似处于重装后环境，残留判定可靠性下降」

⚠️ 这一项对权重影响最大，**必须等 47 条全部回标完成后**再决定，禁止提前动手。

---

## 4.5 改动 5（P0，上报真实性，非权重）— 采集器必须报告「部分失败」

> 由 UI 对话在提权闸门落地时反向发现（见 `docs/ui-0.1/09-…`），Core 已核验属实。

### 问题

`AssociationCollectors.cs:184-203` 进程采集器：

```csharp
foreach (var p in Process.GetProcesses())
{
    try { var path = p.MainModule?.FileName; ... }
    catch { /* 无权限访问进程模块 */ }   // 静默跳过，不留痕
}
return list;                              // 报 Success
```

`EvidenceCollector.cs:329-343` 的闸门本是对的：

```csharp
if (IsSourceReliable(health, RunningProcess))
    result.Add(NoRunningProcess, +12, ...);   // ← 走这条
else
    result.Warn("未能完成【运行中的进程】检查…");  // ← 永远走不到
```

非管理员下采集器报 `Success(193)` → 闸门判定可靠 → **正在运行但无权限访问的进程，反而让目录拿到 +12 的「未检测到运行中进程」正向残留证据**。
这不是"削弱保护"，是**主动加了错误的分**。

### 数据验证（精确吻合）

| 权限 | 采集条数 |
|---|---|
| 非管理员 | 193 |
| 管理员 | **356** |
| 差值 | **163 = 被静默跳过的进程数** |

`193 + 163 = 356` 完全吻合，证实这 163 个进程确因权限不足被丢弃。

### 改动（3 个文件，不碰任何权重）

1. **新增** `Core/Models/CollectionDiagnostics.cs`：`ItemsScanned` / `ItemsFailed` / `Error`
2. `Registry/AssociationCollectors.cs`：
   - `RunningProcesses(CollectionDiagnostics? diag = null)` — 记录扫描数与无权限失败数
   - `ScheduledTasks(CollectionDiagnostics? diag = null)` — 捕获 `UnauthorizedAccessException`
     并写入 `Error = "权限不足，无法枚举 C:\Windows\System32\Tasks"`
3. `Registry/AssociationIndex.cs`：
   - 元组增加 `CollectionDiagnostics? Diag` 字段（9 行各补一项）
   - 收集后统一判定：`if (status == Success && Diag.ItemsFailed > 0) → Partial + 明确错误`
   - 计划任务被拒绝时 → `Failed + 权限不足`（比现在笼统的"未采集到任何记录"准确得多）

### 预期效果

| 来源 | 非管理员（现状 → 改后） | 管理员 |
|---|---|---|
| 正在运行的进程 | `Success 193` → **`Partial`（193 扫描 / 163 无权限）** | `Success 356` |
| 计划任务 | `Partial 0`（"未采集到任何记录"）→ **`Failed`（权限不足）** | `Success 101` |

收益是双份的：

1. **`+12` 不再被错误加上**（非管理员下所有未命中进程的目录都受益）
2. **UI 健康度面板自动获得来源级精度**（"163 个进程无权限访问"、"权限不足无法枚举计划任务"）

管理员下行为**完全不变**（进程采集完整，`Weixin` 确实未运行，`+12` 合理保留）。

### 依赖

无。这是**状态上报的真实性修正，不是权重调整**，不违反「先回标再调权重」的铁律，可优先落地。

---

## 5. 实施顺序与前置依赖

| 顺序 | 改动 | 依赖 | 可立即实施 |
|---|---|---|---|
| 1 | **改动 5**（采集器上报真实状态） | 无（上报真实性，非权重） | ✅ **可以** |
| 2 | 改动 1（有卸载器 → Review + 提示） | 无（安全策略，非权重调整） | ✅ **可以** |
| 3 | 改动 2（消除盲区） | 新增枚举需 UI 知晓；权重待校准 | ⚠️ 枚举可先加，权重留默认 |
| 4 | 改动 3（近期活动文件数） | 权重待校准 | ⚠️ 信号可先采集，权重留默认 |
| 5 | 改动 4（重装场景评估） | **47 条全部回标** | ❌ 必须等 |

### 铁律

1. **不得先调权重再看指标**。回标 → 算 M1–M9 → 再调 → 补回归测试
2. 每次权重调整必须附：真实样本依据 + 回归测试
3. 改动 1 是安全策略不是权重调整，可优先落地

---

## 6. 验证方式（改动落地后）

```powershell
# 重跑管理员全量，对比改动前后
tools\WinCleaner.Validate\bin\Release\net8.0-windows\WinCleaner.Validate.exe `
  --roots "D:\" --out validation\raw\run-after-change.jsonl --commit <sha>
```

必查：
1. U-001/U-002/U-003 的 `Recommendation` 是否都不是 `Quarantine`
2. U-001 置信度是否从 100 降到 ~73
3. 47 条中 `Quarantine` 数量是否仍为 0（应保持不变，方向仍偏安全）
4. 是否出现新的 `HighProbability` 误报（回归检查）

---

## 6.5 改动 5 + 改动 1 已实施：验证结果（2026-10-16）

非管理员轮次，`validation/raw/run-after-user.jsonl`（对比 `run-full-user.jsonl`）。

### 采集源状态：已按预期上报真实失败

| 来源 | 改动前 | 改动后 | ErrorMessage |
|---|---|---|---|
| 计划任务 | `Partial 0` | **`Failed 0`** | `权限不足，无法枚举 C:\WINDOWS\system32\Tasks（Access to the path ... is denied.）` |
| 正在运行的进程 | `Success 193` | **`Partial 197`** | `共扫描 381 项，184 项失败：部分进程无权限访问` |

### 证据：`+12` 已停止错误计入

`U-001 D:\Weixin` 正向证据对比：

```
改动前：… MissingStartupEntry+6  NoRunningProcess+12
改动后：… MissingStartupEntry+6                        ← +12 消失
```

新增警告（**此前该分支永远不可达**）：

```
未能完成【运行中的进程】检查，无法确认该目录是否正在被使用
扫描结果可能不完整：2 个关联来源未能完成检查（计划任务、正在运行的进程）
```

47 条中 **41 条**获得上述「未能完成进程检查」警告。

### 分数变化（非管理员）

| 样本 | 改动前 | 改动后 | 等级 |
|---|---|---|---|
| U-001 `D:\Weixin` | 65 | 65 | VeryHigh → **Suspicious** |
| U-003 `D:\Registry Repair` | 65 | 65 | VeryHigh → **Suspicious** |
| U-004 `D:\T20` | 61 | **41** | Suspicious |

`Quarantine` 数量：**0 / 47**（保持不变）。

### ⚠️ 两点必须说明

**1. 改动 1（自带卸载器）在本轮数据中未改变任何结果。**

47 条里 24 条命中 `Uninstaller` 证据，但只有 **2 条**真正走到 `hasUninstaller` 分支
（其余先被 `risk == High → HighRiskReview` 拦截）。
由于 `Quarantine` 要求 `VeryHighProbability && Risk == Low`，当前数据中
根本不存在「有卸载器 + 低风险 + 高置信」的样本。

→ 改动 1 目前是**纯防御性**的：它拦的是未来可能出现的路径，本轮输出无变化。

**2. 改动 5 只修非管理员场景，管理员下 U-001 仍会是 100 分。**

管理员下进程采集完整（356 项全部可访问），`Weixin` 扫描时确实未运行，
因此 `NoRunningProcess +12` 会被**正确**计入 → 原始分 106 → clamp 100。

→ 管理员下的 FP 必须靠**改动 3（近期活动文件数 −25）**解决：`106 − 25 = 81`。
这印证了方案分工：改动 5 修「上报真实性」，改动 3 修「误判本身」。

### 待验证

管理员轮次尚未重跑（需手动提权）。预期：进程来源应报 `Success`（无失败项），
用于确认诊断不会在权限充足时误报。

---

## 6.6 改动 3 已实施：近期活动文件数（2026-10-16）

### 改动文件（5 个，无一处修改既有权重）

| 文件 | 改动 |
|---|---|
| `Core/Models/EvidenceType.cs` | 新增 `RecentFileActivity` / `HistoricalFileActivity` |
| `Core/Models/EvidenceWeights.cs` | 新增 `RecentFileActivityPenalty = -25`、`HistoricalFileActivityPenalty = -10`（**均标注待回标校准**） |
| `Core/Models/SoftwareDirectoryInfo.cs` | 新增 `RecentModifiedFileCount90` / `RecentModifiedFileCount365` |
| `Scanner/SoftwareDirectoryInspector.cs` | 遍历时顺带统计（已有遍历，额外成本 ≈ 0） |
| `Scanner/EvidenceCollector.cs` | 时间证据之后追加两条负向证据 |

判定规则：

```
90 天内有文件被修改      → RecentFileActivity     -25（很可能仍在使用）
近 90 天无、一年内有     → HistoricalFileActivity -10
```

### 命中统计（47 条，非管理员）

| 证据 | 命中 |
|---|---|
| `RecentFileActivity` | **32 / 47** |
| `HistoricalFileActivity` | **13 / 47** |
| 两者都未命中 | 2 / 47（`D:\NXPowerlite` 最新 2018-03-29、`D:\4173-…` 最新 2025-01-16） |

### 效果（按路径对比，非管理员 cap 65）

| 路径 | 改动前 | 改动后 | 新增证据 |
|---|---|---|---|
| `D:\Weixin`（**真值：在用**） | 65 | **47** | `RecentFileActivity(-25)` ✅ |
| `D:\wangyiyunyinyue`（**真值：在用**） | 65 | 62 | `HistoricalFileActivity(-10)` |
| `D:\Registry Repair`（真值：真残留） | 65 | 62 | `HistoricalFileActivity(-10)` |
| `D:\T20` | 41 | 31 | `HistoricalFileActivity(-10)` |
| `D:\Microsoft VS Code` | 22 | 12 | `HistoricalFileActivity(-10)` |

⚠️ 非管理员上限 65 掩盖了真实幅度。**管理员下预期**：
`Weixin` 原始分 `106 − 25 = 81` → 脱离 VeryHighProbability；
`wangyiyunyinyue` / `Registry Repair` `106 − 10 = 96` → 仍是 VeryHigh。
即改动 3 只能救回 1/2 的 FP，与 §3 的验算一致 —— 另一半仍需改动 1 兜底。

### 🔴 新发现（方法学）：`sampleId` 不稳定

对比两轮扫描，**37 / 47 条** 的同路径样本拿到了不同的 `U-0xx` 编号。

原因：`sampleId` 按扫描顺序分配，而扫描顺序不完全稳定（与权限、枚举顺序相关）。

**影响**：人工回标若按 `sampleId` 关联，会大面积错位，指标全部作废。

**处置**：
1. `validation/raw/to-label.md` 已加警告，明确以「路径」为准
2. Core 侧后续匹配真值一律按路径，不用 ID
3. ✅ **已解决（2026-10-17）**：`sampleId` 改为由**路径哈希**派生
   （`WinCleaner.Validate/Program.cs::StableSampleId`，取 SHA256 前 4 字节，形如 `U-5989C949`）。
   实测两轮扫描 **48/48 一致、0 错位**；对照旧方案同一对照集为 **42/47 错位**。
   47 条已完成回标已按路径迁移到新 ID（`to-label.md`），真值与把握列原样保留。

---

## 6.7 改动 5 的倒退与修复：管理员下上限从 100 掉到 65（2026-10-16）

### 现象

改动 3 后的管理员轮次（`run-act-admin.jsonl`），`runAsAdmin = True` 确认提权成功，
但置信度上限仍是 **65**，三条关键样本全部被压低：

| 样本 | 改动前（管理员） | 改动 3 后（管理员） |
|---|---|---|
| `D:\Weixin`（在用） | 100 | **61** |
| `D:\wangyiyunyinyue`（在用） | 100 | 65 |
| `D:\Registry Repair`（真残留） | 100 | 65 |

### 根因：改动 5 自身造成的倒退

管理员下的采集源状态：

```
正在运行的进程   Partial   357  共扫描 375 项，18 项失败：部分进程无权限访问
```

管理员**同样**有 18 个进程打不开 `MainModule` —— 这些是受保护系统进程
（`System` / `csrss` / `smss` / `lsass` 等），**即使管理员也永远无法访问**，
与权限无关，也不可能是用户软件进程。

而改动 5 当时的判定是 `ItemsFailed > 0 → Partial`，于是管理员下进程来源被降级，
触发「任意来源失败 → 上限 65」。

> 即：为了让非管理员下的不完整采集变保守，顺带把**完整采集**也误伤了。
> 上限 100 → 65 是明显倒退，且改动前的管理员轮次并没有这个问题。

### 修复：降级改看失败率

`CollectionDiagnostics` 新增（不删除 `HasFailures`）：

```csharp
public double FailureRatio => ItemsScanned == 0 ? 0 : (double)ItemsFailed / ItemsScanned;
public double MaxFailureRatio { get; init; } = 0.10;
public bool IsDegraded => HasFailures && (ItemsScanned == 0 || FailureRatio >= MaxFailureRatio);
```

`AssociationIndex.cs` 的判定从 `HasFailures` 改为 `IsDegraded`。

**阈值 0.10 的取值依据**（由真机数据选定，非拍脑袋）：

| 场景 | 失败率 | 判定 |
|---|---|---|
| 管理员（受保护系统进程） | 18/375 = **4.8%** | Success ✅ 不再误伤 |
| 非管理员（权限不足） | 163/375 = **43%** | Partial ✅ 仍正确降级 |

两侧各有 2 倍以上余量。⚠️ 该阈值仍需在更多样本上校准。

`ItemsScanned == 0` 单独处理：来源整体被拒绝时失败率失去意义，必然降级
（计划任务 `Failed` 行为不变）。

### 已验证

- 非管理员轮次：计划任务仍 `Failed（权限不足）`、进程仍 `Partial` —— **改动 5 的收益完好**
- 新增 `tests/CollectionDiagnosticsTests.cs`（5 个测试），其中一个专门断言
  「375 扫描 / 18 失败 → 不降级」，防止该倒退再次回归
- 构建 0/0，测试 **113 通过 / 0 失败**

### 待验证

管理员轮次需重跑，确认上限恢复 **100**、进程恢复 `Success`。

---

## 7. 本轮状态

- 构建 0 警告 0 错误；测试 108 通过 / 0 失败
- **未修改任何代码与权重**（本文档为方案）
- 47 条近期活动数据采集耗时 74 秒，全程只读

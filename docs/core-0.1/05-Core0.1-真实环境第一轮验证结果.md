# Core 0.1 真实环境第一轮验证结果（D 盘）

> 日期：2026-10-16
> 提交：`28fb8d2` + Core 0.1 验证环境改动 + `ScanOutcome`（均未提交）
> 工具：`tools/WinCleaner.Validate`（只读）
> 样本：**D 盘全量，非管理员，47 条**
> 原始数据：`validation/raw/run-full-user.jsonl`（已 gitignore）

> **补充**：管理员轮次已完成，两轮完整对比见
> `06-Core0.1-管理员与非管理员对比结果.md`。
> 本文件 §4.1 关于「计划任务 Partial」的分析已被 06 §3.1 证实（管理员下采到 101 条，属纯权限问题）；
> 本文件 §5 的下一步优先级请以 06 §5 为准。

---

## 1. 本轮执行

| 轮次 | 权限 | 状态 |
|---|---|---|
| 全量 `D:\` | 非管理员 | ✅ 完成，`validation/raw/run-full-user.jsonl` |
| 全量 `D:\` | 首次提权尝试 | ❌ **未真正提权**（`runAsAdmin` 仍为 `False`，见 §6.1） |
| 全量 `D:\` | 管理员 | ⏳ **待重跑** |

环境：`Microsoft Windows NT 10.0.26200.0`，`runAsAdmin = false`，扫描耗时约 8 秒。

---

## 2. 采集源健康度

| 来源 | 状态 | 条数 |
|---|---|---|
| 注册表卸载项 | 成功 | 93 |
| App Paths | 成功 | 54 |
| 开始菜单/桌面快捷方式 | 成功 | 18 |
| Windows 服务 | 成功 | 658 |
| **计划任务** | **不完整（Partial）** | **0** |
| 正在运行的进程 | 成功 | 193 |
| Microsoft Store 应用 | 成功 | 151 |
| 文件关联/右键菜单/COM | 成功 | 12084 |
| 启动项 | 成功 | 4 |

**8/9 成功，共 13255 条引用，本次置信度上限 = 65。**

`Partial` 的 `ErrorMessage` 有值（`"未采集到任何记录，该来源的关联检查可能不完整"`）—— 印证了 `AssociationIndex.cs:89-93` 的赋值路径有效。

---

## 3. 结果分布（47 条）

### Recommendation

| 建议 | 条数 | 占比 |
|---|---|---|
| HighRiskReview | 41 | 87% |
| Review | 4 | 9% |
| **Quarantine** | **0** | **0%** |
| Keep | 2 | 4% |

### Risk

| 风险 | 条数 |
|---|---|
| **High** | **42**（89%） |
| Medium | 4 |
| Low | 1 |

`RiskScore` 分布：100 分 19 条 / 95 分 12 条 / 85 分 11 条 / 55 分 2 条 / 45 分 2 条 / 10 分 1 条。

### ConfidenceLevel

| 等级 | 条数 |
|---|---|
| Normal（<40） | 40 |
| Suspicious（40–69） | 7 |
| HighProbability（≥70） | **0** |
| VeryHighProbability（≥90） | **0** |

### 其他

- 受保护 `IsProtected`：26 条
- 目录类别：开发项目 22 / 无 17 / 绿色软件 4 / 用户数据 3 / 游戏 1
- 运行中进程：6 条
- 含 Warning：47 条（全部，因计划任务 Partial）
- 置信度触顶（=65）：3 条

---

## 4. 三个关键发现

### 4.1 计划任务 `Partial` → 全局压到 65 → 无一条高概率残留

根因已确证（非管理员权限）：

```
C:\WINDOWS\System32\Tasks  存在 = True
Directory.EnumerateFiles → UnauthorizedAccessException「访问被拒绝」
```

`AssociationCollectors.cs:148-155` 用 `catch { return list; }` 吞掉 → 0 条 → `AssociationIndex.cs:89-93` 标 `Partial` → 触发 `MaxConfidenceWhenAnySourceFailed = 65`。

**后果：没有任何目录能达到 HighProbability（≥70）。** 即使真残留也上不去。

方向是安全的（不会误删），但可用性受损。

> 补充核验：`IsAssociationDataUnreliable`（`OrphanConfidenceEvaluator.cs:76-98`）只认
> `Failed ≥ 2` 和「注册表卸载项未完成」，**`Partial` 不计入**。
> 所以 `RiskUnreliableAssociationData = 80` 这一项在本轮**没有**触发 —— 42 条 High 另有原因。

### 4.2 `UserData` 证据触发率 83%，是 42 条 High 的主因

| 证据（负向） | 在 42 条 High 中的命中 |
|---|---|
| `UserData` | **35 / 42（83%）** |
| `DevelopmentProject` | 22 / 42 |
| `RecentUsage` | 13 / 42 |
| `RecentModification` | 11 / 42 |
| `RunningProcess` | 6 / 42 |

按权重可以复现分数：

- `RiskUserData = 45` + `RiskProtectedContent = 40` = **85**
- 再叠加 `RiskRecentlyModified = 40` 或 `RiskNoUninstaller = 10` → **95 / 100**
- 阈值 `RiskHighThreshold = 80`

即：**只要命中 UserData + 任一保护特征，就必然 High Risk → 必然 HighRiskReview。**

**这是本轮最重要的待验证项**：D 盘上 83% 的软件目录被判为"含用户数据"。软件安装目录下有 `config / ini / log / cache / 用户配置` 是常态，不等于用户个人数据。强烈提示 `DirectoryFactsDetector` 的用户数据判据在真实环境下**过度触发**。

⚠️ 但不能仅凭比例断言它是误判 —— 必须由人工回标确认（对应指标 M3「用户数据被错误推荐清理的数量」）。这是下一轮的核心工作。

### 4.3 「开发项目」识别 22/47，同样需要回标确认

47 条里 22 条 `DirectoryCategory = 开发项目`（47%）。D 盘是软件盘不是开发盘，这个比例偏高。可能原因：软件目录普遍含 `dll / pdb / json / node_modules / resources` 等结构特征，与 `DirectoryFactsDetector` 的开发项目判据重叠。

同样是**偏安全的失败**（受保护 → Review），但会显著减少可用结论。

### 综合结论

> **在真实环境下，GhostCleaner 本轮没有对任何一个目录给出「建议隔离」。**
> 47 条中 41 条 `HighRiskReview`、0 条 `Quarantine`。

三重因素叠加：
1. 计划任务 `Partial` → 置信度上限 65 → 无高概率残留
2. `UserData` 83% 触发 → Risk 必然 High
3. 保护/排除 26 条 → 大部分进 Review

方向上全部偏安全（**不会误删**），符合产品定位；但**产品可用性接近归零** —— 用户跑完扫描得不到任何可执行建议。这是 Core 0.2 必须优先解决的问题，且**必须在人工回标数据支撑下解决，不能靠调阈值让数字好看**。

---

## 5. 下一步

1. **人工回标**（最高优先级）：对 47 条逐条填 `groundTruth`，重点核对
   - `UserData` 命中的 35 条：到底几条真的含用户数据？（M3）
   - `开发项目` 的 22 条：几条真是开发项目？几条是普通软件？（M4）
   - 是否有真残留被漏掉（FN）
2. **补跑负样本**：`--include-all` 扫 `D:\Downloads`、空目录等 J 类，否则 TN 无法统计
3. **管理员轮次**：见下
4. 回标完成后再计算 M1–M9，**然后**才讨论是否调整 `EvidenceWeights`
   （严禁先调阈值再看指标）

---

## 6. 管理员轮次：需要你手动执行

### 6.1 首次提权尝试失败（2026-10-16）

手动跑了一次并输出到 `run-full-admin.jsonl`，但对比两轮数据可以确认**并未真正提权**：

| 指标 | 非管理员 | "管理员"轮次 | 判定 |
|---|---|---|---|
| `environment.runAsAdmin` | `False` | **`False`** | 未提权（硬证据） |
| 计划任务 | `Partial` 0 条 | `Partial` 0 条 | 无变化 |
| 运行中进程 | 193 条 | 195 条 | 仅自然波动，非提权带来 |

即：该轮等价于再跑一次非管理员扫描。必须先确认窗口真的持有提升令牌，再重跑。

**附带收获**：两轮 47 条样本的置信度序列完全一致（65/65/65/61/61/54/42/37/24/21/19/11/4/0/0）。
说明同权限下扫描结果**可复现**，验证数据可信。


UAC 提权弹窗出现在安全桌面，自动化无法代确认。请**右键「以管理员身份运行」PowerShell**（Win+X → 终端(管理员)），**先自检**：

```powershell
([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
```

必须回显 `True` 才算提权成功（账户属于 Administrators 组 ≠ 令牌已提升，UAC 点「否」就是 `False`）。确认为 `True` 后再跑：

```powershell
cd d:\文档\GitHub\GhostCleaner
tools\WinCleaner.Validate\bin\Release\net8.0-windows\WinCleaner.Validate.exe `
  --roots "D:\" --out validation\raw\run-full-admin.jsonl --commit 28fb8d2
```

跑完先自查两条：
- 输出里「正在运行的进程」条数是否明显多于 193（本轮非管理员基线）
- 结果行里 `计划任务` 是否不再是「不完整」

（不需要设 `DOTNET_ROOT` —— 运行只需 runtime，`C:\Program Files\dotnet` 有；SDK 只在 `dotnet build` 时需要。）

跑完把 `validation\raw\run-full-admin.jsonl` 留着，告诉我一声，我做两轮对比。重点看：

- 计划任务是否从 `Partial(0 条)` 变成 `Success`（有实际条数）
- 置信度上限是否从 65 回到 100
- 进程采集条数是否增加（本轮 193 条）

---

## 7. 本轮 Core 侧状态

- 构建：0 警告 0 错误
- 测试：108 通过 / 0 失败
- 未调整任何 `EvidenceWeights` 权重，未修改任何判定逻辑
- 只读，未删除/隔离/写入注册表

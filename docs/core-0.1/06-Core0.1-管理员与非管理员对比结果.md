# Core 0.1 真实环境：管理员 vs 非管理员对比结果

> 日期：2026-10-16
> 提交：`28fb8d2` + Core 0.1 验证环境改动 + `ScanOutcome`（均未提交）
> 样本：D 盘全量，同机同参数，两轮各 47 条
> 数据：`validation/raw/run-full-user.jsonl`（非管理员）、`run-full-admin.jsonl`（管理员）

---

## 1. 两轮总览

| 指标 | 非管理员 | 管理员 | 变化 |
|---|---|---|---|
| `runAsAdmin` | `False` | `True` | 提权生效 |
| **计划任务** | **Partial · 0 条** | **Success · 101 条** | ✅ 采集成功 |
| 运行中进程 | 193 条 | **356 条** | **+163（+84%）** |
| 引用总数 | 13255 | 13519 | +264 |
| 采集源健康度 | 8/9 | **9/9** | ✅ |
| **置信度上限** | **65** | **100** | ✅ 解除 |
| 高概率残留（≥70） | **0** | **5** | ✅ |
| 含 Warning 条目 | 47 | 23 | 计划任务警告消失 |

---

## 2. 关键结果：解除上限后，Recommendation 一个都没变

| Recommendation | 非管理员 | 管理员 |
|---|---|---|
| HighRiskReview | 41 | **41** |
| Review | 4 | **4** |
| Keep | 2 | **2** |
| **Quarantine** | **0** | **0** |

| Risk | 非管理员 | 管理员 |
|---|---|---|
| High | 42 | **42** |
| Medium | 4 | **4** |
| Low | 1 | **1** |

| ConfidenceLevel | 非管理员 | 管理员 |
|---|---|---|
| VeryHighProbability（≥90） | 0 | **3** |
| HighProbability（≥70） | 0 | **2** |
| Suspicious（40–69） | 7 | 3 |
| Normal（<40） | 40 | 39 |

**置信度天花板从 65 抬到 100、出现 5 个高概率残留，但建议分布一字未变，`Quarantine` 仍然是 0。**

原因：`Quarantine` 的触发条件是 `VeryHighProbability && Risk == Low`（`RiskEvaluator.cs:203-206`）。
3 条 100 分样本的 Risk 分别是 High / Medium / Medium，**没有一条 Risk 为 Low**；
而唯一 Risk=Low 的条目置信度只有 32（Normal）→ 走 `Keep`。

> **结论：瓶颈已从「置信度上限」完全转移到「Risk 判定」。**
> 上一轮推测的两个瓶颈（计划任务 cap 65、UserData 拔高 Risk）中，
> 前者已由本轮证明可通过提权解除，后者**不受权限影响**，是真正的卡点。

---

## 3. 五个发现

### 3.1 计划任务 `Partial` 确证为纯权限问题（已闭环）

非管理员下 `C:\WINDOWS\System32\Tasks` 枚举被 `UnauthorizedAccessException` 拒绝，
采集器 `catch { return list; }` 吞掉 → 0 条 → 标 `Partial`。
管理员下采到 **101 条**，`Status = Success`。

判定口径（`0 条 → Partial`）本身**是对的**，不需要改成 `Success`。
真正要修的是采集器上报异常原因（Core 0.2），让用户知道「以管理员运行即可补全」。

### 3.2 非管理员下少采近一半进程 → 削弱「运行中软件」保护

进程采集 193 → 356（+84%）。这意味着**未提权时，「正在运行的软件」这一最强保护证据会大面积失效**：

- 非管理员：6 个目录命中 `RunningProcess` 证据
- 管理员：8 个

未命中的目录会拿到 `NoRunningProcess(+12)` 正向残留证据。

**产品含义**：未提权运行时，正在使用但未被采到的软件更容易被判为残留。
建议 UI 在未提权时**禁止或强警告执行隔离/清理**（目前只有黄底横幅提示）。这条已可同步给 UI。

### 3.3 `UserData` 触发率 83%，且完全不受权限影响

两轮都是 **39/47（83%）** 命中 `UserData`（纯目录结构判定，与进程/权限无关）。

按权重可复现 Risk：
- `RiskUserData = 45` + `RiskProtectedContent = 40` = **85** ≥ `RiskHighThreshold(80)` → High
- 再叠加 `RiskRecentlyModified(40)` 或 `RiskNoUninstaller(10)` → 95 / 100

即：**命中 UserData + 任一保护特征 ⇒ 必然 High Risk ⇒ 必然 HighRiskReview。**

⚠️ 不能仅凭 83% 断言误判 —— 软件目录下的 `config / log / cache` 是否算"用户数据"必须由人工回标裁定（指标 M3）。但这是 Core 0.2 的**第一优先级**。

### 3.4 三条 100 分样本都含卸载器 —— 首要回标对象

| ID | 路径 | 置信 | 风险 | 建议 |
|---|---|---|---|---|
| U-001 | `D:\Weixin` | 100 | High/85 | HighRiskReview |
| U-002 | `D:\wangyiyunyinyue` | 100 | Medium/45 | Review |
| U-003 | `D:\Registry Repair` | 100 | Medium/45 | Review |

证据构成（三条完全相同）：

```
正向：SoftwareDirectory+8  Executable+10  Library+5  Uninstaller+25
      VersionResource+10  Publisher+10
      MissingUninstallEntry+20  MissingAppPath+8  MissingShortcut+8
      MissingService+8  MissingScheduledTask+6  MissingStartupEntry+6
      NoRunningProcess+12                          合计 +136
负向：UserData -30
净分 106 → clamp 100
```

**值得警惕的点**：这三条都命中 `Uninstaller(+25)`，即目录里**自带卸载程序**。
一个自带卸载器、有版本资源、有厂商信息的目录被判 **100% 残留**，FP 风险很高。

可能的解释（需人工裁定）：
- 软件新版装在别处（如 C 盘），D 盘这里是旧残留 → 判对（TP）
- 软件就装在这里、日常在用，只是注册表卸载项缺失/指向别处 → 严重误报（FP）

### 3.5 Footprint 系数导致「越像正规安装，越容易被判残留」

`U-004 D:\T20`（73 分）**没有** `Uninstaller` 证据，缺失类证据被 Footprint 系数衰减：
`MissingUninstallEntry 20→13`、`MissingAppPath 8→5`、`MissingService 8→5`…

而 U-001~003 有卸载器 → Footprint 高 → 缺失证据**全额计入** → 100 分。

设计初衷是「绿色软件不应因缺失关联得高分」，实际效果却是
**「安装痕迹越完整的正规目录，缺失关联时得分越高」**。
逻辑上说得通（重装系统后这正是典型残留），但配合 3.4 的卸载器现象，需要在回标后重新评估系数是否合理。

---

## 4. 综合判断

> 即使完全提权、置信度跑满，**GhostCleaner 本轮仍未对任何目录给出「建议隔离」**。

- 方向仍然安全（0 个 Quarantine = 不会误删），符合产品定位
- 但**产品可用性仍接近归零**：47 条里 41 条「高风险人工复核」，用户得不到可执行建议
- 卡点已精确定位：**`UserData` 判据（83% 触发）+ Risk 阈值联动**，而非置信度上限

---

## 5. 下一步（按优先级）

1. **人工回标 47 条**，优先这三类：
   - U-001/002/003（100 分且自带卸载器）—— 判定 TP 还是 FP，直接决定 FP 率
   - `UserData` 命中的 39 条 —— 到底几条真的含用户个人数据（M3）
   - `开发项目` 的 22 条 —— 几条真是开发项目（M4）
2. **补负样本**：`--include-all` 扫 `D:\Downloads`、空目录等 J 类，否则 TN 无法统计
3. 回标完成后计算 M1–M9，**然后**才讨论是否调整 `EvidenceWeights`
   （严禁先调阈值再看指标）
4. 同步给 UI：未提权时禁止/强警告执行清理（见 3.2）

---

## 6. 本轮状态

- 构建：0 警告 0 错误；测试：108 通过 / 0 失败
- 未调整任何权重与判定逻辑
- 两轮均为只读，未删除/隔离/写入注册表
- 副产品结论：同权限下两次扫描置信度序列完全一致（65/65/65/61/61/54/42/…），
  **扫描结果可复现**

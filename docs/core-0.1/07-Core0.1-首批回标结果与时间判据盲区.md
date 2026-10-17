# Core 0.1 首批回标结果与「时间判据盲区」

> 日期：2026-10-16
> 数据来源：管理员轮次 `validation/raw/run-full-admin.jsonl`（47 条）
> 已回标：3 条（置信度最高的三条）→ `validation/raw/labels-draft.jsonl`
> 待回标清单：`validation/raw/to-label.md`（本地，含真实路径，不入库）

---

## 1. 首批回标结果

| ID | 路径 | Core 置信 | 人工真值 | 把握 | 判定 |
|---|---|---|---|---|---|
| U-001 | `D:\Weixin` | **100** | **ActiveSoftware** | Certain | **FP（误报）** |
| U-002 | `D:\wangyiyunyinyue` | **100** | **ActiveSoftware** | Certain | **FP（误报）** |
| U-003 | `D:\Registry Repair` | **100** | ConfirmedOrphan | Likely | TP（判对） |

**置信度最高的 3 条里 2 条是误报，FP 率 67%。**

用户原话：Weixin / wangyiyunyinyue「日常在使用，是我之前重新安装电脑后的 D 盘残留项目，然后有软件更新之后就继续使用了」；Registry Repair「不是日常使用的软件」。

⚠️ 样本仅 3 条，不足以计算全局 FP 率。但**这 3 条恰恰是置信度最高的样本**，在清理工具里这等于「最可能被用户采纳的建议全是错的」，严重性远高于均值。

---

## 2. 根因：时间判据存在 31~179 天的盲区

### 2.1 判定逻辑

`EvidenceCollector.cs:305-327`：

```csharp
var days = (DateTime.Now - info.LastModified).TotalDays;

if (days >= w.LongUnmodifiedDays)            // LongUnmodified +12（正向残留）
else if (days <= w.RecentModificationDays)   // RecentModification -30（负向）
else if (days <= w.RecentUsageDays)          // RecentUsage -15（负向）
// 其余区间：不产生任何时间证据
```

`EvidenceWeights.cs`：

| 参数 | 值 |
|---|---|
| `RecentModificationDays` | **7** |
| `RecentUsageDays` | **30** |
| `LongUnmodifiedDays` | **180** |

`info.LastModified` = 目录内最新写入时间（`SoftwareDirectoryInspector.cs:25` 取目录 mtime，遍历中更新为最新文件时间，`:163` 写回）。

### 2.2 三条样本全部落在盲区

| 目录 | 最后修改 | 距今天数 | 落在区间 | 时间证据 |
|---|---|---|---|---|
| `D:\Weixin` | 2026-09-12 | **34** | 30 < 34 < 180 | **无** |
| `D:\wangyiyunyinyue` | 2026-06-13 | 125 | 30 < 125 < 180 | **无** |
| `D:\Registry Repair` | 2026-06-14 | 124 | 30 < 124 < 180 | **无** |

**31~179 天之间不产生任何时间证据。** 于是「每天在用的微信」和「一年没碰的 Registry Repair」拿到**完全相同**的证据集：

```
正向：SoftwareDirectory+8  Executable+10  Library+5  Uninstaller+25
      VersionResource+10  Publisher+10
      MissingUninstallEntry+20  MissingAppPath+8  MissingShortcut+8
      MissingService+8  MissingScheduledTask+6  MissingStartupEntry+6
      NoRunningProcess+12                           合计 +136
负向：UserData -30
净分 106 → clamp 100
```

两条 FP 与一条 TP **分数一模一样**，Core 完全无法区分。

### 2.3 被忽略的强信号

实测 `D:\Weixin`：

- 目录 mtime 2026-09-12（34 天前）
- **90 天内有 19 个文件被修改**
- 目录内最新文件 2026-09-12

即：明确的「仍在使用」信号客观存在，只差 4 天就跨过 `RecentUsageDays = 30` 阈值。
而即便跨过，`RecentUsagePenalty = -15` 也只能把 100 压到 85 —— 仍是 VeryHighProbability，**不足以纠偏**。

---

## 3. 深层含义：重装场景下「缺失卸载项」失去区分度

这正是 GhostCleaner 的核心场景：重装系统后，D 盘**所有**软件目录都会缺失注册表卸载项。

- `MissingUninstallEntry(+20)` 在本机上是**普遍现象**，不能区分「真废弃」和「重装后继续用」
- 唯一有区分度的是**近期活动信号**，而它在 31~179 天完全缺失
- 叠加 §2.2 的盲区 → 在用软件与真残留拿到相同满分

> **结论：当前模型在「重装后继续使用」这一类样本上系统性误报，而这正是产品主场景。**

---

## 4. Core 0.2 候选修复方向（**现在不改权重**）

按优先级，需更多回标数据支撑后才能动手：

1. **消除盲区**：时间判据改为连续分段（≤7 / ≤30 / ≤90 / ≤180 / >180），任何天数都产出证据
2. **改用「近期活动文件数」而非单个 max mtime**：Weixin 有 19 个文件在 90 天内修改，比单点 mtime 稳健得多
3. **提高近期活动证据权重**：`RecentUsagePenalty = -15` 相对 +136 的正向分太弱，无法纠偏
4. **重新评估 `MissingUninstallEntry` 在重装场景下的区分度**：可能需要与「是否有卸载器」联合判定
   （三条样本都命中 `Uninstaller(+25)` —— 自带卸载器的目录被判 100% 残留本身就很可疑）

严格遵守：**先完成回标 → 计算 M1–M9 → 再决定是否调权重**。不得先调阈值再看指标。

---

## 5. 下一步

1. **继续回标**：`validation/raw/to-label.md` 已列全部 47 条（含路径、Core 判断、是否有卸载器），
   只需在最后两列填真值与把握。优先回标置信度 ≥40 的样本。
2. **补 J 类负样本**：`--include-all` 扫 `D:\Downloads`、空目录等，否则 TN 无法统计
3. 回标完成后计算 M1–M9，再进入第 4 节的权重讨论

---

## 6. 本轮状态

- 构建 0 警告 0 错误；测试 108 通过 / 0 失败
- **未调整任何权重与判定逻辑**（含本轮发现的盲区，按流程留到回标后处理）
- 全程只读，未删除/隔离/写入注册表

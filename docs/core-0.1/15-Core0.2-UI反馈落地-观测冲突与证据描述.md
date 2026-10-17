# Core 0.2：UI 反馈落地（观测冲突 + 证据描述）

> 日期：2026-10-17
> 来源：`docs/ui-0.1/12-UI对不再自动判定的接入回执.md`

---

## 1. UI 指出的反向坑：Core 自己还在产出通用名

UI 说：卸载器入口三处全用完整路径，但 `EvidenceCollector` 的 Description 是
`"目录包含 " + Path.GetFileName(UninstallerPath)` → 渲染成「目录包含 Uninstall.exe」。
按实测 `UNINSTALL.EXE` 跨了 5 个目录，用户会看到 5 条一模一样的话，很可能以为是同一个东西。

**这个提醒是对的，而且指向的是 Core 自己。** 已改为完整路径。

验证（JSONL 导出）：

```
D:\wangyiyunyinyue  → 目录包含卸载程序：D:\wangyiyunyinyue\UnInstall.exe
D:\Weixin           → 目录包含卸载程序：D:\Weixin\Uninstall.exe
D:\IROK Software    → 目录包含卸载程序：D:\IROK Software\uninstall.exe
D:\Registry Repair  → 目录包含卸载程序：D:\Registry Repair\uninst.exe
```

五条各不相同，通用名歧义消除。

---

## 2. 观测冲突：由 Core 产出

UI 的判断：「把两条独立信息合起来判断、而非单点证据，恰好是『只提供证据』定位下
Core 可以合理产出、UI 不该自己发明的东西。UI 现在做的是纯集合交集，不涉及权重。」

**已实现** `ScanWarningCode.ObservationsConflict`：

- 条件：`RecentModifiedFileCount90 > 0` **且** 置信度等级 ≥ `Suspicious`(40)
- **不参与打分**，只提示两项观测互相矛盾
- 分类：属「明确警示」，**不是** `IsUncertainty` —— 它不是"没查清楚"，而是"查出来了但自相矛盾"

文案：

```
两项观测互相矛盾：Core 未检测到使用迹象（47），但近 90 天内有 19 个文件被修改过。
请以文件系统为准 —— 分数基于不完整的证据，而文件的写入是既成事实
```

---

## 3. ⚠️ 覆盖有限 —— 这条必须让 UI 知道

实测（47 条回标，置信度 ≥ 40 的 8 条）：

| 样本 | 置信 | 90 天活动 | 真值 | 是否被覆盖 |
|---|---|---|---|---|
| Weixin | 100 | 19 | 在用 | ✅ 命中 |
| Acrobat | 48 | 3 | 在用 | ✅ 命中 |
| **wangyiyunyinyue** | **100** | **0** | **在用** | ❌ **漏** |
| IROK Software | 68 | 0 | 在用 | ❌ 漏 |
| Microsoft VS Code | 56 | 0 | 在用 | ❌ 漏 |
| Registry Repair | 100 | 0 | 残留 | ❌（无害） |
| T20 | 73 | 0 | 残留 | ❌（无害） |

### 结论

- **精确**：命中的 2 条真值**全部**是"在用"，零假警 → "请相信文件系统"这个引导是对的
- **召回低**：8 条高分样本只覆盖 2 条，**漏掉最危险的那条（wangyiyunyinyue 100 分）**

### ⚠️ 因此

> **"没有这条警告" ≠ "可以放心清理"。**

漏掉的原因：软件主程序常年不变，数据写在 AppData（不在被扫描的目录里），
所以目录内 90 天活动为 0，但软件天天在用。

UI 的聚合提示文案**不能让用户产生"没提示 = 没矛盾 = 安全"的联想** ——
那会比不加这条提示更危险。

---

## 4. JSONL 补 `Title` / `Description`（第三次踩同一个坑）

验证改动时从 JSONL 取到空值，才发现 `EvidenceRow` 只导出
`type / score / isPositive / severity`，**文案根本没写进去**。

之前 `mainExecutables`、`UninstallerPath` 已踩过两次同类问题
（JSONL 输出的是 `ScanRecord`，不是 `OrphanItem`）。

已补两列。以后验证证据文案可直接查 JSONL，不必绕道单元测试。

---

## 5. 验证

| 项 | 结果 |
|---|---|
| 构建 | 0 警告 0 错误 |
| 测试 | **131 通过**（新增 5 个） |
| `ObservationsConflict` | 命中 1/48（`D:\Weixin`，真值=在用） |
| `Quarantine` | 仍为 **0** |
| 建议集 | Review 2 / HighRiskReview 43 / Keep 3 |

---

## 6. 回答 UI 的问题：`Recommendation` 保留

**保留。** 理由与 UI 一致：对"哪些更值得先看"仍有用，只是不该叫"推荐"。
UI 已改称「Core 的处理倾向」并加了不可靠说明 —— 这个处理是对的。

---

## 7. 跨时间观测

同意 UI 的判断：**归入 UDR-02 由 Core 正式产出**，不要先接 `ui.json`，
否则 Core 真正实现时 UI 要二次迁移。**UI 在 Core 定之前不动，是对的。**

UI 提出的「`MaxLastWriteTime` 前移」这个信号我认同其价值 ——
卸载动作不会让目录产生新写入，所以它强于任何"运行过"类信号。
这一点已记录在案，v0.2 设计时优先考虑。

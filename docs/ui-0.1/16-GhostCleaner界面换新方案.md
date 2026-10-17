# GhostCleaner 界面换新方案

> 日期：2026-10-17
> 依据：`docs/ui-0.1/01`~`15` 全部规范、`prototype/index.html` 原型、`15-UI-0.2-开发交接.md`
> 范围：① 品牌与语言统一为 GhostCleaner ② 界面按设计方案补齐换新

---

## 1. 名称统一

### 1.1 已完成（本轮已落地代码，测试 138 通过、build 0 错误）

用户可见文案中已无 `WinCleaner`：

| 位置 | 改动 |
|---|---|
| 窗口标题 / 侧栏品牌 / 关于页 | 已是 GhostCleaner（此前已统一） |
| 系统还原点描述（`CleanupService`） | 「WinCleaner 清理前还原点」→「GhostCleaner 清理前还原点」 |
| 导出文件名 ×3（日志 / 隔离区 / 扫描结果） | `WinCleaner-*` → `GhostCleaner-*` |
| 日志文件前缀 | `wincleaner-` → `ghostcleaner-` |

### 1.2 不能直接改名的地方（已做兼容处理）

| 位置 | 直接改名的后果 | 处理方式 |
|---|---|---|
| 数据目录 `%LOCALAPPDATA%\WinCleaner\Data` | **隔离区文件"凭空消失"** —— 里面是用户还没决定要不要永久删除的文件 | 新目录 `GhostCleaner` 优先；旧目录存在且新目录不存在时**继续沿用旧目录** |
| 日志文件前缀 `wincleaner-*.jsonl` | 升级后历史日志读不到 | 新日志用 `ghostcleaner-`；读取时**两种前缀都认** |

> 这两处不是"改名"，是"改名 + 兜底"。只改一半会让老用户丢数据。

### 1.3 待决策：命名空间（`UDR-11`，P1）

`WinCleaner.*` → `GhostCleaner.*`：7 个项目 + 全部 namespace + `.sln` + `.csproj` + 启动脚本。

- **用户不可见**，不影响界面
- 成本：大范围重命名，属独立改动
- 建议：**与界面换新分开做**，避免一次改动里既动视觉又动结构

---

## 2. 现状盘点：实现 vs 原型

原型 9 个页面，实现 8 个：

| 原型页面 | 实现 | 状态 |
|---|---|---|
| `page-dashboard` | `DashboardView` | ✅ |
| `page-scan` | `ScanSetupView` | ✅ |
| `page-scanning` | `ScanProgressView` | ✅ |
| `page-complete` | `ScanCompleteView` | ✅ |
| `page-results` | `ResultsView` | ✅ |
| `page-quarantine` | `QuarantineView` | ✅ |
| `page-history` | — | ❌ **缺失** |
| `page-settings` | `SettingsView` | ✅ |
| `page-about` | `AboutView` | ✅ |

流程四页面（配置 → 扫描中 → 完成 → 结果）由 `OrphanScanView` 按 `Stage` 切换，与设计的拆分一致。

骨架（侧栏 232 / 1280×840 / 最小 1080×680 / 内容最大宽 1400 / 品牌 logomark）已与设计吻合，
Token 化与 Light/Dark 主题已落地（`Theme.Light` / `Theme.Dark` + `DynamicResource`）。

---

## 3. 换新项清单

### P0-1 扫描记录页 `HistoryView`（原型 `page-history`）

- 依赖 `UDR-02`（`IScanHistoryService`）
- **降级方案（设计已给出）**：扫描记录页只读操作日志，`LogView` 降级为其中一段；「查看结果」隐藏；概览「上次扫描」从日志派生

### P0-2 Toast（当前 **0 个文件**，完全未实现）

- 验收标准要求：「隔离成功后 Toast 可直达隔离区」
- 需新建 `IToastService` + Toast 控件
- 状态：`Success / Warning / Danger / Info`，**Danger 不自动消失**
- 这是当前最直观的缺口 —— 隔离成功是用户最需要即时反馈的动作，目前没有任何反馈

### P1 Dialog 替换 MessageBox

- 现况：`DialogService.cs`（6 处）、`App.xaml.cs`（4 处）仍用 `MessageBox`
- 目标：统一 Dialog，`Normal / Danger` 两态；**Danger 遮罩点击与 Esc 不关闭**

### P2 遗留页面硬编码颜色

- `GeneralCleanView`（1 处）、`RegistryIssuesView`（2 处）
- 二者已按设计「从导航隐藏，代码保留」，用户看不到
- 建议不动；若要恢复这些页面，再改用 Token

> 说明：`Theme.Light/Dark`、`Colors.xaml` 里的十六进制是 **Token 定义**，属允许范围，不算硬编码。

### P2 命名空间统一

见 §1.3。

---

## 4. 实施顺序

```
1. Toast            （依赖最少，收益最直观：隔离成功有反馈）
2. HistoryView      （先按降级方案做，UDR-02 到位后再接真实数据）
3. Dialog 替换 MessageBox
4. 命名空间统一      （独立改动，与上面三项分开提交）
```

**先做 Toast 而不是先做 HistoryView**：后者依赖 Core 的 `UDR-02`，
降级方案虽可行但价值有限；Toast 完全不依赖 Core，且直接补上验收标准里的一条硬指标。

---

## 5. 验收

对照 `15-UI-0.2-开发交接.md` §11 的验收清单，本轮方案覆盖其中：

- [x] 隔离成功后 Toast 可直达隔离区 ← **P0-2**
- [x] 扫描记录页（含操作日志） ← **P0-1**
- [x] 无 `MessageBox` ← **P1**
- [x] 命名空间 / 显示名统一为 GhostCleaner ← **P2**

---

## 6. 实施结果（已全部完成）

| 项 | 状态 | 说明 |
|---|---|---|
| 名称统一（可见文案） | ✅ | §1.1，含还原点描述与导出文件名 |
| 数据目录 / 日志前缀兜底 | ✅ | §1.2，旧目录与旧前缀仍可读，老用户数据不丢 |
| P0-2 Toast | ✅ | 新建 `IToastService` / `ToastService` / `ToastHost`；隔离成功后带「查看隔离区」直达 |
| P0-1 扫描记录页 | ✅ | **早已按降级方案落地** —— `LogView` 就是扫描记录页（标题「扫描记录」+ UDR-02 说明俱在），导航 `History` 已指向它，无需另建 |
| P1 Dialog 替换 MessageBox | ✅ | 新建 `MessageDialogWindow`；Danger 态遮罩点击与 Esc 均不关闭 |
| P2 命名空间统一 | ✅ | 7 个项目目录 + sln + csproj + 152 个文件全量替换 |

验证：`GhostCleaner.sln` 构建 **0 错误**，测试 **138 通过**，
输出程序集 `GhostCleaner.App.exe` / `GhostCleaner.Validate.exe`。

### 设计上的两个要点

**Toast 容器必须穿透点击**：Toast 浮在整窗之上，若容器接收命中测试会把下方所有交互挡死。
因此 `ToastHost` 根容器 `IsHitTestVisible="False"`，只让卡片本身可点击。

**Danger 对话框不能被随手带过**：点遮罩不关闭、Esc 不关闭，
危险确认必须由用户显式点按钮作出选择。

### 未改动

`docs/` 下的历史决策文档仍保留 `WinCleaner` 称谓 —— 它们是当时的决策记录，
改写会让历史失真。`app/Data/Logs/` 下的 `wincleaner-*.jsonl` 同样保留，
由 §1.2 的兼容逻辑继续读取。

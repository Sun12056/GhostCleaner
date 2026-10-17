# GhostCleaner UI 0.1 — 设计交付

> 阶段目标：**完成 GhostCleaner 从信息架构到视觉设计、Design System、页面原型的完整 UI/UX 设计**。
> 本阶段主要进行设计，**不修改 GhostCleaner 业务代码**。
> UI 0.2 将直接根据本文档进入 WPF 实现。

## 快速开始

```powershell
# 启动本地服务器后打开（推荐）
cd docs\ui-0.1\prototype
npx http-server -p 5771 -c-1
# 浏览器打开 http://127.0.0.1:5771/index.html
# 也可直接双击 index.html（图标已内联，file:// 下同样可用）
```

原型支持：`#/dashboard`、`#/scan`、`#/scanning`、`#/complete`、`#/results`、`#/quarantine`、`#/history`、`#/settings`、`#/about`
右上角按钮或「设置 → 外观」可切换 Light / Dark / 跟随系统。

---

## 交付物索引

### 设计流程（Phase 1–15）

| 文件 | 内容 |
|---|---|
| `01-环境与设计工具检查.md` | Skills / Figma / MagicPath / tldraw 可用性与替代策略 |
| `02-现有项目审查.md` | 现有 WPF UI 技术栈、8 个页面、20 个问题、**11 条 UI Data Requirement** |
| `03-产品定位.md` | 一句话定义、三条不可违背的产品原则、设计红线 |
| `04-信息架构.md` | 最终 IA（7 页）、导航结构、页面主任务、尺寸骨架 |
| `05-用户流程.md` | 5 条流程图（Mermaid）+ 扫描状态机 |
| `06-视觉方向探索.md` | 5 个方向对比打分 → **最终方向：Evidence Instrument（证据仪器）** |
| `07-DesignTokens.md` | 颜色 / 字体 / 间距 / 圆角 / 阴影 / 动效 / 无障碍对比度 |
| `08-图标系统.md` | 24 栅格线性图标规范 + 38 个图标清单 + 空状态插画 |
| `09-组件规范.md` | 28 个组件的尺寸 / 变体 / 全状态 / token |
| `10-页面设计.md` | 9 个页面的逐区块规格 + 跳转矩阵 |
| `11-状态设计.md` | Empty / Loading / Error / Permission / Protected / Partial / Success |
| `12-DarkMode.md` | 同 token 双值的暗色方案 + WPF 实现要点 + 验收清单 |
| `13-动效规范.md` | 时长曲线表 + 逐组件动效 + 禁止清单 + WPF 映射 |
| `14-UX-Review.md` | 验收对照、16 个问题与建议、5 个待 Core 决策项 |
| `15-UI-0.2-开发交接.md` | 文件结构、改造映射、token 映射、图标迁移、UDR、实现顺序 |

### 资产

```
assets/
├── brand/
│   ├── logomark.svg          主标记（盾形描边 + 抽象幽灵）
│   ├── logomark-solid.svg    小尺寸简化版（16 / 20 px）
│   ├── app-icon.svg          Windows 应用图标 256
│   └── wordmark.svg          横版字标
└── icons/
    └── gc-icons.svg          38 个图标 Sprite
```

### 原型

```
prototype/
├── index.html                全部 9 个页面（可点击、Hash 路由）
├── css/tokens.css            Design Tokens（Light / Dark）
├── css/components.css        28 个组件
├── css/pages.css             Shell + 页面布局
└── js/app.js                 路由 / 主题 / 结果列表 / 详情 / 对话框 / Toast
```

---

## 三个最重要的决定

1. **把「证据」做成视觉本身。** 最终视觉方向是 Evidence Instrument —— 中性灰阶 + 单一靛蓝，卡片用 1px 边框不用阴影，信息密度中高，让用户第一眼看到"依据"而不是"分数"。

2. **疑似残留度与清理风险用两套完全不同的视觉编码。**
   - 疑似残留度 = 靛蓝强度条 + 百分比（单一色相，绝不出现绿/红）
   - 清理风险 = 语义色低强度徽章
   允许「残留度 92% + 风险高」并存 —— 这是 GhostCleaner 与普通清理工具的分水岭。

3. **把一条流程拆成四个页面。** 现有 `OrphanScanViewModel`（703 行）把配置/进度/结果/详情/清理塞在一起。UI 0.1 拆为：残留扫描（配置）→ 扫描中 → 扫描完成 → 扫描结果（+详情面板）。

---

## 与 Core 0.1 的同步

UI 0.1 已消费 Core 0.1 全部已有模型（`OrphanItem` / `Evidence` / `OrphanAssessment` / `QuarantineEntry` / `ScanOptions` / `ScanProgress` …），**未发明任何新概念**。

登记给 Core 的需求：**11 条 UDR**（`02` §5 与 `15` §7）。其中三条 P0：

| ID | 需求 | 缺失时 UI 0.2 的降级方案 |
|---|---|---|
| `UDR-01` | 扫描暂停 / 恢复 | 隐藏暂停按钮，步骤条退化为单阶段 |
| `UDR-02` | 扫描结果持久化（扫描记录） | 扫描记录页只读操作日志，「查看结果」隐藏 |
| `UDR-03` | 进度真实总量（修 `ProgressValue` 分母错误） | 改用不确定进度条，只显示「已检查 N 个目录」 |

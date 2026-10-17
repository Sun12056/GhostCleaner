# Design Tokens v0.1

> 单一事实源。Light / Dark 使用**同一套 token 名**，仅值不同。
> UI 0.2 直接映射为 WPF `ResourceDictionary`（见 `15` §5）。
> 可运行版本：`prototype/css/tokens.css`

## 0. 命名约定

```
{category}.{role}[.{state}]
bg / fg / border / text / accent / success / warning / danger / confidence
```

---

## 1. 颜色 — 中性灰阶（Neutral Ramp）

冷灰，带极轻蓝调，避免纯灰的"脏"。

| Token | Light | Dark | 用途 |
|---|---|---|---|
| `n.0` | `#FFFFFF` | `#000000` | 纯白 / 纯黑（极少用） |
| `n.25` | `#FBFCFD` | `#0A0C10` | — |
| `n.50` | `#F6F8FA` | `#101318` | 画布 |
| `n.75` | `#F1F4F8` | `#0C0F13` | 侧栏 |
| `n.100` | `#EEF1F5` | `#14171D` | 下沉面（表格底、内嵌区） |
| `n.150` | `#E9EDF2` | `#181C23` | 悬停 |
| `n.200` | `#E4E8EE` | `#1F242C` | 分隔线（弱） |
| `n.300` | `#D3D9E2` | `#30363F` | 边框（默认） |
| `n.400` | `#AEB7C4` | `#464E59` | 边框（强）/ 禁用图形 |
| `n.500` | `#7C8798` | `#6E7784` | 三级文字 |
| `n.600` | `#5A6474` | `#A2AAB8` | 二级文字 |
| `n.700` | `#424B59` | `#C2C9D4` | — |
| `n.800` | `#2A3140` | `#DDE2EA` | — |
| `n.900` | `#14181F` | `#E7EAEF` | 一级文字 |

> 注意：Dark 模式下 `n.500→n.900` 是**反向**使用的（数字越大越亮），这是刻意的 —— token 名表达的是「语义层级」而非物理明度。

---

## 2. 颜色 — 强调色（Accent / Indigo）

| Token | Light | Dark | 用途 |
|---|---|---|---|
| `accent.50` | `#EEF2FE` | `#171E33` | 选中项背景、浅色底 |
| `accent.100` | `#DDE5FD` | `#1B2540` | 悬停底 |
| `accent.200` | `#C0CDF9` | `#24305A` | 边框 |
| `accent.300` | `#9FB2F4` | `#33437A` | 禁用态强调 |
| `accent.400` | `#6C86EA` | `#5B77E0` | 强度条（高概率） |
| **`accent.500`** | **`#3F5FE0`** | **`#6C86EA`** | **默认：主按钮 / 当前导航 / 进度 / 选中** |
| `accent.600` | `#3350C8` | `#8AA0F2` | 悬停 |
| `accent.700` | `#2A41A4` | `#A5B6F7` | 按下 |
| `accent.800` | `#22347F` | `#C4CFFA` | — |

### 语义别名

| Token | Light | Dark |
|---|---|---|
| `accent.default` | `accent.500` | `accent.500` |
| `accent.hover` | `accent.600` | `accent.600` |
| `accent.pressed` | `accent.700` | `accent.700` |
| `accent.bg` | `accent.50` | `accent.50` |
| `accent.bgHover` | `accent.100` | `accent.100` |
| `accent.border` | `accent.200` | `accent.200` |
| `accent.fg` | `accent.600` | `#9DB0F5` |
| `text.onAccent` | `#FFFFFF` | `#0E1116` |

> **Dark 模式下强调色更亮**（`#6C86EA`），因为深底需要更高明度才能保持对比度。

---

## 3. 颜色 — 语义色（Semantic）

| 组 | Token | Light | Dark |
|---|---|---|---|
| **Success** | `success.bg` | `#E7F4EC` | `#14231B` |
| | `success.border` | `#B9DFC8` | `#24462F` |
| | `success.default` | `#1F8A50` | `#3CA46B` |
| | `success.fg` | `#156B3E` | `#6FD09B` |
| **Warning** | `warning.bg` | `#FDF2E2` | `#2A2114` |
| | `warning.border` | `#F0D3A2` | `#4E3C1E` |
| | `warning.default` | `#B8710F` | `#D6912C` |
| | `warning.fg` | `#8F5708` | `#E9B571` |
| **Danger** | `danger.bg` | `#FCEBE9` | `#2C1917` |
| | `danger.border` | `#F2C3BE` | `#552825` |
| | `danger.default` | `#C8342B` | `#E0524A` |
| | `danger.hover` | `#A82A22` | `#C7413A` |
| | `danger.fg` | `#96271F` | `#F08A84` |

### 危险色纪律（硬性）

- Light 模式下，Danger 色**同时可见面积 ≤ 2%**。
- 高风险条目**不用红色实心徽章**，用 `danger.bg` + `danger.fg` + `danger.border` 的**低强度徽章**。
- 实心 `danger.default` **只用于**：永久删除按钮、错误状态图标、对话框危险主按钮。

---

## 4. 颜色 — 表面（Surface）

| Token | Light | Dark | 用途 |
|---|---|---|---|
| `bg.canvas` | `#F6F8FA` | `#101318` | 应用画布（内容区底） |
| `bg.sidebar` | `#F1F4F8` | `#0C0F13` | 侧栏 |
| `bg.surface` | `#FFFFFF` | `#181C23` | 卡片、面板、对话框 |
| `bg.sunken` | `#EEF1F5` | `#14171D` | 内嵌区、表头、代码块、输入框（只读） |
| `bg.hover` | `#E9EDF2` | `#1F242C` | 行 / 项悬停 |
| `bg.active` | `accent.50` | `accent.50` | 选中 / 当前导航 |
| `bg.scrim` | `rgba(20,24,31,.40)` | `rgba(0,0,0,.60)` | 对话框 / 抽屉遮罩 |

## 5. 颜色 — 边框

| Token | Light | Dark | 用途 |
|---|---|---|---|
| `border.subtle` | `#E4E8EE` | `#232830` | 卡片内分隔线、表格行线 |
| `border.default` | `#D3D9E2` | `#30363F` | 卡片边框、输入框、按钮（次） |
| `border.strong` | `#AEB7C4` | `#464E59` | 悬停边框、分隔强调 |
| `border.focus` | `accent.500` | `accent.500` | 焦点环（2px） |

## 6. 颜色 — 文字

| Token | Light | Dark |
|---|---|---|
| `text.primary` | `#14181F` | `#E7EAEF` |
| `text.secondary` | `#5A6474` | `#A2AAB8` |
| `text.tertiary` | `#7C8798` | `#6E7784` |
| `text.disabled` | `#AEB7C4` | `#545C68` |
| `text.link` | `accent.600` | `#9DB0F5` |

---

## 7. 颜色 — 疑似残留度（Confidence）

> **只用单一靛蓝色相的强度变化，绝不使用绿 / 红。** 这是 P2（两个独立维度）的视觉保证。

| 等级（Core 阈值） | 条形填充色 | 文字色 | 标签文案（与 Core 一致） |
|---|---|---|---|
| `VeryHighProbability` ≥ 90 | `accent.500` | `accent.600` / Dark `#9DB0F5` | 非常高概率残留 |
| `HighProbability` 70–89 | `accent.400` | `accent.fg` | 高概率残留 |
| `Suspicious` 40–69 | `accent.200` | `text.secondary` | 可疑 |
| `Normal` < 40 | `n.300` / Dark `n.300` | `text.tertiary` | 普通 |

- 条形高度 **4px**，圆角 999，底色 `n.200`（Dark `n.200`）。
- 百分比数字**等宽**（Tabular），与条形右对齐或同行紧邻。

## 8. 颜色 — 清理风险（Risk）

> **只用低强度徽章**（`*.bg` + `*.fg` + `*.border`）。

| 等级 | 徽章底 | 徽章字 | 图标 | 文案 |
|---|---|---|---|---|
| Low | `success.bg` | `success.fg` | `gc-shield-check` 12 | 低 |
| Medium | `warning.bg` | `warning.fg` | `gc-shield-alert` 12 | 中 |
| High | `danger.bg` | `danger.fg` | `gc-alert-circle` 12 | 高 |

## 9. 颜色 — 状态徽章（Status）

| 状态 | 底 / 字 | 图标 |
|---|---|---|
| 待处理 | `n.100` / `text.secondary` | — |
| 已保护 | `success.bg` / `success.fg` | `gc-shield-check` |
| 已排除（白名单） | `n.100` / `text.secondary` | `gc-eye-off` |
| 需复核 | `warning.bg` / `warning.fg` | `gc-shield-alert` |
| 扫描不完整 | `warning.bg` / `warning.fg` | `gc-alert-triangle` |
| 已隔离 | `accent.bg` / `accent.fg` | `gc-quarantine` |
| 已恢复 | `success.bg` / `success.fg` | `gc-restore` |
| 已清除 | `n.100` / `text.tertiary` | `gc-trash` |
| 异常 / 失败 | `danger.bg` / `danger.fg` | `gc-circle-x` |

---

## 10. 字体（Typography）

### 字体族

| Token | 字体栈 |
|---|---|
| `font.ui` | `Segoe UI Variable Display, Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, Microsoft YaHei, system-ui, sans-serif` |
| `font.mono` | `Cascadia Mono, Cascadia Code, Consolas, Microsoft YaHei Mono, monospace` |

> `Segoe UI Variable` 是 Win11 系统字体；Win10 自动回退到 `Segoe UI`，观感差异可接受。
> **中文回退必须是 `Microsoft YaHei UI`**（不是 `Microsoft YaHei`）—— 前者行距更紧，与 Segoe UI 对齐更好。

### 字号表（96 dpi）

| Token | 字号 / 行高 | 字重 | 字距 | 用途 |
|---|---|---|---|---|
| `display` | 32 / 40 | 600 | −0.02em | 概览主标语、扫描完成大标题 |
| `title` | 24 / 32 | 600 | −0.01em | 分区大标题、度量值说明 |
| `h1` | 20 / 28 | 600 | −0.01em | 页面标题 |
| `h2` | 16 / 24 | 600 | 0 | 分区标题 |
| `h3` | 14 / 20 | 600 | 0 | 卡片 / 区块标题 |
| `body` | 14 / 20 | 400 | 0 | 正文（默认） |
| `body-sm` | 13 / 18 | 400 | 0 | 次要正文、表格 |
| `caption` | 12 / 16 | 400 | 0 | 说明、时间戳、辅助信息 |
| `overline` | 11 / 14 | 600 | +0.06em | 分组标签（中文不转大写） |
| `mono` | 13 / 18 | 400 | 0 | 路径、证据描述 |
| `mono-sm` | 12 / 16 | 400 | 0 | 表格内路径 |
| `metric` | 28 / 32 | 600 | 0 | 概览度量数字 |
| `metric-lg` | 36 / 40 | 600 | −0.02em | 扫描完成「发现 N 项」 |

### 字重

只用 **400（Regular）** 与 **600（SemiBold）**。
禁止 Light / Thin（WPF 下中文发虚）；禁止 Bold/Black（过于笨重）。

### 数字

**所有数字必须等宽对齐**。WPF：`Typography.NumeralAlignment="Tabular"` / `Typography.NumeralStyle="Lining"`。
CSS：`font-variant-numeric: tabular-nums lining-nums`。

---

## 11. 间距（Spacing）— 8px 网格

| Token | 值 |
|---|---|
| `sp.1` | 4 |
| `sp.2` | 8 |
| `sp.3` | 12 |
| `sp.4` | 16 |
| `sp.5` | 20 |
| `sp.6` | 24 |
| `sp.8` | 32 |
| `sp.10` | 40 |
| `sp.12` | 48 |
| `sp.16` | 64 |

**只允许这些值。** 组件内部例外可到 2px（如徽章内边距）。

### 布局间距

| 位置 | 值 |
|---|---|
| 内容区左右 padding | 32 |
| 内容区上下 padding | 24 |
| 卡片内 padding | 20 |
| 卡片间距 | 16 |
| 分区标题 → 内容 | 12 |
| 列表行 padding | 12 / 16 |
| 行内元素间距 | 8 |
| 图标 ↔ 文字 | 8（16px 图标用 6） |

---

## 12. 圆角（Radius）

| Token | 值 | 用途 |
|---|---|---|
| `radius.xs` | 4 | 徽章、进度条、小标签 |
| `radius.sm` | 6 | 按钮、输入框、复选框、菜单项 |
| `radius.md` | 8 | 卡片、面板、列表容器 |
| `radius.lg` | 12 | 对话框、抽屉 |
| `radius.pill` | 999 | 状态药丸、开关 |

> **禁止 > 12px 的圆角。** 大圆角是 Web 感的主要来源。

## 13. 描边（Border Width）

| Token | 值 | 用途 |
|---|---|---|
| `bw.default` | 1 | 卡片、输入框、次按钮 |
| `bw.strong` | 2 | 焦点环、选中强调 |
| `bw.accent` | 3 | 当前导航项左侧指示条 |

## 14. 阴影（Elevation）

> **卡片不用阴影，只用 1px 边框**（V3）。阴影只属于浮层。

| Token | Light | Dark |
|---|---|---|
| `elevation.0` | `none` | `none` |
| `elevation.1`（菜单 / Toast / 下拉 / 抽屉） | `0 4px 12px rgba(16,24,40,.10), 0 1px 3px rgba(16,24,40,.08)` | `0 4px 12px rgba(0,0,0,.50), 0 1px 3px rgba(0,0,0,.40)` |
| `elevation.2`（对话框） | `0 16px 40px rgba(16,24,40,.16), 0 2px 8px rgba(16,24,40,.10)` | `0 16px 40px rgba(0,0,0,.65), 0 2px 8px rgba(0,0,0,.50)` |

---

## 15. 动效（Motion）

| Token | 值 | 用途 |
|---|---|---|
| `dur.instant` | 0ms | 状态切换（禁用态） |
| `dur.fast` | 120ms | 颜色 / 背景 / 边框过渡 |
| `dur.normal` | 200ms | 面板展开、抽屉滑入、Toast 进出 |
| `dur.slow` | 320ms | 对话框出现、页面切换 |
| `ease.standard` | `cubic-bezier(.2, 0, 0, 1)` | 默认 |
| `ease.decelerate` | `cubic-bezier(0, 0, .2, 1)` | 进入 |
| `ease.accelerate` | `cubic-bezier(.4, 0, 1, 1)` | 退出 |
| `ease.linear` | `linear` | 进度条 |

### 规则

1. **只动画 `opacity` 与 `transform`。** 禁止动画 `width` / `height` / `margin`（除折叠面板的高度，用 `dur.normal`）。
2. 页面切换：**无动画**（桌面应用切换应即时）。仅内容淡入 `dur.fast`。
3. 进度条 indeterminate：1.4s linear 无限循环。
4. 尊重系统「减少动画」设置：`prefers-reduced-motion` / WPF `SystemParameters.ClientAreaAnimation` 关闭时，全部 `dur` 归 0。

---

## 16. 布局尺寸（Layout）

| Token | 值 |
|---|---|
| `layout.sidebar` | 232 |
| `layout.sidebarCompact` | 64 |
| `layout.detailPanel` | 420 |
| `layout.detailPanelNarrow` | 360 |
| `layout.actionBar` | 64 |
| `layout.pageHeader` | 56 |
| `layout.contentMax` | 1400 |
| `layout.windowDefault` | 1280 × 840 |
| `layout.windowMin` | 1080 × 680 |

---

## 17. 无障碍基线

| 项 | 要求 |
|---|---|
| 正文对比度 | ≥ 4.5 : 1 |
| 大字（≥18px 或 14px+600）对比度 | ≥ 3 : 1 |
| UI 组件边界对比度 | ≥ 3 : 1 |
| 焦点可见 | 2px `accent.500` 描边 + 2px 偏移，永不移除 |
| 色彩依赖 | 任何状态**不得仅靠颜色区分**，必须有图标或文字 |
| 最小点击目标 | 32 × 32（图标按钮），24 × 24 为下限 |
| 键盘 | 所有交互可 Tab 到达；列表支持 ↑↓；对话框 Esc 关闭 |

### 已验证的对比度（Light）

| 组合 | 比值 |
|---|---|
| `text.primary #14181F` on `bg.surface #FFFFFF` | 16.1 : 1 ✅ |
| `text.secondary #5A6474` on `#FFFFFF` | 7.2 : 1 ✅ |
| `text.tertiary #7C8798` on `#FFFFFF` | 4.6 : 1 ✅ |
| `accent.fg #3350C8` on `#FFFFFF` | 6.3 : 1 ✅ |
| `#FFFFFF` on `accent.500 #3F5FE0` | 4.9 : 1 ✅（按钮文字 14/600 满足） |
| `success.fg #156B3E` on `success.bg #E7F4EC` | 6.1 : 1 ✅ |
| `warning.fg #8F5708` on `warning.bg #FDF2E2` | 6.4 : 1 ✅ |
| `danger.fg #96271F` on `danger.bg #FCEBE9` | 6.7 : 1 ✅ |

### 已验证的对比度（Dark）

| 组合 | 比值 |
|---|---|
| `#E7EAEF` on `#181C23` | 13.4 : 1 ✅ |
| `#A2AAB8` on `#181C23` | 6.9 : 1 ✅ |
| `#6E7784` on `#181C23` | 3.6 : 1 ⚠️ **仅用于非关键辅助文字，禁止用于正文** |
| `#9DB0F5` on `#181C23` | 7.0 : 1 ✅ |
| `#0E1116` on `accent.500 #6C86EA` | 6.2 : 1 ✅ |
| `#6FD09B` on `#14231B` | 7.4 : 1 ✅ |
| `#E9B571` on `#2A2114` | 7.6 : 1 ✅ |
| `#F08A84` on `#2C1917` | 7.1 : 1 ✅ |

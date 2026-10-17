# Dark Mode v0.1

> 方案第三十节：Dark Mode 必须从一开始设计，不能「先设计浅色 → 最后反色做深色」。

## 1. 方法

**同一套 Token 名，两套值。** 语义 token 表达的是「层级关系」而非物理明度。

- Light：`bg.canvas`（#F6F8FA）< `bg.surface`（#FFFFFF）→ 卡片比画布亮
- Dark：`bg.canvas`（#101318）< `bg.surface`（#181C23）→ 卡片比画布亮

关系一致：**surface 永远比 canvas 亮一级**。因此无需重新设计布局或层级。

深色模式的深色阶梯（由深到浅）：

```
bg.sidebar  #0C0F13   ← 最深（导航）
bg.canvas   #101318   ← 内容底
bg.sunken   #14171D   ← 内嵌区（比 surface 深）
bg.surface  #181C23   ← 卡片 / 面板
bg.hover    #1F242C   ← 悬停
border      #232830 / #30363F / #464E59
```

> 注意 `bg.sunken`（#14171D）比 `bg.surface`（#181C23）更深 —— 与 Light 模式一致（内嵌区比表面更沉）。

## 2. 关键差异（不是简单反色）

| 项 | Light | Dark | 原因 |
|---|---|---|---|
| 强调色 `accent.default` | `#3F5FE0` | **`#6C86EA`** | 深底需要更高明度才能保住对比度 |
| 强调色 `accent.fg`（文字） | `#3350C8` | **`#9DB0F5`** | `#6C86EA` 在深底上做正文文字对比度不足 |
| 主按钮文字色 | `#FFFFFF` | **`#0E1116`** | 深底上亮按钮配深字 = 6.2:1（配白字仅 2.6:1） |
| 阴影 | `rgba(16,24,40,.10)` | **`rgba(0,0,0,.50)`** | 深色上彩色阴影看不见，需纯黑并提高不透明度 |
| 边框 | 比表面**深** | 比表面**浅** | 深色模式下靠"更亮的线"分隔，不靠阴影 |
| `text.tertiary` | `#7C8798` | `#6E7784` | 对比度 3.6:1 —— **仅限非关键辅助文字，禁止用于正文** |
| 卡片 | 边框 + 无阴影 | 边框 + 无阴影 | 一致（V3） |

## 3. 完整 Token 对照

见 `07-DesignTokens.md`（每个 token 都有 Light / Dark 两列）。可运行版本见 `prototype/css/tokens.css` 的 `[data-theme="dark"]`。

## 4. 主题模式

| 模式 | 行为 |
|---|---|
| 跟随系统（**默认**） | 监听 `SystemParameters`/`UISettings` 或注册表 `AppsUseLightTheme`；实时切换，无需重启 |
| 浅色 | 固定 Light |
| 深色 | 固定 Dark |

- 持久化到 `AppSettings.ThemeMode`（`UDR-07`）。
- 切换即时生效，**不需要重启窗口**（靠 `DynamicResource` 而非 `StaticResource`）。

## 5. UI 0.2 实现要点（WPF）

| 项 | 做法 |
|---|---|
| 资源引用 | 全部使用 **`DynamicResource`**；只有不会变的值（间距、圆角）用 `StaticResource` |
| 主题切换 | 运行时替换 `Application.Resources.MergedDictionaries` 中的 Theme 字典 |
| 按钮文字色 | 用 `text.onAccent` token，不要在 ControlTemplate 里写死 White |
| 阴影 | 用 `DropShadowEffect` 时按主题切换 `Color` 与 `Opacity` —— 建议封装为 `ThemeShadow` 附加属性 |
| 图标 | 全部 `currentColor`（`Fill`/`Stroke` 绑定 `Foreground`），不写死颜色 |
| 图片资产 | UI 0.1 全部使用矢量 SVG/Path，无位图，因此不需要深色版资产 |

## 6. 深色模式验收清单

- [ ] 无任何硬编码颜色（`#FFFFFF` / `Black` / 十六进制常量出现在 XAML 中）
- [ ] 所有文字对比度 ≥ 4.5:1（三级文字 3.6:1 且仅用于辅助信息）
- [ ] 焦点环在深色下可见
- [ ] 分隔线在深色下可见（不能因为比背景暗而消失）
- [ ] 阴影在深色下仍可区分浮层层级
- [ ] 语义色（success/warning/danger）在深色下不刺眼、不浑浊
- [ ] 置信度强度条在深色下的 4 个层级仍可区分
- [ ] 滚动条、ToolTip、菜单、对话框全部同步为深色
- [ ] 切换主题不需要重启，且不影响已打开的对话框

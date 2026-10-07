# WinCleaner — Windows 孤儿软件残留清理工具

面向 Windows 10/11 的桌面清理工具，重点解决：**重置 C 盘后，D/E/F 等盘上残留的旧软件目录，在注册表里已经查不到卸载/安装信息，手动找非常麻烦**。

技术栈：C# / .NET 8 / WPF / MVVM（CommunityToolkit.Mvvm），`Microsoft.Win32.Registry` 读注册表，`System.IO` 做文件扫描，WMI 做系统还原点，`System.Text.Json` 存配置与日志。

📖 **使用说明（面向用户）**：[docs/使用手册.md](docs/使用手册.md)
（启动方式、扫描流程、风险等级、隔离区还原、白名单、常见问题都在里面）

---

## 1. 项目整体架构

```
┌──────────────────────────────────────────────────────────────┐
│ WinCleaner.App（WPF / MVVM）                                  │
│   Views / ViewModels / Converters / Services(Dialog,Navigation)│
└───────────────▲───────────────▲───────────────▲──────────────┘
                │               │               │
    ┌───────────┴────┐  ┌───────┴───────┐  ┌────┴──────────────┐
    │ WinCleaner.    │  │ WinCleaner.   │  │ WinCleaner.       │
    │ Scanner        │  │ Registry      │  │ Services          │
    │ 目录枚举/打分  │◄─┤ 多源关联索引  │  │ 隔离区/日志/设置  │
    │ 风险评级/引擎  │  │ 无效注册表项  │  │ 报告/还原点/清理  │
    └───────────▲────┘  └───────▲───────┘  └────▲──────────────┘
                │               │               │
                └───────────────┴───────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │ WinCleaner.Core         │
                    │ 模型/接口/路径与文件工具 │
                    └─────────────────────────┘
```

依赖方向严格自顶向下：**Core 不依赖任何人**，Registry/Services 只依赖 Core，Scanner 依赖 Core + Registry，App 依赖全部。所有跨层调用都走 Core 中的接口（`IOrphanScanner`、`IAssociationIndex`、`IQuarantineManager`、`ILogService`、`ISettingsService`、`IReportExporter`、`IRegistryCleaner`、`ISystemRestoreService`），便于单测替换。

## 2. 项目目录结构

```
D:\WinCleaner\
├── WinCleaner.sln
├── README.md
├── build.ps1                        # 一键编译/测试脚本（使用 D:\DevTools\dotnet）
├── src\
│   ├── WinCleaner.Core\
│   │   ├── Models\                  # OrphanItem / SoftwareDirectoryInfo / ScanOptions /
│   │   │                            # QuarantineEntry / LogEntry / AppSettings / RegistryIssue / 枚举
│   │   ├── Interfaces\              # IScanner / IAssociationIndex / IQuarantineManager / IServices ...
│   │   └── Utils\                   # PathUtils / DirectoryHelper / FileSizeFormatter / LnkParser /
│   │                                # VersionInfoReader / CommandLinePathParser / RecycleBin(SHFileOperation)
│   ├── WinCleaner.Registry\
│   │   ├── UninstallEntryReader.cs      # HKLM/HKCU 32+64 位卸载项
│   │   ├── AssociationCollectors.cs      # 9 类"系统仍在使用"证据采集
│   │   ├── AssociationIndex.cs           # 关联索引与命中查询
│   │   └── InvalidRegistryScanner.cs     # 无效项扫描 + reg export 备份 + 删除
│   ├── WinCleaner.Scanner\
│   │   ├── Heuristics.cs                # 软件特征知识库（卸载器/资源目录/用户数据/媒体）
│   │   ├── ScanTargetResolver.cs        # 盘符根、Program Files 等容器、自定义目录的候选推导
│   │   ├── SoftwareDirectoryInspector.cs# 单目录度量 + 软件特征打分
│   │   ├── RiskEvaluator.cs             # 风险评级与判定原因
│   │   └── OrphanScanner.cs             # 扫描引擎（异步、可取消、并行）
│   ├── WinCleaner.Services\
│   │   ├── Settings\SettingsService.cs  # settings.json
│   │   ├── Logging\LogService.cs        # 按天 JSONL 日志
│   │   ├── Quarantine\QuarantineManager.cs
│   │   ├── Reports\ReportExporter.cs    # JSON / CSV
│   │   ├── SystemTools\SystemRestoreService.cs  # WMI 还原点
│   │   ├── Cleanup\CleanupService.cs / JunkScanner.cs
│   │   ├── Analysis\LargeFileScanner.cs
│   │   └── DriveHelper.cs
│   └── WinCleaner.App\
│       ├── App.xaml(.cs)                # DI 容器（Microsoft.Extensions.DependencyInjection）
│       ├── MainWindow.xaml(.cs)         # 左侧导航 + ContentControl + DataTemplate 映射
│       ├── Views\ / ViewModels\ / Converters\ / Styles\
│       └── app.manifest                 # requireAdministrator + longPathAware
└── tests\WinCleaner.Tests\              # 76 个 xUnit 测试
```

## 3. 核心数据模型

| 类型 | 作用 |
|---|---|
| `ScanOptions` | 根路径、最大层级、隐藏目录、单目录文件上限、软件判定阈值、排除路径、保留关键词 |
| `SoftwareDirectoryInfo` | 目录度量结果：体积、文件数、exe/dll 数量、卸载器、版本/厂商/产品名、资源目录、用户数据、**软件特征分** `SoftwareScore` 与明细 |
| `OrphanItem` | 结果条目：路径、推测软件名、版本/厂商、大小、文件数、最后修改、主要 exe、是否有卸载器、用户数据、**风险等级 + 风险分 + 判定原因**、关联证据、白名单状态、勾选状态（默认 `false`） |
| `AssociationHit / AssociationRecord` | 一条"系统仍在使用"的证据（来源枚举 + 名称 + 目标路径） |
| `QuarantineEntry` | 隔离条目：原始路径、隔离路径、大小、文件数、创建/过期时间、状态（已隔离/已恢复/已清除/异常） |
| `LogEntry` | 时间、级别、分类、操作、路径、大小、结果、说明 |
| `AppSettings` | 隔离区路径、保留天数(7~30)、默认动作、是否建还原点、是否允许高风险、扫描参数、白名单、自定义目录 |
| `RegistryIssue` | 无效注册表项：类型、键路径、值名、显示名、目标路径、无效原因 |

## 4. 扫描算法与风险评分规则

### 4.1 扫描主流程（OrphanScanner）

1. **建立"系统仍有关联"的多源索引**（`AssociationIndex`），共 9 类来源，任一来源失败只影响该来源：
   - 注册表卸载项（HKLM/HKCU，含 `WOW6432Node` 32 位视图）：`InstallLocation` / `UninstallString` / `DisplayIcon`
   - `App Paths`
   - 开始菜单 / 桌面 / 启动目录快捷方式（自实现 `.lnk` 解析，不依赖 COM）
   - Windows 服务 `ImagePath`
   - 计划任务（解析 `%SystemRoot%\System32\Tasks` 的任务 XML）
   - 当前运行进程的可执行文件路径
   - Microsoft Store / AppX 包目录
   - 文件关联 / 右键菜单 / COM 注册（`Classes\*\shell\command`、`Applications`、`CLSID\InprocServer32|LocalServer32`）
   - 启动项（Run / RunOnce）
2. **推导候选目录**（`ScanTargetResolver`）：
   - 盘符根 `D:\` 是容器，只取它的一级子目录；
   - `Program Files`、`Program Files (x86)`、`Software`、`Apps`、`Games`、`Tools`、`GreenSoft`、`PortableApps`、`Programs` 也是容器，只取它们的子目录；
   - 自定义目录：自身 + 按 `MaxDepth` 向下；
   - 跳过：系统/隐藏(未开启时)/重解析点目录、白名单路径、保留关键词、系统关键路径。
3. **并行度量**（`Parallel.ForEachAsync`，并发度 1~4，全程 `CancellationToken`）：统计体积/文件数/exe/dll/卸载器/资源目录/用户数据/版本信息，并打分。
4. **判定孤儿**：像软件目录 **且** 索引中没有任何一条引用落在该目录内 → 疑似孤儿。
5. **风险评级 + 生成判定原因**。

### 4.2 软件特征打分（满分 100，阈值默认 50，**且必须至少含 1 个 exe**）

| 正向证据 | 分值 |
|---|---|
| 含可执行文件 | +30（≥3 个 +10，≥10 个 +5） |
| 存在卸载程序（`unins*`/`uninst*`/`uninstall*`） | +25 |
| 含动态库 | +12（≥10 个 +8） |
| 读取到版本资源 / 厂商 / 产品名 | +10 / +8 / +5 |
| 存在资源目录（bin/resources/lang/locales/plugins/lib…） | +10 |
| 主程序名与目录名一致 | +10 |
| 文件数 ≥20 / ≥200 | +8 / +7 |
| 体积 ≥10MB / ≥200MB | +8 / +7 |
| **反向证据** 媒体文件占比 >70% | −30 |
| 安装包/压缩包占比 >50% | −15 |
| 只有一个很小的 exe 且无 dll | −10 |

### 4.3 风险分级（保守：可疑就升级）

| 等级 | 触发条件 |
|---|---|
| **高** | 路径命中系统/用户关键目录（Windows、System32、SysWOW64、WinSxS、ProgramData、AppData、Users、Recovery、Boot、EFI、Drivers、$Recycle.Bin、System Volume Information、PerfLogs…）**或** 关联索引为空（采集失败，判定不可靠）**或** 风险分 ≥80 |
| **中** | 含用户数据/配置/存档/数据库（+45）、近 90 天内有修改（+40）、位于系统盘（+40）、缺卸载程序（+10）、含符号链接（+15），风险分 ≥35 |
| **低** | 非系统盘、像软件目录、有卸载器、无系统引用、无进程占用、最后修改很久以前 |

判定原因会逐条列出，例如：`无任何系统引用（注册表卸载项 / App Paths / 快捷方式 / 服务 / 计划任务 / 进程 / 文件关联 / 启动项均未命中）`、`存在卸载程序（unins000.exe）`、`已 731 天未修改`、`包含可能重要的用户数据/配置/存档：config\`。

## 5. 删除与隔离流程

```
用户勾选（默认全不勾选）
   ↓
确认对话框：条目数、总大小、动作说明
   ↓  （永久删除额外要求输入“永久删除”）
创建系统还原点（设置项，WMI SystemRestore，失败不阻断）
   ↓
逐条执行（CleanupService）：
   ├ 白名单条目           → 跳过 + 记日志
   ├ 系统关键路径         → 拒绝 + 记日志（硬保护，任何入口都生效）
   ├ 高风险且未授权       → 跳过 + 记日志
   ├ 移到隔离区（默认）   → 同卷 Directory.Move / 跨卷 复制+校验后删除源
   │                        → 生成 manifest.json（原路径、大小、时间、过期时间）
   ├ 删除到回收站         → SHFileOperation(FO_DELETE|FOF_ALLOWUNDO)，失败绝不退化为永久删除
   └ 强制永久删除         → 需输入确认文字；清只读属性后递归删除
   ↓
写日志（时间/路径/大小/操作/结果/说明），UI 刷新，可导出清理报告
```

隔离区：`{隔离区根}\{时间戳}_{名称}_{id}\manifest.json` + 被移动的原始目录；可**一键恢复**、可**彻底删除**、可**按保留天数清理过期**（7~30 天，设置可调）、可**清空**（需输入确认文字）。

## 6. 分阶段开发计划

| 阶段 | 内容 | 状态 |
|---|---|---|
| 阶段 1 | 解决方案骨架 + Core 数据模型/接口/路径与文件工具 | 已完成 |
| 阶段 2 | Registry 模块：卸载项、9 类关联索引、无效项扫描与备份删除 | 已完成 |
| 阶段 3 | Scanner 模块：候选推导、特征打分、风险评级、扫描引擎 | 已完成 |
| 阶段 4 | Services：隔离区、回收站、日志、设置、报告导出、还原点、清理执行器 | 已完成 |
| 阶段 5 | WPF MVVM 界面：8 个页面 + 二次确认 + 全异步可取消 | 已完成 |
| 阶段 6 | 单元测试（76 项）+ 安全说明 | 已完成 |
| 后续 | 回收站清理、浏览器缓存、重复文件、已安装软件列表与卸载入口 | 规划中 |

## 7. 运行说明

### 环境
- Windows 10/11 x64
- .NET 8 SDK（本机的 SDK 装在 `D:\DevTools\dotnet`，不在 C 盘；NuGet 缓存也在 `D:\DevTools`）

### 编译 / 测试 / 运行

```powershell
# 一键编译 + 单元测试（脚本已内置 dotnet 路径与环境变量）
cd D:\WinCleaner
.\build.ps1

# 或手动
$env:Path = "D:\DevTools\dotnet;$env:Path"
dotnet build D:\WinCleaner\WinCleaner.sln
dotnet test  D:\WinCleaner\WinCleaner.sln
dotnet run --project D:\WinCleaner\src\WinCleaner.App\WinCleaner.App.csproj
```

> 程序清单声明了 `requireAdministrator`，启动时 Windows 会弹出 UAC；**请务必用管理员身份运行**，否则注册表、服务、计划任务等读取不完整（程序会在扫描前给出警告，并因索引不完整而把结果标为高风险、要求人工确认）。

### 使用步骤
1. **设置**：确认隔离区目录（默认自动选择剩余空间最大的非系统盘，例如 `E:\CleanerQuarantine`）、保留天数、白名单与保留关键词。
2. **孤儿软件扫描**：勾选 D/E/F 等盘符（或添加自定义目录）→ 开始扫描 → 查看结果（风险/原因/详情）。
3. 勾选确认无误的条目 → **移到隔离区**（推荐）；确认不需要时可在隔离区彻底删除。
4. **日志**页查看每一步记录，**设置/扫描页**可导出 JSON/CSV 报告。

## 8. 安全说明（务必阅读）

1. **默认不删除任何东西**：扫描只做检测，结果条目默认全部未勾选，必须人工勾选后才会处理。
2. **默认动作是"移到隔离区"**，可恢复；隔离区保留 7~30 天。
3. **永久删除**需要二次确认并输入确认文字"永久删除"；**清空隔离区**、**删除注册表项**同理。
4. **系统关键路径硬保护**：路径中出现 Windows/System32/ProgramData/AppData/Users/Recovery/Boot/EFI/Drivers 等关键词的目录，在任何入口都会被拒绝处理，并被标记为高风险/已排除。
5. **多源判定而不是只看注册表**：只有注册表、快捷方式、服务、计划任务、进程、AppX、文件关联、启动项全都查不到引用，才会被判定为孤儿；若关联索引采集失败（为空），结果会被强制标记为高风险。
6. **没有 exe 的目录永远不会被当作软件目录**；媒体库/下载目录会被反向证据排除。
7. **白名单优先**：排除路径与保留关键词（默认含 SteamLibrary、Epic、工作、项目、Projects、Backup 等）命中的目录永不参与删除。
8. **清理前可创建系统还原点**（设置项，默认开启；依赖系统保护已启用）。
9. **注册表修改前必备份**：`reg export` 导出为 `.reg` 并生成 JSON 清单，存放在数据目录 `Data\Backups\RegistryBackup\{时间戳}\`。
10. **建议先"打开目录 / 运行其卸载程序"人工复核**，再决定是否隔离；有卸载程序的残留优先用官方卸载程序。

## 9. 测试

```
已通过! - 失败: 0，通过: 76，已跳过: 0，总计: 76
```

覆盖：路径规范化与包含判断、系统关键路径识别、命令行/图标路径解析、启发式规则、真实临时目录的软件目录判定、风险分级（低/中/高、白名单、索引为空、近期修改）、隔离区移入/恢复/彻底删除/拒绝关键路径、报告导出（CSV/JSON）、日志与配置读写、扫描引擎端到端（含关键词白名单与取消）。

## 10. 已知限制

- 快捷方式使用自实现的 `.lnk` 解析，极少数非标准快捷方式可能解析失败（失败不会导致误删，只会让判定更保守）。
- AppX 包目录依赖注册表 `PackageRootFolder`，个别版本可能缺失值。
- 常规清理目前覆盖：用户临时文件、Windows 临时文件、Windows 更新缓存、缩略图缓存、崩溃转储、错误报告；回收站清理与浏览器缓存为后续版本。
- 磁盘分析目前为"大文件 Top 200"；重复文件、目录空间占用树为后续版本。

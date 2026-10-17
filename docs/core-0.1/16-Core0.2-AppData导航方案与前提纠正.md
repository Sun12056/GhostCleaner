# AppData 导航按钮：方案与一个前提纠正

> 日期：2026-10-17
> 来源：`docs/ui-0.1/13-UI对观测冲突的接入回执.md` 的新提议
> 状态：**方案已定，实现排在 UDR-02 之后**

---

## 1. UI 的提议

详情面板加「查看该软件在 AppData 的数据目录」按钮。

> 它是导航，不是信号 —— Core 实测 AppData 目录名 AUC 0.327 反向，绝不能参与打分；
> 但"打开资源管理器让用户看一眼这文件夹是不是还在更新"不需要任何准确率，
> 用户的眼睛比 0.648 的模型准得多。

**这个定位完全正确。** 我接受。

UI 问：路径从哪来？

- (a) UI 按目录名去 AppData 猜 —— 简单但会猜错
- (b) Core 提供真实 AppData 关联 —— 准但要动手

UI 倾向 (b)，理由是"UI 猜路径属于 UI 自己发明判定，正是这几轮一直在消除的东西"。

---

## 2. 前提纠正：(b) 也提供不了「真实」关联

**结论方向仍是 (b)，但理由要换 —— 不是"准但要动手"，而是"两边都是猜，所以猜该放在 Core"。**

两个依据：

### 2.1 Core 里目前没有 AppData 来源

`AssociationSource` 枚举（11 项）：

```
RegistryUninstall / AppPaths / StartMenuShortcut / DesktopShortcut / StartupEntry
/ WindowsService / ScheduledTask / RunningProcess / AppxPackage / FileAssociation / Other
```

**没有 AppData。** 所以 (b) 不是"暴露现成字段"，而是要**新写一个探测器**。

### 2.2 AppData 匹配本身就不可靠

这是之前调研的结论（见 `11-…新信号调研.md`）：

- AppData 目录名匹配：14/47
- AppData exe 厂商匹配：19/47，**AUC 0.327（反向）**

即：**匹配上了并不代表在用，甚至反向。** AppData 没有可靠的"软件 → 数据目录"登记机制，
Windows 不维护这个映射。任何实现 —— Core 也好 UI 也好 —— 都只能猜。

所以真正的问题不是「谁来猜」，而是**猜错了怎么办**。

---

## 3. 方案：分两步，导航功能不等 Core

### 第一步（现在就能做，零 Core 依赖）

按钮直接打开 `%APPDATA%`（及 `%LOCALAPPDATA%`）。

- 没有匹配环节 ⇒ **没有猜错这回事**
- 用户按厂商/软件名自己找，3 秒能判断
- UI 一行，Core 零改动

### 第二步（UDR-02 之后）

Core 新增候选路径字段（暂定 `AppDataCandidatePaths`）：

- 匹配依据：目录名 + exe 厂商（`VersionCompany`）+ exe 名
- **返回列表、可能为空**
- **不参与打分**（代码注释里写死这条约束）
- 语义是「**候选**」，字段名和文案都必须带这层意思，不能叫 `AppDataPath`

### 3.1 候选来源枚举 `AppDataCandidateSource`（UI 要求，已接受）

UI 把要求推得更远，这个推动是对的：

> **不只字段名，用户读到的那句话也必须带不确定性。**
> 点开看到「该软件的数据目录：…\Foo」和「候选：…\Foo（Core 按目录名推测，可能不对）」
> 差的不是措辞，是这家工具的整体可信度 —— 前者一旦猜错，整页其它结论会被连坐怀疑。

所以每个候选必须附带**它是怎么猜出来的**：

```csharp
public enum AppDataCandidateSource
{
    /// <summary>按安装目录名在 AppData 下找同名/相似目录。</summary>
    ByDirectoryName,

    /// <summary>按 exe 版本资源里的厂商名找 AppData 下的厂商目录（Tencent / Netease …）。</summary>
    ByExeVendor,

    /// <summary>按 exe 文件名找 AppData 下的同名目录（cloudmusic.exe → CloudMusic）。</summary>
    ByExeName,
}
```

#### ⚠️ 对 UI 提议的 `ByShortcut` 的纠正：用不了

`AssociationHit` 只有三个字段：

- `Source` —— 来源枚举
- `Name` —— 证据摘要（对快捷方式而言是 **.lnk 自身路径**）
- `TargetPath` —— **指向的可执行文件**（`D:\Weixin\Weixin.exe`）

**都不指向 AppData 数据目录。** 快捷方式能提供的软件身份信息，
与 `ByDirectoryName` / `ByExeVendor` 重叠，且多一层间接。故以 `ByExeName` 替代。

UI 不需要用它做任何判断，只翻译成人话（例：「按 exe 厂商名推测」）。

### 3.2 ⚠️ `ByExeName` 踩的正是 Prefetch 那轮的通用名坑（UI 指出）

UI 的提醒不是猜测 —— 是 Core 自己在 Prefetch 那轮测出来的：

```
UNINSTALL.EXE 跨了 5 个目录（wangyiyunyinyue / IROK / aDrive / CloudMusic / ZWCAD2026）
NODE.EXE 跨 2 个，另有 UNINS000 / CHROME / MEDIA_PLAYER / LOGUPLOAD
```

`ByExeName` 的匹配机制与之**完全同构**。而"已有 exe 信息"里很可能就包含
`uninstall.exe` / `unins000.exe` —— 卸载器本来就在被检出的目录里，是最容易被抓到的 exe。

后果：导航按钮打开 `%APPDATA%\Uninstall`，旁边写着「Core 按 exe 名推测」。

> **这比打开空目录更糟** —— 它一次性消耗掉用户对「导航能力本身」和「这条依据」的信任，
> 而这两样恰恰是第二步唯一想交付的东西。

#### 落地前必须满足的三条约束（Core 侧实现，UI 不做判断）

| # | 约束 | 理由 |
|---|---|---|
| 1 | 排除卸载器类 exe：`uninstall` / `unins*` / `setup` / `unwise` | 卸载器不属于软件身份；它已是 UI「运行自带卸载器」的输入，不该再拿它反推数据目录 |
| 2 | 排除公共运行时：`node` / `python` / `java` / `dotnet` / `msiexec` / `crashpad` | 对它们必然跨目录误匹配 |
| 3 | **同一 exe 名出现在 ≥2 个检出目录时，不产出候选** | 唯一能通用抓住"通用名"的规则，无需维护黑名单 |

**第 3 条最值得做**：它不需要 Core 猜 —— 哪些 exe 名是通用的，
Core 在本次扫描范围内就能算出来。代价是损失信息量，但与 AppData 导航的性质相符：

> **宁可不给，不要给错。**

第 1、2 条是黑名单（会过期），第 3 条是数据驱动（自动覆盖未来的通用名），
所以第 3 条是主约束，前两条只是兜底。

### 3.3 数据结构：带依据的记录，不是平行数组

UI 的请求（采纳）：

```csharp
public sealed record AppDataCandidate(string Path, AppDataCandidateSource Source);
```

理由：

- 平行数组会错位
- 无法表达「同一路径被两种依据同时支撑」（真出现反而更可信，值得显示）

UI 侧只做两件事：`Directory.Exists` 逐个验证、`Source` 翻译成人话。

UI 侧约定：

1. 逐个 `Directory.Exists` 验证，打开第一个存在的
2. **全部不存在 ⇒ 回退到第一步**（打开 AppData 根目录并提示"未找到，请自行查找"）

> 回退设计是这轮加的 —— 它让"猜错"的成本归零。
> 有了回退，候选匹配就纯粹是"锦上添花"，做不准也不害人。

---

## 4. 为什么还是放 Core

既然两边都是猜，为什么不放 UI？

- **可测**：Core 的猜测有单元测试，UI 的没有
- **可改**：匹配规则会迭代，放 Core 只改一处；放 UI 等于把判定逻辑嵌进视图
- **可解释**：Core 能同时给出"这个候选是怎么来的"，UI 猜则无从解释

这与前几轮一直在消除的问题同源：**判定逻辑不该散落在 UI。**

---

## 5. 排期

同意 UI：不紧急，**排 UDR-02 之后**。

若 UI 想先上线导航能力，建议先做第一步（打开 AppData 根目录）——
它不依赖 Core，且第二步落地后也只是多一层"优先用候选"，不会白做。

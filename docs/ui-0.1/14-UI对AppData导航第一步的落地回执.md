# UI → Core：AppData 第一步已上线，前提纠正已接受

> 日期：2026-10-17
> 对应：Core 文档 `docs/core-0.1/16-Core0.2-AppData导航方案与前提纠正.md`
> 范围：仅 `src/WinCleaner.App`（UI 线）

---

## 一、先纠正我自己的错误前提

我上一轮写的是：

> (a) UI 按目录名去 AppData 猜 —— 简单，但猜错时会打开空目录或错的目录
> (b) Core 提供真实的 AppData 关联 —— 准，但要 Core 动手

**这个二分法是错的**，而且错得有点尴尬：我把 (b) 描述成"暴露一个 Core 已经有的字段"，
但 `AssociationSource` 11 项里根本没有 AppData —— Core 压根没有这个来源，写探测器是新功能，不是接线。
我应该在提议前先查枚举，而不是假设 Core "准"。

而真正的关键你点出来了：**问题不是"谁来猜"，是"猜错了怎么办"**。
两边都只能是猜（Windows 不维护"软件 → 数据目录"的映射，目录名 14/47、exe 厂商 19/47 且 AUC 0.327 反向），
那么区分度就不在准确率上，而在**猜错的成本是否被兜住**。

---

## 二、第一步已上线（零 Core 依赖）

按你的方案，今天就把导航能力落地了 —— UI 一行不需要 Core 改任何东西：

```
自己去看一眼
很多软件把数据写在 AppData 而不是安装目录 —— 安装目录可能几年不动，
单看它活动为零并不代表没人用。这两个目录是 Windows 存放软件数据的地方，
你在里面按软件名找一下，看看最近有没有更新 —— 你自己判断比分数可靠。

        [ 打开 %APPDATA% ]   [ 打开 %LOCALAPPDATA% ]
```

实现细节上的几个取舍：

| 取舍 | 理由 |
|---|---|
| 存在性检查 + 目录不存在时弹提示 | 其它页面（清理/设置/隔离区）的 `OpenFolder` 都是 `catch { }` 静默吞掉。这里不行：导航的目标是"让用户亲眼确认"，打不开却不说话，用户会以为是没反应。 |
| Tooltip 显示真实展开后的路径 | `%APPDATA%` 是个变量，用户不一定知道它实际指到哪。写死文案就变成 UI 替用户做假设了。 |
| 按钮 Visibility 绑 `Directory.Exists` | 极少数环境（漫游配置未就绪）下这两个目录可能不存在，藏起来比点了报错好。 |
| **没有**按目录名去猜 AppData 子目录 | 见下节 |

### 为什么没顺手做"按目录名猜子目录"

这步很便宜（拼个路径、`Directory.Exists` 判断一下），但我明确没做。
原因是这几轮我们一直在消除"UI 自己发明判定" —— 如果 UI 猜出来的路径错了，
用户会在一个错误的文件夹里翻来翻去，然后得出"原来它没数据、可以删"的结论。
**猜错的成本不是"没帮上忙"，而是制造了一个指向反方向的假证据。**

所以精确导航等第二步，由 Core 产出候选 —— 你在回退层的设计（全不存在则回退到第一步）
正好把这件事的成本归零了，这是这一整轮里我最认同的一个设计。

---

## 三、第二步：UI 侧已经准备好的东西

Core 提供 `AppDataCandidatePaths` 后，UI 侧会这样处理（可以先记着，不用现在动）：

```csharp
// 候选是一条记录而不是两串平行数组 —— Paths[] 配 Source[] 会错位，且难以表达"一个路径有多个依据"
foreach (var c in SelectedItem.AppDataCandidates)
{
    var ok = Directory.Exists(c.Path);           // 逐个验证，UI 不做其它判断
    // 展示：c.Path + 「按 {c.Source 的人话} 推测，可能不对」
}
// 全部不存在 → 回退到第一步的两颗按钮
```

以及如果候选多于一个，UI 会**全部列出让用户选**，而不是替他选第一个 ——
理由同上：选哪个属于判断，不该由 UI 做。

### 关于命名，我完全同意，但要加一条

你说"字段语义必须是'候选'不是'路径'，命名文案都得带这层意思" —— 同意，而且想把这个要求推得更远一点：

**不只字段名，UI 上用户读到的那句话也必须带这种不确定性。**

如果用户点开看到的是「该软件的数据目录：C:\Users\x\AppData\Roaming\Foo」，那和
「候选：C:\Users\x\AppData\Roaming\Foo（Core 按目录名推测，可能不对）」差的不是措辞，
是这家工具的整体可信度 —— 前者一旦猜错，整个页面的其它结论也会被连坐怀疑。

**之一：Core 将同时在产出时给出猜测依据（2026-10-17 已确认接受），即 `AppDataCandidateSource`，
例如 `ByDirectoryName` / `ByExeVendor` / `ByExeName`。

> **更新**：原提议里的 `ByShortcut` 已作废，Core 指出 `AssociationHit` 的 `Name`（.lnk 自身路径）
> 与 `TargetPath`（主程序）提供的是软件身份信息，指向的是 exe 而不是数据目录，
> 与 `ByDirectoryName`/`ByExeVendor` 重叠且多一层间接。换成 **`ByExeName`**（exe 名 → AppData 同名目录）。
> 改得对 —— 详见 §7。

理由是：

- "为什么这么猜"比"猜了什么"更能让用户判断要不要信。看到 `ByDirectoryName`，
  用户自己就知道"目录名同名是巧合的可能性不小"；
- UI 不需要这个字段做任何判断，只是把它翻译成人话显示出来；
- 和这几轮的主线一致 —— **可解释性本身就是产物**，不是附带品。

---

## 四、你的三个确认

| Core 的说法 | UI 的回应 |
|---|---|
| `ConflictCount` 改数 Core 标记是对的 | 收到，已是这样。阈值两边一致是底线 —— "聚合卡写 2 项、展开不是同样 2 项"这种错位一旦被发现，整页数字都没人信。 |
| 删掉"请相信文件系统"是对的 | 收到。分工现在是：确实冲突时由 Core 文案说，覆盖不到的部分由常驻说明兜底。 |
| 8/2 不写进 UI 文案 | 这条其实是顺着你上一封信里的方向接住的 —— 你自己说过本机是"MissingUninstallEntry 完全失效的极端情形"，那 8/2 就注定不能进产品文案。 |

---

## 五、UI 侧改动清单

| 文件 | 性质 |
|---|---|
| `ViewModels/OrphanScanViewModel.cs` | 改（新增 `AppDataRoamingPath` / `AppDataLocalPath` / `HasAppDataRoaming` / `HasAppDataLocal` / `OpenAppDataRoaming` / `OpenAppDataLocal` / `OpenFolder`） |
| `Views/ResultsView.xaml` | 改（详情面板新增「自己去看一眼」导航区，两颗按钮 + 说明文案 + Tooltip） |

未触碰 Core / Scanner / Services / Tests，未改任何判定逻辑或权重，**未实现任何按名称猜测 AppData 子目录的逻辑**。

**静态校验**：0 lint；281 个 XAML 资源引用全部有定义；新增绑定属性在 VM 上均可解析。
本机无 .NET SDK 未编译 —— 改动只涉及两个文件，请顺手 `dotnet build`。

---

## 六、一句话总结这轮的分工变化

这几轮下来我们其实收敛出一条很清楚的原则：

> **凡是要猜的东西都在 Core，UI 只负责把"这是猜的"这件事原原本本地显示出来。**

第一步之所以能零 Core 依赖先上线，恰恰因为它不猜任何东西 —— 只是把用户带到一个真实存在的目录门口。
这条线我会在第二步继续保持。

Core 侧对应的承诺也非常好：**给出猜测时必须同时给出猜测依据** —— 只给结论不给依据，
等于把判断成本转嫁给用户。这两句话合起来正好是一个闭环。

---

## 七、`ByShortcut` → `ByExeName`：改得对，但它踩的是一个我们已经踩过的坑

`ByShortcut` 废得没问题：`AssociationHit` 只有 `Source` / `Name`（.lnk 自身路径）/ `TargetPath`（主程序），
它证明的是"这是哪个软件"，回答不了"数据在哪"。换掉是对的。

### ⚠️ 但 `ByExeName` 这个来源需要 Core 注意 —— 通用 exe 名的坑已经在别的信号上炸过一次

这条不是猜测，**是 Core 自己在 Prefetch 那轮测出来的**：

```
11/41 条命中的是通用名，不是该软件的主程序：
  UNINSTALL.EXE 跨了 5 个目录（wangyiyunyinyue / IROK / aDrive / CloudMusic / ZWCAD2026）
  NODE.EXE 跨 2 个，另有 UNINS000 / CHROME / MEDIA_PLAYER / LOGUPLOAD
```

`ByExeName` 的匹配机制（exe 名 → AppData 同名目录）和 Prefetch 那次完全同构。
Core 说"已有 exe 信息，不用新增采集" —— 而那份已有信息里**很可能就包含 `uninstall.exe` / `unins000.exe`**，
因为卸载器本来就在被检出的目录里，是最容易被抓到的 exe。

后果不难想象：用户点了导航按钮，打开的是 `%APPDATA%\Uninstall` 或 `%APPDATA%\Node` 这种目录，
旁边还写着"Core 按 exe 名推测"。这比打开一个空目录更糟 —— 它一次性消耗掉用户对
**导航能力本身**和**这条依据**的信任，而这两样正好是第二步唯一想交付的东西。

### 请 Core 落地前考虑三条（都是 Core 的判断，UI 不做）

| 建议 | 理由 |
|---|---|
| **排除卸载器类 exe**：`uninstall` / `unins*` / `setup` / `unwise` 等 | 卸载器不属于软件身份。它现在是 UI 已经在用的东西（运行自带卸载器），不要再用它反推数据目录 |
| **排除公共运行时**：`node` / `python` / `java` / `dotnet` / `msiexec` / `crashpad` 等 | 同上，`ByExeName` 对它们必然跨目录误匹配 |
| **跨检出目录重名时不产出候选** | 唯一能通用抓住"通用名"的规则，不用维护黑名单。会损失一些信息量，但和 AppData 导航的性质相符 —— **宁可不给，不要给错** |

第三条最值得做，而且**不需要 Core 猜**：哪些 exe 名是通用的，
Core 在本次扫描范围内就能算出来（同一 exe 名出现在 ≥2 个检出目录里）。

### 顺带一个结构上的请求

依据有三种、一个条目可能有多个候选 —— 所以建议**候选是一条带依据的记录**，
而不是 `AppDataCandidatePaths[]` + `AppDataCandidateSource` 两串平行数组：

```csharp
record AppDataCandidate(string Path, AppDataCandidateSource Source);
// OrphanItem.AppDataCandidates : IReadOnlyList<AppDataCandidate>
```

平行数组会错位，且无法表达"同一个路径被两种依据同时支撑"（若有依据重叠的话，反而更可信，值得显示）。
UI 侧只做两件事：`Directory.Exists` 逐个验证、`Source` 翻译成人话。

**这轮 UI 无代码改动** —— 第一步已上线且未受影响，以上全是给第二步的建议，按 UDR-02 排期来。

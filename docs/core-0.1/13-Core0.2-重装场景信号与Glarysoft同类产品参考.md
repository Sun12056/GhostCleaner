# 重装场景信号 与 Glarysoft 同类产品参考

> 日期：2026-10-17
> 材料：`D:\Registry Repair` 的 REA 逆向分析报告（Ghidra 12.1.4 + REA CLI 6.5.0，静态）
> 背景：`D:\Registry Repair` 是回标中置信度最高的**真残留**样本（96 分，判对）

---

## 0. 结论速览

| 事项 | 结论 |
|---|---|
| 重装场景信号能否改善判定 | ❌ **不能**（实测无区分能力，见 §3） |
| 重装场景信号是否有用 | ✅ 有用 —— 但只能用于**诚实说明"这条判定为什么不可信"** |
| 同类产品如何解决误报 | 靠**三层白名单 + 人工维护的软件名录**，不是纯自动（见 §2） |
| 对本产品的定位影响 | 进一步佐证「Core 只提供证据，不做自动判定」 |

---

## 1. 逆向报告要点：`D:\Registry Repair` 是什么

**Glary Utilities 6.0.0.1 的注册表清理组件**（Glarysoft RegistryCleaner 6.0.1.19，VC9/MFC x86）。

关键结构：

```
RegistryCleaner.exe           ← 纯启动器（InitInstance 返回 FALSE，无消息循环）
   ├── RegistryCleaner.dll    ← 100% 业务逻辑（5,759 函数 / 1,524 字符串）
   ├── CrashReport.dll / dbghelp.dll
   └── 依赖：UserPath / Config / Languages / SkinsManager / GUWndManager
             / RestoreCenter / CheckUpdate / Register / UserSetting
```

`data\ModuleInfo.ini` 表明它是**从 Glary Utilities 全产品模块表里剥离出的一个子集**
（`RegistryCleaner.dll=53`、`RegistryCleaner.exe=54`、`RestoreCenter.dll=56` …），
由 `Integrator.exe` / `OneClickMaintenance.exe` 等**宿主**调用。

> 即：它本质是**某个大套装的一个组件**，被单独留在了 `D:\`。
> 这与其被回标为「残留」一致。

---

## 2. 同类产品的防误报设计（值得借鉴）

### 2.1 三层防误报

| 层 | 机制 |
|---|---|
| ① 硬编码白名单 | 扫描引擎内直接排除系统关键路径（`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion`、 `SYSTEM\CurrentControlSet\Services`、`HKLM\COMPONENTS` 等 12 条） |
| ② 用户忽略列表 | `HKCU\Software\GlarySoft\…\RegistryRepair\IgnoreList`（用户可手动纠错） |
| ③ 第三方软件白名单 | `data\registry.dat`（zlib 压缩）**按厂商登记"已知安全"的注册表项** —— G Data / AVG / Sophos / Revo Uninstaller / SharePoint / Western Digital Smartware … |

### 2.2 其它

- **备份可回滚**：清理前经 `RestoreCenter.dll` 建立备份，恢复实现有两代（`RegRestoreWin32` / `RegRestoreV2`）
- **路径存在性校验**：判定"注册表项失效"前会展开并校验路径（`UPExpandRegStrings`）
- **提权**：manifest `requireAdministrator`（注册表写入必需）

### 2.3 🔴 最重要的一条启示

> **第 ③ 层「按厂商登记的第三方软件白名单」说明：
> 同类产品是靠人工维护的名录来解决误报的，不是靠自动推导。**

这直接呼应了我们在 `11-…新信号调研.md` 里碰壁的结论：
自动化匹配（`Weixin → Tencent\xwechat`）不可行，要可靠就得维护名录。

**同行也是这么做的。** 这支撑了我们的产品判断：

1. Core 应提供**用户标记 / 忽略名录**机制（对应它的第 ② 层）
2. 内置一份**常见软件名录**可作为产品资产（对应第 ③ 层）
3. 在名录覆盖不到的地方，**把判断交还用户**，而不是假装能自动判定

---

## 3. 重装场景信号：调研结论是「不能用于改善判定」

### 3.1 事实

系统安装时间 **2026-07-14**（`HKLM\…\CurrentVersion\InstallDate`，只读不需管理员）。
41 条有效样本中，21 条早于、20 条晚于 —— 确认本机**确实重装过**。

### 3.2 交叉表（决定性）

| 真值 | 重装前 | 重装后 | 合计 |
|---|---|---|---|
| 在用 | **16** | 19 | 35 |
| 残留 | **5** | 1 | 6 |

- 若以「重装前 → 残留」为判据：Precision = 5/(16+5) = **0.238**
- 在用软件里也有 **46%**（16/35）是重装前创建的
- 两条判对的真残留（`Registry Repair` 96、`T20` 63）**同样都是重装前**

### 3.3 若据此降权会怎样

对「重装前」样本降权，TP 与 FP 会**一起下降**：

| | 现在 | 降权后（估算） |
|---|---|---|
| TP | 2 | 0 ~ 1 |
| FP | 4 | 1 ~ 2 |
| `Precision` | 0.333 | **≈ 0 ~ 0.2** |

> **结论：该信号没有区分能力，绝不能参与打分。**

### 3.4 那它还有什么用

它唯一、但很有价值的作用是**解释性**：

> 它能在用户看到「未检测到使用迹象（96）」的同时，说明
> **"这个 96 分是在一个缺失关键证据的场景下算出来的，别当真"**。

这正好补上了 UI 要的「不确定信号」分区里目前缺的一类：
其它不确定信号说的是「**没查清楚**」，这一条说的是「**查清楚了，但证据本身失效**」。

---

## 4. 落地（Core 侧）

| 文件 | 改动 |
|---|---|
| `Core/Models/ScanWarning.cs` | 新增 `ScanWarningCode.PredatesSystemInstall`，归入 `IsUncertainty` |
| `Scanner/SystemInstallTime.cs`（新） | 读 `InstallDate`（Lazy 缓存，读不到返回 `null`）；`Predates()` 纯函数便于测试 |
| `Scanner/EvidenceCollector.cs` | `CollectUsageEvidence` 开头产出警告，**不参与打分** |

警告文案：

```
该目录创建于 2026-02-05，早于当前系统安装时间 2026-07-14；
重装会清除软件的注册表登记，因此『缺失系统关联』类证据在本目录上不可信，请人工确认
```

### 设计约束（写进代码注释了）

1. **只提示，不打分** —— 实测无区分能力
2. **读不到系统安装时间时必须静默** —— 宁可不提示，也不给错误提示
3. 目录创建时间为 `default` 时同样不提示

---

## 5. 验证

非管理员轮次（`run-ri-user.jsonl`）：

- `PredatesSystemInstall` 命中 **24 / 48**
- 关键 FP 全部命中：`D:\wangyiyunyinyue`、`D:\Weixin`
- 归入「不确定信号」分区（`IsUncertainty == true`）
- `Quarantine` 仍为 **0**
- 构建 0 警告 0 错误；测试 **126 通过 / 0 失败**（新增 6 个）

---

## 6. 本轮状态

- 全程只读，未修改任何被扫描内容
- 未改动任何权重值
- 逆向材料为**静态分析**（未执行目标程序），其中"扫描项失效判定的具体规则"在原报告中亦标注为未确认

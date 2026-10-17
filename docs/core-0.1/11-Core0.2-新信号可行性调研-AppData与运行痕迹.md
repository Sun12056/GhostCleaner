# 新信号可行性调研：AppData / 运行痕迹

> 日期：2026-10-16
> 动机：`10-…md` 的结论是「现有证据集区分能力不足（AUC 0.648），调权重没用，需要新信号」。
> 目标：找出一个能区分「在用但程序目录不变」与「真残留」的**目录外使用痕迹**信号。
> 判据：能否**自动化可靠匹配** + 区分度（AUC）。

---

## 0. 结论速览

| 信号源 | 匹配方式 | 匹配率 | 区分度 | 可行性 |
|---|---|---|---|---|
| AppData（目录名精确） | 目录名归一化相等 | 14/47 | — | ❌ 关键 FP 匹配不上 |
| AppData（exe 厂商） | `VersionInfo.Company` → AppData 一级目录 | 19/47 | **AUC 0.327** | ❌ **比随机还差** |
| UserAssist（注册表） | exe 路径精确索引 | — | — | ❌ 本机器 **0 条目** |
| Recent（最近打开） | 快捷方式 TargetPath 精确匹配 | 覆盖 4 个目录 | AUC 0.571 | ❌ 不覆盖软件目录 |
| **Prefetch** | exe 名精确匹配 | 待测 | 待测 | ⚠️ **需管理员，已确认启用** |

**目前未找到可行的自动化新信号。** Prefetch 是唯一候选，待管理员验证。

---

## 1. AppData：目录名精确匹配

枚举 `Roaming` + `Local` 的一二级目录（1753 条），按归一化目录名精确匹配。

**命中 14/47**，但两个最关键的 FP 都**未命中**：

| 样本 | 真值 | 目录名匹配 | AppData 实际位置 |
|---|---|---|---|
| `D:\Weixin` | 在用 | ❌ | `…\Roaming\Tencent\xwechat` |
| `D:\wangyiyunyinyue` | 在用 | ❌ | `…\Local\NetEase\CloudMusic` |

原因：AppData 用的是**产品名**（`xwechat`），而 D 盘用的是另一套命名。
纯目录名匹配无法跨越这个别名鸿沟。

---

## 2. AppData：exe 厂商匹配（❌ 决定性失败）

改用 exe 版本资源里的 `Company` 匹配 AppData 一级目录（AppData 结构通常是 `<厂商>\<产品>`）。
读到厂商 **46/47**，匹配到 AppData **19/47**。

但采活动后算 AUC：

```
以「AppData 活动越少越像残留」为判据的 AUC = 0.327   （0.5 = 随机）
```

**比随机还差 —— 信号是反向的。** 根因：

| 样本 | 真值 | 匹配到 | 活动数 |
|---|---|---|---|
| `D:\Weixin` | 在用 | `…\Roaming\Tencent` | **6359** |
| `D:\QClaw` | **残留** | `…\Roaming\Tencent` | **6359** |

**同一厂商的多个软件共享同一个 AppData 目录**，厂商级匹配把「残留」和「在用」绑成了一个值。
同为腾讯系的 `CodeBuddy CN`（在用）也拿到 3692。

要做产品级匹配（`Tencent\xwechat`），需要 `ProductName → AppData 目录名` 的映射，
而 `Weixin → xwechat` 这类别名无法自动推导，**只能手工维护映射表，不可扩展**。

其他问题：
- `Microsoft` 这类大厂商的 AppData 目录是公共的（`T20` 残留、`VS Code` 在用都匹配到它），无区分度
- 部分软件 AppData 在 `Local` 而非 `Roaming`（网易云音乐 `Roaming\Netease` 活动仅 2，数据在 `Local\NetEase\CloudMusic`）
- `IROK Software`、`keke`、`QuarkCloudDrive` 等根本没有 AppData 目录

> **结论：AppData 方案不可行。**

---

## 3. UserAssist（❌ 不可用）

`HKCU\Software\…\Explorer\UserAssist` 按 **exe 路径**精确索引（ROT13 编码），
含运行次数与最后运行时间 —— 理论上是最理想的信号。

**本机器实测：9 个子键，Count 下条目总数 = 0。**

讽刺但合理：这类清理工具的目标用户**恰恰会清空 UserAssist**。
把它作为判据，等于依赖一个会被同类产品破坏的数据源。

> **结论：不可靠，放弃。**

---

## 4. Recent（❌ 不覆盖软件目录）

`%APPDATA%\Microsoft\Windows\Recent`：113 条可读快捷方式，其中 **100 条指向 D 盘** —— 覆盖率看着很好。

但按目录匹配后：

```
AUC = 0.571
命中的目录只有：文档(38) 桌面(22) 下载(3) Git(1) node.js(1)
```

**全是用户数据目录。** 关键 FP `Weixin`、`wangyiyunyinyue`、`IROK Software`、`VS Code` **全部为 0**。

原因：Recent 记录的是「用户打开过的**文档**」，不是「运行过的**程序**」。
软件目录根本不进 Recent。

> **结论：只对用户数据目录有效，救不了软件目录的 FP。**

---

## 5. Prefetch（⚠️ 唯一候选，待验证）

- 已确认启用：`EnablePrefetcher = 3`（HKLM，全部启用）✅
- 按 **exe 名**精确索引：`WECHAT.EXE-<HASH>.pf`，**文件名匹配即可，无需解析二进制**
- 文件的 mtime ≈ 该程序最后运行时间
- **但目录枚举需要管理员权限**（非管理员实测 `Access to the path 'C:\Windows\Prefetch' is denied.`）

对 `D:\Weixin\WeChat.exe` → 找 `WECHAT.EXE-*.pf` → 若 mtime 在 90 天内 → 近期运行过 → 在用信号。
这是**精确匹配、无歧义**，且直接回答"这个程序还在不在跑"。

### 验证方法（需管理员 PowerShell）

见 §6。

### 已知风险

1. 非管理员下不可用 → 与改动 5 一样，降级为「来源不可靠」，不得产生正向残留证据
2. SSD 上 Windows 可能禁用 Prefetch（本机器启用，但其他机器不一定）
3. Prefetch 只保留最近 128（Win10）/ 1024（Win11）个条目，冷门程序可能已被挤出
4. 后台服务的 exe 也会产生 Prefetch —— 对 `DriverGenius` 这类「残留服务仍在跑」的样本可能误判为在用

---

## 6. 下一步

管理员 PowerShell 执行，验证 Prefetch 的匹配率与区分度：

```powershell
$cut = (Get-Date).AddDays(-90)
$pf = @(Get-ChildItem 'C:\Windows\Prefetch' -Filter *.pf -ErrorAction SilentlyContinue)
"Prefetch 文件数: " + $pf.Count
"90天内有更新: " + @($pf | Where-Object LastWriteTime -gt $cut).Count

$j = Get-Content 'd:\文档\GitHub\GhostCleaner\validation\raw\run-fix-admin.jsonl' -Encoding UTF8 |
     Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json }
$truth=@{}; $conf=@{}
foreach ($l in (Get-Content 'd:\文档\GitHub\GhostCleaner\validation\raw\to-label.md' -Encoding UTF8)) {
  if ($l -match '^\|\s*U-\d+\s*\|') { $c=$l.Split('|')|ForEach-Object{$_.Trim()}; $truth[$c[2]]=$c[8]; $conf[$c[2]]=$c[9] } }
$res=@()
foreach ($r in $j) {
  $t=$truth[$r.scannedPath]; $cf=$conf[$r.scannedPath]
  if (-not $t -or $t -eq '' -or $t -eq '不确定' -or $cf -eq '猜') { continue }
  $exe=$null
  try { $exe = @(Get-ChildItem -LiteralPath $r.scannedPath -Filter *.exe -Recurse -EA 0 |
                 Sort-Object Length -Descending | Select-Object -First 1).Name } catch {}
  $hits=@(); if ($exe) { $hits=@($pf | Where-Object { $_.Name -like ($exe -replace '\.exe$','') + '-*.pf' }) }
  $res += [PSCustomObject]@{ Leaf=(Split-Path $r.scannedPath -Leaf); Truth=$t; Exe=$exe;
                             Pf=$hits.Count; Recent90=@($hits|Where-Object LastWriteTime -gt $cut).Count }
}
$res | Sort-Object Recent90 -Descending | Format-Table -AutoSize
$p=@($res|Where-Object Truth -eq '残留'|ForEach-Object Recent90); $n=@($res|Where-Object Truth -eq '在用'|ForEach-Object Recent90)
$t2=0;$w2=0; foreach($a in $p){foreach($b in $n){$t2++; if($a -lt $b){$w2+=1} elseif($a -eq $b){$w2+=0.5}}}
"AUC = " + [Math]::Round($w2/$t2,3)
```

判读：`AUC ≥ 0.8` 才值得作为新证据落地；`< 0.65` 则不值得。

---

## 7. 若 Prefetch 也不行，怎么办

必须承认一个**产品级现实**：

> 目录内的信息（时间戳、文件活动、卸载器、版本资源）**不足以**
> 区分「重装后继续使用的软件」与「真废弃的软件」—— 两者特征几乎完全一致。

届时正确的应对不是继续找信号，而是**改变产品策略**：

1. **不给 `Quarantine`，只给 `Review`**（改动 1 已部分落地：有卸载器 → 引导用自带卸载器）
2. **把"在用/废弃"的最终判断交还用户**，Core 只负责把证据摆出来（近期活动、最后修改、是否有卸载器、是否在运行）
3. 若要追求自动化判定，必须引入**跨时间观测**（同一目录多次扫描比对趋势），这是 v0.2+ 的能力

---

## 8. 本轮状态

- 全程只读，未修改任何被扫描内容
- 未改动任何生产代码（本轮为纯调研）
- 构建 0/0，测试 113 通过（上一轮改动后状态，本轮未再改动）

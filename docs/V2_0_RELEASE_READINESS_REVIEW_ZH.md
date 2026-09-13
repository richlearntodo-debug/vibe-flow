# Vibe Link V2.0.0 · 发布就绪评审（2026-09-13）

本评审由两路只读审计 + 本机实测合成：一路审文档与版本标识，一路审代码、隐私、仓库卫生与打包；
所有结论都带文件/行号或实测命令，未验证项单独列出。审计时的 HEAD 是 `13d8e81`，评审中已修一项 P0（见 §1）。

---

## 0. 结论

| 目标 | 结论 | 说明 |
| --- | --- | --- |
| **作为正式稳定版 V2.0.0 发布** | **暂不可以** ✗ | `docs/V2_0_RELEASE_CHECKLIST_ZH.md:53-58` 列的 5 项正式版前提**一条都未满足**（签名 / 干净账户生命周期 / 无 VB-CABLE 机器首启 / RC003 隔离 / CI 未跑），另加本评审新增的 3 项发布面缺陷（§2）。 |
| **作为 `v2.0.0-rc` 公测发布** | **可以，但先把 §2 的三项修掉** ✔ | 功能面已经稳：冻结采集件三处一致、载荷 51/51、界面矩阵 12/12、三套自测全绿、隐私面干净（§5）。发布说明与页面却还是**候选版 2 + 旧品牌 + 错误工具指引**，这些是"发出去就会被用户看到"的问题。 |

---

## 1. 本轮已修（P0，功能缺陷）

**开机自启的名字写读不一致** ✗→✔（`scripts/VibeMic.cs`，提交 `0f8fdcd`）

- 原状：`SetLaunchAtStartup` 写 `"Vibe Flow"`，却删除与回读 `"Vibe Link"`；`HasLaunchAtStartupRegistration` 与 `ReadStartupExecutableDirectory` 也读 `"Vibe Link"`。
- 用户可见后果：**关不掉开机自启**（要删的项名字不对）、每次启动都记 `STARTUP REPAIRED=true reason=registry_entry_missing_or_stale`、协调结果永远 `applied=false`。
- 修法：新增常量 `StartupRegistryValueName = "Vibe Flow"`（与 `installer/VibeFlow.iss:355` 同一个字面量），写入/删除/回读/两处读取全部走它；旧名字（`言灵` / `Vibe Mic` / `声启 MIC`）两条路径都清。
- 证据：修复前本机日志 `STARTUP RECONCILE required=True configured=True onboarding_resume=False applied=False` ✗；修复后同一行 `applied=True` ✔ 且不再出现修复记录；自测新增"常量 + 旧名清单 + 一次性注册表键往返"断言；`validate.js` 新增门禁（任何启动项读写用字面量即失败、安装器字面量必须一致），**负控**把一处读取改成字面量 → 门禁报 `The startup registration name is not shared between the app's writes, its reads and the installer` ✔。

---

## 2. 发布前必修（P0，发布面缺陷）

1. **发布正文仍在教用户选已下线的工具** ✗
   `docs/GITHUB_RELEASE_BODY_ZH.md:83`：「选**微信输入法 / Typeless / Windows 语音输入 / 其他**，并真的说一句」；同文件 `:158` 也把 Typeless 列为第三方产品。
   该文件被 `BUILD_RELEASE.ps1:233` **原样复制**成 `release/RELEASE_BODY_v2.0.0.md`，即公开发布页正文；`scripts/validate.js` 与 `Test-ReleaseArtifacts.ps1` 都不检查这里的工具清单。
   修法：改写成三个工具，并把标题/版本/下载链接/截图链接从 `v2.0.0-candidate.2` 换成本次要发布的 tag。
   **已修 ✔（2026-09-13）**：正文工具清单已收敛为 微信输入法 / 网易八哥说 / 其他语音工具；标题、版本、下载链接、截图链接与 tag 全部换成 `v2.0.0-candidate.3`；发布正文标题为「Vibe Link V2.0.0 候选版 3」，并补上「安装包未签名」与 SmartScreen 步骤。

2. **品牌分裂** ✗
   应用内是 **Vibe Link**（`scripts/VibeMic.cs:22`），安装包与系统可见处仍是 **言灵 Vibe Flow Remote**：`installer/VibeFlow.iss:1,58,60,61,231`，实机证据 —— 开始菜单目录 `…\Programs\言灵 Vibe Flow Remote`、快捷方式 `言灵 Vibe Flow Remote.lnk`、`卸载言灵.lnk`、桌面快捷方式、卸载项显示名 `言灵 Vibe Flow Remote 2.0.0`；`VIBE_MIC_VERSION.md:1`、`README.md:1`、发布正文标题同样还是言灵；`docs/V2_0_UPDATE_SUMMARY_ZH.md:106` 甚至断言言灵标题格式才是对的。
   另有：`scripts/VoxDeckInputBridge.cs:15` 的 `AssemblyProduct("Vibe Flow Remote")` 与宿主 `Vibe Link` 不一致，成品里两个 exe 的产品名不同。
   修法：二选一并统一（推荐统一到 **Vibe Link**，因为站内已改），涉及 .iss、3 处标题、卸载显示名与 `V2_0_UPDATE_SUMMARY_ZH.md`。
   **已修 ✔（2026-09-13）**：文档侧已统一为 **Vibe Link**（`VIBE_MIC_VERSION.md:1`、`README.md:1`、发布正文标题、`V2_0_UPDATE_SUMMARY_ZH.md:106` 不再断言言灵标题格式）；安装包与卸载显示名由 `installer/VibeFlow.iss` 本轮同步统一。

3. **用户指南里的发布资产哈希是错的** ✗
   `docs/V2_0_USER_GUIDE_ZH.md:63-64` 给的 Setup/ZIP 哈希与**实际产物**和 `frozen-parameters.json` **三方互不相同**，且没写 `SHA256SUMS.txt`。这是仓库里唯一一处给用户核对哈希的地方。
   修法：由发布链生成后回填，或改成"以 `SHA256SUMS.txt` 为准"。

4. **冻结记录与实际不符** ✗
   `frozen-parameters.json` 的 `commit` 是 `32b18415…`（实际 HEAD `0f8fdcd`），`releaseAssets` 与 `release/` 实际产物不一致（`frozenAt 01:49` vs 产物 `13:49`），`frozenAt` 与 `docs/V2_0_FROZEN_PARAMETERS_ZH.md:6` 的日期又不一致（该文件 `:4` 明说两者必须一致）；`docs/V2_0_BASELINE_LOCK_ZH.md:13` 还写着第三个 commit。
   修法：发布前用脚本一次性回填"commit + 三个资产哈希 + 时间戳"，并加门禁（现在没有任何门禁读这个文件）。
   **已修 ✔（本轮）**：`frozen-parameters.json` 改由发布链在打包时按 HEAD 一次性回填 commit、三个资产哈希与 `frozenAt`（该文件不在本次文档改动范围内）。

5. **12 个未跟踪的草稿会随手进发布提交** ✗
   `git status --untracked-files=all` 恰有 12 项：`.agents/**`（4 个 SKILL）、`.codex/**`（配置与 3 个 agent）、`templates/AGENTS.merge.md`、`qa/ACCEPTANCE_TESTS.md`、`notes-export.md`（**真实笔记正文导出**）、`custom-button-test-result.json`（**含 token**）。
   `.gitignore` 已正确忽略 `release/`、`tools/`、构建产物与日志，但**没有**忽略这六类。
   修法：移出仓库或加 `/.agents/`、`/.codex/`、`/qa/`、`/templates/`、`notes-export.md`、`custom-button-test-result.json`、`/Flow/` 到 `.gitignore`；含 token 的那份建议直接删除。
   **已修 ✔（本轮）**：仓库卫生一侧按此处置这 12 项未跟踪草稿（含 token 的文件删除），并补 `.gitignore` 规则；该改动不在本次文档改动范围内。

---

## 3. 已执行的决定（P1，2026-09-13 本轮）

| # | 事项 | 本轮决定与证据 |
| --- | --- | --- |
| a | VB-CABLE 驱动包 | **不把第三方二进制入库**：`RESTORE_BUILD_DEPS.ps1` 现在**尽力下载**官方包并按固定哈希校验（失败只警告不中断），`BUILD_RELEASE.ps1` **不再因缺包 fail**（缺失时输出 `VB-CABLE bundle: absent`，安装时由 `Install-VBCable.ps1` 在线获取并校验）。实测：把包移走后整条发布链 exit 0、载荷 50 文件；放回后 `VB-CABLE bundle: included (SHA-256 verified)`、载荷 51 文件。 |
| b | 未签名 | **按已披露处理**：发布正文、README、快速上手、安装指南、`CODE_SIGNING_ZH.md` 都写明"当前未签名 + SmartScreen 未知发布者 → 更多信息 → 仍要运行 + 校验 SHA-256"；签名仍走 `VIBE_FLOW_SIGN_PFX` / `…_THUMBPRINT` 两个环境变量，CI 无证书时明确打印未签名。 |
| c | RC003 按键隔离 | **作为已披露限制接受**：向导、首页、快捷键页、自检页、导出诊断、README 与已知限制均写明"遥控器在线时 F5 被拦截，离线时普通键盘 F5 原样直通；不能与签名过滤器等同"。 |
| d | CI | 分支已推 `main` + `feature/v2-off-key-loop`。CI 实测：#92/#94 在 **第 11 步 `Build release`** 失败（就是缺驱动包，见 (a)）；修好后的 #95 里**第 11 步已通过** ✔，但暴露了下一个问题 —— **第 12 步 `Test clean install…` 秒退**：`Test-ReleaseLifecycle.ps1` 的"可弃用账户"守卫把前序步骤自己产生的 `%LOCALAPPDATA%\Vibe Flow Remote\UserData` 当成脏机器。已新增 `scripts/tests/Reset-LifecycleSandbox.ps1`（用假根实测可清理四类残留且幂等）并在三个生命周期步骤前各调用一次。**随后 CI 仍失败，于是给三步加了 `::error::` 注解诊断**（运行日志需登录才能读，注解可通过公开 API 读）→ 拿到真因：**`Test-ReleaseLifecycle.ps1` 在英文 runner 上根本无法解析** —— 文件是**无 BOM 的 UTF-8**，Windows PowerShell 5.1 按 ANSI 读取，`"上键"` 变成 `"ä¸Šé”®"`，报 `Missing '=' operator after key in hash literal`。已给**含中文的 6 个 .ps1 + 安装器 .iss 全部加 UTF-8 BOM**（逐个用 5.1 解析器验证 0 错误），并新增门禁（含非 ASCII 的脚本缺 BOM 即失败，负控已验证）。**仍待办** ✗：49 个 `.cs` 同样无 BOM —— 其中 `scripts/VibeMicAtvvCapture.cs` 是**冻结采集件源码**（其 SHA-256 在 4 处 + 版本文档里钉着），加 BOM 会**有意**改变这个冻结哈希，需单独一次决定，故本轮未动。 |
| e | 更新器零测试 | **已补**：`RunUpdaterSelfTests()` 进入主机自测 —— GitHub-only HTTPS 白名单（含 `github.com.evil.example` 负例）、版本解析（`v2.0.0-candidate.3` → 2.0.0.0 与 7 个非法值）、`SHA256SUMS.txt` 读取（`*` 前缀与三种畸形清单）、资产查找大小写无关；**负控**：放宽主机白名单 → 自测报 `The updater accepted an untrusted asset URL: https://evil.example/…`。仍未覆盖的是真正联网的 `GetLatest` / `DownloadAndVerify`（需要已发布版本）。 |
| f | 真机与生命周期 | **生命周期验收已完成** ✔（2026-09-13，本机清空安装目录/用户数据/卸载记录/启动项后跑 `scripts/Test-ReleaseLifecycle.ps1`，用的是**最终发布产物**）：① 干净安装 → 组件校验 → 卸载 ✔；② 未配置干净安装 → 卸载 ✔；③ **V1.5 → 候选版升级**（含"已下线的 provider 在升级后**首次加载时**迁移为微信输入法稳定基线"的新断言）→ 组件校验 → 二次升级 → 卸载 ✔。过程中修掉脚本自身两个问题：`Get-ConfigContractProjection` 用 ANSI 读 UTF-8 配置导致中文键被判为"被改动"（假失败）；清理时对非空目录 `Remove-Item` 不带 `-Recurse` 触发 PS 5.1 的 `NullReferenceException`。**仍需你做**：真机说一句（语音链路）、一台**没有 VB-CABLE** 的机器首启（或等 CI 用同一条脚本覆盖）。 |

### 本轮一并做完的发布面修正

- 安装器与系统可见处全部改名 **Vibe Link**（`MyAppName` / 发布者 / 开始菜单 / 卸载项 / 欢迎页），并加 `UsePreviousGroup=no` + `[InstallDelete]` 清理旧版残留：实测升级后只剩 `Vibe Link` 组与 `Vibe Link.lnk`，卸载项 `Vibe Link 2.0.0` / 发布者 `Vibe Link Contributors`。
- 发布正文（`docs/GITHUB_RELEASE_BODY_ZH.md`）改名为 `Vibe Link V2.0.0 候选版 3`，工具清单改为三个、全部链接指向 `v2.0.0-candidate.3`，补上未签名说明与 V1.5 回落说明。
- 用户指南**不再抄写会过期的资产哈希**，改为指向随包 `SHA256SUMS.txt`；`frozen-parameters.json` 回填 product/tag/commit/三个资产哈希，并新增门禁读取该文件（此前没有任何门禁读它）。
- 新增门禁：可选驱动包行为、恢复脚本的哈希校验、更新器自测存在、用户指南哈希指针、冻结记录字段。
- `.gitignore` 补齐 12 个草稿路径（`.agents/`、`.codex/`、`templates/`、`qa/`、`Flow/`、`notes-export.md`、`custom-button-test-result.json` 等）。

## 3.1 原始待决策清单（保留备查）

| # | 事项 | 现状（实测） | 影响 |
| --- | --- | --- | --- |
| a | **VB-CABLE 驱动包不在仓库** | 本机存在且哈希与 `BUILD_RELEASE.ps1:157` 一致，也在成品载荷里；但 `tools/` 被 `.gitignore:24` 忽略，从未入过任何 checkout，`RESTORE_BUILD_DEPS.ps1` 也不下载它 → **干净克隆/CI 的 `Build release` 必炸**（`BUILD_RELEASE.ps1:154` 抛错）。最终用户不受影响（`Install-VBCable.ps1` 会回落到官网下载）。 | 决定：入库（注意 VB-Audio 再分发条款）/ CI 下载 / 让该任务可选 |
| b | **未签名** | `VibeFlow-Setup.exe` 与三个 exe 全部 `NotSigned`；CI 无证书 secret | 用户会看到 SmartScreen「未知发布者」；发布页必须写明步骤 |
| c | **RC003 设备级按键隔离未实现** | 界面四处已披露"前台应用可能收到录音键"（`VibeMic.cs:16999`、向导、自检、导出诊断） | 要么实现+签名，要么作为**已披露限制**接受 |
| d | **CI 自 2026-09-03 未跑** | 分支已推（`0f8fdcd`），但没有一次真正的 CI 结果 | 发布前应至少跑绿一次 |
| e | **更新器零测试** | `SecureUpdateClient`（`VibeMic.cs:26405-26659`）无任何自测/门禁：下载、校验、URL 校验、版本解析都没测 | 一个会下载并执行安装包的组件不该裸奔 |
| f | **真机与生命周期未复验** | 干净账户安装→升级→卸载、无 VB-CABLE 机器首启、最近改动后的真机语音、更新端到端 | 需要一台一次性账户 / 一台无驱动机器 / 你按一次遥控器 |

---

## 4. 文档一致性（P2，会误导用户或审查者）

1. **电源键自相矛盾**：`docs/V2_0_FROZEN_PARAMETERS_ZH.md:105-115`（§10.1 三层全空、桥 `enabled=false`）vs 同文件 `:274`/`:312-314`（§12.5/§13.2 短按 DeepSeek / 长按 B 站 / 双击任务切换、桥 `enabled=true,suppress=true`）vs `docs/V2_0_KNOWN_LIMITATIONS_ZH.md:17`（"现已两处都清空"）。**实机与 §13 一致**（`gesture-layers.json`、`vibe-mic-config.json` 都是 DeepSeek 动作）→ §10.1 与 KNOWN_LIMITATIONS:17 是错的。
2. **长按时长**：`docs/V2_0_USER_GUIDE_ZH.md:293` 写"约 650 ms"、`FROZEN:40` 写"界面写 0.65 秒"，代码是 `LongPressMs = 600`（`features/GestureLayerPolicy.cs:27`）、界面渲染"0.6 秒" → 两处都错。
3. **页数**："五页"（`V2_0_HARDWARE_TEST_MATRIX_ZH.md:18`、`V2_0_AUTOMATED_TEST_REPORT_ZH.md:17,30`、`V2_0_INSTALLER_GUIDE_ZH.md:3`）vs 六页（代码与其余文档）。
4. **Capture & Ask / Browser Remote Lite 是否属于产品**：`README_VIBE_MIC.md:12-13`（随包发布）把它们当亮点，`V2_0_RELEASE_NOTES_ZH.md:60`、`FEATURES_ZH.md:76`、`KNOWN_LIMITATIONS_ZH.md:47` 又否认；二进制里两者都可达（托盘"截图提问"、快捷键页"浏览器遥控"）→ 否认的那三处不准。
5. **DPI 与更新流程**：`V2_0_RELEASE_CHECKLIST_ZH.md:15` 说 125/150/200% 已验证，`V2_0_UPDATE_SUMMARY_ZH.md:92` 与硬件矩阵说未验证；`docs/CODE_SIGNING_ZH.md:56-60` 把"下载→校验→安装"写成已实现行为，checklist:40 说从未跑过。
6. **旧文档/旧日志**：`docs/V2_0_PROGRESS.md:818,872,896` 仍写 Live HUD「未完成/暂不可用」（代码与其它文档相反）；`:5529,5569,5592` 引用不存在的 `docs/images/03-shortcuts-layout.png`；`CHANGELOG.md:15-27` 在"当前 2.0.0 candidate"标题下以现在时描述已下线的讯飞/搜狗/豆包；`frozen-parameters.json:69` 把已下线的讯飞写成"现实约束"；`README.md:36/38` 重复 V1.5 下载入口；`docs/V2_0_UPDATE_SUMMARY_ZH.md:35` 错字"应内选择"。
7. **随包的历史文档把已下线工具写成现状**：`docs/V1_5_USER_GUIDE_ZH.md:40`、`docs/RELEASE_NOTES_ZH.md:82` 仍在包里（这是刻意保留历史，但用户读包内文档会看到"支持 Typeless/豆包/Windows 语音输入"）。

---

## 5. 已经站得住的部分（可以据以发布的信心来源）

- **冻结件完整** ✔：`VibeMicAtvvCapture.exe` 在根目录、`release/`、安装目录三处同为 `B62DE035…2E683`；采集件源码 `736017A0…37E2` 与文档一致；11 处独立校验，运行时还会用 `FileMatchesSha256` 复核。
- **打包** ✔：载荷 51 文件 **51/51 逐文件一致**；52 个拷贝源全部存在；包内不含 `vibe-mic-config.json` / 采集件源码 / 采集件构建脚本。
- **门禁与自测** ✔：`node scripts/validate.js`、`VibeMic.exe --self-test`、桥 `--self-test`、`Test-V2FeatureSuite.ps1`、`Test-InstallerRequirements.ps1`、发布身份与产物校验全绿；界面矩阵 **12/12**；安装器 exit 0。
- **三工具状态一致** ✔：微信输入法 / 网易八哥说 / 其他语音工具；默认快捷键两两不同；微信输入法钉死单击切换；粘贴回落只在微信输入法；免驱动模式下主机能唤起八哥说/自定义；已下线的五个值一律**通配迁移**为微信输入法。
- **隐私面干净** ✔：成品宿主**只有一条**外呼 —— `api.github.com/.../releases/latest`（无 body/无 query/无机器标识，默认开启的更新检查，安装需两次确认）；**不读取转写文字**（仅 `GetClipboardSequenceNumber`），无遥测、无崩溃上传；AI 供应商相关源码根本不进构建。
- **代码卫生** ✔：无 TODO/FIXME/HACK、无 `NotImplementedException`、无 `#if`、无硬编码凭据、无被吞掉的发布关键异常；唯一诊断开关 `VIBE_FLOW_TRIGGER_ONLY` 默认关闭。

---

## 6. 建议的发布路径

1. 先做 §2 的五项（都是文本/元数据/仓库卫生，不改功能），再决定 §3 的 a/b/c；
2. 让 CI 在本分支跑绿一次（§3d），并至少补一个更新器的最小自测（§3e）；
3. 以 **`v2.0.0-rc`**（或你选定的候选名）发布，下载页显式写明：**未签名**、**录音键可能被前台应用收到（F5 原样直通）**、**V1.5.0 仍是推荐稳定版**；
4. 正式稳定版前补齐 §3 的 f（一次性账户生命周期 / 无 VB-CABLE 机器 / 一次真机语音）。

---

## 7. 附：本评审用到的复核命令

```powershell
node scripts/validate.js
.\VibeMic.exe --self-test
.\scripts\tests\Test-V2FeatureSuite.ps1
.\scripts\tests\Test-InstallerRequirements.ps1
.\scripts\tests\Test-ReleaseIdentity.ps1
.\scripts\tests\Test-ReleaseArtifacts.ps1
.\BUILD_RELEASE.ps1            # 含界面矩阵 12 例
Get-AuthenticodeSignature .\release\VibeFlow-Setup.exe
```

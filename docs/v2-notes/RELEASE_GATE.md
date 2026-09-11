# V2 Notes Deck 阶段门禁记录

日期：2026-09-06  
规格：`V2-NOTES-DECK-20260906-R1`  
阶段：6，集成回归与候选构建  
结果：**PASS WITH MANUAL HARDWARE CHECKS**

## 本阶段实际范围

- 补齐项目级只读 reviewer 配置、产品 UI/稳定数据/release loop Skill、AI 提示词、决策、兼容性、来源和验收资产。
- AI BYOK 请求禁止自动重定向，3xx 返回 `AI-REDIRECT-BLOCKED`；不自动重试或切换供应商。
- 本阶段没有修改 Capture、录音参数或用户配置文件；`scripts/VoxDeckInputBridge.cs` 在当前工作区存在既有版本/输入隔离改动，Notes 功能没有继续扩大其录音核心范围，真实设备行为仍按未验证记录。
- 回归审查发现并修复：快捷入口热键注册失败原子回滚；按键设置等待 Bridge ACK；AI 设置空 Key 保留已存在 DPAPI Key；AI 取消 continuation 不再读取已取消 Task.Result。
- 最终 auditor 复核后进一步修复：现代按键下拉和恢复动作也等待 Bridge ACK；原子回滚保存旧 modifiers/key；DPAPI secrets 主文件损坏回退 `.bak` 并有 focused test。
- 本轮阶段修复：`NotesStore.TrySave` 增加文档级 revision CAS，主窗与 Deck 的 Load→Append/Delete/Restore→Save 竞争在旧快照时返回 `NOTES-REVISION-CONFLICT`；`TestDocumentRevisionConflict` 已覆盖旧快照不能覆盖新便签。
- 本轮进一步修复：严格 JSON 备份格式/驱动器相对路径、损坏主文件下有效 `.bak` 保留、Deck 关闭 flush 失败可重试，以及退出取消时先 flush 再注销热键；AI prompt pack 加载/回退、超时/HTML/3xx/Retry-After/真实取消和 provider 未知字段保留均有 focused tests。
- 本轮审查修复：快捷入口失败回滚保存并恢复实际运行时 modifier/key；AI secrets 在备份恢复后保存不会覆盖有效 `.bak`，主文件单项解密失败会尝试备份；AI 连接测试回调增加 generation/CTS 身份门禁，旧测试不会覆盖新状态。对应 focused tests 已通过。

## 构建与测试

| 命令 | 结果 |
|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File BUILD_RELEASE.ps1` | PASS；含 Bridge/Host 构建、Inno Setup 6.7.3、artifact validator 和 V2 focused suite |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS（停止运行 Host 释放锁后构建） |
| `powershell -NoProfile -ExecutionPolicy Bypass -File BUILD_HARDWARE_CANDIDATE.ps1 -OutputRoot .\artifacts` | PASS；生成含 prompt pack 的硬件候选 ZIP |
| `npm test` | PASS |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Test-V2FeatureSuite.ps1` | PASS（含文档 revision CAS 冲突测试） |
| `.\VibeMic.exe --self-test` | PASS（exit 0） |
| `.\VoxDeckInputBridge.exe --self-test` | PASS（exit 0） |
| `.\VibeMicAtvvCapture.exe --self-test` | PASS |
| `git diff --check` | PASS（仅有既有 CRLF 提示） |

Focused suite 覆盖 Smart Focus 多窗口、Capture & Ask、Browser Remote Lite、Live HUD、Notes
Deck AI、快捷入口；新增备份恢复、关闭顺序、prompt pack、失败分类、未知字段和重定向分类测试通过。

硬件候选：`artifacts/Vibe-Flow-v2.0.0-Hardware-Candidate-20260906-135228.zip` 仍保留作历史证据；正式候选为 `release/Vibe-Flow-Windows-x64.zip` 与 `release/VibeFlow-Setup.exe`，具体 SHA-256 以同批生成的 `release/SHA256SUMS.txt` 为准。`Test-ReleaseArtifacts.ps1` 已通过；包内 prompt pack 5 个文件，根/包 Host 哈希一致，Capture 哈希保持冻结。安装器为本地未签名候选，未执行安装。

## 冻结与回归

- Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- Capture 二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 按住录音/松开结束、设备自然边界、尾音排空、原按键时序和录音键专用路径未改。
- 代码搜索未发现录音链路读取/回填第三方转写或自动 Enter；AI/便签/Deck 不进入录音线程。
- 旧用户配置、`project-spaces.json` 和未知字段未删除；V2 新配置独立保存。

## 实际原生 UI

- 已启动 `C:\Users\Admin\Documents\ChatGPT\vibe -flow\VibeMic.exe`，窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”。
- 已检查首页、便签页、快捷入口页和自检页；首页明确“先确认 APP 输入目标，再按住录音键说话，松开结束”。
- 快捷入口显示 ChatGPT 为“对话工作台”，当前状态“已打开 · 输入框待确认”；未把打开应用等同于 UIA 输入框验证。
- 便签页显示本地优先、无模型也可用；自检页区分真实链路、等待按键和输入框目视确认。
- 未完成 125/150/200% DPI、深色/跟随系统、1366x768 键盘全路径的完整人工矩阵。
- 本轮实际观察便签工具栏和 Deck 自动保存状态；`qa/stage3-notes.png` 保留为 UI 证据。
- 正式包等待启动完成后也已由主 Agent 和只读 auditor 检查首页；早期数秒无窗口是 Bridge/Capture 启动等待，不是 artifact gate 失败。
- 最后一次候选启动确认 `VibeFlow.exe` 与 `VoxDeckInputBridge.exe` 均在运行；当前配置 `minimizeToTray=true`，主窗口按设计隐藏到托盘。Computer Use 对该应用返回未获批准，未执行点击/导航，不能把原生 UI 全路径标为已验证。

## 只读审查

- `vibeflow_auditor`/recording path audit：BLOCKER 0；最终回执中的 HIGH（ACK 虚报、热键回滚、secrets 回退）已逐项修复并重测。工作区 Bridge 输入隔离改动未被本阶段作为新功能绕过，需保留其未提交状态并继续真机复核。
- `vibeflow_regression_reviewer.toml` 与 `vibeflow_ux_reviewer.toml` 已加载为只读项目角色；本轮 reviewer 回执单独追加。
- reviewer 不得操作共享前台或写入产品文件。
- 最新 reviewer 结论：BLOCKER/HIGH 0；新建便签 CAS、自动保存延后、清空后草稿丢弃回归已覆盖。完整硬件/真实供应商/ChatGPT UIA/DPI/主题/安装迁移仍按 NOT_RUN 记录。

## 2026-09-07 最新阶段门禁复核

### 范围与变更

- 便签页键盘导航、两栏最小宽度、搜索提示和搜索/预览一致性。
- Deck 固定位置/大小、置顶独立状态、显示器/DPI 工作区重新约束和录音优先二次检查。
- provider/prompt 备份恢复、AI 操作结果语义锁定、Smart Focus 前台变化取消。
- 未修改 Capture 源码、Capture 二进制、录音参数、Raw Input、Hook、设备过滤或冻结按键时序。

### 验证结果

| 检查 | 结果 |
|---|---|
| `BUILD_VIBE_MIC.cmd` | PASS |
| `BUILD_RELEASE.ps1` | PASS；安装器、ZIP、SHA-256 同批生成 |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS；Notes/AI、Focus、Capture Ask、Browser、HUD、快捷入口 |
| Host/Bridge/Capture `--self-test` | PASS；退出码 0，Capture 输出 PASS |
| `Test-ReleaseArtifacts.ps1` | PASS |
| Capture SHA-256 | PASS；源码 `736017A0...74137E2`，二进制 `B62DE035...0582E683` |
| 两位只读 reviewer | BLOCKER 0、HIGH 0 |
| Computer Use 原生 UI | PASS（首页、两栏便签、搜索提示、Deck 基础路径） |

### 未验证与发布边界

真实设备、真实 provider、ChatGPT UIA、完整 DPI/主题/多显示器、安装升级卸载和签名仍为 `NOT_RUN`。本门禁只允许结论 `PASS WITH MANUAL HARDWARE CHECKS`，不允许宣称正式稳定版或真机端到端通过。

## 未验证与限制

| 维度 | 状态 | 限制 |
|---|---|---|
| BUILD_TEST | PASS | `BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、Host/Bridge/Capture self-test、artifact validator |
| GUI | PASS（基础路径） | 完整 DPI/主题/键盘矩阵未跑 |
| REAL_PROVIDER | NOT_RUN | 未使用真实 Key 或付费 API |
| REAL_APP_FOCUS | NOT_RUN | ChatGPT UIA Edit 学习/聚焦未验证 |
| HARDWARE | NOT_RUN | RC003 100 次、VB-CABLE、微信输入法端到端未验证 |
| DATA_MIGRATION | NOT_RUN | 安装器已编译但未执行安装升级/卸载/回退 |

因此不能宣称正式稳定版、真实供应商通过、ChatGPT 输入框已验证或遥控器真机通过。

## 2026-09-07 最终收口复核

- 新增配置文件的缺失/非法 `schemaVersion` 现在会进入损坏/备份恢复路径；合法的 `schemaVersion=0` Notes 迁移仍可幂等升级。
- `npm start` 已与 `START_VIBE_FLOW.cmd` 对齐；根启动器会优先启动本地正式候选，再回退开发 Host。入口修复已由 `npm test` 静态门禁覆盖。
- 新候选已重新构建并通过 `Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`；本批 `release/SHA256SUMS.txt`：Setup `3C75466685D69BF92717B14F3550F443AF14C661833224858809D0B027B2000A`，ZIP `F29294A6096421C7766DDE656C9DAA7CB52E1631D0163A1BD6E995DF9B7413B8`。
- Computer Use 实际启动新候选并检查首页、两栏便签、搜索提示、Deck 和置顶独立性；没有修改用户的最终置顶偏好。
- 冻结 Capture 源码/二进制哈希保持 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。两位只读 reviewer：BLOCKER 0、HIGH 0。

最终结果：**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、VB-CABLE、第三方语音工具、ChatGPT UIA、DPI/主题、安装升级/卸载和代码签名仍为 `NOT_RUN`，候选未发布。

## 当前候选增量（2026-09-06）

- F2 本地系统提示词设置已补齐：可编辑、独立保存、原子恢复和二次确认恢复默认；空模型时也可以单独保存提示。
- `AiPromptStore` 的 `.bak` 恢复不会被保存/重置动作覆盖；`NotesDeckAiTests` 已增加损坏主文件、恢复和安全边界回归。
- 文案门禁已重新通过：README、功能看板、快速开始、V2 用户指南和更新说明都明确 Notes Deck 是当前主线，旧 Project/Capture/Browser 功能只做兼容索引。
- 最终候选（本轮重建）哈希已由 artifact gate 读取并写入 `release/SHA256SUMS.txt`；同批根/包 Host、Bridge 哈希一致，Capture 二进制仍为冻结哈希。
- 本轮 `BUILD_RELEASE.ps1`、`npm test`、`Test-ReleaseArtifacts.ps1`、V2 focused suite、三组件 self-test 和 `git diff --check` 均通过；安装器未签名、未安装、未发布。

## 2026-09-07 文案定位收口复核

### 本阶段变更

- 首页和托盘的旧 `Context Deck` 入口改为“遥控器状态”。
- `ContextDeckForm` 的标题、可访问名称、眉标题和上下文行改为“遥控器状态 / 快捷入口”。
- 快捷入口页及兼容执行回执改用“APP 快捷入口”，底层 `ProjectSpace` 数据与执行器仅保留兼容用途。
- `scripts/validate.js` 的产品文案门禁同步到新的真实 UI 文案。

### 证据

| 检查 | 结果 |
|---|---|
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `powershell -NoProfile -ExecutionPolicy Bypass -File BUILD_RELEASE.ps1` | PASS；候选安装器、ZIP、SHA-256 同批生成 |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS |
| `VibeMic.exe --self-test` | PASS |
| `VoxDeckInputBridge.exe --self-test` | PASS |
| `VibeMicAtvvCapture.exe --self-test` | PASS |
| `Test-ReleaseIdentity.ps1` | PASS |
| `Test-ReleaseArtifacts.ps1` | PASS |
| `git diff --check` | PASS（仅既有换行提示） |
| Computer Use：首页、快捷入口页、遥控器状态窗口 | PASS（基础路径） |

### 冻结与未验证

Capture 源码 SHA-256 仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，二进制仍为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、完整 DPI/主题、多显示器、睡眠唤醒、安装升级卸载和代码签名仍为 `NOT_RUN`。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。未 Push、未发布、未签名。

## 2026-09-07 快捷入口热键冲突回执修复

- Reviewer 发现的 MEDIUM 已处理：快捷入口 `RegisterHotKey` 冲突现在返回统一 `ActionResult`；保存成功与快捷键生效分开表达。
- 冲突且旧绑定恢复成功：`QUICK-ENTRY-HOTKEY-CONFLICT` warning，提示更换快捷键。
- 冲突且旧绑定恢复失败：`QUICK-ENTRY-HOTKEY-ROLLBACK-FAILED` error，提示重启后检查入口。
- `QuickEntryTests` 覆盖三态；没有修改录音键、Raw Input、低级 Hook、设备过滤或 Bridge 时序。
- 最新完整候选构建、自动测试、三组件 self-test 和 artifact gate 均通过。真实 Windows 热键占用、RC003、ChatGPT UIA、DPI/主题、安装迁移仍按 `NOT_RUN` 记录。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。

## 2026-09-07 启动注册反馈

- `OnShown` 现在发布快捷入口热键注册的 warning/error；冲突不会静默显示为普通启动成功，后台启动也不会被强制激活。
- 最新候选构建、自动测试、三组件 self-test、Release Identity/Artifact gate 和首页 Computer Use 检查均通过。
- Gate 结论保持 **PASS WITH MANUAL HARDWARE CHECKS**；真实遥控器、语音工具、ChatGPT UIA、DPI/主题和安装升级卸载仍为 `NOT_RUN`。

## 2026-09-07 迟到结果、快捷键格式与 UIA 收口

- 快捷入口保存层拒绝无法注册的快捷键格式，并与 `RegisterHotKey` 使用同一解析规则；录音键、Raw Input、低级 Hook 和设备过滤未改动。
- Notes AI 结果在显示、复制、保存前执行来源存在性/删除状态/revision gate；删除或修改来源不会再让迟到结果留在可复制预览中。
- Deck 的列表、标题、正文已添加显式可访问名称；Computer Use 在正式候选上实际看到这些名称。
- `BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、三组件 self-test、Release Identity/Artifact gate 和 `git diff --check` 均通过；Setup `9D7D18DFCF71B4B3CE9FF6CB96D0ECD41B3F72CB7932B5F94CAD4658F4A81CED`，ZIP `F8AA154595295348B9E8F9C73B93A600FC0B890BB29201F46A51D2E835CD5F99`。
- Reviewer 结果：BLOCKER 0、HIGH 0、MEDIUM 0；原 LOW 可访问性 finding 已修复。Gate 仍为 **PASS WITH MANUAL HARDWARE CHECKS**，不宣称真机、真实 provider、ChatGPT UIA、完整 DPI/主题或安装生命周期通过。

## 2026-09-07 历史快捷键配置校验收口

### 阶段范围

- 修复旧 `quick-entry` 配置非法快捷键被加载后静默跳过的问题。
- 保持 Notes Deck、APP 快捷入口和原稳定录音/输入契约不变。

### 构建与测试

| 检查 | 结果 |
|---|---|
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS；包含先失败后通过的存储快捷键回归 |
| `BUILD_RELEASE.ps1` | PASS；Host/Bridge、安装器、ZIP、SHA-256 同批生成 |
| `BUILD_VIBE_MIC.cmd` | PASS |
| `npm test` | PASS |
| `Test-ReleaseIdentity.ps1` | PASS |
| `Test-ReleaseArtifacts.ps1` | PASS |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| `VibeMicAtvvCapture.exe --self-test` | PASS，exit 0 |
| `git diff --check` | PASS；仅保留工作区既有换行提示 |

### 冻结与输入回归

- Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- Capture 二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 未修改 Capture 源码/二进制、调用参数、录音状态机、Raw Input、低级 Hook、设备过滤或录音键映射。
- 无效历史快捷键现在在 Store Load 层返回 `QUICK-ENTRY-HOTKEY-INVALID`；有效备份仍可恢复，数据不会静默被当作可运行配置。

### 原生 UI

- 实际启动候选：`release/Vibe-Flow-Windows-x64/VibeFlow.exe`。
- 已检查：首页、快捷入口、便签、独立便签 Deck，以及 Deck 关闭后的主窗口恢复。
- Deck UIA 实际暴露“便签列表”“便签标题”“便签分类”“便签正文”；未执行固定/置顶切换，避免改变用户偏好。
- ChatGPT 入口仍诚实显示“已打开 · 输入框待确认”，没有把前台进程等同于输入框聚焦。

### 审查与边界

- 稳定性 reviewer：BLOCKER 0、HIGH 0、MEDIUM 0；UX reviewer 未发现新的确定性阻塞问题。
- 可选 LOW：快捷入口页可补充“从备份恢复”提醒；当前不影响数据安全或候选门禁。
- 真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、完整 DPI/主题、多显示器、安装升级卸载和签名仍为 `NOT_RUN`。

### 结论

`PASS WITH MANUAL HARDWARE CHECKS`。候选未签名、未 Push、未发布。

## 2026-09-07 最终候选 Gate

### 范围

Notes Deck 当前候选、APP 快捷入口恢复提示、热键状态反馈，以及正式候选启动与同批产物一致性。

### 构建与测试

| 检查 | 结果 |
|---|---|
| `BUILD_RELEASE.ps1` | PASS；第一次因旧子进程锁文件而中止，结束子进程后重跑成功 |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS |
| `Test-ReleaseIdentity.ps1` | PASS |
| `Test-ReleaseArtifacts.ps1` | PASS；根 Host 与包内 Host SHA-256 一致 |
| Host/Bridge/Capture self-test | PASS |
| `git diff --check` | PASS（仅既有换行提示） |

### 原生 UI

- Computer Use 实际启动正式候选并检查首页；标题为“言灵 · Vibe Flow Remote · V2.0.0”。
- 首页显示“已准备好”，并显示“先确认 APP 输入目标，再按住录音键说话，松开结束”；没有 `--ui-smoke` 恢复警告。
- 未对真实 RC003、ChatGPT 桌面输入框或第三方语音工具执行未经授权的自动化操作。

### 冻结与限制

- Capture 源码/二进制 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- Setup：`4B32523190AF964494CA1F075903F54430E5B87CF563E4AED1001F0A4A119EC4`；ZIP：`4305E1D39ACD9514C2D9318D5ED1C8FF5112C76248BD9DBF94D986F7C899BB70`。
- 真实 RC003 按键边沿/录音、VB-CABLE、微信或其他语音工具端到端、ChatGPT UIA、真实 provider、完整 DPI/主题、多显示器、睡眠/蓝牙重连、安装升级卸载和代码签名仍为 `NOT_RUN`。

### 结论

**PASS WITH MANUAL HARDWARE CHECKS**。不签名、不 Push、不发布。

## 2026-09-07 最终候选低风险修复复核

- 修复 `QuickEntriesPage` 在恢复提示渲染前读取旧加载结果的时序问题；现在先调用配置加载，再依据最新结果显示恢复提示。
- 只读稳定性 reviewer 复核：BLOCKER 0、HIGH 0、MEDIUM 0；该 LOW 已关闭。未发现录音键接入 Notes/Smart Focus/Deck/普通映射，也未发现转写剪贴板回填或录音后自动 Enter。
- `BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture self-test、`git diff --check`：全部 PASS。
- Computer Use 实际启动正式候选首页；显示“已准备好”，无恢复夹具警告。
- Setup：`B34234DA4B54E80319653DA15EF2B778BDD10AA1C766DF534E35DA1CFBA49292`；ZIP：`CE266B40EF04E9422ED265A0CE6E5FF7519FCF4E4B0292BD71428A2986D3A1A2`。

### Gate 结论

**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、VB-CABLE、第三方语音工具、ChatGPT UIA、真实 provider、DPI/主题和安装生命周期仍为 `NOT_RUN`；候选未签名、未 Push、未发布。

## 2026-09-07 批量便签汇总阶段复核

### 范围

- Notes 管理页多选便签与“汇总已选”入口。
- 汇总窗口显示明确来源范围，结果仅预览/复制/保存为新便签。
- 来源 ID、来源 revision、Provider、prompt version、status 元数据持久化。
- 来源修改、删除或迟到结果安全阻断；没有配置 Provider 时不发送请求。

### 构建与测试

| 检查 | 结果 |
|---|---|
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS；含批量来源去重、至少两条、超长输入、revision/删除门禁和结果元数据 round-trip |
| `BUILD_RELEASE.ps1` | PASS；Host/Bridge、安装器、ZIP、SHA-256 同批生成 |
| `Test-ReleaseIdentity.ps1` | PASS |
| `Test-ReleaseArtifacts.ps1` | PASS |
| Host/Bridge/Capture `--self-test` | PASS；退出码 0 |
| `git diff --check` | PASS；仅有既有换行提示 |

### 原生 UI

- 正式候选 `release/Vibe-Flow-Windows-x64/VibeFlow.exe` 已启动。
- Notes 页实际选中两条便签后，按钮显示“汇总已选便签（已选 2 条）”。
- 汇总窗口实际显示“汇总已选便签 → 发送范围说明 → 来源列表 → 结果预览”；无 Provider 时显示“尚未配置模型……”，未执行联网请求。
- 无活动请求时取消可关闭窗口；运行中的取消会停止当前请求、保留窗口显示取消状态且不自动重试。Provider 取消后的迟到回调仍需真实供应商验收。

### 冻结与发布边界

- Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- Capture 二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- Setup SHA-256：`910A36075F3777F3A8696D2566212FEBD392ECCBEBA839C5D797A200550FDC32`。
- ZIP SHA-256：`127C17A136202E72D7FCE2AA51DE9E72AD3E309CFBA1C797E101E070BF18CB42`。
- reviewer：UX/spec reviewer 发现取消竞态 MEDIUM，已通过独立请求代次/取消意图门禁修复；稳定性 `BLOCKER 0 / HIGH 0`，旧 QuickEntry LOW 为已存在的提示时序检查且当前调用顺序已先加载配置。
- 真实 RC003、VB-CABLE、第三方语音工具、ChatGPT UIA、真实 Provider、DPI/主题、安装升级卸载和签名仍为 `NOT_RUN`。

### 结论

**PASS WITH MANUAL HARDWARE CHECKS**。候选未签名、未 Push、未发布。

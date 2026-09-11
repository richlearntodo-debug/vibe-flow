# V2.0 Notes Deck 历史归档

> 本文件只保留 2026-09-06 至 2026-09-07 的 Notes Deck 实验记录，已经从当前产品范围移除。它不是当前 Host 的功能说明、自动化测试报告或发布依据；当前范围以 `docs/V2_0_PROGRESS.md`、`docs/V2_0_USER_GUIDE_ZH.md` 和 `docs/V2_0_RELEASE_NOTES_ZH.md` 为准。

## 当前阶段

阶段 6：集成回归与候选构建。Notes Deck 代码、AI 文本整理边界、确定性测试、文档级 revision CAS、JSON 备份恢复、正式安装包和原生 UI 检查已完成；真实硬件、真实供应商、ChatGPT 输入框、完整 DPI/主题和升级安装仍未验证，不声明正式稳定版。

## 本阶段目标

- 确认 Notes Deck 替代旧 V2 Project 业务范围。
- 记录真实代码路径、稳定录音契约和未提交工作区。
- 不改 Capture、录音参数或用户旧配置；Bridge/Hook/设备过滤保留当前工作区基线，Notes 功能不再扩大其录音核心范围，真实设备行为继续标记未验证。

## 已完成

- 读取并核对仓库根 `AGENTS.md`、`vibeflow-release-gate`、`vibeflow-auditor.toml` 和 Notes Deck 附件。
- 核对分支、commit、dirty worktree、构建脚本和当前运行入口。
- 核对 Capture 源码/二进制 SHA-256、录音参数、按键时序和自动化基线结果。
- 新增 `BASELINE.md`、`PLAN.md`、`UI_SPEC.md`、`APP_COMPATIBILITY.md`、`QA_REPORT.md` 的文档框架。
- 实现本地便签主页面、独立便签 Deck、固定/置顶/关闭偏好、revision 冲突保护、未知字段保留和备份恢复。
- 实现用户自带 AI 配置与 DPAPI Key 存储，以及整理、分类、汇总、翻译操作；没有 API Key 时便签仍可离线使用。
- 实现 APP 快捷入口模型、编辑器和 RegisterHotKey；ChatGPT 状态按真实进程/输入目标证据区分“未运行”“已打开，输入框待确认”“输入目标已验证”。
- 首次设置旧“创建项目现场”入口已替换为“添加 APP 快捷入口”；最终 CTA 为“打开便签”，不会自动执行旧 Project Space。
- 普通键盘 F5 不会进入录音状态机；Capture、录音参数和冻结边界未修改，Bridge/Hook 的当前工作区输入隔离状态按现有基线保留并需真机验证。
- 已创建两个只读 reviewer 配置、三个项目 Skill、AI 提示词、`DECISIONS.md`、`COMPATIBILITY.md`、`SOURCES.md`、`qa/ACCEPTANCE_TESTS.md` 和可审阅的 `templates/AGENTS.merge.md`。
- 已确认录音不会自动触发 Smart Focus：正确流程是先锁定并验证输入目标，再按住原录音键；当前 ChatGPT 只有“已打开，输入框待确认”证据。
- AI 外发请求已禁止自动重定向，并将 3xx 分类为 `AI-REDIRECT-BLOCKED`；没有自动重试或切换供应商。
- 根据回归审查修复了快捷入口热键注册的原子回滚、按键设置等待 Bridge ACK 后再显示成功、AI 设置空 Key 保留已有 DPAPI Key，以及 AI 取消 continuation 的 `Task.Result` 竞态。
- 修复便签根级 revision 竞争：`NotesStore.TrySave` 写入前重新读取当前文档并执行 CAS；主窗与 Deck 的新建、删除、恢复、编辑和 AI 回写不会再用旧整文档快照静默覆盖新内容。新增 `TestDocumentRevisionConflict`，旧快照返回 `NOTES-REVISION-CONFLICT`。
- 修复 JSON 恢复在主文件损坏但 `.bak` 有效时覆盖有效备份的问题；恢复前严格要求根对象和 `notes` 数组，拒绝驱动器相对路径、遍历路径、活动 store 路径和备份路径。
- 修复 Deck flush 失败时丢失窗口引用的问题：`CloseWithOwner` 返回可重试结果，宿主只有成功 flush 后才注销快捷入口/取消运行时；失败会取消退出并保留 Deck。
- AI 阶段补齐 `product_ai_prompts` 运行时加载与内置安全回退，补充超时、网络、HTML、3xx 重定向、`Retry-After` 和真实取消分类；AI provider root/profile 未知字段保存时保留。
- 候选脚本会把 `product_ai_prompts` 复制进硬件候选包；最新候选 ZIP 已重建并核对 Host/Capture 哈希。
- 修复新建便签取消语义：主编辑器取消或关闭空白新便签会按 revision 物理丢弃草稿，Deck 对同一草稿执行相同 CAS；关闭失败保留窗口和数据。
- 修复录音期间自动保存：计时器在录音中只延期并继续排队，录音结束后的下一次 tick 才保存，不触碰录音线程；新增自动保存策略和 Deck 自动保存后清空再丢弃回归测试。
- 补齐 F2 的本地系统提示词设置：设置页现在可以编辑、原子保存和恢复默认提示；配置独立于 API Key、便签和录音参数，超过长度或包含控制字符时拒绝写入。
- 为 `AiPromptStore` 增加有效备份恢复和未知字段保留测试；主文件损坏时保存/重置不会覆盖仍然有效的 `.bak`。
- 根据 reviewer 复核补齐快捷入口运行时绑定快照、AI secrets 备份保留/单项解密回退和 AI 测试 generation 门禁；`QuickEntryTests` 与 `NotesDeckAiTests` 均覆盖并通过。

## 修改文件

本轮涉及：`scripts/features/AiModels.cs`、`scripts/features/AiTextService.cs`、`scripts/ui/AiSettingsPage.cs`、`scripts/VibeMic.cs`、`scripts/tests/NotesDeckAiTests.cs`、`docs/v2-notes/PROGRESS.md`，以及前序 Notes Deck 文件、构建脚本和文档资产。

## 构建与自动测试

本轮另行完成 `BUILD_RELEASE.ps1` 候选构建（退出码 0），正式候选已包含本地提示词设置代码；其余基线命令继续沿用下列记录。

本轮重跑：`BUILD_VIBE_MIC.cmd`、`BUILD_HARDWARE_CANDIDATE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`.\VibeMic.exe --self-test`、`.\VoxDeckInputBridge.exe --self-test`、`.\VibeMicAtvvCapture.exe --self-test` 和 `git diff --check` 均通过。Host/Bridge 构建需先停止运行实例以释放输出锁，未修改冻结 Capture 源码或二进制。

最新硬件候选：`artifacts/Vibe-Flow-v2.0.0-Hardware-Candidate-20260906-135228.zip`；ZIP SHA-256：`700B2A42A3567A922F3940A7EE061D9656B1724E14C69794B542CD7C14918404`。候选目录包含 5 个 `product_ai_prompts` 文件；包内 Host SHA-256 与根 `VibeMic.exe` 一致；包内 Capture SHA-256 为冻结值。

正式构建闭环还重新执行了 `BUILD_RELEASE.ps1`（退出码 0），包含 Bridge/Host 构建、Inno Setup 6.7.3 安装器编译和 `Test-ReleaseArtifacts.ps1`；正式候选与本次便签修复保持一致。

正式候选已重建并通过 artifact gate；安装器与 ZIP 的 SHA-256 以同批生成的 `release/SHA256SUMS.txt` 为准，Host/Bridge 以候选目录与根目录同批文件哈希核对为准。包内包含 5 个 prompt 文件；Capture 仍为冻结哈希。安装器未签名，未执行安装/升级/卸载。

## Computer Use

本轮实际启动的候选构建已通过首页、便签页和独立 Deck 检查；便签工具栏的“备份 JSON/恢复 JSON/打开便签 Deck”无溢出，Deck 输入后约 500ms 自动保存，关闭路径可保留失败状态。首页显示“已准备好”但“语音目标：尚未设置”，并明确提示先确认 APP 输入目标再按住录音键；没有真实设备/目标时未显示输入成功。截图证据：`qa/stage3-notes.png`。

Computer Use 已启动普通构建并观察首页、便签页和独立 Deck。首页显示“正在连接”和“语音目标：尚未设置”；没有真实设备时未显示成功。便签 Deck 失焦不自动关闭，主窗口可继续导航。

正式包 `release/Vibe-Flow-Windows-x64/VibeFlow.exe` 已等待启动完成后由主 Agent 和只读 auditor 检查；窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”，首页状态和便签入口正常。启动初期约需数秒等待 Bridge/Capture 状态，不应把短暂无窗口误判为崩溃。最后一次候选运行确认 Host/Bridge 进程存在，`minimizeToTray=true` 时主窗口按设计隐藏到托盘；Computer Use 未获该应用控制权限，原生 UI 点击/导航仍未验证。

## 未验证

真实 RC003、VB-CABLE、微信输入法、ChatGPT UIA 聚焦、双显示器/DPI、安装升级卸载、真实 API 供应商调用。

## 状态矩阵

| 维度 | 状态 | 证据/边界 |
|---|---|---|
| BUILD_TEST | PASS | Host、Bridge、Capture self-test、npm/V2 focused suite；Host 输出为本轮构建 |
| GUI | PASS（基础路径） | 首页、便签、Deck、快捷入口、自检已实际检查；完整 DPI/主题矩阵未跑 |
| REAL_PROVIDER | NOT_RUN | 未使用用户 Key，未发起付费/联网请求 |
| REAL_APP_FOCUS | NOT_RUN / 手动待确认 | ChatGPT 可启动，但未发现可验证 UIA Edit |
| HARDWARE | NOT_RUN | 无 RC003、VB-CABLE、真实语音工具 |
| DATA_MIGRATION | NOT_RUN | 确定性存储测试通过，安装升级/回退未跑 |

## Auditor / release gate

- 两位只读 reviewer 和正确路径 `.codex/agents/vibeflow-auditor.toml` 已复核当前 diff；主 Agent 已处理文档级 CAS 竞争问题。该 auditor 配置声明只读，现有 reviewer 配置也保持只读。
- 本轮新增的根级便签 CAS HIGH 已修复；Notes 专用 schema 迁移显式测试、自动保存、JSON 备份 UI、Focus 前台切走取消、真实供应商取消/Retry-After 仍记录为未完成或未验证，不通过录音核心绕过。
- 两位只读 reviewer 已回传最新 diff 结果；当前范围 BLOCKER 0、HIGH 0，主 Agent 已处理文档级 CAS 竞争问题。
- release gate 预期为 `PASS WITH MANUAL HARDWARE CHECKS`，因为本环境没有 RC003、VB-CABLE、微信输入法和可验证的 ChatGPT UIA 输入框。
- 回归 reviewer 初审发现的两个 HIGH（热键丢旧绑定、Bridge ACK 虚报成功）已修复并重跑构建/测试；其余存储自动保存、JSON 备份 UI、Focus 前台切走取消、真实供应商取消/Retry-After 记录为未完成或未验证，不通过录音核心绕过。
- 最终 auditor 复核指出的具体 HIGH 已全部处理：快捷键下拉/恢复路径统一等待 Bridge ACK；热键原子回滚保存旧 modifiers/key；AI secrets 主文件损坏会回退 `.bak`，并有 `TestCredentialBackupRecovery`。
- UX reviewer：BLOCKER 0；实际首页/便签/快捷入口/自检已检查，完整 DPI/主题矩阵仍 NOT_RUN。
- 最终 `vibeflow_auditor` 快速复核：BLOCKER 0、HIGH 0；仍不声明正式稳定版，因为真实硬件、供应商、ChatGPT UIA、DPI/主题和安装迁移未验证。
- 最终 auditor 回执中的 HIGH 已逐项修复；重新执行 Host 构建、`npm test`、V2 focused suite、Host/Bridge/Capture self-test 和 diff check 均通过。
- 本轮回归 reviewer 复核新建草稿 CAS、录音期间自动保存和正式 artifact：BLOCKER/HIGH 0；新增 Deck 自动保存后清空再关闭测试通过，原 MEDIUM 行为缺口已修复。剩余 MEDIUM 仅为未具备真实 RC003/供应商/ChatGPT UIA/DPI 设备的人工验证范围。
- 正式 release gate：`PASS WITH MANUAL HARDWARE CHECKS`；未签名、未发布、未 Push。

## 最终审计修复

- auditor 发现 Notes Deck 可能在录音开始后继续可见并抢焦点；已按 TDD 增加录音中拒绝打开测试。
- `NotesDeckForm` 现在使用非激活显示，录音时返回失败并隐藏；Host 的录音优先取消路径会调用 `HideNotesDeckForRecording`。
- 修复后 `npm test`、V2 focused suite、Host/Bridge/Capture self-test 和实际 UI Smoke 均通过；Capture 哈希不变。

## 下一阶段

真实 Windows 10/11、RC003、VB-CABLE、语音工具、ChatGPT 目标、DPI/主题和安装升级的人工验收仍是下一步；当前只生成本地未签名候选，不 Push、不创建 GitHub Release。

## 2026-09-07 最新收口（源码与候选已重建）

本轮基于最新工作区重新完成只读 reviewer 复核。两位 reviewer 均返回 BLOCKER 0、HIGH 0；之前关于数据恢复、Deck 固定、搜索预览、错误卡叠加和录音竞态的旧 finding 已逐项修复并加入 focused tests。

本轮修复：

- `AiProviderStore` 和 `AiPromptStore` 在从有效 `.bak` 恢复后首次保存不会覆盖该备份；缺少关键 provider/prompt 字段的 JSON 会被视为损坏并回退备份。
- AI 文本整理请求期间禁用操作选择器，并以请求开始时捕获的 operation 保存结果，避免分类结果被误写成整理结果。
- Smart Focus 服务统一监测目标执行期间的前台进程变化；用户切走前台时取消后续聚焦步骤。
- 便签 Deck 的“固定”实际锁定窗口位置和大小；置顶仍是独立状态。显示器工作区或 DPI 变化时重新约束 Deck。
- 便签列表设置最小左右栏宽度；列表项可通过 Tab 聚焦并用 Enter/Space 选中；搜索框显示中文提示，搜索结果与右侧正文保持一致。
- 录音开始优先级在 Deck/编辑器真正显示前再次检查；便签读取失败复用两栏错误状态，不再叠加固定卡片。

最新证据：`BUILD_RELEASE.ps1`、`BUILD_VIBE_MIC.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture self-test、`Test-ReleaseArtifacts.ps1` 和 `git diff --check` 均通过。候选安装器与 ZIP 已重新生成，`release/SHA256SUMS.txt` 为同批哈希清单；Capture 源码/二进制哈希仍为冻结值 `736017A0...74137E2` / `B62DE035...0582E683`。

Computer Use 实际检查了最新根目录 `VibeMic.exe` 的首页、两栏便签页、可见搜索提示和独立 Deck。Deck 固定/置顶按钮可操作；非置顶截图中的像素图在置顶后消失，源码无对应资源，记录为外部桌面/浮层捕获现象，不作为产品渲染。最新源码没有把 9 月 6 日旧截图冒充本轮证据。

本阶段结论：`PASS WITH MANUAL HARDWARE CHECKS`。仍为 `NOT_RUN`：真实 RC003 按键边沿与 100 次录音、10/30/约 60 秒真实音频、VB-CABLE/微信及其他语音工具端到端、ChatGPT UI Automation 输入框、真实 AI provider、Chrome/Edge、Windows 10/11 安装升级卸载、100/125/150/200% DPI 完整矩阵、深浅主题、多显示器拔插、睡眠唤醒和蓝牙重连。候选未签名、未 Push、未发布。

## 2026-09-07 最终候选入口与 schema 收口

- 修复三个 Notes Deck 新配置 Store 对缺少或非整数 `schemaVersion` 的宽松回退；`schemaVersion=0` 的既有 Notes 迁移仍保留，未版本化文件现在按损坏处理并尝试有效 `.bak`。
- 修复 `package.json` 的 `npm start`，统一转发到 `START_VIBE_FLOW.cmd`；根启动器优先使用 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`，再回退开发目录 `VibeMic.exe`。
- README 首段改为 Notes Deck 当前定位，明确便签、APP 快捷入口和用户主动整理，不再把旧网页项目闭环写成 V2 主流程。
- 新增 `TestMissingSchemaVersionsRecoverFromBackup`，并在 `scripts/validate.js` 增加启动入口一致性门禁。
- 本轮 `npm test`、`Test-V2FeatureSuite.ps1`、`BUILD_VIBE_MIC.cmd`、`BUILD_RELEASE.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture self-test、`git diff --check` 均通过。
- 实际 `npm start` 启动 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`；Computer Use 检查首页、便签页和 Deck，置顶切换后恢复原偏好。当前结果仍为 `PASS WITH MANUAL HARDWARE CHECKS`。

## 2026-09-07 文案定位收口（当前阶段）

- 调查路径：`scripts/VibeMic.cs` 首页/托盘入口、`scripts/ui/ContextDeckForm.cs` 遥控器状态窗口、`scripts/ui/ProjectsPage.cs` 快捷入口页、`scripts/features/ProjectSpaceRunner.cs` 兼容执行回执、`scripts/validate.js` 静态门禁。
- 修复：将可达入口从“查看/打开 Context Deck”改为“查看/打开遥控器状态”；窗口标题、可访问名称和眉标题改为“遥控器状态”；上下文行改为“快捷入口”。
- 修复：快捷入口页和执行回执统一使用“APP 快捷入口/打开快捷入口”，不再把兼容的 `ProjectSpace` 数据呈现为 V2 主产品“项目现场”；底层类型、存储 schema 和兼容执行器保留。
- 稳定边界：未修改 Capture 源码/二进制、录音参数、Raw Input、键盘 Hook、设备过滤、录音状态机或旧配置。
- 构建与测试：`BUILD_VIBE_MIC.cmd`、`BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture self-test、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、`git diff --check` 均通过。
- Computer Use：实际启动正式候选；首页显示“查看遥控器状态”，快捷入口页显示 APP 状态和“打开或激活 APP”，遥控器状态窗口显示“遥控器状态/快捷入口”，未再出现旧主线文案。
- 仍需人工验证：真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UI Automation 输入框、完整 DPI/主题/安装升级卸载矩阵；本阶段结论为 `PASS WITH MANUAL HARDWARE CHECKS`。

## 2026-09-07 快捷入口冲突反馈修复

- 只读稳定性 reviewer 发现快捷入口热键与其他程序冲突时，注册回滚虽执行但保存提示仍可能显示成功。
- 新增 `QuickEntryHotkeyOutcome`：冲突且旧绑定恢复成功显示 warning、冲突且恢复失败显示 error，均提供原因、影响和修复入口；保存入口时使用该真实结果，不再虚报“已生效”。
- `QuickEntryTests` 新增冲突 warning、回滚失败 error 和 success 三态断言；没有扩大普通键盘拦截，也没有进入录音键路径。
- `BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、Host/Bridge/Capture self-test、release identity/artifact gate 均通过；当前候选仍为 `PASS WITH MANUAL HARDWARE CHECKS`。

## 2026-09-07 启动注册反馈

- 启动路径现在保留并发布 `RegisterQuickEntryHotkeys()` 的 warning/error 结果；冲突不会再静默落入“尚未验证输入框”。正常启动不弹成功提示，后台启动不抢焦点。
- 最新候选已通过完整构建、自动测试、三组件 self-test、Release Identity/Artifact gate，并由 Computer Use 实际启动检查首页。
- 当前结论仍为 **PASS WITH MANUAL HARDWARE CHECKS**；真实遥控器、语音工具、ChatGPT UIA、DPI/主题和安装生命周期保持 `NOT_RUN`。

## 2026-09-07 迟到 AI 结果与可访问性收口

- `ProjectSpaceValidation.TryParseQuickEntryShortcut` 与注册路径共用解析规则；新建/编辑 APP 快捷入口会拒绝 `foo`、`Ctrl+Unknown`、空 token 等非法格式，空快捷键仍合法。`QuickEntryTests` 覆盖合法、非法和存储拒绝。
- 新增 `NotesAiResultPolicy`：AI 回调显示、复制和保存前都重新验证来源便签仍存在、未删除且 revision 未变化；迟到结果会清空预览并禁用复制/保存，返回 `NOTES-AI-STALE-NOTE`。保存自己的 AI 结果后同步本地来源 revision，复制仍可继续使用。
- Deck 的列表、标题、正文控件增加显式 UIA 名称；最新 Computer Use accessibility tree 已实际看到“便签列表 / 便签标题 / 便签正文”。
- 证据：focused suite、`BUILD_RELEASE.ps1`、`npm test`、Host/Bridge/Capture self-test、Release Identity/Artifact gate、`git diff --check` 均通过；最新候选 Setup `9D7D18DFCF71B4B3CE9FF6CB96D0ECD41B3F72CB7932B5F94CAD4658F4A81CED`，ZIP `F8AA154595295348B9E8F9C73B93A600FC0B890BB29201F46A51D2E835CD5F99`。
- Computer Use 实际检查最新候选首页、便签页和独立 Deck；关闭 Deck 后主窗口恢复。截图中的像素角色仍是外部桌面浮层，源码无对应资源，不作为产品渲染证据。
- 当前结论：**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、完整 DPI/主题、多显示器、睡眠/蓝牙重连和安装升级卸载仍为 `NOT_RUN`。

## 2026-09-07 历史快捷键配置校验收口

- 稳定性 reviewer 发现：旧 `project-spaces.json` 中的 `quick-entry` 非法快捷键此前只校验长度，启动注册阶段会静默跳过，用户看不到明确的配置原因。
- 先加入 `QuickEntryTests.TestStoredQuickEntryShortcutValidation`，确认旧行为会错误返回可用文档；随后在 `ProjectSpace.TryValidateStoredStructure` 复用 `ProjectSpaceValidation.TryParseQuickEntryShortcut`，并让 `ProjectSpaceStore.TryReadDocument/LoadCore` 传出具体 `QUICK-ENTRY-HOTKEY-INVALID`，无效配置不会再进入运行时。
- 空快捷键仍合法，旧 `project` 数据、录音键、Raw Input、键盘 Hook、设备过滤和 Capture 均未修改。合法 `.bak` 仍可恢复；主文件与备份都不可读时保持 `PROJECT-STORE-CORRUPT`。
- 红绿证据：修复前 focused suite 按预期失败；修复后 `scripts/tests/Test-V2FeatureSuite.ps1` 通过，包含 QuickEntry、Notes、Focus、Capture Ask、Browser 和 HUD 测试。
- 正式候选重新构建并通过 `BUILD_RELEASE.ps1`、`BUILD_VIBE_MIC.cmd`、`npm test`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、三组件 self-test 和 `git diff --check`。本批 `release/SHA256SUMS.txt`：Setup `A9826A1789FD7D1D4FCE7689674156B8B2C065A8BAF9FF18AC87876D4E2DE07E`，ZIP `45D2CBF11DE59AA5D32453A970C30F129BE476871F6ED8764253358E26280D65`。
- Computer Use 实际启动本批 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`，检查首页、快捷入口页、便签页和独立 Deck；Deck 的“便签列表/便签标题/便签分类/便签正文”可访问名称可见，关闭 Deck 后主窗口恢复。首页仍诚实显示“先确认 APP 输入目标，再按住录音键说话”，ChatGPT 入口仍显示“输入框待确认”。
- 只读稳定性 reviewer：BLOCKER/HIGH/MEDIUM 0；UX reviewer 未发现新的确定性阻塞问题。快捷入口从备份恢复的提醒仍是可选 LOW 改进，不影响当前候选安全性。
- 当前阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA 输入框、真实 provider、完整 DPI/主题/多显示器、安装升级卸载和代码签名仍为 `NOT_RUN`；候选未签名、未 Push、未发布。

## 2026-09-07 正式候选启动验证

- 清理上一轮 `--ui-smoke` 专用临时夹具后，启动正式候选 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`；首页实际显示“已准备好”，并保留“先确认 APP 输入目标，再按住录音键说话，松开结束”的真实边界文案。
- `BUILD_RELEASE.ps1` 重新生成同批安装器/ZIP；`npm test`、V2 focused suite、Release Identity、Release Artifacts 和三组件 self-test 均通过。
- 本批 Setup SHA-256：`4B32523190AF964494CA1F075903F54430E5B87CF563E4AED1001F0A4A119EC4`；ZIP SHA-256：`4305E1D39ACD9514C2D9318D5ED1C8FF5112C76248BD9DBF94D986F7C899BB70`。
- 根 Host 与包内 Host 哈希一致；Capture 冻结源码/二进制哈希未变。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；硬件、真实语音工具、ChatGPT 输入框 UIA、真实 provider、DPI/主题和安装生命周期仍未验证。

## 2026-09-07 最终候选提示时序修复

- 快捷入口页现在先完成主配置/备份加载，再显示恢复提示；修复了极窄时序下恢复入口可见但提示遗漏的问题。
- 自动证据：`BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、Release Identity/Artifact gate、三组件 self-test 和 `git diff --check` 均通过。
- 本批 Setup SHA-256：`B34234DA4B54E80319653DA15EF2B778BDD10AA1C766DF534E35DA1CFBA49292`；ZIP SHA-256：`CE266B40EF04E9422ED265A0CE6E5FF7519FCF4E4B0292BD71428A2986D3A1A2`。
- 正式候选首页已由 Computer Use 实际检查，显示“已准备好”，没有恢复测试警告；Capture 冻结哈希未变。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；reviewer 当前 BLOCKER/HIGH/MEDIUM 均为 0，硬件与真实第三方目标仍未验证。

## 2026-09-07 批量汇总取消与迟到结果门禁收口

### 用户目标与范围

- 在便签管理页多选至少两条便签后，用户主动发起一次汇总；明确显示本次发送范围，AI 结果只作为预览、复制或新建便签，不覆盖来源便签。
- 每次汇总请求保存 `sourceNoteIds`、`sourceRevisions`、`providerId`、`promptVersion` 和 `status`；来源便签被编辑、删除或 revision 变化时，迟到结果不得显示、复制或保存。
- 取消或关闭汇总窗口后，迟到的 Provider 回调不得更新 UI、启用复制/保存或写入新便签。

### 本轮调查与修改

- 调查真实路径：Notes 管理页多选 → 汇总窗口 → `AiTextService` 请求 → Notes AI 结果策略 → NotesStore CAS 保存；取消路径同时覆盖窗口取消、窗口关闭和旧 request generation。
- 修复汇总窗口 WinForms Dock 顺序，实际视觉顺序为：标题、范围说明、来源列表、结果预览。
- 为每次请求创建独立 `CancellationTokenSource` 与 request generation；取消、关闭和新请求都会使旧回调失效。
- 新增 `TestNotesBatchCancelGate`，覆盖取消后迟到成功、旧代次结果和当前请求结果三种门禁情况。

### 构建、测试与证据

- `cmd /c BUILD_VIBE_MIC.cmd`：PASS。
- `npm test`：PASS。
- `scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- `BUILD_RELEASE.ps1`：PASS；候选安装器与 ZIP 已重新生成。
- `scripts/tests/Test-ReleaseIdentity.ps1`、`scripts/tests/Test-ReleaseArtifacts.ps1`：PASS。
- `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test`：PASS。
- `git diff --check`：PASS。
- 最终候选 Setup SHA-256：`910A36075F3777F3A8696D2566212FEBD392ECCBEBA839C5D797A200550FDC32`。
- 最终候选 ZIP SHA-256：`127C17A136202E72D7FCE2AA51DE9E72AD3E309CFBA1C797E101E070BF18CB42`。
- 冻结 Capture 源码/二进制 SHA-256 保持：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。

### 原生 UI 与只读审查

- Computer Use 实际启动 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`，确认首页可见、便签页可进入、汇总窗口顺序正确；首页仍明确显示“先确认 APP 输入目标，再按住录音键说话，松开结束”。
- UX/spec reviewer：`BLOCKER 0 / HIGH 0 / MEDIUM 0 / LOW 0`。
- 稳定性 reviewer：`BLOCKER 0 / HIGH 0`；确认 Capture、Raw Input、Hook、设备过滤、录音键映射和旧配置边界未被修改。
- Release gate：`PASS WITH MANUAL HARDWARE CHECKS`。自动化证据不替代真实硬件或第三方应用验收。

### 未验证与下一步

- 仍为 `NOT_RUN`：真实 RC003 按键边沿/100 次录音/10 秒、30 秒及接近设备边界音频；VB-CABLE；微信输入法及其他语音工具端到端；ChatGPT UI Automation 输入框；真实 AI Provider；Chrome/Edge；Windows 10/11 安装升级卸载；100%/125%/150%/200% DPI；深浅主题、多显示器、睡眠唤醒和蓝牙重连；代码签名。
- 候选未签名、未 Push、未发布。下一步是具备真实 Windows、RC003、VB-CABLE、语音工具和目标 APP 后，按 `qa/ACCEPTANCE_TESTS.md` 逐项记录人工结果；在此之前不把当前候选称为正式稳定版。

## 2026-09-07 最终 release gate 文案复核

- 规格 reviewer 发现 `docs/v2-notes/RELEASE_GATE.md` 将运行中取消写成“实际关闭窗口”；已修正为：无活动请求时可关闭窗口，运行中取消保留窗口并显示取消状态，不自动重试；Provider 取消后的迟到回调仍为真实供应商待验收。
- 修正后重新运行 `npm test`、`scripts/tests/Test-V2FeatureSuite.ps1` 和 `git diff --check`，全部通过。
- 只读 reviewer 最终结果：规格 reviewer `BLOCKER/HIGH/MEDIUM 0`；稳定性 reviewer 的同一代码范围前一轮回执为 `BLOCKER/HIGH/MEDIUM 0`，本轮仅文档变更未重复产生新回执。当前不改变候选结论，仍为 `PASS WITH MANUAL HARDWARE CHECKS`。
- 同步更正 `docs/v2-notes/BASELINE.md` 的附件文件名为实际执行的 `VibeFlow_V2_0_Notes_Deck_Execution_Prompt_ZH.md`；随后 `npm test` 与 `git diff --check` 仍通过。
- 为避免旧 V2 文档继续造成产品定位误解，在早期 UI 规范、实施计划、自动化报告、基线/迁移、安装器、限制和真机清单顶部增加了“已被 Notes Deck 规格取代”的范围提示；历史证据正文保留，不改变运行时。

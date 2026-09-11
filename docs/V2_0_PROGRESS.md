# Vibe Flow V2.0 开发进度

> **当前范围（2026-09-08）**：便签本、Notes Deck、AI 便签整理及其用户入口已从当前 Host 产品面移除。下方 2026-09-07 及更早的 Notes Deck、项目现场和便签页记录只用于历史审计，不代表当前可达功能；历史 `notes.json`、备份和旧源码为兼容保留，不由生产 Host 构建或执行。当前可达主导航为：首页、快捷键、语音、自检、设置。

## 2026-09-10 豆包输入法链路彻查与最终选型（用户真机迭代）

- **结论：豆包无法接入遥控器自动链路**（已用全部可行手段实测）：
  1. 面板无法被软件唤起——豆包安全过滤忽略全部模拟输入：`keybd_event`、`SendInput`（含硬件扫描码）、模拟鼠标点击；对照实验显示同一组合真人按键立刻有效，悬浮条的“中/英”等状态按钮对注入点击同样不响应。
  2. 面板不采集遥控器音频——豆包面板只使用其自选的麦克风（本机为蓝牙耳机 `HUAWEI FreeClip 2`），其麦克风列表不提供 `CABLE Output`，言灵送入的 CABLE 通道不会被豆包读取；豆包之所以“识别质量好”，正因为它直接采集真人声音。
  3. 其“长按录音”模式仅在按住快捷键期间收音，软件无法代替真人按住。
- **实现的诚实处理**：`provider=doubao` 时唤醒路径不再徒劳注入按键/点击（避免移动用户鼠标），改为记录 `DOUBAO AUTOMATION unavailable=true reason=vendor_filters_synthetic_input` 并在 60 秒节流下提示“按住左 Alt+D 手动唤起豆包，或切换到微信/Windows 语音输入使用全自动链路”；相关窗口枚举/点击代码与自检项已移除。
- **最终选型（用户确认）**：默认语音工具恢复为**微信输入法**（全自动：松开后引擎识别 → 剪贴板载荷落地 → 受控 Ctrl+V 回填，实测松开到入框约 1.9 秒，其中约 1.3 秒为微信引擎固有识别耗时）。豆包仍可在需要时手动使用（不改动其自身设置）。
- 验证：`BUILD_VIBE_MIC.cmd`、`--self-test`、`npm test`、`Test-V2FeatureSuite.ps1` 全部 PASS；发布目录/桌面发布目录/已安装版一致。

## 2026-09-10 微信回填提速与默认引擎切换（用户真机迭代）

- **回填提速**：触发点从“面板关闭确认（SESSION END，约 +3.3s）”提前到“提交回执（TRANSCRIPTION SUBMIT，松开后约 0.5s）”，并按剪贴板公开序列号等待载荷写入+稳定（2 次稳定采样约 120ms）后立即粘贴；实测松开→文字入框约 1.9s，其中约 1.3s 为微信引擎识别耗时（第三方固有）。`ShouldScheduleProviderPasteFallback` 新增提交回执分支，自检新增 4 组早触发用例；validate.js 门禁不变（仍禁 Enter/剪贴板文本访问，wechat 专属）。
- **默认语音工具切换为豆包输入法**（用户选择“直写、零剪贴板”）：用户配置改为 `inputMethod=doubao / alt+space / toggle / 180ms`（备份 `vibe-mic-config.json.pre-doubao.bak`）；`CAPTURE START provider=doubao` 已确认生效。微信输入法可随时在“语音”页切回（回退到受控粘贴路径）。
- **真机诊断（转译无文字）**：日志显示快速连按会反复切换微信面板导致音频被丢弃（`voice_panel_unavailable`），且当时 BLE 音频流出现 0.5–1.1s 断流（max_gap_ms 553/1109）与 100% 削顶——判定为遥控器电量/距离或连按导致，非软件缺陷；用户确认电量正常后复测。
- 待真机验证：豆包在目标应用内的直写链路（需先在目标应用激活豆包输入法）。

## 2026-09-10 微信剪贴板回填彻底修复（用户真机反馈）

- **问题复现**：用户真机听写后文字仍停在剪贴板。日志证据（01:33–01:34 三次会话）——唤醒时微信语音面板抢占前台，`voice_wake_preflight` 报 `FOCUS-PROCESS-MISMATCH`；会话结束后粘贴回退在面板让出前台之前就放弃（`target_unverified / FOCUS-TRANSIENT-UNSUPPORTED`、`FOCUS-TARGET-NOT-EDITABLE`）。
- **修复（`scripts/VibeMic.cs`）**：
  1. 唤醒快照：按住录音键的瞬间记录“会话来源进程 + 其已聚焦可写编辑控件描述符 + 剪贴板序列号基线”（仅观察计数器，不读取文字）。
  2. 载荷等待 `AwaitPastePayloadReady`：按唤醒时的序列号基线等待微信剪贴板载荷真正落地，避免把过期内容粘进输入框；已落地则零等待。
  3. 目标解析链：已验证 Smart Focus 目标（观察或经 `TryRestorePasteTarget` 恢复）→ 会话来源目标（等面板让出前台后恢复/重捕获）→ 前台实时捕获兜底。全程不抢无关应用前台（前台属于目标进程或微信面板才等待/激活）。
  4. 注入仍为单次 Ctrl+V；不读文字、不自动回车、不自动发送；仅 wechat、仅确认会话回执后触发。
- 门禁：`validate.js` 粘贴回退段约束不变（仍含 wechat/回执/ctrl+v/target_unverified，仍禁 Enter 与 `Clipboard.` 文本访问——序列号 P/Invoke 与辅助函数位于该段之外）。`BUILD_VIBE_MIC.cmd`、`--self-test`、`npm test`、`Test-V2FeatureSuite.ps1` 全部 PASS。
- 待真机验证：用户在常用目标应用内按住说话→松开→文字自动入框；另测“听写中切走应用→不抢焦点、提示手动粘贴”。

## 2026-09-09/10 全应用视觉美妆收口与安装器语音组件任务

- **全应用视觉收口（UI 设计子代理 + vibe-flow-ui-redesign 技能，沿用已定稿 Liquid Glass 语言）**：首次设置向导脚部品牌渐变发丝线 + 步骤条紫→青竖向渐变（共享 `DrawBrandHairline`）；首页“开始一次听写”流程圆点/连接点改主题感知 `StatusSurface`/`StatusBorder`/muted；快捷键各内联对话框（录制/选择/取消、绑定 Smart Profile、新建档案、EXE 选择）按钮全部接入共享 `ApplyFlatButtonFeedback`（主题化悬停/按压 + 6px 圆角 Region），Smart 切换与按键卡片加入悬停反馈；语音页状态带 8px 圆角、锁定/设置输入目标升为 PrimaryButton；自检评分圆盘描边主题化、不支持项灰→muted、会话总结带圆角；设置页 白天/夜间/跟随 Windows 分段控件逐项悬停/按压、启动状态带圆角；`FocusTargetDialog`/`CaptureAskForm`/`BrowserRemoteLiteForm` 按钮补齐真实悬停/按压阶梯 + 圆角（深色主题主按钮品牌紫）；`ContextDeckForm` 标题下品牌渐变发丝线。冻结件与语音链路零改动（`LiveHudForm.cs`、`VibeMicAtvvCapture.cs`、`VoxDeckInputBridge.cs`、语音/供应商/音频/蓝牙逻辑未动，HUD 已定稿版未动）。
- **“从下载第一步”安装体验**：`installer/VibeFlow.iss` 新增默认勾选任务 `installvbcable`——安装完成后以内置官方 VB-CABLE Pack45 包校验后运行驱动安装（UAC 由脚本自理）；静默安装按 `skipifsilent` 跳过，不打扰无人值守部署。`validate.js` 新增门禁：`[Tasks]` 必须含 `installvbcable` + 捐赠软件署名 + `checkedonce`；`[Run]` 必须含 `Install-VBCable.ps1 -Install` + `Tasks: installvbcable` + `skipifsilent`。
- **向导文档截图真机化**：`capture-ui-screenshots.ps1` 支持 `--ui-smoke` 预览文案（预览下一任务/结束界面预览）自动识别并按持久句柄驱动；文档 5 步向导截图全部改为真实链路捕获（真实方向键证据→本地音频通道→真实听写确认→开机即用），正式按钮文案（完成本步，继续/打开首页）与真实状态一致。
- 验证：`BUILD_VIBE_MIC.cmd`、`VibeMic.exe --self-test`（exit 0）、`npm test`、`Test-V2FeatureSuite.ps1`、`--ui-resource-test`（300 次切页，USER Δ7 / GDI Δ12，无 FAILURE）全部 PASS；`BUILD_RELEASE.ps1` 重建 Host/Bridge、`VibeFlow-Setup.exe`、ZIP 与 SHA256SUMS；发布目录/桌面发布目录/已安装版三处二进制逐字节一致（Capture 仍为冻结 SHA `B62DE035…E683`）。

## 2026-09-09 移除快捷入口（产品负责人确认）

- 产品负责人要求完全移除“快捷入口”用户功能。删除入口页面、对话框、热键注册和向导源文件（`scripts/ui/ProjectsPage.cs`、`QuickEntriesPage.cs`、`QuickEntryDialog.cs`、`QuickEntriesIntegration.cs`、`QuickEntryHotkeys.cs`、`ProjectSpaceWizard.cs`、`scripts/tests/QuickEntryTests.cs`），主导航收口为首页、快捷键、语音、自检、设置五页。
- Project Space 数据后端保留：`project-spaces.json` 等用户数据文件不删除，仅继续服务 Capture & Ask 目标解析、HUD/Context Deck 项目名显示和 Host 自检；初始化与执行辅助逻辑移入 `scripts/VibeMic.cs`，不再构建任何入口 UI 或执行后端。
- 按键页移除 V13 旧版“选择应用或网页”绑定入口；首次设置第 5 步不再出现 APP 入口按钮；启动/退出不再注册入口热键；Context Deck 行标签改为“项目现场”，HUD 未配置文案改为“未配置项目”。
- 验证：`BUILD_VIBE_MIC.cmd`、`VibeMic.exe --self-test`、`npm test`、`Test-V2FeatureSuite.ps1`、`BUILD_INPUT_BRIDGE.cmd`、`VoxDeckInputBridge.exe --self-test`、`BUILD_RELEASE.ps1` 全部通过；发布目录与安装包已同步更新。

## 2026-09-09 深夜：品牌化 UI 升级、收音诊断与豆包适配

- **UI 品牌化（科技品牌风）**：五页+首次设置向导沿用统一设计令牌；共享按钮全部加入主题化悬停/按压反馈（Primary/Secondary/FlatButton）；自检评分圆盘修正为卡片底色；HUD 经用户多轮评审定稿为 Liquid Glass 风——圆角窗体、深空蓝黑玻璃、紫→青渐变饰边与顶部扫光、程序绘制的高清矢量麦克风图标（不依赖字体）、按状态扩散脉冲光环（录音紫/处理青/错误红/成功绿，纯状态光波，不代表音频）、真实音频峰值条（仅回显实测 RMS）、透明标签背景消除色差、精简核心信息布局（标题状态+一行关键信息+目标/Profile+峰值）。HUD 状态色与主窗一致（录音=品牌紫、处理=青、成功=绿、警告=琥珀、错误=红）。
- **收音诊断结论（不动冻结 Capture 与稳定参数）**：真实诊断 WAV 分析——安静段底噪 -67 dBFS、处理后信噪约 28 dB、原始瞬态峰值已达 0 dBFS。据此**保持 gain=1.0 等冻结档位**；误识别主因在语音工具引擎/设置，语音页 wechat 提示新增“识别语言选普通话、保持 10–20 cm 说话距离”指引。
- **豆包输入法适配（v0.9.0.0，真机验证）**：定位到 `C:\Program Files\DoubaoIME`（语音快捷键=Alt+空格、全局快捷键默认关闭）；Host 将 doubao 默认快捷键改为 `alt+space`、进程识别加入 ImeService/DoubaoImeSettings、帮助文案说明需在目标应用内激活豆包输入法。**真机：切豆包输入法→按住遥控器说话→松开→文字入框 ✅**。默认语音工具按用户选择**保持微信输入法**（豆包可随时在语音页切换）。
- 构建/测试：Host/Bridge 重建、`npm test`、`--self-test`、V2 套件、`BUILD_RELEASE.ps1` 全部 PASS；安装版、桌面发布目录、发布包一致；HUD 静态图/动图（hud-*.png/gif）与全页面截图（tmp/ui-final-brand-20260909）同步到桌面发布目录 docs\images。

## 2026-09-09 深夜：微信输入法受控粘贴投递（用户确认通过）

- 用户要求“只用微信输入法”。微信输入法 2.1.3.18 语音在本机不提供“自动上屏”选项，识别文字由输入法自行放入剪贴板（设置中无相关开关，用户已确认）。
- 新增受控粘贴投递（`scripts/VibeMic.cs`）：仅 `provider=wechat`、仅在“WETYPE SESSION END + audio_delivered=True + submitted=True”的真实回执后；仅当输入目标刚被验证为“可写 Edit + 键盘焦点”（锁定目标或当前前台可写控件，最多重试 4 次）时，自动发送一次 Ctrl+V 把输入法自己的剪贴板文字粘贴进输入框。不读取/保存/上传文字、不自动回车、不自动发送；目标未验证或用户已切走则跳过并提示手动粘贴。
- 自检断言 `ShouldScheduleProviderPasteFallback`（wechat 专属、无音频/未提交/录音中/重复 generation 均拒绝）；`validate.js` 新增门禁：回退段不含 Enter 注入、不含 Clipboard.* 文本访问。
- 真机：ChatGPT 输入框 → 按住说话 → 松开 → **文字自动入框、松开不发送、确认键只发一次**（用户确认 ✅）。首次会话日志显示一次 target_unverified 跳过（微信输入法自身该次已直写），重试逻辑已补强。
- 构建：Host/Bridge 重建、`npm test`、`--self-test`、`BUILD_RELEASE.ps1` PASS；桌面发布目录、已安装版、开机自启项均已更新为同一构建。

## 2026-09-09 晚：录音键 F5 隔离、输入落点真机结论与稳定性收口

### 实测机制（探针数据）

- 本机输入栈中，低级键盘钩子先于 Raw Input 约 10 ms；钩子返回 1（抑制）会取消对应 Raw Input 包。用户态无法逐事件判定来源设备，因此精确“仅遥控器按键隔离”不可行；V1.5 的钩子抑制是把 F5 当语音键并全局吞掉。
- 新方案：**RC003 在线范围内**的钩子抑制 + 钩子直接驱动语音状态机（来源 `rc003_present_hook`）；遥控器不在线时普通键盘 F5 直通；健康签名过滤器存在时钩子全部旁路。真机验证：浏览器前台按录音键不再刷新页面（此前每次按压都会刷新）。
- 新增释放边沿看门狗、重复 DOWN 日志限流、设备变更统一释放注入键、health 暴露 `voice_f5_isolation`/`voice_f5_suppressed_edges`。

### 输入落点真机对照（用户配合实测）

- 微信输入法 × ChatGPT 桌面端：松开后文字进剪贴板，输入框为空。**不经过 Vibe Flow 单独操作微信输入法同样失败**——第三方工具在 Chromium/网页输入框的语音回退，非 Vibe Flow 缺陷。追加证据：微信输入法 × 记事本（原生 Win32 编辑框）在本机同样直写失败（链路日志完整：面板就绪、音频送达、提交已发，文字仍进剪贴板）。用户知悉后选择**保持微信输入法为默认**，Windows 语音输入仍可在语音页一键切换并已实测直写 ChatGPT/记事本。
- Windows 语音输入 × ChatGPT 桌面端：路径 A（手动点击输入框）与路径 B（语音页“锁定输入目标”）全部真机通过：按住说话→松开→文字自动进入原输入框；松开不发送；按确认键只发送一次。录音键不再触发页面刷新。
- 语音页新增如实提示（微信输入法网页类输入框会退剪贴板，建议 Windows 语音输入）；默认语音工具仍为微信输入法（用户选择保留），可在语音页随时切换。
- 开机键实测：可上报 vk 0xFF/scan 0x5E，但同时打开麦克风流且与语音键重连变体相同，无法安全绑定动作；不改动 Windows 系统电源键行为，限制已写入文档。

### 修复文件（本增量）

- `scripts/VoxDeckInputBridge.cs`：RC003 在线范围 F5 隔离（钩子驱动语音边沿）、设备变更统一释放、卡死释放看门狗、重复日志限流、健康字段、损坏字符串编码修复（全文件 ASCII 化后转回 UTF-8 中文）。
- `scripts/VibeMic.cs`：语音页语音工具兼容性提示（如实、不虚构）；`VoiceProviderCompatibilityNote` 纯函数。
- `scripts/validate.js`：门禁更新为新的“RC003 在线范围”策略（含自检断言：遥控器不在线/过滤器健康/映射关闭时普通键盘 F5 必须直通）。
- 文档：`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`、`docs/V2_0_RELEASE_NOTES_ZH.md`。

### 验证状态

`npm test` PASS；Host/Bridge/Capture `--self-test` PASS；`BUILD_RELEASE.ps1` PASS（未签名本地候选）。真机：浏览器无刷新、ChatGPT 双路径落点、松开不发送、确认键单发、逐键矩阵（方向/功能/Home/TV/确认）通过。待验证：蓝牙断连/移除中途按住、休眠恢复、系统重启后的启动路径、100% 长按矩阵、VB-CABLE 全新安装、Windows 10/11 安装生命周期。

## 2026-09-09：已验证输入目标的录音焦点恢复（当前实现）

### 根因与修复

- 日志证明 RC003 音频帧、VB-CABLE 输出和 WeType 开始/结束动作均已到达；Host/Bridge/Capture 没有普通转写文字的剪贴板读写路径。剩余断点是语音工具面板出现后可能短暂夺走已验证输入控件的焦点。
- `scripts/features/FocusTargetService.cs` 新增仅面向已验证 UI Automation 编辑控件的焦点恢复：只扫描原目标进程、验证可写 `Edit` 和键盘焦点，不读取文字、不触碰剪贴板、不发现新目标。
- `scripts/VibeMic.cs` 在真实录音开始及停止提交回执处执行一次受限恢复；仅当目标锁仍有效，且前台属于原目标或已知语音工具时执行。用户切换到无关应用时安全跳过，不强抢焦点。
- 新增 `ShouldRestoreVerifiedVoiceFocus` 回归断言，覆盖目标被工具夺焦、目标仍聚焦、无锁、无关前台和非录音状态。

### 构建、测试与 UI 证据

- `BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd`、`scripts/tests/Test-DevelopmentBuild.ps1`：PASS。
- `npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test`：均 exit 0。
- Capture 源码 SHA-256 仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`；Capture 二进制 SHA-256 仍为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 实际启动新构建 Host 并通过 Windows UI Automation 点击“语音”页的“锁定输入目标”；实测焦点为 ChatGPT `ControlType.Edit`、`ProseMirror ProseMirror-focused`、`HasKeyboardFocus=true`。截图保存在 `tmp/ui-captures-after-focus`。

### 未验证边界

- 当前 RC003 设备级过滤器仍为 `fallback:open_failed_win32_2` / `native_passthrough`；未安装驱动前不能安全阻止 RC003 原始 F5 穿透，也未宣称普通键盘与遥控器已完成硬件隔离。
- 本环境没有在本轮执行真实 RC003 按住/松开后的 WeType/ChatGPT 最终文字入框，因此不能声称第三方文字落点已经通过真机验收。必须在用户目标设备上验证“无手动粘贴、文字进入当前 ChatGPT 输入框、确认键只发送一次”。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。代码侧已修复已验证目标被语音工具面板夺焦时的恢复路径；硬件过滤器和第三方文本投递仍保持诚实的未验证状态。

## 2026-09-09：录音焦点审计后安全收口（当前状态）

### 审计结论

- 当前冻结 Capture 仍按 V1.5 的录音边沿工作：Bridge 立即派发 `VoiceKeyPressed`，Host 的 UI Automation 观察只能在其后提供状态反馈，不能成为 Capture 的录音前门禁。`VoiceFocusReady/Rejected` 事件没有 Bridge 消费者，已不再作为握手能力宣传。
- 设备级 RC003 过滤器当前仍是 `fallback:open_failed_win32_2`，低级 Hook 没有设备身份。此前的 `keyboard_hook_rc003_fallback` 会在 RC003 存在时误吞普通键盘 F5，已移除；无健康签名过滤器时普通键盘 F5 必须直通，设备隔离仍为未验证/受阻状态。
- Vibe Flow、Bridge 和冻结 Capture 没有普通转写文字的剪贴板读取、写入或回填路径。文字落入剪贴板仍只能归因于第三方语音工具在输入焦点失效时的外部回退，ChatGPT/WeType 端到端落点尚未验证。

### 本阶段修改

- `scripts/VoxDeckInputBridge.cs`：移除设备盲低级 Hook 的录音键回退拦截；保留 V1.5 的按住开始、松开结束和去重时序。
- `scripts/VibeMic.cs`：移除无消费者的 VoiceFocusReady/Rejected 命名事件；Host 事件监听改为单次异常可恢复，避免一次 UIA/BeginInvoke 异常永久停止唤醒与缺失目标反馈。
- `scripts/validate.js`、`scripts/tests/FeatureSurfaceTests.cs`：门禁改为拒绝危险 Hook 回退，并准确区分“录音边沿保持”与“焦点观察反馈”。

### 当前结果与未验证项

- 自动构建和测试需在本阶段修改后重新运行；Capture 源码、二进制、参数、录音 generation、Raw Input 和配置 schema 未改动。
- 真实 RC003 按住/松开、普通键盘 F5 隔离、ChatGPT UIA 目标测试、WeType 文字直写、VB-CABLE、蓝牙重连/睡眠恢复仍为 `NOT_RUN`。当前阶段不得宣称“剪贴板问题已完全修复”。
- 若要消除无设备身份时的 F5 穿透，必须先获得用户明确授权，再安装/验证签名 RC003 设备过滤器；本轮不安装驱动、不修改系统设备。

阶段结果：**FAIL / BLOCKED BY HARDWARE FILTER AND THIRD-PARTY TEXT DELIVERY**。代码侧已移除危险绕过并保持冻结录音契约；完整闭环仍需真实设备和语音工具证据。

## 2026-09-09：录音前焦点租约去重与失效锁清理

### 本阶段问题与根因

- Bridge 之前等待 `VoiceFocusReady` / `VoiceFocusRejected` 两个一次性事件；迟到结果没有请求关联，旧 Focus 锁也可能在新的录音边沿被误认为仍然有效。
- 这些事件只能控制显式 `VoiceKeyPressed`，冻结 Capture 收到 RC003 自然 ATVV 音频时仍会按既有契约启动会话；因此不能把事件握手描述成对第三方输入法的完整前门禁。
- 当前机器的设备级 RC003 过滤器仍为 `fallback:open_failed_win32_2` / `native_passthrough`。低级 Hook 没有设备身份，不能安全吞掉普通键盘 F5；这仍可能让 RC003 F5 穿透前台并使第三方工具回退到剪贴板。

### 本阶段修改

- `scripts/VoxDeckInputBridge.cs` 使用共享的 `Local\\VibeMicFocusTargetLocked` 手动租约：每次录音边沿先清旧租约，等待 Host 被动验证当前前台可写控件，且只在按键仍按住、租约仍为当前信号时派发显式录音事件。
- `scripts/VibeMic.cs` 的预检失败先清理旧锁，Host 侧只接受“本次验证成功 + 按键仍按住 + 当前租约仍存在”，不再沿用历史锁；退出时继续复位并释放租约。
- 增加 Host/Bridge 自检断言，未修改 Capture 源码、二进制、录音 generation、Raw Input、Hook、设备过滤器、录音参数或剪贴板文字路径。

### 构建、测试与原生 UI

- `cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd`：PASS。
- `npm test`、`node scripts/validate.js`、`powershell -File scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test`：均 exit 0。
- 实际启动根目录 `VibeMic.exe`（PID 3864），Host 日志记录 `VOICE FOCUS LOCK armed=true ... source=voice_wake_preflight`；UIA 当前焦点实测为 ChatGPT `ControlType.Edit` / `ProseMirror ProseMirror-focused` / `HasKeyboardFocus=true`。原生截图保存于 `tmp/ui-captures-focus-lease`，首页、语音、快捷键、自检、设置均可达，无明显遮挡或刷新抖动。
- 当前 `input-bridge-health.json` 仍报告过滤器未健康；真实 RC003 按住/松开、WeType/ChatGPT 最终文字入框、普通键盘 F5 隔离、VB-CABLE 和蓝牙恢复仍为 `NOT_RUN`，不能声称剪贴板问题已完成真机闭环。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。本阶段收口了 Host/Bridge 的焦点租约竞态，但冻结 Capture 的自然音频路径和缺少设备过滤器仍是外部边界；不得以自动化结果替代用户真机验收。

## 2026-09-09：录音唤醒与 Smart Focus 竞态收口

### 问题与根因

- 低级键盘 Hook 只有 F5 和扫描码，没有设备来源；旧逻辑在 Smart Focus 锁存在时吞掉 F5，可能同时吞掉普通键盘 F5。设备级 RC003 过滤器当前仍为 `fallback:open_failed_win32_2`，不能用全局 Hook 伪造设备隔离。
- Smart Focus 的空闲轮询会在前台变化时异步建立或撤销录音锁；录音唤醒和 Host UI 刷新之间存在竞态，不能证明语音工具启动前焦点仍是已验证编辑控件。

### 修复动作

- 移除低级 Hook 的 Smart Focus F5 抑制分支；只有健康的设备级过滤器可以承担 RC003 键盘隔离，普通键盘 F5 始终直通。
- 停止空闲焦点轮询；在收到物理录音唤醒事件后，先同步、被动检查当前前台可写 `Edit` 控件，再进入原有 Host/Provider 流程。已锁定目标不会被该检查重置；检查失败只显示未确认，不激活窗口、不读取文字、不读取或回填剪贴板。
- 新增 `ShouldRunVoiceFocusPreflight` 和 Bridge 自检断言，锁定录音前置检查与输入隔离边界；Capture 源码、二进制、参数和录音 generation 未修改。

### 构建、测试与原生 UI

- `cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd`：PASS。
- `npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test`：均 exit 0。
- Capture 二进制最终从既有候选副本恢复并复核冻结 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`；源码 SHA-256 仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- 实际启动根目录 `VibeMic.exe`（PID 22008）并执行 `scripts/capture-ui-screenshots.ps1 -ProcessId 22008 -OutputDirectory tmp/ui-captures-focus-fix-current -AllowUnhealthyDiagnostics`。首页、按键、语音、自检、设置截图生成成功；语音页实际显示 `ChatGPT 输入框 · 已验证 · 录音前请先锁定`，按键页保持独立可达。
- 计算机桌面交互工具当前不可用，未执行真实 RC003 按住/松开、WeType/ChatGPT 最终文字落点和设备级过滤器安装验证；这些项目继续标记为 `NOT_RUN`。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。设备过滤器缺失和第三方语音工具的实际文字落点仍是硬件/外部兼容性边界，不能声称剪贴板问题已完成真机闭环验证。

## 2026-09-08：ChatGPT 输入目标收口与候选包重建

### 本阶段目标

- 保持便签本用户入口移除，不删除历史 `notes.json`、备份或旧源码。
- 针对“录音后文字落入剪贴板”问题完成真实路径核查：Host、Bridge、Capture 均没有普通文字剪贴板写入、读取或回填路径；当前缺少已验证默认输入目标时，第三方语音工具可能自行回退到剪贴板。
- 为 ChatGPT 桌面端增加受约束的 Smart Focus 学习提示：仅识别已实测的 `chatgpt` 进程、`Edit` 控件和 `ProseMirror` 稳定类名；仍要求用户点击“立即测试”成功后才能保存，不把录音键接入焦点流程。

### 本轮证据

- `cmd /c BUILD_VIBE_MIC.cmd`：PASS。
- `cmd /c BUILD_INPUT_BRIDGE.cmd`：PASS。
- `npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- Host、Bridge、冻结 Capture `--self-test`：均 exit 0。
- `BUILD_RELEASE.ps1`：PASS，重新生成未签名本地 `release/VibeFlow-Setup.exe`、`release/Vibe-Flow-Windows-x64.zip`、`release/SHA256SUMS.txt`；包内 Host 与根目录哈希一致。
- `scripts/tests/Test-ReleaseIdentity.ps1`、`scripts/tests/Test-ReleaseArtifacts.ps1`：PASS。
- `scripts/Test-ReleaseLifecycle.ps1 -NoConfigFixture`：未运行完整生命周期，因当前账户已有 Vibe Flow 启动项、安装目录和用户数据而按保护规则拒绝；未删除或覆盖现有状态。

### 原生 UI 与未验证边界

- 已实际启动 Host：无便签入口；快捷键页仍独立可达；语音页显示“尚未设置输入目标 · 不会自动切换 APP”。
- ChatGPT 目标选择会显示“ChatGPT 输入框”，学习按钮可识别稳定 `ProseMirror` 控件并要求“立即测试”后保存。
- 尚未由 Agent 自动操作 ChatGPT 桌面端，因此 ChatGPT 真实聚焦、WeType/微信真实文字落点、RC003 按住/松开和设备级按键过滤器仍标记为未验证；不能声称已完成端到端直填。
- 只读 auditor 未发现 BLOCKER/HIGH；保留一个 MEDIUM：Bridge 的部分 `SendInput` 失败补偿仍是单次尝试，极端注入失败时可能留下修饰键，后续单独安排输入注入回归，不以修改 Capture 绕过。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。未签名、未发布。

## 2026-09-08：录音前 ChatGPT 当前焦点验证

### 根因证据

- 最近两次真实遥控器录音前，Host 日志均为 `VOICE INPUT TARGET ready=false code=FOCUS-TARGET-MISSING`；随后 Capture 正常收到音频，WeType 正常派发工具栏开始/提交动作。
- 当前 `focus-targets.json` 的 `defaultTargetId` 仍为空，说明 Smart Focus 没有可执行的持久目标；这不是 Capture 音频失败。
- Host、Bridge、Capture 没有普通转写文字写入、读取或回填剪贴板的代码路径；剪贴板结果来自语音工具在未验证输入焦点时的外部回退行为。

### 最小修复

- 新增“当前焦点临时验证”：没有保存目标时，只对当前已获得键盘焦点的 ChatGPT `Edit + ProseMirror` 编辑框进行一次性确认。
- 该路径不激活窗口、不抢焦点、不保存配置、不读取文字；非 ChatGPT、非编辑控件或无焦点时继续拒绝验证并显示修复入口。
- 已按 TDD 先验证缺失函数导致 Host 编译失败，再实现并通过 Host self-test。

### 验证

- `cmd /c BUILD_VIBE_MIC.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`：PASS。
- Host、Bridge、Capture self-test：PASS；Bridge 首次并行构建遇到运行锁后已单独重建并通过。
- Capture 源码/二进制哈希仍为冻结值，未修改 Capture 或录音参数。
- 仍需用户在 ChatGPT 输入框实际获得焦点后，用 RC003 完成端到端落点验证；RC003 过滤器当前仍是 `fallback:open_failed_win32_2`，不能宣称剪贴板问题在真实硬件上已完全消失。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。未签名、未发布。

## 2026-09-08：最终稳定性收口与候选重建

### 本阶段目标

- 移除便签本全部用户可达功能，同时保留历史数据和源码作为迁移/审计资料。
- 修复 Bridge 配置重载时的活动快捷键释放、手动重载绕过释放、部分 `SendInput` 注入失败后的修饰键清理。
- 修复首次设置第 4 步读取测试框文字的问题，改为“真实音频/工具回执 + 用户目视确认”两段式证据，不读取、不保存第三方转写文字。
- 保留录音、麦克风、快捷键、自检和配置保护冻结契约；不修改 Capture、Raw Input、Hook、设备过滤或录音状态机。

### 修改文件

- `scripts/VoxDeckInputBridge.cs`：活动 hold 映射缓存；配置缺失/手动重载统一先释放；部分 `SendInput` 和 Task View chord 失败时补发释放；Bridge self-test 增加空配置释放与恢复顺序断言。
- `scripts/VibeMic.cs`：首次设置不再读取 `testInput.Text` 或计算文字长度；用户目视确认后点击“我已看到文字”；状态文案不宣称转写完成或 AI 收到。
- `scripts/validate.js`：增加 onboarding 不读取测试框文字的门禁。
- `BUILD_HARDWARE_CANDIDATE.ps1`：候选包图片改为与正式包一致的明确白名单，历史 `02-notes.png` 不再进入载荷。
- `docs/V2_0_PROGRESS.md`：记录本阶段证据和未验证边界。

### 构建与自动测试

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` / `node scripts/validate.js` | PASS |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `VibeMic.exe --self-test` | PASS |
| `VibeMic.exe --ui-resource-test` | PASS；300 次页面切换，USER/GDI 增量在门限内 |
| `VoxDeckInputBridge.exe --self-test` | PASS；含重载释放和部分注入恢复顺序断言 |
| `VibeMicAtvvCapture.exe --self-test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS |
| `scripts/tests/Test-ReleaseIdentity.ps1` | PASS |
| `scripts/tests/Test-ReleaseArtifacts.ps1` | PASS |
| `BUILD_HARDWARE_CANDIDATE.ps1` | PASS；候选包不含 `02-notes.png`、Notes 源文件或 `product_ai_prompts` |
| `BUILD_RELEASE.ps1` | PASS；生成未签名本地 Setup、ZIP、SHA256SUMS |
| `git diff --check` | PASS（仅换行风格警告） |

冻结 Capture 仍为：源码 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，二进制 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。版本 `1.2.1.0`、按住录音、自然松开结束、180 ms 排空、CABLE Input 和 `captureSeconds=0` 均未改变。

### Computer Use 原生 UI

- 实际启动当前 `VibeMic.exe`：首页无便签入口，显示录音“按住说话/松开结束/用户确认发送”和 RC003 过滤器未就绪的真实警告。
- 实际打开“快捷键”：V1.5 Profile、实体键动作、录音键“固定稳定链路”和独立测试按钮均可见。
- 实际打开“语音”：显示按住模式、稳定参数、CABLE 状态和“尚未设置输入目标·不会自动切换 APP”。
- 实际打开“自检”：真实音频/麦克风正常，按键过滤器和输入目标仍以“待验证/需要配置”呈现，没有伪造成功。
- 实际打开“设置”：Raw Input 安全直通、隐私边界和首次设置入口可见。
- 实际打开唯一“五任务首次设置”：第 1 步显示按住/松开/目视确认/手动发送；侧栏显示任务 1/5；文案明确“不读取或记录转译文字”。

### Auditor 与未验证边界

- 只读 `release_audit_final` 复核：此前候选包过期、历史 Notes 截图和 Bridge 重载释放问题已处理；最终正式包与根目录 Host/Bridge 哈希一致。
- 尚未完成的人工硬件项目：RC003 设备级过滤器安装与 100 次按住/松开、普通键盘 F5 隔离、微信/WeType 与 ChatGPT 输入框真实文字落点、蓝牙休眠/唤醒、VB-CABLE 重启恢复、Windows 10/11 安装升级、100/125/150/200% DPI、浅色/深色主题。
- 当前本机 `input-bridge-health` 仍为 `rc003_filter_healthy=false` / `fallback:open_failed_win32_2`；麦克风真实音频可以到达，但设备级隔离和第三方工具输入落点不能据此宣称通过。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。候选包未签名、未发布；不声称 ChatGPT 已收到文字或 AI 已完成理解。

## 2026-09-08：发布文档与候选载荷范围收口

### 本阶段目标

- 修复审计发现的范围漂移：当前 Host 已移除便签入口，但 README、V2 指南和候选包仍把 Notes Deck/便签 AI 描述为现行功能。
- 让安装后的用户只看到当前可达的首页、快捷键、语音、自检和设置说明。
- 不删除历史源码、历史 `notes.json`/备份或归档记录，不修改 Capture、Bridge、Raw Input、Hook、设备过滤或录音参数。

### 修改文件与边界

- `README.md`、`QUICK_START_ZH.md`、`docs/FEATURES_ZH.md`、`docs/RELEASE_NOTES_ZH.md`：移除便签/Notes Deck/便签 AI 的当前产品描述，改为稳定语音输入和快捷键配置闭环。
- `docs/V2_0_RELEASE_NOTES_ZH.md`、`docs/V2_0_USER_GUIDE_ZH.md`、`docs/V2_0_INSTALLER_GUIDE_ZH.md`、`docs/V2_0_*` 基线/测试/限制文档：统一当前范围，明确旧 Notes 仅作历史审计。
- `BUILD_RELEASE.ps1`、`BUILD_HARDWARE_CANDIDATE.ps1`：不再把 `product_ai_prompts` 或便签截图放入候选载荷。
- `scripts/capture-ui-screenshots.ps1`：不再生成便签页截图。
- `docs/v2-notes/PROGRESS.md`：顶部标记为历史归档，不作为当前测试或发布依据。

### 验收标准与状态

- 构建后候选目录和 ZIP 不包含 `product_ai_prompts`、`02-notes.png`、便签主导航或便签用户指南。
- `npm test`、V2 focused suite、发布身份/载荷测试、冻结 Capture 双哈希和 `git diff --check` 必须通过。
- 原生 UI 需重新启动实际 Host 检查六页导航；RC003、设备过滤器、第三方语音工具、ChatGPT 文字落点、DPI 和安装生命周期仍标记未验证。

## 2026-09-08：最终本地候选复核

- 重新构建 `BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd`（Bridge 运行锁解除后通过）和 `BUILD_RELEASE.ps1`；本地未签名候选已生成 `release/VibeFlow-Setup.exe`、`release/Vibe-Flow-Windows-x64.zip`、`release/SHA256SUMS.txt`，未发布。
- `npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture self-test、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、`git diff --check` 均通过；`VibeMic.exe --ui-resource-test` 通过 300 次页面切换，USER 增量 23、GDI 增量 12。
- 冻结 Capture 源码 SHA-256 为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，二进制 SHA-256 为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，均匹配；没有修改 Capture 源码、二进制、签名或期望哈希。
- Computer Use 实际检查：首页不显示便签入口；“快捷键”页可达且录音键显示“固定稳定链路”；“语音”页显示“尚未设置输入目标·不会自动切换 APP”；“自检”页显示真实音频正常、按键过滤器等待实体验证、输入目标尚未确认，没有伪造“转写完成”或“AI 已收到”。
- 只读 auditor 最终复核：未发现 BLOCKER/HIGH；保留的人工验证包括 RC003 设备级过滤器、真实按住/松开、微信/WeType 与 ChatGPT 输入框落点、普通键盘 F5 隔离、蓝牙/睡眠恢复、DPI/主题和安装升级生命周期。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。该结果不等同于 RC003、麦克风、WeType 或 ChatGPT 真实文字落点已通过。

## 2026-09-08：候选载荷与构建回归复核

- `npm test`、`node scripts/validate.js`、`Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture self-test 和 `VibeMic.exe --ui-resource-test` 均通过。
- 候选包已重建：`release/VibeFlow-Setup.exe`、`release/Vibe-Flow-Windows-x64.zip`、`release/SHA256SUMS.txt`。包内/ZIP 内不再包含 `product_ai_prompts`、`02-notes.png`、Notes 页面截图或便签用户文案；`RELEASE_NOTES` 仅是当前版本更新说明，不代表便签功能。
- Smart Focus 多窗口测试曾因构建步骤改变真实前台窗口而出现一次非确定性失败；先复现并确认根因后，为 `FocusTargetService` 增加可选的前台进程读取器，生产默认仍使用真实 Win32 前台窗口，测试使用固定 fixture 目标。修改后 focused suite 和完整候选构建通过。
- Computer Use 实际候选 UI：六项导航均可达；首页无便签入口；快捷键页保留 V1.5 映射且录音键显示“固定稳定链路”；语音页显示“尚未设置输入目标 · 不会自动切换 APP”；自检页显示真实音频正常、按键验证和输入落点待验证；设置页显示 Raw Input 安全直通与隐私边界。
- Auditor 范围漂移 BLOCKER 已通过文档和载荷收口处理；Capture 源码/二进制仍匹配冻结 SHA-256，未修改录音核心、Bridge 输入时序或设备过滤。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。未签名、未发布；RC003 过滤器、普通键盘 F5 隔离、真实 ChatGPT/语音工具落点、DPI、安装升级生命周期仍需人工验收。

## 2026-09-08：移除便签产品面与 ChatGPT Smart Focus UX 修复

### 本阶段目标

- 按用户确认移除便签本的所有产品入口和运行时构建入口；保留 `%LOCALAPPDATA%\Vibe Flow Remote\UserData` 下的历史 `notes.json`/备份文件，不删除用户数据。
- 保留首页、独立快捷键、语音、自检、设置五项主导航，避免删除便签时误删 V1.5 快捷键页。
- 修复 ChatGPT Smart Focus 学习体验：选择 `chatgpt` 进程后自动建议“ChatGPT 输入框”；仍必须完成稳定 UI Automation 学习、立即测试和保存为默认目标。
- 不把录音键绑定到 Smart Focus，不读取/回填转写文字，不修改 Capture、录音状态机、Raw Input、低级 Hook 或过滤器。

### 调查与实际 UI 证据

- 当前 `focus-targets.json` 的 `defaultTargetId` 为空，录音路径不会自动切换到 ChatGPT；这是现有冻结行为，不是可以通过录音键抢焦点绕过的缺陷。
- ChatGPT 进程实际存在可写 `Edit` 控件（`ClassName=ProseMirror ProseMirror-focused`，无 AutomationId），可由用户手动学习并验证；本轮未自动操作 ChatGPT 桌面客户端。
- Host、Bridge、Capture 和运行日志没有普通转写文字剪贴板写入/读取/回填。WeType 工具栏唤起和提交动作均已派发，但文字落点仍需真实 WeType + ChatGPT/目标输入框复现；不能宣称已由 Vibe Flow 修复。
- `input-bridge-health.json` 仍为 `rc003_filter_healthy=false`、`fallback:open_failed_win32_2`；Raw Input 和麦克风可用，但 RC003 原始录音键可能直通前台，过滤器安装/硬件验证未完成。

### 修改文件

- `scripts/ui/PageShell.cs`：移除便签主导航/页面构建，保留历史页面 ID 重定向，导航恢复为五项稳定页面。
- `scripts/VibeMic.cs`：移除首页便签卡片/入口、首次设置便签 CTA、设置页 AI/便签卡片、Notes Deck 生命周期调用；首页卡片重新排布避免空白和重叠。
- APP 入口集成文件：APP 入口使用独立录音优先保护，不再依赖 Notes Deck 兼容方法。
- `scripts/ui/FocusTargetDialog.cs`：增加 ChatGPT/Cursor/VS Code/通用应用目标名建议；选中应用时显示建议但不覆盖用户输入或已保存目标。
- `BUILD_VIBE_MIC.cmd`：生产 Host 不再编译 Notes/AI/Deck 源文件；历史源文件和用户数据保留用于兼容审计。
- `scripts/tests/Test-V2FeatureSuite.ps1`、`scripts/tests/FeatureSurfaceTests.cs`、`scripts/validate.js`：新增便签表面移除和 ChatGPT 目标建议门禁，停止执行 Notes Deck 产品测试。

### 构建与测试

| 命令/证据 | 结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS；含 FeatureSurfaceTests、Focus、Capture Ask、Browser、HUD、APP 入口 |
| `VibeMic.exe --self-test` | exit 0 |
| `VibeMicAtvvCapture.exe --self-test` | PASS |
| `VoxDeckInputBridge.exe --self-test` | exit 0 |
| Capture 源码/二进制 SHA-256 | 匹配冻结值，未修改 |

### Computer Use 原生 UI

- 实际启动 `VibeMic.exe --ui-smoke`：首页显示六项导航、设备/语音状态和过滤器警告；不再显示便签入口或最近便签卡片。
- 实际打开“快捷键”页：Profile、录音键固定稳定链路、确认键、Home、TV、功能键和方向键均可见。
- 实际打开“Smart Focus 输入目标”：ChatGPT 下拉项可见，目标名称自动显示“ChatGPT 输入框”；保存按钮仍保持禁用，直到学习和立即测试成功。

### 尚未验证与限制

- 需要用户在 ChatGPT 桌面客户端前台手动点击输入框完成学习/立即测试/保存默认目标；本轮未自动操作 ChatGPT。
- 需要 RC003、微信/Typeless 等真实供应商复现“文字进入剪贴板”现象；软件没有普通文字剪贴板回填路径，不能仅凭静态检查判定第三方回退已修复。
- 设备级过滤器、蓝牙休眠/唤醒、VB-CABLE、Win10/11、DPI/主题和安装升级仍未验证。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。未发布、未签名、未声明正式稳定版。

## 2026-09-08：核心 UI 回归修复与社群二维码更新

### 本阶段目标

- 恢复独立的 V1.5「快捷键」侧栏页面，保持录音键、长按/重复触发、Profile 和 Bridge ACK 行为不变。
- 精简首页，只保留设备/语音真实状态、便签、快捷键和自检入口；移除目标、项目、截图提问和 Live HUD 主卡片。
- 将便签页整理为 Notion 风格的两栏布局，提供明确的 Markdown `.md`、TXT `.txt` 导出，并将 JSON 备份/恢复与 Deck 放入「更多」。
- 将 `docs/images/vibe-flow-community.png` 替换为用户提供的新社群二维码；文件可读，SHA-256：`AC71A77366152CE38D14AC049B283B41142EBCDCFB782613036C03EDD85B317D`。

### 录音问题调查结论

- Host 录音事件路径没有调用 `ShowPage` 或重建主窗；录音状态只更新已有控件。
- 本机 `input-bridge-health.json` 显示 `rc003_filter_available=false`、`rc003_filter_healthy=false`、`routing_authority=raw_input`。在这种环境下，RC003 的 F5 可能沿低级 Hook 直通前台应用并触发刷新；这解释了输入框失焦现象。
- 未恢复 V1.2 的设备盲全局 Hook 拦截，因为它会把普通键盘 F5 误当遥控器录音，违反冻结输入安全边界。首页现在明确提示过滤器缺失并引导打开自检。需要签名 RC003 过滤器和真实硬件复测，当前标记为 `NOT_RUN`。

### 修改文件

- `scripts/ui/PageShell.cs`：恢复「快捷键」「语音」顶层导航，并保持 `BuildMappingsPage()` 的 V1.5 实际映射逻辑。
- `scripts/VibeMic.cs`：同步六页 UI 资源门禁，释放页面字体/Region，更新首页入口和非侵入式过滤器缺失提示。
- `scripts/ui/NotesPage.cs`：Notion 风格工具栏、独立 Markdown/TXT 导出、「更多」菜单和本地数据说明；未改变 `NotesStore`、revision、备份恢复或 AI 结果隔离。
- `scripts/validate.js`：更新六页导航、首页极简入口和便签导出门禁。
- `docs/images/vibe-flow-community.png`：替换社群二维码资产。

### 构建与测试

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `VibeMic.exe --self-test` | PASS |
| `VoxDeckInputBridge.exe --self-test` | PASS |
| `VibeMicAtvvCapture.exe --self-test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `+23`、GDI `+12` |
| Capture 源码 SHA-256 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，匹配 |
| Capture 二进制 SHA-256 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，匹配 |

### Computer Use 原生 UI

- 实际启动工作区 `VibeMic.exe --ui-smoke`，首页可见六项导航、设备/语音状态、打开便签/快捷键/自检入口，以及过滤器缺失的警告；目标、项目、截图提问和 Live HUD 主按钮不再出现在首页。
- 实际打开「快捷键」页：V1.5 Profile、录音键固定稳定链路、实体键动作和测试按钮均可见。
- 实际打开「便签」页：Markdown/TXT 导出按钮和「更多」按钮可见；展开菜单实际显示 JSON 备份、JSON 恢复、便签 Deck、汇总已选。
- 实际打开「自检」页：未运行 Bridge/RC003 过滤器时显示错误原因、影响和修复入口，没有伪造全绿。

### 尚未验证

- RC003 真机 100 次录音、蓝牙休眠/唤醒、VB-CABLE、微信/Typeless/ChatGPT 前台焦点和真实转写闭环。
- 设备过滤器安装/签名、Windows 10/11 多 DPI（100%/125%/150%/200%）、深色/跟随系统、安装升级生命周期。
- 新二维码在外部发布渠道中的扫码有效性。

阶段结果：`PASS WITH MANUAL HARDWARE CHECKS`。未发布、未签名、未声明正式稳定版。

最后更新：2026-09-06  
分支：`feature/v2-off-key-loop`  
基线 commit：`b47f7cdce8b753fade0c64c97332bebe80f17d2d`

> 当前工作区说明（2026-09-06）：Notes Deck 阶段未修改 Capture 源码/二进制、录音参数或用户配置保护。`scripts/VoxDeckInputBridge.cs` 相对仓库基线存在工作区输入隔离与版本改动，本文不把它描述为冻结文件；其 RC003、Raw Input、Hook 和设备过滤行为仍需真实硬件复核。

## 当前阶段

阶段 6 收口：移除便签产品面、恢复独立快捷键页并完成 ChatGPT Smart Focus UX 收口。旧 Notes Deck、项目现场和“言灵webflow”记录仅作历史审计，不再作为当前业务范围；生产 Host 不编译或执行 Notes/AI/Deck 源文件。真实 RC003、设备级过滤器、第三方语音工具和 ChatGPT 输入框落点仍未验证，不能作为正式稳定版发布。

## Notes Deck 最新闭环（2026-09-06）

- 新建便签取消/X 不再留下空白记录：主编辑器和 Deck 都使用 `NotesStore.TryDiscardDraft` 的 note revision CAS；关闭冲突时保留窗口和数据。
- 录音期间自动保存只延期，不抢录音焦点或写入录音链路；录音结束后下一次计时器 tick 继续保存。Deck 自动保存后清空再关闭的回归测试已加入。
- `BUILD_RELEASE.ps1` 已成功生成正式本地候选：`release/VibeFlow-Setup.exe` SHA-256 `C9F79C9819057D7AF6D8E6C021A33ADFFDAF2518DE1B9FE92AACC235782632C7`；`release/Vibe-Flow-Windows-x64.zip` SHA-256 `EC8ED7D6E42ED52BDED7D7B38EE234DA14B1A4514C9F95387B070121F2F71F28`。
- 正式包 Host 与根构建哈希为 `6119768807CC053D314D9326B071EEA8FFF5A8D068D87E1915623B6E0564979D`；Capture 源码/二进制仍分别为冻结值 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- `npm test`、V2 focused suite、Host/Bridge/Capture self-test、`Test-ReleaseArtifacts.ps1` 和 `git diff --check` 均通过；两个 reviewer 返回 BLOCKER/HIGH 0。
- Computer Use 已检查正式包首页和便签入口；启动初期需要等待 Bridge/Capture 数秒，之后窗口正常显示。正式安装器未运行、未签名、未发布。

状态仍分开记录：`BUILD_TEST=PASS`、`GUI=PASS（基础路径）`、`REAL_PROVIDER=NOT_RUN`、`REAL_APP_FOCUS=NOT_RUN`、`HARDWARE=NOT_RUN`、`DATA_MIGRATION=NOT_RUN`。结论为 `PASS WITH MANUAL HARDWARE CHECKS`，不是正式稳定版发布声明。

## 本轮用户问题与处理结果（2026-09-05）

- 已创建 `project-spaces.json` 中的 `yanling-webflow`：`editorKind=chatgpt`，使用稳定 packaged-app 引用 `app-id:OpenAI.Codex_2p2nqsd0c76g0!App`，不向 ChatGPT 传递 Workspace 参数，也不自动发送消息。
- 已实际重启 Vibe Flow 并在首页看到“言灵webflow · 未设置预览”；项目页看到 ChatGPT 对话工作台和正确 Workspace。项目进入回执只描述“打开/切换请求已派发”，不会声称 ChatGPT 已收到内容。
- 修复 Smart Focus 状态显示：已验证但目标进程不存在时显示“已验证 · 应用未运行”，避免把残留 Notepad smoke 目标伪装成可用；输入目标列表和项目选择也使用同一状态文案。
- 修复 Project Space 卡片把 ChatGPT 误标为“其他应用”的问题，改为“ChatGPT · 对话工作台”。
- 录音剪贴板调查：`VibeMicAtvvCapture.cs`、Bridge 和运行日志均未发现文本剪贴板写入/读取/回填；当前可见文本剪贴板仅用于诊断问题摘要，图片剪贴板仅属于 Capture & Ask。没有证据支持 Vibe Flow 主动把转写文字写入剪贴板，仍需在真实微信输入法和有效输入焦点下复现用户现象。
- Smart Focus 仍按冻结边界“先单独锁定并验证目标，再按住录音”；录音键不会在按下时抢焦点或进入第二套状态机。由于 Computer Use 明确禁止自动操作 ChatGPT 桌面客户端，ChatGPT 输入框学习/真实 UIA 验证标记为未完成、需用户手动点击 ChatGPT 输入框配合验证。

### 本轮验证

| 命令/操作 | 结果 |
| --- | --- |
| `npm test` | PASS |
| `BUILD_DEVELOPMENT.ps1` | PASS；Capture SHA-256 仍为 `B62DE035...E683` |
| `VibeMic.exe --self-test` | exit 0 |
| `VoxDeckInputBridge.exe --self-test` | exit 0 |
| `VibeMicAtvvCapture.exe --self-test` | exit 0 |
| 实际启动 Vibe Flow | 首页显示项目现场和“应用未运行”目标状态 |
| 实际项目页 | 显示“ChatGPT · 对话工作台”、Workspace 和“输入目标：未设置” |
| 真实硬件/ChatGPT UIA 学习 | 未验证；需要用户配合真实窗口和输入控件 |

## 阶段 0 用户目标

- 恢复并检查真实仓库，不根据提示词臆测。
- 锁定 V1.5 稳定录音、输入、配置、向导、构建和 UI 基线。
- 运行改动前测试与真实构建。
- 建立 V2.0 基线锁、实施计划、UI 规范和持续进度记录。
- 补足冻结身份/参数门禁；不修改 Capture 或 Bridge 输入核心。

## 阶段 0 最小完成标准

- [x] 当前 commit、分支、未提交内容和最近提交已记录。
- [x] 项目规则、release gate skill、auditor 定义已加载。
- [x] explorer 已只读调查录音/输入及 UI/配置/构建真实路径。
- [x] 改动前 `npm test`、Host/Bridge 构建和三个 self-test 已通过。
- [x] Capture 源码与本机冻结二进制实物哈希已匹配。
- [x] 活跃首次设置与 Legacy 可达性已确认。
- [x] 基线锁、实施计划和 UI 规范已创建。
- [x] 已对 UI 虚假转写完成提示先写失败测试，再做外围修正。
- [x] 只读 auditor 最终复审：BLOCKER/HIGH/MEDIUM/LOW 均为 0。
- [x] `vibeflow-release-gate` 阶段 0 报告完成，结果为 `PASS WITH MANUAL HARDWARE CHECKS`。

## 本轮实际加载的指令

- `AGENTS.md`
- `.agents/skills/vibeflow-release-gate/SKILL.md`
- `.codex/agents/vibeflow-auditor.toml`
- 会话流程能力：using-superpowers、brainstorming、writing-plans、using-git-worktrees、systematic-debugging、test-driven-development、receiving-code-review、verification-before-completion、computer-use。

仓库内未发现其他层级 `AGENTS.md` 或常见仓库级指令文件。

## 阶段 0 调查路径

### 录音与输入

- `VIBE_MIC_VERSION.md`
- `vibe-mic-config.default.json`
- `docs/RELEASE_QUALITY_GATE_ZH.md`
- `docs/ISSUE_2_REGRESSION_ZH.md`
- `scripts/VibeMicAtvvCapture.cs`
- `scripts/VoxDeckInputBridge.cs`
- `scripts/VibeMic.cs`
- `scripts/Get-StableCaptureBinary.ps1`
- `scripts/validate.js`

确认路径：Host 配置加载/迁移 → Bridge 配置原子生成与 revision ACK → 录音专用 physical transition → 冻结 Capture 的单 generation ATVV stream → 真实音频 → 设备自然 stop → 尾包与 drain → 第三方工具结束指令。录音键不进入普通 mapping queue。

### UI、向导、配置与发布

- `BUILD_VIBE_MIC.cmd`
- `BUILD_INPUT_BRIDGE.cmd`
- `BUILD_RELEASE.ps1`
- `installer/VibeFlow.iss`
- `scripts/capture-ui-screenshots.ps1`
- `.github/workflows/validate.yml`
- `scripts/Test-ReleaseLifecycle.ps1`
- `README.md`
- `docs/V1_5_USER_GUIDE_ZH.md`
- `docs/FEATURES_ZH.md`
- `docs/COMPATIBILITY_MATRIX_ZH.md`

确认路径：单文件 WinForms Host → 五页导航 → `ShowSetupWizard()` 唯一可达 5 任务向导 → 10 项自检。两个 Legacy 向导方法不可达。`BUILD_RELEASE.ps1` 构建 Host/Bridge、解析固定 Capture、运行三个 self-test、打包 ZIP/Inno/SHA，不重建或重签 Capture。

## 阶段 0 修改文件

| 文件 | 阶段 0 目的 |
| --- | --- |
| `scripts/validate.js` | 实际计算冻结 Capture 源码 hash；锁定 captureSeconds、录音键隔离、六个输入时序；禁止把录音结束显示为转写完成 |
| `scripts/VibeMic.cs` | 修正外围反馈；用纯分类函数和真实 Capture 日志样本验证“已送出音频 → 等待工具、未送出 → 错误” |
| `README.md` | 把首段“松开完成转译”改为“松开结束录音并等待语音工具处理” |
| `docs/V2_0_BASELINE_LOCK_ZH.md` | 真实基线与不可变边界 |
| `docs/V2_0_IMPLEMENTATION_PLAN_ZH.md` | 分阶段文件/接口/风险/回滚/验收 |
| `docs/V2_0_UI_SPEC_ZH.md` | V2 UI、状态、焦点、文案与可访问性规范 |
| `docs/V2_0_PROGRESS.md` | 持续进度与证据 |

没有修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、默认配置、安装器或冻结 hash 期望值。

阶段 0 开始前已有且继续保留的未跟踪项目指令/Agent 配置：`AGENTS.md`、`.agents/skills/vibeflow-release-gate/SKILL.md`、`.codex/agents/vibeflow-auditor.toml`、`.codex/config.toml`。它们不是本轮生成物。

本轮构建和 UI smoke 产生、且已由 `.gitignore` 忽略的文件：

- `VibeMic.exe`：Host 构建产物。
- `VoxDeckInputBridge.exe`：Bridge 构建产物。
- `vibe-flow.ico`：Host 构建生成的图标。
- `tmp/ui-resource-test.txt`：300 次页面切换资源压力报告。
- `tmp/ui-smoke/vibe-mic-config.json` 与 `.bak`：独立 smoke 用户状态。
- `tmp/ui-smoke/remote-voice-session/vibe-flow-host.log`：独立 smoke Host 日志。
- `input-bridge-log.txt`：误启普通模式时产生的工作区 Bridge 运行日志。
- `voxdeck-shortcuts.json` 与 `.bak`：误启普通模式时由 Host 生成的工作区 Bridge 配置快照；均被忽略，不属于用户主配置。

## 阶段 0 构建与自动测试

### 改动前

| 命令 | 结果 |
| --- | --- |
| `npm test` | PASS；`Vibe Flow V1.5.0 release validation passed.` |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，生成 `VoxDeckInputBridge.exe` |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，生成 `VibeMic.exe` |
| `VibeMic.exe --self-test` | exit 0 |
| `VoxDeckInputBridge.exe --self-test` | exit 0 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | exit 0 |
| `VibeMic.exe --ui-resource-test` | exit 0 |

### TDD 记录

1. 在 `scripts/validate.js` 增加“录音结束不得显示为已验证转写完成”的断言。
2. `npm test` 按预期失败：`Recording completion is still reported as verified transcription completion`。
3. 只修改 Host 反馈文案/状态映射，不改 Capture 或 Bridge。
4. `npm test` 再次 PASS。
5. 增加 README 不得把录音结束写成转写完成的断言；`npm test` 按预期失败后修正文案，再次 PASS。
6. 复核发现 `waiting` transient 状态会在下一次 UI timer 刷新落入默认分支；增加失败断言后，把持久状态映射到已有 `processing` 视觉状态，`npm test`、Host 构建/self-test 和资源压力再次 PASS。
7. auditor 指出上述源码字符串门禁不能执行会话分类，并发现冻结 Capture 的 `WETYPE SESSION END` 不含 Host 要求的 `input_target_ready`，真实成功路径会被误报为错误。
8. 在 `RunHostSelfTests()` 加入真实样本 `WETYPE SESSION END generation=1 audio_delivered=True submitted=True panel_wait_ms=50`；纯分类器空实现时 self-test 按预期失败：`Delivered WeChat session was not classified as waiting for visual confirmation`。
9. 最小实现 `audio_delivered=True -> waiting` 后 Host self-test PASS。
10. 再加入 `audio_delivered=False` 样本；self-test 按预期失败：`WeChat session without delivered audio was not classified as an error`。
11. 补齐 `audio_delivered=False -> error` 并让实际 `HandleRuntimeFeedbackLine()` 复用该纯函数；Host self-test 再次 PASS。未修改冻结 Capture。
12. auditor 复审指出无音频错误缺少影响、可能原因和恢复动作；为实际错误文案增加可执行断言，Host self-test 按预期失败：`Session failure feedback lacks impact, likely cause, or recovery action`。
13. 只补充“未确认语音工具收到音频 / 可能原因 / 重新按住重试 / 打开连接与自检”并让实际反馈路径复用同一纯文本函数；Host self-test 再次 PASS。
14. 最终复审发现 `validate.js` 仍只在旧方法区间查找已抽取文案，`npm test` 实际失败。修正门禁作用域，分别检查分类/文案纯函数与运行时调用关系，同时禁止运行时重新依赖 `input_target_ready`；`npm test` 再次 PASS。
15. 针对错误详情在窄控件可能截断，先加入短恢复摘要断言并观察 Host self-test 红灯，再实现 `未收到音频 · 请重新按住重试；仍失败请打开自检`，绿灯。
16. 针对错误只保留 3.2 秒，先加入至少 10 秒的策略断言并观察红灯，再把 waiting/error 反馈都保留 12 秒，绿灯。
17. 针对 waiting 活动文本被错误染成绿色，先加入 processing 必须为 warning 色义的策略断言并观察红灯，再让实际活动标签使用青色 warning 语义，绿灯。
18. auditor 继续追踪发现 `ShowToast()` 会在绘制前用短摘要覆盖完整 hero 详情；先加入会话 toast 不得镜像覆盖 hero 的可执行断言并观察红灯，再为 toast 增加显式 mirror/duration 参数：会话反馈不覆盖 hero，toast 与 transient 同步保留 12 秒，其他 toast 保持 2.8 秒。
19. 按 UI 规范把 processing/warning 活动标签从辅助青色调整为琥珀色；Host build/self-test 再次 PASS。
20. 使用最新构建 `VibeMic.exe --ui-resource-test` 重跑 300 次切页：阶段中间报告 USER +22 / GDI +16；正式 gate 再次重跑为 USER `114 -> 133`（+19）、GDI `35 -> 51`（+16），exit 0，最终报告时间 2026-09-03 23:51:30。

### 改动后最终复验

| 命令 | 结果 |
| --- | --- |
| `npm test` | PASS |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `VibeMic.exe --self-test` | exit 0 |
| `VoxDeckInputBridge.exe --self-test` | exit 0 |
| 冻结 Capture `--self-test` | exit 0 |
| `VibeMic.exe --ui-resource-test` | exit 0；最终报告 USER +19、GDI +16 |
| Capture 源码 SHA-256 | `736017A0...74137E2`，匹配 |
| Capture 二进制 SHA-256 | `B62DE035...E683`，匹配 |
| `git diff --check` | 无 whitespace error；只有 Git 的预期 LF/CRLF 提示 |

## 阶段 0 Computer Use 实际操作

- 构建并启动 `VibeMic.exe --ui-smoke`，使用独立 smoke 用户状态，不启动 Bridge/Capture，不修改真实用户设置。
- 精确选择工作区中的 `VibeMic.exe` 窗口，没有操作另一个位于工作区外的既有 VibeFlow 实例。
- 实际看到 1280 x 840 V1.5 首页：五页导航、VOICE LINK OFF、默认通用 Profile、启动语音桥接/检查连接、遥控器示意、常用按键、设备/音频/隐私状态和最近动作。
- 首次尝试进入“快捷键”页时 Computer Use 检测到用户并发输入；动作未执行。刷新时窗口已被最小化，因此没有抢回焦点，并安全关闭该 smoke 进程。
- 重建后再次启动 `--ui-smoke`：首页实际显示“松开结束录音”；随后实际进入快捷键、语音、自检、设置五页。
- 快捷键页显示录音键“固定稳定链路 / 按住听写 · 松开结束”，Smart Profiles 未开启；没有点击任何测试或配置动作。
- 语音页显示 hold 模式、稳定档 v11、1.0x 和高级参数锁定；没有启动语音桥接。
- 自检实际显示 2 项错误：工作区外另一个 VibeFlow/Capture/Bridge 实例，以及 smoke 模式未运行本目录 Bridge。页面没有伪造全绿；没有点击修复入口。
- 设置页可见启动/托盘/主题/反馈/来源保护/隐私入口；没有修改任何设置。
- 使用 Alt+F4 关闭本轮 smoke 窗口，并确认工作区 `VibeMic.exe` 没有剩余窗口。
- 最新布局复验时，Computer Use 的 `launch_app` 不支持 `--ui-smoke` 参数，曾误启动一次工作区普通模式；该模式按既有单实例逻辑启动了工作区 Bridge，并可能关闭了工作区外既有实例。发现后立即通过项目退出事件停止工作区 Host/Bridge，确认 Host/Bridge/Capture 均无残留；没有重启或访问工作区外路径。普通模式产生的未跟踪 `input-bridge-health.json` 已删除，ignored 日志/Bridge 配置仍列为构建运行产物。是否影响外部实例需用户自行确认。
- 随后以明确 `--ui-smoke` 参数启动唯一工作区窗口，并由 Computer Use 捕获 1280 x 840 首页。扩大后的副标题区域未覆盖状态标签、按钮、遥控器图或下一分区，首屏无可见重叠；关闭后无工作区进程残留。
- 未完成：真实录音后的“等待语音工具处理”动态状态；100–200% DPI；深色/跟随系统；首次设置完整交互。

## 阶段 0 explorer 发现

- HIGH：既有 Host 把录音停止/音频派发误报为转写和文字写入完成。已通过失败测试及外围文案修正处理。
- HIGH：原 `npm test` 只检查冻结 hash 字符串，未实际计算源码 hash。已新增实际 SHA-256 计算断言；冻结二进制由本轮独立实物验证和 resolver 保证。
- 稳定基线例外：现有 Bridge 已有全局低级键盘 Hook；V2 不得重写或扩大。
- MEDIUM：稳定音频参数是默认/推荐档，现有 UI 允许用户明确解锁排障。V2 不静默覆盖合法旧设置。
- MEDIUM：`SaveConfig()` 与 Bridge runtime ACK 可能分叉；V2 必须单独等待 ACK。
- MEDIUM：现有封闭 config 类型保存时不能保留未知顶层字段；V2 独立存储必须解决。
- INFO：Host 保留不可达旧长听写和 Legacy 向导方法；未按名称直接删除。
- INFO：`npm start` 指向不存在的 `scripts/open-ui.ps1`；当前开发入口为实际构建的 `VibeMic.exe`。

## 阶段 0 auditor 发现

- 第一轮结果：无 BLOCKER；1 个 HIGH、1 个 MEDIUM、3 个 LOW，另有真机检查项。
- HIGH：默认微信会话被 Host 对冻结 Capture 不存在的 `input_target_ready` 要求恒定误判为错误。已用真实日志样本的可执行 Host self-test 复现，并只修正 Host 外围分类。
- MEDIUM：原 `validate.js` 仅检查源码字符串，不能证明运行时反馈语义。已补可执行纯函数测试，并修正本文件此前对证据强度的表述。
- LOW：阶段 0 文件/回滚漏列 README。已补齐。
- LOW：ignored/generated 文件清单不完整。已通过 `git status --short --ignored --untracked-files=all` 清点并记录。
- LOW：基线 UI 记录与后续 smoke 时序不一致。`V2_0_BASELINE_LOCK_ZH.md` 已统一为阶段 0 最终巡视状态。
- 第二轮复审确认 BLOCKER/HIGH 为零、上一轮三个 LOW 已关闭；新增 MEDIUM 为无音频错误缺少影响、可能原因和恢复动作，已补可执行测试并修复。
- 第三轮复审发现 HIGH：抽取纯函数后 `validate.js` 断言范围过时，当前 `npm test` 失败；已修正测试作用域。另报告长错误文案可见性 MEDIUM 和 waiting 绿色成功态 LOW。
- 第四轮复审确认 HIGH 已关闭，但继续指出 toast 会覆盖完整 hero 详情、资源报告过期，以及 warning 应为琥珀色。已用可执行策略红绿测试修复调用链/停留时长/颜色，并用最新二进制重跑资源压力。
- 最终复审：BLOCKER 0、HIGH 0、MEDIUM 0、LOW 0；确认可进入阶段 0 release gate。

## 阶段 0 release gate 结果

阶段：0（基线与保护）  
最终结果：**PASS WITH MANUAL HARDWARE CHECKS**

### 范围与文件

- 阶段 tracked 修改：`README.md`、`scripts/VibeMic.cs`、`scripts/validate.js`。
- 阶段新增文档：`docs/V2_0_BASELINE_LOCK_ZH.md`、`docs/V2_0_IMPLEMENTATION_PLAN_ZH.md`、`docs/V2_0_UI_SPEC_ZH.md`、`docs/V2_0_PROGRESS.md`。
- 阶段开始前已有且未修改的未跟踪规则：`AGENTS.md`、`.agents/`、`.codex/`。
- ignored 构建/运行产物已在“修改文件”后的清单逐项归因；没有无关 tracked diff 或依赖变更。

### 构建与自动化

| 命令/证据 | Gate 结果 |
| --- | --- |
| `npm test` | PASS，exit 0 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0 |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0 |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| 冻结 Capture `--self-test` | PASS，exit 0 |
| `VibeMic.exe --ui-resource-test` | PASS，300 次切页；USER +19、GDI +16 |
| `git diff --check` | PASS；只有 Git 的 LF/CRLF 未来转换提示，无 whitespace error |
| Host / Bridge / Capture 版本 | `1.5.0.0` / `1.5.0.0` / `1.2.1.0` |
| Capture 源码 / 二进制 SHA-256 | `736017A0...74137E2` / `B62DE035...E683`，匹配 |

Host self-test 覆盖默认/既有配置、不完整合法值、schema 迁移、二次迁移幂等和原子替换；本阶段没有修改配置加载、保存或 ACK 路径。当前最终门禁没有失败测试；开发中的预期红灯与对应绿灯均记录在上方 TDD 记录。

### UI、冻结与回归

- Computer Use 实际巡视工作区构建的首页、快捷键、语音、自检、设置五页；最终静态首页为 1280 x 840，无可见重叠，录音键显示固定稳定链路，Smart Profiles 未开启，自检没有伪造全绿。
- 动态 waiting/error 没有用真实录音触发；纯分类/self-test 只证明 Host 反馈策略，不能替代真机。
- Capture、Bridge 源码、Raw Input、Hook、设备过滤、默认配置、安装器、构建脚本和预期 Capture hash 均无 tracked diff。
- hold-to-talk、`captureSeconds=0`、稳定音频参数、录音键隔离、六项输入时序、Smart Profiles 默认关闭均由自动门禁保持。
- 没有新增 MIC_EXTEND、自动续接、toggle 录音、转写读取/回填、自动 Enter 或第二套录音状态机。
- V2 功能尚未启用或绑定，阶段 0 的旧行为回退路径保持原状；运行时 ACK 分叉风险仍作为后续新代码约束保留。

### 未运行与限制

- 未运行 `BUILD_RELEASE.ps1`：工作区缺少 NAudio build dependencies，脚本会下载恢复；按权限边界没有自动下载。
- 未运行正式安装/升级/卸载生命周期，也没有候选包内 Capture 身份证据。
- 未完成 Win10/Win11、RC003、蓝牙、VB-CABLE、第三方语音工具、普通键盘隔离、睡眠恢复、100 次按住/松开、10/30/近 60 秒录音。
- 未完成 125%/150%/200% DPI、深色/跟随系统、1366 x 768/1920 x 1080、完整首次设置。
- Computer Use 误启一次普通模式，可能关闭工作区外既有实例；工作区 Host/Bridge/Capture 已退出，但外部实例状态未经授权未恢复或检查。

### 阻塞与限制判断

- Gate BLOCKER：无。
- auditor 最终未处理发现：无。
- 稳定候选/正式发布声明：不允许；阶段 0 仅完成基线保护。
- 所有硬件、安装和动态端到端项目继续标记“未验证”。

## 阶段 0 已修复问题

- 不再把 `REMOTE STREAM STOP` 显示为“正在整理并回填文字”。
- 不再依据 `audio_delivered`/`input_target_ready` 显示“听写已完成 / 文字已写入 / 转写完成”。
- 首页流程改为“松开结束录音”。
- 录音结束状态现在提示等待第三方语音工具并由用户目视确认最终文字。
- 冻结 Capture 的 `WETYPE SESSION END audio_delivered=True` 现在进入等待工具的 warning/processing 视觉状态，不再因不存在的 `input_target_ready` 字段进入错误状态。
- `audio_delivered=False` 会明确说明未确认语音工具收到音频、可能原因、重试方法和自检入口。
- 长错误详情在首页两行区域保留，toast/活动区使用短恢复摘要；error/waiting toast 与活动反馈均保留 12 秒，warning 使用琥珀色且不会覆盖完整详情。
- 自动门禁开始实际计算冻结 Capture 源码 SHA-256。

## 阶段 0 仍需真机验证

- RC003 100 次按住/松开；10/30/接近 60 秒真实音频。
- 蓝牙/遥控器休眠唤醒、断连重连、Windows 睡眠锁屏恢复。
- VB-CABLE 真实端点、路由和恢复。
- 微信输入法与至少一个其他工具端到端。
- 普通键盘 F5 与 RC003 隔离。
- Win10/Win11 干净安装、升级、卸载。
- 100/125/150/200% DPI、两种分辨率、三种主题。

## 阶段 1：UI 基础与模块化

### 用户目标与最小完成标准

- [x] 左侧导航统一为：首页、项目、按键、语音、自检、设置。
- [x] WinForms Host 使用 partial 增量模块化，构建脚本显式编译新增源文件。
- [x] 导航使用可滚动 `FlowLayoutPanel`，避免第六项与底部连接状态重叠。
- [x] 项目页提供无执行副作用的安全空状态；本阶段不创建、保存或运行 Project Space。
- [x] 首页、按键、语音、自检、设置继续调用原有业务页面和事件处理器。
- [x] 资源测试真实构建六页并断言页面索引、导航数量、内容和标题。
- [x] 真实构建已启动，并用 Computer Use 逐页检查。
- [x] auditor 没有 BLOCKER/HIGH；release gate 已完成。

### 实际加载与调查路径

- 实际加载：`AGENTS.md`、`.agents/skills/vibeflow-release-gate/SKILL.md`、`.codex/agents/vibeflow-auditor.toml`、executing-plans、test-driven-development、verification-before-completion、computer-use 及其 Windows 控制/确认规则。
- explorer 只读调查：`VibeMicForm` 构造与主题重建、`BuildShell()`、`ShowPage(int)`、`DisposePageControls()`、`RunPageResourceTest()`、五个旧页面构建入口、`CreateNavigationIcon()`、`BUILD_VIBE_MIC.cmd`、`scripts/validate.js`、`scripts/capture-ui-screenshots.ps1`。
- 稳定边界：旧五页事件处理器继续留在 `scripts/VibeMic.cs`；没有迁移或改写 Capture、Bridge、Raw Input、Hook、设备过滤、稳定手势、配置保存/迁移或 revision ACK。

### 修改文件

| 文件 | 阶段 1 目的 |
| --- | --- |
| `scripts/ui/DesignTokens.cs` | 六页稳定 ID 与 shell 尺寸/间距 token |
| `scripts/ui/PageShell.cs` | 六页导航元数据、显式页面分派、资源测试标题识别 |
| `scripts/ui/UiComponents.cs` | DPI 布局容器构成的可访问空状态卡 |
| `scripts/ui/ProjectsPage.cs` | 无副作用项目空状态；创建按钮保持 disabled |
| `scripts/VibeMic.cs` | partial 声明、六页索引、可滚动导航、项目图标、六页资源断言；按键页标题与导航一致 |
| `BUILD_VIBE_MIC.cmd` | 显式编译四个新增 UI 源文件 |
| `scripts/validate.js` | 六页、模块、构建输入、项目页副作用和截图脚本门禁 |
| `scripts/capture-ui-screenshots.ps1` | 增加“项目”并将旧“快捷键”导航定位更新为“按键”；保留旧图片文件名 |

本阶段没有修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、`vibe-mic-config.default.json`、Capture resolver/构建脚本、安装器或配置 schema。

### TDD 与自动化证据

- RED 1：新增阶段 1 静态门禁后，`npm test` 因 `scripts/ui/DesignTokens.cs` 不存在而按预期失败。
- GREEN 1：实现四个 UI 模块、六页 shell、构建与截图脚本后，`npm test` 通过。
- RED 2：Computer Use 发现导航“按键”与页面“快捷键”不一致、项目文案偏内部化；新增门禁后 `npm test` 按预期失败。
- GREEN 2：页面标题统一为“按键”，项目文案改为用户视角；`npm test` 通过。

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS，exit 0 |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，显式编译主文件与四个 UI 模块 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS；仅做冻结回归构建，无 Bridge 源码 diff |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| 本机冻结 Capture `--self-test` | PASS，exit 0 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页循环，USER `+22`、GDI `+37` |
| `git diff --check` | PASS；只有预期 LF/CRLF 提示 |

### Computer Use 实际操作

- 通过 PowerShell 以明确参数启动工作区 `VibeMic.exe --ui-smoke`，精确选择 app id 为工作区绝对路径的唯一窗口；没有使用 `launch_app`，没有操作工作区外实例。
- 实际点击首页、项目、按键、语音、自检、设置六页；六项导航在当前窗口同时可见，无重叠。
- 首页旧状态与操作保持；项目页显示安全空状态，`创建项目现场` 在可访问性树中为 disabled，未启动应用、网址或配置写入。
- 按键页显示录音键固定稳定链路、Smart Profiles 未开启；语音页保持 hold、稳定档 v11、1.0x；自检在 smoke 环境真实显示 2 项错误，没有伪造全绿；设置页仍可达。
- 首轮检查发现按键页标题仍为“快捷键”、项目文案偏内部化；修复后只复验项目页与按键页，截图和可访问性文本均与预期一致。
- 关闭测试窗口后确认工作区 Host 没有残留。没有点击任何配置、测试、修复、语音桥接或系统设置动作。

### auditor 与 release gate

- auditor：BLOCKER 0、HIGH 0、LOW 0；1 个 MEDIUM 为阶段 1 证据尚未写入本文件，现已关闭。
- auditor 确认冻结 Capture/Bridge/默认配置/Raw Input/Hook/设备过滤/录音按钮路径无 tracked diff，建议进入 gate。
- `vibeflow-release-gate` 最终结果：**PASS WITH MANUAL HARDWARE CHECKS**。
- Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- Capture 二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`；文件版本 `1.2.1.0`。
- 输入时序继续为 650/420/80/500/250/350 ms；没有新增 MIC_EXTEND、自动续接、toggle、自动 Enter、转写读取/回填或第二套录音状态机。
- 新项目页无配置、后台任务、外部启动或按键绑定；关闭/未实现 V2 业务时，旧五页与原配置路径保持。

### 已修复问题与未验证项

- 已修复：第六项导航的低高度结构性重叠风险，导航改为独立可滚动流式容器。
- 已修复：项目页/按键页可达性、标题一致性、项目空状态文案和页面显式分派。
- 未验证：125%/150%/200% DPI、1366 x 768/1920 x 1080、最小窗口、深色/跟随系统、完整键盘导航。
- 未验证：真实 RC003、普通键盘隔离、按住/松开 generation、10/30/近 60 秒音频、蓝牙/睡眠恢复、VB-CABLE、第三方输入法。
- 未验证：Win10/Win11 安装、V1.5 升级配置保留、卸载和首次设置；本阶段相关源码无 diff。
- 不在本阶段：Live HUD、Context Deck、Smart Focus、Project Space 存储/执行、Capture & Ask、Browser Remote Lite。

## 阶段 2：统一反馈、Live HUD、Context Deck 与自检

### 用户目标与最小完成标准

- [x] 建立 `idle/checking/running/success/warning/error/canceled` 统一结果模型；warning/canceled 不得显示为 success。
- [x] 页面、toast、日志和只读 Deck 可消费同一份脱敏状态快照；错误回执包含原因、影响、恢复入口和错误码。
- [x] 实现只读 Context Deck；它不发送按键、不启动 Bridge/Capture、不建立第二套输入状态机。
- [x] 保留已有自检项目，并增加 V2 场景占位/证据结构。
- [ ] Live HUD 生产入口与实际非激活显示未完成。连续三种非激活显示尝试均未得到可靠可见窗口，已按三次失败规则停止扩大修改；当前首页和托盘明确显示“暂不可用”。
- [x] auditor 的所有 HIGH 已处理并由第三次只读复核确认关闭。
- [x] `$vibeflow-release-gate` 已执行；由于 Live HUD 是本阶段核心交付且不可达，最终结果为 `FAIL`。

### 实际调查路径与稳定边界

- explorer 只读调查：`ShowToast()`、`SetSessionFeedback()`、`HandleRuntimeFeedbackLine()`、`PollActivity()`、`ReadKeyboardBridgeHealth()`、`PollKeyboardBridgeHealth()`、`BuildSelfCheckReport()`、`ShowSetupWizard()`、`BuildShell()`、首页/托盘入口、`StartCapture()` 与 Bridge revision ACK 调用链。
- 新 UI 路径：`scripts/features/ActionResult.cs`、`scripts/ui/LiveHudForm.cs`、`scripts/ui/ContextDeckForm.cs`、`scripts/ui/PageShell.cs`、`scripts/ui/UiComponents.cs`。
- 稳定边界保持：没有修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、Raw Input、Hook、设备过滤、稳定手势状态机、默认配置或安装器。
- Host 仅增加当前已启动 Capture 的 provider/hotkey/trigger 身份记录，用于首次设置证据归属；不改变 Capture 参数、generation、启动时序或停止时序。附着到既有 Capture 时身份保持未知并 fail-closed。

### 修改文件

| 文件 | 阶段 2 目的 |
| --- | --- |
| `scripts/features/ActionResult.cs` | 七态 ActionResult、配置保存/ACK 结果、错误恢复详情 |
| `scripts/ui/LiveHudForm.cs` | 非激活 HUD 窗口结构；当前无生产调用入口 |
| `scripts/ui/ContextDeckForm.cs` | 只读当前上下文、设备、语音、Profile、目标和实体键动作 |
| `scripts/VibeMic.cs` | 状态快照/回执接入、Deck 入口、资源测试、五任务向导严格证据与 ACK、Bridge revision fail-closed |
| `BUILD_VIBE_MIC.cmd` | 显式编译阶段 2 新模块 |
| `scripts/validate.js` | 七态、HUD/Deck 输入隔离、生产入口、隐私与构建门禁 |
| `docs/V2_0_PROGRESS.md` | 阶段调查、验证、审计、门禁和未验证项 |

### TDD 与已修复问题

1. 七态映射先红后绿：warning/canceled 不再被 `IsSuccess` 或 UI 颜色映射成 success。
2. Bridge 动作回执游标先红后绿：允许 Bridge PID 迁移后的新序列 `pid=0, seq=100 -> pid=202, seq=1`，同时拒绝旧 PID/旧序列和无所有权证据。
3. 五任务向导 ACK 先红后绿：完成要求新鲜 health、running、Hook、Raw Input、当前保存产生的非空 revision、空 `config_error` 和当前 owned PID；缺少 ACK 时 `setupCompleted=false` 且向导不关闭。
4. provider 保存事务先红后绿：保存失败重新加载持久配置且不重启 Capture；重启反馈只写“正在等待连接”或明确失败，不写“已生效”。
5. Capture 启动异常先红后绿：未启动/已释放的 `Process` 不再让 `IsCapturing` 抛异常，失败临时对象被清理，真实运行对象保留。
6. provider 证据身份先红后绿：最终通过必须满足“当前 UI 配置 = 当前 Capture 运行时配置 = 文字证据配置”，且 generation 新于测试基线、音频成功、用户确认文字。旧运行时 A 产生的新 generation 不能归属到新 UI 配置 B。
7. Bridge 空 revision 先红后绿：`BridgeConfigurationMatchesExpected()` 要求 actual/expected 均非空且一致；健康轮询与自检统一 fail-closed，不再把同步失败后的空 revision 当作无需确认。
8. Live HUD 经过三次有限非激活显示尝试仍无法在真实窗口中可靠出现；没有改 Capture/Bridge 绕过，也没有继续第四种实验。生产入口保持禁用，避免伪装完成。

### 构建与自动测试

| 命令/证据 | 最终结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0；Bridge 源码无 diff |
| `npm test` | PASS，exit 0；`Vibe Flow V1.5.0 release validation passed.` |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `118 -> 139`（+21），GDI `36 -> 79`（+43），门限 120/50 |
| `git diff --check` | PASS；只有预期 LF/CRLF 提示 |
| Capture 源码 SHA-256 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，匹配 |
| Capture 二进制 SHA-256 / 版本 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` / `1.2.1.0`，匹配 |

资源测试通过但 GDI 只剩 7 个计数的门限余量；后续 UI 阶段继续每阶段重跑并观察趋势。

### Computer Use 实际操作

- 构建并以明确参数启动工作区 `VibeMic.exe --ui-smoke`，精确选择该绝对路径返回的唯一窗口；smoke 配置不启动 Bridge/Capture，不修改真实用户配置。
- 实际看到首页六页导航、真实 `VOICE LINK OFF`、通用 Profile、固定 hold-to-talk 说明、只读状态区和禁用的“Live HUD 暂不可用”；没有伪造就绪状态。
- 实际进入项目页，看到安全空状态；`创建项目现场` 在可访问性树中为 disabled，没有运行阶段 4 功能。
- 实际从首页打开 Context Deck：窗口显示当前应用、Profile、项目、语音目标、设备/语音状态，以及录音/Home/方向/TV/功能/确认键的实际动作；关闭后主窗口恢复焦点且无残留 Deck。
- 实际进入设置页、滚动到维护区并打开首次设置；屏幕中只有一套 5 任务流程，任务 1 显示按住、持续说话、松开、检查文字后确认的固定边界。
- 没有点击启动语音桥接、VB-CABLE、蓝牙、系统设置、配置导入/导出或任何硬件测试动作。测试实例关闭后工作区 `VibeMic` 进程无残留。
- 较早一次 Computer Use 复验受到并发用户输入中断；本次最终复验成功覆盖上述子流程，但不能替代未执行的硬件与 DPI 流程。

### auditor 结果

- 首轮审计发现 4 个 HIGH：Bridge PID 迁移游标、向导 ACK 证据不足、provider 保存事务、Capture 启动失败对象；均已修复。
- 二次审计新增 2 个 HIGH：旧 Capture 运行时证据可归属新 provider 配置、Bridge 同步失败后的空 revision 可显示为已确认；均用失败测试复现并修复。
- 第三次只读复核：上述 HIGH 全部关闭，没有新 HIGH。
- 未关闭 BLOCKER：Live HUD 无生产入口，`ShowInactive()` 无调用，首页和托盘入口禁用。
- 已知 MEDIUM：任务 4 在 `bridgeReady` 前即可提示开始说话；VB-CABLE 安装启动失败/UAC 取消仍可能收到乐观恢复文案；`persistProgress` 忽略保存失败；重开向导把 `bridgeChoice`/`trayChoice` 初始化为 true，可能覆盖用户关闭选择。
- 另有阶段性结构限制：旧 toast 错误元数据不完整，自检尚无独立“影响”字段；后续只在对应功能阶段做有测试的增量修正。

### `$vibeflow-release-gate` 结果

阶段：2（统一反馈、Live HUD、Context Deck 与自检）  
最终结果：**FAIL**

- 冻结契约：自动证据 PASS；Capture/Bridge/默认配置/安装器无 diff，版本、两项 hash、hold 参数、六项输入时序匹配。
- 配置兼容：Host self-test 覆盖 clean/existing/malformed/partial config、二次迁移幂等；本阶段新增 ACK 和 provider 证据测试 PASS。真实升级仍未运行。
- 构建与自动化：上述命令全部 PASS；没有失败自动测试。
- UI：主界面、项目占位、设置、Context Deck 和唯一 5 任务向导入口已有实际 UI 证据；Live HUD 未通过，DPI/主题矩阵未运行。
- 回归：smoke 模式中 Bridge/Capture 保持关闭；录音键未进入普通映射、Deck 或反馈动作。真实遥控器/普通键盘回归仍未验证。
- BLOCKER：Live HUD 核心交付不可达，因此不能将阶段 2 标记 PASS，也不能声称 V2.0 候选完成。

### 仍需真机/人工验证

- Live HUD 可靠可见且不抢焦点；当前实现不可用。
- Deck 在真实遥控器输入下只高亮一次、不造成双重动作；录音开始时不抢焦点。
- 真实 Bridge revision ACK、同步失败恢复和旧配置保留。
- RC003、蓝牙、VB-CABLE、微信输入法及至少一个其他语音工具；100 次按住/松开、10/30/近 60 秒音频、断连/睡眠恢复。
- 100/125/150/200% DPI、1366 x 768/1920 x 1080、浅色/深色/跟随系统、完整键盘导航。
- Win10/Win11 安装、V1.5 升级、卸载、重启恢复和唯一首次设置完整路径。

## 阶段 3：Smart Focus

### 用户目标与最小完成标准

- [x] 使用独立、版本化的 `focus-targets.json` 保存输入目标，不进入 Capture 配置。
- [x] 保存前必须完成一次明确的“立即测试”；未验证目标不能从首页生产入口执行。
- [x] 只接受所选进程中可验证为可写的 UI Automation `Edit` 控件。
- [x] 激活前跨同名进程和所有可见顶层窗口检查目标唯一性；歧义或扫描不完整时 fail closed。
- [x] 聚焦后复核描述符、可写性、键盘焦点和选定窗口归属，再显示锁定成功。
- [x] 同一时间只运行一个请求；超时、取消和录音优先有明确状态与错误码。
- [x] 首页、项目、按键和 Context Deck 使用同一目标状态；Smart Focus 不派发按键、不读取文字或剪贴板。
- [x] 构建、自动测试、真实 UI smoke、auditor 和 release gate 已执行。

### 实际加载与调查路径

- 实际加载：`AGENTS.md`、`.agents/skills/vibeflow-release-gate/SKILL.md`、`.codex/agents/vibeflow-auditor.toml`、test-driven-development、systematic-debugging、verification-before-completion、computer-use 及其 Windows 控制/确认规则。
- explorer 只读调查：`VibeMicForm` 用户数据根和原子保存、首页/项目/按键入口、Context Deck 快照、`StartCapture()` 与 `Local\\VibeMicRecordingStartCue` 消费点、UI Automation 系统程序集、构建脚本和 Host 自测接缝。
- UIA 生产路径：`FocusTargetDialog` 学习 -> `WindowsUiaFocusAutomationBackend.CaptureFocusedEditableTarget()` -> `FocusTargetService.ExecuteForVerification()` -> `ActivateApplication()` -> `FocusAndVerify()` -> 保存；首页生产入口只执行已有默认且已验证目标。
- 录音优先通过 Host 现有 recording cue 消费点调用 `CancelForRecording()`；没有为 Capture 事件新增第二个等待者，也没有修改 recording generation。
- 稳定边界：没有修改 Capture、Bridge、Raw Input、Hook、设备过滤、输入注入隔离、稳定手势状态机或录音按键映射。

### 修改文件

| 文件 | 阶段 3 目的 |
| --- | --- |
| `scripts/features/FocusTargetModels.cs` | 目标描述符、验证规则、未来 schema 的不可执行边界 |
| `scripts/features/FocusTargetStore.cs` | 独立 schema、原子写入、备份恢复、幂等迁移、未知字段保留和敏感字段过滤 |
| `scripts/features/FocusTargetService.cs` | 单请求执行、取消/超时/录音优先、跨窗口唯一性、真实聚焦和最终验证 |
| `scripts/ui/FocusTargetDialog.cs` | 应用选择、非激活学习提示、稳定采样、立即测试、保存/重学/删除和关闭竞态保护 |
| `scripts/tests/FocusTargetSmokeApp.cs` | 真实 UI Automation 可写输入控件夹具 |
| `scripts/tests/FocusTargetMultiWindowTests.cs` | 两个同名进程和两个顶层窗口的 fail-closed 集成测试 |
| `scripts/VibeMic.cs` | Store/Service 生命周期、首页和按键入口、录音取消、状态回执、Host 自测 |
| `scripts/ui/ProjectsPage.cs` | 项目页共享输入目标设置入口；未提前实现 Project Space 执行 |
| `scripts/ui/ContextDeckForm.cs`、`scripts/ui/LiveHudForm.cs` | 只读快照显示当前目标；不增加输入执行能力 |
| `BUILD_VIBE_MIC.cmd` | 编译 Focus 模块并引用 Windows UI Automation 系统程序集 |
| `scripts/validate.js` | Smart Focus 构建输入、隐私、输入隔离和冻结契约门禁 |

本阶段未修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、`vibe-mic-config.default.json`、安装器或 Capture hash 解析器。没有下载依赖、修改系统音频/注册表/驱动、防火墙、启动项、Push、Release 或签名。

### TDD 与明确修复记录

1. Focus Store 自测覆盖 clean/existing/malformed/backup/future schema、二次迁移幂等、原子替换、未知字段保留和隐私字段过滤；生产执行对未来 schema fail closed。
2. 未验证目标生产执行测试先失败后通过：只有明确“立即测试”允许执行待保存目标，首页入口要求历史验证证据并在执行时再次真实验证。
3. `TextPattern` 只读旁路测试先暴露错误，随后收紧为必须存在 `ValuePattern` 且 `IsReadOnly=false`；真实 WinForms 只读 TextBox 和 TextPattern-only 证据均被拒绝。
4. 双窗口集成测试在旧实现上暴露“首个窗口即返回”，修复后启动两个相同夹具进程并得到 `FOCUS-TARGET-AMBIGUOUS`。
5. auditor 的最终焦点/截断/关闭竞态三个 HIGH 先加入 Host 红测：构建因缺少四参数唯一性策略、最终四项验证和安全 UI 投递接口而按预期失败；最小实现后 Host build/self-test 转绿。
6. UIA 扫描不再静默截断 2500 个元素；必须完成 `elements.Count` 扫描，否则返回 `FOCUS-TIMEOUT`，不能使用部分结果宣称唯一。
7. 最终成功要求描述符匹配、可写、属于选定 HWND 且真实获得键盘焦点；录音取消在前置窗口和 `SetFocus()` 前重复检查。
8. Dialog 学习/测试回调和 Host 首页完成回调统一安全处理窗口关闭竞态；已销毁句柄不再排队或抛出未捕获 `BeginInvoke` 异常。

开发中另有两次非产品失败已查明并纠正：首次独立测试命令漏传 fixture 参数；首次重编测试夹具漏纳入 `FocusTargetStore.cs`。两者均在进入产品断言前失败，不计为产品回归，最终命令已使用完整参数和源码集合。

### 构建与自动测试

| 命令/证据 | 最终结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0；最新 Host 晚于 Host/Focus 源码 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0；Bridge 源码无 diff |
| `npm test` | PASS，exit 0；`Vibe Flow V1.5.0 release validation passed.` |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| `%TEMP%\\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0 |
| 最新源码重编 `FocusTargetMultiWindowTests.exe` 并传入 `FocusTargetSmokeApp.exe` | PASS，两个真实进程/顶层窗口，exit 0 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `120 -> 143`（+23），GDI `36 -> 79`（+43），`50539 ms` |
| `git diff --check` | PASS；只有 Git 的 LF/CRLF 未来转换提示，无 whitespace error |
| Capture 源码 SHA-256 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，匹配 |
| Capture 二进制 SHA-256 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，匹配 |

Host self-test 同时覆盖目标描述符验证、进程不匹配、应用不存在、目标失效、歧义、超时、取消、单请求、录音优先、日志脱敏、只读控件、未来 schema 和关闭句柄回调。独立双窗口测试尚未接入 `npm test`，必须在后续相关阶段继续显式运行。

### Computer Use 实际操作

- 使用工作区真实 `VibeMic.exe --ui-smoke`，按绝对路径精确选择唯一窗口；smoke 数据只在仓库 `tmp/ui-smoke`，没有读取或改写 `%LOCALAPPDATA%` 的真实 Focus 配置。
- 缺失目标进程的真实执行在日志中为 `elapsed_ms=6`，页面约 0.6 秒显示“未锁定输入目标，未发送按键”；Context Deck 完整显示原因、修复动作和 `FOCUS-APP-NOT-RUNNING`。
- 对未来 schema 配置，目标仍可见，但立即测试、重新学习、删除、学习和保存均禁用；页面显示 `FOCUS-SCHEMA-NEWER`，未把未知 schema 当作可执行配置。
- 最新构建再次打开 Smart Focus 对话框，实际看到空目标列表、应用选择、学习入口、隐私边界和禁用保存；关闭后 Host 继续运行，焦点返回首页“输入目标”。
- 修复过一次 Context Deck 底部裁切；本轮关闭所有工作区测试实例，未残留 `VibeMic.exe`。
- 没有对 Cursor、VS Code 或真实用户应用输入框执行学习/聚焦；没有输入、粘贴或发送任何用户文字。

### auditor 结果

- 最终复审：Phase 3 自身 BLOCKER 0、HIGH 0，可进入 release gate。
- 已关闭 HIGH：TextPattern-only 只读控件；跨所有同名进程/顶层窗口唯一性；最终四项焦点验证；2500 元素静默截断；Dialog 两处和 Host 一处关闭竞态。
- 保留 MEDIUM：第三方 UIA provider 的同步 COM 调用无法被硬中止；成功验证时间按 ID 回写且保存失败反馈不足；主配置与备份同时损坏时缺少重建入口；Smart Focus 对话框深色/高 DPI 小工作区证据不足；双窗口测试未接入 `npm test`。
- 保留 LOW：全局唯一性是激活前快照；`EnumWindows` 返回值未检查；“已验证”表示历史时间而不是当前在线状态。
- 继承 BLOCKER：Phase 2 Live HUD 无生产入口。按三次失败规则不进行第四次绕行尝试；它不是 Phase 3 新回归，但继续阻塞整体 V2 候选。

### `$vibeflow-release-gate` 结果

阶段：3（Smart Focus）  
最终结果：**PASS WITH MANUAL HARDWARE CHECKS**

- 范围：阶段文件均服务于 Focus 模型、Store、UIA 执行、共享入口、状态快照或测试；未发现依赖安装、仓库级格式化或无关生成文件。`AGENTS.md`、`.agents/`、`.codex/` 为此前已存在的项目规则文件，不归因于 Smart Focus 产品实现。
- 冻结录音：Capture 源码/二进制 hash 匹配，Capture/Bridge/default config/installer 无 diff；hold-to-talk、`captureSeconds=0`、稳定音频参数和 device-controlled 约 60 秒边界未改。
- 输入保护：六项时序仍为 `650/420/80/500/250/350`；Smart Focus 文件不含按键派发、剪贴板读取、Raw Input/Hook/过滤或录音键映射。
- 配置兼容：Focus 数据独立保存，Host 自测覆盖原子保存、备份恢复、迁移幂等、未知字段和未来 schema；现有语音、Mappings、Profiles 与 Smart Profile 状态路径未迁移。
- 实现证据：Host/Bridge 构建、现有回归、三项 self-test、Focus 双窗口集成和 UI 资源测试均有 exit 0 证据；`git diff --check` 无错误。
- UI：实际检查了缺失应用、未来 schema、空配置、打开/关闭 Dialog 和 Deck 错误修复入口；没有观察到伪造“AI 已收到”或“转写完成”。
- 功能关闭/未配置回退：smoke 模式无默认目标、Bridge/Capture 均未启动；Smart Focus 不自动学习、不绑定按键、不启用 Smart Profiles、不执行后台焦点工作。
- Gate 阻塞：Phase 3 自身无；整体 V2 仍因 Phase 2 Live HUD 为 **FAIL**，不能声明候选完成。

### 已修复问题

- 目标应用不存在时立即失败，不再耗尽完整超时。
- 只读控件、TextPattern-only 控件、class-only 描述符、未来 schema 和未验证目标全部 fail closed。
- 同一窗口或跨进程/跨窗口多个匹配目标均不再选择第一个执行。
- 最终焦点不在选定窗口、控件失去可写性或未获得键盘焦点时不再报告锁定成功。
- UIA 扫描未完成时不再用部分结果报告唯一成功。
- 录音取消后不再恢复/激活 Host 或 Dialog；焦点动作前增加取消检查。
- Dialog/Host 关闭期间的后台回调不再存在未捕获 `BeginInvoke` 退出竞态。

### 仍需真机/人工验证

- Cursor 和 VS Code 的真实输入控件学习、立即测试、多窗口和连续 50 次正确目标执行；当前不能标记为“已验证适配器”。
- 真实 RC003 在 UIA 扫描、应用激活和 `SetFocus()` 各阶段开始录音时是否立即优先；同步 provider 阻塞仍是已知限制。
- 普通键盘、遥控器映射、Smart Profiles、Hook/Raw Input 和修饰键清理回归。
- 100%/125%/150%/200% DPI、1366 x 768/1920 x 1080、浅色/深色/跟随系统和完整键盘导航。
- Win10/Win11 安装升级、V1.5 配置保留、首次设置、VB-CABLE、蓝牙和第三方语音工具。
- 主 Focus 配置与备份同时损坏后的用户可见修复入口。

## 阶段 4：Project Spaces

### 用户目标与最小完成标准

- 用户可以通过引导式 5 步向导创建、编辑、复制、停用和删除 Project Space。
- Project Space 只执行受约束的线性步骤：打开/激活应用、打开已验证 Workspace、打开 URL、切换 Profile、锁定 Focus Target 和显示真实结果。
- 不接受任意命令或附加参数；默认失败即停，取消只阻止后续步骤；录音开始后停止后续可能抢焦点的步骤。
- Project Space 使用独立版本化配置，具备原子保存、备份恢复、迁移幂等、未来 schema 只读和并发冲突检测。
- Smart Profiles 开启时只更新回退 Profile；不改锁定的 active Profile，不复制或覆盖映射。
- [x] Host/Bridge 构建及现有、新增自动测试通过。
- [x] 真实程序中的项目列表、5 步向导、执行预览、执行结果和取消路径已检查。
- [x] auditor 确认阶段 4 自身 BLOCKER 0、HIGH 0。
- [x] `$vibeflow-release-gate` 已执行。

### 调查过的真实路径

- `scripts/VibeMic.cs`：Host 启动、配置保存与 Bridge revision ACK、Profile 状态、录音优先 epoch、首页项目入口、唯一可达 5 任务首次设置。
- `scripts/ui/ProjectsPage.cs`：项目列表、编辑/复制/停用/删除、执行状态、取消、默认 Focus 回写。
- `scripts/ui/ProjectSpaceWizard.cs`：5 步引导、资源选择、执行预览和保存边界。
- `scripts/features/ProjectSpaceModels.cs`：Project Space 模型、URL/路径/应用验证、执行快照。
- `scripts/features/ProjectSpaceStore.cs`：独立 schema、原子替换、备份恢复、CAS 冲突和未来 schema。
- `scripts/features/ProjectSpaceRunner.cs`：受约束步骤、单实例、失败停止、取消和录音优先。
- `scripts/features/FocusTargetStore.cs`、`FocusTargetModels.cs`：Project Space 关联目标的只读加载、稳定 ID 和 future schema 边界。
- `scripts/VoxDeckInputBridge.cs`：仅只读核对 Profile revision ACK 与六项冻结时序；没有修改。

explorer 在实施前确认：现有 Host 已有应用启动、Profile 保存/ACK、Focus 执行与用户状态根目录；Project Space 应复用这些边界，不应接入 Raw Input、Hook、Capture 或普通录音映射。

### 修改文件

| 文件 | 阶段 4 目的 |
| --- | --- |
| `scripts/features/ProjectSpaceModels.cs` | Project Space、运行快照和受约束资源验证 |
| `scripts/features/ProjectSpaceStore.cs` | 独立配置、原子保存、备份恢复、迁移、CAS 和 fail-closed 加载 |
| `scripts/features/ProjectSpaceRunner.cs` | 线性执行、真实逐步回执、取消、失败停止和录音优先 |
| `scripts/ui/ProjectsPage.cs` | 项目卡片、操作入口、执行状态与安全反馈 |
| `scripts/ui/ProjectSpaceWizard.cs` | 5 步创建/编辑向导与执行预览 |
| `scripts/VibeMic.cs` | 项目页接线、首页入口、Profile ACK、录音优先、自测及首次设置偏好保护 |
| `scripts/features/FocusTargetModels.cs`、`FocusTargetStore.cs` | 关联目标的 future schema 保留与原始长度校验 |
| `BUILD_VIBE_MIC.cmd`、`scripts/validate.js` | 编译新增模块并锁定项目边界 |

未修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、`vibe-mic-config.default.json`、安装器或冻结 hash 期望值。

### 实现与修复记录

1. 建立版本化 `project-spaces.json`，原子替换前保留有效备份；主文件缺失或损坏时读取 `.bak`。
2. 同路径 store 共享锁并使用加载指纹做 CAS；陈旧编辑返回 `PROJECT-STORE-CONFLICT`，临时文件名唯一。
3. schema 缺失或非法时拒绝主文件；显式 schema 0 迁移幂等；未来 schema 只读显示但禁止保存和执行。
4. 当前 schema 拒绝重复/空稳定 ID；应用或 Workspace 暂时离线不会被误判为整份配置损坏。
5. Project/Focus 加载改为先验证原始稳定 ID/描述符长度，再规范化；65/161/513 字符越界值不能被截成可执行配置。
6. future-schema Focus 保留 `DefaultTargetId` 原值但 `DefaultTarget()` 继续返回 null，满足“只读保留、禁止执行”。
7. Project Space 使用配置快照执行；同一时间只允许一个运行实例；取消和录音 epoch 会阻止后续步骤。
8. 应用激活在 `ShowWindow` 和 `SetForegroundWindow` 前分别复核录音优先级。
9. Smart Profiles 开启时仅保存 `smartProfileFallbackId`；关闭时才按既有 ACK 流程切换 active Profile，保存/ACK 失败会回滚。
10. 默认 Focus 验证时间保存失败不再沿用成功回执，改为 warning 并给出修复入口。
11. 真实可达 5 任务向导从现有配置初始化 Bridge 自启和托盘选项；从设置页重开不会把 false 静默改成 true。

### 构建与自动测试

| 命令/证据 | 最终结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0；证据 `tmp/phase4-host-build.txt` |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0；证据 `tmp/phase4-bridge-build.txt` |
| `npm test` | PASS，exit 0；证据 `tmp/phase4-npm-test.txt` |
| `VibeMic.exe --self-test` | PASS，exit 0；证据 `tmp/phase4-host-self-test.txt` |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0；证据 `tmp/phase4-bridge-self-test.txt` |
| `%TEMP%\\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0；证据 `tmp/phase4-capture-self-test.txt` |
| 重编并运行 `FocusTargetMultiWindowTests.exe` | PASS，exit 0；证据 `tmp/phase4-focus-multiwindow-test.txt` |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `125 -> 148`（+23），GDI `36 -> 79`（+43），`54517 ms` |
| `git diff --check` | PASS；只有 LF/CRLF 未来转换提示，无 whitespace error |
| Capture 源码 SHA-256 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，匹配 |
| Capture 二进制 SHA-256 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，匹配；文件版本 `1.2.1.0` |

Host self-test 覆盖 clean/existing/missing/malformed/backup/future schema、迁移二次幂等、未知字段、恢复后备份、CAS、URL 白名单、路径/参数约束、单实例、失败停止、取消、执行快照、录音优先、Profile ACK/回滚、Smart Profile fallback 和不虚报外部完成状态。所有修复均先观察到新增断言失败，再取得最终 PASS。

### Computer Use 实际操作

- 启动真实工作区 `VibeMic.exe --ui-smoke`，只使用仓库 `tmp/ui-smoke` 状态，不启动 Bridge/Capture，不读取或修改 `%LOCALAPPDATA%` 用户配置。
- 项目页实际显示项目卡片、编辑/复制/停用/删除、进入和取消执行入口；执行后实际显示快照 ID、逐步状态、耗时和失败即停结果。
- 实际逐步检查创建/编辑向导第 1 至第 5 步；第 5 步显示线性执行预览、失败即停和录音优先说明。
- 为补齐 Smart Profile 分支，在 smoke 数据中启用 Smart Profiles 并绑定 `vibe-coding`；第 4 步实际显示 `Vibe Coding`，第 5 步实际显示“Smart Profiles 开启时仅设置回退 Profile”。随后取消，Project Space 未保存改写。
- 尝试复验首次设置中 Bridge/托盘关闭状态时，Computer Use 检测到用户切回 Codex；按安全规则停止输入并正常关闭 Host。该视觉复验仍未完成，自动化自测已覆盖 true/false 保留。
- 所有工作区 `VibeMic.exe` 均已关闭；没有残留 Host/Bridge/Capture 测试进程。

### auditor 结果

- 最终复审：Phase 4 自身 `BLOCKER 0 / HIGH 0`。
- 已关闭 HIGH：Smart Profile 不得覆盖锁定 active Profile/mappings；激活应用前录音优先；缺主文件备份恢复；非法 schema；store 并发/CAS；恢复后备份保护；重复/非法 ID；future schema 只读；默认 Focus 持久化失败；原始超长字段截断；首次设置偏好覆盖。
- auditor 曾把不可达 `ShowSetupWizardElevenStepLegacy()` 误归入真实向导，复核方法边界后撤销该发现；真实入口为 `ShowSetupWizard()` 的 5 个分支。
- 继承 BLOCKER：Live HUD 只有禁用首页/托盘入口，`LiveHudForm.ShowInactive()` 无生产调用。它不是 Phase 4 回归，但继续阻塞整体 V2 候选。

### `$vibeflow-release-gate` 结果

阶段：4（Project Spaces）  
最终结果：**PASS WITH MANUAL HARDWARE CHECKS**

- 范围：新增/修改文件均服务于 Project Space 模型、Store、Runner、向导、项目页、共享 Focus/Profile 边界或测试；未安装依赖、未格式化全仓库、未修改系统设置。
- 冻结录音：Capture 源码/二进制 hash 与文件版本匹配；冻结 Capture、Bridge、默认配置无 diff；hold-to-talk、release-to-stop、尾音排空、音频路由和设备自然边界未改。
- 输入保护：六项时序仍为 `650/420/80/500/250/350`；Project Space 文件不含 Raw Input、Hook、设备过滤、录音映射、双击手势或普通键盘拦截。
- 配置兼容：Project/Focus 独立 store 覆盖 clean/existing/missing/malformed/backup、迁移幂等、未知字段、future schema 和 CAS；旧语音工具、映射、Profiles、Smart Profile 状态不被迁移或默认覆盖。
- 实现证据：Host/Bridge 构建、现有回归、三项 self-test、Focus 双窗口和 UI 资源测试均为 exit 0；`git diff --check` 无错误。
- UI：真实检查项目页、5 步向导、Smart Profile fallback 预览、取消和逐步结果；没有把打开 localhost 写成服务已启动，没有宣称 AI 已收到或转写完成。
- 功能关闭/未配置回退：升级用户不自动创建、运行或绑定 Project Space；无目标时不执行 Focus；smoke 模式不启动 Bridge/Capture。
- Gate 限制：真实应用/硬件与完整 DPI/主题矩阵未验证；Phase 4 可进入下一阶段，但整体 V2 因 Phase 2 Live HUD 仍为 **FAIL**。

### 仍需真机/人工验证

- Cursor/VS Code/其他编辑器的真实启动、Workspace 打开、Focus 锁定以及编辑器/终端/浏览器组合。
- 真实 Bridge revision ACK、Smart Profiles 锁定/回退、普通键盘和遥控器映射保持。
- Project Space 连续运行、取消及在每个可能抢焦点步骤开始真实 RC003 录音的优先级。
- Windows 10/11、100%/125%/150%/200% DPI、1366 x 768/1920 x 1080、浅色/深色/跟随系统。
- 首次设置 Bridge/托盘关闭选项的实际 UI 复验；VB-CABLE、蓝牙和第三方语音工具。

## 阶段 5：Capture & Ask

### 用户目标与最小完成标准

- 用户明确触发后可获取当前前台窗口或选择屏幕区域，预览实际将复制/粘贴的图像。
- 仅向已配置且再次验证的 Focus Target 派发一次图像粘贴；不读取文字剪贴板、不自动录音、不发送 Enter、不声称 AI 已收到。
- 取消、超时、录音优先和失败均停止后续焦点动作并清理本次临时 PNG。
- [x] 当前窗口、跨显示器区域、预览、复制、重截、取消和临时文件清理已实现。
- [x] Project Space `CaptureTargetId` 优先；显式项目目标失效时 fail closed，不回退到默认目标。
- [x] 两次真实焦点证据之间仅写入本次图片并派发一次 `Ctrl+V`。
- [x] Host/Bridge 构建、现有测试、新增测试、self-test 和 UI 资源回归通过。
- [x] auditor 最终确认 Phase 5 新增 HIGH 为 0。
- [ ] 粘贴后的成功回执没有可见 Live HUD；因此黄金流程和本阶段 gate 仍为 `FAIL`。

### 调查过的真实路径

- `scripts/VibeMic.cs`：Host 生命周期、首页/托盘入口、录音 cue、主题、当前 Project Space 和统一 ActionResult 发布。
- `scripts/features/FocusTargetService.cs`、`FocusTargetStore.cs`：已验证目标加载、执行、最终 UIA 焦点证据和日志脱敏。
- `scripts/features/ProjectSpaceModels.cs`：Project Space 的默认 Capture Target 关联。
- `scripts/ui/LiveHudForm.cs`、`ContextDeckForm.cs`：只读状态快照和实际生产调用；确认 Live HUD 仍无生产实例。
- `scripts/VibeMicAtvvCapture.cs`、`VoxDeckInputBridge.cs`：仅只读核对冻结录音与注入事件隔离；未修改。
- explorer 结论：截图、剪贴板、Focus、临时目录和用户入口均应留在 Host UI 外围，不新增 Capture/Raw Input/Hook 消费者。

### 修改文件

| 文件 | 阶段 5 目的 |
| --- | --- |
| `scripts/features/CaptureAskModels.cs` | 截图准备、目标解析和图像边界模型 |
| `scripts/features/CaptureAskService.cs` | 单实例、Focus 前后验证、图片剪贴板、单次粘贴派发、取消与清理 |
| `scripts/features/CaptureAskWindows.cs` | 前台窗口稳定采样、DWM 同步、窗口/虚拟屏幕捕获、焦点证据与安全 Ctrl+V |
| `scripts/ui/CaptureAskForm.cs` | 当前窗口/区域、预览、目标选择、复制/重截/取消、主题、DPI 滚动与录音优先 |
| `scripts/ui/CaptureAskIntegration.cs` | Host 生命周期、默认目标解析、首页/托盘入口和录音取消接线 |
| `scripts/tests/CaptureAskServiceTests.cs` | 服务、隐私、目标、取消、单实例、几何和前台稳定性回归 |
| `scripts/tests/CaptureAskUiTests.cs` | 可达 UI、主题、关闭竞态、录音完成竞态、PNG 清理、2x DPI 滚动和回执幂等 |
| `scripts/VibeMic.cs` | Capture & Ask 生产入口、主题刷新、录音优先和 Host 自测 |
| `BUILD_VIBE_MIC.cmd`、`scripts/validate.js` | 编译新模块并锁定不自动发送、输入隔离和冻结契约 |

本阶段未修改 `scripts/VibeMicAtvvCapture.cs`、`scripts/VoxDeckInputBridge.cs`、`vibe-mic-config.default.json`、`installer/VibeFlow.iss` 或 Capture hash 期望值。

### TDD 与修复记录

1. 服务测试覆盖预览像素与临时 PNG 一致、只写图像剪贴板、未验证/失效目标零副作用、Focus 前后复核、录音取消、单实例、项目目标禁止静默回退和多显示器负坐标几何。
2. Windows 前台观察器先暴露 DWM 切窗残影；修复为三次稳定前台采样、至少 160ms 且两次 `DwmFlush` 后再截图。
3. 区域选择层无法由 UIA 稳定定位；补充固定窗口 Name/AccessibleName，不增加第二套键盘导航状态机。
4. 夜间模式先暴露固定浅色和标准 DropDownList 白底；加入 Host 主题传递、`ApplyTheme(bool)` 和可渲染深色调色的 `FlatStyle.Flat`。
5. auditor 的截图完成竞态 HIGH 先以后台完成时录音已开始的 RED 测试复现；当前窗口和区域在 Prepare 前后及恢复前重复 fail closed，已准备 PNG 被清理，Host 不恢复。
6. auditor 的高 DPI HIGH 先以缺少滚动宿主的 RED 测试复现；加入工作区 clamp 与 `captureAskScrollHost`，实际 WinForms 用例执行 `Scale(2x)`、限制到 1366x728、建立真实滚动范围并把 footer 滚入可见区。
7. 区域 selector 与 Host 可能重复发布录音取消；RED 用例确认两次回执，随后用 `PublishVoiceCancellationOnce()` 收口，最终只发布一次 canceled。

### 构建与自动测试

| 命令/证据 | 最终结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0；最终 `VibeMic.exe` 时间晚于 Capture Ask 源码 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0；Bridge 源码无 diff |
| `npm test` | PASS，exit 0；`Vibe Flow V1.5.0 release validation passed.` |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0 |
| 最新源码重编并运行 `CaptureAskServiceTests.exe` | PASS，exit 0 |
| 最新源码重编并运行 `CaptureAskUiTests.exe` | PASS，exit 0 |
| `FocusTargetMultiWindowTests.exe FocusTargetSmokeApp.exe` | PASS，exit 0 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `126 -> 149`（+23），GDI `36 -> 54`（+18），`50407 ms` |
| `git diff --check` | PASS；仅 LF/CRLF 未来转换提示，无 whitespace error |
| Capture 源码 SHA-256 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，匹配 |
| Capture 二进制 SHA-256 / 版本 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` / `1.2.1.0`，匹配 |

### Computer Use 实际操作

- 仅启动仓库真实 `VibeMic.exe --ui-smoke` 和仓库内 `FocusTargetSmokeApp.exe`；smoke 数据位于 `tmp/ui-smoke`，未读写真实用户 Focus/Project 配置。
- 实际完成首页“截图提问”、无目标时禁用粘贴、当前窗口截图、区域拖选、预览、复制、重截、Esc 取消、学习/测试 Smoke 输入目标和安全粘贴。
- 实际看到 `Capture Smoke` 自动预选；粘贴后 Smoke 输入框获得键盘焦点，Capture Ask 关闭，临时 PNG 清理；未发送 Enter、未自动录音。
- 实际检查夜间主题，目标选择器和操作按钮使用深色调色；最新布局在本机 `AppliedDPI=96` 下打开，底部四个操作完整可达，`captureAskScrollHost` 出现在真实 UIA 树。
- 最终复验“当前窗口”按真实前台捕获了当时的 Codex 窗口；没有执行复制或粘贴，随后精确删除仓库 smoke 目录中的 1 个临时 PNG，剩余 PNG 为 0。
- 所有工作区 VibeMic/Smoke 测试进程已关闭。本机没有切换系统 DPI；125%/150%/200% 仅有生产布局测试，不是真实桌面证据。

### auditor 结果

- 最终：Phase 5 新增 `HIGH 0`；截图完成抢焦点、2x DPI footer 可达性和双录音取消回执均已关闭。
- BLOCKER：Live HUD 仍无生产实例或 `ShowInactive()` 调用。粘贴时 Capture Ask 和 Host 均隐藏，成功结果只更新隐藏 Host/Deck，因此用户看不到“粘贴动作已派发，请按住录音键描述问题”。
- MEDIUM：异常退出遗留 PNG 只在正常 Shutdown 清理；第二次焦点复核失败时未披露剪贴板已经被本次图片替换；Host 启动即开启 200ms 前台窗口观察器。
- LOW：长错误原因、恢复动作和错误码仍可能在固定高度单行状态区被省略。

### `$vibeflow-release-gate` 结果

阶段：5（Capture & Ask）  
最终结果：**FAIL**

- 范围：改动服务于截图、预览、目标验证、图像剪贴板、UI 接线和测试；无依赖安装、系统设置、驱动、仓库级格式化或无关生成文件。
- 冻结录音：Capture source/binary hash 与 `1.2.1.0` 版本匹配；Capture/Bridge/default config/installer 无 diff；hold-to-talk、release-to-stop、尾音排空、音频路由和设备自然边界未改。
- 输入保护：六项时序仍为 `650/420/80/500/250/350`；只派发一次 Ctrl+V 并在 finally 释放 V/Ctrl；Bridge 忽略 injected input；无文本读取、Enter、录音键映射、Raw Input/Hook/过滤改写。
- 配置兼容：Focus/Project 数据仍在独立 store；Capture Ask 不迁移或覆盖语音工具、Mappings、Profiles、Smart Profile 状态；无目标时 fail closed。
- 实现证据：Host/Bridge build、现有回归、三项 self-test、Capture Ask 两组测试、Focus 多窗口和 UI 资源测试均为 exit 0。
- UI：已真实检查主界面、入口、当前窗口/区域、预览、目标选择、复制、重截、取消、粘贴和深色主题；本机实际 DPI 为 96。
- Gate BLOCKER：粘贴成功后的下一步指引没有可见 Live HUD。该根因在阶段 2 已连续三种方案失败，按有限循环规则不进行第四次绕行；不得声明 Phase 5 完成或 V2 候选完成。

### 仍需真机/人工验证

- Cursor、VS Code 或第三方 AI 输入框真实接受图片；当前 Smoke 目标只证明焦点和按键派发。
- 真实 RC003 在窗口截图、区域选择、Focus 前后和粘贴派发各阶段开始录音时立即优先。
- Windows 10/11、混合多显示器、负坐标区域、最小化/受保护/提升权限窗口。
- 真实 100%/125%/150%/200% DPI、1366x768/1920x1080、完整浅色/深色/跟随系统与键盘导航。
- VB-CABLE、微信输入法和另一语音工具端到端；100 次按住/松开；截图后用户手动确认发送。
- 异常退出残留 PNG 的启动清理、焦点二次复核失败后的剪贴板披露和长错误详情入口。

## 阶段 6：Browser Remote Lite 与 Live HUD 收口

### 用户目标与最小完成标准

- 用户可查看 Browser Remote Lite 的逐键现有/推荐差异，显式应用并定向撤销；录音键、Home、TV、语音参数和 Smart Profiles 不被覆盖。
- 逐项测试只对已验证的前台浏览器、已运行且 revision 已确认的 Bridge 派发按键；录音优先且任何失败都释放修饰键。
- Live HUD 有唯一生产实例、可从首页/托盘打开、非激活显示，并承接 Host 隐藏后的 Capture & Ask 回执。
- [x] Browser 推荐、差异、显式应用、ACK 等待、一键撤销和单项测试 UI 已实现。
- [x] Live HUD 生产接线、完整原生控件树、主题、状态优先级、关闭抑制和生命周期清理已实现。
- [x] Host/Bridge 构建、现有回归、专项测试、自检、资源测试和只读 auditor 通过。
- [ ] 真实 RC003、Chrome/Edge 实际快捷键、HUD 多显示器位置和完整 DPI 仍需真机验证。
- [ ] HUD 完成态显示时长尚无用户偏好；留到设置重构并使用独立 V2 UI 偏好存储。

### 调查过的真实代码路径

- `scripts/VibeMic.cs`、`scripts/ui/BrowserRemoteLiteIntegration.cs`：Profile 保存、Bridge revision ACK、测试请求、统一 ActionResult、首页/托盘入口。
- `scripts/features/BrowserProfileTemplate.cs`、`BrowserProfileUndoStore.cs`、`BrowserRemoteTestService.cs`：受约束推荐、定向撤销、浏览器前台 PID/HWND 验证和现有 Bridge 门禁。
- `scripts/VoxDeckInputBridge.cs`：自定义测试请求消费、目标二次验证、Browser 专用 tap、录音转换锁和修饰键清理；未改 Raw Input、Hook、过滤或稳定手势识别。
- `scripts/ui/LiveHudForm.cs`、`ContextDeckForm.cs`：统一快照、非激活窗口、显示时长、主题和销毁路径。
- `scripts/VibeMicAtvvCapture.cs`、`vibe-mic-config.default.json`、冻结 Capture 二进制：仅核对身份和稳定契约，未修改或重建。

### 修改文件

| 文件 | 阶段 6 目的 |
| --- | --- |
| `scripts/features/BrowserProfileTemplate.cs`、`BrowserProfileUndoStore.cs` | 推荐差异、受限动作和精确撤销快照 |
| `scripts/features/BrowserRemoteTestService.cs` | Chrome/Edge 激活、稳定前台验证和录音优先协调 |
| `scripts/ui/BrowserRemoteLiteForm.cs`、`BrowserRemoteLiteIntegration.cs` | DPI 友好面板、应用/撤销、Bridge ACK 与真实回执 |
| `scripts/ui/LiveHudForm.cs` | 单一 ownerless HUD、完整 HWND 树、非激活显示、计时/关闭抑制、主题和录音状态优先 |
| `scripts/VibeMic.cs` | 首页/托盘入口、统一反馈接线、Host 生命周期和资源测试 |
| `scripts/VoxDeckInputBridge.cs` | 仅 Browser 测试外围的目标复核、原子 down/up、有限 key-up 清理和自测 |
| `scripts/tests/BrowserRemoteLiteTests.cs`、`BrowserRemoteLiteUiTests.cs`、`LiveHudUiTests.cs` | 模型、协调、UI、原生句柄、焦点和状态时长回归 |
| `BUILD_VIBE_MIC.cmd`、`scripts/validate.js` | 编译新模块并锁定可达入口与冻结边界 |

未修改 `scripts/VibeMicAtvvCapture.cs`、`vibe-mic-config.default.json`、`installer/VibeFlow.iss`、Capture 版本或期望 hash。

### RED 到 GREEN 与明确修复

1. Browser 测试不再启动、停止或重启 Bridge；请求只在已运行、健康、新鲜且 revision ACK 一致时写入。
2. Browser tap 不再在录音锁内等待 100 ms；最终目标复核、down 和 up 在同一短锁区完成，录音只能在修饰键释放后进入。
3. partial down 始终执行 up；首次 up 失败时在同一锁内有限重试一次，自测确认录音不能插入两次清理之间。
4. Host 建立唯一 Live HUD，并在正常交互前预热隐藏句柄；首页和托盘入口显式解除本次会话的关闭抑制。
5. auditor 的空白 HUD BLOCKER 先由子控件 `IsHandleCreated` 测试 RED 复现；递归创建全部原生子句柄后 GREEN。
6. `checking/running` 12 秒自动隐藏先由持续时间测试 RED 复现；现保持可见直至状态变化或用户关闭，成功 2.8 秒、警告/错误/取消 12 秒。

### 构建与自动测试

| 命令/证据 | 最终结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0 |
| `npm test` | PASS，`Vibe Flow V1.5.0 release validation passed.` |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，含 Browser 并发与两次 key-up cleanup 探针 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0 |
| 最新源码重编并运行 `BrowserRemoteLiteTests.exe` | PASS，exit 0 |
| 最新源码重编并运行 `BrowserRemoteLiteUiTests.exe` | PASS，exit 0 |
| 最新源码重编并运行 `LiveHudUiTests.exe` | PASS，完整 HWND 树可见且 HUD 不成为前台 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `128 -> 158`（+30），GDI `42 -> 82`（+40），`56928 ms` |
| `git diff --check` | PASS；仅 LF/CRLF 未来转换提示，无 whitespace error |
| Capture 源码 / 二进制 SHA-256 | `736017A0...74137E2` / `B62DE035...E683`，匹配 |
| Capture 版本 / 输入时序 | `1.2.1.0`；`650/420/80/500/250/350`，匹配 |

### Computer Use 实际操作

- 启动真实工作区 `VibeMic.exe --ui-smoke`；数据隔离在 `tmp/ui-smoke`，不启动 Bridge/Capture，不读写正式用户配置。
- 实际检查六页导航、浅色/夜间主题、首页 Live HUD 入口；点击 HUD 后前台焦点仍在 Host。
- HUD 是 ownerless `WS_EX_TOOLWINDOW`，Computer Use 的应用窗口截图不合成该独立浮层；因此未把肉眼可见性写成通过。原生测试另确认根窗口、全部子 HWND 和非激活约束。
- 实际打开 Browser Remote Lite，看到 7 个逐键差异、Chrome/Edge 目标、录音/Home/TV/Smart Profiles 边界和应用/撤销入口。
- 选择“功能键短按：刷新页面”后，在 smoke 无 Bridge 条件下实际得到 `BROWSER-TEST-SMOKE-NO-BRIDGE`，未派发按键。
- 实际应用推荐后显示 `BROWSER-PROFILE-ACK-PENDING`，没有把保存虚报为生效；一键撤销后显示 `BROWSER-UNDO-ACK-PENDING`，原两项配置恢复。
- 最终关闭面板和 Host；未发现残留 VibeMic、Bridge 或 Capture 进程。

### auditor 发现

- 最终最新 diff：新增 `BLOCKER 0 / HIGH 0`。
- 已关闭 BLOCKER：HUD 只创建根 HWND、子控件可能空白且关闭按钮不可用。
- 已关闭 HIGH：Browser down/up 间允许录音进入；首次 partial key-up 后修饰键可能残留；Host canceled 文案与已派发边沿不一致。
- 保留 MEDIUM：HUD 完成态时长不可调；Capture & Ask 启动观察器、异常 PNG 清理和剪贴板披露；首次设置保存/恢复/VB 启动/听写证据问题移交 Phase 7。
- 保留 LOW：Browser helper 的旧 wait 参数/方法已无执行调用；Capture & Ask 长错误详情可见性。

### `$vibeflow-release-gate` 结果

阶段：6（Browser Remote Lite + Live HUD 收口）  
最终结果：**PASS WITH MANUAL HARDWARE CHECKS**

- 范围：改动服务于 Browser 推荐/撤销/验证、统一反馈、Live HUD 和相关测试；无依赖安装、系统设置、驱动、签名、发布或仓库级格式化。
- 冻结录音：Capture source/binary hash、版本和稳定参数匹配；无 MIC_EXTEND、续流、toggle、文本读取/回填或自动 Enter；录音键不进入新功能。
- 输入保护：稳定时序未变；Browser 只向最终 PID/HWND 验证目标派发，所有修饰键清理发生在录音转换进入前；普通映射、Raw Input、Hook 和过滤路径未重写。
- 配置兼容：推荐仅在用户确认后改 `browser-ai`，保存与 Bridge ACK 分离，一键撤销只恢复本次快照；不启用 Smart Profiles、不覆盖 Home/TV/录音键。
- UI：真实检查差异、选择、无 Bridge 安全失败、应用 ACK pending、撤销 ACK pending、主题、HUD 入口和前台焦点；没有宣称网页变化、AI 接收或语音转写完成。
- 回归：新功能未配置时不启动 Bridge、截图、Focus 或 Project Space；升级用户的现有映射和 Smart Profile 状态保持。

### 仍需真机/人工验证

- 真实 RC003 在 Browser 动作边界开始录音时的优先级、无卡键和 100 次按住/松开。
- Chrome/Edge 对无 100 ms hold 的 PageUp/PageDown/Back/Tab/Ctrl+R/Ctrl+L/Ctrl+F 实际识别；未验证前不标记浏览器“已验证”。
- HUD 实际点击关闭、轮询不重开、托盘显式恢复、多显示器定位，以及 100%/125%/150%/200% DPI。
- Cursor/VS Code 图片粘贴、VB-CABLE、微信及另一语音工具、Windows 10/11 和睡眠/重连。

## 下一阶段

阶段 7：安装器与首次设置。explorer 已只读确认生产入口只有 `ShowSetupWizard()` 五任务；两个 Legacy 方法仅定义、无调用。安装器仍是标准 V1.5 页面，任务 5 尚未接入 Browser Remote Lite、Project Space、Smart Focus；VB-CABLE 启动失败、Bridge ACK 前清恢复标记、真实听写证据和升级配置测试是优先风险。实现只改安装器/Host 外围与测试，不触碰 Capture、录音 generation、Raw Input、Hook、过滤或稳定按键时序。

## 阶段 7：安装器与五任务首次设置（进行中）

### 用户目标与最小完成标准

- 安装器明确说明系统要求、准备事项、隐私边界、旧版检测、配置保护、最终安装目录和桌面快捷方式选择。
- 生产只保留一套可达的五任务首次设置；任务 5 显式提供 Smart Focus、Project Space、Browser Remote Lite、Smart Profiles 和进入第一个项目。
- VB-CABLE 只有在官方安装进程真实启动后才保存重启恢复进度；取消、缺失或启动失败不得留下首次设置恢复标记。
- 完成设置必须区分配置落盘和 Bridge revision ACK；旧用户重新打开向导时，ACK 超时不得把已完成状态改回未完成。
- 安装、升级、二次升级、卸载和启动项保护必须由一次性 Windows 账户执行；当前常用账户不得作为破坏性生命周期沙箱。

### 调查过的真实执行路径

- `installer/VibeFlow.iss`：Inno Welcome、目录/任务、安装前检查、文件复制、旧配置迁移、启动项恢复及卸载清理。
- `scripts/VibeMic.cs`：唯一生产调用 `ShowSetupWizard()`、五任务状态保存、真实听写证据、Bridge ACK、VB-CABLE 启动、自检修复入口和设置页重开入口。
- `scripts/ui/ProjectsPage.cs`：任务 5 “进入第一个项目”调用项目创建向导并执行新建结果。
- `scripts/Test-ReleaseLifecycle.ps1`：一次性账户保护、配置投影、双次升级和卸载保留探针。
- `BUILD_RELEASE.ps1`：根 Host/Bridge 到打包目录、固定 Capture、ZIP、安装器和 SHA-256 的唯一完整候选构建路径。

### 修改文件

| 文件 | 阶段 7 目的 |
| --- | --- |
| `installer/VibeFlow.iss` | V2 安装说明、真实安装前检查、受检迁移/启动项恢复、中央与旧目录 `.bak` 回退、卸载启动项失败传播 |
| `scripts/VibeMic.cs` | 五任务向导、ACK/恢复标记保护、可取消快捷键测试、VB-CABLE 真实启动回执、自检入口安全时序、Smart Profiles 显式选择 |
| `scripts/ui/ProjectsPage.cs` | 新建第一个项目后立即进入该项目现场 |
| `scripts/Test-ReleaseLifecycle.ps1` | 一次性账户前置保护、V1.5 配置投影、二次升级幂等和卸载保留验证 |
| `scripts/validate.js` | 唯一五任务入口、VB-CABLE 顺序、配置备份回退、卸载失败传播和阶段 7 静态门禁 |
| `.gitignore` | 忽略包含本机运行状态的 `input-bridge-health.json` |

未修改 `scripts/VibeMicAtvvCapture.cs`、稳定 Capture 二进制、`vibe-mic-config.default.json`、录音 generation、Raw Input、Hook、设备过滤或稳定手势时序。

### RED 到 GREEN 与明确修复

1. 自检页曾在 UAC 取消或安装器启动失败前写入恢复标记；门禁 RED 后改为先取得 `ActionState.Running`，仅未完成用户保存任务 3 恢复状态，保存失败回滚内存并显示错误。
2. Inno 曾只读主配置并在损坏时阻断升级；门禁 RED 后按中央主配置、中央备份、旧安装主配置、旧安装备份顺序回退，全部存在但不可解析时才停止。
3. 卸载曾忽略启动项删除失败；现先检查值，删除失败直接中止并说明注册表权限问题。
4. 任务 5 缺少 Smart Profiles 明确选择；门禁 RED 后增加可选复选框，新用户默认关闭、升级用户保留现值，并与最终配置一起等待 Bridge ACK。
5. 安装器实际目录/桌面快捷方式信息曾在选择页面之前生成；现检查页位于目录和任务选择之后，并在进入页面时重新读取真实选择。
6. 已完成用户重新打开向导时，ACK 超时不再重置 `setupCompleted`；新用户在 ACK 前继续保留安全恢复标记。
7. auditor 复审指出 Inno 的括号/字符串完整性检查仍不等于 JSON 解析；现由已安装 Host 的 `--installer-config-startup-query` 使用 `JavaScriptSerializer` 解析顶层布尔字段，损坏、缺字段、嵌套同名字段和字符串伪字段均不能冒充有效配置。
8. auditor 复审指出取消“以后自动启动桥接”可绕过当前运行 Bridge 的 ACK；现只要 Bridge 当前运行或用户选择启动 Bridge，都必须取得非空 revision 和匹配 PID 的 ACK 才能完成向导。
9. 活跃向导的 VB-CABLE 路径在进度保存失败时现会恢复原 `onboardingStep` 和恢复标记，避免后续保存把未协调状态再次落盘。

### 构建与自动测试

| 命令/证据 | 当前结果 |
| --- | --- |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS，exit 0 |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS，exit 0 |
| `npm test` | PASS，`Vibe Flow V1.5.0 release validation passed.` |
| `VibeMic.exe --self-test` | PASS，exit 0 |
| `VoxDeckInputBridge.exe --self-test` | PASS，exit 0 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | PASS，exit 0 |
| 六组 V2 专项测试 | PASS：Smart Focus 多窗口、Capture & Ask 服务/UI、Browser Remote Lite 服务/UI、Live HUD UI |
| Inno Setup 6.7.3 编译 `installer/VibeFlow.iss` | PASS，exit 0；只证明脚本可编译，不代表当前载荷是候选 |
| `VibeMic.exe --ui-resource-test` | PASS；300 次六页切换，USER `130 -> 151`（+21），GDI `42 -> 54`（+12），`97509 ms` |
| `git diff --check` | PASS；仅 LF/CRLF 未来转换提示，无 whitespace error |
| Capture 源码 / 二进制 SHA-256 | `736017A0...74137E2` / `B62DE035...E683`，匹配冻结值 |
| Capture 版本 / 输入时序 | `1.2.1.0`；`650/420/80/500/250/350`，匹配 |

### Computer Use 实际操作

- 启动本轮真实 `VibeMic.exe --ui-smoke`，数据仅写入仓库 `tmp/ui-smoke`，不启动 Bridge/Capture，不修改正式用户配置。
- 从设置页实际点击“重新打开首次设置”，确认唯一五任务向导完整可达。
- 逐步进入任务 2、3、4、5；任务 3 显示 CABLE Input/Output 已检测、RC003 麦克风等待连接，未把设备配对当作语音就绪。
- 任务 5 在 100% 缩放、约 `1002×711` 窗口中完整显示 Smart Profiles 可选项、Smart Focus、Project Space、Browser Remote Lite、快捷键、自检和“进入第一个项目”，无截断或重叠。
- 未点击 VB-CABLE 安装、Windows 设置、启动项、真实语音工具快捷键或最终完成按钮；未产生系统级副作用。
- 安装器此前已实际检查 Welcome、许可、目录、任务、安装前检查和 Ready 页面，并在“安装”前取消；只能作为 UI/脚本探针。

### 当前阻塞与未验证项

- 当前 `release/Vibe-Flow-Windows-x64` 载荷仍旧：根 Host SHA-256 `93B1E54D...FCDD9`，打包 Host `CD5D5919...A07D`；根 Bridge `3D4CD3A6...43251`，打包 Bridge `D9B7C93C...BCBEA`。因此当前 `release/VibeFlow-Setup.exe` 不是候选包。
- `BUILD_RELEASE.ps1` 需要缺失的固定 NAudio 2.2.1 依赖；恢复脚本会联网下载，按权限边界必须先取得用户授权。
- 全新安装、V1.5 升级、二次升级和卸载会修改安装目录与当前用户启动项，只能在一次性 Windows 账户并取得用户明确授权后执行。
- VB-CABLE UAC 取消、成功安装、重启恢复、真实 RC003、微信及另一语音工具、Windows 10/11 和完整 DPI 矩阵仍为真机未验证。

### auditor 发现与处理

- 阶段 7 只读 auditor 最终判定为 `FAIL`：两个 BLOCKER 是安装器载荷陈旧、真实安装生命周期未执行；这两项都需要后续授权范围内的构建/系统验证，未被降级或隐藏。
- auditor 的 HIGH“伪 JSON 可被子串匹配接受”已处理：Inno 通过 Host 隐藏查询模式等待真实解析结果，Host 自测覆盖顶层 `true/false`、嵌套同名字段、字符串伪字段、损坏 JSON 和缺字段配置。
- auditor 的 HIGH“当前 Bridge 运行但取消自动启动即可跳过 ACK”已处理：完成门禁现在同时检查当前真实 Bridge 和启动选择；没有 revision、匹配 PID 或 ACK 时保持在向导内。
- auditor 的 MEDIUM“活跃向导保存失败不回滚 VB-CABLE marker”已处理。
- 保留 MEDIUM：任务 5 跳转外部配置页时，尚未最终提交的复选选择不会跨向导实例保留；生命周期 fixture 尚未覆盖完全无配置、`launchAtStartup=true` 和四级损坏回退的真实安装矩阵。

### `$vibeflow-release-gate` 结果

阶段：7（安装器与五任务首次设置）  
最终结果：**FAIL**

- 范围：阶段 7 改动集中在安装器、首次设置、项目首次 CTA、生命周期脚本、静态门禁和本机运行状态忽略项；未安装依赖、驱动或软件，未修改音频设备/注册表，未签名、push 或发布。
- 冻结录音：Capture source/binary hash 与 `1.2.1.0` 匹配；稳定参数和录音/松开语义门禁通过；无新 MIC_EXTEND、续流、toggle、文本读取/回填、自动 Enter，录音键未进入新功能。
- 输入保护：`650/420/80/500/250/350` 匹配；本阶段未改 Raw Input、Hook、设备过滤、录音 transition 或普通键盘边界。
- 配置兼容：Host 原子保存/备份恢复/迁移自测通过；安装器四级候选顺序和 Host 真实 JSON 查询已编译；运行中 Bridge 必须 revision ACK。真实升级/二次升级尚未执行，所以不能判定配置迁移端到端通过。
- UI：Computer Use 实际检查设置页入口和五任务 1–5；任务 5 新选项及入口在 100% 缩放下可见。125%/150%/200%、Windows 10/11、重启恢复与硬件闭环未验证。
- 回归：Host/Bridge/Capture self-test、`npm test`、六组 V2 专项测试、300 次资源测试和 `git diff --check` 通过；冻结文件无 diff。
- 失败原因：当前安装器仍打包旧 Host/Bridge，且全新安装、V1.5 升级、二次升级和卸载未在一次性账户执行。

### 下一阶段

在获得依赖下载和一次性账户生命周期授权、生成包含最新 Host/Bridge 的安装包并完成真实生命周期前，阶段 7 保持未完成，不进入阶段 8 版本更新。

## 阶段 7 收口补充（2026-09-05）

### 遥控器麦克风故障调查与修复

- 根因不是冻结 Capture、Raw Input、Hook 或设备过滤逻辑，而是仓库根目录的开发运行布局缺少 `VibeMicAtvvCapture.exe`、`NAudio.Core.dll` 和 `NAudio.Wasapi.dll`。Host 可以恢复 Bridge，却无法启动麦克风 Capture。
- 新增 `BUILD_DEVELOPMENT.ps1` 与 `scripts/Prepare-DevelopmentRuntime.ps1`：只复制 SHA-256 已核验的冻结 Capture 和固定 NAudio 2.2.1 运行库，不重编 Capture。
- Host 在启动 Capture 前检查三项运行时文件；缺件时记录 `VOICE-RUNTIME-INCOMPLETE`，显示缺失文件、实际影响和修复入口，不再只显示短暂 Toast，也不再在系统恢复时虚报“正在重新连接”。
- 历史阶段记录曾写入 `state=ready`、`atvv_ready=true`、`ble_connected=true` 及约 3.48 秒真实音频证据；但对应 capture 日志未保留在当前 checkout，不能作为本轮可复核的真机通过证据，仍按未验证矩阵处理。
- 本轮没有新的实体方向键回执；普通遥控器快捷键仍标记为未验证，`VF-KEYS` 继续要求真实按键证据。没有修改 Bridge 输入核心作为猜测性修复。

### 首次设置补充验证

- 任务 5 的开机启动、自动连接 Bridge、托盘运行和 Smart Profiles 使用独立持久草稿；跳转 Smart Focus、Project Space 或 Browser Remote Lite 时不提前启用正式设置。
- Computer Use 在真实 `VibeMic.exe --ui-smoke` 上勾选 Smart Profiles，打开 Smart Focus，关闭并重新打开首次设置，再回到任务 5；勾选状态仍保留。
- smoke 模式为布局和入口测试而放宽任务 2/3/4 的硬件门禁；生产路径仍要求本次 RC003 方向键事件、完整音频证据和当前语音工具听写证据，不能据 smoke 的步骤推进声称真机通过。

### 最终开发与候选构建证据

| 命令/证据 | 结果 |
| --- | --- |
| `BUILD_DEVELOPMENT.ps1` | PASS；Host、Bridge、冻结 Capture 和两个 NAudio DLL 同目录完整 |
| `npm test` | PASS；`Vibe Flow V1.5.0 release validation passed.` |
| `Test-DevelopmentRuntime.ps1` | PASS；临时目录中的运行布局和冻结 Capture hash 通过 |
| `Test-DevelopmentBuild.ps1` | PASS；串行重跑后完整开发构建及三组件 self-test 通过 |
| Host / Bridge / Capture `--self-test` | 三项均 exit 0 |
| 六组 V2 专项测试 | 从最新产品源码重编并全部 exit 0 |
| `BUILD_RELEASE.ps1` | PASS；Inno Setup 6.7.3、ZIP 和 SHA-256 同批生成 |
| 根 Host 与包内 Host | SHA-256 均为 `A4FC8CD0A51D70BF21F88B8251686A708262940129F8C46CACC9476D46CFFEFC` |
| 根 Bridge 与包内 Bridge | SHA-256 均为 `596E0AC5B28397F12B4B4ECB1DF80AD5634E5D210373C223B7C1250F60DA75FF` |
| Capture 源码 / 包内二进制 | `736017A0...74137E2` / `B62DE035...E683`，匹配冻结契约 |
| `release/VibeFlow-Setup.exe` | 7,065,144 bytes；SHA-256 `90C2625AB64D1A2006055B67204B53AB4DEDCEA6B1DA6E11C289861E81D6ED2F` |
| `release/Vibe-Flow-Windows-x64.zip` | 3,903,059 bytes；SHA-256 `D222F56D94724A88EA6D3EDD1BBD0E377DBEF9DD3A4C88D13B89FAF5B48790F0` |

一次并行检查曾让 `Test-DevelopmentBuild.ps1` 与 Host self-test 同时访问 `VibeMic.exe`，编译器因文件锁退出；所有 self-test 结束后串行重跑即通过。该失败是测试调度互斥，不是产品编译回归。

### 当前状态

- 旧记录中的“候选包夹带旧 Host/Bridge”和“开发运行目录缺少 NAudio/Capture”已消除。
- 仍未执行：一次性 Windows 账户上的无配置全新安装、带启动项 clean、V1.5 升级、二次升级和卸载。当前常用账户存在正式安装目录、启动项和用户数据，生命周期脚本会安全拒绝，不能越权覆盖。
- 仍需真机：普通 RC003 方向键/快捷键、100 次录音、10/30/接近 60 秒音频、蓝牙/睡眠恢复、微信和另一语音工具、Windows 10/11、125%/150%/200% DPI、VB-CABLE UAC/安装/重启恢复。
- 阶段 7 最新 auditor 与 `$vibeflow-release-gate` 尚待对本次最终候选 diff 复审；在复审和一次性账户生命周期完成前不得把当前 V1.5 标识产物称为 V2.0 候选。

## 阶段 7 最终复审补充（2026-09-05）

### Context Deck 录音优先修复

- auditor 发现 Deck 显示前的两次录音检查仍存在 TOCTOU 竞态：录音可能在最终检查后开始，普通窗口随后激活并遮挡输入目标。
- `ContextDeckForm` 现同时使用 `ShowWithoutActivation` 与 `WS_EX_NOACTIVATE`；Deck 作为只读状态面板打开时不成为前台窗口。
- Deck 显示后立即复核录音状态；录音开始的统一取消序列撤销 V2 提交 epoch 后异步隐藏 Deck，不等待 UI 锁，不阻塞录音启动。
- TDD 证据：五段取消序列断言先因缺失 Deck 步骤编译失败；原生非激活契约随后以 `Context Deck lacks the native non-activating window contract` 失败；最小实现后 Host self-test 与完整 V2 专项套件通过。

### 最终阶段 7 验证证据

| 命令/证据 | 结果 |
| --- | --- |
| `BUILD_RELEASE.ps1` | PASS；重新构建当前 Host/Bridge、冻结 Capture、Inno Setup、ZIP 和 SHA-256 |
| 根 Host / 包内 Host | `8AF332B9AA3612913DCB68A3A860FB3A251358FC32788A97E2F8C63DC52CE30D`，完全一致 |
| 根 Bridge / 包内 Bridge | `8B619A407069D9D10B6F77D14C027A2D1EEC918E4846474013E4338F7EA9376C`，完全一致 |
| Capture 源码 / 根与包内二进制 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` |
| `release/VibeFlow-Setup.exe` | 7,082,975 bytes；`ACFDF9845E6D870C330B84E716E280EC89EEE361FF85FF33388673174130173F` |
| `release/Vibe-Flow-Windows-x64.zip` | 3,937,723 bytes；`3A4D0AA8ECD66B28DE9B43F015EE4E3EC223EF7BF36829BB1AA1C0912503290B` |
| `VibeMic.exe --ui-resource-test` | PASS；最终 Deck 源码下 300 次六页切换，USER `130 -> 151`（+21），GDI `42 -> 54`（+12），89,734 ms |
| `git diff --check` | PASS；仅行尾转换提示，无 whitespace error |

### Computer Use 实际操作

- 启动本轮真实 `VibeMic.exe --ui-smoke`，实际点击首页“查看 Context Deck”。
- Deck 完整显示当前应用、Profile、项目、目标、设备、语音、11 项实体键映射和最近动作；正常工作区无截断、重叠或内部滚动条。
- 实际点击 Deck 关闭按钮后返回首页，焦点落回“查看 Context Deck”；无残留 Deck 或 Host 进程。
- smoke 模式明确不启动 Bridge/Capture，本次仅验证窗口、布局、入口和关闭行为，不作为遥控器或录音真机证据。

### auditor 与 `$vibeflow-release-gate`

- 最新只读 auditor：代码层 `BLOCKER 0 / HIGH 0`；原 Deck 录音竞态已清除。
- 阶段 7 release gate 最终结果仍为 **FAIL**，不是代码回归失败，而是权限边界内无法闭合的候选证据：全新安装、V1.5 升级、二次升级和卸载尚未在一次性 Windows 账户执行。
- 当前 Host/Bridge/Installer 仍标识 `1.5.0`，只用于验证阶段 7 载荷，不得称为 V2.0 候选。
- 真实 RC003 麦克风/普通快捷键、普通键盘 F5 隔离、100 次录音、10/30/接近 60 秒音频、蓝牙/睡眠恢复、微信与另一语音工具、Windows 10/11、125%/150%/200% DPI、VB-CABLE UAC/安装/重启恢复及 Chrome/Edge 仍为未验证。

## 阶段 8：V2.0.0 最终候选

### 用户目标与最小完成标准

- [x] 非 Capture 产品、Host、Bridge 和安装器统一为 `2.0.0` 候选身份。
- [x] Capture 保持 `1.2.1.0`，源码和二进制哈希保持冻结值。
- [x] V2 用户指南、迁移、自动测试、真机矩阵、限制、回滚、更新说明和安装器说明已补齐。
- [x] 重新构建根 Host/Bridge 和开发运行布局，组件自检、迁移及 V2 专项测试通过。
- [x] 生成候选目录、ZIP、未签名安装器、`SHA256SUMS.txt` 和 `RELEASE_BODY_v2.0.0.md`。
- [x] 根目录、候选目录和 ZIP 内 Host/Bridge/Capture/NAudio 通过逐文件身份校验。
- [x] 使用 Computer Use 实际操作打包后的候选 Host 和主要 V2 用户流程。
- [x] 最终只读 auditor 无 BLOCKER/HIGH。
- [x] 阶段 8 `$vibeflow-release-gate` 已记录。

### 实际调查路径

- explorer 只读复核 `BUILD_RELEASE.ps1`、开发构建、版本门禁、制品门禁、Inno Setup 和遥控器现有测试路径。
- 确认 `BUILD_RELEASE.ps1` 是唯一端到端候选入口；它不重建 Capture，不自动安装、不签名、不 push 或发布。
- 确认 Bridge self-test 覆盖 RC003 F5 Hook fallback、100 次按键边沿/去重、scan code `0x3F -> F5`、配置解析和注入隔离；这些自动化证据不能代替实体遥控器测试。

### 修改文件

- 版本/构建/安装：`package.json`、`VIBE_MIC_VERSION.md`、`BUILD_RELEASE.ps1`、`BUILD_DEVELOPMENT.ps1`、`BUILD_VIBE_MIC.cmd`、`installer/VibeFlow.iss`、CI 和 release tests。
- Host/Bridge 与 V2 模块：`scripts/VibeMic.cs`、`scripts/VoxDeckInputBridge.cs`、`scripts/ui/`、`scripts/features/`、`scripts/tests/`。
- 候选说明：README、Quick Start、CHANGELOG、版本归档、架构、功能、兼容矩阵、签名说明及八份 `docs/V2_0_*.md` 文档。
- 界面截图：六页 V2 主界面与项目页；截图来自真实 `--ui-smoke` 构建。
- 冻结文件 `scripts/VibeMicAtvvCapture.cs` 没有 tracked diff，期望哈希没有修改。

### 构建与自动测试

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS；V2.0.0 candidate validation，Capture 1.2.1.0 |
| `Test-ReleaseDependencyPreflight.ps1` | PASS |
| `Test-DevelopmentBuild.ps1` | PASS；开发运行布局、冻结 Capture 和迁移测试通过 |
| `Test-V2FeatureSuite.ps1` | PASS；Focus、Capture & Ask、Browser Remote、HUD/Deck |
| 根 Host / Bridge / Capture `--self-test` | exit 0 / PASS / PASS |
| `VibeMic.exe --ui-resource-test` | PASS，exit 0；300 次六页切换，USER `128 -> 151`（+23），GDI `42 -> 54`（+12），88,516 ms |
| `BUILD_RELEASE.ps1` | PASS；Inno Setup 6.7.3 成功生成候选制品 |
| 候选目录内三组件 `--self-test` | 均 exit 0 |
| `Test-ReleaseIdentity.ps1` | PASS；Host/Bridge/Installer 2.0.0，Capture 1.2.1.0 |
| `Test-ReleaseArtifacts.ps1` | PASS；根、目录、ZIP、清单一致 |
| `git diff --check` | PASS；只有 LF/CRLF 未来转换提示 |

首次执行 `BUILD_RELEASE.ps1` 时，仍在后台运行的 `--ui-resource-test` 锁住根 `VibeMic.exe`，编译器报 `CS0016`。检查命令行后确认资源测试仍在正常执行；等待其自然结束并取得新报告后串行重跑，候选构建通过。没有用修改源码或跳过测试绕过文件锁。

### 候选制品

| 文件 | 大小 / SHA-256 |
| --- | --- |
| `release/VibeFlow-Setup.exe` | 8,806,504 bytes；`72F799AB090A499193F2E468F994B316718F25F7BF59D8276ACE7586A8E17551` |
| `release/Vibe-Flow-Windows-x64.zip` | 5,798,919 bytes；`7BCD7D2A37B820E723E9EBB1436E1B2C6138616BE6369BE8E7D54E337D711D1B` |
| 根 / 包内 Host | `E5D0B40C2FF54C191EF047F26ABA52715AD279F940352FB2FC2B029770C2F4E2` |
| 根 / 包内 Bridge | `60857941DB371E13B0F258B1DD58D898A44856CDE897A639A9979F56E58E7487` |
| 根 / 包内 Capture | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` |
| Capture 源码 | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` |

安装器未配置 Authenticode 签名，构建日志和产品文档均按事实标记为未签名候选。

### Computer Use 实际操作

- 启动打包目录中的 `release/Vibe-Flow-Windows-x64/VibeFlow.exe --ui-smoke`，确认窗口身份为 V2.0.0。
- 实际巡视首页、项目、按键、语音、自检、设置六页；首页明确显示语音桥接暂停、目标未设置和用户手动确认发送。
- 实际打开 Live HUD 和 Context Deck；Deck 显示当前上下文、Profile、项目、目标、设备、语音、全部稳定按键映射和最近动作。
- 实际打开 Capture & Ask，确认没有已验证目标时“粘贴到目标”禁用。从首页直接请求当前窗口截图时，没有合格的外部前台窗口，程序返回 `CAPTURE-ASK-WINDOW-NOT-FOUND`，明确显示“未写入剪贴板”、影响、重试方法和错误码；取消后首页显示“未执行后续步骤”。外部应用截图与图片粘贴仍列为真机未验证。
- 实际打开 Smart Focus 目标管理；空状态、应用选择、学习、测试、默认目标和隐私说明可见，未学习或保存虚假目标。
- 实际打开 Project Space 五步向导；空表单点击下一步保持在第 1 步并显示 `PROJECT-NAME-INVALID`，随后取消，无资源或配置写入。
- 实际打开 Browser Remote Lite；可见当前/推荐差异、逐项测试、一键撤销和显式应用，关闭时未应用配置。
- 从自检进入唯一一套五任务首次设置，逐页检查任务 1–5；未验证步骤显示“进度已保存，待复核”，Smart Profiles 默认关闭，未自动修改按键。
- 实际切换夜间模式和白天模式；主要页面未观察到文本截断、按钮重叠或不可见操作。
- 关闭候选 Host；没有保留 smoke Host、Bridge 或 Capture 进程。
- 最终一次 `BUILD_RELEASE.ps1` 后又启动同批候选目录 Host，窗口身份仍为 V2.0.0；该最终进程已关闭，候选目录中未发现日志、health、用户映射、V2 用户配置或仓库绝对路径污染。

### 遥控器问题结论

- 开发目录缺少冻结 Capture/NAudio 时，Host 现在以 `VOICE-RUNTIME-INCOMPLETE` 提供具体修复入口；`BUILD_DEVELOPMENT.ps1`/`Prepare-DevelopmentRuntime.ps1` 生成完整可运行布局。
- Bridge 不再错误绕过 RC003 的 F5 Hook fallback；恢复条件为 `filterHealthy && (mappingResolved || taskSwitcherNavigation)`。
- Bridge self-test、开发构建和候选目录 self-test 均通过；冻结 Capture、Raw Input、Hook、设备过滤和手势状态机没有被重写。
- 本机最终 UI 自检显示 Windows 蓝牙查询超时、未找到已配对 RC003，且 smoke 模式不启动按键 Bridge；因此当前环境不能把用户报告的麦克风/快捷键问题归因于最终候选，也不能声称实体设备已经修复。程序给出了蓝牙设置、添加设备和重建监听入口。
- 最终候选的实体 RC003 普通快捷键、普通键盘 F5 隔离和完整录音矩阵仍需真机复验，不能由自动测试替代。

### 仍需授权或真机验证

- 一次性 Windows 账户上的全新安装、V1.5 升级、二次升级和卸载。
- RC003 普通键逐键、100 次录音、10/30/接近 60 秒、蓝牙/睡眠恢复。
- 微信输入法与至少一个其他语音工具的最终候选端到端输入。
- Chrome/Edge 页面响应、Smart Focus 50 次、截图图片粘贴目标矩阵。
- 125%/150%/200% DPI、Windows 10/11 和两种目标分辨率。
- VB-CABLE 安装/UAC/重启恢复、代码签名、push、PR 和正式 Release 均未执行。

## 下一阶段

完成最终只读 auditor 和阶段 8 release gate；只有在授权的一次性 Windows 环境与真实硬件补齐剩余证据后，才评估正式稳定版发布。

## 阶段 8 最终收口补充（2026-09-05）

### 本轮明确修复

- `scripts/VibeMic.cs` 的 Host 自测对 `RecordingPriorityCommitGate.TryCommit` 使用明确参数签名查找，消除了新增重载导致的 `AmbiguousMatchException`；该问题只影响自测，不改变录音或输入运行路径。
- `scripts/VoxDeckInputBridge.cs` 的 Bridge 自测启用隔离标记；模拟录音键事件不再因为找不到 Host 而启动真实后台 Host/Capture，构建和自测结束不再遗留服务进程或锁住输出文件。
- `START_VIBE_FLOW.cmd` 现在优先启动候选包中的 `VibeFlow.exe`，开发目录无该文件时回退到 `VibeMic.exe`，两者都不存在时明确提示先运行 `BUILD_DEVELOPMENT.ps1`。

### 最新串行证据

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS；V2.0.0 candidate validation，Capture 1.2.1.0 |
| `Test-DevelopmentBuild.ps1` | PASS；开发运行布局、迁移测试及三组件自测通过 |
| `Test-V2FeatureSuite.ps1` | PASS；Focus、Capture & Ask、Browser Remote、HUD/Deck |
| `BUILD_RELEASE.ps1` | PASS；当前源码重建 Host/Bridge、ZIP、未签名安装器和 SHA-256 |
| 根 Host / 候选 Host | `3FF10DDA0057A9EFFB75748BE399E3CA64AC14DEE87F40F0BD98C32AD2256931`，一致 |
| 根 Bridge / 候选 Bridge | `157578AA37CFCE69B94B640812864FF51045857F1E530CB7A8930CB2280AA3D0`，一致 |
| `release/VibeFlow-Setup.exe` | `6016A96D7108B9505CE90973E520C3FB0F82EE8C8819619860953F46DD90B379` |
| `release/Vibe-Flow-Windows-x64.zip` | `924F670317D78129282DE0E0B4A0B1CE95C389D65B95EAC3F0068BC36DE4DD36` |
| Capture 源码 / 二进制 | `736017A0...74137E2` / `B62DE035...E683`，仍匹配冻结契约 |
| 构建结束进程检查 | PASS；没有残留 `VibeFlow/VibeMic/VoxDeckInputBridge/VibeMicAtvvCapture` 进程 |
| `START_VIBE_FLOW.cmd` | PASS；开发目录回退入口已纳入候选包 |

### 最终审计状态

- 最新只读 auditor 已复核当前根/候选 Host 与 Bridge 同 hash，代码层 `BLOCKER 0 / HIGH 0`；最终阶段 8 gate 记录待本轮 release-gate 命令完成后勾选。
- 遥控器麦克风问题：没有发现可安全修复的录音核心或输入路由逻辑 BUG。设备链路仍是 RC003 专属 Raw Input（可选过滤驱动）→ Bridge → 命名事件 → Capture；普通键盘 F5 不触发录音。当前机器没有真实 RC003 按键边沿，且过滤驱动未安装，`fallback:open_failed_win32_2` 是已记录的回退状态，不是通过全局 F5 Hook 绕过的理由。
- 当前可交付物是 **V2.0.0 unsigned candidate**，不是正式稳定版。真实 RC003 逐键、录音时长矩阵、VB-CABLE/语音工具、浏览器、安装升级卸载、Windows 10/11 和 125%–200% DPI 仍必须在对应真机/一次性账户验证后才能改为通过。

## 阶段 8 Release Gate 收口（2026-09-05）

### 本轮额外时序修复

- 按测试驱动先把 Bridge 自测改为要求录音转换可以在 Browser Remote key-up 前进入；旧实现自测按预期失败。
- `RunBrowserRemoteTapWithVoicePriority` 现不再用 `voiceTransitionLock` 包住 key-up/重试，`HandleVoicePhysicalTransition` 也不再等待 Browser Remote 活跃窗口；录音 DOWN 先登记 pending 并立即进入稳定状态，浏览器按键仍保证最终 key-up 和重试。
- Bridge 自测、Host/V2 专项和候选构建均已重新通过；没有修改 Capture、Raw Input 设备过滤契约、键盘 Hook 的设备隔离或手势常量。

### 最终候选证据

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS |
| `BUILD_RELEASE.ps1` | PASS；当前源码重建并完成 Inno Setup 6.7.3、ZIP、SHA-256 |
| 根/候选 Host | `402C0DD3836F669BC1F9AD76B77711A39B891C98F0B60A32C475892E1BD3B63E`，一致 |
| 根/候选 Bridge | `693C42EADA61E0C2AA67B64C9FA9C09ACBEF55549E142C68C2C7E3E75A49AE1C`，一致 |
| `release/VibeFlow-Setup.exe` | `ED1A16B029559CFCBDFAA0DD2345174A9726A71D9F610B9C14391D54905DAC32` |
| `release/Vibe-Flow-Windows-x64.zip` | `6A3E12B6F00ADFCB91DA3C5922580B033398D063635814F3DEA29AB56733AAB9` |
| Capture 源码 / 二进制 | `736017A0...74137E2` / `B62DE035...E683`，匹配冻结值 |
| 六组件 self-test、V2 suite、Release Identity/Artifacts/Dependency | PASS |
| 构建结束进程检查 | PASS；无 Host、Bridge、Capture 残留进程 |

### Gate 结论

- 冻结录音、输入路由、配置保护、UI 反馈和制品身份：**PASS**。
- 自动测试、构建和实际候选 UI 页面检查：**PASS**。
- 最终结果：**PASS WITH MANUAL HARDWARE CHECKS**。
- 必须保留为“未验证”的项目：真实 RC003 普通按键与 100 次录音、10/30/约 60 秒音频、蓝牙/睡眠重连、微信及另一语音工具端到端输入、VB-CABLE UAC/重启恢复、Chrome/Edge 页面响应、125%/150%/200% DPI、Windows 10/11、一次性账户全新安装/升级/卸载和代码签名。
- 当前机器的 `rc003_filter_state=fallback:open_failed_win32_2` 仍表示可选过滤驱动未安装后的 Raw Input 回退；没有真实 RC003 按键边沿证据，不能将用户报告的麦克风快捷键问题标为硬件已修复。普通键盘 F5 仍不会触发录音。

## 最终收口复核（2026-09-05，本轮继续）

### 最终 release-gate 证据

| 命令/证据 | 结果 |
| --- | --- |
| `npm test` | PASS |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS；Smart Focus、Capture & Ask、Browser Remote Lite、Live HUD/Deck |
| `scripts/tests/Test-DevelopmentBuild.ps1` | PASS；开发运行布局、配置迁移及自测 |
| `scripts/tests/Test-ReleaseIdentity.ps1` | PASS |
| `scripts/tests/Test-ReleaseArtifacts.ps1` | PASS；根目录、候选目录、ZIP、安装器和 `SHA256SUMS.txt` 同批一致 |
| `scripts/tests/Test-ReleaseDependencyPreflight.ps1` | PASS |
| `BUILD_RELEASE.ps1` | PASS；Host/Bridge、未签名安装器、ZIP 和清单重新生成 |
| Host / Bridge / Capture `--self-test` | PASS；Capture 仍为 1.2.1.0 |
| `git diff --check` | PASS；仅有预期 LF/CRLF 转换提示 |
| 最终进程检查 | PASS；清理本轮 UI/自测实例后无 Vibe Flow 进程残留 |

本轮重新生成的同批制品哈希：

- 根/候选 Host：`2569CD6E5140F473C89E49C77FED8D297184CD48113D91CFF4699F1303497DEF`
- 根/候选 Bridge：`4388C3674B5C90C8AF2EAE65167712E80CDC7C50E16FE77C5D059A2AB8B20461`
- `release/VibeFlow-Setup.exe`：`128CB503CF92A6E78513B7FE8A4AF581D0B147CF510D60457B7505ED424B6D81`
- `release/Vibe-Flow-Windows-x64.zip`：`5DB2026D67F7E7D400E26939C329337E5E3EA3847DE07D08768F28B11467E9E9`
- Capture 源码 / 二进制：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`

### 最终只读 auditor

- `vibeflow_auditor`：BLOCKER 0、HIGH 0、MEDIUM 0、LOW 0。
- Browser Remote 复核确认录音 DOWN 不等待浏览器 key-up，key-up/重试在锁外并由 `finally` 清理；Bridge 自测覆盖录音优先、取消、部分失败清理及 100 次边沿去重。
- 冻结 Capture、Raw Input、设备过滤、键盘 Hook、手势常量和录音参数未被修改；没有新增自动发送、转写回填、第二套录音状态机或普通键盘 F5 录音路径。

### 本轮 Computer Use

- 实际启动最终候选 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`，检查首页、项目页和自检页；项目空状态、创建入口、六页导航和自检的“待验证/正在检测”状态均可见，没有伪造硬件成功。
- 普通启动模式的关闭行为按现有 `minimizeToTray` 配置进入托盘；测试结束后按父子关系清理 Host/Bridge/Capture。`--ui-smoke` 路径仍用于不启动后台链路的 UI 压力和页面回归。
- 未在 Computer Use 中操作系统设置、蓝牙配对、音频设备、安装器或第三方语音工具。

### 麦克风快捷键问题最终判断

- 当前证据不能证明实体遥控器已经失效，也没有发现可安全修复的源码 Bug。链路仍为 RC003 专属 Raw Input（可选过滤驱动）→ Bridge → 命名事件 → 冻结 Capture；`fallback:open_failed_win32_2` 是过滤驱动未安装时的预期回退。
- 日志只有 self-test 的录音边沿，没有真实 RC003 按键边沿；普通键盘 F5 仍必须保持不触发录音。不能用恢复全局 F5 Hook 或修改冻结 Capture 作为绕过方案。
- 仍需真实 RC003 逐键、100 次录音、蓝牙/睡眠恢复、VB-CABLE 和语音工具端到端验证；在此之前候选保持 `PASS WITH MANUAL HARDWARE CHECKS`，不是正式稳定版。

### 下一阶段

在真实 Windows 10/11、RC003、VB-CABLE、语音工具和一次性用户账户完成未验证矩阵后，再评估签名、升级/卸载生命周期和正式发布；本轮不 push、不创建 PR、不发布 Release。

## 遥控器问题补充验证（本轮继续）

- 使用最终候选 `release/Vibe-Flow-Windows-x64/VibeFlow.exe` 实际启动普通运行模式，确认首页显示“按住录音键 / 松开结束”，未进入 UI smoke 假链路。
- 通过 Computer Use 向候选窗口发送一次普通键盘 `F5`。界面仍保持“已就绪，等待按住录音键”，没有进入录音、没有创建新的 Capture 会话，也没有发送语音工具唤起信号。
- 候选 Bridge 日志记录 `RAW KEY DEVICE MISMATCH vk=0x74 scan=0x00 type=1 name=`；同一时段没有 `Key 录音键 DOWN`、`Voice key event` 或 `RC003 RAW KEY`。这证明普通键盘 F5 被设备身份检查拒绝，不会冒充 RC003 麦克风按键。
- 测试结束后按父子关系清理候选 Host、Bridge、Capture，进程检查为零残留。
- 这条证据增强了“普通键盘隔离正常”的结论，但仍不能替代实体 RC003 按住/松开和真实音频验证；遥控器本身无响应时仍需检查蓝牙唤醒、VB-CABLE、权限和真实设备边沿。

### 文档与门禁收口

- （历史结论，已被 2026-09-06 证据取代）曾记录为兼容蓝牙 HID 重连保留 `keyboard_hook` 录音兜底；当前基线已改为普通 Hook 永不进入录音状态机。
- 历史阶段曾记录的真实音频摘要已标为“当前 checkout 无对应日志、不可作为本轮真机通过证据”，避免间接证据被误报为当前硬件通过。
- 文档修正后重新运行 `npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseDependencyPreflight.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、三组件 self-test 和 `git diff --check`，全部通过。

### 最终候选重建（本轮最后一次）

- 第一次串行重建遇到短暂的 `VibeMic.exe` 文件锁；等待现有自测进程排空后原样重跑成功，没有删除或覆盖用户文件。
- `BUILD_RELEASE.ps1` 最终成功；其内置 V2 专项、迁移、身份、制品和 Inno Setup 构建均通过。
- 当前根/候选 Host：`849EEF80814C9D57986F72FA61CE482116E175303E5495BFC1095BE734102910`。
- 当前根/候选 Bridge：`09ADACFB4A754AC1206A3E68DCC4238B6D01B9E0CB907269D7FFFE863239830D`。
- 当前安装器：`4D05010F54AAA6932F154ECDCF3654DA76BC2153B5B1DA2EC3D3BF1B63267E7D`；当前 ZIP：`D4B23FA3C97E730C05255692F777FB10C4C4E03C9F5BADB906AE06AFAD136C4B`。
- Capture 源码/二进制仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 最终 self-test 进程自然排空后检查为零残留；本轮没有签名、Push、PR 或正式 Release。

## 本轮用户问题收口（2026-09-05）

- （历史结论，已被 2026-09-06 证据取代）曾恢复 `keyboard_hook` 录音兜底；该路径已移除，避免普通键盘 F5 触发录音。
- `npm test`、`BUILD_DEVELOPMENT.ps1`、Host/Bridge/Capture self-test、`scripts/tests/Test-V2FeatureSuite.ps1` 均通过；Capture 二进制哈希仍为冻结值。
- 用户配置中的旧 smoke Focus 目标已取消默认绑定并保留 `.bak`；首页现在显示“语音目标：尚未设置”，不会再把不存在的 Notepad 目标当作可用目标。
- Computer Use 实际启动并操作开发构建：项目页显示 `言灵webflow`、`ChatGPT · 对话工作台`、Workspace 为 `C:\Users\Admin\Documents\ChatGPT\vibe -flow`，输入目标为“未设置”。点击“进入”后只验证了 ChatGPT 启动请求，未伪造窗口或对话框成功；结果明确要求先学习并测试输入目标。
- ChatGPT 不是 Cursor/VS Code 编辑器集成：本项目把它作为对话工作台打开，Workspace 仅作为 Vibe Flow 项目现场记录，不会向 ChatGPT 自动注入目录或自动发送消息。
- 录音键不会自动触发 Smart Focus，这是冻结录音优先规则；必须先单独学习并验证 ChatGPT 输入框。当前 ChatGPT 桌面 UIA 未提供可验证的编辑控件，因此仍标记为未验证。
- 源码、运行日志和 self-test 没有发现 Vibe Flow 将转写文字写入剪贴板；文本剪贴板仅用于“复制问题摘要”，图片剪贴板仅用于 Capture & Ask。用户看到的剪贴板现象需要在真实 ChatGPT 输入焦点和微信输入法环境下复现，不能通过修改冻结 Capture 绕过。
- 最终结果：`PASS WITH MANUAL HARDWARE CHECKS`；实体 RC003、微信输入法、VB-CABLE、ChatGPT UIA 聚焦、DPI 和安装升级仍未完成真机验证。

## 本轮录音键根因修复（2026-09-06）

- 用户反馈“录音键只把文字放进剪贴板”后，先检查了真实日志、Bridge 健康状态和 Capture 源码；当前过滤器状态为 `fallback:open_failed_win32_2`，`WH_KEYBOARD_LL` Hook 确实会在无设备身份时把普通 F5 送入录音 transition。
- 失败测试先验证了设备来源边界；随后修改 `scripts/VoxDeckInputBridge.cs`，普通 `keyboard_hook` F5 只直通 Windows，不再调用录音状态机。RC003 Raw Input 与 `rc003_filter` 继续复用原有 generation、hold-to-talk、release-to-stop 状态机。
- 新增 `IsDeviceScopedVoiceSource` 自检，并更新 `scripts/validate.js` 静态门禁；没有修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、录音参数或文本剪贴板路径。
- 自动证据：`npm test`、`BUILD_DEVELOPMENT.ps1`、`VoxDeckInputBridge.exe --self-test`、`VibeMic.exe --self-test`、`VibeMicAtvvCapture.exe --self-test` 和 `Test-V2FeatureSuite.ps1` 均通过；Capture SHA-256 仍为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- Computer Use 实际启动本地 `VibeMic.exe`，首页显示“按住录音键 / 松开结束”和“语音目标：尚未设置”；WinForms accessibility 可读，但一次页面点击报告 `coordinate input geometry is unavailable`，因此只记录为部分 UI 验证。ChatGPT 桌面 UI 按安全规则未自动化，输入框 UIA 仍未验证。
- 旧文档中“keyboard_hook 录音兜底”的历史记录已由当前基线更正；不得恢复该设备盲路径作为遥控器修复方案。
- 审计发现并已修复一个中等反馈问题：普通 Hook F5 不再刷新 `last_input_at/last_input_kind` 的遥控器活动统计；Hook 仅保留诊断字段，避免自检把普通键盘显示成最近遥控器事件。
- 最终候选门禁：`BUILD_RELEASE.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1` 均通过；根目录、`release/Vibe-Flow-Windows-x64` 和 ZIP 内 Bridge SHA 对齐，Capture 仍为冻结哈希。最终 `vibeflow_auditor` 复核为 BLOCKER 0、HIGH 0；仅保留真实 RC003/音频/第三方语音工具的人工验证项。
- 当前结果：`PASS WITH MANUAL HARDWARE CHECKS`。仍需实体 RC003、VB-CABLE、微信输入法和 ChatGPT 输入框真实验证；用户看到的剪贴板现象暂不能归因于 Vibe Flow 自己写入文本剪贴板。

## Notes Deck 收口复核（2026-09-06）

- 活跃首次设置已与 Notes Deck 范围对齐：最后一步显示“打开便签”，完成后进入便签页；不再从首次设置自动运行旧 Project Space。
- 本次构建：`BUILD_VIBE_MIC.cmd` 通过；`npm test` 通过；`scripts/tests/Test-V2FeatureSuite.ps1` 通过；`VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test` 通过；`git diff --check` 仅有换行提示。
- 冻结 Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`；二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，均未变化。
- Computer Use 实际启动普通 `VibeMic.exe`（无 `--ui-smoke`），首页可见；设备状态诚实显示“正在连接”，语音目标显示“尚未设置”，没有伪造硬件或 AI 成功。
- 当前普通进程已保持运行，主窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”；独立便签 Deck 可由应用内按钮打开。
- 尚未验证：真实 RC003 按住/松开录音、VB-CABLE、微信输入法端到端输入、ChatGPT 输入框 UI Automation 聚焦、蓝牙/睡眠恢复、Windows 10/11、DPI 100/125/150/200%、安装升级卸载和真实供应商 API。
- 本阶段最终门禁仍只能标记为 `PASS WITH MANUAL HARDWARE CHECKS`，不能声称正式稳定版或真机通过。

## 录音优先竞态修复（2026-09-06，最终复核）

- 只读 auditor 发现 Notes Deck 已打开时，录音开始没有隐藏 Deck，且 `ShowDeck()` 缺少二次录音门禁。
- 先新增失败测试，再修复 `scripts/ui/NotesDeckForm.cs`：录音期间拒绝显示、使用 `ShowWithoutActivation`、移除自动 `Activate()`，并提供 `HideForRecording()`。
- `scripts/ui/NotesDeckIntegration.cs` 接入统一录音取消路径；录音开始时会隐藏已打开 Deck，避免抢焦点或遮挡外部输入目标。
- RED：`Test-V2FeatureSuite.ps1` 在新测试 API 上按预期编译失败；GREEN：修复后 V2 专项重新通过。
- 最终命令结果：`BUILD_VIBE_MIC.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture self-test、`git diff --check` 均通过。
- Computer Use：普通构建实际启动；首页、便签页、独立 Deck 和 APP 入口均检查；Deck 关闭后返回主窗口。录音期间真实 RC003 抢焦点仍需硬件验收。
- Auditor：BLOCKER 0、HIGH 0（原 Notes Deck 录音竞态已修复）；保留 ChatGPT UIA、RC003、VB-CABLE、微信输入法、DPI、安装升级等人工验证项。

## Notes Deck 文档级 CAS 修复（2026-09-06，当前候选）

- 调查确认主窗和独立 Deck 共用同一 `NotesStore`，但新建、删除、恢复的 Load→修改→Save 过去缺少根级 revision 比较，存在旧快照覆盖新便签的风险。
- `NotesStore.TrySave` 现在在原子写入前重新读取当前文档并执行 CAS；旧快照返回 `NOTES-REVISION-CONFLICT`，不会覆盖磁盘。新增 `TestDocumentRevisionConflict` 并纳入 `Test-V2FeatureSuite.ps1`。
- 本轮 `BUILD_VIBE_MIC.cmd`、`npm test`、V2 focused suite、Host/Bridge/Capture self-test、冻结 Capture 双哈希和原生首页/便签/Deck smoke 均通过。
- 真实 RC003、VB-CABLE、微信输入法、ChatGPT UIA 输入框、DPI/主题矩阵和安装升级仍未验证；候选状态保持 `PASS WITH MANUAL HARDWARE CHECKS`，不声明正式稳定版。

## Notes Deck 本地提示词设置（2026-09-06，继续）

- 修复 F2 明确缺口：设置页原“恢复默认提示”仅为说明弹窗，现改为可编辑的“本地整理提示”文本框，支持保存和二次确认恢复内置提示。
- 新增 `AiPromptStore`（`ai-prompts.json`）：独立于 API Key、便签和录音配置，限制长度/控制字符，原子写入并保留未知字段；主文件损坏时从有效 `.bak` 恢复，保存或重置不会覆盖有效备份。
- `AiTextService` 在每次用户主动 AI 操作/连接测试时读取提示词；固定安全边界仍强制保留，不自动发送便签、不读取全库、不执行文本命令。
- 自动证据：`BUILD_VIBE_MIC.cmd`、`BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture self-test、`git diff --check` 均通过；正式候选仍未签名、未安装、未发布。
- 原生 UI 检查已通过 `--ui-smoke` 可访问性树确认首页可见；设置页在当前无交互 Computer Use 连接中未完成完整点击/滚动矩阵，记录为 `GUI=PASS（基础）/SETTINGS_PROMPT=NOT_RUN`，不能扩大为完整 DPI/主题通过。
- 真实 RC003、VB-CABLE、第三方语音工具、ChatGPT UIA、DPI/主题和安装升级仍未验证；当前候选结论保持 `PASS WITH MANUAL HARDWARE CHECKS`。

## Notes Deck 最新候选收口（2026-09-07）

- 最新工作区的两位只读 reviewer 复核结果为 BLOCKER 0、HIGH 0；旧报告中的数据恢复和 UI finding 已由当前代码与 focused tests 覆盖。
- 修复 provider/prompt 有效备份保留、AI operation 结果锁定、Focus 前台变化取消、Deck 固定位置/大小与工作区重夹、便签两栏最小宽度、键盘列表选择、搜索提示/预览一致性、读取失败状态复用和录音优先二次门禁。
- `BUILD_RELEASE.ps1`、`BUILD_VIBE_MIC.cmd`、`npm test`、`Test-V2FeatureSuite.ps1`、三组件 self-test、`Test-ReleaseArtifacts.ps1`、`git diff --check` 均通过；安装器和 ZIP 已按同批清单重建。
- Computer Use 实际检查最新构建首页、两栏便签页和独立 Deck。非置顶 Deck 截图出现外部像素浮层，切换置顶后消失；源码无对应图像资源，因此不归因于产品渲染。
- 结论仍为 `PASS WITH MANUAL HARDWARE CHECKS`。RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、浏览器、完整 DPI/主题、多显示器、睡眠蓝牙和安装升级卸载必须在真机补验；不签名、不 Push、不发布。

## 2026-09-07 最终入口与配置校验修复

- Notes、AI Provider、AI Prompt 三个新配置存储现在拒绝缺少或非法 `schemaVersion` 的 JSON，同时保留 `schemaVersion=0` 的既有 Notes 迁移。
- `npm start` 已指向 `START_VIBE_FLOW.cmd`；启动器在根目录缺少 Host 时会查找正式候选目录，避免用户误启动不存在的旧入口。
- README 首段已与 Notes Deck 范围一致，明确“便签 + APP 入口 + 用户主动 AI 整理”，不把旧项目/网页截图闭环当作当前主线。
- 新增 schema 缺失回退测试并加入静态启动入口门禁；本轮构建、自动测试、artifact gate 和 Computer Use 均通过。
- 结论仍为 `PASS WITH MANUAL HARDWARE CHECKS`；硬件、真实供应商、ChatGPT 输入框、完整 DPI/主题和安装生命周期不得写成已通过。

## 2026-09-07 APP 入口热键状态修复

- APP 入口热键冲突不再被保存成功文案掩盖：warning/error 会显示未生效、可能原因和修复动作。
- 新增 focused test 覆盖注册成功、冲突回滚成功、旧绑定恢复失败三种结果；冻结录音和输入路由未修改。
- 最新候选已重新构建、启动并检查首页、APP 入口页和遥控器状态窗口；仍未进行真实设备、真实热键占用、ChatGPT UIA、完整 DPI/主题和安装迁移验证。

## 2026-09-07 当前候选文案收口

- 可达的 `Context Deck` 入口已改名为“遥控器状态”，面板内部的旧“项目现场”行已改为 APP 入口标签。
- APP 入口页面和兼容执行回执不再把 `ProjectSpace` 术语作为 Notes Deck 主线展示；兼容类型、配置和执行边界保持不变。
- 最新候选已重新构建并由 Computer Use 检查首页、APP 入口页和遥控器状态窗口；旧主线文案未再出现在这些路径。
- 自动化、冻结身份和 artifact gate 均通过；真实硬件、provider、ChatGPT UIA、DPI/主题和安装迁移仍是 `NOT_RUN`。

## 2026-09-07 启动注册反馈

- 修复 `VibeMicForm.OnShown` 丢弃入口热键注册结果的问题。
- 启动时只有快捷键冲突或旧绑定回滚失败才进入统一 Toast/HUD 和反馈快照；正常启动保持静默，后台启动不强制抢焦点。
- `BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、Host/Bridge/Capture self-test、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1` 和 `git diff --check` 均通过。
- Computer Use 已启动最新候选并检查首页：窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”，首页明确显示“已准备好”和“先确认 APP 输入目标，再按住录音键说话，松开结束”。
- 最新候选哈希：Setup `126C9BDFABD27717FE6CD21E915F1CDDEDD650486D8311D37145E0DE5908E975`；ZIP `2D8D57E38EAB0B41CCF32D47630B4C13960A721CEC03149E30A919EACFC35E1E`。
- Capture 源码/二进制冻结哈希未变：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；真实 RC003、VB-CABLE、第三方语音工具、ChatGPT UIA、完整 DPI/主题和安装升级卸载仍未验证。

## 2026-09-07 最终候选数据与 UI 收口

- APP 入口保存层现已拒绝非法组合键格式，并复用运行时解析器；Notes AI 迟到结果在显示、复制、保存前均检查来源便签存在性、未删除状态和 revision。
- Deck 列表、标题、正文控件补充显式 UIA 名称；最新候选 Computer Use 实际看到“便签列表 / 便签标题 / 便签正文”。
- 最新候选 Setup SHA-256：`9D7D18DFCF71B4B3CE9FF6CB96D0ECD41B3F72CB7932B5F94CAD4658F4A81CED`；ZIP SHA-256：`F8AA154595295348B9E8F9C73B93A600FC0B890BB29201F46A51D2E835CD5F99`。
- `BUILD_RELEASE.ps1`、`npm test`、V2 focused suite、三组件 self-test、Release Identity/Artifact gate 和 `git diff --check` 均通过；只读 reviewer 当前 BLOCKER/HIGH/MEDIUM 均为 0。
- 仍未验证：真实 RC003/VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、完整 DPI/主题/多显示器、睡眠蓝牙恢复、安装升级卸载；候选未签名、未 Push、未发布。

## 2026-09-07 历史快捷键配置校验收口

- 稳定性 reviewer 发现旧 `project-spaces.json` 中 `quick-entry` 非法快捷键只校验长度，启动注册会静默跳过；已先加入失败回归，再在存储结构校验层复用运行时快捷键解析器。
- `ProjectSpaceStore.Load` 现在在无有效备份时返回具体 `QUICK-ENTRY-HOTKEY-INVALID`，有效 `.bak` 仍安全恢复；空快捷键和旧 `project` 数据保持兼容。
- 修复未修改 Capture、录音状态机、Raw Input、Hook、设备过滤、录音键映射或冻结参数。Capture 源码/二进制哈希仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- `BUILD_RELEASE.ps1`、`BUILD_VIBE_MIC.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、三组件 self-test 和 `git diff --check` 均通过。最新 Setup `A9826A1789FD7D1D4FCE7689674156B8B2C065A8BAF9FF18AC87876D4E2DE07E`，ZIP `45D2CBF11DE59AA5D32453A970C30F129BE476871F6ED8764253358E26280D65`。
- Computer Use 实际启动正式候选并检查首页、APP 入口、便签、独立 Deck；Deck 的 UIA 名称和关闭后主窗口恢复均已确认。ChatGPT 入口仍显示“输入框待确认”，未虚报聚焦成功。
- 稳定性 reviewer：BLOCKER/HIGH/MEDIUM 0；UX reviewer 无新的确定性阻塞。APP 入口备份恢复提示是可选 LOW，未扩大本轮范围。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、完整 DPI/主题、多显示器、安装升级卸载和签名仍未验证，候选未签名、未 Push、未发布。

## 2026-09-07 正式候选启动与最终门禁复核

- 停止上一轮 `--ui-smoke` 恢复夹具实例，并清理明确的 `release/tmp/ui-smoke` 与 `release/Vibe-Flow-Windows-x64/tmp/ui-smoke` 临时目录；未触碰用户配置目录。
- 首次重打包被上一轮子进程锁住 `NAudio.Core.dll`，结束由 Vibe Flow 启动的 `VibeMicAtvvCapture.exe` / `VoxDeckInputBridge.exe` 后重跑成功；这是构建环境锁，不是产品代码失败。
- `BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1` 均通过；三组件 self-test 在本次 release build 中通过。
- Computer Use 实际启动 `release/Vibe-Flow-Windows-x64/VibeFlow.exe`，首页显示“已准备好”“先确认 APP 输入目标，再按住录音键说话，松开结束”；正常状态没有恢复测试警告。窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”。
- 本批 `release/SHA256SUMS.txt`：Setup `4B32523190AF964494CA1F075903F54430E5B87CF563E4AED1001F0A4A119EC4`；ZIP `4305E1D39ACD9514C2D9318D5ED1C8FF5112C76248BD9DBF94D986F7C899BB70`。根 `VibeMic.exe` 与包内 `VibeFlow.exe` SHA-256 一致。
- Capture 源码/二进制冻结哈希仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA 输入框、真实 provider、完整 DPI/主题、多显示器、睡眠/蓝牙重连、安装升级卸载和代码签名仍为 `NOT_RUN`；候选未签名、未 Push、未发布。

## 2026-09-07 最终候选低风险提示修复

- 修复 APP 入口页面的恢复提示读取顺序：先完成主配置/备份加载，再渲染“已从备份恢复”提示，避免极窄时序下入口已恢复但提示遗漏。
- 仅修改 APP 入口页面源文件的 UI 组装顺序；未修改存储格式、热键注册、录音、Raw Input、Hook、设备过滤或 Capture。
- 本轮 `BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture self-test 和 `git diff --check` 均通过。
- Computer Use 实际启动正式候选并检查首页；显示“已准备好”，无恢复测试警告。候选根/包 Host 哈希一致。
- 本批 `release/SHA256SUMS.txt`：Setup `B34234DA4B54E80319653DA15EF2B778BDD10AA1C766DF534E35DA1CFBA49292`；ZIP `CE266B40EF04E9422ED265A0CE6E5FF7519FCF4E4B0292BD71428A2986D3A1A2`。

阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；只读 reviewer 结论为 BLOCKER/HIGH/MEDIUM 0，原 LOW 提示时序已修复。真实 RC003、VB-CABLE/第三方语音工具、ChatGPT UIA、真实 provider、DPI/主题和安装生命周期仍为 `NOT_RUN`。

## 2026-09-07 批量便签汇总增量

- 用户目标：在 Notes 管理页明确多选便签，查看发送范围后请求 AI 汇总；结果只能预览、复制或保存为新便签，不能覆盖原文。
- 真实路径：`scripts/ui/NotesPage.cs` → `NotesBatchSummaryForm` → `NotesBatchSummaryPolicy` → `NotesStore` / `AiTextService`。
- 最小完成标准：至少两条便签才能请求；请求和保存前验证来源 ID、删除状态与 revision；迟到结果不显示、不复制、不保存；结果保存 `sourceNoteIds`、`sourceRevisions`、`providerId`、`promptVersion`、`status`。
- 修改文件：`scripts/features/NotesBatchSummary.cs`、`scripts/features/NotesModels.cs`、`scripts/features/AiModels.cs`、`scripts/features/NotesStore.cs`、`scripts/ui/NotesBatchSummaryForm.cs`、`scripts/ui/NotesPage.cs`、`scripts/tests/NotesDeckAiTests.cs`、`scripts/tests/Test-V2FeatureSuite.ps1`、`BUILD_VIBE_MIC.cmd`。
- 已修复问题：WinForms 汇总窗口 Dock 顺序导致标题/来源/结果阅读顺序反转；现已按标题、范围说明、来源列表、结果预览显示。
- 构建与测试：`cmd /c BUILD_VIBE_MIC.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`BUILD_RELEASE.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture `--self-test`、`git diff --check` 均通过。一次 Focus 测试受前台窗口竞态影响失败，随后单独重跑及发布构建均通过，未修改冻结路径。
- Computer Use：正式候选首页、Notes 页、两条复选框选择、按钮启用状态、汇总窗口来源范围、无 Provider 警告和取消不重试均已实际检查；未发起真实 Provider 请求。
- Reviewer：UX/spec reviewer 发现取消竞态 MEDIUM，已通过独立请求代次/取消意图门禁修复；稳定性 reviewer `BLOCKER 0 / HIGH 0`，当前未发现录音、Raw Input、Hook 或配置安全回归。
- Release gate：`PASS WITH MANUAL HARDWARE CHECKS`。Setup SHA-256 `910A36075F3777F3A8696D2566212FEBD392ECCBEBA839C5D797A200550FDC32`；ZIP SHA-256 `127C17A136202E72D7FCE2AA51DE9E72AD3E309CFBA1C797E101E070BF18CB42`。
- 仍需真机验证：RC003、VB-CABLE、第三方语音工具、ChatGPT UIA 输入框、真实 Provider、DPI/主题、安装升级卸载、签名。

## 2026-09-08 最终收尾复核

- 重新运行 `npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、`BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd`、Host/Bridge/Capture self-test 和 `git diff --check`，均通过。
- 最终复核发现两处不可达的旧向导/自检文案仍暗示“单击录音键、再按一次结束”，已统一为“按住录音键，松开结束”；未修改录音状态机、Capture、Raw Input、Hook 或设备过滤。
- 社群二维码与用户提供源图 SHA-256 一致：`AC71A77366152CE38D14AC049B283B41142EBCDCFB782613036C03EDD85B317D`。
- 截图自动化已切换到当前六页导航并成功生成 `02-notes.png`；发布清单和 artifact 测试同步包含该截图。最终候选 SHA-256：Setup `9F8E651EF52D36ABE098C1E329E0AA975132415221DB32976360A7E080B83B50`，ZIP `4C9D398F0E96870FB21BE1FC1D117A3A6591A6F8035853BEDEA923B98FB578A2`。
- 当前本地 `VibeMic.exe` 已启动，窗口标题为“言灵 · Vibe Flow Remote · V2.0.0”；快捷键独立导航、便签页和 Notes Deck 可达。Computer Use 工具在本轮未暴露，沿用此前实际 UI 检查证据，不扩大验证范围。
- Capture 源码/二进制冻结哈希仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。真实 RC003、设备级过滤器、VB-CABLE、第三方语音工具、ChatGPT 输入框 UIA、完整 DPI/主题及安装升级卸载仍未验证；候选未签名、未 Push、未发布。

## 2026-09-08 录音结束后输入目标与设备过滤器复核

- 用户反馈：松开录音键后文字落入剪贴板而非目标输入框，应用内发送/确认动作似乎无效，首页同时提示 RC003 设备级按键过滤器未就绪。
- 真实路径调查：`HandleVoiceWakeRequest` 只负责唤起稳定语音链路，不会在录音键事件中调用 Smart Focus；录音键仍保持按住开始、松开结束，避免录音期间抢焦点。默认 `focus-targets.json` 的 `defaultTargetId` 为空，现有条目是历史 smoke 目标，不是 ChatGPT 的已验证输入框。
- 日志证据：最近真实运行记录显示 RC003 音频有真实帧、排空完成、微信工具面板与提交动作已派发；当前没有 Vibe Flow 写入普通转写文字剪贴板的代码路径。无法据此证明 ChatGPT 输入框收到文字，故不把 provider 的外部剪贴板/回填行为误报为 Vibe Flow 成功或失败。
- 输入路由结论：当前问题属于“输入目标未学习/未验证”和第三方语音工具未取得目标焦点的未验证闭环；语音页已提供“设置输入目标/锁定输入目标”，失败时明确显示“未锁定输入目标，未发送按键”。录音键不自动调用 Smart Focus，符合冻结录音和焦点保护契约。
- 设备路由结论：`input-bridge-health.json` 报告 `rc003_filter_available=false`、`rc003_filter_healthy=false`，并曾记录 `fallback:open_failed_win32_2`；桥接回退到 Raw Input，前台应用可能收到 F5/录音键等原始按键。不能用全局 Hook 代替设备级过滤器，也不能未经用户授权安装驱动或修改系统设备。
- 本轮代码状态：录音/输入/设备过滤冻结文件未修改；当前 Host 已区分“音频已送达但未确认发送”和“等待语音工具处理”，自检不再把未验证输入目标显示为通过。
- 审计发现：release 候选包曾落后于根目录二进制，需停止 smoke/宿主进程后重新构建并运行 artifact gate；在候选包与根文件 SHA-256 对齐前，不得使用该包做发布声明。
- Computer Use：本轮工具未提供可用的原生交互通道，未执行 ChatGPT UIA 学习、真实微信/ChatGPT 输入落点或 RC003 设备级过滤器人工验证；这些项目保持 `NOT_RUN`。
- 下一步：重建根组件和 release 候选，运行全部自动测试与 `vibeflow-release-gate`；随后由用户在 ChatGPT 前台手动完成“设置输入目标 → 立即测试 → 锁定输入目标 → 录音 → 目视确认文字”验证。设备级过滤器若要彻底消除前台按键副作用，需要用户明确授权后再进行安装/启用与真机验证。

## 2026-09-08 用户反馈复核：剪贴板落点与 RC003 过滤器告警

- 使用 Computer Use 实际启动并检查当前 `VibeMic.exe`：语音页显示“尚未设置输入目标 · 不会自动切换 APP”，Smart Focus 对话框可见，但当前只保存历史 `focustargetsmokeapp` 目标，`defaultTargetId` 为空；因此没有可用于 ChatGPT 的已验证输入控件。
- 当前语音运行日志确认：RC003 音频帧、排空、WeChat 面板唤起和提交动作均有真实记录；Vibe Flow 没有普通转写文字的剪贴板写入路径，也无法据此证明 ChatGPT 输入框已收到文字。用户看到的剪贴板落点属于未锁定/未验证目标时第三方语音工具的外部行为，不能通过修改 Capture 或录音状态机绕过。
- 首页告警与音频链路分离：`input-bridge-health.json` 显示 `rc003_filter_healthy=false`、`rc003_filter_state=fallback:open_failed_win32_2`、`routing_isolation=native_passthrough`；Capture/ATVV 仍为 READY。该告警表示 RC003 原始按键可能继续传给前台应用，不表示麦克风录音失败。
- 真实 UI 检查结果：语音页、Smart Focus 目标管理器和首页告警均可达，未发现应用内“发送”按钮可替代 ChatGPT 的外部确认动作；Vibe Flow 只负责唤起并派发语音工具动作，最终文字和发送必须在已验证目标中由用户目视确认。
- 本轮未改动 Capture、录音 generation、Raw Input、Hook 或设备过滤逻辑；未安装驱动或修改系统设备。ChatGPT 输入框 UIA 学习、微信/ChatGPT 文字落点、RC003 过滤器安装仍为 `NOT_RUN`，需用户手动配合或明确授权。
- 本轮自动门禁：`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`VibeMicAtvvCapture.exe --self-test`、Capture SHA-256 复核和 `git diff --check` 均通过；正常 Vibe Flow 进程保持运行。由于本轮没有产品源码变更且现有候选进程正在运行，未强制停止用户当前会话重编 EXE。
- 本轮 release gate：**PASS WITH MANUAL HARDWARE CHECKS**。阻塞项不是录音或麦克风，而是 ChatGPT 输入目标尚未学习/验证，以及未获授权的 RC003 设备过滤器安装和真实按键副作用测试。

### 本轮收口证据

- 配置迁移修复：旧 schema 的 Home、TV、功能键、确认键和方向自定义动作只在缺失时补默认；旧单字段动作会迁移到新的短按字段；带 schema 但缺少后续字段的可读配置由 `MigrateConfig` 补齐，无备份也不丢失已有 provider/mapping；新增 Host 自检覆盖。
- 自定义按键识别保存失败时恢复原内存对象，避免一次失败写入污染后续保存；Host 自检、V2 focused suite 和构建均通过。
- 最终命令结果：`BUILD_RELEASE.ps1`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1`、Host/Bridge/Capture `--self-test`、`git diff --check` 均通过。`BUILD_RELEASE.ps1` 日志明确为 `Vibe Flow host self-test passed`。
- 最终根/包 Host SHA-256：`1B06D9981A5ADF54D8AD3C26DC3EF9C7BF7FC8F0E1EBF4144C01D0046B89DC2F`；Bridge SHA-256：`C1A6FE5B8DEE77A981510FF27FBCAE786911727A8AD0C606F5E523848A6FC4F7`。根与包一致。
- Capture 源码/二进制及根/包 SHA-256 仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，未改动、未重签。
- 只读 reviewer：当前 BLOCKER/HIGH/MEDIUM 均已处理或降为手工硬件风险；release gate 结果为 **PASS WITH MANUAL HARDWARE CHECKS**。设备过滤器不可用的 Raw Input 直通叠加风险仍不能靠源码测试消除。
- 当前候选安装包未签名、未 Push、未发布。由于本轮 Computer Use 工具未提供可用的 ChatGPT 桌面交互，真实 ChatGPT UIA 学习、微信/ChatGPT 文字落点、RC003 过滤器安装和真实按键副作用仍为 `NOT_RUN`。

## 2026-09-08 ChatGPT 输入目标学习复核

- 用户再次确认：微信输入法处理后，文字仍进入剪贴板而不是 ChatGPT 输入框。
- 本轮实际启动当前 `VibeMic.exe` 并打开“语音 → 设置输入目标”。界面显示目标应用可选 `ChatGPT (chatgpt)`，但当前唯一目标仍是历史 `focustargetsmokeapp`，`defaultTargetId` 为空；语音页明确显示“尚未设置输入目标 · 不会自动切换 APP”。
- 已启动一次 ChatGPT 目标学习，但未捕获到 ChatGPT 编辑控件；未写入半成品目标，随后关闭学习对话框。没有读取、保存或写入普通转写文字剪贴板的 Vibe Flow 路径。
- 运行日志仍证明 RC003 音频帧、排空、微信面板唤起和提交动作正常；因此当前剪贴板落点仍属于未锁定/未验证输入目标或第三方工具回退行为，不能通过修改 Capture、录音键状态机或全局 Hook 绕过。
- 用户下一步必须手动完成：选择 `ChatGPT (chatgpt)` → 点击“学习输入目标” → 切到 ChatGPT 单击底部消息输入框并保持约 1 秒（不发送内容）→ 返回 Vibe Flow 等待捕获 → “立即测试”→ 勾选“保存后设为默认语音目标”并保存→ 点击“锁定输入目标”。
- ChatGPT UI Automation、微信/ChatGPT 实际文字落点、设备级过滤器安装和 RC003 原始 F5 隔离仍标记 `NOT_RUN`；本轮没有修改产品代码、Capture、Raw Input、Hook 或设备过滤。

## 2026-09-08 再次复现：ChatGPT 桌面端仍出现剪贴板落点

- 运行日志在 `21:23:14`、`21:23:19`、`21:23:47` 三次会话均记录 `VOICE INPUT TARGET ready=true target_id=implicit-chatgpt`；对应当前焦点为 ChatGPT 进程的 `ControlType.Edit` / `ProseMirror`，但该目标仍是一次性观察结果，没有写入 `focus-targets.json`。
- 同三次会话的 Capture 日志均为 `WETYPE TOOLBAR CLICK phase=start`、`WETYPE PANEL READY`、真实音频帧、排空完成和 `WETYPE TRANSCRIPTION SUBMIT sent=True`。没有 `WETYPE HOTKEY TAP`，说明冻结 Capture 每次都命中了固定工具栏分支；Capture 源码/二进制哈希没有变化。
- `VibeMicAtvvCapture` 在 RC003 自然音频流内直接启动 provider；Host 的 `HandleVoiceWakeRequest`/Smart Focus 只能观察焦点，不能在不修改冻结 Capture 的前提下阻止 provider 或把第三方转写文字重新粘贴。Bridge 也不能安全拦截这条 0x04 音频流，否则会同时破坏录音。
- 当前 WeType 为 `2.1.3.18`，工具栏窗口类为 `wetype.statusbar.window`，冻结 Capture 使用固定比例点击。Vibe Flow 未发现普通文字剪贴板写入、读取或回填路径，因此“文字进入剪贴板”目前只能归因于 WeType/ChatGPT 桌面端的外部交付或兼容性，尚未有真机证据证明是 Smart Focus 写入。
- 本轮不修改 Capture、Host/Bridge 调用参数、Raw Input、Hook、设备过滤或录音状态机。继续修改这些组件来绕过第三方回退会违反冻结契约。
- 待用户手动完成的最小判别：在记事本、ChatGPT 浏览器网页和 ChatGPT 桌面端分别保持编辑框焦点，使用同一套 WeType 语音输入；记录哪一个目标能直写、哪一个目标回退剪贴板，并确认 WeType 进程与 Vibe Flow/目标应用的权限级别一致。该项未完成前，不能声称 ChatGPT 桌面端已修复。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。Vibe Flow 自身的录音、焦点观察和日志边界保持稳定；ChatGPT 桌面端的 provider 文字落点仍为未验证外部兼容性问题。
- 本轮稳定性门禁：`npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`、`VibeMicAtvvCapture.exe --self-test` 和 `git diff --check` 均通过；Capture 源码/二进制 SHA-256 仍匹配冻结值。仅修改本进度文档，未重建或替换运行中的产品二进制。
- 当前 Bridge 健康证据：Raw Input 与 RC003 音频设备状态为 `ready`，但设备级过滤器为 `fallback:open_failed_win32_2` / `rc003_filter_healthy=false`；这解释前台按键副作用告警，不代表麦克风链路失败。过滤器安装或系统级修复仍需用户明确授权。

## 2026-09-08 Smart Focus 前台一致性修复

- 根因确认：无持久化默认目标时，Host 会读取 UI Automation 当前焦点作为一次性 ChatGPT fallback；仅凭 `FocusedElement` 可能在窗口已切走后仍拿到旧的 ChatGPT 编辑元素，导致目标状态被误报为已就绪。
- 先加入失败自测，再实现最小修复：`IsImplicitChatGptTargetForForeground` 现在同时要求当前前台进程规范化为 `chatgpt`，并通过既有的 `ControlType.Edit`、`ProseMirror`、可写和键盘焦点检查；不激活窗口、不保存目标、不读文字或剪贴板。
- `ObserveVoiceInputTargetBeforeProviderStart` 已记录并校验前台进程；窗口切换到其他应用时返回 `FOCUS-PROCESS-MISMATCH`，仍保持录音继续但不宣称目标已锁定。
- 本轮构建与自动验证：`cmd /c BUILD_VIBE_MIC.cmd`、`npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、`git diff --check` 均通过。首次构建遇到运行中 `VibeMic.exe` 文件锁，停止本地 Host 后重建成功。
- 原生 UI：已启动当前根目录 Host 并执行 `scripts/capture-ui-screenshots.ps1 -ProcessId 14908 -AllowUnhealthyDiagnostics`；首页和语音页可见，输入目标未配置时显示警告，不虚报 ChatGPT 已锁定。Computer Use 原生交互工具本轮未暴露，因此未完成真实 ChatGPT UIA 学习、WeType 文字落点和设备级过滤器人工复测。
- 冻结边界：`scripts/VibeMicAtvvCapture.cs`、Capture 二进制、录音 generation、Raw Input、Hook、设备过滤逻辑均未修改；冻结 Capture 源码/二进制 SHA-256 仍为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 审计：只读 reviewer 未发现本修复引入的 BLOCKER/HIGH；设备级过滤器 fallback 造成的前台按键副作用仍是需授权安装驱动后才能验证的条件性风险。release gate 结果：**PASS WITH MANUAL HARDWARE CHECKS**。
- 仍需用户真机复测：在 ChatGPT 前台点击消息输入框，先完成“设置输入目标 → 立即测试 → 保存为默认 → 锁定输入目标”，再按住 RC003 录音并目视确认文字是否进入输入框。若 WeType 仍仅写剪贴板，应记录其工具栏/快捷键输出模式，不能由 Vibe Flow 读取剪贴板回填。

## 2026-09-09 录音后剪贴板落点竞态修复

- 用户反馈仍出现“松开录音键后文字进入剪贴板”。只读追踪确认 Host 的录音停止 cue 线程曾在 WeType 异步提交前调用 `RestoreVoiceTargetFocusForSubmission()`，会改变提交焦点并触发第三方工具的剪贴板兜底。
- 先加入 `scripts/validate.js` 回归断言并确认改动前红灯；随后移除 Host 的目标快照、停止后 UI Automation 恢复、800 ms 节流状态和录音路径调用。显式“锁定输入目标”按钮、UIA 验证、WeType 原有工具栏提交队列保持不变。
- 未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、Bridge、Raw Input、键盘 Hook、设备过滤、录音 generation 或配置 schema。冻结 Capture 源码/二进制 SHA-256 仍匹配基线。
- 验证：`cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、`BUILD_RELEASE.ps1`、Release Identity/Artifacts 和 `git diff --check` 均通过。实际启动同批 `release\\Vibe-Flow-Windows-x64\\VibeFlow.exe`，窗口标题为 `言灵 · Vibe Flow Remote · V2.0.0`；首页、按键、语音、自检、设置和首次设置截图成功生成。
- 设备过滤器仍为 `fallback:open_failed_win32_2` 时，RC003 F5 可能泄漏到前台并刷新页面；这是设备级驱动缺失的条件性风险，不能通过修改冻结录音或全局 Hook 绕过。当前真实 RC003、VB-CABLE、WeType 文字落点和 ChatGPT 桌面端端到端仍为 `NOT_RUN`。
- 阶段结果：功能修复自动化验证通过，但整体候选仍受设备过滤器和第三方 WeType 真机验证限制；需要在 ChatGPT 前台先“锁定输入目标 → 立即测试”后再进行真实录音复测。
- 最新只读 auditor 复核确认快照/恢复代码已移除；但设备级 RC003 过滤器未随候选包安装，Raw Input fallback 仍可能造成前台 F5 副作用，整体稳定性门禁暂记 **FAIL / BLOCKED BY HARDWARE FILTER**。安装或构建签名过滤器需要用户明确授权和真实设备验证，不能由本轮自动执行。
- 本批候选校验清单：`release/SHA256SUMS.txt` 中 Setup 为 `80F72A91FC398FB10881CF74096F69B76EF9CEFB96F84651F57182E16F952F05`，ZIP 为 `07015CBD8CB08056B582C08570BF02A00A7EFDCC6DF0A8A22197EFA396B79FFB`。

## 2026-09-09 文本入框竞态与反馈界面增量优化

- 问题清单：WeType 音频、工具栏开始/提交均有真实回执；剪贴板落点只在目标失焦或进程不匹配时出现。Host 先前在录音释放事件中再次执行 UI Automation `SetFocus`，可能与 WeType 异步提交竞争；设备过滤器仍为 `fallback:open_failed_win32_2` 时，RC003 F5 也可能刷新前台页面并丢失光标。
- 修复动作：移除 Host 的 `voiceKeyReleasedEvent`、录音后焦点快照和 `ExecuteForVerification` 恢复路径；录音仍仅按住开始、松开结束，Smart Focus 只允许用户显式锁定，松开不发送 Enter、不读取或回填文字。新增 `validate.js` 回归断言防止该路径回归。
- 视觉动作：Hero 电平不再使用 50 ms 正弦假波形，改为由真实 `AUDIO LIVE START`/已测 `output_rms_pct` 驱动的静态指示；快捷键页文案改为“管理遥控器实体键动作；录音键保持独立”，动作按钮增加可访问名称和完整动作 tooltip；语音页分别显示 CABLE Input 播放端、CABLE Output 录音端及当前播放端点；截图脚本补齐项目标签变量。
- 修改文件：`scripts/VibeMic.cs`、`scripts/validate.js`、`scripts/capture-ui-screenshots.ps1`、`docs/V2_0_PROGRESS.md`。未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、Bridge、Raw Input、Hook、设备过滤或配置 schema。
- 自动验证：`npm test`、`cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd`、`scripts/tests/Test-V2FeatureSuite.ps1`、`VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、`VibeMicAtvvCapture.exe --self-test`、`Test-DevelopmentBuild.ps1`、`Test-ReleaseIdentity.ps1` 均通过。一次并行构建因本地 UI smoke 进程锁定 EXE 失败，停止该测试进程后单独重跑通过。
- 原生 UI：使用同批根目录 `VibeMic.exe --ui-smoke` 实际启动并生成 `tmp/ui-captures-v2/01-overview.png`、`02-dictation.png`、`03-shortcuts.png`、`04-diagnostics.png`、`05-settings.png`；语音页的两端点状态和快捷键页独立导航、固定录音键均可见，未发现文本遮挡。Computer Use 工具未提供可操作的 ChatGPT/WeType 硬件通道。
- 仍需真机验证：ChatGPT/Chrome 输入框中的第三方文字直写、RC003 设备级过滤器安装与 F5 隔离、VB-CABLE 真实收音、蓝牙重连/睡眠恢复、DPI 100/125/150/200%、安装升级卸载。不能据此声称剪贴板问题已在真实 WeType 目标上完全解决。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；核心 Host/Bridge/Capture 自动门禁通过，设备过滤器和第三方文字落点仍是外部验证阻塞项。

## 2026-09-09 最终隔离候选构建

- 根目录 Host/Bridge 已按当前源码重建；由于旧 `release\\Vibe-Flow-Windows-x64` 仍有用户启动进程占用，未强制停止或覆盖该目录。
- 在隔离目录 `tmp\\v2-candidate-root-5a69e81ae287405a8cef40851affc266` 生成当前候选 EXE、ZIP 和 Inno Setup 安装器；`Test-ReleaseArtifacts.ps1 -Root` 对该隔离候选通过。
- 候选 SHA-256：`VibeFlow-Setup.exe` = `31ABFD1EEE2315414BF26E200E6D16DC81258A5014628178EC4F775EE11C225F`；`Vibe-Flow-Windows-x64.zip` = `171424E085B53B08C4D4EB10F9F13B6DA0B8F456E3E6663F767AD213EF863F77`。
- 额外回归：`npm test`、V2 focused suite、Host/Bridge/Capture self-test、安装器需求和 `git diff --check` 均通过；标准 `release` 目录制品因运行中旧实例未重写，仍需用户关闭旧实例后再由正式脚本覆盖并复验。
- 最终 release gate：**PASS WITH MANUAL HARDWARE CHECKS**。冻结录音/输入契约未发现新的 BLOCKER/HIGH；ChatGPT/WeType 文本直写、RC003 设备级过滤器、VB-CABLE、蓝牙恢复、DPI 与安装升级仍是未验证项目。

## 2026-09-09 录音焦点锁生命周期修复

- 根因：Smart Focus 的后台观察每约 700 ms 重新读取当前 UI Automation 焦点。WeType 浮层在录音期间获得焦点后，旧逻辑会解除已验证的 `Local\\VibeMicFocusTargetLocked`，使无设备级过滤器环境下 RC003 F5 的 legacy 键盘消息重新穿透前台。
- 修复：`scripts/VibeMic.cs` 现在在录音键保持或录音状态期间保留已建立的目标锁；只在空闲时继续观察并解除失效目标。重复的“锁定成功”日志也只在状态从未锁定变为锁定时记录，避免刷新线程制造误导性噪音。没有新增焦点抢占、剪贴板读取、文字回填或 Enter 发送。
- 回归测试：`FeatureSurfaceTests` 先以缺失策略红灯，加入 `ShouldPreserveVoiceFocusLock` 实现后通过；`scripts/tests/Test-V2FeatureSuite.ps1` 全部通过。
- 构建与运行：`cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd` 成功；当前根目录 `VibeMic.exe`、`VoxDeckInputBridge.exe` 于 07:03 启动，Capture 保持原 `1.2.1.0` 二进制并在 07:03:41 报告 `CAPTURE READY`。
- 冻结证据：Capture 源码 SHA-256 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`，二进制 SHA-256 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，均未改动。
- 仍需真机验收：当前机器的 RC003 设备过滤器仍为 `fallback:open_failed_win32_2`，没有设备级身份时无法安全拦截普通键盘 F5；ChatGPT/WeType 的最终文字落点、设备过滤器安装、蓝牙和 VB-CABLE 仍未在本轮声称通过。release gate：**PASS WITH MANUAL HARDWARE CHECKS**。

## 2026-09-09 当前焦点自动锁定增量修复

- 用户问题：未保存默认目标或 Smart Focus 观察失效时，录音前的 ChatGPT 输入框没有建立焦点锁；RC003 F5 在设备过滤器不可用时可能穿透到前台，导致光标丢失，WeType 随后可能回退到剪贴板。
- 修复动作：Host 的后台焦点观察现在先读取当前 `AutomationElement.FocusedElement`，仅接受当前前台进程中的可写 `Edit` 控件、键盘焦点和稳定 UI Automation 描述符；ChatGPT 的 `ProseMirror` 适配器可在没有持久化默认目标时使用一次性目标。该目标只保存在内存，不读取控件文字、不写入或读取剪贴板、不改变录音状态机。
- 录音优先：观察到的临时目标通过既有 `Local\\VibeMicFocusTargetLocked` 令牌保护录音期间的 F5 legacy 消息；录音和 WeType processing 阶段保持锁定，空闲时焦点离开才解除。录音键仍按住开始、松开结束，松开不发送 Enter。
- 反馈：语音页在有证据时显示“当前焦点已验证：ChatGPT 输入框”，失焦时恢复为已保存目标或“尚未设置输入目标”；日志只记录目标 ID、策略和状态。
- 自动验证：`npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`、`Test-DevelopmentBuild.ps1`、Host/Bridge/Capture `--self-test`、`git diff --check` 均通过。构建期间曾因运行中 Bridge 文件锁失败，停止本轮自启动的三个进程后重跑通过。
- 原生 UI：使用当前根目录 Host（07:41 启动）生成 `tmp/ui-captures-focus-fix-final`；首页、语音页、独立快捷键页、自检和设置页均可达，语音页显示 `ChatGPT 输入框 · 已验证`，未观察到 UI 刷新抢焦点。Computer Use 原生交互通道不可用，截图不能替代真实 RC003 按键和 WeType 文字落点验证。
- 当前真实运行证据：`vibe-flow-host.log` 记录 `VOICE INPUT TARGET ready=true target_id=foreground-chatgpt source=focused_observation_transient`；Capture 记录 `ATVV READY`。这证明 Host 已识别焦点并建立保护，不证明第三方 WeType 已把文字写入 ChatGPT。
- 未验证/阻塞：`input-bridge-health.json` 仍为 `routing_authority=raw_input`、`routing_isolation=native_passthrough`、`rc003_filter_healthy=false`、`rc003_filter_state=fallback:open_failed_win32_2`；ChatGPT + WeType 最终文字落点、RC003 过滤器安装与普通键盘 F5 隔离、VB-CABLE、蓝牙恢复和 DPI 仍为 `NOT_RUN`。不能通过修改 Capture 或剪贴板回填绕过。
- 阶段结果：**FAIL / BLOCKED BY HARDWARE FILTER**。Host 侧焦点观察竞态已修复并自动验证，但设备级过滤器和第三方文字落点仍需用户授权/真机复测后才能升级为可交付闭环。

## 2026-09-09 录音前 Smart Focus 握手修复（后续审计已否定其作为录音门禁）

- 只读 auditor 确认上一版仍存在根因：Bridge 先设置 `Local\\VibeMicVoiceKeyPressed`，冻结 Capture 随即启动 WeType；Host 后续才处理 `VoiceWakeRequested`，所以 Smart Focus 不是录音前门禁。另一个问题是 Host 的 `FocusTargetLocked` 事件此前没有 Bridge 消费，且空闲焦点观察函数没有生产调用方。
- 修复动作：Bridge 现在先请求 Host 的被动 UIA 前台验证，等待有界的 `Local\\VibeMicVoiceFocusReady` / `Local\\VibeMicVoiceFocusRejected` 结果，再决定是否向 Capture 派发一次录音按下事件。目标未验证、用户已松开、Host 超时或关闭时，本次不派发，避免在未知焦点下触发第三方输入法的剪贴板回退。
- Host 每次物理录音唤醒都会重新验证当前前台可写 `Edit` 控件，不再把旧锁当作当前焦点证明；空闲 `PollActivity` 重新接入被动 `PollFocusTargetForVoiceLock`，录音和语音工具处理期间不解除已验证状态，空闲切窗或目标失效后才撤销。
- 保护边界：未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、录音 generation、Raw Input、键盘 Hook 或设备过滤器；没有新增 Clipboard 文字读取/回填、自动 Enter、点击录音或第二套状态机。握手运行在 Bridge 工作线程，不阻塞低级 Hook 回调。
- 自动验证：`BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd`、`npm test`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、冻结 Capture 双哈希和 `git diff --check` 通过；新增 focused test 和静态门禁覆盖“焦点结果先于 Capture 派发”及 fail-closed 判定。
- 原生 UI：同批 `VibeMic.exe --ui-smoke` 已启动并生成 `tmp/ui-captures-focus-gate`，语音页、快捷键、自检和设置页可达，未观察到录音前窗口刷新或布局遮挡。Computer Use 桌面交互通道当前不可用。
- 未验证/阻塞：真实 RC003 按住/松开、ChatGPT/WeType 最终文字直写、设备级 RC003 过滤器安装与 F5 隔离、VB-CABLE、蓝牙重连/睡眠恢复、DPI 和安装升级仍需真机。当前 `input-bridge-health.json` 仍为 `rc003_filter_healthy=false` / `fallback:open_failed_win32_2`，因此不能声明普通键盘与 RC003 的物理 F5 已完全隔离。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**；自动化和 Host/Bridge 边界已收口，真实硬件和第三方文字落点继续保持 `NOT_RUN`，不得据此宣称 ChatGPT 已收到文字。

## 2026-09-09 V1.5 录音边沿恢复与 RC003 F5 回退修复

- 第二轮只读审计确认：上述 Focus lease 存在旧线程迟到、按下代际混用、检查后 Reset 竞态，并且冻结 Capture 的自然 ATVV 音频帧仍可绕过 Bridge lease；因此它不能作为录音门禁，继续保留会延迟或丢失 V1.5 的首个音频边沿。
- 根因修复：`scripts/VoxDeckInputBridge.cs` 恢复 V1.5 的顺序，录音键 DOWN 立即设置 `VoiceKeyPressed`，随后才发送 Host 被动观察通知；录音键 UP 仍只结束当前 held generation。移除了 Bridge 侧无效 lease 等待和重复预检线程，避免 stale generation、关闭悬挂和 Capture 自然起流绕过。
- 前台焦点保护：当前机器设备级过滤器仍为 `fallback:open_failed_win32_2` 时，低级 Hook 对“已发现 RC003 + 持久化录音扫描码一致 + voice mapping”的 F5 走受限 V1.5 兼容回退并吞掉该边沿，防止 ChatGPT/浏览器收到刷新键；过滤器健康或未发现 RC003 时保持普通键盘 passthrough。该回退仍不能替代签名设备过滤器，普通键盘 F5 在过滤器缺失时需要真机复测。
- Smart Focus：Host 的 UIA 观察保留为被动验证和显式“锁定输入目标”能力；UIA 瞬时 null/失效现在转换为 `FOCUS-*` 失败结果，不再退出唤醒监听。冷启动录音在未验证输入目标时不启动 Capture；已运行 Capture 不被新逻辑强制重启，保持自然 ATVV 录音契约。
- 修改文件：`scripts/VoxDeckInputBridge.cs`、`scripts/VibeMic.cs`、`scripts/validate.js`、`scripts/tests/FeatureSurfaceTests.cs`、`docs/V2_0_PROGRESS.md`。未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、录音参数、配置 schema、VB-CABLE 或系统设备。
- 自动验证：`cmd /c BUILD_VIBE_MIC.cmd`、`cmd /c BUILD_INPUT_BRIDGE.cmd`、`npm test`、`node scripts/validate.js`、`scripts/tests/Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、Capture 双 SHA-256、`git diff --check` 通过。真实 RC003 + WeType + ChatGPT 文字直写仍未在本机完成端到端复测。
- 阶段结果：**PASS WITH MANUAL HARDWARE CHECKS**。代码侧已消除本轮发现的 lease 竞态和 F5 穿透回退缺口；设备过滤器、普通键盘隔离和第三方文字最终落点仍必须用真实设备复测，不能据自动化结果宣称“剪贴板问题已完全解决”。

## 2026-09-09 Provider 就绪焦点恢复与 Host 恢复检查

- 用户目标：继续解决 Smart Focus 失效后第三方语音工具把文字落到剪贴板的问题，并保持 V1.5 按住录音、松开结束边沿。
- 根因证据：Capture 日志的 `TRANSCRIPTION READY` 发生在 WeType 面板接管前台之后；Host 原有恢复只在后续音频 cue 到达时执行，存在 provider 面板夺焦窗口。另 `EventWaitHandle.Set()` 的返回值不代表 Host 进程仍在监听，Bridge 可能在 Host 已退出时误以为已唤醒。
- 修复动作：`scripts/VibeMic.cs` 在 `TRANSCRIPTION READY` 的实时反馈路径中，仅当已有已验证焦点锁且语音会话仍活跃时恢复 ChatGPT 可写编辑控件；不读取文字、剪贴板，不发送 Enter，不改变 Capture。`scripts/VoxDeckInputBridge.cs` 在录音按下后独立检查本目录 Host 进程，缺失时才恢复启动；不改变 Hook、Raw Input 或录音边沿。
- 新增回归：`FeatureSurfaceTests` 覆盖 provider-ready 恢复只在活动焦点锁内执行；Bridge 原有 Host 恢复策略 self-test 保持通过。
- 构建与自动测试：`BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd`、`Test-DevelopmentBuild.ps1`、`npm test`、`node scripts/validate.js`、`Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、`Test-ReleaseIdentity.ps1` 均通过。
- 原生运行证据：最新根目录 `VibeMic.exe` 已启动，ChatGPT 桌面端 UIA 实际暴露唯一可写 `Edit`：`ClassName=ProseMirror ProseMirror-focused`、`ValuePattern.IsReadOnly=false`；这证明当前适配器可见，不证明第三方 WeType 已把真实文字写入目标。
- Release gate：**PASS WITH MANUAL HARDWARE CHECKS**。Capture 源码/二进制冻结哈希保持 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` / `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- 未验证/阻塞：当前 `input-bridge-health.json` 仍为 `rc003_filter_healthy=false`、`fallback:open_failed_win32_2`、`routing_isolation=native_passthrough`；RC003 F5 仍可能原生刷新前台。真实 RC003、VB-CABLE、WeType/ChatGPT 最终文字落点、设备过滤器安装、蓝牙恢复和 DPI 仍需人工真机验证；不能声称剪贴板问题已完全根治。

## 2026-09-10 落地第 1 项：虚拟麦克风“设备类别”能力（让语音输入法看到 CABLE Output）

- 用户目标：开始落地 V2.0 升级，第一项是把“连接稳定性 + 配置成本”里最贵的一环——第三方语音输入法看不到遥控器麦克风——变成应用自己的能力，而不是让用户手改系统。
- 已核实根因：Windows 把虚拟声卡的录音端 `CABLE Output` 报告为 `LineLevel`（form factor 2），而只列出“麦克风/耳机”的语音输入法（豆包输入法等）因此不显示它；真实麦克风为 4、耳麦为 5。注册表对应的属性 ACL 只允许 SYSTEM/Audiosrv/AudioEndpointBuilder/TrustedInstaller，直接写注册表会被拒绝，但 Core Audio 的端点属性存储对普通中等完整性进程可写（本机两次 PoC 已验证，无需管理员）。
- 新增能力：`scripts/features/AudioEndpointService.cs`（全局命名空间内部类）。纯策略 `AudioEndpointShapePolicy`（`IsVirtualCableCaptureName` 只匹配同时含 `CABLE` 与 `Output` 的端点；`NeedsMicrophoneShape`/`NeedsLineLevelShape` 只在 form factor 为 2/4 时成立），以及 COM 互操作服务 `AudioEndpointService`（`IMMDeviceEnumerator` → `IMMDeviceCollection` → `IMMDevice::OpenPropertyStore(STGM_READWRITE)` → `IPropertyStore::SetValue/Commit`，读取友好名与类别，x64 `PROPVARIANT` 用 `ole32!PropVariantClear` 释放）。**只改设备类别这一个属性，不改变音频链路、路由、音量、增益或冻结的录音内核。**
- Host 接线：`OnShown` 在既有启动序列后调用 `EnsureVirtualCableCaptureShapeAsync()`，线程池里读取真实端点类别，必要时标记为麦克风；每次真实改动只提示一次（`NotifyVirtualCableShapeChanged`）并写入 `AUDIO ENDPOINT REPAIR` 日志。自检页 `cable` 项改为读取真实类别：类别为线路设备时给出 warning + “优化可见性”（`repair-cable-shape`），已是麦克风时给出 pass + “还原线路设备”（`restore-cable-shape`），两条路径都调用同步的 `ApplyVirtualCableCaptureShape(bool)`，结果通过既有 `ActionResult` 反馈，自检项数量仍为 10。
- 修改文件：`scripts/features/AudioEndpointService.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`、`docs/V2_0_PROGRESS.md`。未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、`scripts/VoxDeckInputBridge.cs`、配置 schema（仍为 32）、Bridge schema（仍为 7）。
- 静态门禁：`npm test` 新增三条断言——音频能力必须走 Core Audio 端点属性存储且不得出现 `Clipboard.`/`SendKeys.`/`keybd_event`/`SendInput`；Host 构建清单必须编译新文件；Host 必须在启动序列之后调用形状对齐并暴露可还原的自检修复。
- 自动验证：`cmd /c BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunAudioEndpointShapeSelfTests` 覆盖命名匹配、需要修复/可还原判定与三种状态上报，纯策略、不接触真实设备）；`npm test` 通过；`scripts/tests/Test-V2FeatureSuite.ps1` 通过（Smart Focus、Capture & Ask、Browser Remote Lite、Live HUD、FeatureSurface 全绿）。
- 真机证据（本机，普通用户、非管理员）：先用生产代码的临时控制台宿主 `tmp/ShapeHarness.exe` 把 `CABLE Output` 写回 Windows 默认的 2 并复读为 2；随后启动**已安装的新版应用**（`--background`），Host 日志出现 `AUDIO ENDPOINT REPAIR name=CABLE_Output_(VB-Audio_Virtual_Cable) from=2 to=4 applied=True`，应用起来后再读端点已是 `formFactor=4 isMicrophone=True needsMicrophone=False`。这条证据证明“应用自己能在用户机器上修复可见性”，不证明任何第三方输入法已经把它列进麦克风列表。
- 部署一致性：重新生成 `release\Vibe-Flow-Windows-x64`、`VibeFlow-Setup.exe`、ZIP 与 `SHA256SUMS.txt`；`release\Vibe-Flow-Windows-x64\VibeFlow.exe` = 桌面发布目录 `D:\Window\Desktop\dsh\vibe flow\VibeFlow.exe` = 已安装 `C:\Users\Admin\AppData\Local\Programs\Vibe Flow Remote\VibeFlow.exe`，SHA-256 均为 `2DBE0511C9B4672A5FDC74E379F2B693A4FA0C5BB625AA60F520F3D38FB8D4E8`（发布脚本编译出的 Host；开发构建 `BUILD_VIBE_MIC.cmd` 产出的根目录 `VibeMic.exe` 为 `68D22E2529F7BAFC55E57E4C1CE2A8715133169C97E94679F310EA1591D80974`，两者内容不同属既有行为）；`VoxDeckInputBridge.exe` 三处均为 `10E2826350F5E6DB1FA499762D84746E83760899BC2A65CF11DE28B6499C9CDA`，`VibeMicAtvvCapture.exe` 保持冻结 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。静默安装 `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER` 退出码 0；重装后再启动，日志为 `AUDIO ENDPOINT CHECK ... form_factor=4 repair=not_needed`，证明修复幂等，不会每次启动都改写系统属性。
- UI 证据：`scripts/capture-ui-screenshots.ps1 -OutputDirectory tmp\ui-captures-audio-endpoint` 对已安装运行实例出图成功（首页、语音、快捷键、自检、设置、工具下拉、Smart Profiles），自检页正常渲染且总览仍为“等待 2 项真实验证”（说明类别为麦克风时不会新增告警）；行文字全部 `AutoEllipsis`，新动作按钮复用既有尺寸，无截断风险。
- 未验证/阻塞：豆包输入法麦克风列表中是否出现 `CABLE Output` 仍需用户在自己的第三方工具里目视确认；豆包面板无法被模拟输入唤起的问题不因本项改变，结论仍是不适用于豆包自动听写。
## 2026-09-10 落地第 2 项：零配置首次成功（免驱动模式 + 向导不再被 VB-CABLE 卡死）

- 用户目标：让“没有 VB-CABLE、装不上驱动、没有管理员权限”的机器也能完成首次成功，而不是卡在驱动步骤上。
- 已核实的硬约束：冻结捕获 `ClockedVirtualMicSink` 在录制启动时无条件构造并打开播放端点，端点缺失直接抛 `Audio endpoint not found: ... Install VB-CABLE first.`（源码 `scripts/VibeMicAtvvCapture.cs`），所以“没有 VB-CABLE 时照常启动捕获”在物理上不可能；产品化只能走 Host 侧降级。
- 新增运行模式（Host 侧，不动 Capture/Bridge/配置 schema）：`IsTriggerOnlyVoiceMode()` 由“播放端点缺失”这一可验证能力推出（另提供诊断开关 `VIBE_FLOW_TRIGGER_ONLY=1` 便于在有驱动的机器上复现该模式）。该模式下：`StartCapture()` 先启动按键桥接后直接返回并记录 `CAPTURE START skipped=true reason=trigger_only_no_virtual_cable mode=trigger_only`，不再启动注定失败的捕获进程；语音唤醒走 `VOICE WAKE mode=trigger_only`，Host 已有的 Windows（Win+H 单击）与 Typeless（按住）快捷键派发继续生效；微信/豆包因适配器在捕获侧，明确提示需要安装 VB-CABLE，不做静默失败。
- 如实呈现：首页在免驱动模式下由 `UpdateCaptureUi` 走独立分支——标题“免驱动模式已就绪”、状态标签 `TRIGGER ONLY`、副标题说明按键唤起与电脑麦克风收音、主按钮改为“查看语音设置”、状态条把“遥控器麦克风”换成“遥控器按键 由按键桥接处理”，并新增 `ApplyVisualState("trigger_only")` 文案，避免出现“语音桥接已暂停”这种误导状态；语音页在免驱动模式显示“当前是免驱动模式（未检测到 VB-CABLE）…不切换默认录音设备、不经过虚拟声卡”，并把“听写时自动使用遥控器麦克风”复选框改为不可勾选的免驱动说明（不写入配置，安装驱动后自动恢复原语义）；自检页 `remote`/`microphone` 标为“不支持”、`cable` 从“错误”降为“需要配置（安装 VB-CABLE 切换到完整模式）”、`session` 在工具无法被 Host 唤起时为“需要配置”；总览不再出现红灯（本机实测为“等待 1 项真实验证 / 另有 2 项可选设置”）。
- 首次设置向导不再被驱动卡死：第 1 步（音频通道）在缺少 `CABLE Input/Output` 时不再返回“请先准备两个 CABLE 端点”，而是说明并接受免驱动模式（同时把 `autoRouteVirtualMicrophone` 置为 `false`）；第 2 步的“语音链路已就绪”在免驱动模式下只需要按键桥接；第 5 步的首次听写门禁在免驱动模式下改用 Host 自己记录的“遥控器唤醒已派发”计数（`triggerOnlySessionDelivered`）+ 用户目视确认，工具无法被免驱动模式唤起时不会放行并直接给出两条修复路径。
- 修改文件：`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`、`docs/V2_0_PRODUCT_STRATEGY_ZH.md`、`docs/V2_0_PROGRESS.md`。未修改 `scripts/VibeMicAtvvCapture.cs`、Capture 二进制、`scripts/VoxDeckInputBridge.cs`、配置 schema（仍为 32）、Bridge schema（仍为 7）。
- 静态门禁：`npm test` 新增两条断言——缺驱动时必须在 `TryAttachExistingCapture` 之前进入免驱动分支并记录 `CAPTURE START skipped`/`VOICE WAKE mode=trigger_only`，且不得出现剪贴板与注入回退；向导必须暴露 `ONBOARDING trigger_only=true` 与两步基线（`firstTriggerOnlyBaseline`），自检项数量继续锁定为 10（本轮把三个分支写法收敛回“每项一次 `new SelfCheckItem(`”，避免门禁被绕过）。
- 自动验证：`cmd /c BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunTriggerOnlyVoiceModeSelfTests`：模式判定四象限 + 只承认 Windows/Typeless 两个 Host 可自行唤起的工具，`wechat`/`doubao`/`custom`/空值全部为否）；`npm test` 通过；`scripts/tests/Test-V2FeatureSuite.ps1` 通过。
- 真机证据（本机，用诊断开关强制免驱动模式以复现无驱动机器）：启动后进程列表只有 `VibeMic` 与 `VoxDeckInputBridge`，**没有 `VibeMicAtvvCapture`**；日志 `CAPTURE START skipped=true reason=trigger_only_no_virtual_cable mode=trigger_only` 与 `VOICE MODE trigger_only=true reason=capture_start mode_source=option virtual_cable=present audio_source=computer_microphone`；UI 出图确认语音页免驱动说明与复选框状态一致、自检页三项如实标注且无红灯。
- 未验证/阻塞（诚实边界）：免驱动模式“按下遥控器→语音工具出字”这一步**未在真机无驱动环境验证（NOT_RUN）**，本机只能用诊断开关验证运行侧行为；微信/豆包在免驱动模式下不自动唤起，是设计边界而非缺陷；有 VB-CABLE 的完整模式行为未改动，仍需回归确认。
## 2026-09-10 事故记录与安全化改造：虚拟声卡录音端消失，端点属性写入改为显式确认

- 观察到的事实：本机 `CABLE Output`（VB-CABLE 录音端）在 20:51 仍可读（`form_factor=4`、捕获进程 `ATVV READY` 正常），到 20:59 已从 Core Audio 全状态枚举（`DEVICE_STATE_ALL`）与注册表 `MMDevices\Audio\Capture` 中整体消失；同一驱动的播放端 `CABLE Input` / `CABLE In 16ch` 仍为 `state=1`，驱动设备 `ROOT\MEDIA\0000`（服务 `VBAudioVACMME`）状态 OK，`setupapi.dev.log` 最后一次 VB-Audio 活动是 18:56 的驱动包导入（本机 16:42 开机，即驱动是在本次开机后被替换的），系统事件日志在 20:50–21:00 无 PnP/音频事件，`Component Based Servicing` 与 Windows Update 均无待重启标记。
- 归因结论：**无法排除“应用写入端点属性”是诱因**（微软文档明确不保证应用修改端点属性；本机在消失前累计 4 次写入均写入成功且可复读），也无法证明是驱动替换后未重启导致的端点丢失。按最坏情况处理。
- 安全化改造（产品决策）：`scripts/VibeMic.cs` 启动路径不再写入任何端点属性，`EnsureVirtualCableCaptureShapeAsync` 改为只读的 `CheckVirtualCableCaptureShapeAsync`（读取类别、写 `AUDIO ENDPOINT CHECK found=... form_factor=... repair=available|not_needed` 日志、必要时一次性提示用户去自检页处理）；写入只保留在“自检 → VB-CABLE 本地音频通道 → 优化可见性/还原线路设备”，执行前必须通过 `MessageBox` 风险确认（说明这是未公开支持的实验性写入、个别机器可能需要重装 VB-CABLE），执行后立即复读校验，成功与失败分别返回 `ENDPOINT-SHAPE-UNVERIFIED` 错误或成功结果并写日志 `AUDIO ENDPOINT SHAPE applied=... verified=...`。
- 静态门禁：`scripts/validate.js` 更新为——启动路径必须调用只读检查、禁止在启动段出现 `TrySetFormFactor(shape.EndpointId, target`、写操作必须带确认与 `verified` 校验。`npm test`、`BUILD_VIBE_MIC.cmd` 通过。
- 未决与用户动作：本机 `CABLE Output` 仍缺失，已确认非管理员无法修复（`pnputil /scan-devices` 返回 Access denied），需要用内置驱动包重新安装 VB-CABLE（需 UAC，通常还要重启 Windows）才能恢复；恢复前，完整模式下语音输入法会录到电脑麦克风而不是遥控器音频，免驱动模式（自动进入）行为正常。
## 2026-09-10 事故收尾：虚拟声卡录音端已恢复，免驱动模式在真实缺驱动环境自动生效

- 用户动作：接受修复建议后重启 Windows（21:09:57 开机）。**重启并没有恢复 `CABLE Output`**——因为在此之前为尝试恢复而执行的 `Disable-PnpDevice`/`Enable-PnpDevice`（`ROOT\MEDIA\0000`）把 VB-Audio 设备实例整体摘掉了（播放端 `CABLE Input` 一并消失，重启后仍未回来），这一步是本次事故中唯一确定由“修复尝试”造成的额外破坏，已写入已知限制并把“不要用禁用/启用设备恢复”列为硬结论。
- 真正的恢复手段：重新运行内置官方驱动包（`scripts\Install-VBCable.ps1 -Install`，校验固定 SHA-256 `b950e39f…16bfb` 后执行 `VBCABLE_Setup_x64.exe /install`，UAC 确认）后，驱动设备与两个端点都被重新创建，**无需再重启**：Core Audio 读回 `state=1 ff=2 name=CABLE Output (VB-Audio Virtual Cable)`（新 GUID `4c48ae83-1dad-4467-945d-bab9ea280818`，旧 GUID `c616f7cc…` 作废），渲染端 `CABLE Input` 同时恢复。
- 顺带拿到的真实证据（比诊断开关更有价值）：重启后那段“确实没有 VB-CABLE”的窗口里，应用**自动**进入免驱动模式，日志为 `VOICE MODE trigger_only=true reason=capture_start mode_source=capability virtual_cable=missing audio_source=computer_microphone`，进程列表只有 `VibeFlow` 与 `VoxDeckInputBridge`、没有捕获进程；这正是本次落地的降级路径在真实环境下的行为。
- 恢复后的完整模式回归：重新启动应用后日志为 `CAPTURE START pid=5600 provider=wechat voice_mode=hold` 与 `AUDIO ENDPOINT CHECK found=true name=CABLE_Output_(VB-Audio_Virtual_Cable) form_factor=2 repair=available`（**只读检测、未自动写入**），`capture-health.json` 为 `state=ready / atvv_ready=true / ble_connected=true / recording_kernel=v1.0.3`，配置 `autoRouteVirtualMicrophone` 仍为 `true`、工具/快捷键未变；自检页总览为“等待 2 项真实验证 / 另有 1 项可选设置”，RC003 项恢复“正常”，`cable` 项回到“需要配置（优化可见性）”的可选状态。
- 待用户验证：完整模式下按一次遥控器录音键做一次真实听写（本机按键与文字落点仍需目视确认）；`CABLE Output` 是否出现在豆包输入法的麦克风列表，需要用户在“优化可见性”执行后再看一次。
## 2026-09-10 落地第 3 项：活跃输入法检测 + 引擎能力矩阵（“更好的自动化识别”的地基）

- 目标：让应用知道自己正处在哪个输入法之下，从而能如实解释“为什么这次没有出字”，并为后续按引擎选择投递策略打地基。
- 取证纠正：此前文档把 **TIP CLSID** 当成了 **Profile GUID**。本机实测（`HKLM\SOFTWARE\Microsoft\CTF\TIP`）的正确配对是——豆包 `9D2B2E2B-3C93-4D2F-9D35-6EEB85F0D2B0` / `2B4D4B3A-4D4F-4C0A-8E66-7F771A2B9C10`；微信输入法 `86598FB9-66A2-463E-B9C2-AEB906D477AD` / `607FDF85-FCC8-4DBD-A365-41296F980C9C`；微软拼音 `81d4e9c9-1d3b-41bc-9e6c-4b40bf79e35e` / `FA550B04-5AD7-411f-A5AC-CA038EC515D7`（本机 `Enable=0`）。检测按**两者任一**匹配，避免再次被这组相似值误导。
- 新增能力：`scripts/features/InputMethodDetector.cs`（全局命名空间内部类）。`InputEngineCatalog` 是纯策略（CLSID/Profile 归类、显示名、`ProviderRequiresOwnInputMethod`、`ActiveEngineBlocksProvider`）；`InputMethodDetector.TryReadActiveEngine` 走 `CLSID_TF_InputProcessorProfiles {33C53A50-…}` → QI `ITfInputProcessorProfileMgr {71C6E74C-…}` → `GetActiveProfile(GUID_TFCAT_TIP_KEYBOARD {34745C63-…})`，活跃键盘布局用 IMM32 `GetKeyboardLayout(0)` 交叉读取。
- 踩到的坑（已解决并记录）：把 `TF_INPUTPROCESSORPROFILE` 当 `out` 结构体封送时首次调用返回 `E_INVALIDARG`；缓冲区按 `Marshal.SizeOf` 只分配 56 字节时，服务端写入更多字节会**破坏进程堆**（`STATUS_HEAP_CORRUPTION`，本机 harness 200 次循环复现）。改为调用方 `AllocHGlobal(256)` 清零后 `Marshal.PtrToStructure`，连续 200 次调用稳定，单次平均 **0.022 ms**——因此可以安全地在空闲轮询里刷新，但语音边沿仍只读缓存。
- Host 接线（不改变任何派发行为）：`RefreshActiveInputEngineIfStale()` 挂在 `PollActivity()`（2 秒节流）与启动路径；`BuildSelfCheckReport()` 同样刷新，并把结果显示在 `provider` 项的“当前状态”里（“当前前台输入法：微信输入法（与配置一致）”）；首页状态条在“配置的工具 ≠ 前台输入法”时把「转写工具」值改成「前台：豆包输入法」；语音唤醒时调用 `NotifyActiveInputEngineConflict()`——仅在“微信/豆包面板必须自己在前台”这一条真实约束被违反时，给出一次（60 秒节流）说明性提示，并写 `INPUT ENGINE conflict=true action=guidance_only` 日志。日志用签名去重，只在切换时记录 `INPUT ENGINE ACTIVE engine=… known=… provider=… conflict=…`。
- 修改文件：`scripts/features/InputMethodDetector.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_TECH_MOAT_RESEARCH_ZH.md`、`docs/V2_0_PRODUCT_STRATEGY_ZH.md`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunInputEngineCatalogSelfTests`：六组 CLSID/Profile 归类、三个显示名、五个 provider 的“是否需要自己在前台”、八条冲突判定全量断言）；`npm test` 通过（新增三条门禁：检测器必须保留全部实测标识与 TSF/缓冲区实现且不得出现注入路径、Host 构建清单必须编译新文件、启动与空闲轮询必须刷新活跃输入法）；`scripts/tests/Test-V2FeatureSuite.ps1` 通过。
- 真机证据：独立 harness `tmp/EngineHarness.exe` 读回 `engine=wechat name=微信输入法 known=True clsid={86598FB9-…} profile={607FDF85-…} langid=0x0804 hkl=0x08040804`，与用户配置的默认语音工具（微信输入法）一致；策略输出 `blocks wechat provider=False / blocks doubao provider=True / blocks windows provider=False`。
- 未验证/阻塞：应用内 `INPUT ENGINE ACTIVE` 日志与首页/自检显示需要在真实运行实例上复看（本轮部署后验证）；豆包作为当前输入法时的冲突提示、以及豆包面板能否被其自身全局热键唤起（PoC 1-A）仍未做。
## 2026-09-10 落地第 4 项：装完驱动自动切回完整模式（无需重启应用）

- 目标：把上一轮事故中实测到的事实——“用内置官方驱动包重装 VB-CABLE 后端点立即回来、不需要重启 Windows”——变成产品行为，直接减少“配置成本高 + 连接不稳定”的体感。
- 实现：`BeginVbCableInstallMonitor` 在观察到安装状态到达 `installed` 后调用新增的 `ApplyVbCableInstallCompletion()`。判定逻辑抽成纯函数 `VbCableInstallCompletionAction(cableInputReady, cableOutputReady, capturing, triggerOnlyMode)`，四种结果：`reboot_required`（端点仍未出现 → 如实告知需要重启 Windows，不再让用户猜）、`diagnostic_trigger_only`（端点已在但当前是诊断用免驱动开关 → 明确说明重启应用后生效，不假装已经切换）、`switch_to_full_mode`（端点就绪且未在录音 → 直接 `StartCapture()` 切回完整模式）、`already_full_mode`（已在完整模式 → 只提示就绪）。同时设置 `refreshSelfCheckOnActivate`，若当前就在自检页则重建页面，让复检结果立刻反映真实状态。
- 日志：`VB-CABLE INSTALL endpoints input=… output=… capturing=… action=…`，与既有的 `VB-CABLE INSTALL observed_state=…` 一起构成可审计的安装→切换链路。
- 修改文件：`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge、配置 schema、安装器脚本。
- 自动验证：`BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunVbCableInstallCompletionSelfTests` 覆盖四种分支组合，包括“端点只回来一个”“已经是完整模式”）；`npm test` 通过（新增两条门禁：安装监视器必须把结束状态交给切换逻辑、切换策略必须被 self-test 钉住）。
- 未验证（诚实边界）：**“免驱动 → 完整模式”的实时切换分支本轮未做真机点击验证**——它需要“驱动缺失”这一状态才能触发安装流程，本机驱动已完好；用 `VIBE_FLOW_TRIGGER_ONLY=1` 也无法模拟（该开关会保留免驱动模式，因此会走 `diagnostic_trigger_only` 分支，这正是本轮新增该分支的原因）。`reboot_required`/`diagnostic_trigger_only`/`already_full_mode` 三条分支的判定已被 self-test 全量钉住，实时链路仍待下一次真实的驱动安装场景复看。
## 2026-09-10 真机更正：TSF 活跃输入法是“按线程”的，不可宣称“当前前台输入法”

- 观察到的矛盾：部署后主机日志在 21:25:21 记录 `INPUT ENGINE ACTIVE engine=doubao known=True provider=wechat conflict=True`，但同一时间之后的首页截图里「转写工具」仍显示“微信输入法”（即冲突值为真时首页应显示“前台：豆包输入法”）。原因是刷新发生在页面构建之后、且读取值随后变回了 wechat。
- 结论（已写回研究文档）：`ITfInputProcessorProfileMgr::GetActiveProfile` 返回的是**调用线程**的输入法上下文，不等于前台应用的输入法。同一个进程内，言灵窗口被激活前后可以读到不同引擎（本机 doubao → wechat）。因此**不能**据此断言“当前前台输入法”，也不能断言“这就是这次没出字的原因”。
- 代码层面的诚实化改造：日志加上 `scope=thread`；首页状态条与自检项文案改为「言灵所在输入法上下文：X（与默认语音工具不同，仅作排查参考）」；唤醒时的提示改成条件式、可操作的说法（“如果这次没有出字，可先切回该输入法再试，或把默认语音工具改为 Windows 语音输入”），不再声称面板一定打不开。
- 文档更正：`docs/V2_0_TECH_MOAT_RESEARCH_ZH.md` 增加“按线程 + 实测矛盾”的更正段；`docs/V2_0_PRODUCT_STRATEGY_ZH.md` 的引擎矩阵把“面板只在它是当前输入法时可用”标注为**待验证假设**。
- 未实现（诚实边界）：判断**前台应用**的输入法需要按目标窗口线程查询（例如 `AttachThreadInput` + `GetKeyboardLayout`，或对前台窗口所在线程做 TSF 查询），本轮未做；在补上之前，检测结果只作为排查信息，不作为投递策略的自动依据（派发行为与之前完全一致）。
## 2026-09-10 产品收敛：豆包输入法不再作为可选语音工具（旧配置可见迁移）

- 用户决策：去掉豆包输入法的支持选项，语音工具收敛为 **微信输入法、Typeless、Windows 语音输入**（外加既有的“其他语音工具/自定义全局快捷键”入口）。
- 移除范围（`scripts/VibeMic.cs`）：语音页与两处向导的语音工具下拉列表去掉“豆包输入法”；`ProviderIndex`/`ProviderKeyFromIndex` 重新编号（0 微信 / 1 Typeless / 2 Windows / 3 自定义）；删除 `ProviderDisplayName`/`ProviderSummary`/`ProviderSetupInstruction`/`ProviderHotkeyHelp`/`DefaultHotkeyForProvider`/`DefaultStartupDelayForProvider`/`IsProviderRunning`/`OpenProviderHelp` 中的豆包分支；删除豆包专属自动化路径 `ShouldToggleProviderVoiceBarForSession` + `BeginDoubaoVoiceBarSession` 及唤醒分支（它本来只会显示“豆包不接受自动按键”的说明），以及 `lastDoubaoGuidanceTick` 字段；`NormalizeProviderKey` 不再把 doubao 归一化为 provider（未知值回落到微信输入法）。
- 旧配置可见迁移（新逻辑）：新增 `IsRetiredProviderValue(raw)` 与 `retiredProviderMigrated` 标记；`MigrateConfig` 在检测到 V1.5 遗留的 `doubao`/`豆包`/`doubao-ime` 时，把语音工具改为微信输入法并同时套用微信输入法的稳定参数（`Ctrl + Win`、单击切换、80 ms），写回配置并触发一次性提示（`PROVIDER MIGRATED retired=doubao action=use_wechat_input_method defaults=applied` + 界面提示「豆包输入法不再作为言灵的语音工具选项…已切换到微信输入法」）。**不做静默降级**。
- 诊断保留：`InputEngineCatalog` 仍然认识豆包输入法的 CLSID/Profile（用于“言灵所在输入法上下文”的排查信息），但 `ProviderRequiresOwnInputMethod` 只对微信输入法成立；因此豆包不再影响任何派发决策。
- 文档同步：`README.md`、`QUICK_START_ZH.md`、`docs/FEATURES_ZH.md`、`docs/COMPATIBILITY_MATRIX_ZH.md`（豆包行改为“V2.0 起不提供”）、`docs/V2_0_USER_GUIDE_ZH.md`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`（改写为“不再提供 + 迁移行为 + 现状三工具”）、`docs/V2_0_PRODUCT_STRATEGY_ZH.md`（引擎矩阵前增加产品决策段，取证信息保留供未来 PoC 参考）。V1.3/V1.5 历史指南保持原文不动。
- 自动验证：`BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（更新 `RunInputEngineCatalogSelfTests` 的 provider 归属与冲突断言）；`npm test` 通过（新增三条门禁：工具列表必须正好是三个已验证工具 + 自定义且不得再出现 `case "doubao"`、迁移必须由 `MigrateConfig` 内可见地执行并写日志与提示、豆包自动化路径必须已从唤醒处理中移除）。
- 真机验证（本机，已还原）：先把用户配置临时改成 `"inputMethod":"doubao"` 再启动开发构建，日志出现 `PROVIDER MIGRATED retired=doubao action=use_wechat_input_method defaults=applied`，配置被写回为 `inputMethod=wechat / inputMethodHotkey=ctrl+win / trigger=toggle / providerStartupDelayMs=80`，同目录 `.bak` 保留迁移前内容；与测试前的完整配置逐字段比对**完全一致**（说明迁移只改该改的字段）。测试后已把配置恢复到测试前状态。
## 2026-09-10 落地第 5 项：稳定性与信任（链路质量面板 + 会话前体检快照）

- 依据：战略稿路线图第 2 项「稳定性与信任」（用户第一痛点），本切片交付其中可本地量化、可验证的两块：**连接质量（链路质量）**与**会话前体检快照**；断流预警以“间隔抖动/丢包/响应”判据落地，不做无依据的预测。
- 新增 `scripts/features/LinkQualityPolicy.cs`（纯策略，无注入/剪贴板路径）：`LinkQualityPolicy.Classify(...)` 只用冻结内核已经发布的指标（`AudioMs`/`MaxGapMs`/`QueueDrops`/`SinkQueueDrops`/`OutputRmsPercent`/`TriggerToReadyMs`/成功与失败标志），阈值与自检门禁**逐一对齐**（间隔 250/600 ms、响应 1500/3000 ms、输出 0.8%、可评估音频 700 ms），输出 `good/fair/poor/unknown` + `ReasonCode`（`HEALTHY`/`AUDIO_DROPS`/`GAP_HIGH`/`GAP_ELEVATED`/`LATENCY_HIGH`/`LEVEL_LOW`/`AUDIO_TOO_SHORT`/`NO_RECEIPT`/`SESSION_FAILED`/`NO_SESSION`）+ 中文摘要/详情/建议。**始终是建议性判定**：链路好也不等于文字已到达（应用读不到目标输入框）。
- Host 接线：`CurrentLinkQuality()` 单点计算，保证首页状态条、自检与日志三者一致；首页状态条第 3 项由「语音数据」改为「**链路质量**」，值直接用判定摘要、颜色按 `IsGood` 上色；自检 `session` 项追加「链路判定：质量良好（间隔 18 ms · 丢包 0 · 响应 220 ms）」，降级时把「原因」换成对应建议；会话结束回执路径新增 `ReportLinkQualityAfterSession(generation)`，写 `LINK QUALITY state=… reason=… detail=…` 日志，并在 `fair/poor` 时给出**一次/5 分钟节流**的、点名测量值的提示（例如“蓝牙音频间隔过大：把遥控器与电脑保持在 5 米内”）。
- 会话前体检（元数据）：物理录音键唤醒时新增 `SESSION PREFLIGHT bridge=… capture=… atvv=… cable_in=… cable_out=… provider=… provider_running=… mode=…`，用于把“这次为什么没出字”与按键当时的真实状态对应起来；只记录布尔与枚举，不含任何文字。
- 修改文件：`scripts/features/LinkQualityPolicy.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunLinkQualityPolicySelfTests`：无会话→unknown、健康会话→good、丢包→poor、间隔 900→GAP_HIGH、间隔 400→fair/GAP_ELEVATED、响应 4 s→poor/LATENCY_HIGH、音频 300 ms→AUDIO_TOO_SHORT、输出 0.2%→LEVEL_LOW、失败/传输出错→poor、成功但无回执→NO_RECEIPT）；`npm test` 通过（新增三条门禁：策略必须保留全部阈值与原因码且不得出现注入路径、Host 构建清单必须编译新文件、Host 必须计算/记录/展示链路质量且会话结束回执后必须调用上报、策略必须被 self-test 钉住）；`scripts/tests/Test-V2FeatureSuite.ps1` 通过。
- 待真机复看：`LINK QUALITY` 与 `SESSION PREFLIGHT` 两行日志需要一次真实按键听写才会产生（本轮未按键，故只验证了首页面板与策略）；`fair/poor` 提示的用户体验需在实际抖动场景下确认阈值是否合适。
## 2026-09-10 落地第 6 项：编排与自动化 · 按应用的工作流卡片（路线图第 3 项）

- 目标：让用户一眼看清“某个应用为什么没有按预期工作”。产品里已经有三块能力（Smart Profiles 的按应用键位、Smart Focus 的输入目标、默认语音工具），但此前没有任何地方把它们**组合**到同一个应用名下；失败时用户只能自己猜是哪块缺了。
- 新增 `scripts/features/WorkflowCards.cs`（纯策略，**不新增任何持久化**）：`WorkflowCards.Build(bindings, targets, providerRunning, observedProcess, observedProfile, observedRecently)` 把「应用 → 键位 Profile → 输入目标 → 语音工具运行状态 → 最近一次真实生效证据」组合成卡片，产出五类缺口码：`NO_PROFILE`（未绑定键位）、`NO_TARGET`（未学习/未验证输入目标）、`TARGET_UNVERIFIED`、`PROVIDER_NOT_RUNNING`、`NEVER_OBSERVED`（配置齐全但从没在真实按键下生效），每类都有说明、建议、动作文案与动作码；`Summarize()` 给出与卡片数量严格一致的摘要（含最集中的缺口标签）。判定只在真实观察到的证据（桥接 `last_execution` + Smart Profile 前台进程）上成立。
- UI：自检页新增「**应用工作流**」区块，渲染在**十项环境自检之上**（组合信息优先），顶部右侧显示摘要；每张卡片沿用既有行式布局（正确状态/当前状态/原因/下一步/一键修复），动作直接复用既有能力：`workflow-target` → Smart Focus 目标对话框、`workflow-profile` → 快捷键页、`provider` → 语音页、`test-dictation` → 首页测试引导。区块高度会**整体下移**环境自检与高级诊断卡片，自检项数量仍严格为 10。
- 日志：组合内容变化时记录一次 `WORKFLOW CARDS cards=… summary=…`（按签名去重，避免每 250 ms 的页面重建刷屏）。
- 修改文件：`scripts/features/WorkflowCards.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema/任何既有存储。
- 自动验证：`BUILD_VIBE_MIC.cmd` 通过；Host `--self-test` 通过（新增 `RunWorkflowCardsSelfTests`：三张卡片的组合顺序、完整卡为 ready/无动作、缺目标卡的缺口与动作码、缺 Profile 卡的两条缺口与首选动作、语音工具未运行→`PROVIDER_NOT_RUNNING`、从未生效→`NEVER_OBSERVED`、五类缺口的说明/建议/动作均非空、空列表摘要必须给出创建路径、摘要计数必须与卡片一致）；`npm test` 通过（新增四条门禁：组合文件不得出现文件/目录写入或注入路径、构建清单必须编译新文件、Host 必须渲染并暴露一键修复、摘要与分组必须被 self-test 钉住且区块下移不得改变自检项数量）；`scripts/tests/Test-V2FeatureSuite.ps1` 通过。
- 真机验证：已安装实例的自检页截图显示「应用工作流 6 个应用：0 个已就绪 · 5 个缺少输入目标」，逐行列出 `应用 · windows-terminal` / `powershell` / `pwsh` / `cmd` 等卡片的「键位 Terminal Agent · 输入目标 未设置 · 语音工具 已运行」与「设置输入目标」按钮——这正是此前无处可见的组合信息。第一版摘要措辞冗长且被裁切、分组前缀显示为“V2.0 场景能力”，均已按截图修正（改用短标签 `ShortGapLabel`、为 `workflow-` 前缀分配「应用」分组）。
- 未验证：卡片的「验证一次」路径需要一次真实按键；没有任何应用的机器上应显示“还没有应用工作流”的引导行（本轮本机有 6 个应用，未覆盖空状态的真机显示，逻辑已被 self-test 覆盖）。
## 2026-09-10 用户反馈排查：收音质量与“文字被收回”（结论 + 产品化）

- 用户反馈：收音质量不如之前；输入能直接进对话框，但很多文字会被“收回撤销”。
- 取证一（回填路径）：两次真实会话的主机日志均为 `WETYPE PASTE FALLBACK payload_ready=False` → `skipped=true reason=payload_missing`，即**言灵的剪贴板回填根本没有触发**；同时 `VOICE INPUT TARGET ready=false code=FOCUS-PROCESS-MISMATCH`（Smart Focus 目标与当前前台进程不匹配，未取得锁定）。因此“文字被收回”不可能由言灵的粘贴造成。
- 取证二（音频链路）：Core Audio 直读结果——`CABLE Output` 音量 **100%**、未静音、混音格式 **48000 Hz / 2 ch / 32-bit float**（共享模式标准），端点层无异常；`CABLE Input`/`CABLE In 16ch` 的音量读取抛异常（已记录，未影响链路）。驱动重装（本机 21:07）**不是**收音量下降的原因。
- 取证三（真实指标趋势）：`vibe-mic-runtime.log` 的历史 `REMOTE STREAM STOP` 显示输入电平长期偏低（`raw_rms_pct` 1.5–3.5%，内核自动增益 3–6 倍；唯一一次 5.7% 出现在贴近说话时），且**蓝牙间隔经常超阈值**（近期 164–465 ms，偶发 912 ms，健康基线 ≤ 250 ms）。该模式自 18:09 起就存在，**早于本轮任何改动**；本轮所有改动（链路质量面板、工作流卡片、豆包下线）都不触碰音频与派发路径。
- 取证四（“文字被收回”的真实机制）：内核日志显示面板响应慢时会被重复激发——`WETYPE TOOLBAR CLICK`（start attempt=1 / start_retry attempt=2）与 `WETYPE HOTKEY TAP phase=start_fallback`，提交侧同样有 `submit_after_audio_drained` + `submit_fallback`；某次 `trigger_to_ready_ms=1238` 即触发重试。面板被启动或提交两次就可能替换/收回已出现的文字。
- 本轮产品化（不改冻结内核）：①`AudioEndpointService` 新增 `TryReadEndpointLevel`/`TrySetEndpointLevel`/`TryReadEndpointFormat`（Core Audio `IAudioEndpointVolume`/`IAudioClient::GetMixFormat`，音量写入是微软公开支持的混音器操作）；②自检 `cable` 项显示“录音端音量 xx%”，音量 <90% 或静音时降为“需要配置”并给出**一键「恢复 CABLE 音量」**（写后复读校验，失败给出手动路径与 `CABLE-LEVEL-UNVERIFIED`）；③`ObservePanelStimulus`/`ReportPanelStimulusAfterSession` 统计每次会话的面板激发次数，>1 时记录 `PANEL STIMULUS start=… submit=…` 并给出一次可操作提示（改用 Windows 语音输入直写、或在微信输入法里改为直接上屏并关闭 AI 整理）。
- 修改文件：`scripts/features/AudioEndpointService.cs`、`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增三条门禁：端点音量/格式接口必须存在且保留 IID、自检必须能检测并修复被静音/衰减的虚拟声卡、Host 必须统计面板激发并在会话结束回执后上报）全绿。
- 给用户的直接结论：①“文字被收回”是微信输入法面板被重复激发/其语音模式改写的表现，建议改用 Windows 语音输入（已验证直写上屏）或把微信语音模式改为直接上屏；②收音质量的可控杠杆是**贴近遥控器说话**与**改善蓝牙链路**（距离/遮挡/电池），言灵已把这两项变成可见指标与提示；③端点音量若再被重置，自检里有一键修复。
## 2026-09-10 转译质量排查：音频确实进了输入法，瓶颈是蓝牙断流

- 用户反馈：说“hello vibe coding test”被识别成 “BERTThe batch”。这个反馈很关键，把问题分成了两种可能：音频根本没进输入法，或输入法识别英文失败。
- 取证一（**核心假设验证**）：新增 `AudioEndpointService.TryListCaptureSessions`（Core Audio `IAudioSessionManager2`/`IAudioSessionEnumerator`/`IAudioSessionControl2::GetProcessId`），实测 `CABLE Output` 上**确实存在微信输入法的录音会话 `wetype_update`**，同时 `CABLE Input`（播放端）上是 `VibeMicAtvvCapture (active)` + `VibeFlow`。结论：音频链路与“输入法读的是 CABLE Output”都成立，问题不在投递链路。
- 取证二（会话指标）：用户随后 4 次测试（session 46–49）——`raw_peak_pct` 16–42%（**没有削波**）、`raw_rms_pct` 1.2–3.4%（电平偏低、内核增益 2.5–5.7 倍），但 `max_gap_ms` 分别是 **343 / 537 / 271 / 282 ms**，每次会话都断流。几百毫秒的块状断流会整块吃掉音节，这才是英文被识别成噪声的直接原因（听写时贴合麦克风并不能修复断流）。
- 取证三（可能的干扰源）：本机蓝牙是 **Generic Bluetooth Adapter（USB\VID_10D7&PID_B012，Actions）**，同一适配器上同时挂着 HUAWEI FreeClip 2 耳机、EDIFIER BLE 音箱、Logi K580 BLE 键盘与 MI RC 遥控器；蓝牙音频（A2DP）与遥控器麦克风共用空口，是 300–500 ms 断流的高度可疑来源。
- 产品化（不改冻结内核）：①自检 `cable` 项新增**录音会话证据**（“正在读取 CABLE Output：wetype_update”，或如实说明“此刻没有会话，输入法只在听写时打开麦克风”）；②硬件探测新增 `bluetooth_devices` / `bluetooth_audio_endpoints` 计数，链路判定为降级时在“原因”里追加蓝牙争用提示（“同时连接了 N 个蓝牙设备（含 M 个蓝牙音频端点）：耳机/音箱会与遥控器麦克风争用空口带宽”）。
- 修改文件：`scripts/features/AudioEndpointService.cs`、`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增两条门禁：必须能枚举录音会话并保留全部会话接口 IID、自检必须给出录音会话证据与蓝牙争用提示）、`Test-V2FeatureSuite.ps1` 全绿。
- 给用户的结论与建议：音频通路正常、输入法确实在读 CABLE Output（已实测），转译差是**蓝牙断流**造成的；请先断开蓝牙耳机/音箱（尤其正在播放音频时）再测一次，并确认遥控器与 dongle 之间无遮挡、尽量远离 USB 3.0；若断流仍存在，下一步在“稳定性”里做**断流计数与恢复耗时**的基线，并把 USB 蓝牙适配器的电源管理（选择性挂起）纳入检查项。
## 2026-09-10 落地第 7 项：稳定性与信任（断流基线 + USB 选择性挂起检查）

- 依据：上一轮已证实“转译质量差”的直接原因是**每次会话 271–537 ms 的蓝牙块状断流**，因此本轮把可量化的断流指标与最可疑的系统开关做成产品能力。
- 取证：`powercfg /query SCHEME_CURRENT` 实测本机 **USB 选择性挂起 = 已启用（AC=1/DC=1）**；蓝牙适配器（`USB\VID_10D7&PID_B012`，Actions 通用适配器）**没有任何禁用挂起的覆盖**；同一适配器上已连接 **4 个蓝牙设备**（MI RC / Logi K580 Keyboard / HUAWEI FreeClip 2 / EDIFIER BLE）。空闲时挂起适配器与多设备争用空口，正好解释数百毫秒级断流。
- 新增 `scripts/features/LinkBaselineStore.cs`（**只存链路元数据**）：把每次会话的 `maxGapMs`/`drops`/`triggerToReadyMs`/`audioMs`/原因码写入 `%LOCALAPPDATA%\Vibe Flow Remote\UserData\link-baseline.json`（容量 20、原子写 + `.bak`、未来 schema 只读不写），并提供 `CurrentSummary()` 生成「本机基线：最近 N 次会话平均最大间隔 X ms、最差 Y ms，其中 Z 次超过 250 ms」。不存文字、不存剪贴板、不存窗口标题。
- Host 接线：硬件探测脚本新增 `usb_selective_suspend_ac/dc`、`bluetooth_devices`、`bluetooth_audio_endpoints`（全部只读）；`BluetoothContentionNote()` 在链路降级或存在挂起风险时给出可执行建议；`ReportLinkQualityAfterSession` 现在先落一条基线样本并写 `LINK BASELINE recorded=… samples=… average_gap_ms=… worst_gap_ms=… over_250ms=…`，再写既有的 `LINK QUALITY`；自检 `session` 项的「原因」追加蓝牙争用与 USB 选择性挂起提示，并附本机基线。
- 诚实修正：蓝牙音频端点计数在本机恒为 0（耳机的音频端点没有独立 PnP AudioEndpoint 节点），因此文案改为只陈述**确定事实**（已连接蓝牙设备数 + USB 选择性挂起状态），不再引用会误报的端点计数（字段保留备用）。
- 修改文件：`scripts/features/LinkBaselineStore.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增三条门禁：基线存储必须存在、容量受限、且不得出现剪贴板/注入/窗口标题字段；构建清单必须编译；Host 必须在会话结束回执后落基线并暴露摘要）、`Test-V2FeatureSuite.ps1` 全绿；探针脚本单独实跑确认 `usb_selective_suspend_ac=1`、`bluetooth_devices=4` 解析正确。
- 未验证：`link-baseline.json` 需要一次真实听写才会写入（本轮未按键）；「禁用 USB 选择性挂起后断流是否消失」需要用户在电源选项里改一次并复测——这是下一步的判断点。
## 2026-09-10 根因确认并产品化：USB 选择性挂起 → 蓝牙断流 → 转译劣化

- 用户反馈“收音质量很好”后复核数据：禁用 USB 选择性挂起前，同一批英文测试的 `max_gap_ms` 为 **343 / 537 / 271 / 282**（`GAP_ELEVATED`）；禁用后（22:15–22:19 四次会话）为 **120 / 74 / 164 / 164**，丢包 0，`LINK QUALITY state=good reason=HEALTHY`，`link-baseline.json` 记录 `samples=4 average_gap_ms=130 worst_gap_ms=164 over_250ms=0`。**根因确认：USB 选择性挂起在空闲时挂起蓝牙适配器 → 数百毫秒音频断流 → 语音识别缺块 → 转译成噪声。**
- 产品化：新增 `scripts/Set-UsbSelectiveSuspend.ps1`（`-StatusOnly`/`-Disable`/`-Restore`，自查管理员并用 `-Verb RunAs` 自提权，写入 `%LOCALAPPDATA%\Vibe Flow Remote\usb-suspend\state.json` 记录 before/after，**可还原**）；Host 新增 `SetUsbSelectiveSuspend(bool)` 与动作 `repair-usb-suspend`/`restore-usb-suspend`；自检「Windows 蓝牙」项在 `UsbSelectiveSuspendAc == 1` 时降级为“需要配置”，原因写明本机实测数据（禁用后断流从 271–537 ms 降到 74–164 ms），并给出一键「禁用 USB 选择性挂起」；禁用后该项回到“正常”并注明「USB 选择性挂起已禁用（遥控器音频不会被空闲挂起打断）」。这正是本机电源选项里**根本不存在**的那个设置项——GUI 找不到，`powercfg` 能改。
- 修改文件：`scripts/Set-UsbSelectiveSuspend.ps1`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增两条门禁：电源脚本必须保留可还原的提权实现与 powercfg 三件套、自检必须用实测值驱动一键修复且动作必须在蓝牙项里）、`Test-V2FeatureSuite.ps1` 全绿；脚本 `-StatusOnly` 单独实跑输出 `AC=0 DC=0`。
- 待办（下一步）：把「断流频率/恢复耗时」写进版本验收指标（基线已有 4 个样本）；如果用户愿意，可在自检里再加“还原 USB 选择性挂起”的入口（动作已实现，UI 目前只在启用时显示禁用）。
## 2026-09-10 落地第 8 项：编排收尾 —— 工作流卡片的一键「打开应用并学习输入目标」

- 问题：工作流卡片已能指出“N 个应用缺少输入目标”，但用户仍需要**自己先切到那个应用、再点学习**——这正是“配置成本”的残留部分。
- 实现：`WorkflowCard.Action` 对 `NO_TARGET` 输出带进程名的动作 `workflow-learn:<进程名>`（动作文案改为「打开应用并学习」）；Host 新增 `BeginWorkflowTargetLearning(processName)`：用既有 `Process.GetProcessesByName` 找到该进程的主窗口，`ShowWindowForProject(SW_RESTORE)` + `SetForegroundWindowForProject` 把它带到前台，随后打开既有的 Smart Focus 学习对话框，并如实提示两种结果（“已切到 X：请点击要落字的输入框，然后点‘立即测试并保存’” / “X 没有可用的前台窗口：请先打开它”）；日志 `WORKFLOW LEARN process=… activated=…`。`TARGET_UNVERIFIED` 仍走既有的 `workflow-target`（只打开对话框，不切窗口）。
- 边界：只做“激活窗口 + 打开学习对话框”，**不代替用户点击输入框**，也不读任何文字；激活失败时给出手动路径而不是假装成功。
- 修改文件：`scripts/features/WorkflowCards.cs`、`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`（更新 `RunWorkflowCardsSelfTests`：缺目标卡的动作为 `workflow-learn:Code`、文案为「打开应用并学习」）、`npm test`（新增一条门禁：动作必须带进程名、Host 必须实现窗口激活并复用既有 ShowWindow/SetForegroundWindow P/Invoke、且必须挂在 `workflow-profile` 分支之后）全绿。
- 未验证：真机点击一次卡片按钮的端到端体验（需要用户点一下；窗口激活与对话框打开逻辑复用项目空间里已验证过的同一对 P/Invoke）。
## 2026-09-10 缺陷修复：输入目标只认全局默认，导致 Chrome 也会被拉进 ChatGPT

- 用户报告：目标定位到 ChatGPT 后很稳定（任何页面都能锁定到 ChatGPT 对话框），但把学习目标设为 **Chrome 浏览器** 后，输入依旧落在 ChatGPT 对话框。
- 根因（代码定位）：①`FocusTargetDialog.SavePending()` 只在勾选「设为默认目标」（或原本没有默认目标）时才更新 `document.DefaultTargetId`，所以学习 Chrome 目标通常只是**新增一个目标**，默认目标仍是 ChatGPT；②唤醒路径 `ExecuteDefaultFocusTarget()` 只调用 `DefaultFocusTarget()`（= `focusTargetDocument.DefaultTarget()`），即**永远使用全局默认目标**，并会主动激活它——因此人在 Chrome 时焦点被拉回 ChatGPT。这与工作流卡片、按应用 Profile 的按应用模型不一致。
- 修复（按应用选目标）：`FocusTargetService.SelectVoiceTarget(targets, defaultTargetId, foregroundProcess)`（纯策略）：当前台进程存在**已验证**（`LastVerifiedUtc` 有值）的专属目标时优先使用它，否则回退到配置的默认目标；前台进程未知或无匹配时不改变行为。Host 新增 `VoiceWakeFocusTarget(out source)` 并在 `ExecuteDefaultFocusTarget()` 中使用，日志 `VOICE FOCUS TARGET source=default|foreground_process target_id=… process=…`，便于用户与我们核对“这次用的是哪个目标”。
- 修改文件：`scripts/features/FocusTargetService.cs`、`scripts/VibeMic.cs`、`scripts/validate.js`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge/配置 schema/目标存储格式（纯策略 + 调用点）。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`（新增 `RunVoiceWakeTargetSelectionSelfTests`：前台有已验证专属目标→用它；前台就是默认目标的进程→用默认；前台无专属目标→回退默认；前台未知→回退默认；只有一个目标时→默认；不得返回未验证目标或 null）、`npm test`（新增两条门禁：选择函数必须存在且不得读取窗口标题/坐标、Host 唤醒路径必须使用它并记录来源）全绿。
- 未验证：真机在 Chrome 中按住录音键，确认文字落入 Chrome 的输入框且日志出现 `source=foreground_process`。
## 2026-09-10 修复：控制台窗口无法学习（焦点型目标）+ 两步学习

- 用户反馈：手动打开 cmd、鼠标移过去，再用 Windows Terminal“锁定/学习”，试了多次都失败；并希望学习流程简化成「选应用 → 点输入框」。
- 实测根因（新增 `tmp/ConsoleUiaProbe` 用 UIA 直接探测）：Windows Terminal 的终端区域暴露的是 `ControlType.Text` + `class=TermControl` + `TextPattern=True` + **`ValuePattern=False`**，经典 conhost 同类。也就是说**控制台窗口没有可写的 ValuePattern**，而学习/验证此前只接受 `ControlType.Edit` 且要求可写（`HasWritablePatternEvidence`）——所以无论如何操作都会以 `FOCUS-TARGET-NOT-EDITABLE` 失败，这不是操作问题。
- 关键认知：投递文字并不需要目标可写。微信输入法/Win+H 都是把文字键入“当前焦点窗口”，目标的作用只是**校验别落错地方**与**回填时切回正确窗口**。
- 实现一（焦点型目标）：`FocusTargetService.CaptureFocusedEditableTarget` 现在接受 `Edit`/`Text`/`Document` 且 `IsKeyboardFocusable && HasKeyboardFocus && !IsOffscreen && !IsPassword` 的元素；`Edit` 仍要求可写（策略 `uia`），`Text`/`Document` 走新增的 `uia_focus` 策略并要求 `TextPattern`（或非只读 ValuePattern）；描述符用既有 `Strategy` 字段标记，**不改存储格式**。`FocusTargetDescriptor.TryValidateForVerification` 允许 `uia_focus` + `Text`/`Document`/`Edit`。语义边界写在代码注释与文档里：**应用只锁定/恢复焦点，从不自己向该目标写入文字**。
- 实现二（两步学习）：`FocusTargetDialog` 在验证成功后**自动保存**（原来还要再点一次“保存”），状态文案改为“目标已锁定并验证；已自动保存”。配合上一轮的“打开对话框时预选该应用”，流程变为：**选应用 → 点一下目标输入框 → 完成**。
- 修改文件：`scripts/features/FocusTargetService.cs`、`scripts/features/FocusTargetModels.cs`、`scripts/ui/FocusTargetDialog.cs`、`scripts/validate.js`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge、配置 schema、目标存储格式；ChatGPT/Chrome 的既有 `uia` 路径行为不变。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增两条门禁：捕获必须支持 Text/Document 焦点型控件、验证必须放行 uia_focus）、`Test-V2FeatureSuite.ps1` 全绿。
- 未验证：真机在 Windows Terminal / cmd 中完成一次两步学习（需要用户点一下；UIA 探测数据已证明该控件可被捕获为焦点型目标）。
## 2026-09-10 重做 Smart Focus（第 2 轮）：前端极简——点选应用 + 点一下输入框即成

- 目标：把跨多次点击、还要手打名字的旧「Smart Focus 学习器」换成极简工作流。
- 本轮交付：
  - 新增 `scripts/ui/AppPickerDialog.cs`：**唯一的新对话框**——列出**本机正在运行的应用**（可读名 + 进程名），选中后点「开始学习」，**没有任何输入框、零打字**（门禁明确禁止 `TextBox`）；
  - 语音页「设置/锁定输入目标」按钮改为打开该列表（`BeginFavoriteAppLearning`），不再进入旧对话框；
  - 选定后 `ActivateProcessWindow` 把该应用窗口带到前台并提示“请点一下你要落字的输入框，我正在识别…”，随后 `CaptureFavoriteTarget` 以 250 ms 轮询最多 10 秒，**一旦焦点元素属于该应用且是可用的输入面**就捕获 → `ExecuteForVerification` 验证 → 保存；
  - `SaveLearnedFavoriteTarget`：**同一应用只保留一个目标**（重新学习即覆盖，不再堆积重复项），并写入/更新 `favorite-apps.json`、把该应用设为当前；控制台/终端走上一轮的「焦点型目标」（`uia_focus`）；
  - 唤醒时固定使用**当前常用应用**（`source=favorite_app`），未启动则自动启动（第 1 轮已完成）。
- 修改文件：`scripts/ui/AppPickerDialog.cs`（新增）、`scripts/VibeMic.cs`、`scripts/validate.js`、`BUILD_VIBE_MIC.cmd`、`docs/V2_0_PROGRESS.md`。未修改 Capture/Bridge、收音链路、配置 schema。
- 自动验证：`BUILD_VIBE_MIC.cmd`、Host `--self-test`、`npm test`（新增四条门禁：选择器必须是纯点选、不得出现 `TextBox`/`TextChanged`；构建清单必须编译；语音页按钮必须驱动新流程；重新学习必须覆盖同一应用的目标且必须在默认目标为空时补齐）、`Test-V2FeatureSuite.ps1` 全绿；已安装 `VibeFlow.exe` = `D4AADE8B…`，**Capture 冻结哈希仍 `B62DE035…E683`**。
- 未完成（下一轮）：①常用应用**列表界面**（显示已添加的应用、可切当前/重新学习/删除）——目前只能逐个学习、学习即设为当前；②按钮与文案仍叫“锁定输入目标”，要改成「选择常用应用」这类直白说法；③真机端到端确认（需要用户点一次）。
## 2026-09-11 V2.0 总方案推进：工作流整合完成 + 旧 Smart Focus 退役（2/3）+ 后续交接

### 已完成（均已构建、门禁、部署，Capture 冻结哈希始终为 B62DE035…E683）
- 消费级「工作流」页：导航栏独立页面；编号行、图标、可读名、筛选框、模式胶囊、成功横幅内「保存」、打开/删除；
  语音页只保留音频链路（工作流入口已按“页面隔离”要求移除）；首页底部有简要入口（当前应用 + 「配置工作流」）。
- 「添加应用」：本机运行中 + 已安装（含 UWP/Store，经 shell:AppsFolder 枚举，130 条取 111 条，总数 54→165）；
  未运行的应用选定后自动启动并等待窗口（20s），再进入“点一下输入框”的学习；学习成功才可出现「保存」。
- 旧 Smart Focus 退役：所有用户入口切断（按钮/卡片/向导/自检动作 → 跳工作流页）；
  生产代码引用清零（字段、ShowFocusTargetsDialog 两个方法、实例化、清理块）；
  用户可见中文里的「输入目标」已统一为「工作流」（含 FOCUS-TARGET-STALE 恢复文案，生产端与 self-test 期望同批改）。
- 工具改进：`--self-test` 失败时写 `self-test-report.txt`（含消息与调用栈），排障不再靠猜。
- 缺陷修复：常用应用流程里 6 处 `ShowPage(PageVoice)` 改为 `ShowPage(currentPageIndex)`（不再无故跳页）。
- 诊断入口（支持用）：`--list-installed` / `--learn-app` / `--learn-and-save` / `--set-app` / `--start-app` / `--open-app` / `--toggle-mode`。

### 剩余：旧对话框退役第 3 步（删文件与门禁收尾）
`scripts/ui/FocusTargetDialog.cs` 目前仍参与编译，但生产代码已不再引用；`validate.js` 中仍有 6 条断言读取该文件内容。

> **⚠️ 更正（2026-09-11，见文末「旧对话框退役第 3 步完成」）**：本节前提有误——`FocusTargetDialog` 当时**仍被生产代码引用**（`DispatchUi` → `TryPostToUi`）并被两处编译期测试引用，`FavoriteAppsPanel.cs` 里也没有这些语义。实际退役按「保留意图、改指真实落点」完成，请以文末那节为准。
正确做法：把这些断言的**意图**保留并改指向 `scripts/ui/FavoriteAppsPanel.cs`（安全语义不能丢），再删文件。
需要处置的断言消息（原文）：
1. "Smart Focus learning UI lacks non-activating capture, stability, test-before-save, or status states"
2. "Smart Focus status does not distinguish a verified target whose application is no longer running"
3. "The Host does not cancel Smart Focus for recording or publish the default target state"
4. "The Host build does not compile the Smart Focus implementation and Windows UIA references"
5. "Smart Focus must not read the clipboard or dispatch keyboard input"
6. "Smart Focus reads UI text, values, RuntimeId, or screen coordinates"
同时：`const focusTargetDialog = read("scripts/ui/FocusTargetDialog.cs");` 与文件清单里的同名条目要一并移除，并从 `BUILD_VIBE_MIC.cmd` 的源列表移除。
约束：保留基座（FocusTargetModels / FocusTargetStore / FocusTargetService）及其断言；只退役 UI 文件。

### 后续路线图（按序）
1. 遥控器价值（路线图主项）：快捷键页做「短按 / 长按 / 双击」手势分层（每类可绑定一个动作、可测试），再叠“宏”（一串动作按序执行）；复用既有 MappingCard/Profile 结构，避免新造配置格式。
2. 个人化资产：仅元数据的统计页（会话次数、最大间隔、成功率；不记录任何文字）+ 用语/片段包（用户自备文本，不回填第三方转写）。
3. 未实测项：UWP 应用的学习与冷启动（进程名与 AUMID 可能不匹配，学习可能报 FOCUS-PROCESS-MISMATCH）、UWP 图标（需 IShellItemImageFactory）、带参数启动的复用、首页入口在底部（是否上移到首屏）。

### 本会话的操作教训（重要）
- 含中文的代码改动必须用 read+edit 工具；PowerShell here-string 传中文会截断字符串字面量（曾导致 `"工作流"` 丢引号）。
- 多行范围删除必须用 read+edit 精确匹配；按行号范围删除曾两次造成大括号失衡（靠 self-test-report.txt 才定位）。
- 单行 .Replace 与跨文件短语替换安全可靠，适合文案/条件/常量；改门禁时优先按“消息锚点”替换整块断言。
## 2026-09-11 接着做：手势分层（后端已完成，UI 与派发待接）

### 已完成的后端（全部有 self-test 断言 + 门禁钉住 + 已部署）
- `scripts/features/GestureLayerPolicy.cs`
  - `Classify(holdMs, previousTapWithinWindow)`：`>= LongPressMs(600)` → 长按；否则双击窗口 `DoubleTapWindowMs(320)` 内 → 双击；否则短按。
  - `ActionFor(binding, kind)`：双击未配 → 回退长按 → 回退短按；长按未配 → 短按。
  - `HasOwnBinding` / `Describe`（短按/长按/双击）/ `NormalizeAction`。
- `scripts/features/GestureBindingStore.cs`（持久化 `UserData\gesture-layers.json`，原子写 + `.bak`，schema 1）
  - `GestureLayerEntry{ key, shortAction, longAction, doubleAction, macroName, macroSteps }`；`Find` / `UpsertLayer`（键名归一化小写，`Home` 与 `home` 同键）。
  - 宏：`AttachMacro` + `ValidateMacro`（`GESTURE-MACRO-EMPTY` / `-TOO-LONG`（>8） / `-BLANK-STEP` / `-NO-NAME`）、`HasMacro`、`ResolveSteps(entry, kind)`（双击挂宏 → 按序返回宏步骤；否则返回该层动作）。
  - 卡片文案：`DescribeLayer(entry, kind, actionText)`、`FallbackKind`、`OwnAction`；常量 `UnboundLayerText="未配置"`、`FallbackPrefix="回退到"`、`MacroPrefix="宏："`。
- `scripts/features/GestureMacroRunner.cs`
  - `RunSteps(steps, execute)`：按序执行、**失败即止**、返回 `Succeeded/Executed/FailedStep/Error`（`GESTURE-NO-ACTION` / `GESTURE-STEP-FAILED`）。
  - `DescribeResult(result)` → `GESTURE MACRO steps=3 executed=2 failed=2 error=GESTURE-STEP-FAILED`。

### 待做（下一步，按序）
1. **「快捷键」页三行手势卡片**
   - 入口方法 `BuildMappingsPage()`；现有卡片构建含 `shortEdit` / `longEdit` / `supportsLongPress` / `MappingCardActionText(action)` / `EditMappingAction(key, label)` / `TestMappingAction(label, action)`（用 Select-String 定位当前行号，避免按旧行号改）。
   - 做法：在既有卡片上增加第三行（双击），行文本一律用 `GestureBindingStore.DescribeLayer(entry, kind, MappingCardActionText)`；`entry` 由 `GestureBindingStore.Find(store.Load(), key)` 取得；点击行调用既有 `EditMappingAction` 的同类路径，写入用 `UpsertLayer` + `TrySave`。
   - 双击行的“测试”按钮复用 `TestMappingAction`，宏则用 `GestureMacroRunner.RunSteps` 逐步执行并显示 `DescribeResult`。
   - 默认范围：对所有可配置键开放双击（用户已确认按默认做）。
2. **按键派发接线**
   - 在按键触发处把“短按/长按”两个分支改为 `GestureLayerPolicy.Classify(按住时长, 前次点击是否在窗口内)` → `GestureBindingStore.ResolveSteps(entry, kind)` → `GestureMacroRunner.RunSteps(steps, 实际动作执行器)`；日志写 `DescribeResult`，失败时如实提示中止在第几步。
   - 注意：录音键（F5）固定在稳定语音链路上，**不参与手势分层**。
3. 门禁新增：手势卡片必须由 `DescribeLayer` 生成；派发必须经过 `ResolveSteps`/`RunSteps`；`GestureMacroRunner.cs` 必须在 `BUILD_VIBE_MIC.cmd` 源列表中。
4. 之后：旧对话框收尾（把 6 条读 `FocusTargetDialog.cs` 的断言改指 `FavoriteAppsPanel` 后删文件，见上一节清单）→ 个人化资产（仅元数据统计页 + 用语/片段包）。

### 操作纪律（务必遵守，本会话已多次踩坑）
- 含中文的代码改动只用 read+edit；PowerShell here-string 会截断中文字面量。
- 多行删除只用 read+edit 精确匹配；按行号范围删除曾两次造成大括号失衡。
- 单行 .Replace 与跨文件短语替换安全；改门禁优先“消息锚点整块替换”。
- `--self-test` 失败会写 `self-test-report.txt`（exe 同目录），先读它再动手。
- 每步验证链：`cmd /c BUILD_VIBE_MIC.cmd` → `.\\VibeMic.exe --self-test`（exit 0）→ `npm test` → `BUILD_RELEASE.ps1` → robocopy 到 `D:\Window\Desktop\dsh\vibe flow` → 静默安装 → 重启；每轮复核 `VibeMicAtvvCapture.exe` 的 SHA-256 仍为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。

## 2026-09-11 手势分层：UI 与派发已接（上一节的待做 1、2、3 全部完成）

### 已完成

- **快捷键页三行手势卡片**（`scripts/VibeMic.cs`）
  - `AddMappingOverviewCard` 由「短/长并排一条」改为 `短按 / 长按 / 双击` 三行；卡片 286×152、行距 `GestureCardPitch=164`，画布 960×610 → 960×960，`AutoScrollMinSize` 常数 `716` → `1066`；遥控器下方补「手势分层」说明卡（`AddGestureLegendCard`）；录音键卡片改用同一高度。原先只服务两层的 `supportsLongPress` 形参已删除（它与 `longKey` 是否为空完全等价），9 处调用点同步收窄。
  - 每行文本一律由 `GestureBindingStore.DescribeLayer(entry, kind, MappingCardActionText)` 生成；`layerEdit` / `layerTest` 按 `hardwareReady` 启用。
  - 点击行：短/长复用既有 `EditMappingAction` 路径；双击走 `UpsertLayer` + `TrySave`，落 `UserData\gesture-layers.json`。
  - 测试按钮：普通层复用 `TestMappingAction`；宏走 `GestureMacroRunner.RunSteps` 并按 `DescribeResult` 报告；一次测试只覆盖最后一个动作的回执，toast 如实写明。
  - **宏的图形编辑入口**（`ShowGestureMacroEditor`）：双击行新增「宏」按钮，对话框含宏名称 + 有序步骤表（`添加动作…` 复用既有动作选择器、上移 / 下移 / 删除这一步），上限 `MaxMacroSteps=8`。校验与落盘仍归 `GestureBindingStore`（`ValidateMacro` → `AttachMacro` → `TrySave`），因此编辑器的规则与桥接派发的规则不可能漂移；校验在对话框关闭**之前**做，非法宏就地改而不是弹到模态框后面的 toast。步骤必须能被真正执行（`NormalizeGestureLayerAction` + `IsPersistableMappingAction` 过滤 `none`/`passthrough`），否则作者写的列表会与实际执行的不一致。挂宏后该行单个动作按钮**锁定**并写明原因（宏优先），`删除宏` 把双击层交还给单个动作/上一层。
- **按键派发接线**（`scripts/VoxDeckInputBridge.cs`）
  - `HandleShortLongMapping` 的短/长两分支改为 `GestureLayerPolicy.Classify(实测按住时长, 前次点击是否在 DoubleTapWindowMs 内)` → `GestureBindingStore.ResolveSteps(entry, kind)` → `GestureMacroRunner.RunSteps(steps, ExecuteMappingAction)`；日志写 `DescribeResult`，失败如实显示「中止在第 N 步」。
  - 长按改由实测 `hold_ms` 判定（原来只看定时器阈值），因此略早于阈值松手也能正确归为长按。
  - 录音键（F5）在派发入口直接排除；`ToGestureEntry` + `NormalizeGestureAction` 把 `none`/`passthrough`/空统一归一为「未绑定」，避免被 `ExecuteMappingAction` 的 `false` 误报成失败步骤。
  - `ShortcutMapping` 新增 `doubleShortcut` / `macroName` / `macroSteps`；`BUILD_INPUT_BRIDGE.cmd` 补上三个手势源文件（此前只编进宿主）。
- **宿主下发**：`BuildKeyboardBridgeDocument` / `BuildBridgeMappings` 增加 `gestureOverrides` 参数（默认 null，既有 self-test 调用点不变），`SyncKeyboardBridgeConfig` 传入 `BuildGestureOverrides()`；分层的键带 `doubleShortcut` / `macroName` / `macroSteps`，并按三层任一有动作重算 `enabled`/`suppress`（短按被关掉时不会静默丢掉双击）。
- **门禁**：`validate.js` 删掉失效的 `shortEdit.Enabled`/`longEdit.Enabled` 锚点，新增手势卡片（`DescribeLayer` + 三种 `GestureKind`）、宿主编译手势源、桥接派发链路（`Classify`/`ResolveSteps`/`RunSteps`/`DescribeResult`）、录音键排除、两个构建源清单共 6 条断言；宿主与桥接各补 self-test 断言（层表与配置键一致、`none` 归一、双击层到达 bridge 文档并改变 revision、宏按序解析并在首个失败步停止）。

### 三处判断（与上一节字面写法不同，均已实测确认）

1. **短/长层仍归每 Profile 的映射表；`gesture-layers.json` 只拥有新增的双击层与宏。** 上一节写「行文本一律用 `DescribeLayer`」，但 store 是全局单条、映射表是按 Profile 的；若让 store 拥有三层，Smart Profiles 切换会让卡片与派发停留在旧方案。现按 Profile 读短/长、按 store 读双击/宏，`DescribeLayer` / `UpsertLayer` / `ResolveSteps` 全部照用。
2. **只有「已有长按层」或「已配双击/宏」的键进入分层派发。** 冻结的宿主 self-test 钉住方向键 `mode=="tap"`（按下即触发）且 `"none"` 必须是 `passthrough`；把 8 个可配置键一律改成 shortlong 会让 6 个键变成松手触发并引入 650ms 阈值。因此默认行为零变化，未分层键的「长/双」两行如实显示 `未配置`（传 null entry，不走回退话术）。
3. **双击的第一次点击仍会先执行短按层**（短按未配置时不会）。这是冻结的 `Classify(holdMs, previousTapWithinWindow)` 语义决定的；要避免就得给每次短按加 320ms 延迟，判断为更差的回归。

### 验证（本轮实测）

- `BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd` 编译通过；`VibeMic.exe --self-test` 与 `VoxDeckInputBridge.exe --self-test` 均 exit 0；`node scripts/validate.js`（= `npm test`）通过。
- `--ui-smoke` 实拍 `03-shortcuts.png`：三行正常、无文本遮挡；上键/左键/右键/确认键显示「短=动作、长=未配置、双=未配置」，录音键卡片同高。页面下半部分改用坐标几何校验（左列底 722、右列底 886、图例 570–880、底部说明 892–934、画布 960，互不重叠）。
- 截图脚本会按进程名抓到**已安装**的 `VibeFlow.exe`；抓新构建必须显式传 `-ProcessId`。
- `VibeMicAtvvCapture.exe` SHA-256 仍为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，冻结未动。
- `docs/images/03-shortcuts.png` 与 `03-shortcuts-screenshot.png` 已用新构建重拍并替换；其余页面图未动（本轮只有快捷键页有视觉变化）。
- 宏编辑对话框实拍（用 `PrintWindow` 只抓该窗口，不抓桌面）：标题「配置手势宏 · TV 键双击」、空步骤表、无内容时 `上移`/`下移`/`删除这一步`/`删除宏` 正确置灰，`保存` 为主按钮，无文本遮挡。
- self-test 补了两处此前没覆盖的：`GESTURE-MACRO-TOO-LONG`（正好 8 步通过、9 步拒绝）与「删除宏后双击层交还给回退层（本例为长按层）而不是被留成屏蔽状态」。

### 仍未做

- 宏的图形编辑入口**已补**（见上）；后续若要「宏内嵌条件/分支」或「把宏导出分享」仍未做——当前一个宏就是一串无条件、按序、失败即停的动作。
- ~~未新增宏编辑器的文档截图~~：**此项已失效** —— 宏的图形编辑入口连同宏功能本身在后续一轮被整体移除（`validate.js` 现在有一条「`ShowGestureMacroEditor` 必须不存在」的反向门禁），所以没有可截的界面。当时记录的约束仍然成立：`docs/images` 的图片清单同时钉在 `BUILD_RELEASE.ps1`、`BUILD_HARDWARE_CANDIDATE.ps1`、`validate.js`（4 处）与三份文档里，**新增**一张图要同步改 7 处以上；**重拍**已有图片不需要改清单（本轮重拍 `03-shortcuts.png` 就是走的这条路）。
- 上一节的「旧对话框收尾」（6 条读 `FocusTargetDialog.cs` 的断言改指 `FavoriteAppsPanel` 后删文件）与个人化资产未动。
- 真机 RC003 三层手势验收、DPI 矩阵、安装生命周期仍未验证。
- **已知空档**：短/长层归属 Profile、双击/宏归属全局 store，因此同一次双击在任何 Profile 下都相同；若日后要求「每 Profile 各自一份双击/宏」，需要把 `gesture-layers.json` 升成按 Profile 分节（现有 schema 1 可加字段迁移）。

### 本轮发布链路（已跑完，未签名候选）

- `BUILD_RELEASE.ps1` 全绿：两个 exe 编译、`Prepare-DevelopmentRuntime`、`validate.js`、`Test-ReleaseIdentity`、`Test-InstallerConfigMigration`、`Test-DevelopmentRuntime`、宿主/桥接/Capture 三个 self-test、`Test-InstallerRequirements`、`Test-V2FeatureSuite`、Inno Setup 6.7.3 编译、`Test-ReleaseArtifacts` 全部通过（Authenticode 未配置 → 未签名，属预期）。
- robocopy `release\Vibe-Flow-Windows-x64` → `D:\Window\Desktop\dsh\vibe flow`（用 `/E`，不要 `/MIR`，以免删掉部署目录里额外的配置/日志）。
- 静默安装 `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER` 退出码 0。
- 三处一致（新哈希，与上一节记录的旧值不同属预期）：`VibeFlow.exe` = `2DEC06F3AFAC56FF246C8C909436D722E8C6B07D41CD612AE31B585F19C5FE3D`（release 包 = 桌面发布目录 = 已安装），`VoxDeckInputBridge.exe` = `6D7DA01AE02E12ABD57178EA1F7033C31BB6CE136E7BE1C5FE51160177ADCEAD`，`VibeMicAtvvCapture.exe` 保持冻结 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。补上宏编辑入口后又跑了一轮完整链路并用同样步骤重新部署；**注意这一节的哈希已被后续「旧对话框退役」那轮再次刷新，当前值以文末为准**。
- **哈希每轮都会变，不要拿它判断"源码是否变过"**：.NET Framework 的 `csc` 默认不开启确定性编译，会写入时间戳/MVID，同一份源码两次构建产出不同哈希（`VibeMic.exe` 与 `VibeFlow.exe` 内容一致但两次构建的哈希不同）。判断"包含某次改动"要看 `VibeMic.exe` 的写入时间是否晚于 `VibeMic.cs`，或直接比对三处部署是否一致。
- 重装后启动正常：日志出现 `Config loaded version=7 revision=39c0ac02… mappings=9 routing=strict` 与热重载，用户配置未丢；对**已安装**进程重新截图确认三行手势卡片正常渲染。

### 操作教训（新增，务必记住）

- **静默安装前必须停掉全部三个进程**：`VibeFlow.exe`、`VoxDeckInputBridge.exe`、`VibeMicAtvvCapture.exe`。只停 Host 会让 `PrepareToInstall` 的 `WaitForVibeFlowExit` 失败——它检测的是 `Local\VibeMic` 互斥体，而 Capture/Bridge 仍持有该对象句柄，于是**静默安装以退出码 5 中止且不覆盖任何文件**（本轮先撞过一次）。停掉三个进程后重装即退出码 0。
- **截图脚本按进程名找窗口**：仓库根 `VibeMic.exe` 与已安装 `VibeFlow.exe` 同时存在时会抓错目标，必须显式传 `-ProcessId`。
- `--ui-smoke` 下抓 `04-diagnostics.png` 需要 `-AllowUnhealthyDiagnostics`，否则脚本在自检非 10/10 时直接抛错（与本轮改动无关）。
- **新写的临时 `.ps1` 若含中文字面量，必须带 UTF-8 BOM（或直接用 pwsh 7 运行）**：无 BOM 的 UTF-8 脚本会被 Windows PowerShell 当 ANSI 读，中文字符串把解析器带崩，且报错行号落在无关的 ASCII 行上（本轮为此白跑两次）。写一次性验证脚本时改用纯 ASCII + `[char]` 码点拼出中文最稳。

## 2026-09-11 旧对话框退役第 3 步完成（`FocusTargetDialog.cs` 已删除）

### 先纠正上一节的前提（重要）

上一节写「`FocusTargetDialog.cs` 目前仍参与编译，但**生产代码已不再引用**；只需把 6 条断言改指 `FavoriteAppsPanel` 后删文件」。实际核对后这个前提是**错的**，照字面做会直接编译失败：

1. **生产代码仍在用它**：`VibeMic.cs` 的 `DispatchUi()`（所有后台流程回到 UI 线程的唯一通道，指令派发/手势宏/学习都在用）调用 `FocusTargetDialog.TryPostToUi(this, action)`。
2. **两个编译期测试面仍引用该类型**：`VibeMic.cs` self-test 里 `new FocusTargetDialog(...)` 等反射夹具；`scripts/tests/FeatureSurfaceTests.cs` 断言 `FocusTargetDialog.SuggestedTargetName("chatgpt")`。
3. **目标文件也不对**：`FavoriteAppsPanel.cs` 只是「行 + 成功横幅」的构建器，`AppPickerDialog.cs` 只是应用选择器；学习捕获、稳定性、测试后保存这些语义早已搬进 `VibeMic.cs`（`CaptureFavoriteTarget` / `SavePendingFavorite`）与 `FocusTargetService`，`FavoriteAppsPanel.cs` 里一条都不存在。

所以按「**保留意图**」这条真正的约束执行，而不是按建议的文件名执行。

### 实际做法

- **搬迁生产必需的静态helper**：`TryPostToUi(Control, Action)` 移到 `scripts/ui/UiComponents.cs`（该文件本就是 `VibeMicForm` 的 partial），仍为 `internal static`；`DispatchUi` 与 self-test 的「不得向已释放窗口投递回调」断言一并改指它——这条断言保留，因为 helper 还在、语义还在。
- **随对话框一起退役的**：`ShouldRestoreAfterRecording`（唯一消费者是对话框自己 + 反射断言；新流程没有任何学习窗口，也就不存在「录音结束后窗口自我恢复并抢焦点」的路径）与 `SuggestedTargetName`（应用友好名现在来自已安装应用目录 `GetApplicationDisplayName`，那条路径另有断言）。
- **门禁改指真实落点**（6 条）：
  - 学习捕获/稳定性/测试后保存/状态 → `favoriteLearning`（`app` 中 `CaptureFavoriteTarget` 到 `OpenFavoriteApp` 之间）+ `FocusTargetService`（`CaptureFocusedEditableTarget`、`FOCUS-DESCRIPTOR-UNSTABLE`、`FOCUS-TARGET-NOT-EDITABLE`、`VerificationStatusText`）。
  - 「不得读剪贴板/派发键盘」与「不得读 UI 文本/RuntimeId/坐标」→ 同样改指 `favoriteLearning` + `FocusTargetService`（不再读已删文件）。
  - 「不能在没有学习成功的情况下保存」→ `FavoriteAppsPanel`（`已经识别到「`、`点「保存」完成设置`、`等待你点「保存」`、`onSave(pendingProcess)`）+ `app`（`BeginFavoriteAppLearning()`、`FAVORITE SAVE blocked=true reason=no_successful_learning`）。
  - 构建清单断言加强：`!hostBuild.includes("FocusTargetDialog.cs")`，防止这个文件被重新编进宿主。
- **删除**：`scripts/ui/FocusTargetDialog.cs`；从 `BUILD_VIBE_MIC.cmd` 源列表与 `validate.js` 的 `requiredFiles` / `read(...)` 移除。注意 `Test-V2FeatureSuite.ps1` 是**从 `BUILD_VIBE_MIC.cmd` 正则推导产品源列表**的，所以这里改一处，测试编译列表自动跟进——但也意味着 `FeatureSurfaceTests.cs` 必须同步改（已改）。

### 覆盖账（哪些保住了、哪些随 UI 一起退）

| 原断言/夹具 | 处置 | 保证是否还在 |
| --- | --- | --- |
| 未来 schema 只读（store 层） | 保留 | ✅ 仍在 self-test（直接断言 `focusStore.Load()`） |
| 未来 schema 对话框不允许测试 | 退役 | ⚠️ 仅 UI 层断言随 UI 消失；store 契约不变且仍被断言 |
| 学习捕获/稳定性/可编辑性 | 改指 service | ✅ 语义完整保留，且更强（断言真实生产路径） |
| 测试后保存 / 未学习不得保存 | 改指面板 + app | ✅ 保留（新流程更严：从「验证后自动保存」改为「验证后用户点保存」） |
| 录音期间不得抢焦点 | service 断言保留 | ✅ `FOCUS-CANCELED-VOICE` 等断言未动 |
| 对话框 200% DPI 可滚动不裁切 | 退役 | ⚠️ 随对话框消失；新「常用应用」面板的 DPI 表现**没有**对应断言（见下） |
| 不得读剪贴板/键盘/RuntimeId/坐标 | 改指真实落点 | ✅ 保留，且覆盖新学习代码 |
| 不得向已释放窗口投递回调 | 改指搬迁后的 helper | ✅ 保留 |
| ChatGPT 目标名建议 | 退役 | ⚠️ 友好名现由已安装应用目录提供，另有断言覆盖不同机制 |

### 验证（本轮实测）

- `BUILD_VIBE_MIC.cmd` 编译通过；`VibeMic.exe --self-test` exit 0；`node scripts/validate.js`（= `npm test`）通过。
- `scripts/tests/Test-V2FeatureSuite.ps1` 全绿（含 `FeatureSurfaceTests`、Focus、Capture Ask、Browser、HUD）。
- 全仓已无 `FocusTargetDialog` 的类型引用，只剩注释与两条「必须不存在」的门禁。

### 仍未做 / 新增空档

- **净减少已补回**：旧对话框的 200% DPI「裁切改为可滚动」断言随 UI 退役后，已在 self-test 里对着替代面板重建等价保证——`FavoriteAppsPanel.MeasureHeight` 必须为每一行与保存横幅各留空间（差值分别是 `RowHeight` 与 `BannerHeight+12`），`Build` 出的面板宽度不得超过宿主页宽，且**任一子控件不得越过面板边界**（这是原断言「不裁切」的等价形式）；另加 `Controls.Count >= 4` 防止空集合让边界检查空转。
  - **已做反向验证**：把 `MeasureHeight` 的横幅预留去掉后重新构建——构建仍成功（exit 0），但 `--self-test` 以「Favourite-app panel measurement does not reserve one row and the save banner」失败（exit 1）。证明这条断言确实会触发，不是空转。
  - 踩坑记录：第一次反向验证用 PowerShell `Set-Content` 改写该文件，**写掉了 UTF-8 BOM**，构建因此失败却只把旧 `VibeMic.exe` 留在原地（我又把构建输出管道到 `Out-Null`），于是 self-test 假绿。代码改动一律用 read+edit 的纪律在这件事上又救了一次。
- 个人化资产（仅元数据统计页 + 用语/片段包）仍未开始。

### 发布链路（本轮，未签名候选）

- `BUILD_RELEASE.ps1` 全绿（含 `Test-V2FeatureSuite`——它从 `BUILD_VIBE_MIC.cmd` 推导源列表，因此这次「删文件」被测试编译自动覆盖到）。
- robocopy → `D:\Window\Desktop\dsh\vibe flow`；三个进程全停后静默安装退出码 0；重启正常。
- 三处一致：`VibeFlow.exe` = `05180375D5A5253E21FEF2A0F86F5739C466EA0C53E5BB58B3F4778F3C0D6905`，`VoxDeckInputBridge.exe` = `709E4AD93F930DBF6EEFAD2A2186EF7F4363EA74441DC3CF11F3F0F56F527637`，`VibeMicAtvvCapture.exe` 保持冻结 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。（补上「净减少」那条布局断言后重跑发布链路，host 哈希由 `91B6E4EF…` 变为上值；桥接未改，但 `csc` 非确定性编译使哈希也不同属正常。）
- **回归证据**：对已安装进程重拍 `03-shortcuts.png`，与退役前那次截图 **SHA-256 完全相同**（`ba9cb85f…`），说明搬走 `TryPostToUi`、删掉对话框、补布局断言都没有造成视觉或页面构建回归；日志无异常，配置热重载正常。

## 2026-09-11 个人化资产（1/2）：仅元数据的使用统计

### 用户已确认的边界

路线图第 2 项「个人化资产」含两件：仅元数据的统计页 + 用语/片段包。片段包必然要落盘用户自备文本，与「不保存第三方转写文字」的文案相邻，因此先请用户定调：**选 (a)**——落盘到 `UserData`、界面明确标注「这些是你自己写的文本，仅本机保存」、触发方式只做按键直接输入。本轮先做统计部分。

### 已完成

- **新增 `scripts/features/UsageStatsPolicy.cs`**（纯策略、**刻意为纯 ASCII**）：`Summarize(IList<string> lines, Func<string, DateTime?> timestampOf)` 只聚合「结束回执」行（`WETYPE SESSION END` / `TRANSCRIPTION SESSION END`），干净运行必须同时满足 `audio_delivered=True` 与 `submitted=True` 且不含 `SESSION ERROR`，否则计为未完成——成功率不会自我美化；最长间隔只在时间戳可解析时才累加。
- **宿主侧**：`BuildUsageStats()` 读 `sessionDir\vibe-mic-runtime.log` 的 512 KB 尾巴（复用既有 `ReadLogTailLines` 与 `TryParseRuntimeTimestamp`，不新写任何日志）；`UsageStatsLine()` / `FormatUsageGap()` 负责中文展示。**格式化的中文刻意留在宿主**：策略文件无 BOM，而无 BOM 的源文件里放非 ASCII 字面量会被 `csc` 按 ANSI 读坏（下面「操作教训」第 5 条踩过）。
- **界面**：设置页「隐私与维护」卡片新增「使用统计（仅元数据）」区块——会话数/成功/未完成/成功率/最长间隔一行，加一行范围说明。卡片 318→420，设置页滚动高度 990→1092。
- **门禁**：`requiredFiles` 与 `hostBuild` 收录新源；新增 3 条断言——策略只能以「行 + 时间戳函数」聚合（签名被钉死）、策略源码不得出现 `ExtractMetric|WindowTitle|TargetName|TranscriptionText`、设置页必须显示该区块与范围说明。
- **self-test**：新增 `RunUsageStatsSelfTests()`，覆盖计数与干净运行判定、成功率、最长间隔、**无时间戳不得编造间隔**、**空历史不得编造成功率**，以及 `FormatUsageGap` 的秒/分/小时三档。

### 隐私边界（这是本轮的重点，不是附注）

- **不新增任何落盘**：统计完全由宿主**已经**在写的运行日志聚合，所以没有产生新的个人数据，也没有新的清理/迁移负担。
- **只读时间戳与两个布尔标记**：策略的入口签名就是 `IList<string>` + 时间戳函数，拿不到别的东西；门禁把这条签名和「不得出现识别性字段」都钉住了。
- **面板自己声明范围**：只统计有结束回执的会话、范围限于当前日志窗口（日志限长后旧记录随滚动丢弃）、不含录音/转写文字/窗口标题/设备地址。宁可把范围写窄，也不让数字暗示它覆盖不了的东西。

### 验证

- `BUILD_VIBE_MIC.cmd` 编译通过、`VibeMic.exe --self-test` exit 0、`node scripts/validate.js` 通过。
- 设置页区块在折叠线以下；改用**无障碍树探针**确认渲染与几何：`使用统计（仅元数据）` x=946 y=1291 w=420 h=26，范围说明 x=946 y=1349 w=890 h=42。
- 没有为了截图去抓桌面。顺带记录：WinForms `AutoScroll` 的滚动条**不是** `EnumChildWindows` 能枚举到的子窗口（找到 0 个），滚轮与 `WM_VSCROLL` 两种办法都没滚动，所以最后用 UIA 探针——它给出的证据比截图更硬（文本 + 几何都拿到了）。

### 仍未做（2/2）

- **用语/片段包**：设计要点已定——新增 `snippet:<id>` 动作类型，走既有映射/手势层通道下发到桥接；注入用 `SendInput` 的 `KEYEVENTF_UNICODE` 逐字符直写、**不碰剪贴板**（剪贴板注入正是竞品调研里被点名的技术债）；存储 `UserData\snippets.json`（原子写 + `.bak`，与其他 store 一致）；首次使用时明确说明这是用户自己的文本、仅本机保存。

## 2026-09-11 个人化资产（2/2）：用语片段（用户自备文本）已完成

### 已完成

- **`scripts/features/SnippetStore.cs`（新，纯 ASCII）**：`snippets.json` 原子写 + `.bak`，schema 1；`Snippet{id,name,text}`；校验 `SNIPPET-TEXT-EMPTY` / `-TEXT-TOO-LONG`(500) / `-NO-NAME` / `-NAME-TOO-LONG`(40) / `-TOO-MANY`(40)；`ActionFor` / `IdOf` / `IsSnippetAction` / `IsManageAction` / `NormalizeText`（保留换行、去首尾空白）/ `Upsert` / `Remove` / `Describe`。
- **动作只带 id**：映射表里存的是 `snippet:<id>`，短语正文只在 store 里。宿主 `IsSupportedMappingAction` 接受片段动作、拒绝 `snippet:manage`；`CustomActionText` 渲染成「片段 · 名字」，id 找不到时显示「用语片段（已删除）」而不是装作还能用。
- **命名钩子**：`CustomActionText` / `MappingCardActionText` 是静态且被门禁钉住的，拿不到宿主实例状态，因此由宿主在构造时把两个只读查询发布到 `SnippetNaming`（`ResolveName` / `ResolveAll`）。headless 工具里它们保持 null，所有调用方都容忍。
- **管理界面**：动作选择器新增「用语片段 · 管理…」入口 → `ShowSnippetManager`（列表 + 名字 + 多行文字 + 新建/保存/删除/关闭）。选择器同时列出每个已存片段（「用语 · 名字」）供绑定到按键或手势层。
- **桥接注入**：`BridgeConfig.snippets` + `BridgeSnippet`；`snippet:` 进入 `IsCustomAction`；`HandleCustomAction` 新分支 → `SnippetTextFor(id)` → `TypeUnicodeText` → `SendUnicodeCharacter`（`KEYEVENTF_UNICODE`，每字符 2 个 INPUT，换行发真实 Enter）。**全程不碰剪贴板**；日志只写 `characters=<长度>` 从不写正文。
- **桥接文档**：片段表随映射文档下发（桥接不必知道用户数据在哪），并计入 `revision`，所以**改一句短语会让桥接重载**，绑定的按键不会再打旧文本。
- **门禁**：requiredFiles / hostBuild；断言片段 store 无剪贴板路径、桥接全文无 `Clipboard.|SendKeys.|keybd_event`、注入必须走 `KEYEVENTF_UNICODE`、**日志不得出现 `+ snippetText +`**（只许长度）、宿主必须保留管理入口与本机保存说明。
- **self-test**：`RunSnippetSelfTests()`（校验码、id/动作往返、id 不重复、删除精确、命名钩子下「片段 · 名字」与「已删除」两种文案、片段进入桥接文档、**改文本必须改变 revision**、映射校验接受片段但拒绝 `manage`）；桥接侧补 `IsCustomAction("snippet:…")`、`SnippetTextFor` 大小写解析、未知/空 id 解析为空、`BridgeConfig.Default().snippets` 非 null。

### 一处刻意的测试克制

桥接 self-test **不执行真实注入**：`SendInput` 会把短语打进当时的前台窗口（也就是你的桌面）。因此只测「动作识别 + 文本解析 + 未知 id 归零 + 默认值安全」，注入本身留给真机验收。这条已写在测试注释里。

### 隐私边界的精确化（顺手修了一处会变成不实陈述的文案）

原用户指南写「（言灵）不把文字写入或从剪贴板回填到输入框」。片段功能确实会把**用户自己的**文字写进输入框，所以这句话若不改就会变成不实陈述。已改为区分主体：不写入的仍然是**第三方转写文字**；片段是唯一例外，并明确它由用户自备、仅存本机、逐字符直接写入、不经过剪贴板、日志只记长度。

### 验证

- `BUILD_VIBE_MIC.cmd` / `BUILD_INPUT_BRIDGE.cmd` 编译通过；`VibeMic.exe --self-test` 与 `VoxDeckInputBridge.exe --self-test` 均 exit 0；`node scripts/validate.js` 通过；`Test-V2FeatureSuite.ps1` 全绿（它会从 `BUILD_VIBE_MIC.cmd` 推导源列表，新 store 自动进入测试编译）。
- 真机 RC003 触发片段输入、以及 200%/多 DPI 下的片段管理对话框布局，仍需真机验收。

### 发布链路（本轮）

- `BUILD_RELEASE.ps1` 全绿 → robocopy → 三进程全停 → 静默安装 exit 0 → 重启正常。
- 三处一致：`VibeFlow.exe` = `07839379561749B2134EC705F161CB39FCA79038B6840D7E1D9E03627087DEB5`，`VoxDeckInputBridge.exe` = `189DBF966AE6D0F29563BC158AB374A357294E4FC9FAC46615B8D82C23165502`，`VibeMicAtvvCapture.exe` 保持冻结 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`。
- **视觉确认已补齐**：片段管理对话框已用「UIA 识别 + 窗口消息点击」驱动到并实测：窗口标题 `用语片段`、尺寸 646×549（客户区 640×520 + 边框）、本机保存说明存在、**命名控件两两重叠数为 0**——先前手算发现并修掉的状态行/按钮重叠（440–460 与 470–508）由此得到自动验证。测试时的布局检查脚本按「任意两个命名控件矩形是否相交」判定，比截图更能直接证明「没撞」。
- 驱动过程中确认的两件既有事实（不是本轮改动）：
  - **WinForms 把按钮的 `Text` 报成 UIA 名，而不是我们设的 `AccessibleName`**，且该树里控件的 `ControlType` 全是 `Pane`。因此按 `AccessibleName`（如「上键当前动作：…」）找按钮会失败；按可见文本或窗口类（EDIT/BUTTON）找才可靠。
  - 顺带在**实时**无障碍树里看到手势分层的真实渲染：`手势分层` 图例、每键 `短/长/双` 三行、`宏` 按钮，以及 Home 卡片的 `未配置（回退到短按：显示桌面）`——与 `DescribeLayer` 的设计一致；点击 `上方向` 打开的选择器标题为 `配置 上键短按`，证明「行 → 层」的传参正确。
- UIA 驱动操作经验：`SendMessage` 的 P/Invoke 必须写 `EntryPoint = "SendMessageW"`，否则托管名会被当成导出名而报 `EntryPointNotFoundException`；把构建输出管道到 `Out-Null` 会掩盖编译失败并让后续 self-test 假绿（本轮又差点踩到）。

## 2026-09-11 用户要求「把长短和双击都配上」，因此暴露并修复一个真缺陷

### 缺陷：6 个键的「长按」行是死的

`GestureLayerKeys` 只为 Home / 功能键 提供了 `:long` 配置键，其余 6 个键（上/左/右/下/确认/TV）第三列为 `""`；而 `EditGestureLayerAction` 里写的是 `if (string.IsNullOrEmpty(configKey)) return;`——**这 6 行的「长按」看起来可点，点下去静默什么都不做、也不落盘**。用户「帮我把所有快捷键长短和双击都配置上」的请求正好撞在这上面。这是我上一轮引入的缺陷，不是既有行为。

### 修复：让 store 拥有那些没有配置长键的键的长按层

- `GestureLayerOverride` 增加 `LongAction`；`GestureOverrideFor` 只在**该键没有配置长键时**才从 store 读 `longAction`（Home/功能键 的长按仍归 Profile 映射表所有，绝不被全局值悄悄覆盖）；`Layered` 判定把「store 里的长按」也算作已分层。
- `GestureLayerFor`：无配置长键的键，`longAction` 取 store；有配置长键的键照旧读映射表。
- `EditGestureLayerAction`：把「这一层存在哪里」提取成纯函数 `IsConfigBackedGestureLayer(kind, longKey)`——短按永远在映射表；长按仅在**该键有配置长键**时在映射表；其余（含双击、以及无配置长键的长按）由 store 落盘。顺带把保存时的 `UpsertLayer(..., kind, ...)` 改成写真实层（原来自称 `layer=double`）并对 `none` 归一，因此「不执行动作」现在真的清空该层。
- `ConfigurableBridgeMapping`：分层的键若配置长键为空，则用 store 的长按填充 `longShortcut`，所以长按真的会执行卡片上写的动作。
- **self-test**：新增对 `IsConfigBackedGestureLayer` 六种组合的断言（短按恒真、Home/功能键长按为真、空长键的长按为假、双击恒假）。**门禁**：新增「三层都必须能落盘」的源锚点断言。

### 顺带修掉的显示问题

`CustomActionText` 对 `pageup`/`pagedown`/`tab`/`shift+tab`/`escape` 没有中文名，会在全中文界面里显示生词（用户卡片上就出现了 `pageup`）。已补「向上翻页 / 向下翻页 / Tab 键 / 反向 Tab / Esc 键」。

### 已为用户实际配置（保留了用户自己的短按与两个已有双击）

用户 `vibe-mic-config.json` 的 `general` 表里短按**早已被他自己改过**（上=复制、下=粘贴、左=粘贴、右=撤销、功能=删除、Home=显示桌面、TV=任务视图、确认=换行），并且 `gesture-layers.json` 里已有两条他自己的双击（上=剪切、左=撤销）——**全部保留，未覆盖**。只补齐缺失层：

| 键 | 短按（保留） | 长按 | 双击 |
| --- | --- | --- | --- |
| 上键 | 复制 | 向上翻页 | 剪切（用户原有） |
| 左键 | 粘贴 | 返回上一页 | 撤销（用户原有） |
| 右键 | 撤销 | 重做 | 保存 |
| 下键 | 粘贴 | 向下翻页 | 全选 |
| 确认键 | 确认 / 换行 | 静音切换 | 播放 / 暂停 |
| 功能键 | 删除 | 查找（原有配置长键） | 音量增加 |
| Home | 显示桌面 | 区域截图（原有配置长键） | 打开 / 切换 Cursor |
| TV | 任务视图 | 打开 / 切换 ChatGPT | 音量减少 |

- 新增的 12 个动作互不重复，也不与用户既有绑定重复；只写了 `gesture-layers.json` 一个文件，**没有改动 `vibe-mic-config.json`**（Home/功能键 的长按本来就有值）。
- 写入前已停掉三个进程，并在同目录留了 `*.before-agent-<时间戳>.bak` 备份。
- **行为副作用（必须让用户知道）**：双击需要观察「松手」，所以这 8 个键被提升为分层派发后，**短按从「按下即触发」变成「松手触发」**。清空双击即可回到原来的行为。
- **全局性**：store 拥有的是全局层，所以这 6 个长按与 8 个双击在 3 个 Profile 下都一样；短按仍按 Profile 各自生效。

### 验证

- 构建 + 宿主/桥接 self-test + `validate.js` + `Test-V2FeatureSuite.ps1` 全绿。
- 发布链路：`BUILD_RELEASE.ps1` 全绿 → robocopy → 静默安装 exit 0 → 重启；三处一致 host `EC8FF15F25C4A9C02CC98EAF618D865DE187D34EDE0D8D6E6C9A1D738434F6E0`，Capture 仍冻结 `B62DE035…2E683`。
- **对正在运行的已安装进程**读回无障碍树，8 个键 × 3 行共 **24 行全部有动作**，无「未配置」残留；`pageup`/`pagedown` 已显示为中文。
- 用户指南新增「手势分层与宏（快捷键页）」一节，用大白话解释了宏是什么、怎么建、规则与限制。

## 2026-09-11 宏已按用户判断移除（保留三层动作）

### 用户为何认为宏没必要——以及这个判断是对的

用户在配好三层动作后提出「宏好像没有真正的必要使用场景」。复盘后确认这不是感觉问题，而是三个结构性原因：

1. **宏只能挂双击层**（冻结后端的 `ResolveSteps` / `DescribeLayer` 只对 `Double` 返回宏步骤），而双击是全界面最别扭的手势；更糟的是**双击的第一下会先把该层的短按动作执行掉**（我当初为避免给每次短按加 320ms 延迟而选择的语义）。于是「双击 Home 跑一串动作」实际是：先把桌面最小化，再在空桌面上跑那串动作——**结果不对，不只是收益小**。
2. **步骤之间没有等待**，8 步一瞬间发完；而切窗口/启动应用是异步的（几百毫秒），所以任何涉及换窗口的宏都会把键发到旧窗口。
3. **宏想解决的问题已被 Smart Profiles 自动做了**（按前台应用自动换 Profile，无需手势）。

### 结论与做法（用户选择 A：移除）

- **移除**：卡片上的「宏」按钮、`ShowGestureMacroEditor` 对话框、`GestureMacroErrorText`、`TestGestureLayer` 的宏分支；宿主侧的宏管线（`GestureLayerOverride` 的宏字段、`GestureOverrideFor` / `GestureLayerFor` / `ConfigurableBridgeMapping` 的宏读写）；桥接侧的 `ShortcutMapping.macroName/macroSteps` 与 `ToGestureEntry` 的宏解析。
- **保留（休眠）**：`GestureBindingStore` 的 `AttachMacro` / `ValidateMacro` / `HasMacro` / `macroName` / `macroSteps` 与 `GestureMacroRunner`。前者是「已完成的后端」且带 self-test；后者仍是**唯一派发引擎**（`DispatchGestureLayer` 用 `RunSteps` 跑每层那一个动作）。因此将来若要重做宏，是「补 UI + 补管线」而不是重写后端。
- **保证「宏不可能再被执行」**：宿主不再下发宏字段；桥接不再解析宏字段；`ToGestureEntry` 明确注释只填三层动作。这样即使磁盘上残留旧文档也不会复活宏。
- **门禁改为负向**：断言 `ShowGestureMacroEditor` / `GestureMacroErrorText` / `GESTURE MACRO` / `macroSteps` / `macroName` **不得出现**，并注明原因（避免在没有解决「第一下会触发短按 + 无等待」之前悄悄长回来）；同时把宿主侧的层门禁改为断言真实编辑路径（`UpsertLayer(document, gestureKey, kind,` + `TrySave` + `TestMappingAction(label, action)`），而不是靠 self-test 里的字符串撑过去。
- **self-test 平移**：宿主侧原本用宏夹具验证「改层必须改变 bridge revision」，改为用**双击动作本身**验证（`win+shift+s` → `ctrl+s` 必须改变 revision，且文档不得含 `macroName`/`macroSteps`）；桥接侧原本验证「宏按序解析」，改为验证**每层恰好解析出一个动作**，并保留 `RunSteps` 的「首个失败步即停」契约（引擎仍在用）。

### 一处方法说明（与项目纪律的关系）

删除 `GestureMacroErrorText` + `ShowGestureMacroEditor` 共 265 行时，我没有用 read+edit 逐字复现（265 行的字面匹配本身就是纪律警告过的易错操作），而是：先从**原始字节**确认编码（该文件无 BOM、行尾 CRLF）、校验首行/末行/次行与**跨度内 21 个 `{` 对 21 个 `}`**、再按范围删除并原样写回；删除后立即确认编译通过、`ShowGestureMacroEditor`/`GestureMacroErrorText` 已消失、中文（`向上翻页`/`用语片段`）未损坏。纪律的本意是「别让大括号失衡」，这次用括号计数把它变成了可验证条件，而不是靠肉眼。

### 验证

- 构建两个 exe 通过；宿主与桥接 self-test 均 exit 0；`validate.js` 通过；`Test-V2FeatureSuite.ps1` 全绿。
- 发布链路与对已安装进程的读回验证见下一节（本轮已重新发布并部署）。

## 2026-09-11 文案收尾：把「输入目标」这个旧词从**在用的**代码与随包文档里清掉

上一轮计划里的第 ② 项（「按钮与文案仍叫*锁定输入目标*，要改成直白说法」）此前只改了按钮，**执行回执与错误文案、以及随安装包一起交付的文档里还留着旧词**。本轮收尾。

### 用词规则（与更早那次统一一致）

- 用户可见的**概念**（哪个应用 + 哪个输入框接收文字）→ **工作流**；
- 字面意义上的**控件** → **输入框**；
- 不再使用「输入目标」。

### 代码（`scripts/features/FocusTargetService.cs`，共 16 处）

- 该文件内 `输入目标` → `工作流` 一次性替换（`未锁定输入目标，未发送按键` → `未锁定工作流，未发送按键`；`输入目标焦点已恢复` → `工作流焦点已恢复`；`ActionResult.Create("锁定输入目标"` → `"锁定工作流"` 等）；
- 仅有一处需要特殊处理：`当前环境不支持 Windows 工作流恢复` 语义不通，改为 `当前环境不支持 Windows 焦点恢复`（说的其实是 UIA 焦点恢复能力）。

### 宿主（`scripts/VibeMic.cs`，6 处用户可见文案）

逐条按含义改，而不是机械替换：

| 位置 | 原文 | 现在 |
| --- | --- | --- |
| 首次设置 | 按需添加输入目标 | 按需添加**常用应用** |
| 会话健康 NextAction | 但未确认输入目标；请重新聚焦输入框后再试 | 但未确认**输入框**；请重新聚焦后再试 |
| 诊断摘要 | 输入目标跟踪（不读取文字） | **落字位置**跟踪（不读取文字） |
| 诊断摘要 | 未记录输入目标 | 未记录**落字位置** |
| 会话总结 | 但未确认输入目标 | 但未确认**输入框** |
| 首页提示 | 先确认 APP 输入目标，再按住录音键说话 | **先点一下目标应用的输入框**，再按住录音键说话 |

self-test 夹具里的 `"AI 输入目标"` 也顺手改成 `"AI 输入框"`。

### 随包文档（9 个文件）

`V2_0_USER_GUIDE_ZH.md`（含一节标题 `## Smart Focus 输入目标` → `## Smart Focus 工作流`）、`FEATURES_ZH.md`、`USER_GUIDE_ZH.md`、`QUICK_START_ZH.md`、`README.md`、`V2_0_RELEASE_NOTES_ZH.md`、`V2_0_HARDWARE_TEST_MATRIX_ZH.md`、`V2_0_AUTOMATED_TEST_REPORT_ZH.md`、`V2_0_INSTALLER_GUIDE_ZH.md`。其中 `V2_0_RELEASE_NOTES_ZH.md` 那句「路径 B（语音页"锁定输入目标"）」改成了「语音页选择常用应用并学习输入框」——原文描述的是一个**已经不存在**的按钮。

### 刻意不动的地方

- **已归档功能**：`CaptureAskService/Form/Integration`、`ProjectSpaceRunner` 与 `VibeMic.cs` 里 Project Space 步骤接线（`ActionResult.Create("添加应用", "输入目标"`）仍留着旧词。它们是历史代码，不在当前导航里；改它们没有用户收益，却会无谓扩大改动面。
- **历史记录**：`V2_0_PROGRESS.md`、`V2_0_IMPLEMENTATION_PLAN_ZH.md`、`V2_0_UI_SPEC_ZH.md`、各调研报告、`V1_2_HARDWARE_ACCEPTANCE_ZH.md`。日志与归档不重写。
- `validate.js` 里那条 `!overviewSection.includes('SecondaryButton("输入目标"')` 是**保证首页不再出现该按钮**的负向断言，保留。

### 门禁（防回退）

- 代码：断言 `FocusTargetService.cs` 全文不得含 `输入目标`，且宿主不得再出现 `未确认输入目标` / `未记录输入目标` / `按需添加输入目标` / `先确认 APP 输入目标`；
- 文档：对 10 份**随包且在用的**文档逐一断言不得含 `输入目标`（`v2Guide`、`featuresBoard`、`readme`、`quickStart`、`guide`、`v2ReleaseNotes`、`v2HardwareMatrix`、`v2AutomatedTests`、`v2InstallerGuide`、`v2Migration`）。

### 验证

- `BUILD_VIBE_MIC.cmd` 编译通过；`VibeMic.exe --self-test` exit 0；`validate.js` 通过；`Test-V2FeatureSuite.ps1` 全绿。
- 全仓仅剩「已归档功能 + 历史文档 + 该负向门禁」中的旧词，已在上面逐条列明。
- **对已安装的 `VibeFlow.exe` 做二进制取证**（按完整字符串，逐条「旧串必须消失、新串必须出现」）：

  | 串 | 旧 | 新 |
  | --- | --- | --- |
  | `未锁定输入目标，未发送按键` / `未锁定工作流，未发送按键` | 无 | 有 |
  | `同一时间只能锁定一个输入目标` / `…一个工作流` | 无 | 有 |
  | `输入目标尚未完成一次真实测试` / `工作流尚未完成…` | 无 | 有 |
  | `输入目标焦点已恢复` / `工作流焦点已恢复` | 无 | 有 |
  | `未确认输入目标` / `未确认输入框` | 无 | 有 |
  | `先确认 APP 输入目标` / `先点一下目标应用` | 无 | 有 |

- 同时确认旧词**仍以归档功能的形式存在于二进制里**（`未锁定输入目标，未执行粘贴`、`重新锁定输入目标后重试`、`输入目标已失去焦点` 均来自 `CaptureAskService`）——这与「已归档功能不动」的决定一致，不是遗漏。
- 发布：`BUILD_RELEASE.ps1` 全绿 → robocopy → 静默安装 exit 0 → 重启；三处一致 host `D11B730B611088D115B17B4CE509F047861881C66E0B5E1A527B68AB7CB6884A`，Capture 仍冻结冻结值 `B62DE035…2E683`；随包 `docs/V2_0_USER_GUIDE_ZH.md` 已确认不含旧词。

### 操作教训（新增）：扫 .NET 程序集里的 UTF-16 字面量要按**字节**或**两种对齐**扫
我第一次用 `[Text.Encoding]::Unicode.GetString(整个文件)` 再 `Contains` 去查字符串，结果 `FocusTargetService` 的字面量**全部查不到**（连 `SMART FOCUS target_id=` 都没有），一度以为该文件没被编译进去。实际原因是：`#US` 堆里的字面量**不保证落在偶数偏移**，而整段按 UTF-16 解码会把奇偏移处的字符两两错位，于是 `Contains` 全部假阴。

正确做法二选一：把待查串编码成 UTF-16 字节后做**字节匹配**（或用 `indexOf` 在两种偏移各解一次）。这一点很重要——**用错误的扫描方法会得出「改动没生效」的错误结论**，进而去改本来正确的东西。本轮的结论已用字节匹配复核。
- 本轮补了一条更实在的覆盖：片段 store 的**文件往返**（真实临时目录写入 + 重新加载 + 未来 schema 视为只读），此前 self-test 只用了内存文档。

## 2026-09-11 工作流页收口：状态、动作、编辑（用户报「保存之后就没有后续步骤了」）

用户报：在工作流页「添加应用 → 进目标 APP 输入框 → 学到文字进入 → 点保存」之后，页面**只剩「添加应用」和「打开」**，没有后续步骤、没有完整状态、也没有编辑管理。

### 根因（两条，都有代码证据）

1. **新学到的应用被写成 `mode = null`**。`SaveLearnedFavoriteTarget` 与 `SyncFromTargets` 创建 `FavoriteApp` 时**都没有赋 `mode`**，而 `OpenFavoriteApp` 只在 `IsWorkflowMode(app.mode)` 为真时才把应用设为「当前」。于是一个刚保存的应用**永远无法通过「打开」成为接收文字的那个**，用户只能去猜那个小胶囊。核对用户真实数据确认：`favorite-apps.json` 里两个应用（notepad / windowsterminal）**都是 `"mode":null`**，而 `selectedProcess` 是 notepad——存储状态本身就自相矛盾。
2. **两种模式的含义在代码里就是互相矛盾的**。`FavoriteApp.mode` 的注释写「workflow = 通过「打开」召唤；空/shortcut = 按录音键直接把文字送进去」，`ToggleFavoriteMode` 的气泡文案也是这个说法；但 `OpenFavoriteApp` 的行为恰好相反（workflow 才设为当前）。而运行时实际只认 `selectedProcess`，**模式从来没影响过文字去哪**——所以「快捷键 → 文字直接进它」这句是假的。

另外：那一行主按钮只有四种形态（学习/打开/重新学习/删除），**没有编辑、没有测试、无法显式设为当前**；缺口诊断卡（`WorkflowCards`）在**自检页**，工作流页看不到。

### 用户已定的范围

- 「后期的动作」= **只做管理动作**（不做说话结束后的自动步骤，不碰「不自动发送」的边界）；
- 模式胶囊**给它真实含义**。

### 已完成

- **新增 `scripts/features/FavoriteAppStatus.cs`**：状态模型（`NotLearned` / `LearnedUnverified` / `Verified` / `TargetMissing`）＋ `CanBecomeCurrent` ＋ 行文案 `DescribeForRow` ＋ 模式说明 `ModeTooltip` ＋ 模式常量与 `IsWorkflowMode` / `IsShortcutMode` / `ModeValue`。
- **模式语义改为运行时契约**：**工作流** = 可被设为「当前」，说话前恢复焦点到它学到的输入框，文字固定送进它；**快捷键** = 只在点「打开」时被召唤，**不自动接走文字**。把当前应用切回快捷键时**自动取消「当前」**（`dropped_current` 日志），不再留下「文字还会进一个已经不认领它的应用」的状态。气泡提示与 toast 文案全部改写为这个契约。
- **默认值修复**：新学到的应用、由已验证目标镜像出来的应用都显式写 `mode = workflow`；`FavoriteAppStore.Load` 把历史空/非法 `mode` **归一为 workflow，并在首次读到时就地落盘**（一次性自愈，之后幂等）。
  - ⚠️ **我在这里先写错过一次**：最初只做了「内存归一」，并在文档里写下「用户现有记录会在下次启动被修正」——**实测并没有**。原因是 `SyncFavoriteApps()` 只在特定触发路径里被调用，**不在启动流程里**，所以文件里始终还是 `null`。改成 Load 自愈后，已用**用户真实数据实测确认**：notepad 与 windowsterminal 的 `mode` 都从 `null` 变成 `"workflow"`。
  - 教训：**「读取时归一」不等于「磁盘上被修正」**。要声称数据被迁移，必须在真实数据上验证文件字节，而不是只验证代码路径。
- **行内状态与动作**：每行显示明确状态，并新增 **设为当前**（未学习/目标失效时禁用并说明原因）与 **编辑**；主按钮对「目标已失效」也改为「重新学习」（此前会显示「学习」，与状态文案不一致）。状态文案由 `FavoriteAppStatus.DescribeForRow` 统一生成；**刚学到未保存的行仍保留「等待你点「保存」」**（这条是门禁帮我抓回来的回归，见下）。
- **编辑对话框**（`ShowFavoriteAppEditor`，放在 VibeMic.cs——面板文件被门禁禁止出现 `TextBox`）：显示名称、模式（带实时说明）、学到的输入框详情（策略 / 控件类型 / 上次验证时间）、**测试焦点**（`ExecuteForVerification`，不必重新学习）、**设为当前**、保存。日志 `FAVORITE EDIT/TEST/CURRENT process=` 全程只记进程名与结果，不记用户文字。

### 门禁帮我抓回来的两件事（值得单独记）

1. **pending 文案回归**：我把行状态文本整体换成 `DescribeForRow` 后，刚学完待保存的应用被显示成「还没学习过」（因为它的 `targetId` 还是空的），而上方横幅却在说「点「保存」完成设置」。原有门禁 `"等待你点「保存」"` 直接红，才让我发现这个前后矛盾的回归。
2. **我自己的按钮几何 bug**：新加的「设为当前 / 编辑 / 删除」我算了 X 坐标却**忘了设宽度**，而 `MakeButton` 硬编码 `120×40`，于是三个按钮互相重叠、最后一个还超出面板右边界（932 > 924）。这次不是靠肉眼发现的——我在 self-test 里给这个面板加了一条**同级控件两两不重叠**的断言，它把冲突的两对按钮连同坐标一起打了出来。

### 验证

- 构建通过；宿主 self-test exit 0（含新加的：状态分类四态、`CanBecomeCurrent` 边界、模式常量与 `ModeValue`、行文案与状态一致、**面板同级控件不重叠**）；`validate.js` 通过。
- 新增门禁：状态模型与模式语义必须存在、行必须提供「设为当前 / 编辑」、宿主必须能改名/切模式/测试焦点、构建必须编译 `FavoriteAppStatus.cs`、切回快捷键必须取消当前。
- 顺带确认了两件事：源码是 **UTF-8 无 BOM**（csc 优先按 UTF-8 读，所以无 BOM 也能写中文；本轮之前我对此的担心过宽）；以及 `Get-Content` 不带 `-Encoding` 会按 **ANSI(936)** 读，把用户 `favorite-apps.json` 里正常的「输入框」显示成乱码——**别用它核中文文件**，用 read 工具。

## 2026-09-11 路线图第 3 项：UWP / 打包应用盲区实测（发现一个更严重的既有缺陷）

用户选了路线图第 3 项（UWP 冷启动学习 / AUMID vs 进程名 / UWP 图标 / 带参数启动的实例复用 / 首页入口是否在首屏之上）。**先说最重要的结论：真正的根因不是 UWP 特有的，而是「焦点型目标在学完之后永远无法被验证」**，这条同时打死了用户已经保存的 notepad 和 windowterminal 两个应用，也打死了产品文档明确宣称支持的「控制台 / 终端窗口」。

### 硬证据（用户真实日志，不是推测）

`vibe-flow-host.log` 里用户 11:26 那一轮的实际记录：

```
FAVORITE LEARN failed=true process=notepad code=FOCUS-PROCESS-MISMATCH
FAVORITE LEARN failed=true process=notepad code=FOCUS-TARGET-NOT-EDITABLE
SMART FOCUS target_id=focus-e26d401c0d68 strategy=uia_focus state=error code=FOCUS-TARGET-STALE   （每秒重复）
VOICE FOCUS LOCK armed=false reason=focused_observation_FOCUS-PROCESS-MISMATCH                     （每秒重复）
VOICE INPUT TARGET ready=false target_id=focus-f0deac831da6 code=FOCUS-PROCESS-MISMATCH action=focused_observation
WETYPE PASTE FALLBACK skipped=true reason=target_unverified code=FOCUS-TARGET-NOT-EDITABLE foreground=notepad
```

最后两行就是用户能感知的故障：**说了话，文字没进去**。

### 根因 1（严重）：焦点型目标学完就再也匹配不上

`FocusTargetService` 的模型层**已经**正确地允许焦点型目标（`TryValidateForVerification` 接受 `Text`/`Document`/`Edit`），但**运行时验证层否定了它**，三处不一致：

1. `Matches(...)` 硬要求 `current.ControlType != ControlType.Edit → false`。而焦点型目标存的正是 `Document`（新版记事本的 `RichEditD2DPT`）或 `Text`（Windows Terminal 的 `TermControl`）→ **`descriptorMatches` 恒为 false**。
2. `ObserveFocusedTarget` / `Execute` / `RestoreVerifiedTarget` 都用 `HasWritableEditablePattern(...)` 取「可写」证据，而该函数要求一个**可写的 ValuePattern**；焦点型目标按定义只有 TextPattern（这正是当初设计成「焦点型」的原因）→ **永远拿不到证据**。
3. `ScanWindowCandidates` 找候选时用的条件是 `ControlType.Edit`，焦点型目标**连被检查的机会都没有**。

所以这是一个**结构性死结**：能被学、能被保存、能被显示，然后每次观察都报 `FOCUS-TARGET-STALE` / `FOCUS-TARGET-NOT-EDITABLE`，文字永远不投递。用户的 `focus-targets.json` 里两个目标 `strategy` 都是 `uia_focus`——**两个都中招**。

**修复**（`scripts/features/FocusTargetService.cs`，全部按「策略」分流，可写目标的行为一字未改）：

- 新增 `IsFocusOnlyTarget(target)`、`AcceptsStoredControlType(target, controlType)`（焦点型接受 `Edit`/`Text`/`Document`，与模型层 `TryValidateForVerification` 对齐；可写型仍只接受 `Edit`）、`SatisfiesTargetEvidence(target, element)`（焦点型用 `HasFocusOnlyPattern`，可写型用 `HasWritableEditablePattern`）。
- `Matches` 改用 `AcceptsStoredControlType`；`requireEditablePattern` 分支改用 `SatisfiesTargetEvidence`。
- 观察 / 执行 / 恢复三处验证改用 `SatisfiesTargetEvidence`。
- `ScanWindowCandidates` 对焦点型目标用 `OrCondition(Edit, Text, Document)` 枚举候选。
- **没有削弱** `HasWritablePatternEvidence`：`TextPattern` 仍然**不算**可写证据（这条是既有 self-test 钉死的，保持不变）。

**真机实证**（用用户自己那条已经坏掉的 notepad 记录，未改任何数据）：

```
改前：SMART FOCUS target_id=focus-f0deac831da6 strategy=uia_focus state=error  code=FOCUS-TARGET-STALE
改后：SMART FOCUS target_id=focus-f0deac831da6 strategy=uia_focus state=success code=OK elapsed_ms=304
改后：FAVORITE OPEN process=notepad activated=True focused=True      （改前 focused=False）
改后：VOICE FOCUS LOCK armed=true target_id=foreground-notepad source=focused_observation_transient
改后：VOICE INPUT TARGET ready=true target_id=foreground-notepad code=OK
```

`armed=true` + `ready=true` 就是「文字这次真的会进记事本」。

### 根因 2（严重）：打包应用根本没法冷启动

1. **AUMID 被当成进程名。** `InstalledAppCatalog.CollectStoreApps` 取 AppUserModelID 里 `!` 之前的部分当进程名，于是 Store 记事本在列表里叫 `microsoft.windowsnotepad_8wekyb3d8bbwe`，而真实进程是 `Notepad.exe`。后果连锁：`Process.GetProcessesByName` 永远查不到 → 每次召唤都重复启动；`ActivateProcessWindow` 找不到窗口；启动后学习必然 `FOCUS-PROCESS-MISMATCH`。
2. **`File.Exists` 把 shell 路径判死。** `EnsureFavoriteAppRunning` 要求 `File.Exists(exePath)` 才启动，而打包应用的启动标识是 `shell:AppsFolder\<AUMID>`——**shell 解析路径不是磁盘文件**，于是 `skipped=true reason=no_executable_path`，**每一个冷启动的 Store 应用都不会被启动**。
3. **启动标识被丢掉。** 从选择器添加应用时只用了 `launchTarget` 启动一次，**没有存进 `pendingFavoriteLaunchTarget`**，保存时 `exePath` 退化成 `LookupExecutablePath()`（读当前进程的 `MainModule`）——冷启动场景下必然是空。

**修复**：

- **新增 `scripts/features/PackagedAppIdentity.cs`**：`IsStoreLaunchTarget` / `AumidFromLaunchTarget` / `RunningPackagedProcesses()` / `ResolveProcessName` / `WaitForProcessName`。真进程名用 **`GetApplicationUserModelId`（kernel32）**向操作系统反查——这是唯一公开的「这个进程属于哪个包」的答案，不用读任何用户内容。匹配先按完整 AUMID，再按包名部分回退。
- `CollectStoreApps`：**正在运行**的打包应用直接用反查到的真进程名（因此能和「正在运行」列表去重，不再重复出现两遍）；未运行的才退化成包名键，并且**添加流程会在启动后把真名补上**。
- 目录列出时**同时加载图标**（见根因 3）。
- `BeginFavoriteLearning`：选中 shell 目标时先启动、再 `ResolvePackagedProcessName`（最长 15s）拿到真名；**拿不到就取消学习并明确告知**，而不是把包名键存成一个永远不会被匹配到的应用。同时把 `launchTarget` 存进 `pendingFavoriteLaunchTarget`。
- `EnsureFavoriteAppRunning`：`IsStoreLaunchTarget(path) || File.Exists(path)` 才算可启动；已在运行时若存了 `arguments`，记 `FAVORITE APP AUTOSTART reused=true arguments_applied=false`（见下）。
- `--add-installed=` 自动化钩子走同一条 `ResolvePackagedProcessName`，否则我的验证链本身是假的。

**真机实证**（端到端，用 ChatGPT 这个打包应用做冷启动添加学习，跑完已恢复用户数据）：

```
AUTO ADD INSTALLED target=OpenAI.Codex_2p2nqsd0c76g0!App process=openai packaged=True   ← 推出来的名字是错的（openai）
FAVORITE LEARN packaged=true aumid_chars=30 resolved=chatgpt                            ← 反查到真名
FAVORITE LEARN verify=passed code=unknown
FAVORITE LEARN captured=true pending=true process=chatgpt strategy=uia type=Edit
FAVORITE SAVE process=chatgpt saved=True
```

落盘结果确认 `exePath` 保留了 shell 启动标识：`"exePath":"shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App"`，且 `processName` 是真名 `chatgpt`。

### 根因 3：Store 应用没有图标

`CollectStoreApps` **从来不设 `Icon`**，而 `AppPickerDialog` 会画 `item.Icon`——所以列表里每个 Store 应用都是一块空白，只有开始菜单快捷方式来的应用有图标。

**修复**：`LoadStoreIcon` 用 **`IShellItemImageFactory`**（`SHCreateItemFromParsingName` + `GetImage`，手工声明 COM 接口，`IconOnly | BiggerSizeOk`，32×32），转成 `Icon` 后 `Clone()` 并释放 `HICON` / `HBITMAP`。打包应用没有磁盘 exe，只能向 shell 要图标，这也正是 `ExtractAssociatedIcon` 对它无效的原因。

**实证**：`--list-installed` 的日志里打包条目现在 `icon=True`（例如 `name=ChatGPT target=OpenAI.Codex_2p2nqsd0c76g0!App process=chatgpt icon=True`——一行同时证明图标与真名解析）。

### 根因 4：目录枚举跑了两遍

`CollectStoreApps(...)` 被写在 `foreach (string root in roots)` **循环体内部**（缩进看不出来）。所以整个 AppsFolder 被走两遍，诊断串被追加两次（`items=130 items=0`）。**修复**：移到循环外。现在日志是 `items=130 packagedRunning=24 kept=111`，只有一份。加了负面门禁。

### 根因 5：带参数启动的实例复用（结论是「诚实地记录」，不是修）

链路本身是通的：`.lnk` 的 `Arguments`（`ResolveShortcutArguments`）→ 选择器 `SelectedLaunchArguments` → `pendingFavoriteLaunchArguments` → `favorite.arguments` → `ProcessStartInfo(path, arguments)`，都已接好。真正的限制是 Windows 语义：**如果应用已在运行，`ShellExecute` 只是激活既有实例，存的参数不会再被应用一遍**。这不是 bug，是单实例应用的固有行为，所以**没有去「修」**，而是把它变成可观测的：记 `FAVORITE APP AUTOSTART reused=true arguments_applied=false`，而不是假装启动成功。

### 根因 6：首页的工作流入口在首屏之下（实测）

首页 `workflowEntry` 卡原先在 **y=900..1018**，而默认窗口（1280×840）的内容视口只有 **约 744px** 高（`UiDesignTokens.ContentMinimumHeight = 744`，与 840 减去标题栏和上下 padding 26×2 一致）→ **入口在首屏之下 156px**，也就是「决定文字去哪」这个最重要的动作必须滚动才能看到。

**修复**：入口上移到 hero 正下方（`HomeWorkflowEntryTop = 620`），信息类卡片（status / receipt）下移。入口现在 620..738，**在 744 的首屏之内**。

**实测对比**（UI Automation 取屏幕坐标，窗口 rect = 640,300,1280,840，即 y 范围 300..1140）：

```
y=620（修复后）：配置工作流 rect top = 983   → 在窗口内，可见
y=900（修复前）：配置工作流 rect top = 1263  → 超出窗口下边 1140，不可见
```

**这里有一个必须记下来的教训（我自己先搞错了一次）**：我第一次的验证写的是「`IsOffscreen == False` 就算在首屏之上」。**负面控制直接推翻了这个判据**——我把卡片放回 y=900 后，UI Automation 依然报 `IsOffscreen=False`（一个比窗口下边缘低 123px 的控件仍自称在屏上）。原因是 WinForms 的 `AutoScroll` 面板不会裁剪它向 UIA 汇报的子控件屏幕坐标。**如果我只看 `IsOffscreen`，我会拿到一个假的绿灯并且宣称修好了。**
所以最终判据换成**屏幕坐标与窗口/视口高度的比较**，并且把这条几何关系做成**确定性断言** `RunHomeLayoutSelfTests`（拿 `HomeWorkflowEntryTop + HomeWorkflowEntryHeight` 与 `UiDesignTokens.ContentMinimumHeight` 比），同时加了**该断言自己的负面控制**：把常量改回 900 → self-test `exit=1` 并打出 `The 工作流 entry on the home page is below the fold of the content viewport`。

### 我在这轮踩到的第二个坑：PowerShell 回写源码加了 BOM

做「首页入口」的负面控制时，我用 `(Get-Content -Raw -Encoding UTF8) ... | Set-Content -Encoding UTF8 -NoNewline` 直接把常量从 620 改成 900。**这个 pwsh 的 `Set-Content -Encoding UTF8` 写入了 BOM**（`EF BB BF`），而本仓库所有源码都是 **UTF-8 无 BOM**。
- 发现方式：改完立刻核了首三字节（这一步救了我），随即**从字节级备份 `Copy-Item` 还原**并确认首三字节回到 `75 73 69`、大小与备份一致。
- 正式修改改回**只用 `write` / `edit` 工具**（唯一被认可的源码编辑路径），负面控制也用 `edit` 做。
- 教训：**源码文件的 PowerShell 文本往返是真实风险，不是理论风险**；任何不得已的往返之后必须核 BOM 与字节数。另外 `powershell.exe`（5.1）读**无 BOM** 的 `.ps1` 会按 ANSI 解析，脚本里的中文会变乱码并导致语法错误——给 5.1 用的临时脚本必须带 BOM。

### 已验证 / 未验证

**已验证（全部在本机真机上，且用完已还原用户数据）**：

- notepad 焦点型目标从 `FOCUS-TARGET-STALE` 变为 `success / OK`，`armed=true`、`ready=true`。
- 打包应用冷启动添加学习端到端跑通（launch → 反查真名 → 聚焦 → 捕获 → 验证通过 → 保存），落盘字段正确。
- Store 条目图标加载成功（`icon=True`）。
- AppsFolder 只枚举一次。
- 首页入口屏幕坐标从「窗口外」变为「窗口内」；门禁与负面控制都过。
- `validate.js` 通过；宿主 self-test `exit=0`；Bridge self-test `exit=0`；源码 BOM 已核为无。

**未验证（仍需真机，已留在 `V2_0_HARDWARE_TEST_MATRIX_ZH.md`）**：

- **真正的经典 UWP 应用**（会被 `ApplicationFrameHost` 托管顶层窗口的那一类，如「设置」「计算器」）。本机实测确认这类应用的可见顶层窗口**属于 `ApplicationFrameHost.exe`**（`hwnd=0x53052E class=ApplicationFrameWindow name=设置`，真正的内容是它的子窗口、属于 `SystemSettings.exe`）。这类应用的**输入框**是否存在、能否被学习，本轮**没有找到一个既有文本输入框、又不会破坏用户数据的 UWP 应用来实测**——所以「UWP 应用可学习」这句话我**不声称**。已确认的是：新版记事本、Windows Terminal、ChatGPT 都**不是**被 frame host 托管的那一类（它们的顶层窗口属于自己）。
- Windows Terminal 那条焦点型目标是否也随之恢复（用户当前未运行终端，未实测；机制与 notepad 完全相同）。
- `arguments` 在真实多实例应用上的复用行为。

## 2026-09-11 遥控器按键实测（开机/返回/音量）＋ 一个真缺陷的根因修复

### 用户的问题：能不能把开机键、返回键、音量加减也录进快捷键页

结论：**这四个键在当前 RC003 / Windows 蓝牙组合上绑不上，而且不是本产品的问题。** 这是三条互相独立的证据链得出来的，不是推测。

**证据 1：遥控器向 Windows 只声明了一个 HID 集合。** Raw Input 设备表里 RC003 只有：

```
[KEYBOARD] \\?\HID#{00001812-...}_Dev_VID&012717_PID&32b8_REV&00a4_c05d39c36a3f#...
```

而同一台机器上的罗技 K580（也是 BLE）有 **3 个**集合（键盘 + 消费类控制 + 厂商自定义）。**音量/电源这类键走的正是消费类控制通道，本遥控器没有这个通道。** 另外 Raw Input 全程 **0 条 HID 报告**——遥控器的键只以"已翻译的键盘事件"到达。

**证据 2：逐键实测（每键单独测，避免时间戳混在一起）。**

| 按键 | 实测 |
| --- | --- |
| 上键（对照组） | `vk=0x26 scan=0x48` 正常 |
| 返回键 | 按 3 下，Raw Input **零事件** |
| 音量加 / 音量减 | 零事件，且**系统音量条毫无变化** |
| 开机键 | 零事件 |
| 录音键 | `vk=0xFF scan=0x5E`（`source=raw_input`） |

**证据 3：用桥接自己的日志交叉验证。** 探针在 `12:34:22.267` 抓到 `vk=0xFF scan=0x5E`，桥接同一时刻写着 `Key 录音键 DOWN vk=0xFF scan=0x5E source=raw_input`——**差 2 毫秒**。这既证明探针读数可信，也排除了「以为开机键是 0x5E」的误判。

**私有 GATT 通道也查了（用户批准后）**：只读枚举 9 个服务（跳过被语音组件占用的 ATVV），订阅了 `8a7a0001` 的三条 notify 特征，两轮监听（150s + 180s）。结果**零通知**——而且**对照组（录音键 + 上键）同样零通知**，说明这条私有通道是**命令/响应**通道、根本不转发按键。要引出响应就必须往未公开协议里**写**命令，我一个字都没写（设备侧零改动：结束时需恢复的描述符数量 = 0，三条 CCC 本来就是开的）。HID 服务本身被 Windows 独占（`AccessDenied`），所以"遥控器到底发了什么 HID 报文"这份最直接的证据在用户态谁都读不到。

**而且产品文档早就写了这件事**：`README.md`「开机、返回和独立音量键在当前 RC003 / Windows 蓝牙组合上没有稳定事件，因此 V2.0 仍不提供配置入口」；界面上 `VibeMic.cs` 也有同样的能力说明行；`validate.js` 甚至有一条门禁要求 6 份文档都必须写这句。所以其他模型说的"硬件限制"结论**与项目自己的文档一致**，只是没有人给出证据。最可能的物理原因是开机/音量由遥控器**红外直发电视**、返回走电视/盒子控制通道（这一层我无法证明，电脑没有红外接收器，如实标注）。

### 顺带挖出的真缺陷（本轮修的就是它）

排查过程中机器进入了一个状态：桥接每秒约 30 次记录 `RC003 ISOLATION scoped_suppress=true down vk=0x74`，**全是 DOWN、一个 UP 都没有**，持续数分钟。量化后：

- `RC003 ISOLATION`：当前日志 **13862** 行、轮转文件 **17486** 行——占全部日志约 **五分之四**；2 MB 日志约十分钟被写满并轮转，**把有用的历史挤掉了**。
- `Voice key duplicate DOWN` 只有 255 行——**说明旁边那条重复日志的限流是好的**，洪水来自**没有限流的 isolation 日志**。
- `VOICE STUCK RELEASE` = **0**，`voice_f5_suppressed_edges` 涨到 4950。
- 言灵每 10–14 秒触发一次录音并失败：`ERROR Voice key was detected but RC003 delivered no audio after MIC_OPEN recovery.`，当天 **50 次**。

**根因（两条，都在桥接里，都不在冻结的 Capture 里）**：

1. **卡键看门狗在结构上永远无法触发。** `ReleaseStuckVoiceHoldIfIdle()` 用「距上次活动时间 ≥ `RC003_VOICE_STUCK_RELEASE_MS`(900ms)」判断，而 `lastVoiceActivityUtc` 被**每一个重复边沿**刷新（约每 31ms 一次）→ 该条件永远不成立。**它恰好在自己被写出来要处理的那种情况下失效**：松开边沿丢失的键，正是"活动一直在继续"的键。
2. **后果被冻结的 Capture 放大成自持循环。** 持有状态没被释放 → 命名事件 `Local\VibeMicVoiceKeyHeld` 一直是 set → 冻结的 Capture 在**每次 ATVV 重连**时都会 `RecoverHeldVoiceRequestAtReady()` 重新起一个会话 → 拿不到音频失败 → ATVV 断开重连 → 再次重起。日志实证：`STARTUP VOICE HOLD recovered_at_atvv_ready=true` **52 次**，间隔正好 10–14 秒，与观测的自持循环完全吻合。
3. **isolation 日志每边沿写一行**，没有任何限流（`HandleVoicePhysicalTransition` 里那条反而有 2 秒限流）。

**修复（只动桥接；Capture 二进制保持冻结）**：

- **看门狗增加"持有的年龄"判据**：新增 `voiceHoldStartedTicks`（本次按住的起点）与 `VOICE_HOLD_STUCK_BOUND_MS = 90000`。阈值**故意远在遥控器自己的会话边界（约 60 秒）之后**，所以它只可能在音频早就结束之后触发，**不可能截断一次真实录音**。触发时记 `VOICE STUCK RELEASE reason=held_past_device_bound held_ms=… bound_ms=…`。
- **释放后加闩锁**：`voiceHoldStaleLatched` 置位后，仍在涌入的重复 DOWN 边沿**不再被当成新按下**（这是循环的燃料）；只有"松开边沿终于到达"或"安静 ≥ `VOICE_HOLD_LATCH_CLEAR_MS`(1500ms)"才解除闩锁，记 `VOICE STUCK LATCH CLEARED reason=release_edge_seen|key_quiet`。
- **日志聚合**：新增 `LogIsolationEdge`，按住的开始、每 2 秒一条重复计数、结束时一条 `hold_summary repeats=N`。**实测 50 次重复只产生 1 行**（原来 50 行），诊断价值不变。
- **自检暴露**：桥接 health 新增 `voice_hold_repeats_suppressed` / `voice_hold_stale_releases` / `voice_hold_stale_latched`；自检页「RC003 配对与连接」在闩锁时把状态降为 warning，并给出可执行恢复步骤（按一下录音键确认松开；仍如此就重启遥控器）。

**没有碰的东西**（用户明确要求）：录音键的判定逻辑（何时算按下、何时算松开）、`VOICE_RESTART_GUARD_MS`、任何音频参数、以及冻结的 `VibeMicAtvvCapture.exe`（哈希仍为 `B62DE035…E683`）。

### 门禁抓回我一个真 bug（值得记）

我在"安静解除闩锁"分支里顺手写了 `lastVoiceReleaseUtc = DateTime.UtcNow`。新加的 self-test 断言"卡键释放后下一次真实按下必须被接受"直接红了——因为那会把**早已发生的**松开当成"刚刚松开"，让 500ms 重启守卫误杀用户的**下一次**按下。修法是**不写**这个时间戳（松开发生在 ≥1500ms 前，不是现在），并把 `VOICE_HOLD_LATCH_CLEAR_MS > VOICE_RESTART_GUARD_MS` 这条**不变式本身**也写进 self-test 断言，而不是假设它成立。

### 验证

- 桥接 self-test exit 0（含新断言：活动仍在涌入时卡键必须被释放、闩锁期间重复不得重新武装、安静后闩锁必须解除、释放后真实按下必须成功、50 次重复必须被聚合、闩锁与重启守卫的不变式）。
- 宿主 self-test exit 0；`validate.js` 通过；`Test-V2FeatureSuite.ps1` 全部通过。
- **新门禁逐条验证过非空转**：用 node 拿**旧代码形态**回放——看门狗门禁在旧代码上判 false（能抓住回归）、isolation 负面门禁的模式匹配旧写法而不匹配新写法、health 字段与自检文案均存在、不变式确有断言。
- 日志聚合效果实测：50 次重复 → 1 行。
- 真机副作用：设备侧零改动（GATT 描述符恢复数 0）；用户数据未改；临时探针全部删除。

## 2026-09-11 反馈浮层收口：右下角 HUD 与窗口内卡片不再重复与互相遮挡

用户问「右下角那个 dock 栏是不是和我们的弹窗会冲突，能不能整合成同一个东西」。查清后发现是**两个独立问题**，而且都不是主观感受：

**这两个浮层是什么**

| 浮层 | 位置 | 尺寸 | 性质 |
| --- | --- | --- | --- |
| `LiveHudForm`（用户说的"dock 栏"） | 屏幕右下角，距工作区右/下各 18px | 400×160 | `TopMost` + `WS_EX_NOACTIVATE`（永不抢焦点）、半透明、深色玻璃 |
| 窗口内 toast（`BuildToastOverlay`） | 主窗口内部右下角，距窗口右/下各 24px | 420×58 | 浅色卡片 |
| `ContextDeckForm` | **居中**（`VibeWindowLayout.FitToWorkingArea` 是居中逻辑） | 820×696 | 不参与这个冲突 |

**问题 1：一条消息弹两次（代码写死的）**

`ShowActionToast(result, text, kind, mirrorToHero, durationMs)` 原来是这样：

```csharp
latestActionResult = ...;
PublishFeedbackSnapshot();   // → PresentLiveHud(...) → 弹 Live HUD
...
toastPanel.Visible = true;   // → 同时弹窗口内卡片
```

**同一个 `ActionResult` 同时喂给两个浮层**，同样的文字、同样的时长，但两套视觉语言（浅色卡片 + 图标 vs 深色玻璃 + 麦克风芯片 + 呼吸光环）。

**问题 2：贴在一起时置顶浮层盖住卡片（几何可算）**

卡片在窗口内侧右下角 420×58 内缩 24px；HUD 在屏幕右下角 400×160 内缩 18px 且是 `HWND_TOPMOST`。**窗口最大化或位于右下角时，卡片矩形几乎完全落在 HUD 矩形内，于是被盖掉。**

**为什么不直接"合成一个"**：两个浮层存在的理由不同——HUD 的**全部意义**是"你没在看言灵窗口时也能看到状态"（非激活 + 置顶正是为此），删掉它就丢了这个能力；窗口内卡片的理由是"你正在言灵里操作"时的就地反馈，此时弹屏幕级窗口反而是噪音。所以正确做法是**按"看不看得见窗口"分工**，让同一条消息任何时刻只有一个出口。

**已完成**

- **集中出口判定**（用户指定在 `ShowActionToast` 里做）：新增纯函数 `ShouldPresentLiveHud(explicitlyRequested, windowInFront)` / `ShouldPresentInlineToast(...)` / `WindowCountsAsInFront(visible, minimized, isForeground)`，加 `IsMainWindowForegroundAndVisible()`（复用已有的 `GetForegroundWindow` 与既有的 `Visible && WindowState != Minimized` 惯例）。真值表：

  | 显式请求 HUD | 窗口在前台 | Live HUD | 窗口内卡片 |
  | --- | --- | --- | --- |
  | 否 | 是 | 不弹 | 弹 |
  | 否 | 否 | 弹 | 不弹 |
  | 是 | 是/否 | 弹 | 弹 |

  最后一行是有意的：用户从托盘主动点「显示 Live HUD」时，那是**追加**而不是替换，不能被动作卡片顶掉。为此新增 `liveHudExplicitlyRequested`，由 `ShowLiveHud()` 置位、`OnLiveHudDismissed` 复位。
- `PublishFeedbackSnapshot` 拆出 `PublishFeedbackSnapshotInternal(bool presentHud)`，让卡片作为唯一出口时可以**只刷新 HUD 内容而不显示它**，同时不影响那些纯状态变化（没有卡片可弹）继续由 HUD 呈现。
- **共用设计 token**：`UiDesignTokens` 现在统管状态色（`StatusAccent`）、图标（`StatusGlyph` / `InlineStatusGlyph`）、字体族与字号档、圆角档（`FeedbackRadiusCompact` / `FeedbackRadiusPanel`）、时长（`DurationForState` / `FeedbackInfoDurationMs` / `FeedbackProblemDurationMs`），两个浮层都从那里取。
  - **实测确认了一件好事**：两处的颜色**本来就是逐字节相同的**（`green 10,164,104` / `amber 229,151,39` / `coral 204,70,82` / `cyan 0,153,190` / `violet 104,82,244`），所以这次统一是**纯去重、零视觉变化**——只有"已取消"的时长真的不一致（HUD 12s、卡片 2.8s），统一为 12s。
- **文档**：用户指南「五个页面」→「六个页面」（导航一直是 6 项，标题一直写错），并补上单出口反馈规则；UI 技能说明也同步修正为六页并把这条约束写进 hard constraints。

**没碰**：录音/蓝牙/按键钩子/隐私链路；`LiveHudForm` 的非激活置顶契约、`ContextDeck` 的录音期拦截、`--ui-resource-test` 里既有的 HUD 呈现/抑制断言全部保持通过。

**验证**：宿主 self-test exit 0（含新的真值表断言）、`--ui-resource-test` exit 0（既有 Live HUD 断言仍过）、`validate.js` 通过、`Test-V2FeatureSuite.ps1` 全过；**新门禁逐条用旧代码形态回放验证过非空转**（HUD 私有调色板门禁、卡片私有调色板门禁、出口判定门禁在旧代码上均判 false）。

### 追加：查这个"dock 栏"时发现的第三个缺陷——浮层会把自己永久钉在屏幕上

**现象**：排查时我用 UIA 枚举该进程的顶层窗口，发现那个 400×160 的浮层**一直可见**，20 秒后仍在、此前已经挂了**好几分钟**：

```
hwnd=0xA20B1A visible=True rect=2142,1262,400,160 class=WindowsForms10.Window...
```

顺带修正一件事：我最初把 Case A（窗口在前台）判为"失败"，其实**是我的探针没真正抢到前台**（`SetForegroundWindow` 从无前台的后台进程调用会被拒绝），所以那一次不是逻辑错，是测法错。真正的问题是下面这个。

**读浮层自己的文字定死了根因**：

```
[正在检查] [正在检查蓝牙和遥控器语音通道] [真实音频峰值 · 等待一次真实听写] [目标 · notepad 输入框 · 已验证 ｜ Profile · 浏览器 AI]
```

- `ActionResult.FromLegacyFeedback`（`ActionResult.cs:183`）**把任何以「正在」开头的文案判成 `ActionState.Checking`**；
- `LiveHudDurationMilliseconds` 对 `Running`/`Checking` **返回 0 = 永不自动隐藏**。

于是**一句普通的信息提示就能把一个置顶浮层永久留在右下角**。这才是用户会把它叫作"dock 栏"而不是"弹窗"的真正原因——它根本不是一闪而过的弹窗。

**修复（关键：不削弱既有契约）**

我第一版把 `Running`/`Checking` 的 `0` 直接改成有界时长，结果 **`BUILD_RELEASE.ps1` 里的 `LiveHudUiTests.exe` 红了**：

```
Require(DurationFor(ActionState.Checking) == 0 && DurationFor(ActionState.Running) == 0,
        "Live HUD auto-hides an operation that is still running");
```

这条断言**本身是合理的**（真正在进行的操作不该自动消失），所以正确做法不是改测试，而是**把两条规则分开**：

- `LiveHudDurationMilliseconds`（**状态**路径）：原样不动 —— 进行中的操作与真实音频仍然不自动隐藏；`LiveHudUiTests` 原封不动通过。
- `FeedbackMessageLifetimeMilliseconds`（**消息**路径，新增）：面板携带的是**一条转瞬即逝的操作消息**时，时长一律有界（进行中/检查中给 12 秒，其余按 token）——因为消息不是"活着的状态"。由 `PresentLiveHud(snapshot, force, carryMessage)` 的 `carryMessage` 贯通，只从 `ShowActionToast` 传 `true`。

**真机前后对照**（安装版，用 UIA 打 `检查连接` 触发 `正在检查蓝牙和遥控器语音通道`）：

```
baseline:            hud present = False          （重启后没有残留）
t=0.9s  hud=True   [正在检查] [正在检查蓝牙和遥控器语音通道] ...
t=11.7s hud=True
t=12.6s hud=False   ← 有界时长到时自动消失
final:               hud present = False
```

修复前同一句提示会让它**永久停留**（实测数分钟、20 秒观察仍在）。现在 12 秒自清。

**门禁**：新增断言要求消息路径有界、且**状态路径的 `0` 仍然存在**（正反两面都钉住）；用旧代码回放确认该门禁能抓住回归。`BUILD_RELEASE.ps1` 完整通过（exit 0）。

## 2026-09-11 快捷键全量验证 + 四套 Profile 方案落地

用户要求：验证所有快捷键是否可靠稳定可用；并配置一套方案（浏览器 / Web coding / 通用，外加已有的 terminal）。

### 一、验证：新增 `scripts/tests/ShortcutActionTests.cs`（已并入 V2 套件）

**为什么必须自动化**：真正的失败模式是**静默**的。`NormalizeShortcutProfileMappings` 会把任何 `IsSupportedMappingAction` 拒绝的动作**替换成该键的默认值**——也就是说，一个看起来可选、实际不支持的动作，保存后会悄悄变成另一个动作，界面上没有任何提示。逐个人肉点选根本发现不了。

审计覆盖：动作选择器 **30** 项、逐键选择器 **216** 项（12 键 × 18）、去重后 **40** 个不同动作。对每一个断言：

1. `IsSupportedMappingAction` 为真（`:prompt` 对话框触发器与 `snippet:manage` 除外，它们本就不可持久化，且已断言**不能**被持久化）；
2. 文案 `CustomActionText` 非空；
3. 不会捕获录音键（`f5` / `shortcut:f5` / `voice`）；
4. 同一次选择器里没有两个不同名字指向同一动作；
5. **保存往返**：把动作写进每个键后归一化，必须**原样存下**（唯一允许的改写是 `左键 + alt+left → browserback` 这条历史迁移，并且额外断言"改写总数 ≤ 1"，防止它变成习惯）。

另外断言四套预置 Profile：顺序为 `general / vibe-coding / browser-ai / terminal-agent`、名称非空、`preset` 与 id 一致、**每个键都有值且受支持**、归一化后不变、`general` 不绑定任何应用而其余三套必须绑定、**任何进程只能属于一套**。

桥接侧补上对称的一半（`--self-test`）：**27** 个可存快捷键必须解析为可注入的键且**恰好一个非修饰键**；`launch-client:*` 七个 id 必须被识别；`task-switcher` / `none` / `passthrough` 必须**不**解析成按键组合。

**结论：动作集本身没有发现缺陷。** 这个测试的价值在于把"已验证"变成可重复的，而不是一次性的结论。

### 二、发现的真实问题不在动作集，而在用户的 Profile 配置

读用户真实数据发现三件事：

1. **`vibe-coding`（Vibe Coding）整套缺失**——用户只有 general / browser-ai / terminal-agent 三套。
2. **`browser-ai` 与 `general` 的 `processNames` 是空的**——而 `smartProfilesEnabled=true`。也就是说 **Smart Profiles 永远不可能切换到浏览器或通用**，只有 terminal 一套能被自动选中。
3. **`smartProfileFallbackId` 指向 `browser-ai`**（回退到"浏览器"不合常理，应为 general）。

代码里的 `DefaultShortcutProfiles()` 本来就是这四套（含 Vibe Coding 与各套的 processNames），所以缺的是用户配置里的落地。

### 三、落地的方案（已写入用户配置，先备份）

设计约束（先说清楚，因为它决定了方案形状）：**可随 Profile 变化的只有每个键的「短按」，加上 Home 与功能键的「长按」**；方向/确认/TV 的**长按**与所有键的**双击**存在全局手势层里，**不随 Profile 变化**。所以方案是：短按做该 Profile 的主职，全局层当共用的"加强层"。

| 键 | 通用导航 | Vibe Coding | 浏览器 AI | Terminal Agent |
| --- | --- | --- | --- | --- |
| 上 / 下 短按 | up / down | up / down | up / down | up / down |
| 左 / 右 短按 | left / right | **ctrl+c / ctrl+v** | **shift+tab / tab** | left / right |
| 确认 短按 | enter | enter | enter | enter |
| 功能键 短按 | ctrl+c | **ctrl+z** | **ctrl+f** | ctrl+c（中断） |
| 功能键 长按 | ctrl+v | **ctrl+s** | **shortcut:ctrl+l** | ctrl+v |
| Home 短按 | win+d | win+d | win+d | win+d |
| Home 长按 | win+shift+s | **launch-client:cursor** | **launch-client:chatgpt** | **launch-client:codex** |
| TV 短按 | task-switcher | task-switcher | task-switcher | task-switcher |
| 绑定应用 | 无（回退） | cursor, code, windsurf, codex | chrome, msedge, firefox, brave | windowsterminal, powershell, pwsh, cmd, wezterm-gui |

设计理由：**上/下 一律是真实方向键**（最安全，而且长按已经是 pageup/pagedown，天然形成两层）；**确认键一律 enter**，让"按确认发送"的肌肉记忆在任何 Profile 下都不变；**Home 短按一律 win+d**（通用逃生），**Home 长按 = 该 Profile 的主角应用**；全局层保持用户原有的剪切/全选/撤销/保存、静音/播放、音量加减、打开 Cursor 与 ChatGPT 不动。

同时把 `smartProfileFallbackId` 改为 `general`。

**真机验证**：重启后桥接配置显示 `profiles=4`、`fallback=general`，每套的 `shortNext`/`longShortcut`/`doubleShortcut` 全部正确生成；前台是 `chrome` 时健康状态变为 `match_state=matched`、`effective=browser-ai '浏览器 AI'`——**这在改之前是不可能发生的**（浏览器那套没有绑定任何应用）。非相关设置（语音工具 wechat、voiceMode hold、gain、主题、向导完成状态）逐项核对未被改动；配置文件 **UTF-8 BOM 原样保留**（该文件带 BOM，与源码不同，回写必须显式指定）；改动前已备份到 `%TEMP%\vibe-config-backup\`。

## 2026-09-11 快捷键逐个真机验证（用户配合实体遥控器）

用户要求"逐个验证所有快捷键"。做法：**桥接会为每一次派发写一行 `Gesture action executed label=… phase=短按|长按|双击 action=… success=…`**，所以由用户按实体键、我读日志**机械化比对**配置表，用户不需要描述任何现象。

### 验证方法上的两个改进

1. **Profile 由我锁定，用户不必切应用**：把 `smartProfilesEnabled` 临时关掉、`activeShortcutProfileId` 固定成要测的那套，用户在任何窗口按都行。比"请你切到 Chrome 再按"省事得多（用户明确反馈过"每次按键太多、操作不过来"）。
2. **一次性收集器取代常驻 watcher**：常驻 watcher 被日志轮转骗过（marker 是旧文件的字节偏移，轮转后失效），改为按需从 marker 读增量；并且**关键数据一律回读原始日志**核对时间戳，不依赖收集器的计数。

### 结果：41 项真机通过

| 层 | 覆盖 |
| --- | --- |
| 全局长按 | `pageup`(上) / `pagedown`(下) / `browserback`(左) / `ctrl+shift+z`(右) / `volumemute`(确认) |
| 全局双击 | `ctrl+x`(上) / `ctrl+a`(下) / `ctrl+z`(左) / `ctrl+s`(右) / `mediaplaypause`(确认) / `volumeup`(功能) / `volumedown`(TV) |
| 通用短按 | `up` / `down` / `left` / `right` / `enter` / `ctrl+c` / `win+d`(Home) / `task-switcher`(TV) |
| 通用专有 | `ctrl+v`(功能长) / `win+shift+s`(Home长) / `launch-client:chatgpt`(TV长) |
| 浏览器短按 | `shift+tab`(左) / `tab`(右) / `ctrl+f`(功能短) |
| 浏览器专有 | `shortcut:ctrl+l`(功能长) |
| Vibe Coding 短按 | `up` / `down` / `ctrl+c`(左) / `ctrl+v`(右) / `enter` / `ctrl+f`(功能短) |
| Vibe Coding 专有 | `tab`(功能长) |
| Terminal Agent 短按 | `up`(上) / `ctrl+c`(功能短) / `task-switcher`(TV短) |
| Terminal Agent 专有 | `ctrl+v`(功能长) / `launch-client:chatgpt`(TV长) |

**Profile 隔离已单独验证**：把 `smartProfilesEnabled` 关掉、`activeShortcutProfileId` 钉成 `terminal-agent` 后（`state=manual`，`effective=terminal-agent`），用户按 4 下（上键短按 / 功能键短按 / 功能键按住约 0.8 s / TV 键），日志逐条落 `profile=terminal-agent`：

```
15:33:37.074 Key 上键 DOWN gesture=layered threshold_ms=650
15:33:37.304 Key 上键 UP hold_ms=235 gesture=短按
15:33:37.421 Gesture action executed label=上键 phase=短按 action=up success=True
15:33:37.423 Action receipt sequence=1 button=上键 trigger=短按 action=up profile=terminal-agent success=True
15:33:46.469 Key 功能键 UP hold_ms=156 gesture=短按
15:33:46.588 Gesture action executed label=功能键 phase=短按 action=ctrl+c success=True
15:34:02.288 Gesture action executed label=功能键 phase=长按 action=ctrl+v success=True
15:34:16.667 Key TV 键 UP hold_ms=0 gesture=long
15:34:17.009 Client launcher started target=chatgpt
15:34:17.009 Gesture action executed label=TV 键 phase=长按 action=launch-client:chatgpt success=True
15:34:21.451 Gesture action executed label=TV 键 phase=短按 action=task-switcher success=True
```

（TV 键那次用户按住约 0.885 s，先触发了 TV 长按的 `launch-client:chatgpt`，随后两次短按各触发一次 `task-switcher`——三种层都拿到了真机证据。）验证完已把 `smartProfilesEnabled=true`、`activeShortcutProfileId=general` 恢复原状并重启核对（`enabled=True configured=general effective=browser-ai state=matched`）。

**仍未真机验证（不影响交付，如实记录）**：Terminal Agent 的 下/左/右 短按（`down`/`left`/`right`）、确认短按（`enter`）、Home 短按（`win+d`）——这几个动作本身在"通用"和"Vibe Coding"里已经验过，且 Profile 隔离已单独证明，所以没有重复按。

**未通过 / 部分通过（2 项，都是"启动某个 AI 客户端"这类动作）—— 均已定位，其中 1 项已修**：

- `Home 双击 → launch-client:cursor`：**手势层已验证正确**（`Key Home 键 UP hold_ms=141 gesture=双击`，成功识别并派发），失败的是动作层（`Client launcher unavailable target=cursor`）。**已修复并复验通过**，详见下文"2）"。修复后冷启动两次连续通过：`15:28:05.141` / `15:30:45.520` `Client launcher started target=cursor`，`Cursor` 进程随之起来，`Smart Profile switched profile=vibe-coding state=matched foreground=cursor`。
- `Terminal Agent Home 长按 → launch-client:codex`：本机**目标不存在**（`Get-StartApps` 无 "Codex" 条目，两个开始菜单也无 `Codex.lnk`），三条启动路径全部落空，报 `Client launcher unavailable target=codex`。这不是启动逻辑的缺陷，而是**本机没有 Codex**；在装有 Codex 的机器上该槽位可用。用户已知悉并选择保留该绑定。

### 真机暴露的三个真问题

**1）双击阈值不适合实体遥控器（已修）**

`DoubleTapWindowMs` 原本硬编码 320 ms。实测三个数据点：276 ms 判成双击 ✔、378 ms 被判成两次短按 ✘；而按我"再快一点"的提示去按，出现**只有 UP、没有 DOWN** 的日志（`RC003 RAW KEY UP vk=0x28 … action_routed=False` 连续两条），即**按得太短，遥控器根本没上报按下**，双击无从形成。也就是"慢了不算、快了丢失"的双向夹逼。

修复：窗口改为读取用户自己的 Windows 双击速度 `GetDoubleClickTime()`，下限 500 ms、上限 900 ms（本机系统值就是 500，所以默认行为等于平台默认）。**真机前后对照**：改后自然速度下 `下键/左键/右键` 双击全部通过，左键实测松手→松手间隔 **331 ms**——这个值在旧窗口下必然失败。

顺带修掉手势卡片上两处**过时文案**：写死的"0.32 秒"（改为读取实际值），以及"双击可以挂一个宏"这一行——**宏已经被移除，这行文案却留了下来**。

**2）AI 客户端的启动解析很脆（已修）**

`LaunchAiTarget` 原本只有两条路：`Get-StartApps` 里按**显示名精确匹配**（`$names -contains $_.Name`），失败后再 `Process.Start("X.exe")` 靠 `App Paths`/PATH 解析。本机实测：

- `chatgpt` ✔ 启动成功、也能聚焦已有窗口；
- `cursor` ✘ 冷启动失败（`.lnk` 指向 `D:\cursor\Cursor.exe`，`App Paths` **未注册**；我用桥接原样脚本手动执行**能成功**，耗时 **4766 ms**，而桥接那次只用了 407 ms 就返回失败——说明它走的不是超时而是更早的分支）；
- `codex` ✘ 完全不可用：本机 `Get-StartApps` 里**没有名为 "Codex" 的条目**（ChatGPT 桌面端的 AppID 是 `OpenAI.Codex_2p2nqsd0c76g0!App`，显示名却是 "ChatGPT"）。

修复分三处：

1. **补上第三条路**：`Get-StartApps` → **开始菜单快捷方式解析出目标路径** → 裸可执行名，按顺序回退。`.lnk` 用 `WScript.Shell` 的 `CreateShortcut().TargetPath` 解析（和资源管理器同一套），不引入任何 COM 互操作程序集；搜索带上限深度遍历**用户和公共两个开始菜单**，按快捷方式文件名精确匹配（或 `名字 + 空格` 前缀，例如 `VibeFlowProbe Beta.lnk`）。
2. **放宽 Win32 启动探测的预算**：PowerShell 子进程 `WaitForExit` 从 6000 ms 提到 12000 ms。本机实测这个子进程冷启动耗时 4766 ms，旧预算就在悬崖边上。
3. **给搜索这一半补上可测的接缝**：搜索函数接受"根目录"参数（产品调用传真实开始菜单），自检自己造一个临时目录、在里面造 `.lnk` 并在**子目录**里再造一个同名带后缀的、外加一个只前缀相同但无空格的诱饵，断言"恰好命中两个、诱饵不命中"。在此之前，**回退路径的两半没有任何测试执行过**。

**真机复验**：修复前 `15:08:06.162 Client launcher unavailable target=cursor`；修复后**两次连续冷启动成功**（`15:28:05.141`、`15:30:45.520`，各约 530 ms），`Cursor` 起来后 Smart Profile 正确切到 `vibe-coding`。补充确认：`Cursor.exe` 在本机**既不在 PATH 也没有 App Paths 条目**，所以"裸可执行名"这条单独存在时**永远不可能**启动 Cursor——它只有 AppID 和 `.lnk` 两条可走的路。

**诚实说明（未复现的部分）**：修复后那次成功走的是**第一条路**（日志里没有 `start_menu_shortcut` 行，且 530 ms 远小于旧的 6 s 预算），所以修复前那次"407 ms 快速失败"的**具体成因没有被机械复现**（旧二进制已被安装覆盖）。因此这条修复的立场是：**启动解析不再是单点策略，且冷启动已端到端复验**；新增的那条路，两半都由桥接自检**确定性执行**。

**3）`win+d` 与双击同键冲突（未修，待定）**

`Home 短按 = win+d`（显示桌面）+ `Home 双击 = 启动应用` 的组合很难用：双击一旦失败就变成两次显示桌面，而第一下把桌面显示出来之后，用户自然会隔一两秒再按第二下（实测两次相隔 **2235 ms**），于是**永远凑不成双击**。这是用户原有的全局设置。可选的修法：Home 短按换成无害动作（如 `escape`），或去掉 Home 的双击层。

## 2026-09-11 补齐缺口：出厂手势、USB 还原、前台输入布局、以及一个真缺陷

用户要求「把缺失的部分补上，除非是之前明确说不用做的」。因此本轮做的是**实现**，不是验证：四条已实现并逐条给出证据，另外顺手抓到一个会让「用语片段」整个功能失效的真缺陷。

### 缺陷（最重要，先修）：切一次 Profile 就会把用语片段表清空

**现象**：把一条片段绑到动作上（`snippet:<id>`）后，真机派发被拒：`Snippet action rejected label=… reason=unknown_snippet`，宿主侧 `voxdeck-shortcuts.json` 里明明有 `snippets: [...]`，`revision` 也和桥接加载的一致。**可复现，两次都失败**。

**根因**：`ActivateSmartProfile()` 用**手写字段拷贝**构造切换后的配置：

```csharp
var next = new BridgeConfig { version = …, revision = …, notes = …, inputRoutingMode = …,
    activeShortcutProfileId = …, activeShortcutProfileName = …, smartProfilesEnabled = …,
    smartProfileLocked = …, fallbackShortcutProfileId = …, profiles = source.profiles,
    mappings = target.mappings };   // ← snippets 没有拷贝
```

Smart Profiles 默认开启，所以**开机第一次前台匹配就会切换 Profile**（日志：`Smart Profile switched profile=browser-ai state=matched foreground=chrome`），切换后 `config.snippets` 变成 `null`，于是任何片段动作都被判成未知 id。**这不是边缘场景，而是默认路径**：装了 V2 且开着智能切换的用户，片段功能 100% 不可用。

**为什么测试没抓到**：桥接 self-test 的片段夹具是**内存里直接赋值** `config = snippetFixtureConfig`，从不经过 Profile 投影；而 Profile 切换的 self-test 只检查 mappings。两条路径各自都测了，中间那一步没有。

**修复**：把投影提成有名字的纯函数 `ProjectActiveProfile(source, target)`，`snippets` 连同其余字段一起搬运；self-test **逐字段**核对投影结果（version / revision / notes / routing / smart 开关 / fallback / profiles / mappings / snippets / 两个 active 字段），所以以后加配置字段忘了搬运会直接红。另加一条 JSON 往返断言：片段表是**以 JSON 形式**随映射文档到达桥接的，之前的夹具全部绕过 JSON，这一条把「文档形状读不回来」也钉住。

**真机复验（端到端，注入真的发生了）**：用一个自建的一次性探针程序当靶子（它在**确实拿到前台焦点之后**才写测试请求，避免把测试文本打进别的窗口），桥接把片段逐字符输入：

```
owns_foreground=1 foreground_process=Probe box_focused=1 dispatched=1
chars=16 text=[VIBEFLOW-7391-中文]
bridge={"action":"snippet:snip-probe7391","success":true,...}
```

16 个字符（含两个中文字）**逐字落到焦点控件**，而桥接日志只写 `Snippet action label=snippet probe characters=16 typed=True`——**正文从未进日志**，隐私边界成立。测试用的片段与探针程序已全部删除，`snippets.json` 回到「原本不存在」的状态。

### 1）出厂不再是一张空白手势表（最大的「没做」）

**问题**：`GestureBindingStore.Load()` 在没有 `gesture-layers.json` 时返回空表，代码里**没有任何 seed 路径**。新装用户打开快捷键页，8 个键的「长按 / 双击」全是「未配置」——三层手势这个卖点**开箱等于不存在**，必须自己一格一格配。

**实现**：新增 `GestureBindingStore.DefaultDocument()`（纯函数，7 个键、13 个动作），宿主在启动时 `EnsureGestureLayerDefaults()` 写入，**唯一条件是文件不存在**：

| 键 | 长按 | 双击 |
| --- | --- | --- |
| 上 | `pageup` | `ctrl+x`（剪切） |
| 下 | `pagedown` | `ctrl+a`（全选） |
| 左 | `browserback` | `ctrl+z`（撤销） |
| 右 | `ctrl+shift+z` | `ctrl+s`（保存） |
| 确认 | `volumemute` | `mediaplaypause` |
| 功能 | —（长按归 Profile 映射表） | `volumeup` |
| TV | `launch-client:chatgpt` | `volumedown` |

**Home 刻意不写**：它的短按是「显示桌面」，而双击的第一下会执行短按，桌面先弹出来之后第二下就凑不成双击（真机实测第二下晚了 2235 ms）。写进去等于出厂就带一个用不出来的绑定。

**边界**：只写一次。用户改过 → 文件存在 → 不再触碰；用户清空全部层 → 文件仍在（空表）→ 不会「复活」；`snippets.json`/`gesture-layers.json` 这类用户数据一律先备份再动（本轮备份在 `%TEMP%\vibe-config-backup\`）。

**真机验证**（真实走了「没有表」这条路）：把用户自己的表挪走 → 启动 → 生成的 `gesture-layers.json` 恰好 7 条、Home 缺席、宿主日志 `GESTURE DEFAULTS seeded=true keys=7 file=gesture-layers.json reason=absent`；桥接文档里 `up/down/left/right/ok/tv` 都带上了 `doubleShortcut`，`mode` 变 `shortlong`——**推荐表真的走到了派发侧**，不是只写了个文件。随后把用户自己的 8 条表原样恢复。

**踩到并修掉的坑**：第一次把 `EnsureGestureLayerDefaults()` 放在构造函数靠前的位置，那时 `hostLogPath` 还是空串 → `File.AppendAllText("")` 抛异常且被 `catch {}` 吞掉，**文件写成了、日志没写**。已把调用移到 `hostLogPath` 赋值之后，并把「调用必须在 hostLogPath 之后」写成门禁（断言源码里两者的 `indexOf` 顺序），因为这个日志是一台机器收到推荐的唯一记录。

### 2）USB 选择性挂起：补上「还原」入口（动作早就有，UI 从来没有发过它）

`restore-usb-suspend` 的分支一直在 `HandleSelfCheckAction` 里，但**没有任何界面会产生这个动作**——`validate.js` 里也只有 `repair-usb-suspend`。现在自检「Windows 蓝牙」项在已禁用时给出「还原 USB 选择性挂起」。

**并且是诚实的还原**：新增 `UsbSuspendWasAppliedByApp()`，读脚本自己写的 `%LOCALAPPDATA%\Vibe Flow Remote\usb-suspend\state.json`，只有 `state=applied` 且 `ac_after=0`（**本次禁用是本应用应用的**）才显示还原入口。理由是：AC 值等于 0 也可能是公司镜像或用户自己调过，那种情况下劝他「还原」等于悄悄改他的电源策略。

### 3）前台窗口线程的输入布局（原来明确写着「未实现」）

原文档写着：「判断**前台应用**的输入法需要按目标窗口线程查询（例如 `AttachThreadInput` + `GetKeyboardLayout`，或对前台窗口所在线程做 TSF 查询），本轮未做」。

现在做了前一半，并且**如实说明只做得了前一半**：新增 `InputMethodDetector.ReadForegroundLayout(IntPtr window)`——附加到前台窗口线程的输入队列（用完解附），读该线程的键盘布局，取低 16 位当语言 id，`DescribeLanguage()` 用 `CultureInfo` 渲染成 `zh-CN` 这类标签（运行时不认识的 id 退回十六进制）。日志里和原来那条并列：

```
INPUT ENGINE ACTIVE engine=… scope=thread … foreground_thread=… foreground_language=zh-CN same_layout=True
```

**另一半（跨进程读 TSF 活跃配置文件）在用户态做不到**：TSF 的活跃 profile 是按线程的，读不到别的进程里那个线程的 profile。所以这里报告的是**布局**（语言 + IME 设备句柄），文案里从不声称「前台应用的输入法」。它的诊断价值在于对比：`same_layout=False` 时，「面板没出来」就有了一个可查的解释。`ReadForegroundLayout` 永不抛异常、永不返回 null——它挂在语音路径的日志上，不能因为一个诊断把链路带崩。

### 4）出厂默认 Profile 与用户实测方案的关系（未改，如实记录）

出厂 `StarterProfileMappings()` 与用户机器上那套（已逐槽真机验证过的那套）**不是同一套**，有 10 个槽位不同。本轮**没有**把出厂默认改成用户那套，理由：用户那套是围绕他自己已有的全局手势层设计的（方向/确认/TV 的长按双击被他占了 `pageup`/`ctrl+z`/`browserback` 等），新用户没有那些全局层，照搬反而浪费位置；而且出厂那套是**有意的**（浏览器左键用专用 `browserback`，比用户那套的 `shift+tab` 更「浏览器」），并且有一处被门禁钉住。要改就要连门禁一起改，且属于产品决策，所以留给用户定。

### 本轮改动文件

`scripts/features/GestureBindingStore.cs`（新增 `DefaultDocument()`）、`scripts/features/InputMethodDetector.cs`（新增 `ForegroundInputLayout` / `ReadForegroundLayout` / `DescribeLanguage`）、`scripts/VoxDeckInputBridge.cs`（`ProjectActiveProfile` + 三条新 self-test）、`scripts/VibeMic.cs`（seeding、USB 还原入口与 `UsbSuspendWasAppliedByApp`、前台布局日志、新增 self-test 断言）、`scripts/validate.js`（5 条新门禁）、`docs/V2_0_USER_GUIDE_ZH.md`（双击窗口文案从写死的 0.32 秒改为跟随系统；补推荐手势说明）、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`（USB 可还原）、`docs/images/03-shortcuts*.png`（重拍，画面里现在是推荐手势而不是一排「未配置」）。

### 仍未做（与本轮无关，如实列出）

- 每 Profile 一份双击/宏 —— 当初定为全局设计（store 是全局单条），要改需把 `gesture-layers.json` 升成按 Profile 分节 + 迁移。
- 宏（含条件/分支/导出）—— 宏已作为产品决定整体移除，不恢复。
- 出厂默认 Profile 对齐到用户实测方案 —— 见上，属产品决策。
- 真机/授权类：RC003 设备级过滤器、DPI 矩阵、安装升级卸载生命周期、VB-CABLE 复测、第三方输入法真实落点、经典 UWP 学习、免驱动模式真机、代码签名。

## 2026-09-11 真机落点验证（记事本 + ChatGPT）与「添加应用」三个缺陷

用户配合实体遥控器做了两次听写，另外报告「添加应用」列表里很多应用没有名字、没有 logo。两条线都有结论。

### 1）落点验证：记事本与 ChatGPT 都通过，且剪贴板全程未被使用

做法：先把日志的字节偏移做成 markerstamp，用户按完再读增量，关键行**回读原始日志**核对时间戳；同时**分别读宿主日志与冻结 Capture 的运行时日志**——真实回执（`WETYPE SESSION END`）写在 Capture 侧，宿主日志里没有它，只看宿主日志会误判成「没有回执」。

**记事本（generation 5，按住 7.7 秒）**

```
16:01:31.233 VOICE FOCUS LOCK armed=true target_id=foreground-notepad
16:01:31.234 VOICE INPUT TARGET ready=true code=OK
16:01:34.744 / :56.680 … VOICE FOCUS RESTORE skipped=true
        foreground=notepad target_foreground=True focused_edit=True   ← 焦点未被面板抢走
capture: audio_ms≈7.7s  丢包 0  间隔 46 ms
16:01:42.724 WETYPE TRANSCRIPTION SUBMIT generation=5 sent=True audio_delivered=True
16:01:43.219 WETYPE SESSION END generation=5 audio_delivered=True submitted=True panel_wait_ms=400
16:01:47.209 WETYPE PASTE FALLBACK payload_ready=False skipped=true reason=payload_missing
```

**ChatGPT（generation 8，按住 8.4 秒）**

```
16:05:55.203 VOICE FOCUS LOCK armed=true target_id=foreground-chatgpt source=focused_observation_transient
16:05:55.203 VOICE INPUT TARGET ready=true target_id=foreground-chatgpt code=OK
16:05:56.574 / :56.680 / 16:06:04.964  VOICE FOCUS RESTORE skipped=true
        foreground=chatgpt target_foreground=True focused_edit=True
capture: frames=569 audio_ms=8535 max_gap_ms=74 queue_drops=0 sink_queue_drops=0
16:06:05.232 WETYPE TRANSCRIPTION SUBMIT generation=8 sent=True audio_delivered=True
16:06:06.303 WETYPE SESSION END generation=8 audio_delivered=True submitted=True panel_wait_ms=850
16:06:09.719 WETYPE PASTE FALLBACK payload_ready=False skipped=true reason=payload_missing
```

**两条结论**：

1. `payload_missing` 是**好事**：两次都是输入法**直接上屏**，剪贴板从没被用过——这正是产品声称的路径，比"粘进去的"强。
2. **修正一条历史结论**：文档里写过「ChatGPT 桌面 UIA 未提供可验证编辑控件，因此输入目标仍未验证」。真机证据表明 ChatGPT 的输入框**能被实时验证**（`code=OK` + 三次 `focused_edit=True`）。精确限定：这次走的是**唤醒时的瞬时前台目标**（`foreground-chatgpt source=focused_observation_transient`），不等于「已把 ChatGPT 学成保存目标」——用户保存的默认目标仍是 notepad。也就是说：**按的时候 ChatGPT 在前台就能落字**，想在别的窗口前台时也收字才需要学习。

**顺带查清的一个疑点**：用户按过的那几轮里有两次前台是 Chrome，`WETYPE PASTE FALLBACK` 都 `payload_ready=False` → **根本没有粘贴动作**，所以浏览器里没有内容是正确结果。更早一轮（generation 6）确实发出了 `sent=True target_id=session-source-chrome`，但用户没看到内容 —— 这说明 **`sent=True` 只证明按键发出去了，不证明文字落进去**。宿主回执在这里存在过度声明，可选的收紧方式是：配置目标不匹配且无法确认可编辑控件时只提示手动粘贴，或把回执改成「已尝试粘贴」。（**未改，留给用户决定**。）

### 2）「添加应用」列表的三个缺陷（用户报告 → 截图复现 → 修复 → 截图复验）

用户报「很多 APP 都没有名字、没有 logo，包括全部当前正在运行的 APP 都没有 logo」。为了不靠猜，写了一个一次性脚本：启动 `--ui-smoke` → 点导航 → 找「＋ 添加应用」按钮 `BM_CLICK` → `PrintWindow` 抓对话框本身（不抓桌面）→ 再对 ListBox 发 `WM_VSCROLL SB_BOTTOM` 抓列表下半部分。**截图与用户描述完全一致**：

| 行 | 修复前 | 修复后 |
| --- | --- | --- |
| 01–07 | ChatGPT / Cursor / 记事本 / Chrome / **catprox** / **typeless** / **vibeflow** —— **全部没有 logo** | ChatGPT / Cursor / 记事本 / Google Chrome / **CatproX** / **Typeless** / **终端** —— **全部有 logo** |
| 08+ | 已安装的行有 logo、有名字 | 同左（未受影响） |

**缺陷 1：运行中的应用一律没有图标。** 根因在 `BeginFavoriteAppLearning`：运行中的应用走的是**新建 `InstalledAppChoice` 的分支，从来没给 `Icon` 赋值**；而 `AppPickerDialog` 画的是 `item.Icon`，为 null 就跳过 → 空白。实测本机 10 个运行应用里 **9 个能从自己的 exe 抽出图标**，所以这不是平台限制。

**缺陷 2：名字是原始进程名。** 运行行的名字来自 `FocusApplicationChoice.DisplayName`，而 `FriendlyProcessName` 只映射了 6 个应用（Cursor / VS Code / ChatGPT / 记事本 / Chrome / Edge），其余直接回退成进程名 —— 截图里的 `catprox`、`typeless`、`vibeflow`、`windowsterminal` 就是这么来的。

**缺陷 3：把言灵自己列成可学习的对象**（`vibeflow`）。排除表里只有 `vibemic`，而发布版进程名是 `VibeFlow`。

**修复**：改为**先建本机目录、再建运行行**，运行行从目录借名字与图标（`Google Chrome`、`终端`、`CatProX` 都是这么来的）；名字优先级抽成纯函数 `RunningApplicationLabel(curated → catalogue → exe 描述 → 进程名)` 并由 self-test 钉住 7 组取值；图标加载加**外壳回退**（`IconForExecutable`：先 `ExtractAssociatedIcon`，失败再问 shell —— 实测 Steam 与 BOOTICE 属于"自己不带图标资源、资源管理器里却有图标"的那类）；排除表补上 `vibeflow` / `voxdeckinputbridge` / `vibemicatvvcapture` 并暴露成 `ExcludedProcesses` 供 self-test 断言。

> 一次自我纠错：新 self-test 第一次就红了，我原以为是代码错，读回实际值才发现是**我的期望写错了**——`Chrome` 与 `chrome` 在 `OrdinalIgnoreCase` 下相等，所以它正确地被判为"没说新东西"而落到目录名 `Google Chrome`。断言改成打印实际值后立刻定位。这条也是教训：**断言必须能说出它看到了什么**。

### 3）顺带发现并修掉：同一个应用被列两次（167 → 99）

修复截图里注意到 `CatproX`（正在运行）与 `Catprox`（未运行）同时在列。实测两者指向**同一个文件** `D:\CatproX\CatproX.exe`，但 AppsFolder 那条的 `Path`/`AppUserModelID` 是 `org.erb.vortex`（应用自己注册的 AUMID，**不是进程名**）→ 两条来源的 `ProcessName` 不同 → 去重没生效。**`shell:AppsFolder` 不只列 UWP，也列桌面应用**，所以本机大量桌面应用都被列了两遍。

修复：两个来源统一按**进程名**去重；AUMID 不是包（不含 `!`）时，先用外壳暴露的 `System.Link.TargetParsingPath` 反查真实 exe 再取进程名（真正的 Store 应用这个属性为空，实测「照片」为空、`org.erb.vortex` 为 `D:\CatproX\CatproX.exe`）。**可选条目从 167 降到 99**，没有应用丢失。

### 4）经典 UWP：定为「不做」，并证明失败是安全的

用户问「这一项有必要测吗，我觉得对于有输入框的 APP，设置这类没有太大必要，你可以自己判断」。**判断：不做，并把它从"待验证"改成"有意的非目标"**。理由：

1. 这一类确实存在（本机实测「设置」的顶层窗口属于 `ApplicationFrameHost.exe`，内容属于 `SystemSettings.exe`），但**值得听写的应用都不在这一类**里；
2. 读代码确认学习是**失败即停**的：`CaptureFavoriteTarget` 10 秒内抓不到可验证控件就 `FAVORITE LEARN failed=true` + 提示「没有学到输入框，请重试」，**什么都不保存**——不会留下一个永远匹配不上的目标；
3. 已经写在 `V2_0_KNOWN_LIMITATIONS_ZH.md` 里，措辞是"决定，不是待办"。

这一项因此**不需要占用你的按键**。

### 本轮改动文件

`scripts/VibeMic.cs`（picker 重建顺序 + `RunningApplicationLabel`/`IsExecutableFileName` + self-test 断言）、`scripts/features/InstalledAppCatalog.cs`（`IconForExecutable`/`ExecutableForProcess`/`DescribeExecutable`/`LoadShellImage` + 按进程名与 AUMID 反查去重）、`scripts/features/FocusTargetService.cs`（`ExcludedProcesses` 补入产品自身进程）、`scripts/validate.js`（3 条新门禁）、`CHANGELOG.md`、`docs/V2_0_KNOWN_LIMITATIONS_ZH.md`。

## 2026-09-11 DPI 矩阵：先把工具做出来，顺手抓到两处真碰撞

### 做出来的工具：`scripts/check-ui-geometry.ps1`（已入库）

启动 `--ui-smoke` → 逐个点击 6 个导航项 → 枚举控件矩形 → 报告**同父控件里两个带文字的控件相交**的情况 → 每页存一张 PNG（`PrintWindow`，不抓桌面）。重叠时退出码为 1。

三条设计决定（都踩过）：

1. **只比"兄弟"控件**。父子重叠是正常的，一刀切"任意两个控件不得相交"会报出几百个假阳性——第一版就是这么被淹没的。
2. **`EnumChildWindows` 已经枚举全部后代**，不是只枚举直接子窗口。第一版我又对每个返回值递归，于是每个控件被按祖先个数重复计数：首页报了 **491 个"重叠"**，全是同一个控件和自己。改成一次调用后立刻变成真实数量。
3. 中文按钮文案用 `[char]` 码点拼，脚本保持纯 ASCII（无 BOM 的 `.ps1` 会被 5.1 按 ANSI 读，中文字面量会把解析器带崩）。

### 100% 基线下抓到两处真碰撞（已修，修完 6 页全部 0）

| 页面 | 碰撞 | 根因（坐标算术） | 修法 |
| --- | --- | --- | --- |
| 语音 | 绿色 `✓ CABLE Input（播放端）已检测 ✓ CABLE Output（录音端）已检测` 与灰色 `当前播放端点：…` **重叠 6 px** | `cableState` y482 高 26 → 482–508；`cableEndpoint` y502 高 20 → 502–522 | 状态行高 26→24、端点行 y502→506 高 20→18，两行恰好 482–524，接回下面的按钮行（524） |
| 设置 | `安全检查更新` 按钮与右侧产品信息标签 **重叠 10 px** | 按钮 x576 宽 124 → 576–700；标签 x690 → 690–928 | 按钮宽 124→108（576–684）、标签 x690→694 |

这两处**不是高 DPI 才出现的问题**——100% 下就存在，只是在四档扫描里才会被系统性发现。修完 `scripts/check-ui-geometry.ps1` 退出码 0、6 页全 0，并已把两处坐标钉进门禁（长期守卫仍是那个脚本本身）。

### 缩放实测进度（诚实记录）

- 用户第一次改为 125% 后，**系统层仍是 100%**：`GetDpiForMonitor(MDT_EFFECTIVE_DPI)=96`、`GetDpiForSystem=96`、且 `HKCU\Control Panel\Desktop\PerMonitorSettings` 键**根本不存在**（改过就会留下 `DpiValue`）。所以那一轮的数据只是重复了 100% 基线，不能算 125% 的证据。
- 教训：**不要凭"用户说改了"就当成改了**——系统层读一次成本极低，而把 100% 的数据标成 125% 会污染整个矩阵。
- 已修正路径提示（Windows 11：设置 → 系统 → 「屏幕」→ 缩放与布局 → 缩放；不是"辅助功能 → 文本大小"，后者只改字号不改 DPI）并请用户重试。**125%/150%/200% 三档仍待采集**。

## 2026-09-11 用户反馈「安装后屏幕乱码」：分诊结论与已做的加固

用户转述：很多用户在自己电脑上安装后屏幕出现乱码，推断是分辨率原因。先分诊再动手——**"乱码"和"分辨率"在技术上不是同一件事**，修错方向等于白干。

### 已排除的三条（都有实证，不是推理）

| 嫌疑 | 检验 | 结果 |
| --- | --- | --- |
| 安装器中文乱码 | 启动 `VibeFlow-Setup.exe`（**不点任何按钮**）并用 `PrintWindow` 截它的向导窗口 | **正常**：`选择安装语言 / 选择安装时使用的语言。/ 简体中文 / 确定 / 取消` 全部正确，图标也在。`.iss`/`.isl` 都无 BOM，但 Inno Setup 6 解码正确 |
| 运行时用了本机 ANSI 代码页 | 全仓 grep `Encoding.Default` / `GetEncoding(` | **一处都没有**（只有 WAV 头用的 `Encoding.ASCII`，那是正确用法） |
| 源码文件编码不合法 | 172 个文本文件做**严格 UTF-8** 校验 + 查编译产物里的 UTF-16 字面量 | 0 个非法；中文在 exe 里完好（注意：UTF-16 字面量可能落在**奇数**字节偏移，只按偶对齐搜会漏掉一半——我第一次就漏了 `语音听写`） |

### 抓到并修掉的真实脆弱性：编译期依赖谁在编译

同一份源码、同一台机器，只加一个开关：

```
默认（未指定）        '添加应用' = found        '语音听写' = found
/codepage:1252 编译   '添加应用' = NOT FOUND    '语音听写' = NOT FOUND   ← 中文全部消失
```

所有源码都是**无 BOM 的 UTF-8**，而四处 csc 调用**没有一处指定编码**——**正确与否取决于编译器的默认值**。这正是"**别人装的包乱码、我本地看不出**"的形态，且任何本地测试都发现不了。

**修复**：宿主、桥接、V2 测试套件三处 csc 加 `/codepage:65001` 并钉进门禁（**冻结的 Capture 构建脚本故意不动**——它按哈希冻结、不重建）。提交 `0525bca`。

### 字体：实测清楚了"缺字体到底会变成什么"

| 检验 | 结果 |
| --- | --- |
| 请求不存在的字体族会怎样 | **静默退化**成 `Microsoft Sans Serif`（`new Font("NoSuchFontXYZ",10).Name` 实测如此） |
| 中文在这种退化下还显示吗 | **还显示**——画「语」和「音」得到**不同**的位图，说明 Windows 做了字体链接（不是两个一样的方框） |
| **界面图标**在这种退化下呢 | **变成方框**——把 U+EA18 用退化字体画出来是一根**空心矩形**（24×24 位图肉眼确认），而正常字体画出来是一面**盾牌**。图标字形来自**私有使用区**，系统不做链接 |

所以"缺字体"的危害集中在**图标**，而且形态是"一页方框"——用户完全会称之为乱码。

**修复**：新增 `scripts/ui/UiFonts.cs`，按本机已装字体解析文本族（雅黑 UI → 雅黑 → 宋体 → 微软正黑 → Segoe UI）与图标族（Segoe MDL2 Assets → Segoe Fluent Icons → Segoe UI Symbol），**9 处图标调用点**全部改用它；新增 self-test：解析结果必须非空且**确实已安装**，并带**反向对照**（`IsInstalled("NoSuchFontXYZ-ForTheSelfTest")` 必须为 false）——证明这个检查真的能看出"缺失"，否则它只是一句复述代码的话。所有 `new Font("Segoe MDL2 Assets", …)` 已从宿主源码中消失（门禁钉住）。

### 让它"以后能自证"：把渲染环境写进日志与诊断

这是本轮最实用的一步——在拿到用户截图之前，先让**报告自带答案**：

```
UI RENDER text_font=Microsoft YaHei UI text_installed=yes text_substitute=no
          icon_font=Segoe MDL2 Assets icon_installed=yes icon_substitute=no
          screen=2560x1440 workarea=2560x1440 dpi=96 scale=1.00 monitors=1 window=1280x840
```

判读方式（已写进 `V2_0_KNOWN_LIMITATIONS_ZH.md` 给用户看）：`icon_installed=no` → 缺图标字体（方框）；`scale` 明显大于 1.00 且 `screen` 小 → 缩放/小屏布局问题；两者都正常而画面仍异常 → 才按视觉问题继续查。同一信息也进了自检页的**导出诊断**（`UI rendering:` + `Display:` 两行）。

### 布局：小屏不是"重叠"，而是"要横向滚动"

用 `scripts/check-ui-geometry.ps1` 的 `-ForceSize` 在四个窗口尺寸下量（1280×840 / 1100×700 / 1024×648 / 960×600）：**每页重叠数全是 0**。原因是内容跑在可滚动面板里（`AutoScrollMinSize`），窗口变小是**滚动**而不是重排。所以小屏的真实体验是"要横向滚动才能看全"，而不是叠字——这与"乱码"的形态不符，因此**高 DPI 下的手绘文字**（`DrawString` 画的卡片/遥控器示意图，几何检查看不到）才是更可疑的一类。

### 仍缺的决定性证据（等用户）

`150%/200%` 两档仍未采集。已请用户提供**一台出问题机器的整屏截图** + 4 项事实（Windows 显示语言、分辨率、缩放百分比、**看到的是方框还是错乱字母**）——这四项能一次性判定是字体、缩放还是编码，不必再猜。

## 2026-09-11 DPI 矩阵（1/3）：125% 实测 —— 每页都撞，根因是手调的固定偏移

用户把缩放改成 125% 后（系统层复核 `GetDpiForMonitor=120`、`GetDpiForSystem=120` ✔ 这次真的生效），几何检查结果：

```
window=1280x840  dpi=120  scale=1.25
01-home: overlaps=3      02-workflow: overlaps=2   03-shortcuts: overlaps=2
04-voice: overlaps=2     05-diagnostics: overlaps=2 06-settings: overlaps=2
```

碰撞全是同一类：**页面标题 × 副标题**（6 页全部，99–179 px 宽 × 9–10 px 高）与**侧栏「言灵」×「VIBE FLOW · V2.0.0」**。100% 下同一位置一个都不撞 —— 这正是"用户机器上乱、我这儿看不出来"的来源。

**根因（坐标算术，可复核）**：`AddPageTitle` 把 24pt 标题放在 y=24、把副标题写在**写死的 y=67**；而标题的**渲染高度**随缩放增长（100% 约 42 px、125% 约 52 px），那个 67 不跟着长。125% 时 24+52=76 > 67 → **重叠 9 px** ≈ 标题高度的四分之一，所以 150%、200% 只会更糟。

**三次修正，其中两次是我自己错的**（都记下来，很有代表性）：

1. 先试「上一行的 `Bottom` + 间隔」→ **更糟**（重叠 9 px 变 23 px）。量出来才发现：**标签在挂到父容器之前，`Height` 是框架默认的 23 px**，而它实际渲染 42 px。读早了。
2. 改用**容器布局**（`FlowLayoutPanel`：TopDown + AutoSize），让框架在渲染时按当前缩放测量——但第一版 helper **忘了设 `AutoSize = true`**，于是容器里每个标签都保持 WinForms 默认的 **100×23** 小盒子，文字全被截断（侧栏变成"VIBE FLOW ·"，标题变成"语语音桥接."）。**这次是截图抓到的**：几何检查当时报 0 重叠。
3. 补上 `AutoSize` 后恢复；三处堆叠（页面标题、侧栏品牌、首页 hero）全部改成容器布局。首页那条 RC003 告警是**固定 610 px 盒子装着 125% 下约 750 px 的文字**（截断在"请打开"自"），改为可换行 + 缩短文案——缩短时被**自家门禁拦下**：那条告警必须同时说清"麦克风音频仍可用"和"前台应用可能收到录音键"，于是保留两者、只压缩措辞。

**125% 终态**：六页 `overlaps=0`、`clipped=0`，并逐页截图肉眼复核（首页与快捷键页的卡片、标签全部完整）。

### 几何检查补上"文字被裁"这一维（两次做错，第三次才对）

**重叠规则看不到裁切**——第 3 次修正正是因为截图里文字被截断、而检查报 0。新指标：按控件**当前宽度**算文字需要多高（`DrawText` + `DT_CALCRECT` + 换行），与控件自身高度比较。

两次做错的记录：
- 只量**单行宽度** → 把所有**自动换行的段落**误报成裁切（880 px 标签装 2701 px 句子，实际正常换行显示）；
- 依赖 `WM_GETFONT` → **WinForms 标签对该消息返回 0**，于是用 DC 默认字体去量，一个确实被裁的标签被判成"放得下"（假阴性，正是截图抓到的那个）。

终版：先取控件字体，取不到退到系统消息字体，并在输出里注明是**近似值**（只用于抓"明显装不下"），另加 3 px 容差。脚本退出码现在同时覆盖重叠与裁切。

## 2026-09-11 DPI 矩阵（2/3）：150% 暴露真正的病根 —— 布局根本不跟着缩放走

### 先纠正我自己的一个方法错误

我第一次"从系统层复核缩放"用的是 `GetDpiForMonitor` / `GetDpiForSystem`，150% 时报 **96**，差点又判成"用户没改"。**错在我的探测方式**：PowerShell 是 DPI **不感知**进程，这两个 API 对它返回 96。改用**应用自己**（PerMonitorV2 感知）的测量后：`dpi=144 scale=1.50` ✔。

教训：**"从系统层复核"本身也要选对进程上下文**——否则它比"听用户说"更危险，因为它看起来像硬证据。

### 病根：页面是导航时构建的，而 WinForms 的 autoscale 只在加载时跑一次

`ShowPage()` 每次导航都 `DisposePageControls()` + `BuildPage(...)`；而 `AutoScaleMode = Dpi` 的缩放发生在**窗体加载时**，只作用于当时已存在的控件。于是：

- **字号**跟着 DPI 长（GDI+ 按点值在当前 DPI 渲染）；
- **几何坐标**永远停在 96 dpi 的设计值。

用应用自报的数字看得很清楚（150%）：

```
修复前： window=1920x1260 … sidebar=232x1204   ← 窗口我上一轮已修，侧栏仍是 96 dpi 的 232
修复后： window=1920x1260 … sidebar=348x1204   ← 232×1.5
```

**这也解释了 125% 我一条条修的那些碰撞**：它们全是同一个病根的症状，逐条修就是打补丁。

### 修法（一次性，覆盖所有页面）

1. `ScaleLayoutTree(control, scale)`：递归按比例缩放**位置**；**非 AutoSize** 控件才缩放**尺寸**（AutoSize 标签自己按字体测量）；`Dock` 控件按停靠边只缩放对应维度（侧栏是 `Dock=Left`，`Size` 被忽略，所以必须显式设 `Width`）；**字体一律不碰**（GDI+ 已按 DPI 渲染点值，再缩放就是二次缩放）。
2. 两个挂载点：`ShowPage()` 里 `BuildPage(...)` **之后**对 `content` 缩放（页面每次重建，必须在每次构建后做）；`ClampWindowToWorkingArea()` 里对窗体缩放一次（侧栏/底栏等构造函数里建的壳）。
3. 滚动画布 `AutoScrollMinSize` 同步按比例（否则页面被裁而不是可滚动）。
4. 两处"位置取决于文字宽度"的行改为**按实测文字摆放**：RC003 告警按 `TextRenderer.MeasureText` 预留高度、下面按钮行跟着走；首页回执卡片图标按标题实测宽度摆放。

### 150% 终态（应用自报 + 几何 + 截图三方一致）

```
UI RENDER … dpi=144 scale=1.50 … window=1920x1260 sidebar=348x1204 content=1524x1204
几何检查：六页 overlaps=0、clipped=0
截图：首页 hero 的事实行不再压副标题、按钮行完整（"检查连接"不再截断）、告警单行完整；快捷键页 Profile 与手势卡片比例正常
```

### 仍未做

- **200% 未测**（按同一机制应同样成立，但需要实测）。
- **100% 未回测**：`ScaleLayoutTree` 在 `scale<=1.01` 时直接返回，所以 100% 路径理论上完全未变；仍应回测一次确认。
- 侧栏**导航图标**是 `CreateNavigationIcon` 生成的固定尺寸位图（34×24），没有跟随缩放——在高 DPI 下会显得偏小（已知，未修）。
- 深色 / 跟随系统主题、1366×768 与 1920×1080 分辨率、最小窗口、完整键盘导航仍未测。

## 2026-09-11 DPI 矩阵（3/3）：200% —— 矩形圆角被自己的 Region 裁掉

### 200% 实测（应用自报 + 几何 + 截图）

```
UI RENDER … dpi=192 scale=2.00 … window=2528x1408 sidebar=464x1337 content=2004x1303
几何检查：六页 overlaps=0、clipped=0
```

窗口设计尺寸是 2560×1680，被工作区上限（2560×1440，留 32 px 边）夹到 **2528×1408** ✔ 符合预期（200% 下窗口装不下屏幕是正常的，页面靠滚动）。侧栏 232→**464**（×2）、内容区 2004 ✔。

### 截图仍然抓到两个自动化检查看不见的问题

**1）圆角徽章被自己的 Region 裁掉（已修）**

Profile 卡上的 `手动模式` / `安全直通` / `未开启` 三个状态徽章在 200% 下渲染成**一个小色块、文字被切掉**。根因很确定：

```csharp
ApplyRoundedRegion(effectiveBadge, 6);   // 用「构造时」的 152x34 生成 Region
…
ScaleLayoutTree 之后 Size 变成 304x68，但 Region 仍是 152x34
→ 控件被裁剪到旧形状，文字画在可见区之外
```

`ApplyRoundedRegion` 现在**在 Resize 时重建 Region**，并且圆角半径也按设计值缩放（6→12），所以比例一致。这个类**重叠规则看不到**（文字在自己的控件内，不发生重叠）、**文字高度规则也看不到**（Region 不计入 client rect）——只能靠截图。

**2）几何检查自己不是 DPI 感知的（已修）**

`GetWindowRect` / `PrintWindow` 对**不感知 DPI 的进程**会返回虚拟化坐标与虚拟化位图：150% 时我把 1920×1260 的窗口读成 1280×840（当时只当是"换算关系"，没意识到位图也是虚拟化的），到 200% 就暴露成**截图只截到窗口左上四分之一**。现在脚本开头声明 PerMonitorV2（失败退回 system-aware），测量与截图都是物理像素，并且**脚本读到的窗口尺寸与应用自报完全一致**（2528×1408 @200%）——这也给了两套独立测量一个交叉校验。

### DPI 矩阵结论（四档全部实测）

| 档位 | 结果 |
| --- | --- |
| 100% | 六页干净；`scale=1.00` 走的是未改动路径（`sidebar=232 content=1015`） |
| 125% | 修掉 6 页「标题×副标题」与侧栏「言灵×版本」碰撞 |
| 150% | **暴露病根**（页面在导航时构建、从不跟随缩放），修完窗口/侧栏/页面/滚动画布同步缩放 |
| 200% | 上述缩放成立；额外修掉圆角徽章被 Region 裁剪 |

### 仍未做

- **侧栏导航图标**与**遥控器示意图**是固定尺寸绘制（导航图标位图 34×24；`RemoteVisual` 内部缩放上限 1.15），在高 DPI 下会显得偏小——已知，未修。
- 深色 / 跟随系统主题、1366×768 与 1920×1080 分辨率、最小窗口、完整键盘导航仍未测。
- 首次设置向导与三个对话框未逐档复核（本轮只覆盖六个页面）。

### 补做：跟随缩放的两处「绘制型」元素（原「仍未做」第 1 项）

| 元素 | 机制 | 修法 |
| --- | --- | --- |
| 侧栏导航图标 | `CreateNavigationIcon` 画进**固定 34×24 位图**（硬编码坐标），按钮放大后图标显得越来越小 | 位图按 `DesignScale()` 放大，绘制用 `graphics.ScaleTransform(scale, scale)`（画笔宽度在**设计单位**里，自动跟着缩放） |
| 遥控器示意图 | `RemoteVisual` 按 112×440 设计单位绘制，且内部缩放**上限写死 1.15** | 上限改为 `1.15 × DesignScaleFactor`，由拥有它的窗体在 4 处构造点注入 `DesignScaleFactor = DesignScale()` |

**200% 复验**：导航图标与文字同比例；快捷键页的遥控器示意图填满卡片（修复前明显偏小）；六页 `overlaps=0 / clipped=0`。门禁钉住 `ScaleTransform`、放大后的位图尺寸、`DesignScaleFactor` 的字段与注入顺序。

## 2026-09-11 主题矩阵：深色主题**根本无法启动**（一个 P0）

原「未验证：深色 / 跟随系统」这一项，一测就发现它不是"没验过"，而是**坏到起不来**。

### 现象与根因

把 `theme` 改成 `dark` 后，已安装的 `VibeFlow.exe` **启动即退出**：

```
退出码 = -532462766 = 0xE0434352（未处理的 .NET 异常）
事件日志 CLR20r3 + Application Error: VibeFlow.exe, 异常代码 0xe0434352
```

.NET Runtime 事件直接给出了异常类型与堆栈：

```
System.ArgumentException
   在 System.Drawing.Color.CheckByte(Int32, System.String)
   在 System.Drawing.Color.FromArgb(Int32, Int32, Int32, Int32)
   在 VibeMicForm.StatusBorder(System.String)
   在 VibeMicForm.BuildOverview()      ← 首页在构造函数里就建了状态边框
   在 VibeMicForm.BuildPage(VibePageId)
   在 VibeMicForm.ShowPage(Int32)
   在 VibeMicForm..ctor(...)
```

代码就一行：

```csharp
return darkTheme
    ? Color.FromArgb(accent.R + 62, accent.G + 62, accent.B + 62)   // ← 通道可超 255
    : Color.FromArgb(accent.R, accent.G, accent.B);
```

深色调色板里 **violet 的蓝 213、amber 与 coral 的红 205**，`+62` 后分别到 275/267/267 —— `Color.FromArgb` 对超范围通道**抛异常**。首页在构造函数里就调用它，所以：**选「深色」，或在深色 Windows 上选「跟随系统」（本机 `AppsUseLightTheme=0`，属于常见情形），应用直接起不来。**

也就是说：**深色主题从来没有可用过**。文档把它列为"未验证"是准确的，但没人跑过它，所以这个 P0 一直躺着。

### 修复

1. 抽成 `LightenChannel(int channel)`：`Math.Min(255, Math.Max(0, channel + DarkBorderLighten))`，`DarkBorderLighten = 62` 提为常量。
2. 新增 `RunThemePaletteSelfTests()`：断言三个**当年会抛异常的值**（213/205/196 → 255）、边界（174→236、0→62、-100→0、400→255），并**遍历 -20..275 全范围**确认每个值都能被 `Color.FromArgb` 接受。
3. 门禁：钉住 `LightenChannel`、常量、`StatusBorder` 的调用形式，并**反向断言 `accent.R + 62` 不得再出现**。

> 顺带记一笔：新断言第一次也红了——我把 `LightenChannel(-40)` 写成了期望 0，实际是 22。**断言写错和代码写错一样要当场量清楚**，改的是断言不是代码。

### 主题矩阵结果（200% 缩放下，已安装/仓库构建都测）

| theme | 启动 | 背景平均亮度（三处采样） | 判定 |
| --- | --- | --- | --- |
| `system` | ✔ | 35 | 深色（本机 Windows 就是深色）✔ 符合跟随语义 |
| `light` | ✔ | 249 | 浅色 ✔ |
| `dark` | ✔ | 35 | 深色 ✔ |

深色模式六页全部构建成功、几何检查 `overlaps=0 / clipped=0`，并逐页截图确认文字可读（不是"能渲染"就算过）。

**用户配置已还原**：改动前备份、改后逐字段比对，最终与备份**完全一致**（`theme=light`，4 个 Profile、12 条映射、smartProfiles=true 不变）。

### 工具顺带增强

`scripts/check-ui-geometry.ps1` 新增 `-ProcessId`：直接测量**已经在运行**的应用（不再另起 smoke 实例）。这是主题矩阵的前提——smoke 模式跑在 `tmp\ui-smoke` 的独立配置上，读不到用户的主题。同时 `-Exe` 改为"仅在需要自行启动时必填"，并在缺参时给出可读的报错。

## 2026-09-11 首次设置向导的 DPI 复核（原「仍未做」第 3 项）

### 先是我自己的工具在骗人

第一次抓向导截图，看到的是**内容被裁到右边、标题挤成一团**——差点判成"向导在 200% 下坏了"。实际是**截图脚本自身不是 DPI 感知的**：不感知 DPI 的进程拿到的窗口矩形是虚拟化的，于是它按**一半尺寸**申请位图，内容画进去就被裁掉：

```
向导真实窗口 2026x1416  →  抓出来的图 1013x708（正好一半）
```

这与我先前在几何检查里修的是**同一个缺陷**，只是发生在另一个脚本里。`capture-ui-screenshots.ps1` 现在同样声明 PerMonitorV2（失败退回 system-aware）。**这条很重要：截图是我判断布局的唯一证据，证据本身失真就会得出反向结论。**

### 向导的真实问题（修完工具后看清）

窗口被 WinForms 缩放成 2026×1416，但**内容仍是 96 dpi 布局 + 双倍字号**：标题被压进副标题里、左侧步骤名被截成「确认设备与」「选择工具并」、圆点编号丢失。

**为什么"跟页面一样缩放一次"不管用**：`renderStep` 是**每次切步骤都清空 `pageContent` 重建**的委托，而且**有 8 处以上的提前 `return`**，所以我加在委托末尾的缩放**根本执行不到**。

修法：

1. 向导 chrome（左栏/页脚/按钮）在 `ShowDialog` 前缩放一次；
2. 新增 **scale-on-add 钩子**（`ScaleControlsAddedLater` / `InstallScaleOnAdd` / `ScaleControlBounds`）：任何**之后**加入的控件——任意深度、每个只缩放一次——在加入时即被缩放，绕开提前 return 的问题；
3. 左栏步骤名与隐私说明由固定盒子（146×28 / 180×48）改为**自测量**（隐私说明可换行）。

### 200% 复验

`00-setup-01-device`（设备与用法页）与 `00-setup-04-dictation`（工具与听写页，含输入框、测试框、四个按钮、两行说明）**逐张与 100% 版并排对比**：布局一致、文字完整、无截断。左栏步骤名恢复为「确认设备与用法 / 连接并测试遥控器 / 准备本地音频通道 / 选择工具并完成听写 / 开机即用」。

### 仍未做

- 三个对话框（添加应用选择器、Capture & Ask、Browser Remote Lite 等**独立 Form**）未逐档复核——它们与向导同类（运行时创建），需要时用同样的 scale-on-add 处理；
- 1366×768 / 1920×1080、最小窗口、完整键盘导航仍未测。

### 补做：独立对话框（原「仍未做」第 1 项）

**先纠正一个我自己的误判**：我最初以为截图里的 `09-smart-profile-apps.png` 是 `AppPickerDialog`（"添加应用"），照着它改成 `UiDisplayScale.Apply(this)`，重拍后**文件大小一模一样**——说明那个窗口根本不是那个类。查证后：它是 `VibeMic.cs` 里**内联创建**的「绑定 Smart Profile 应用」对话框（760×610）。**文件名不等于类名，改之前要先确认窗口是谁建的。**

实测：200% 下该对话框窗口仍是 **766×661**（未缩放），标题被画成"双影"（2× 字号塞进 1× 盒子）、副标题被截断。

### 修法：算法收敛到一处，所有对话框统一接入

新增 `scripts/ui/UiDisplayScale.cs`（已加入 `BUILD_VIBE_MIC.cmd` 与 `requiredFiles`）：

- `ForControl`：取控件所在显示器的缩放（PerMonitorV2 的 `GetDpiForWindow`，失败退回 `CreateGraphics().DpiX`）；
- `Apply(Form)`：挂 `Form.Load`，装载时**把窗口与内容一起缩放**，并安装 scale-on-add；构造函数第一行调用即可；
- `Tree` / `Bounds` / `AddedLater`：与页面/向导同一套算法（AutoSize 不缩放尺寸、Dock 只缩放对应维度、字体永不缩放、每个控件只缩放一次）。

`VibeMic.cs` 里原有的私有实现改为**委托**到该类（签名与调用点不变，避免两套实现漂移）。

接入的对话框：

| 位置 | 数量 | 说明 |
| --- | --- | --- |
| `VibeMic.cs` 内联 | 8 | 绑定 Smart Profile、新建 Profile、动作配置、编辑常用应用、用语片段、录制键盘快捷键、选择应用 ×2 |
| `scripts/ui/*.cs` | 5 | AppPickerDialog、LiveHudForm、ContextDeckForm、CaptureAskForm、BrowserRemoteLiteForm |

**首次设置向导**保持原路径（它自己已有 chrome 缩放 + scale-on-add），**未**重复接入以免二次缩放。

### 200% 复验

`09-smart-profile-apps.png` 由 **766×661 → 1532×1322**（正好 ×2）：标题「哪些应用使用"通用导航"？」单影清晰、副标题完整、表头与行完整、按钮齐全、列表可滚动。向导与主窗口截图尺寸不变（2026×1416 / 2528×1408）。

**注意**：另外 4 个内联对话框与 5 个 `scripts/ui` 对话框是通过**同一个机制**接入的，本轮只对 Smart Profile 这一个新接入点做了截图实证；其余未逐个截图验证（清单已记在下方）。

### 补做：对话框的**逐个实测**（原「仍未做」第 1 项）

上一轮把 13 个对话框接入统一缩放，但只对「绑定 Smart Profile 应用」做了截图实证，并如实标注了其余未验证。本轮补上验证手段与实测。

#### 工具：几何检查新增 `-WindowTitle`（对话框不是页面，页面扫描看不到它们）

`check-ui-geometry.ps1 -ProcessId <pid> -WindowTitle "添加应用"` → 找到该窗口、按同一套规则（兄弟重叠 + 文字是否放得下）测量并截图，**不启动也不结束任何进程**。

驱动方式（临时脚本，不入库——它是应用专属且易碎）：导航到某页 → 按控件文本点击 → 等窗口出现 → 调用上面的检查。

#### 实测结果（200% 缩放，均为物理像素）

| 对话框 | 家族 | 测得尺寸 | 设计尺寸 | 判定 |
| --- | --- | --- | --- | --- |
| 新建快捷键 Profile | 内联（`VibeMic.cs`） | 1052×594 | 526×297 | **×2 恰好一次** ✔ overlaps=0 clipped=0 |
| 添加应用（AppPickerDialog） | `scripts/ui` 类 | 1160×1320 | 580×660 | **×2 恰好一次** ✔ |

**这直接回答了接入时最大的风险——"会不会缩放两次"**（构造函数里加子控件的对话框，可能在 WinForms 自身 autoscale 之前就已存在）。实测两者的窗口都恰好是设计尺寸的 2 倍：既不是 1 倍（没缩放），也不是 4 倍（缩放两次）。

#### 顺带发现并修掉一个**与 DPI 无关**的真实缺陷

AppPickerDialog 的计数标签「共 95 个可选」与「确定」按钮**盒子重叠 28 px**（200% 下测到 56×44）：设计里标签盒子是 x 20..360，而按钮从 x 332 开始。**这个重叠在 100% 下同样存在**——只是当前文案短（约 80 px）所以肉眼看不出来；一旦数量变成「共 1234 个可选」就会压到按钮下。已把标签盒子收到 296 px（20..316）留出间隔。

> 这也说明"盒子重叠"与"视觉重叠"是两件事：本规则报的是**盒子**。计数标签这条属于**潜在**缺陷而非当前可见缺陷——记录时区分清楚。

#### 关于 Margin 的一处改动（诚实标注）

曾假设"锚定控件因 margin 未缩放而贴边"，于是把 `Margin` 一并缩放。**实测该假设不成立**：选择器的重叠不是 margin 造成的（修掉后数值不变，真正的成因是上面的设计盒子）。`Margin` 缩放本身是自洽的（锚定控件与设计边距保持比例），且**六页矩阵复测仍为 0 重叠 / 0 裁切**，故保留；但它不是那个重叠的原因，这一点必须写清楚，避免后来者把它当成因果。

#### 仍未做

- 其余 4 个内联对话框（用语片段、录制快捷键、编辑常用应用、选择应用 ×2）与 4 个 `scripts/ui` 对话框（Live HUD、Context Deck、Capture & Ask、Browser Remote Lite）**仍未逐个实测**——后两者需要真实录音/托盘触发，本机无此条件；
- 1366×768 / 1920×1080、最小窗口、完整键盘导航。

## 2026-09-11 窗口尺寸矩阵 + 最小尺寸未缩放（原「仍未做」第 2 项）

### 强制窗口尺寸扫六页（200% 缩放下）

用 `-ForceSize` 把窗口强制到三档，逐页跑几何检查：

| 强制尺寸 | 物理 | 等价逻辑 | 六页结果 |
| --- | --- | --- | --- |
| 880×500 | 880×500 | 440×250 | 0 重叠 / 0 裁切 |
| 1366×768 | 1366×768 | 683×384 | 0 重叠 / 0 裁切 |
| 1920×1080 | 1920×1080 | 960×540 | 0 重叠 / 0 裁切 |

**这个"干净"要打折看**：窗口小的时候大量内容滚出可视区，而**不可见的控件不参与比较**，所以 0 重叠只说明"看得见的部分不撞"，不等于"这个尺寸下可用"。因此补了截图肉眼确认：880×500 与 1366×768 下界面**完整、可滚动**（出现水平/垂直滚动条），没有被压扁或裁掉——这才是要的结论。

### 真正的缺陷：最小窗口尺寸没跟着缩放

`MinimumSize = new Size(880, 500)` 是**设计值**，我前面的缩放只处理了子控件，没处理它。后果：200% 下窗口仍可被拖到 **880×500 物理像素 = 440×250 逻辑**——远低于布局的设计前提，光侧栏就占掉一半以上。

修法：`MinimumSize = new Size(ScaledDesign(880), ScaledDesign(500))`；`FitToWorkingArea` 本来就会把最小尺寸下调以适配工作区 ✔ 所以小屏不会被锁死。

**可测量化**：诊断行新增 `minimum=`，应用自报 200% 下为 **`minimum=1760x1000`** ✔（100% 下仍是 880×500，未走新代码路径）。

### 仍需在对应缩放档位下补测

1366×768 / 1920×1080 的**真实**场景是"小屏 + 它自然的缩放档"（通常 100%/125%）。本轮是在 200% 下强制窗口尺寸，属**同等严格但不等价**的测试：它覆盖"窗口被压小"的行为，不覆盖"小屏 + 低缩放"下的渲染。等下次系统缩放到 100% 时补测一次即可（成本很低）。

### 仍未做

- 完整键盘导航（Tab 顺序、焦点可见性、快捷键全路径）未测；
- 真机/授权类项目（RC003 设备级过滤器、安装升级卸载生命周期、VB-CABLE 全新建路径、第三方输入法落点、经典 UWP、免驱动真机、签名）。

## 2026-09-11 键盘可访问性：Tab 可达性（原「仍未做」第 1 项的一部分）

**本轮没有改产品代码**，产出是"测量结论 + 一个工具层面的重要发现"。不为了凑提交而提交。

### 先踩到并纠正：UIA 在这台机器上**不可用作仪器**

最初的键盘检查用 UI Automation 遍历控件，输出是：

```
01-home: interactive=0  unreachable=0  ...
（六页全部 interactive=0）
```

这**读起来像"键盘全通过"，实际是什么都没测到**。追下去：UIA 树里 93 个后代**全部是 `ControlType.Pane`，全部 `focusable=False`**，连「启动语音桥接」「检查连接」这种明摆着是按钮的也是 Pane。

**关键的隔离实验**：我用同样的 `csc` 写了一个**最小 WinForms 程序**（一个普通 Button、一个 TextBox、一个 Label，零自定义控件），UIA 读出来**同样是 Pane / focusable=False**。→ 结论：**这是仪器/环境的问题，不是本应用的缺陷**。如果没做这个对照，我就会把"辅助技术看不到界面"当成应用缺陷写进文档。

**由此得到两条工具结论，其中第 2 条我随后自己纠正了：**
1. 本机**不能用 UIA 判定**这个应用的控件类型/可聚焦性；我的 UIA 键盘检查输出作废、不采信；
2. ~~`capture-ui-screenshots.ps1` 里基于 UIA 的操作（如按名字勾选 CheckBox）在本机**很可能静默无效**~~ → **这条是错的，已在下一节纠正**：那个函数**全仓没有任何调用点**（死代码），而且它找不到控件时会 `throw`——**是响亮失败，不是静默通过**。我把一个尚未证实的担心当成结论写了下来，这本身就是要避免的错误。

### 换用可靠的 Win32 判据：`WS_TABSTOP` 审计（逐页）

鼠标能操作的控件必须有 `WS_TABSTOP`，否则 Tab 到不了。按页统计（`BUTTON`/`EDIT`/`COMBOBOX`/`LISTBOX` 等）：

| 页面 | 可操作控件 | 带 TabStop | 缺 TabStop |
| --- | --- | --- | --- |
| 01 首页 | 11 | 11 | 0 ✔ |
| 02 工作流 | 7 | 7 | 0 ✔ |
| 03 快捷键 | 66 | 66 | 0 ✔ |
| 04 语音 | 16 | 15 | 1 |
| 05 自检 | 6 | 6 | 0 ✔ |
| 06 设置 | 22 | 20 | 2 |

三个"缺 TabStop"的，**逐个回到代码确认，全部是刻意的**：

- 语音页「听写时自动使用遥控器麦克风（推荐）」：`autoRoute.TabStop = advancedAudioUnlocked && !triggerOnlyVoicePage;`（同时 `AutoCheck = false`、颜色转灰）→ 未解锁时是**锁定项**，不该拿焦点 ✔
- 设置页「设备识别：…」与「本地安全模式：…」：`AutoCheck = false; TabStop = false;` → 它们是**只读状态行**（用勾选框样式呈现的说明），不是可操作项 ✔

**所以：六页里所有真正可交互的控件都能被 Tab 到达**；被跳过的三个是设计使然。**这条是"代码确认过"的，不是"看数字猜的"。**

### 仍未做到（如实标注）

- **Tab 顺序**（顺序是否合理、是否有焦点陷阱）**未验证**：实测需要读取真实焦点，而我用 `AttachThreadInput` + `GetFocus` 的尝试每次都只返回顶层窗口，没有得到可信的焦点序列——**在拿到可信仪器之前不下结论**；
- 键盘快捷路径（不点鼠标、纯键盘走完一条完整流程：切页 → 操作 → 回来）未验证；
- `capture-ui-screenshots.ps1` 的 UIA 步骤待改成 Win32。

### 纠正 + 收尾：截图脚本不再依赖 UIA

**先纠正我上一节写错的一条**：我说"截图脚本里基于 UIA 的勾选步骤很可能静默无效"。查证结果是**两处都错**：

- 该函数（`Set-ChildCheckboxUnchecked`）**全仓没有任何调用点**——是死代码，它不可能影响任何已产出的截图；
- 它找不到控件时会 `throw "Checkbox not found"`——**是响亮失败，不是静默通过**。

所以那一轮我担心的情况并没有发生。**但我把一个未经证实的推断写成了结论**，这正是今天反复出现的同一类错误（把"看起来可能"当"已确认"）。已在上一节就地标注并纠正，没有悄悄删掉。

**仍然做了改造**（理由：验证脚本里的每一步都应当既正确又自检）：

1. `Set-ChildCheckboxUnchecked` 改为 **Win32 实现**：`Find-ChildCheckbox` 按类名含 `BUTTON` + 文本定位；`BM_GETCHECK`(0x00F0) 读状态；需要时 `BM_CLICK`(0x00F5) 切换（用 `BM_SETCHECK` 会改状态但**不触发应用的 CheckedChanged**，所以要 Click）；**再读一次状态确认已清除，否则 `throw`**——不允许"看起来执行了"。
2. 移除 `Add-Type -AssemblyName UIAutomationClient`：该脚本现在**零 UIA 依赖**。
3. 顺带清掉脚本里残留的 5 个 em dash + 2 行中文注释 → **全文纯 ASCII**（与文件自己声明的约定一致：中文一律由码点构造，否则在 BOM-less + Windows PowerShell 5.1 下会被当 ANSI 读而炸）。

过程中踩到并记录的坑：
- 我第一次把 `SendMessage` 的 P/Invoke 又声明了一遍 → **类型已定义同名同参数成员的编译错误**（该类本来就有）；
- 我插入的 C# 注释里的 em dash 触发了 **Add-Type SOURCE_CODE_ERROR**（正是上面第 3 条的原因）；
- 定位这两处靠的是"**只把 Add-Type 块单独拿出来编译**"这个小手法，比反复跑整个脚本快得多。

**验证**：脚本在 200% 下完整跑通，**14 张截图全部产出**（含六页 + 向导 6 步 + 选择器），退出码 0。门禁已钉住"脚本不得再出现 `Windows.Automation`/`UIAutomationClient`"以及 Win32 实现的三要素（0x00F0 / 0x00F5 / 未清除即抛异常）。

### Tab 顺序：三次仪器尝试都失败，**如实记为"本机测不了"**，不凑数字

在拿到 WS_TABSTOP 审计（结论：所有可交互控件可达，3 个例外是刻意的）之后，我试图进一步验证**Tab 顺序**与纯键盘流程。三次尝试、三个**各自具名**的失败原因：

| 尝试 | 失败原因（实测证据） |
| --- | --- |
| ① UIA 遍历控件并读焦点 | UIA 在本机**看不到控件类型**：93 个后代全是 `Pane`/`focusable=False`。**隔离实验**证明这是环境而非应用问题（最小 WinForms 程序同样如此）。输出作废 |
| ② `AttachThreadInput` + `GetFocus` 读焦点 | 第一次失败是我自己的 bug：`GetWindowThreadProcessId` 的 out 参数传了 `IntPtr.Zero` → 线程号是垃圾值 → attach 必然失败；而我加的 fallback 每次都返回主窗口，**看起来像"焦点一直在窗口上"**。修好 out 参数并强制创建消息队列后，仪器明确回报 **`attached=False`**——attach 被拒绝 |
| ③ `SendKeys('{TAB}')` 驱动 | 同一轮回报 **`foregroundIsApp=False`**：应用从未成为前台窗口，`SetForegroundWindow` 没能生效。**这意味着我发出的 Tab 根本没进应用**，前面所有"焦点序列"都没有意义 |

**结论：本机没有可用仪器**去（a）驱动应用的键盘输入、（b）读取其焦点。因此 **Tab 顺序与纯键盘流程仍为未验证**——我不把 ② 那串"no focused child"当成"没有焦点问题"的结论，它只是仪器没接上。

**下一步的正确做法（不需要外部仪器，也不需要前台）**：在应用自己的 `--self-test` 里做——构造窗体、逐页 `ShowPage`，用 `SelectNextControl` 走一遍 Tab 顺序并记录序列，断言"每个 `TabStop` 控件恰好被访问一次、顺序符合预期、能循环回起点"。这是**确定性**的、可在 CI 跑、且不依赖任何外部焦点控制。已记为待做，尚未实现。

### 补做：Tab 顺序 —— 改为**应用内自测**，绕开三个失败的仪器

外部仪器全部不可用（UIA 看不到控件类型；`AttachThreadInput` 被拒；`SendKeys` 需要前台而拿不到），所以把测量搬进应用自己：`--ui-smoke` 启动时逐页走一遍并写日志 `UI TABORDER ...`。用 `SelectNextControl(current, forward: true, tabStopOnly: true, nested: true, wrap: true)`，**对控件树查询**，不需要前台、不需要焦点、不需要 UIA。

#### 这个仪器本身也错了两次，都记下来

1. **用名字做"访问过"判据** → 自检页有一列按钮**同名**「打开应用并学习」，走到第二个同名控件就被当成"已访问"而提前结束：报告 **stops=1**，而实际可达 34 个。修复：**按控件身份**（引用相等）判重。
   → 这一条是被"Win32 审计说该页有 6 个可见带 TabStop 的按钮、而 walk 只有 1"的矛盾逼出来的；我先假设是"按钮被禁用"，**用 Enabled 探针验证后发现全部 enabled=True**，假设被推翻，才找到真正原因。
2. **同一页面走两遍/日志跨运行累加**：`OnShown` 可多次触发 → 加 `tabOrderLogged` 只记一次；另外该日志文件**跨次运行累加**，读取时必须只取最后一次的 6 行（我第一次把 30 行当成"跑了很多页"，其实是 5 次运行）。

#### 修复后的实测（最后一次运行）

| 页面 | walk（Tab 实际可达） | tree（声明为 TabStop 且可操作） | 差 | skippedNonTabStop（刻意排除） | visitedNonTabStop（负对照） |
| --- | --- | --- | --- | --- | --- |
| 0 首页 | 12 | 11 | +1 | 0 | **0 ✔** |
| 1 工作流 | 7 | 7 | 0 ✔ | 0 | **0 ✔** |
| 2 快捷键 | 66 | 66 | 0 ✔ | 0 | **0 ✔** |
| 3 语音 | 13 | 15 | −2 | 1 | **0 ✔** |
| 4 自检 | 34 | 34 | 0 ✔ | 0 | **0 ✔** |
| 5 设置 | 20 | 20 | 0 ✔ | 2 | **0 ✔** |

- **负对照通过**：六页 `visitedNonTabStop=0`，即"走到过某个刻意不可聚焦的控件"一次都没发生——这是证明"这个数字确实在量 Tab 顺序"的关键。
- **与另一个独立仪器交叉验证**：`skippedNonTabStop` = 语音 1、设置 2，与 Win32 `WS_TABSTOP` 审计发现的 3 个刻意例外**完全对应**（代码已确认：一个锁定开关 + 两条只读状态行）。
- 首页 walk(12) = tree(11) + `RemoteVisual`（自定义绘制控件，带 TabStop 但不在"可操作控件"白名单里）→ 差 +1 已由顺序串自行解释。
- **顺序本身**已可见，例如工作流页：`＋ 添加应用 > 首页 > 工作流 > 快捷键 > 语音 > 自检 > 设置`；设置页：开关项 → 按钮项 → 六个导航键收尾。

#### 仍未解释（如实记录）

**语音页 walk 13 比 tree 15 少 2**，`skippedNonTabStop=1` 只解释了其中"刻意排除"的一个，剩下 2 个 TabStop 控件在当前状态下不可达（推测为隐藏/禁用，但**未验证**——自检页那次"禁用"假设就是错的，所以这次不写结论）。此外六个导航键排在**页面内容之后**，对纯键盘用户不算最优（不是缺陷，是顺序偏好，未改）。

#### 读取该诊断时的注意点（已写入门禁与本节）

日志**跨运行累加**：只取最后一次的连续 6 行；`wrap: true` 是让"stops"有意义的前提。

## 2026-09-11 发布前工作（1/3）：把界面矩阵做成闸门

### 做了什么

新增 `scripts/check-ui-matrix.ps1`：**每个主题 × 每个窗口尺寸**启动一次宿主、走完六页、逐例断言"0 重叠 / 0 裁切 / 主题真的生效"，任一例失败即整体失败。

- 主题：`light` / `dark` / `system`；尺寸：默认 与 `1366x768`（用几何检查现成的 `-ForceSize`）。
- **主题从像素判定，不信标志位**：取首页截图的背景亮度判断实际是深色还是浅色；`system` 则先读注册表 `AppsUseLightTheme` 得出应然值再比对。理由：一个"声明在跑深色、其实在渲染浅色"的结果，正是这道闸门要防的那类假通过。
- **无桌面环境如实记为 skipped**，不失败也不静默通过（CI runner 若无交互桌面，会打印 `skipped (no desktop)`）。
- 宿主新增 `--ui-theme <light|dark|system>`：主题不再只能靠改配置文件才能切——**这正是深色主题当年能"启动即崩"还活了几个月的原因：没有任何自动运行碰过它**。

### 实测（本机，200% 缩放）

```
theme/size        pages  overlaps  clipped  theme      exit  result
light/default         6         0        0  light         0  ok
light/1366x768        6         0        0  light         0  ok
dark/default          6         0        0  dark          0  ok
dark/1366x768         6         0        0  dark          0  ok
system/default        6         0        0  dark          0  ok
system/1366x768       6         0        0  dark          0  ok
interface matrix: 6 case(s) passed
```

（本机 Windows 应用主题为深色，所以 `system` 判为 dark ✔ 符合跟随语义。）

### 接进闸门与 CI

- `BUILD_RELEASE.ps1` 在特性套件之后调用矩阵，失败即 `throw "Interface matrix failed."`；
- **CI 因此自动获得该闸门**——`.github/workflows/validate.yml` 的 "Build release" 步骤就是跑 `BUILD_RELEASE.ps1`。
- `validate.js` 已钉住：矩阵脚本必须存在、必须从像素判主题、必须有 skipped 分支、必须被发布链调用；检查脚本必须有 `-Theme` / `-ExeArguments`；宿主必须解析 `--ui-theme`。

### 顺带纠正我上一轮评估里的一处不准确

我在评估里把"安装升级卸载生命周期"列为未验证。**实际上它已被 CI 覆盖**：`validate.yml` 里有三步 `Test-ReleaseLifecycle.ps1`（干净安装+启动恢复+卸载、未配置的干净安装+卸载、V1.5 升级两次+卸载），在 `windows-latest` 上跑。所以那一项的真实状态是"**CI 已覆盖、本机/真机未跑**"，不是"没做"。

### 仍未做（本项内）

- **CI 上的矩阵未实测**：我无法从这里跑 GitHub runner，只能保证它接进了发布链。若runner 无交互桌面，会打印 skipped 而不是假通过（这一点已验证分支逻辑）。
- DPI 轴无法在 CI 上切换（runner 缩放固定），矩阵覆盖的是"主题 × 窗口尺寸"；DPI 轴靠本机 200% 下跑同一套走查来覆盖。

### 闸门第一次运行就抓到一个问题（这是它存在的意义）

接进发布链后第一次跑 `BUILD_RELEASE.ps1`，矩阵**直接失败**：

```
system/default         6         3        0  dark          1  FAILED
  [   常用按键] x [●]                        18x6px
  [   常用按键] x [录音]                     68x8px
  [   常用按键] x [按住听写 / 松开结束]      100x8px
```

深色下首页「常用按键」卡片标题与第一行相撞 6–8 px。**这是我在本机手动跑过多次都没看到的问题**——正是"闸门比人可靠"的例子。

#### 根因形状（已定位到代码）

`SectionTitle` 是 **AutoSize 标签**（高度由字体测量决定），放在 y=18；卡片的行从 **写死的 y=50** 开始 —— 标题正常高度约 32，**底边正好落在 50**，也就是**零余量**。一旦某次测量多出 6–8 px（冷字体缓存/回退字族都会影响），就必然压到行上。

#### 复现尝试与统计（诚实记录）

| 阶段 | 用例数 | 失败 |
| --- | --- | --- |
| 修复前（含那次链上运行） | 约 47 | **1** |
| 修复前专门复现（整矩阵 ×2） | 12 | 0 |
| 修复后（整矩阵 ×2） | 12 | 0 |

**所以：这是一个约 2% 的罕见事件，我没能复现它。**修复方式是把该卡片的行下移 10 px（> 观测到的 8 px 波动，且卡片 178 px 高的余量足够：行末 162）。**这是"针对已测波动的余量"，不是"已证明的根治"**——标题测量为什么会漂移，我没有查清，文档里就是这么写的。12 次干净运行**不足以证明**一个 ~2% 事件消失（要 95% 置信度排除它需要约 75 次干净运行），所以闸门保持严格：再犯就报出来，不会被掩盖。

#### 这条也顺带说明闸门的分工

- **能在 CI/发布链里跑的**：主题 × 窗口尺寸 × 六页走查（本轮）；
- **只能在有桌面、能改缩放的本机跑的**：DPI 轴（125/150/200%），因为 runner 的缩放固定。

## 2026-09-11 发布前工作（2/3）：崩溃记录 + 诊断导出

### 为什么做

深色主题那个 P0（**启动即崩**）活了几个月，根本原因是：**未处理异常在本机什么都没留下**——只有一条 Windows 错误报告（WER）和 .NET Runtime 事件日志，应用自己完全没有痕迹。**留下不了痕迹的崩溃，就会被打包发出去。**

### 做了什么

1. 新增 `scripts/features/CrashReports.cs`：把未处理异常写成报告（`%LOCALAPPDATA%\Vibe Flow Remote\UserData\crashes\crash-<时间戳>.log`），保留最新 5 份；写入过程自身出错一律吞掉（**崩溃处理器再抛异常比没有还糟**）。
2. 报告内容 = 异常 + **渲染环境**：接口字体与实际所用字族、屏幕/工作区/DPI/缩放、主题、**Windows 版本**、区域与显示语言、是否 64 位、完整类型/消息/堆栈、内部异常（深度上限 5）。
3. 两条处理路径都接上：`Application.ThreadException`（UI 线程：记录 + 告知用户文件位置 + **关闭应用**——录制会话可能正开着，继续在未知状态里跑更危险）与 `AppDomain.CurrentDomain.UnhandledException`（其它线程）。
4. 会话启动时若发现历史报告，日志写明 `CRASH PREVIOUS count=N newest=...` 并列出前 8 行。
5. 「导出诊断」里加入 `Crashes recorded: N (newest ...)` + 最新报告前 14 行。
6. **可验证性**：新增 `--crash-test`（仅在显式传参时生效）在 UI 线程真实抛异常；自测 `RunCrashReportSelfTests()` 用合成异常（含内部异常）、空异常、以及"超过上限必须被裁剪"三条断言覆盖写入器。

### 实测证据

**端到端**（`--ui-smoke --crash-test`）：进程按设计退出，留下报告，内容如下（节选）：

```
Source: ui_thread
App: 2.0.0-candidate
Windows: Windows 10 Pro 25H2 build 26200
Windows (reported): Microsoft Windows NT 6.2.9200.0
Culture: zh-CN / UI zh-CN
Interface: text_font=Microsoft YaHei UI text_installed=yes ... icon_font=Segoe MDL2 Assets icon_installed=yes ...
Display: screen=2560x1440 workarea=2560x1440 dpi=192 scale=2.00 monitors=1
Exception: System.InvalidOperationException
Message: crash handler verification (2.0.0)
Stack: ...
```

**启动点名**：有报告时启动，会话日志出现 `CRASH PREVIOUS ...` 系列行 ✔。**验证用的假报告已删除**（否则下次启动会把它当真实崩溃）。

> 一个值得记的坑：`Environment.OSVersion` 对没有 manifest 的进程在 Win10/11 上都报 `6.2.9200`，所以报告改为从注册表读 `ProductName`/`DisplayVersion`/`CurrentBuildNumber`。但**注册表里的 `ProductName` 在 Windows 11 上可能是 "Windows 10 Pro"**（本机实测：ProductName "Windows 10 Pro" + DisplayVersion 25H2 + build 26200 = Windows 11）——三个字段一起给出，避免读者被单个字段误导，代码注释里也写明了。

### 顺带修正了一个我自己造成的门禁冲突

我把 `RunCrashReportSelfTests()` 插在 `RunFavoriteAppSelfTests();` 与 `RunHomeLayoutSelfTests();` 之间，触发了既有门禁（它钉住这两句的相邻距离）。**做法是把调用挪到后面，而不是放宽门禁**——门禁报得对。

### 仍未做

- 「导出诊断」里那两行是**门禁钉住、但没有真正导出一次文件端到端验证**（导出走系统保存对话框，需要交互）；
- 崩溃报告里没有内存/线程数等更深的运行时信息（够用于定位，不追求穷尽）。

## 2026-09-11 一个发布级缺陷：**覆盖安装**时安装器报「无法迁移旧版配置」

这是本轮（崩溃记录）顺带炸出来的：装完崩溃记录后我第一次跑完整链路，安装器返回 **exit 5**。用 `/?` 之外最可靠的办法——**让安装器自己写日志**（`/LOG=`）——拿到了真相：

```
Installation process succeeded.
CurStepChanged raised an exception.
Runtime error (at 46:353):
无法迁移或保护旧版配置，安装未完成。请检查用户数据目录权限后重试。
```

**文件其实已经装好了**，是安装后那一步（`CurStepChanged` → `MigrateLegacyUserConfig`）失败了。

### 定位过程（这次是崩溃记录直接帮上忙）

`.iss` 里那一步是：

```pascal
Exec('{app}\VibeFlow.exe', '--installer-config-migrate ' + AddQuotes(LegacyConfigRoot) + ' ' + AddQuotes(UserDataDirectory), ...)
Result := ResultCode = 0;
```

而 `LegacyConfigRoot` 取自注册表的 `InstallLocation`，**末尾带反斜杠**。于是命令行是：

```
--installer-config-migrate "C:\...\Vibe Flow Remote\" "C:\...\UserData"
```

`\"` 在 Windows 命令行里被解析成**转义引号** → 应用收到的路径里带一个引号 → `Path.Combine` 抛
`System.ArgumentException: 路径中具有非法字符` ✔

**我是靠刚做好的崩溃记录拿到这一条的**——但第一次没拿到，因为我把它注册在 `Application.Run` 旁边，而安装器这些入口在 `Application.Run` **之前**就 return 了。**于是把注册移到 `Main` 最开头**（这些入口最需要它），第二次复现就拿到了带 `MigrateLegacyUserConfigForInstaller` 帧的报告：

```
System.ArgumentException: 路径中具有非法字符
  在 System.IO.Path.Combine(String path1, String path2)
  在 VibeMicForm.MigrateLegacyUserConfigForInstaller(String legacyRoot, String stateRoot)
```

### 为什么 CI 和我都漏了它

- **全新安装不受影响**：那时 `LegacyConfigRoot` 用应用目录（无尾分隔符）✔ —— 这正是它活下来的原因；
- 它在**覆盖安装/升级**时才出现，而本机我是 18:16、18:33 连续装了两次（第二次就中招了）；
- CI：**这个仓库的 CI 最后一次运行是 2026-09-03**（我通过 GitHub API 确认：最新 run #38，`main`，成功），而当前分支的提交**从未推送**（仓库规范禁止自动推送）——**今天（以及这 8 天）的所有改动都没有经过 CI**。所以"CI 已覆盖安装生命周期"这句安慰话，只对**推送过的**代码成立。

### 修复（两侧都改）

| 侧 | 改动 |
| --- | --- |
| 应用 | 新增 `NormalizeInstallerPath()`：去引号、去首尾空白、去掉除根目录外的尾分隔符；`--installer-config-migrate` 与 `--installer-config-startup-query` 两个入口都先清洗。理由：Windows 路径不可能含引号，清洗是安全的 |
| 安装器 | `ReadPreviousInstallDirectory` 末尾调用 `RemoveBackslashUnlessRoot(Result)`，不再产生尾分隔符 |

自测新增 `RunInstallerPathSelfTests()`：覆盖"引号+尾反斜杠""仅尾反斜杠""引号+两侧空白""仅引号"，以及**根目录 `C:\` 必须保留分隔符**（去掉会变成 `C:`，含义不同）。

> 顺带一个 Inno 的坑：我在 `.iss` 注释里写了 `{app}`，Inno 会把它当常量展开/嵌套注释，直接**编译失败**（`Error on line 158 ... Identifier expected`）。注释里不要出现 `{`。

### 验证（端到端）

```
安装器退出码 = 0
安装日志中「无法迁移 / Runtime error / CurStepChanged raised」：没有再出现 ✔
迁移崩溃崩溃报告数 = 0
安装后应用启动正常，Capture 哈希未变
```
另外用 `cmd /c` 按安装器真实形状复现三种参数（无引号/有引号、有/无尾斜杠）**全部返回 0**。
（中途一次"修复后仍失败"是我自己的测试姿势问题：`Start-Process -ArgumentList` 会自己加引号，我又预先加了引号 → 双重引号 ✗ 用 `cmd /c` 复现才是忠实的。）

## 2026-09-11 发布前工作（3/3）：对话框与小屏窗口逐个复核（进行中）

### 小屏窗口：已进闸门（3 主题 × 4 尺寸 = 12 例全过）

矩阵的尺寸轴从 2 档扩到 4 档：默认、**1366×768**、**1920×1080**、**880×500**（100% 缩放下应用允许的最小窗口），每个主题都跑一遍：

```
light/default   light/1366x768   light/1920x1080   light/880x500     全部 6 页 0 重叠 0 裁切
dark/…          同上                                                  全部 ok
system/…        同上（system 判为 dark，符合本机 Windows 设置）          全部 ok
interface matrix: 12 case(s) passed
```

说明：**缩放轴无法在 CI runner 上切换**，所以尺寸轴在任意缩放的机器上跑；DPI 轴靠本机 200% 下跑同一套走查覆盖。

### 对话框：新增 3 个实测（合计已实测 5 个独立对话框）

方法：临时驱动脚本（不入库——驱动是应用专属且易碎的，测量能力留在 `check-ui-geometry.ps1 -WindowTitle`）按页面导航 → 点击开启按钮 → **列出该进程新出现的可见顶层窗口**（不猜标题）→ 逐个用几何检查测量 → 关闭。

| 对话框 | 实测尺寸 | 设计尺寸 | 结论 |
| --- | --- | --- | --- |
| 重命名快捷键 Profile（内联） | 1052×398 | 526×199 | **×2**，0 重叠 / 0 裁切 ✔ |
| **Browser Remote Lite**（`scripts/ui` 类） | 2536×1416 | 1268×708 | **×2**，0 重叠 / 0 裁切 ✔（并已截图确认渲染正确） |
| 添加应用（AppPickerDialog，`scripts/ui` 类） | 1160×1320 | 580×660 | **×2**，0 重叠 / 0 裁切 ✔（回归） |
| 调整高级音频参数（内联） | 969×374 | 484×187 | **×2**，0 重叠 / 0 裁切 ✔ |

**关键含义**：这些"构造函数里就建好内容"的对话框，窗口尺寸**恰好是设计值的 2 倍**——既不是 1 倍（没缩放）也不是 4 倍（缩放两次）。重复缩放这个风险在两种家族上都排除了。

### 过程中的三次自我纠错（都记下来）

1. **负对照被污染**：我最初用"绝对窗口列表"判断某个用例是否开了窗口，于是「设置页（不应开窗）」用例误报"开了窗口"——其实是**上一个对话框还没关完**。改成**相对比较**（点击前的窗口快照 vs 点击后）后，负对照通过：`settings-none: no window opened` ✔ 这样"出现了窗口"才有意义。
2. **码点写错**：「浏览器遥控」我把 `遥`(0x9065) 写成了 `远`(0x8FDC) → 找不到按钮。
3. **精确匹配太严**：工作流页按钮实际是「＋ 添加应用」（全角加号），精确匹配静默找不到 → 改为先精确后子串。

### 仍未实测的对话框（如实列出原因）

| 对话框 | 为什么没测到 |
| --- | --- |
| 「配置 …」动作选择器 / 录制键盘快捷键 / 用语片段 | 由手势行的 **▶ 打开一个下拉菜单**，菜单项再开对话框；我的枚举把无标题的下拉菜单跳过了，没有继续点菜单项 |
| 编辑常用应用 | 需要先存在一个"常用应用"；smoke 状态下为 0 个，流程要更长 |
| 删除 Profile | 会先弹确认框（并会改动状态），本轮未做 |
| Live HUD / Context Deck / Capture & Ask | 需要真实录音或托盘触发，本机无条件 |
| 保存/导入/导出、打开 EXE、打开网页 | 系统文件/浏览器对话框，非本应用窗口 |

**注意**：「动作选择器」如果其实是**页面内嵌面板**而非独立窗口，那么它已经被页面走查覆盖了（六页扫描会遍历页内控件）。这一点我没有验证到，因此不下结论。

### 补记：安装器 exit 5 的两个**不同**原因（第二个是我自己的进程卫生问题）

今天我一共看到三次安装器 exit 5。查安装器自己的日志（`/LOG=`）后确认是**两个不同原因**：

**原因一（产品缺陷，已修）**：`CurStepChanged` → `MigrateLegacyUserConfig` 抛异常 →「无法迁移或保护旧版配置」。
→ 即"引号 + 尾反斜杠"那个路径 bug（见上一节）。修好后日志里不再出现。

**原因二（不是产品缺陷，是我的操作/工具问题）**：

```
RestartManager found an application using one of our files: Vibe Flow RC003 voice capture
Some applications could not be shut down.
Defaulting to Abort for suppressed message box (Abort/Retry/Ignore):
  安装程序无法自动关闭所有应用程序。…
User canceled the installation process.
Rolling back changes.
```

**冻结的采集进程 `VibeMicAtvvCapture.exe` 还在运行** → 安装器请 RestartManager 关掉占用文件的程序，关不掉 → 静默模式下 Abort/Retry/Ignore 默认 **Abort** → 回滚 → **exit 5**。
**安装器的行为是正确的**（它拒绝强杀进程）；错的是我：

1. **我的矩阵脚本泄漏了这个进程**——它在每个用例**之前**清理残留，却没有在最后一个用例**之后**清理 ✗ → 已修：抽出 `Stop-SmokeLeftovers()`（含 `VibeMicAtvvCapture`），开头、每个用例前、以及 `finally` 都调用；**实测矩阵结束后残留进程 = 0** ✔ 并已进门禁。
2. 我在安装前只停了 `VibeFlow`，没停它启动的 worker（capture/bridge）→ 后来停全四类进程后：**exit 0、日志干净、安装目录与发布目录 51/51 逐文件哈希一致** ✔

**教训（写给未来的自己）**：安装器失败时**先读它自己的 `/LOG=` 日志**，不要凭"应用是不是开着"猜——我今天就是因为第一反应猜错、第二反应又推翻自己，浪费了两轮。同时：**"exit 5"不等于一个原因**。

### 3/3 追记：动作选择器链路的两个对话框已实测（累计 8 个）

上一轮遗留的问题——「动作选择器究竟是独立窗口还是页内面板」——本轮有了确定答案，而且是**两次走错才走对**：

| 我试的 | 实测结果 | 结论 |
| --- | --- | --- |
| 点手势行的 **▶** | 主窗口内**新增 3 个控件**（含一行「正在验证"上键短按"的当前功能…」）、**没有新窗口** | ▶ 是「测试这个动作」按钮，产生的是**页内状态行**，不是选择器 |
| 点 **行的动作按钮**（显示当前动作的那个，例如「上方向」） | 打开窗口 **「配置 上键短按」** | 这才是动作选择器 ✔ |

（方法说明：菜单项/列表项不是窗口，UIA 在本机又看不到控件类型，键盘焦点也拿不到——所以**"按 HWND 点击"和"枚举新窗口"这两件事本身就是判据**：页内面板会让控件数增加而不产生窗口，独立对话框则相反。）

#### 新实测（200% 缩放，物理像素）

| 对话框 | 实测尺寸 | 设计尺寸 | 结论 |
| --- | --- | --- | --- |
| 配置 上键短按（动作选择器） | 1252×1354 | 626×677 | **×2**，0 重叠 / 0 裁切 ✔ |
| 录制键盘快捷键（由选择器里的 `⌨ 录制键盘快捷键` 按钮进入） | 1252×782 | 626×391 | **×2**，0 重叠 / 0 裁切 ✔ |

选择器内部可见控件：`选择要执行的动作`、`支持应用、网页、编辑、系统、媒体与自定义快捷键`、**`⌨ 录制键盘快捷键`（按钮）**、`按下组合键即可记录…`、`搜索动作`、`选择`、`取消`。

#### 本项目前累计已实测 8 个对话框（都是恰好 2× 设计尺寸 + 0 重叠 + 0 裁切）

绑定 Smart Profile 应用 · 新建快捷键 Profile · 重命名快捷键 Profile · 添加应用（AppPicker） · Browser Remote Lite · 调整高级音频参数 · **配置 上键短按** · **录制键盘快捷键**

#### 仍未测到（附**具体**原因，不是"没时间"）

- **用语片段 · 管理…**：它是选择器**列表里的一项**。列表项不是窗口（我的控件清单按"有文字"过滤，列表本身没文字所以没被列出），要选中它需要 `LB_SETCURSEL` + 点「选择」——我写这套链路时连续在脚本拼接上出错，**本轮到此处停手**（与其继续手改脚本，不如记清楚下一步怎么做）。
- 删除 Profile（会先弹确认框并改动状态）、编辑常用应用（需先存在已保存的常用应用）。
- Live HUD / Context Deck / Capture & Ask（需真实录音或托盘触发）。

**下一步（写清楚以便接续）**：在 `vibe-picker-chain.ps1` 里找 class 含 `LISTBOX` 的控件 → `LB_GETCOUNT(0x018B)` 读条目数 → `LB_SETCURSEL(0x0186, count-1)` 选中最后一项 → 点「选择」→ 测量新窗口。

### 3/3 再追记：用语片段（片段管理器）已实测（累计 **9** 个对话框）

本轮把上一轮记下的"下一步命令序列"真正跑通了，而且过程中**四次尝试、前三次都失败**，每次原因都不同——记下来，因为下一次遇到同类控件能省一轮：

| 尝试 | 结果 | 失败原因 |
| --- | --- | --- |
| ① `LB_SETCURSEL(30)` + 点「选择」 | 选择器关闭，但**什么都没打开** | 程序化设置选中项**不触发**对话框依赖的选择变更通知 |
| ② 手工补发 `WM_COMMAND` | 同上 | **lParam 用错**：来自控件的 `WM_COMMAND`，`lParam` 必须是**控件句柄**，通知码放在 **wParam 高字**——我放反了，对话框收到一个畸形通知 |
| ③ 用真实鼠标消息点列表项 | 选中索引回到 **0** | 点击坐标落在可视区之外（列表 31 项、200% 下很高），点到了别的项 |
| ④ **向列表框投递真实方向键**（`VK_HOME` + `VK_DOWN`×30） | **成功** ✔ 打开窗口「用语片段」 | ——（与下拉菜单那次同一个技巧：控件自己处理按键并发出真实通知） |

**实测**：`用语片段` 1292×1142（设计 646×571）→ **恰好 ×2**，**0 重叠 / 0 裁切** ✔
（同一次运行里主窗口也测了：0/0 ✔）

顺带把三个事实变成了证据：确认后**选择器关闭**、**主窗口控件数 151→152**、并出现标题为「用语片段」的**独立窗口**——所以片段管理器是窗口（与 ▶ 那种页内状态行不同）。

#### item 3 对话框累计：9 个已实测（全部恰好 2× 设计尺寸、0 重叠、0 裁切）

绑定 Smart Profile 应用 · 新建快捷键 Profile · 重命名快捷键 Profile · 添加应用 · Browser Remote Lite · 调整高级音频参数 · 配置 上键短按（动作选择器） · 录制键盘快捷键 · **用语片段（片段管理器）**

#### 仍未测到（3 个可自动化 + 3 个需硬件）

- **删除 Profile**：点「删除」会先弹确认框（且会改动状态）——本轮未做，下一步可直接用同一套"发现新窗口"流程。
- **编辑常用应用**：需要先存在一个已保存的常用应用（smoke 状态下为 0 个），需要更长的前置流程。
- **动作选择器里其余 30 个选项**（打开网页…、浏览其他 EXE…、选择本机应用…）：会打开系统对话框或提示输入，属另一类（非本应用窗口）。
- **Live HUD / Context Deck / Capture & Ask**：需要真实录音或托盘触发，本机无条件。

### 3/3 再追记：编辑常用应用已实测（累计 **10** 个对话框）

这个对话框只有在"已经添加过常用应用"之后才够得到，而 smoke 状态里**一个都没有**。**我选择先播种状态，而不是以"fixture 是空的"为由把它跳过**：

1. 按存储自己的格式写一份 `tmp\ui-smoke\favorite-apps.json`（`schemaVersion:1`、一条 `notepad`/记事本、`mode:"workflow"`）；
2. 启动 smoke → 工作流页 → 点行上的「编辑」；
3. 测量弹出的窗口；
4. **finally 里把播种的文件删掉**（并打印确认），smoke 状态恢复原样。

**实测**：`编辑常用应用` **1252×962**（设计 626×481）→ **恰好 ×2**，**0 重叠 / 0 裁切** ✔

顺带（同一轮证据）：播种后工作流页的行内按钮为 `学习 / 已是当前 / 重新学习 / 编辑 / 删除`，并显示 `✓ 当前`、`还没学习过 · 点「学习」开始`——这也说明该页面在有数据时的形态。

#### item 3 累计：10 个对话框已实测（全部恰好 2× 设计尺寸、0 重叠、0 裁切）

绑定 Smart Profile 应用 · 新建快捷键 Profile · 重命名快捷键 Profile · 添加应用 · Browser Remote Lite · 调整高级音频参数 · 配置 上键短按（动作选择器） · 录制键盘快捷键 · 用语片段（片段管理器） · **编辑常用应用**

#### 仍未测到

- **删除常用应用 / 删除 Profile 的确认框**：会改动状态，需要单独一轮"发现新窗口"的流程（下一步可做，成本相同）；
- **Live HUD / Context Deck / Capture & Ask**：需要真实录音或托盘触发，本机无条件；
- 动作选择器里**打开网页… / 浏览其他 EXE… / 选择本机应用…**：会转成系统对话框或提示输入，属另一类窗口。

# Vibe Flow V2.0 基线锁定

> **范围提示（2026-09-08 起）：** 本基线档案用于当前 Host 的稳定语音、快捷键、输入目标和自检。此前 Notes Deck/便签基线仅作历史审计，不是当前产品或发布依据。

更新日期：2026-09-03  
阶段：0（基线与保护）  
开发分支：`feature/v2-off-key-loop`

## 1. Checkout 身份

- 本轮启动时，工作目录只有未跟踪的 `AGENTS.md`、`.agents/`、`.codex/`，`master` 没有提交、远端或可用 `HEAD`。
- 按任务中明确给出的仓库地址恢复 `origin/main`，没有覆盖上述三项用户/项目规则文件。
- 当前基线 commit：`b47f7cdce8b753fade0c64c97332bebe80f17d2d`。
- 最近提交：
  - `b47f7cd docs: feature community banner at top of homepage`
  - `2643a55 fix(ci): bound legacy upgrade lifecycle test`
  - `d0bd39a chore(release): normalize published source tree`
  - `7244e81 chore(release): add public trust and support gates`
  - `e349c09 docs(v1.5): improve release showcase and community`
- 阶段 0 开始前唯一未提交内容是未跟踪的 `AGENTS.md`、`.agents/`、`.codex/`；这些文件不是本轮生成，必须保留。

## 2. 本轮实际加载的项目指令

| 文件 | 状态 | 用途 |
| --- | --- | --- |
| `AGENTS.md` | 已完整读取 | 仓库级稳定录音、输入、配置、UI、向导和完成门禁 |
| `.agents/skills/vibeflow-release-gate/SKILL.md` | 已完整读取 | 每阶段 release gate |
| `.codex/agents/vibeflow-auditor.toml` | 已完整读取 | 只读回归 auditor 定义 |

仓库内没有发现其他 `AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 或 `.github/copilot-instructions.md`。

## 3. 发布身份

| 项目 | 锁定值 | 真实证据 |
| --- | --- | --- |
| V1.5 稳定基线产品版本 | `1.5.0` | `VIBE_MIC_VERSION.md` 的冻结记录 |
| 当前 V2.0 候选产品版本 | `2.0.0-candidate` | `package.json`、当前 Host VersionInfo |
| V1.5 稳定基线 Host / Bridge 版本 | `1.5.0.0` | 冻结记录与 V1.5 构建证据 |
| 当前 V2.0 候选 Host / Bridge 版本 | `2.0.0.0` | 当前根目录与 `release/` 候选构建产物 VersionInfo |
| Capture 文件版本 | `1.2.1.0` | `scripts/VibeMicAtvvCapture.cs`，本机冻结二进制 VersionInfo |
| Host 配置 schema | `32` | `VibeMic.cs`、默认配置 |
| Bridge 配置 schema | `7` | `BuildKeyboardBridgeDocument()`、`BridgeConfig.Default()` |
| Onboarding version | `9` | `VibeMic.cs`、默认配置 |
| Onboarding 任务数 | `5` | `OnboardingStepCount`、活跃向导 |
| Stable voice profile | `v11` | Host/Capture 运行标识 |
| Recording kernel | `v1.0.3` | `VibeMicAtvvCapture.cs` |

阶段 8 之前不统一提升非 Capture 版本。Capture 必须继续保持 `1.2.1.0` 和相同二进制身份。

## 4. Capture 身份

| 对象 | SHA-256 | 阶段 0 结果 |
| --- | --- | --- |
| `scripts/VibeMicAtvvCapture.cs` | `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2` | 实际计算一致；相对 HEAD 无差异 |
| 冻结 `VibeMicAtvvCapture.exe` | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` | `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe` 实物计算一致；文件版本 `1.2.1.0`；`--self-test` 退出 0 |

说明：冻结 EXE 不在 Git checkout 中，`*.exe` 和 `release/` 被忽略。正式构建只能通过 `scripts/Get-StableCaptureBinary.ps1` 解析并验证固定 v1.2.1 二进制；不得运行 `BUILD_VIBE_MIC_CAPTURE.cmd` 生成正式 Capture，不得重签 Capture。

## 5. 冻结录音参数

| 参数 | 锁定值 |
| --- | --- |
| `voiceMode` | `hold` |
| `captureSeconds` | `0` |
| `gain` | `1.0` |
| `autoLevel` | `true` |
| `audioProcessingMode` | `speech` |
| `drainMs` | `180` |
| `autoRouteVirtualMicrophone` | `true` |
| 播放端点 | `CABLE Input` |
| 语音工具录音端点 | `CABLE Output` |
| 微信输入法 | `ctrl+win` / `toggle` / `80 ms` |
| RC003 单段边界 | 设备自然控制，约 60 秒 |

真实 V1.5 例外：语音页允许用户明确解锁高级排障参数，合法的既有偏离值不会被迁移强制覆盖。V2.0 不得扩大这个入口，也不得用迁移静默覆盖旧用户值；普通路径继续展示并推荐上表稳定档。

## 6. 冻结录音执行路径

```text
VibeMic.Main
  -> LoadConfig / MigrateConfig
  -> SyncKeyboardBridgeConfig
  -> StartCapture
  -> 等待 Bridge revision ACK
  -> 附着或启动同目录冻结 Capture

RC003 录音输入
  -> 已验证设备过滤器，或带设备身份的 Raw Input（过滤器不可用时的用户态回退）
  -> HandleVoicePhysicalTransition（录音专用，不进普通 mappingQueue）
  -> Local\VibeMicVoiceKeyHeld / Local\VibeMicVoiceKeyPressed
  -> Capture BLE ATVV control 0x04
  -> 单一 stream generation
  -> 真实音频到达后 AUDIO LIVE START
  -> RC003 自然 control 0x00
  -> generation 校验、80 ms 尾包等待、180 ms 虚拟端排空
  -> 第三方语音工具结束指令、可逆音频路由恢复
```

冻结行为：

- 按住开始、松开结束；不支持 click-to-toggle。
- 活跃 generation 重复 start 忽略；有效 stop 只结束一次；旧 stop 不得结束新 generation。
- `captureSeconds = 0` 不建立 Host 定时停止；约 60 秒来自设备自然边界。
- 不存在 `MIC_EXTEND`、自动续接、第二套 Capture 状态机、Clipboard 转写回填或自动 Enter。
- `VibeMicVoiceKeyReleased` 当前没有 Host/Capture 消费者；不得把它接成强制关流绕过自然 stream-stop。
- 录音键由 Host 固定生成 Bridge `voice` 描述并走专用 transition；普通用户 mappings、Smart Profile 动作表、Deck、Focus、Project Space 和 Capture & Ask 都不能接管它。

## 7. 冻结输入参数与边界

| 常量 | 锁定值 |
| --- | ---: |
| `DEFAULT_LONG_PRESS_MS` | 650 |
| `HOLD_REPEAT_INITIAL_DELAY_MS` | 420 |
| `HOLD_REPEAT_INTERVAL_MS` | 80 |
| `VOICE_RESTART_GUARD_MS` | 500 |
| `SMART_PROFILE_POLL_MS` | 250 |
| `SMART_PROFILE_DEBOUNCE_MS` | 350 |

当前稳定实现仍包含全局 `WH_KEYBOARD_LL` Hook、Raw Input 和可选设备过滤器。低级 Hook 没有设备身份，普通映射候选必须直通；录音映射也不得从 `keyboard_hook` 进入录音状态机。只有带设备身份的 RC003 Raw Input 或 `rc003_filter` 事件可以调用既有录音 transition；这样才能保证普通键盘 F5 不会启动录音。无签名设备过滤器时，用户态不能承诺完全消除遥控器原始系统按键副作用；V2 UI 工作不得扩大 Hook 拦截范围。

## 8. 默认按键与 Profiles

| 实体键 | 默认动作 |
| --- | --- |
| 录音键 | 固定 hold-to-talk；不可配置 |
| 上 / 下 / 左 / 右 | `up` / `down` / `left` / `right` |
| 确认键 | `enter` |
| Home 短按 | `win+d` |
| Home 长按 | `none` |
| TV | `task-switcher` |
| 功能键短按 | `ctrl+c` |
| 功能键长按 | `ctrl+v` |

内置 Profile 为 `general`、`vibe-coding`、`browser-ai`、`terminal-agent`。Smart Profiles 默认关闭，锁定默认关闭，fallback 为 `general`。Power、Back、独立音量键没有稳定 Windows 事件，不能宣传支持。

## 9. 配置与 ACK 基线

- 正常用户配置：`%LOCALAPPDATA%\Vibe Flow Remote\UserData\vibe-mic-config.json`。
- 仅当中央配置不存在时，才从程序目录、旧安装目录或旧启动目录迁移 config/bak。
- `LoadConfig()` 支持迁移、备份恢复和默认回退；Host self-test 覆盖多版本迁移及二次迁移幂等。
- 保存使用同目录 `.tmp`、`File.Replace`/`File.Move` 和 `.bak`。
- Host 生成 Bridge 快照 SHA-256 revision；Bridge 通过 health 文件 ACK revision。
- 风险：通用 `SaveConfig()` 在 `SyncKeyboardBridgeConfig()` 返回空 revision 时仍可能返回 `true`。V2 新代码不得把 `SaveConfig() == true` 当作运行时已生效，必须单独等待并展示 ACK 结果。
- 风险：当前封闭 `VibeMicConfig` 反序列化后再保存会丢弃未知顶层字段。V2 独立配置文件必须显式保留未知字段但不执行。

## 10. 首次设置与自检基线

唯一可达向导是 `ShowSetupWizard()`：

1. 确认设备与用法
2. 连接并测试遥控器
3. 准备本地音频通道
4. 选择工具并完成听写
5. 开机即用

`ShowSetupWizardElevenStepLegacy()` 和 `ShowSetupWizardLegacy()` 只有定义、没有调用点。阶段 7 删除前仍需可达性测试和回滚证据。

活跃自检共 10 项：`components`、`bluetooth`、`remote`、`keys`、`microphone`、`cable`、`profile`、`provider`、`startup`、`session`。V2 只能增加场景能力检查，不能弱化这 10 项。

## 11. UI 与构建基线

- 技术栈：.NET Framework 4.x `csc.exe` + WinForms + System.Drawing + Win32 P/Invoke。
- `scripts/VibeMic.cs` 约 14k 行，UI、配置、服务控制和自绘控件仍在单文件。
- 当前导航：`首页 / 快捷键 / 语音 / 自检 / 设置`。
- 外层使用 Dock；页面内部仍大量使用绝对 `Location`/`Size`，内容区固定 `AutoScrollMinSize = 1000 x 744`。
- 已有 Per-Monitor-V2 尝试、`AutoScaleMode.Dpi` 和 `--ui-resource-test`（300 次页面切换资源压力）。
- `BUILD_VIBE_MIC.cmd` 当前只编译 `scripts/VibeMic.cs`；模块化时必须显式加入新 `.cs` 并更新静态验证。
- 基线锁定时 `npm start` 曾指向不存在的 `scripts/open-ui.ps1`；最终候选已将其改为转发 `START_VIBE_FLOW.cmd`。构建目录入口为 `VibeMic.exe`，安装包入口为 `VibeFlow.exe`。
- 当前安装器只有标准 Inno Setup modern wizard，没有 V2.0 要求的自定义欢迎、检查和分阶段文案。

当前截图锚点：

| 截图 | 尺寸 | SHA-256 |
| --- | --- | --- |
| `docs/images/01-overview.png` | 1280 x 840 | `D00588A060ED3D486093E118C8B082EAFC29592BDEDBBA1BA9D40EC39F331FD1` |
| `docs/images/03-shortcuts.png` | 1280 x 840 | `3125477C4995EEF5D7617C1BAC25E7FB5D489442DCFA14C52CD593D26B509429` |
| `docs/images/04-diagnostics.png` | 1280 x 840 | `5296986636AABC865A315315BC23521ED1136284871CCDD248871B2C878EB2C1` |
| `docs/images/05-settings.png` | 1280 x 840 | `8CF9A02883E1EE9955D9DDEC21B19E0D0EDB0326050FF9A9CB1BB7AD817C8142` |

Computer Use 首次检查 `VibeMic.exe --ui-smoke` 首页时，后续导航检测到用户并发输入且窗口被最小化，因此安全停止，没有抢回焦点。改动后重新构建并启动独立 smoke 实例，已实际巡视 1280 x 840 的首页、快捷键、语音、自检和设置五页；没有点击配置、修复或硬件测试动作。首页“松开结束录音”和快捷键页录音键“固定稳定链路”已实际可见。自检如实显示本机另一个安装目录实例及本目录 Bridge 未运行导致的 2 项错误，没有伪造成功。动态录音结束状态、首次设置和 100–200% DPI 仍未检查。

## 12. 改动前测试证据

| 命令 | 结果 |
| --- | --- |
| `npm test` | PASS；`Vibe Flow V1.5.0 release validation passed.` |
| `cmd /c BUILD_INPUT_BRIDGE.cmd` | PASS |
| `cmd /c BUILD_VIBE_MIC.cmd` | PASS |
| `VibeMic.exe --self-test` | exit 0 |
| `VoxDeckInputBridge.exe --self-test` | exit 0 |
| `%TEMP%\VibeFlow-StableCapture-v1.2.1.exe --self-test` | exit 0 |
| `VibeMic.exe --ui-resource-test` | exit 0 |

正式 `BUILD_RELEASE.ps1` 未运行：当前 NAudio build dependencies 不在工作区，脚本会恢复依赖；安装新依赖需要先获得用户批准。安装/升级/卸载生命周期也未在本轮执行。

## 13. 阶段 0 发现并处理的冲突

- 既有 Host 把 `REMOTE STREAM STOP` 和音频派发结果显示为“正在整理并回填文字 / 听写已完成 / 文字已写入”。程序没有第三方最终文字回执，属于虚假成功。
- 阶段 0 用失败测试复现后，只修改 Host 外围反馈为“录音已结束 / 等待语音工具处理 / 最终文字请目视确认”；没有修改 Capture、Bridge、事件或参数。
- README 首段的“松开完成转译”也由独立失败门禁捕获并改成“松开结束录音并等待语音工具处理”。
- Host 中仍保留 `UsesLongDictation() == false` 保护下的不可达历史分支。它不是当前 Capture 第二状态机；没有调用链与回滚证据前不删除。

## 14. 必须标记未验证的真机项目

- Windows 10 / 11 干净机安装、升级、卸载。
- RC003 100 次按住/松开，无重复 generation。
- 10 秒、30 秒、接近 60 秒真实音频。
- 蓝牙晚启动、休眠、唤醒、断连、重连。
- Windows 睡眠、唤醒、锁屏、解锁。
- `CABLE Input` / `CABLE Output` 真实路由与可逆恢复。
- 微信输入法及至少一个其他语音工具端到端。
- 普通物理键盘 F5 与 RC003 设备隔离。
- 100%、125%、150%、200% DPI 的完整页面巡视。

自动测试或源码判断不能把上述项目写成“通过”。

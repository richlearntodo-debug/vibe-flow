# 言灵 · Vibe Flow Remote

让小米 RC003 / MI RC 蓝牙遥控器成为 Windows 的语音输入入口。

## 最新正式版 · v1.0.3

**发布日期：2026-08-25 · 适用于 Windows 10/11 x64**

| 你需要的内容 | 直接入口 |
| --- | --- |
| 推荐安装版 | [**下载 VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/VibeFlow-Setup.exe) |
| 免安装版 | [下载 Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/Vibe-Flow-Windows-x64.zip) |
| 零基础教程 | [打开完整中文使用教程](docs/USER_GUIDE_ZH.md) |
| 本版说明 | [查看 v1.0.3 更新与已知边界](docs/RELEASE_NOTES_ZH.md) |
| 下载校验 | [下载 SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/SHA256SUMS.txt) |

> [!IMPORTANT]
> 普通用户只需要下载 `VibeFlow-Setup.exe`。请不要下载 GitHub 发布页底部自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`，它们是源码，不是 Windows 安装程序。

[查看最新版发布页](https://github.com/richlearntodo-debug/vibe-flow/releases/latest) · [首次安装](docs/USER_GUIDE_ZH.md#下载与安装) · [升级旧版本](docs/USER_GUIDE_ZH.md#下载与安装) · [问题排查](docs/USER_GUIDE_ZH.md#常见问题)

![言灵 v1.0.3 总览](docs/images/01-overview.png)

## v1.0.3 更新了什么

| 更新方向 | 用户能感受到的变化 |
| --- | --- |
| 开机后首次录音 | 修复蓝牙语音服务或转写工具尚未就绪时，第一次按录音键没有反应的问题。 |
| 后台稳定性 | 自动恢复缺失或停滞的采集服务，并预热微信输入法、Typeless 或 Voquill。 |
| 录音安全状态 | 只有录音键仍被按住时才恢复早到的请求，松开后不会延迟启动或播放旧音频。 |
| 首次使用 | 新增五步中文向导、开机启动授权、每一步检测和真实遥控器听写验收。 |
| 自检与日志 | 新增 7 项本地自检、直达修复按钮、问题摘要和开机恢复日志。 |
| 界面与遥控器 | 首页和快捷方式页按真实 RC003 的比例、按键位置和操作流程重新设计。 |

本次更新没有改变已经反复真机验证的稳定语音档案：状态机 v11、`1.0x` 灵敏度、清晰增强、180 ms 音频排空、自动默认麦克风路由和现有按键映射均保持不变。

## 正式版时间线

| 发布日期 | 版本 | 主要内容 | 发布记录 |
| --- | --- | --- | --- |
| 2026-08-25 | `v1.0.3` | 修复开机后首次录音，增加后台恢复与预热；重做首页遥控器、快捷方式页和五步截图 | [查看发布](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.3) |
| 2026-08-24 | `v1.0.2` | 强化安装包下载入口、升级保护、统一教程与发布说明 | [查看发布](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.2) |
| 2026-08-24 | `v1.0.1` | 完善转写工具配置、症状式排查教程和完整发布截图 | [查看发布](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.1) |
| 2026-08-24 | `v1.0.0` | 首个 Windows 正式版，提供安装版、免安装版和稳定语音档案 v11 | [查看发布](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.0) |

Alpha 版本和完整技术变更记录见 [CHANGELOG.md](CHANGELOG.md)。普通用户始终建议从 [最新版发布页](https://github.com/richlearntodo-debug/vibe-flow/releases/latest) 下载，不要安装历史版本。

## 现在可以做什么

- **遥控器听写**：按住录音键说话，声音从 RC003 麦克风实时进入所选语音转文字工具，松开后等待文字回填。
- **多种转写工具**：支持微信输入法、Typeless、Windows 语音输入、Voquill，以及可配置全局快捷键的其他工具。
- **Vibe Coding 快捷操作**：确认键发送，Home 显示桌面，TV 打开任务切换器，功能键打开或切回所选 Agent 或开发工具。
- **导航和音量**：方向键短按保持原生导航，长按上 / 下调节 Windows 系统音量。
- **一键自检**：检查程序组件、VB-CABLE、稳定参数、后台服务、RC003、转写工具和最近一次真实听写。
- **本地隐私保护**：普通听写不保存录音、不读取识别文字、不自行上传音频；一次性诊断录音必须由用户明确确认。
- **开机自动就绪**：用户可以在首次设置中选择登录 Windows 后自动启动，后台服务会按正确顺序恢复连接。

```text
RC003 遥控器麦克风
  -> Bluetooth ATVV
  -> 言灵 Vibe Flow Remote
  -> VB-CABLE
  -> 用户选择的语音转文字工具
  -> 当前输入框
```

## 零基础五步开始

开始前只需要准备四样东西：Windows 10/11 x64 电脑、RC003 / MI RC 遥控器、一个语音转文字工具，以及免费的 VB-CABLE 本地音频驱动。

1. 下载上方推荐的 `VibeFlow-Setup.exe`，双击完成安装并打开言灵。
2. 在首次向导选择微信输入法、Typeless、Windows 语音输入或其他常用工具。
3. 按页面安装 VB-CABLE，重启 Windows，确认 `CABLE Input` 和 `CABLE Output` 都已检测到。
4. 在 Windows 蓝牙中配对 RC003，回到言灵核对快捷键并完成启动测试。
5. 点击向导输入框，按住遥控器录音键说一句话，松开后看到绿色成功反馈即可完成。

![言灵首次五步设置](docs/images/00-first-run.png)

VB-CABLE 是当前版本唯一必须额外安装的本地驱动，发布包不会捆绑它。完整教程已经写明下载、解压、管理员安装、重启和检测方法：[从零开始安装 VB-CABLE](docs/USER_GUIDE_ZH.md#第二步安装本地音频通道)。言灵向 `CABLE Input` 播放音频，并在听写期间自动让转写工具使用对应的 `CABLE Output`；普通用户不需要长期更改 Windows 默认麦克风。

## 支持的转写工具

| 工具 | 默认启动方式 | 当前状态 |
| --- | --- | --- |
| 微信输入法 | 工具栏优先，`Ctrl + Win` 回退 | 已完成 RC003 端到端真机验证 |
| Typeless | 轻触 `Right Alt` 开始 / 结束 | 已完成 RC003 端到端真机验证 |
| Windows 语音输入 | 轻触 `Win + H` 开始 / 结束 | 已接入 Windows 系统快捷键路径 |
| Voquill | 按住 `Ctrl + Win`，松开结束 | 已接入其当前开源默认热键，需按客户端版本核对 |
| 其他语音工具 | 用户配置快捷键与单击 / 按住模式 | 支持通用全局快捷键路径 |

Typeless、Voquill 和其他第三方客户端不包含在发布包中，其账号、网络、识别和隐私策略由对应软件负责。

![言灵支持的转写工具](docs/images/06-transcription-tools.png)

## 已验证按键

| 遥控器按键 | 默认操作 |
| --- | --- |
| 录音 | 按住说话，松开后等待文字回填 |
| 确认 | `Enter`，用于确认、发送或换行 |
| Home | 显示桌面 `Win + D` |
| TV | 打开任务切换器，左右选择，确认进入 |
| 功能键 | 默认打开或切回 ChatGPT 客户端，也可选择 DeepSeek、Claude、Cursor、VS Code、Windsurf 或其他操作 |
| 方向键 | 短按原生方向；长按上 / 下调整系统音量 |

RC003 的独立返回键和音量 +/- 键在已验证的 Windows 蓝牙栈中没有上报可用事件，因此言灵不展示这些按键，也不宣称不稳定的组合键功能。

![遥控器按键与快捷方式](docs/images/03-shortcuts.png)

## 一键自检与修复

“连接与自检”会在本机检查七个环节：核心组件、VB-CABLE 两个端点、已验证稳定语音档案、后台桥接、RC003 / ATVV、转写工具与快捷键、最近一次端到端听写。异常项会显示唯一的下一步按钮，可直接打开蓝牙、转写配置、官方驱动安装页或恢复稳定参数。

![连接与自检](docs/images/04-diagnostics.png)

自检只读取本地状态与聚合指标，不读取或记录转写文字。遇到问题时请先按 [常见问题](docs/USER_GUIDE_ZH.md#常见问题) 排查，再复制隐私安全的问题摘要提交 Issue。

## 系统要求与已知边界

- Windows 10 或 Windows 11，64 位。
- 小米 RC003 / MI RC 蓝牙语音遥控器。
- 可用的 Bluetooth LE 适配器。
- 至少一种受支持的语音转文字工具。
- [VB-CABLE](https://vb-audio.com/Cable/)，需从官方网站单独安装。
- 安装包尚未进行商业代码签名，Windows 首次运行时可能显示来源提示。请只从本仓库 Releases 下载并核对 SHA-256。

## 开发与构建

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

生成安装包与免安装包需要 Inno Setup 6：

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_RELEASE.ps1
```

详细架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。返回键与独立音量键的 Windows 链路实测见 [RC003 按键研究记录](docs/RC003_BACK_VOLUME_RESEARCH.md)。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 开源与第三方

Vibe Flow Remote 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在本项目中，其许可和安装包由 VB-Audio 提供。其他依赖和协议研究来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

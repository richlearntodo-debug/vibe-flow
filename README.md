# 言灵 · Vibe Flow Remote

让小米 RC003 / MI RC 蓝牙遥控器成为 Windows 的语音输入入口。

言灵是一款面向 Windows Vibe Coding 用户的遥控器语音输入工具。按住录音键时，它把遥控器麦克风音频实时送入微信输入法、Typeless、Windows 语音输入、Voquill 或其他快捷键驱动的听写工具；松开即结束本次输入。音频只在本机流转，言灵不读取输入结果，也不自行上传录音。

> **普通用户推荐：直接下载 [`VibeFlow-Setup.exe`](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/VibeFlow-Setup.exe)。** 这是 Windows 安装程序。请不要下载 GitHub 发布页底部自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`，它们只是源码，不是可直接安装的软件。

[下载免安装版](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/Vibe-Flow-Windows-x64.zip) · [查看最新版发布页](https://github.com/richlearntodo-debug/vibe-flow/releases/latest) · [完整中文教程](docs/USER_GUIDE_ZH.md) · [当前发布说明](docs/RELEASE_NOTES_ZH.md) · [问题排查](docs/USER_GUIDE_ZH.md#常见问题)

![言灵总览](docs/images/01-overview.png)

## 它解决什么问题

```text
RC003 遥控器麦克风
  -> Bluetooth ATVV
  -> 言灵 Vibe Flow Remote
  -> VB-CABLE
  -> 用户选择的语音转文字工具
  -> 当前输入框
```

- 使用遥控器麦克风收音，不再依赖电脑麦克风距离。
- 按住录音键说话，松开结束，继续使用所选工具的识别与整理能力。
- 一键自检本地组件、VB-CABLE、RC003、转写工具、端点恢复和最近一次音频质量，并提供直达修复入口。
- 支持已通过真机验证的确认、Home、TV、功能键和方向键操作。
- 方向键短按导航，长按上/下调节系统音量。
- 默认仅在本地转发且不保存录音；只有用户明确确认一次性音频诊断时，才保存下一段、最长 30 秒的分段 WAV。不读取听写文字，不向转写客户端注入代码。

## 五步开始

1. 下载上方推荐的 `VibeFlow-Setup.exe` 并按提示安装；也可以下载免安装 ZIP，完整解压后运行 `VibeFlow.exe`。
2. 在首次向导选择微信输入法、Typeless 或其他常用转写工具。
3. 按页面安装并检测 `CABLE Input` / `CABLE Output`，然后配对并连接 RC003。
4. 确认工具快捷键与言灵中的预设一致，并完成一次启动测试。
5. 在向导输入框完成首次遥控器听写；成功后会出现绿色反馈和轻提示音。

![首次设置](docs/images/00-first-run.png)

注意：VB-CABLE 是当前架构唯一必须额外安装的本地驱动，发布包不包含它；首次向导仅在检测不到端点时提供官方安装入口。言灵写入播放端点 `CABLE Input`。开始听写前，言灵会临时把 Windows 默认录音端点切换为对应的 `CABLE Output`，音频排空后恢复原麦克风；关闭自动路由时，才需要在转写工具中手动选择 `CABLE Output`。

## 支持的转写工具

| 工具 | 默认启动方式 | 状态 |
| --- | --- | --- |
| 微信输入法 | 工具栏优先，`Ctrl + Win` 回退 | 当前测试机已完成端到端验证 |
| Typeless | 轻触 `Right Alt` 开始/结束 | 当前测试机已完成端到端验证 |
| Windows 语音输入 | 轻触 `Win + H` 开始/结束 | 已接入系统快捷键路径 |
| Voquill | 按住 `Ctrl + Win`，松开结束 | 已按其开源默认热键接入；需安装客户端后联调 |
| 其他语音工具 | 用户配置快捷键与单击/按住模式 | 通用路径 |

Typeless、Voquill 和其他第三方客户端不包含在发布包中，其账号、网络、识别和隐私策略由对应软件负责。

![转写工具选择](docs/images/06-transcription-tools.png)

## 一键自检与修复

“连接与自检”会在本机检查七个环节：核心组件、VB-CABLE 两个端点、已验证稳定语音档案、后台桥接、RC003/ATVV、转写工具与快捷键、最近一次端到端听写。异常项会显示唯一的下一步按钮，可直接打开蓝牙、转写配置、官方驱动安装页或恢复稳定参数。

![连接与自检](docs/images/04-diagnostics.png)

当前稳定档案固定为语音状态机 v11、`1.0x` 灵敏度、清晰增强、180 ms 音频排空、`CABLE Input` 播放端和自动默认麦克风路由。自检只读取本地状态与聚合指标，不读取或记录转写文字。

## 已验证按键

| 遥控器按键 | 默认操作 |
| --- | --- |
| 录音 | 按住说话，松开后等待文字回填 |
| 确认 | Enter / 确认发送 |
| Home | 显示桌面 `Win + D` |
| TV | 打开任务切换器，左右选择，确认进入 |
| 功能键 | 默认打开或切回 ChatGPT 客户端，可选择 DeepSeek、Claude、Cursor、VS Code、Windsurf 或其他快捷操作 |
| 方向键 | 短按原生方向；长按上/下调整系统音量 |

RC003 的独立返回键和音量 +/- 键在已验证的 Windows 蓝牙栈中没有上报可用事件，因此言灵不展示这些按键，也不提供不稳定的组合键。

![遥控器按键与快捷方式](docs/images/03-shortcuts.png)

## 系统要求

- Windows 10 或 Windows 11，64 位。
- 小米 RC003 / MI RC 蓝牙语音遥控器。
- 可用的 Bluetooth LE 适配器。
- 至少一种受支持的语音转文字工具。
- VB-CABLE，需从其官方网站单独安装。

当前版本为 `1.0.3`。此版本修复开机后蓝牙语音服务或所选转写工具尚未就绪时，首次录音键无法触发的问题，并增加稳定参数防误改、明确的开机启动授权、完整五步截图和 7 项本地自检；首页遥控器及快捷方式页也按真实 RC003 比例和操作流程重新设计。稳定语音档案 v11 的音频参数保持不变。安装包尚未进行商业代码签名，首次运行时 Windows 可能显示来源提示；请只从本仓库的 Releases 页面下载，并核对发布页提供的 SHA-256。

## 开发与构建

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

生成安装包与免安装包（需要 Inno Setup 6）：

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_RELEASE.ps1
```

详细架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。返回键与独立音量键的 Windows 链路实测见 [RC003 按键研究记录](docs/RC003_BACK_VOLUME_RESEARCH.md)。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 开源与第三方

Vibe Flow Remote 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在本项目中，其许可和安装包由 VB-Audio 提供。其他依赖和协议研究来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

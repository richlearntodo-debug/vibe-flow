# 言灵 · Vibe Flow Remote

把小米 RC003 / MI RC 蓝牙遥控器变成 Windows 语音输入与 Vibe Coding 快捷控制器：聚焦输入框，按住说话，松开即可得到整理后的文字。

## 最新正式版 · v1.1.0

**发布日期：2026-08-26 · Windows 10/11 x64**

| 直接开始 | 链接 |
| --- | --- |
| 推荐安装版 | [**下载 VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/VibeFlow-Setup.exe) |
| 免安装版 | [下载 Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/Vibe-Flow-Windows-x64.zip) |
| 零基础教程 | [打开完整中文使用教程](docs/USER_GUIDE_ZH.md) |
| 2 分钟快速开始 | [查看快速开始](QUICK_START_ZH.md) |
| 本版说明 | [查看 v1.1.0 更新与边界](docs/RELEASE_NOTES_ZH.md) |
| 下载校验 | [下载 SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/SHA256SUMS.txt) |

> [!IMPORTANT]
> 普通用户只需下载 **VibeFlow-Setup.exe**。GitHub 发布页自动生成的 `Source code (zip)` 和 `Source code (tar.gz)` 是源码，不是 Windows 安装程序。

[最新版发布页](https://github.com/richlearntodo-debug/vibe-flow/releases/latest) · [首次安装](docs/USER_GUIDE_ZH.md#从零开始) · [快捷键配置](docs/USER_GUIDE_ZH.md#配置遥控器快捷键) · [问题排查](docs/USER_GUIDE_ZH.md#按现象排查)

![言灵 v1.1.0 总览](docs/images/01-overview.png)

## v1.1.0 亮点

| 新增或优化 | 用户能感受到的变化 |
| --- | --- |
| **按住说话** | 按下录音键开始，松开立即结束；独立 RELEASE 事件避免结束后再次打开麦克风。 |
| **确定收尾** | 优先等待遥控器自然停止；控制包丢失时才执行一次有界关闭，不做松开后的自动重开。 |
| **微信 AI 整理** | 新安装默认使用 `Ctrl + Win + Shift` 单击切换，保留完整尾音后再交给微信输入法整理。 |
| **清晰反馈** | 首页显示按住听写状态、单次时长和输出电平；录音、整理、完成与异常都有颜色和遥控器光效。 |
| **原生直填** | 沿用上一正式版验证过的焦点保持策略，微信输入法直接把结果写入录音前选中的输入框。 |
| **音量保护** | 微信听写期间不再触发 Windows“通信活动”自动降音；结束后恢复原系统偏好。 |
| **结束提示音** | 开始录音保持安静并显示波纹光效；结束时播放清晰、短促的完成音。 |
| **安全更新** | 应用内检查正式版，GitHub API 限流时自动兜底；安装前下载并核对 SHA-256，始终由用户确认。 |
| **新手流程** | 新安装默认按住说话，五步向导从转写工具、VB-CABLE、蓝牙到真实听写逐项验收。 |
| **高频快捷键** | 新增保存、全选、快速打开文件、新建终端、删除当前行、运行/调试、关闭标签页等选项。 |
| **配置防错** | 快捷键修改立即保存；多个实体键使用相同功能时给出黄色冲突提醒。 |
| **教程与截图** | README、快速开始、完整教程、故障排查和五步截图统一到 v1.1.0。 |

> [!NOTE]
> RC003 固件会在一次物理长按约 60 秒时主动发送按键松开和音频停止；Windows 无法在此后判断用户是否仍按住。因此稳定的按住模式单次以约 60 秒为硬件上限。连续听写仅保留为短按后立即松开的实验选项，不作为默认能力宣传。

本版固化已经反复真机验证的语音参数：稳定档案 v11、`1.0x`、清晰增强、`180 ms` 排空、微信输入法 `Ctrl + Win + Shift` AI 整理模式、`180 ms` 工具启动等待，以及自动 `CABLE Output` 路由。

## 日常使用

1. 点击要输入文字的位置。
2. **按住遥控器录音键**，看到紫色听写状态后自然说话。
3. 说完后**松开录音键**，不要再按第二次。
4. 听到结束音后等待青色“正在整理”变成绿色“已完成”，文字会返回第 1 步选中的输入框。

![语音听写配置](docs/images/02-dictation.png)

完整原理：

```text
RC003 遥控器麦克风
  -> Bluetooth ATVV
  -> 言灵 Vibe Flow Remote
  -> VB-CABLE
  -> 微信输入法 / Typeless / Windows 语音输入 / 其他工具
  -> 当前输入框
```

言灵负责本地收音和传输，所选工具负责语音识别与文字整理。普通听写不保存录音、不读取识别文字，也不会自行上传音频。微信路径不会读取或改写剪贴板，也不会发送合成粘贴；提交时保持录音前的真实编辑焦点，由微信输入法直接写入文字。

## 从零开始只需五步

1. 安装并打开言灵，选择日常使用的语音转写工具。
2. 按向导从 [VB-Audio 官网](https://vb-audio.com/Cable/) 安装 VB-CABLE，重启 Windows。
3. 在 Windows 蓝牙中配对 `MI RC` / RC003，回到向导等待“已连接，可以使用”。
4. 核对所选工具的全局快捷键和触发方式，点击“测试启动与结束”。
5. 在测试输入框中按住录音键开始，说完后松开；绿色验收通过后完成设置。

![首次使用五步向导](docs/images/00-first-run.png)

VB-CABLE 是当前语音链路唯一必须额外安装的本地驱动，不包含在本仓库发布包中。普通用户无需长期修改 Windows 默认麦克风；言灵只在听写期间临时路由到 `CABLE Output`，结束后自动恢复。

## 支持的转写工具

| 工具 | 推荐配置 | 验证状态 |
| --- | --- | --- |
| 微信输入法 | `Ctrl + Win + Shift` · AI 整理 · 单击切换 | RC003 按住说话、松开提交与完整尾音真机验证 |
| Typeless | 常见为 `Right Alt` · 单击切换 | RC003 端到端真机验证；以客户端当前快捷键为准 |
| Windows 语音输入 | `Win + H` · 单击切换 | 已接入 Windows 系统路径 |
| Voquill | 当前开源版本常见 `Ctrl + Win` · 按住触发 | 已接入通用路径，需按安装版本核对 |
| 其他语音工具 | 自定义全局快捷键和触发方式 | 支持通用快捷键路径 |

![可选择的语音转写工具](docs/images/06-transcription-tools.png)

第三方客户端不包含在发布包中，其账号、网络、识别质量、数据处理和时长策略由对应软件负责。

## 遥控器与快捷键

| 实体按键 | 默认行为 | 可配置 |
| --- | --- | --- |
| 录音键 | 按住开始，松开结束；单次约 60 秒硬件上限 | 固定，由言灵管理 |
| 确认键 | `Enter`，确认、发送或换行 | 是 |
| Home | `Win + D` 显示桌面 | 是 |
| TV | 打开任务切换器，左/右选择，确认进入 | 是 |
| 功能键 | 打开或切回 ChatGPT 客户端 | 是 |
| 方向键 | 短按原生导航；长按上/下调节系统音量 | 固定 |

可选操作包含复制、剪切、粘贴、撤销、重做、保存、全选、查找、命令面板、快速打开文件、新建终端、删除当前行、运行/调试、标签页与应用切换，以及 ChatGPT、DeepSeek、Claude、Cursor、VS Code、Windsurf 和 Windows Terminal 客户端。

![遥控器快捷方式配置](docs/images/03-shortcuts.png)

RC003 的独立返回键和音量 +/- 键在已验证的 Windows 蓝牙栈中没有上报稳定事件，因此本版不宣传这些按键，也不提供不可靠的组合键。系统音量使用长按方向上/下。

## 一键自检与修复

“连接与自检”在本机检查七个环节：核心组件、VB-CABLE、稳定语音档案、后台桥接、RC003 / ATVV、转写工具与快捷键、最近一次端到端听写。微信输入法会话会确认原输入框焦点是否保持，以及工具是否完成原生直填；异常项会给出唯一下一步。

![连接与自检](docs/images/04-diagnostics.png)

提交问题前点击“复制问题摘要”或“导出诊断”。普通日志不包含录音、识别文字、完整蓝牙地址或完整设备路径。

## 正式版时间线

| 日期 | 版本 | 主要内容 | 记录 |
| --- | --- | --- | --- |
| 2026-08-26 | `v1.1.0` | 按住说话、松开可靠结束、原生直填、结束提示音、安全更新与新手教程 | [查看](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.1.0) |
| 2026-08-25 | `v1.0.3` | 修复开机后首次录音，增加后台恢复与转写工具预热，重做首页与向导 | [查看](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.3) |
| 2026-08-24 | `v1.0.2` | 强化下载入口、升级保护、教程和发布说明 | [查看](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.2) |
| 2026-08-24 | `v1.0.1` | 完善工具配置、症状式排查和发布截图 | [查看](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.1) |
| 2026-08-24 | `v1.0.0` | 首个 Windows 正式版与稳定语音档案 v11 | [查看](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.0.0) |

完整技术记录见 [CHANGELOG.md](CHANGELOG.md)。

## 系统要求

- Windows 10 或 Windows 11，64 位。
- 小米 RC003 / MI RC 蓝牙语音遥控器。
- 可用的 Bluetooth LE 适配器。
- 至少一种支持的语音转文字工具。
- [VB-CABLE](https://vb-audio.com/Cable/) 本地虚拟音频驱动。

发布流水线已支持 Authenticode 签名和强制验签，但签名需要发布者单独配置商业证书。下载文件的实际签名状态以 Windows“数字签名”页为准；无论是否签名，都应只从本仓库 Releases 下载并核对 `SHA256SUMS.txt`。

## 开发与构建

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

构建正式安装包和免安装 ZIP 需要 Inno Setup 6：

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_RELEASE.ps1
```

架构说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)，连续听写实现与边界见 [连续听写专题](docs/CONTINUOUS_DICTATION_ZH.md)，签名与安全更新见 [Windows 代码签名与安全更新](docs/CODE_SIGNING_ZH.md)，贡献说明见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 社区

遇到问题可先运行应用内自检，再携带“问题摘要”提交 GitHub Issue。也可以扫码加入 Vibe Flow 用户社区，交流使用体验与 Vibe Coding 工作流。

<img src="docs/images/vibe-flow-community.png" alt="Vibe Flow 用户社区二维码" width="860">

## 开源与第三方

Vibe Flow Remote 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在本项目中，其许可和安装包由 VB-Audio 提供。其他依赖与协议研究来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

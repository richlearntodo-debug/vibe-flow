# 言灵 Vibe Flow Remote v1.2.0

把小米 RC003 / MI RC 蓝牙遥控器变成 Windows 长时语音输入与 Vibe Coding 快捷控制器：单击开始说话，再次单击结束，文字由微信输入法、Typeless 等工具直接写回当前输入框。

## 下载

| 你需要的文件 | 直接下载 |
| --- | --- |
| **普通用户推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.0/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.0/Vibe-Flow-Windows-x64.zip) |
| 文件完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.0/SHA256SUMS.txt) |

普通用户只需下载第一行。免安装版必须完整解压后再运行 `VibeFlow.exe`。请勿下载页面自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`，它们是源码，不是 Windows 安装程序。

> 本次公开文件没有商业 Authenticode 签名，Windows 首次运行可能显示来源提示。请只从本仓库 Release 下载，并使用 `SHA256SUMS.txt` 核对文件完整性。

## 加入用户社区

首次安装或排查问题时，可扫码加入 Vibe Flow 用户社区。

<img src="https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.2.0/docs/images/vibe-flow-community.png" alt="Vibe Flow 用户社区二维码" width="640">

![言灵 Vibe Flow Remote v1.2.0 总览](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.2.0/docs/images/01-overview.png)

## 本版亮点

- **长时连续听写**：单击录音键开始并松开，完成后再次单击结束，不需要持续按住。
- **15 分钟真机回归**：最长一轮为 15 分 22 秒，真实音频覆盖 `99.6%`，`MIC_EXTEND 114/114`，蓝牙与 VB-CABLE 丢包均为 `0`。
- **真实音频自检**：显示逻辑时长、真实音频时长、覆盖率、包间隔、续租、队列、WASAPI、端点与内存状态；亚秒误触不会被判为完整通过。
- **微信 AI 整理与原生直填**：推荐 `Ctrl + Win + Shift`，先排空完整尾音，再由微信输入法直接写入原输入框；不读取剪贴板，不模拟粘贴。
- **清晰状态反馈**：紫色表示真实音频正在到达，青色表示正在恢复或整理，绿色表示完成，失败会给出下一步；结束时只播放一次短提示音。
- **按住模式保留**：兼容按下开始、松开结束；已验证单麦克风、零丢包和路由恢复，不会在松开后自动重开。
- **安全更新修复**：中文 GitHub Release 元数据按 UTF-8 解析，API 失败时使用官方重定向兜底，安装前仍强制核对 SHA-256。

## 五步开始

1. 安装并打开言灵，选择微信输入法、Typeless、Windows 语音输入、Voquill 或其他支持全局快捷键的工具。
2. 从 [VB-Audio 官网](https://vb-audio.com/Cable/) 安装 VB-CABLE，并重启 Windows。
3. 在 Windows 蓝牙中配对 `MI RC` / RC003，回到言灵等待“已连接，可以使用”。
4. 核对转写工具快捷键与触发方式，点击“测试启动与结束”。
5. 聚焦测试输入框，单击录音键开始并松开，讲完后再单击一次结束；看到绿色成功状态即可完成。

[完整零基础教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.2.0/docs/USER_GUIDE_ZH.md) · [2 分钟快速开始](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.2.0/QUICK_START_ZH.md) · [连续听写原理与边界](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.2.0/docs/CONTINUOUS_DICTATION_ZH.md)

## 已知边界

- 连续听写单次有 30 分钟软件安全保护，不宣传绝对无限时长。
- 兼容按住模式受 RC003 固件约 60 秒物理长按边界限制。
- 第三方转写工具可能有自己的账号、网络、识别和时长策略。
- 已验证 Windows 蓝牙栈中，RC003 独立返回键和音量 +/- 键没有稳定事件；系统音量使用长按方向上/下。

## 发布验证

- 主程序、ATVV 采集服务与按键桥接统一为 `1.2.0`。
- 配置 schema 20、onboarding 6；新安装默认连续听写，升级保留原模式和快捷键。
- 124 秒、6 分钟、15 分 22 秒连续听写真机回归通过；按住模式独立回归通过。
- 自动校验、语音管线自测、VB-CABLE 时钟测试、7/7 自检、ZIP 审计、SHA-256 校验和隔离安装升级测试通过。

遇到问题先打开“连接与自检”，点击“重新自检”，再复制问题摘要提交 [GitHub Issue](https://github.com/richlearntodo-debug/vibe-flow/issues)。

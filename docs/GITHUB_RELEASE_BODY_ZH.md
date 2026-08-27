# 言灵 · Vibe Flow Remote v1.2.1

这是面向普通用户的稳定版：**把真实能用的功能做稳，把不能稳定工作的功能拿掉。**

## 下载

| 文件 | 直接下载 |
| --- | --- |
| **推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/Vibe-Flow-Windows-x64.zip) |
| 完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/SHA256SUMS.txt) |

普通用户下载第一行。GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。

## 新版亮点

- 捕获组件完整恢复为首个正式版 `v1.0.3` 内核：`Ctrl + Win`、按住说话、松开结束。
- 修复松开后才打开麦克风、结束后出现第二个麦克风的问题。
- 删除长录音续接、`MIC_EXTEND` 和强制松开关闭逻辑。
- 转译工具直接写入当前焦点文本框，不经过剪贴板回填。
- 上下左右四个方向键可分别配置一个常用动作，新增 Windows 区域截图 `Win + Shift + S`。
- TV 改为持久 Windows 任务视图：方向选择，确认进入。
- 默认白色白天模式，新增更克制的夜间模式。
- 开机、返回和独立音量键不再显示为可配置功能。

当前 RC003 稳定单段最长约 `60 秒`。提前松开立即结束，到达硬件边界后开始下一段。

## 使用

聚焦输入框 -> 按住录音键说话 -> 松开等待转译 -> 确认键发送。

首次使用请完成应用内 11 步设置；异常时运行 10 项一键自检。版本 `1.2.1`、配置 schema `25`、onboarding `8`。

[v1.2.1 图文教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.2.1/docs/V1_2_1_TUTORIAL_ZH.md) · [所有历史版本固定下载](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.2.1/docs/VERSION_ARCHIVE_ZH.md)

# 言灵 · Vibe Flow Remote V1.5.0

V1.5 是快捷键与工作流正式升级版，并合并了未完整发布的 V1.4 内容。录音继续使用已经验证稳定的按住说话链路，没有改动 Capture 和语音参数。

## 下载

| 文件 | 直接下载 |
| --- | --- |
| **推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/Vibe-Flow-Windows-x64.zip) |
| 完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/SHA256SUMS.txt) |

普通用户下载第一行。GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。安装包目前未配置商业代码签名，首次运行可能出现 SmartScreen 提醒，请核对下载来源和 SHA-256。

## 本轮重点

- **直接录制快捷键**：在实体键盘按下目标组合即可，不再输入快捷键名称；
- **Profiles**：通用导航、Vibe Coding、浏览器 AI、Terminal Agent 可切换、导入和导出；
- **Smart Profiles**：可选按前台 APP 自动切换，默认关闭；
- **本机 APP 选择器**：同时显示正在运行与已安装应用、名称和图标；
- **浏览器返回修复**：使用 Windows 专用 Browser Back 事件；
- **真实执行回执**：首页明确显示实体按键最终成功或失败；
- **完整新手教程**：5 项首次设置、真实听写、自检、快捷键和排错均配 APP 截图。

## 语音稳定性不变

- 按住录音、松开结束，RC003 单段约 60 秒；
- `v1.0.3` 内核、`v11`、`1.0 / speech / 180 ms`、`CABLE Input` 全部保持；
- 微信输入法默认 `Ctrl + Win`、toggle、`80 ms`；
- 文字由语音工具直接写入聚焦输入框，无剪贴板回填；
- 没有加入长录音续接、宏或新的录音状态机。

开机、返回和独立音量键仍没有稳定 Windows 事件，本版本不提供配置入口。

[V1.5 零基础图文教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/V1_5_USER_GUIDE_ZH.md) · [V1.4 + V1.5 更新说明](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/RELEASE_NOTES_ZH.md) · [所有版本下载](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/VERSION_ARCHIVE_ZH.md)

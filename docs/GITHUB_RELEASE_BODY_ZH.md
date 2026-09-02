# 言灵 · Vibe Flow Remote V1.5.0

**稳定语音输入，更完整的遥控器快捷工作流。**

V1.5 把小米 RC003 / MI RC 遥控器变成 Windows 上的语音输入与 AI 工作流控制器：按住说话、松开转译，并通过可配置按键打开应用、执行快捷键、截图或切换 Profile。本版合并 V1.4 的阶段性成果，同时保留已经反复验证的稳定录音链路。

## 立即下载

| 文件 | 直接下载 |
| --- | --- |
| **推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/Vibe-Flow-Windows-x64.zip) |
| 完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/SHA256SUMS.txt) |

> **普通用户请选择第一行安装版。** GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。安装包目前未配置商业代码签名，首次运行可能出现 SmartScreen 提醒，请核对仓库地址和 SHA-256。

## 核心功能看板

| 能力 | 可以完成的操作 |
| --- | --- |
| **遥控器语音输入** | 使用 RC003 麦克风收音，按住说话、松开结束，由微信输入法、Typeless、豆包输入法或 Windows 语音输入完成转译 |
| **快捷键直接录制** | 在实体键盘按下目标组合即可完成映射，不再手动输入按键名称 |
| **应用与网页控制** | 查找正在运行和已安装的 APP，打开或切换应用，也可打开 HTTPS 网页 |
| **Profiles 工作流** | 使用通用导航、Vibe Coding、浏览器 AI、Terminal Agent，并支持导入、导出和可选自动切换 |
| **高频快捷动作** | 截图、复制、粘贴、撤销、重做、系统动作、媒体控制和自定义组合键 |
| **自检与新手向导** | 检查蓝牙、真实音频、VB-CABLE、语音工具和自启动，并给出明确修复入口 |

[查看完整功能看板](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/FEATURES_ZH.md)

## 加入用户社区

加入社群可获得**配置答疑、设备兼容反馈、版本更新通知和 Vibe Coding 工作流分享**。遇到问题时，可以从“自检”页面导出诊断包后一起排查。

<p align="center">
  <img src="https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.5.0/docs/images/vibe-flow-community.png" alt="扫码加入 Vibe Flow 用户社区" width="760">
</p>

## V1.5 新增与改进

- **直接录制快捷键**：在实体键盘按下目标组合即可，不再输入快捷键名称；
- **Profiles**：通用导航、Vibe Coding、浏览器 AI、Terminal Agent 可切换、导入和导出；
- **Smart Profiles**：可选按前台 APP 自动切换，默认关闭；
- **本机 APP 选择器**：同时显示正在运行与已安装应用、名称和图标；
- **浏览器返回修复**：使用 Windows 专用 Browser Back 事件；
- **真实执行回执**：首页明确显示实体按键最终成功或失败；
- **完整新手教程**：5 项首次设置、真实听写、自检、快捷键和排错均配 APP 截图。

## 稳定性与使用边界

- 按住录音、松开结束，RC003 单段约 60 秒；
- 文字由语音工具直接写入聚焦输入框，无剪贴板回填；
- 没有加入长录音续接、宏或新的录音状态机。

开机、返回和独立音量键仍没有稳定 Windows 事件，本版本不提供配置入口。

<details>
<summary>查看冻结的语音技术基线</summary>

- Capture 使用已验证的 `v1.0.3` 内核；
- 固定参数为 `v11`、`1.0 / speech / 180 ms`、`CABLE Input`；
- 微信输入法默认 `Ctrl + Win`、toggle、`80 ms`；
- V1.5 未修改 Capture 和语音参数。

</details>

[V1.5 零基础图文教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/V1_5_USER_GUIDE_ZH.md) · [V1.4 + V1.5 更新说明](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/RELEASE_NOTES_ZH.md) · [所有版本下载](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/VERSION_ARCHIVE_ZH.md)

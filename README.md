# 言灵 · Vibe Flow Remote

<p align="center">
  <img src="docs/images/vibe-flow-community.png" alt="言灵 Vibe Flow 功能介绍与用户社群二维码，微信扫码加入" width="100%">
</p>

<p align="center">
  <strong>微信直接扫码加入 Vibe Flow 用户社群</strong><br>
  获取安装配置帮助、使用技巧、版本更新与问题反馈支持
</p>

**把小米 RC003 / MI RC 蓝牙遥控器变成 Windows 上稳定、可检查的语音输入工具。**

按住录音键说话、松开结束，把文字送进 ChatGPT 等你聚焦的输入框。V2.0 继续把录音、输入稳定性和用户配置保护放在所有新功能之前。

## V2.0.0 离键闭环候选版

> [!WARNING]
> 当前仓库构建的是 `2.0.0` candidate，不是已发布稳定版。自动化构建与本机 UI 检查已完成；RC003 完整矩阵、Windows 10/11、完整 DPI、VB-CABLE 重启恢复和安装生命周期仍需授权环境验证。不要把候选包转发为正式稳定版。

候选交付物由 `BUILD_RELEASE.ps1` 生成到本地 `release/`，包括 `VibeFlow-Setup.exe`、`Vibe-Flow-Windows-x64.zip` 和 `SHA256SUMS.txt`。候选包未签名，不提供尚未创建的公开下载链接。

[V2.0 候选版使用指南](docs/V2_0_USER_GUIDE_ZH.md) · [V2 更新说明](docs/V2_0_RELEASE_NOTES_ZH.md) · [测试状态](docs/V2_0_HARDWARE_TEST_MATRIX_ZH.md) · [已知限制](docs/V2_0_KNOWN_LIMITATIONS_ZH.md)

## 最新已发布稳定版 · V1.5.0

**Windows 10 / 11 x64 · 稳定语音基线 · 快捷键与 Profiles 正式升级**

| 下载 | 适合谁 | 固定入口 |
| --- | --- | --- |
| **安装版 EXE** | **普通用户，推荐** | [**下载 VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/VibeFlow-Setup.exe) |
| 免安装 ZIP | 熟悉便携软件的用户 | [下载 Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/Vibe-Flow-Windows-x64.zip) |
| SHA-256 | 校验文件是否完整 | [查看 SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/SHA256SUMS.txt) |

> [!IMPORTANT]
> 普通用户下载第一行的 `VibeFlow-Setup.exe`。GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。当前安装包尚未配置商业代码签名，Windows 首次运行可能显示 SmartScreen 提醒；请只从本仓库 Release 下载并核对 SHA-256。

[V1.5 零基础图文教程](docs/V1_5_USER_GUIDE_ZH.md) · [完整功能看板](docs/FEATURES_ZH.md) · [V2 / V1.5 更新说明](docs/RELEASE_NOTES_ZH.md) · [所有版本下载](docs/VERSION_ARCHIVE_ZH.md)

**发布可信度：** [版本身份与冻结参数](VIBE_MIC_VERSION.md) · [兼容性矩阵](docs/COMPATIBILITY_MATRIX_ZH.md) · [正式版质量门禁](docs/RELEASE_QUALITY_GATE_ZH.md) · [Issue #2 回归结论](docs/ISSUE_2_REGRESSION_ZH.md) · [代码签名策略](docs/CODE_SIGNING_ZH.md)

> V2 候选的 Host、Bridge 和 Installer 版本为 `2.0.0`；Capture 文件继续显示 `1.2.1.0`，这是刻意冻结的稳定语音组件，不是混装旧版。源码和二进制 SHA-256 在构建与 CI 中强制校验。

> **范围说明：** 当前 V2.0 候选的主产品是稳定语音输入、快捷键配置、语音设置和自检。Vibe Flow 不提供代码编辑器、不保存普通转写文字，也不自动发送 AI 消息。

## 功能展示看板

| 核心场景 | V2.0 候选能力 | 用户得到什么 |
| --- | --- | --- |
| **遥控器语音输入** | RC003 麦克风、按住说话、松开结束，支持微信输入法、Typeless 和 Windows 语音输入 | 离开键盘也能向 AI、编辑器或网页输入内容 |
| **Smart Focus** | 学习、测试和验证编辑控件；录音开始时安全取消 | 先锁定并验证目标，再按住原录音键，不向错误目标盲发 |
| **Live HUD / Context Deck** | 非激活状态浮层和只读遥控器说明面板 | 看清设备、目标、按键与真实执行结果，不抢录音焦点 |
| **实体快捷键录制** | 直接在键盘按下组合键完成映射，无需填写 `Ctrl`、`Shift` 等名称 | 自定义快捷键更快，也更不容易配错 |
| **稳定按键配置（兼容 Profiles）** | 保留 V1.5 的按键映射与可选 Profile，不改变录音键 | 关闭新功能时，原有遥控器行为保持不变 |
| **稳定快捷动作（兼容能力）** | 复制、粘贴、撤销、重做、系统和媒体等已有动作 | 将已验证的高频操作放到触手可及的位置 |
| **自检与恢复** | 检查蓝牙、遥控器、真实音频、VB-CABLE、语音工具、自启动和会话 | 出现问题时看到原因、影响和修复入口 |

[查看完整功能、按键能力与边界说明](docs/FEATURES_ZH.md)

![V2.0 首页与连接状态](docs/images/01-overview.png)

## V2.0 候选版重点更新

V2.0 候选版在 V1.5 稳定语音与按键基础上，集中收口为一条“锁定 APP、按住说话、检查文字、手动确认”的可见路径：

| 能力 | 使用结果 |
| --- | --- |
| **工作流** | 只对验证过的可编辑控件聚焦；不保存用户文字、窗口标题正文或固定坐标。 |
| **统一反馈** | 页面、Deck 和日志共用真实状态，不把请求派发写成外部完成。 |
| **首次设置** | 唯一一套五任务流程覆盖遥控器、音频、语音工具和第一次真实听写。 |
| **直接录制快捷键** | 点击“录制键盘快捷键”，在实体键盘按下组合即可，不再手动输入 `control` 等名称。 |
| **快捷键 Profiles** | 通用导航、Vibe Coding、浏览器 AI、Terminal Agent 可手动切换，也可新建、导入、导出。 |
| **Smart Profiles** | 可选按当前前台应用自动切换 Profile；默认关闭，原有稳定手动模式不受影响。 |
| **本机应用选择器** | 同时查找正在运行与已安装应用，显示名称和图标，并优先切换已有窗口。 |
| **浏览器返回修复** | Browser AI Profile 使用专用 Browser Back 事件，避免与实体左键冲突。 |
| **真实执行回执** | 首页显示实体按键最终执行的动作、Profile 与成功/失败结果。 |
| **图形化快捷动作** | 支持 APP、HTTPS 网页、截图、系统、媒体、编辑和自定义键盘组合。 |
| **浅色 / 深色主题** | 默认浅色，可切换夜间或跟随 Windows；设置立即生效。 |

### 保持不变的稳定语音链路

- 录音仍是**按住说话、松开结束**；RC003 单段约 60 秒。
- Capture 继续使用已验证的 `v1.0.3` 内核，增益 `1.0`、`speech`、尾音排空 `180 ms`。
- 音频仍走 `CABLE Input -> CABLE Output -> 用户选择的语音工具`。
- 语音工具直接把文字写入当前聚焦输入框；言灵不读取转译文字，也不使用剪贴板回填。
- 没有加入长录音续接、宏、多步骤自动化或新的录音状态机。

V1.4 只完成了 Profile、应用目录、Browser Back 和执行回执的一部分，因此以**不完整预览版**归档，不建议日常安装。[查看 V1.4 归档说明](docs/V1_4_PREVIEW_ZH.md)。

## 三分钟开始使用

1. 安装并启动言灵，完成应用内 5 项首次设置。
2. 在 Windows 蓝牙中配对 `MI RC` / RC003。
3. 按向导安装 VB-CABLE；语音工具的麦克风输入选择 `CABLE Output`。
4. 选择微信输入法、Typeless、Windows 语音输入或自定义工具，并核对双方快捷键一致。
5. 打开 ChatGPT 等你常用的 APP，并在“语音”页添加常用应用、学习它的输入框。
6. 在目标输入框中按住录音键说话，松开后等待语音工具处理并检查文字。
7. 使用确认键手动发送；Vibe Flow 不读取文字，也不会自动发送。

```text
RC003 microphone -> Bluetooth ATVV -> Vibe Flow
  -> CABLE Input -> CABLE Output -> voice tool -> focused text box
```

![语音工具与音频配置](docs/images/02-dictation.png)

## 已公开支持的实体按键

| 实体按键 | 默认行为 | 是否可配置 |
| --- | --- | --- |
| 录音键 | 按住收音，松开结束 | 固定稳定链路，不参与自定义 |
| 上 / 下 / 左 / 右 | 标准方向键 | 可分别设置 APP、网页、截图、系统动作或快捷键 |
| 中间确认键 | `Enter`，确认或发送 | 可配置 |
| Home | 短按显示桌面，长按可自定义 | 支持短按 / 长按 |
| TV | 打开持久任务视图 | 可配置单击动作 |
| 功能键 | 短按复制，长按粘贴 | 支持短按 / 长按 |

开机、返回和独立音量键在当前 RC003 / Windows 蓝牙组合上没有稳定事件，因此 V2.0 仍不提供配置入口，也不宣传为可用功能。

![V2.0 快捷键与遥控器示意](docs/images/03-shortcuts.png)

## 快捷键与 Smart Profiles

在“快捷键”页选择实体按键后，可以直接挑选动作，或点击“录制键盘快捷键”并在真实键盘上按下组合。Smart Profiles 默认关闭；开启后可把 Cursor、浏览器或终端绑定到不同 Profile，切换前台应用时自动使用对应动作表。Profile 只保存快捷键，不包含麦克风或语音参数。

![动作选择与快捷键录制入口](docs/images/07-shortcut-actions.png)

![Smart Profile 应用绑定](docs/images/09-smart-profile-apps.png)

## 自检与排错

“自检”页覆盖组件、蓝牙、遥控器、按键、真实音频、VB-CABLE、稳定参数、语音工具、自启动和完整会话。每项都会显示当前状态、原因与修复入口；完成修复返回后可直接重新检测。

![一键自检](docs/images/04-diagnostics.png)

遇到问题时先确认：

1. 只运行一个 Vibe Flow 实例；
2. 语音工具的麦克风输入是 `CABLE Output`；
3. 文本框已经被单击并出现插入光标；
4. 言灵与语音工具中的快捷键、触发方式完全一致；
5. 从自检页导出诊断包再反馈，日志不包含录音或转译文字。

## 开发与验证

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
powershell -ExecutionPolicy Bypass -File .\BUILD_DEVELOPMENT.ps1
npm test
```

`BUILD_DEVELOPMENT.ps1` 会编译 Host 与按键 Bridge，并把哈希匹配的冻结 Capture 1.2.1 和固定 NAudio 2.2.1 复制到同一运行目录后执行三项自检。它不会重编译 Capture；依赖缺失时会明确停止，避免启动一个只有 UI/快捷键、没有麦克风能力的半成品目录。

Vibe Flow Remote 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在仓库中，由 [VB-Audio](https://vb-audio.com/Cable/) 提供。

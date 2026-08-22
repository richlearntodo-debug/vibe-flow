# 言灵 Vibe Flow

让小米 RC003 / MI RC 蓝牙遥控器成为 Windows 的语音输入入口。

言灵把遥控器麦克风的实时音频送入微信输入法，由微信输入法完成语音转文字与结构化整理。音频只在本机流转，言灵不读取输入结果，也不上传录音。

[下载 Windows 版](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/Vibe-Flow-Windows-x64.zip) · [完整中文教程](docs/USER_GUIDE_ZH.md) · [问题排查](docs/USER_GUIDE_ZH.md#常见问题)

![言灵总览](docs/images/01-overview.png)

## 它解决什么问题

```text
RC003 遥控器麦克风
  -> Bluetooth ATVV
  -> 言灵 Vibe Flow
  -> VB-CABLE
  -> 微信输入法语音转文字
  -> 当前输入框
```

- 使用遥控器麦克风收音，不再依赖电脑麦克风距离。
- 按住录音键说话，松开结束，继续使用微信输入法的整理能力。
- 支持已通过真机验证的确认、Home、TV、功能键和方向键操作。
- 方向键短按导航，长按上/下调节系统音量。
- 本地运行，不保存录音、不读取听写文字、不注入微信进程。

## 三分钟开始

1. 在 Windows 蓝牙设置中配对 `MI RC` 或“小米蓝牙语音遥控器”。
2. 从 [VB-Audio 官网](https://vb-audio.com/Cable/) 安装 VB-CABLE。
3. 下载并解压最新版本，运行 `VibeFlow.exe`。
4. 跟随首次设置，在微信输入法中把麦克风选择为 `CABLE Output`。
5. 聚焦任意输入框，按住遥控器录音键说话，松开后等待文字回填。

![首次设置](docs/images/00-first-run.png)

注意：言灵写入的是播放端点 `CABLE Input`，微信输入法需要选择对应的录音端点 `CABLE Output`。名字很像，但方向相反。

## 已验证按键

| 遥控器按键 | 默认操作 |
| --- | --- |
| 录音 | 按住听写，松开结束 |
| 确认 | Enter / 确认发送 |
| Home | 显示桌面 `Win + D` |
| TV | 打开任务切换器，左右选择，确认进入 |
| 功能键 | 单击 `Ctrl + Shift + P`，可在应用内修改 |
| 方向键 | 短按原生方向；长按上/下调整系统音量 |

RC003 的独立返回键和音量 +/- 键在已验证的 Windows 蓝牙栈中没有上报可用事件，因此言灵不展示这些按键，也不提供不稳定的组合键。

## 系统要求

- Windows 10 或 Windows 11，64 位。
- 小米 RC003 / MI RC 蓝牙语音遥控器。
- 可用的 Bluetooth LE 适配器。
- 微信输入法，并启用语音输入快捷键 `Ctrl + Win`。
- VB-CABLE，需从其官方网站单独安装。

当前版本为 Windows Alpha，尚未进行代码签名。首次运行时 Windows 可能显示来源提示，请确认文件来自本仓库的 Releases 页面。

## 开发与构建

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

生成发布包：

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_RELEASE.ps1
```

详细架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 开源与第三方

Vibe Flow 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在本项目中，其许可和安装包由 VB-Audio 提供。其他依赖和协议研究来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

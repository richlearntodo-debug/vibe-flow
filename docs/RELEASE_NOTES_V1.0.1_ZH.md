# 言灵 · Vibe Flow Remote 1.0.1

这是 V1 正式版的教程与易用性更新。它补齐普通用户从下载到排障所需的说明和真实界面截图，不改变已经通过 RC003 真机验证的语音传输参数。

![言灵 1.0.1 总览](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.1/docs/images/01-overview.png)

## 下载哪个文件

- `VibeFlow-Setup.exe`：推荐。按当前 Windows 用户安装，支持开始菜单、覆盖升级和标准卸载。
- `Vibe-Flow-Windows-x64.zip`：免安装版。完整解压后运行 `VibeFlow.exe`，不要在压缩包内直接运行。
- `SHA256SUMS.txt`：两个发布文件的 SHA-256 校验值。

安装包尚未进行商业代码签名。首次运行出现 Windows 来源提示时，请确认下载地址属于 `richlearntodo-debug/vibe-flow`，并核对 SHA-256。

## 这次更新

- 将中文教程重组为普通用户可以逐项完成的五步配置流程。
- 增加微信输入法、Typeless、Windows 语音输入、Voquill 和其他全局快捷键工具的独立配置说明。
- 增加“现象、检查、处理方法”排查表，覆盖安装、VB-CABLE、蓝牙、快捷键、无文字、延迟、轻声、旧实例和按键边界。
- 新增真实展开的转写工具截图，并在按键页加入 Home、方向键、功能键和 TV 的默认操作速查。
- 明确 RC003 独立音量 +/- 和返回键在已验证 Windows 蓝牙链路中没有稳定事件；音量继续使用长按方向上 / 下。

![支持的转写工具](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.1/docs/images/06-transcription-tools.png)

![遥控器按键速查](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.1/docs/images/03-shortcuts.png)

## 升级说明

安装版可以直接运行新安装包覆盖升级。免安装版请解压到新目录后运行 `VibeFlow.exe`。程序会保留已有的转写工具、快捷键、开机启动、声音反馈和语音参数配置。

升级后在“偏好设置”确认版本为 `1.0.1`，再到“连接与自检”运行一次检查。若诊断里的程序路径仍指向旧目录，请从系统托盘退出旧实例，再从开始菜单打开新版本。

## 语音链路保持不变

本次没有修改稳定档案 v11：`1.0x` 灵敏度、清晰增强、180 ms 排空、`CABLE Input` 播放端、自动默认麦克风路由、音频排空后恢复原麦克风。正常听写不保存录音，不读取转写文字，也不自行上传音频。

## 发布验证

- 三个 Windows 组件从源码重新编译，仓库确定性检查通过。
- v11 语音处理自测、VB-CABLE WASAPI 时钟测试和可逆默认录音端点测试通过。
- 安装版、免安装版、覆盖升级、配置保留、启动和退出流程完成冒烟测试。
- 七张教程截图均来自真实应用窗口；六张主窗口截图为 1280 x 840，首次设置对话框保留原生尺寸。工具列表、按键速查、文字截断和隐私信息已人工检查。

## 已知边界

- 仅支持 Windows 10/11 x64 和已验证的 RC003 / MI RC 遥控器，不提供 Android APK。
- VB-CABLE 是唯一必须额外安装的本地驱动，因第三方许可不包含在发布包中。
- 独立返回键和音量 +/- 键没有上报稳定 Windows 事件；系统音量使用长按方向上 / 下。
- Voquill 和其他第三方工具需要根据已安装版本核对全局快捷键；其账号、网络、识别和隐私策略由对应工具负责。
- 安装包尚未进行商业代码签名，请只从本仓库 Releases 下载并核对 SHA-256。

完整安装、配置、验收、问题排查和卸载步骤见 [中文使用教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.0.1/docs/USER_GUIDE_ZH.md)。

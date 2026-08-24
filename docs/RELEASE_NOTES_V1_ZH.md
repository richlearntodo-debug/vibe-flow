# 言灵 · Vibe Flow Remote 1.0.0

这是首个面向日常使用的 Windows 正式版。言灵把小米 RC003 / MI RC 遥控器麦克风的实时声音交给微信输入法、Typeless、Windows 语音输入、Voquill 或其他快捷键驱动的转写工具，并保留这些工具原有的识别与文字整理能力。

![言灵 V1 总览](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.0/docs/images/01-overview.png)

## 下载哪个文件

- `VibeFlow-Setup.exe`：推荐，按当前 Windows 用户安装，支持开始菜单、覆盖升级和标准卸载。
- `Vibe-Flow-Windows-x64.zip`：免安装版，完整解压后运行 `VibeFlow.exe`。
- `SHA256SUMS.txt`：上述两个文件的 SHA-256 校验值。

安装包尚未进行商业代码签名。首次运行出现 Windows 来源提示时，请确认下载地址属于 `richlearntodo-debug/vibe-flow`，并核对 SHA-256。

## V1 重点

- 固化真机验证的 v11 语音链路：`1.0x`、清晰增强、180 ms 排空、自动默认麦克风路由和异常恢复标记。
- 首次打开提供五步向导：选择转写工具、安装 VB-CABLE、连接 RC003、匹配快捷键、完成真实听写。
- 支持微信输入法、Typeless、Windows 语音输入、Voquill 和通用自定义快捷键工具。
- “连接与自检”覆盖本地组件、VB-CABLE、稳定档案、后台桥接、RC003、转写工具和最近一次端到端听写。
- 首页遥控器会对真实按键、收音、整理、完成和失败状态提供颜色、光效和声音反馈。
- 按键配置页提供遥控器位置示意，只展示 RC003 真机已经验证的单击与长按操作。
- 正式 Windows 安装器不会在覆盖升级时改写用户配置；卸载时会移除言灵产生的配置、日志和一次性诊断音频。

## 五步开始

1. 安装并打开言灵，选择每天使用的转写工具。
2. 按页面安装 VB-CABLE，确认 `CABLE Input` 和 `CABLE Output` 都已检测到。
3. 在 Windows 蓝牙中配对 `MI RC` / RC003，回到言灵等待连接就绪。
4. 确认言灵与转写工具内部使用完全相同的快捷键和触发方式。
5. 聚焦测试输入框，按住遥控器录音键说完一句话后松开。

完整步骤、截图与故障排查见 [中文使用教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.0.0/docs/USER_GUIDE_ZH.md)。

## 发布验证

- 三个 Windows 组件均从源码重新编译。
- v11 确定性语音管线自测通过。
- Windows Console、Multimedia、Communications 三个默认录音角色完成可逆路由测试并全部恢复。
- WASAPI 时钟测试发送 5000 ms 音频，队列丢包为 0，结束后待处理音频为 0。
- 安装、覆盖升级、配置保留、后台启动、正常退出与卸载流程通过。
- 六个主要界面在 1280 x 840 实拍通过，小窗口使用滚动区域保证控件可访问。

![言灵 V1 自检](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.0/docs/images/04-diagnostics.png)

## 已知边界

- 仅支持 Windows 10/11 x64；这不是 Android APK。
- VB-CABLE 是当前架构唯一必须额外安装的本地驱动，因许可原因不包含在发布包中。
- RC003 的独立返回键和音量 +/- 键在已验证的 Windows 蓝牙栈中没有上报稳定事件，因此不宣称支持；系统音量使用长按方向上/下。
- 不提供不稳定的组合键。TV 键打开任务切换器后，使用方向键选择并按确认进入。
- Voquill 与其他第三方客户端需要用户按其当前版本设置全局快捷键；账号、网络、识别结果和隐私政策由对应工具负责。

言灵默认只在本机解码和转发声音，不保存普通听写录音，不读取转写文字，也不自行上传音频。只有用户明确确认“诊断下一段音频”时，下一段最长 30 秒的分段 WAV 才会保存在本机。

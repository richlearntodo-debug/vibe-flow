# 言灵 · Vibe Flow Remote V1.5 快速开始

日常操作：**单击输入框，按住录音键说话，松开结束，检查文字后按确认键发送。**

当前稳定模式遵循 RC003 的物理按键周期，单段约 `60 秒`；提前松开立即结束，不会自动创建第二个麦克风会话。

## 下载

普通用户请下载 [V1.5.0 安装版](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/VibeFlow-Setup.exe)。不要下载 GitHub 自动生成的源码 ZIP。[所有版本固定入口](docs/VERSION_ARCHIVE_ZH.md)。

## 首次设置 5 项

1. 确认 Windows 10/11、RC003 和按住说话方式；
2. 配对 `MI RC` / RC003，并按方向键验证真实设备事件；
3. 安装或检查 VB-CABLE，确认 `CABLE Input` 与 `CABLE Output`；
4. 选择微信输入法、Typeless、豆包输入法、Windows 语音输入或其他工具，并完成真实转译；
5. 选择是否随 Windows 启动，查看最终汇总。

![首次设置](docs/images/00-setup-01-device.png)

## 微信输入法推荐配置

- 全局快捷键：`Ctrl + Win`；
- 触发方式：单击切换；
- 启动等待：`80 ms`；
- 麦克风输入：`CABLE Output`。

其他工具以客户端实际快捷键为准，言灵与工具两边必须完全一致。

## 快捷键

打开“快捷键”页后，可以切换 Profile、绑定 APP、选择动作，或点击“录制键盘快捷键”并直接按下目标组合。Smart Profiles 默认关闭，需要按前台应用自动切换时再开启。

![快捷键配置](docs/images/03-shortcuts.png)

开机、返回和独立音量键没有稳定 Windows 事件，V1.5 不提供映射。录音键固定使用稳定链路，不参与自定义。

## 遇到问题

打开“自检”，从第一项橙色或红色结果开始处理。反馈时导出诊断包；日志不包含录音或转译文字。

[完整 V1.5 图文教程](docs/V1_5_USER_GUIDE_ZH.md)

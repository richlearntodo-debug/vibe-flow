# 言灵 · Vibe Flow Remote v1.3.0

这是快捷键可靠性与新手体验升级版。录音继续使用 v1.2.1 已验证的 `v1.0.3` 内核和约 60 秒按住说话方式，没有加入长录音或新的语音状态机。

## 下载

| 文件 | 直接下载 |
| --- | --- |
| **推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.3.0/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.3.0/Vibe-Flow-Windows-x64.zip) |
| 完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.3.0/SHA256SUMS.txt) |

普通用户下载第一行。GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。

## 新版亮点

- 11 个技术步骤合并为 5 项首次任务，可保存进度并在安装 VB-CABLE 重启后继续。
- 四方向、确认、TV、Home 和功能键提供图形化配置与真实执行回执。
- 本机 APP 支持搜索运行中/已安装应用，并按“切换窗口、EXE、开始菜单”三级启动。
- APP 测试只有在目标窗口真实切到前台后才算成功；Windows 前台锁会使用受控回退处理。
- 旧开机键 APP/网页动作会迁移到空闲的 Home 长按；开机键本身继续禁用。
- 新增通用导航、Vibe Coding、媒体控制三套简单方案。
- 新增配置备份、导入和恢复上次；升级与导入保留稳定语音参数。
- 新增白天、夜间、跟随 Windows 三种主题。
- 新增 Per-Monitor V2 DPI 支持，高 Windows 显示缩放不再仅靠模糊的整窗放大。
- 诊断导出默认隐藏用户路径、设备身份、地址、URL 和应用目标。
- 普通键盘不会触发 RC003 动作；自检会显示真实设备边沿和动作边沿。没有签名过滤器时使用安全直通，遥控器原始键效果可能同时发生。

当前 RC003 稳定单段最长约 `60 秒`。提前松开立即结束，到达硬件边界后开始下一段。

## 使用

聚焦输入框 -> 按住录音键说话 -> 松开等待转译 -> 确认键发送。

首次使用请完成应用内 5 项设置；异常时运行 10 项一键自检。版本 `1.3.0`、配置 schema `29`、onboarding `9`。已移除会误接管实体键盘同名键的实验兼容模式，也移除了被真机证伪的 Hook/Raw 配对路径；历史配置会自动迁移。

[v1.3 图文教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.3.0/docs/V1_3_USER_GUIDE_ZH.md) · [所有历史版本固定下载](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.3.0/docs/VERSION_ARCHIVE_ZH.md)

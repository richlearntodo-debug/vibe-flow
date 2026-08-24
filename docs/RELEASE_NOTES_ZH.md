# 言灵 · Vibe Flow Remote 1.0.2

这是正式版的下载与教程整理更新。它让普通用户更容易找到正确安装包，并移除发布包内重复的历史说明；已经通过 RC003 真机验证的语音传输参数和按键行为保持不变。

![言灵 1.0.2 总览](https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v1.0.2/docs/images/01-overview.png)

## 推荐下载

- [`VibeFlow-Setup.exe`](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/VibeFlow-Setup.exe)：推荐给大多数用户，支持开始菜单、覆盖升级和标准卸载。
- [`Vibe-Flow-Windows-x64.zip`](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/Vibe-Flow-Windows-x64.zip)：免安装版，完整解压后运行 `VibeFlow.exe`。
- [`SHA256SUMS.txt`](https://github.com/richlearntodo-debug/vibe-flow/releases/latest/download/SHA256SUMS.txt)：用于校验上述两个文件的完整性。

请不要下载 GitHub 自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`。它们只是源码快照，不是面向普通用户的 Windows 安装包。

## 本次更新

- 在仓库首页和教程首屏突出推荐安装包，并明确区分安装包、免安装包和源码压缩包。
- 为首次安装、覆盖升级、故障排查和按键查询提供直接的教程入口。
- 将多个历史版本说明统一为 `RELEASE_NOTES_ZH.md`；发布包只保留当前说明，覆盖升级时也会删除安装目录内遗留的版本化说明。
- 加强构建校验，确保安装包、教程、截图、版本号与发布说明保持一致。
- 修复后台语音服务仍在正常退出时，静默覆盖升级可能过早报“无法关闭应用”并中止的问题。
- 保留 `v1.0.0` 和 `v1.0.1` Release 作为回退记录，不把旧版本混入最新版下载包。

## 升级说明

安装版可以直接运行新安装包覆盖升级。免安装版请解压到新目录后运行 `VibeFlow.exe`。程序会保留已有的转写工具、快捷键、开机启动、声音反馈和语音参数配置。

升级后在“偏好设置”确认版本为 `1.0.2`，再到“连接与自检”运行一次检查。若诊断里的程序路径仍指向旧目录，请从系统托盘退出旧实例，再从开始菜单打开新版本。

## 语音链路保持不变

本次没有修改稳定档案 v11：`1.0x` 灵敏度、清晰增强、180 ms 排空、`CABLE Input` 播放端、自动默认麦克风路由、音频排空后恢复原麦克风。正常听写不保存录音，不读取转写文字，也不自行上传音频。

## 发布验证

- Git 远端、默认分支、`releases/latest` 跳转和公开下载路径已验证。
- 三个 Windows 组件从源码重新编译，仓库确定性检查通过。
- v11 语音处理自测、VB-CABLE WASAPI 时钟测试和可逆默认录音端点测试通过。
- 安装版、免安装版、覆盖升级、配置保留、启动和退出流程完成冒烟测试。
- 线上发布验收包含从 GitHub 重新下载安装包与 ZIP，并与 `SHA256SUMS.txt` 和 Release 文件摘要交叉校验。

## 已知边界

- 仅支持 Windows 10/11 x64 和已验证的 RC003 / MI RC 遥控器，不提供 Android APK。
- VB-CABLE 是唯一必须额外安装的本地驱动，因第三方许可不包含在发布包中。
- 独立返回键和音量 +/- 键没有上报稳定 Windows 事件；系统音量使用长按方向上 / 下。
- Voquill 和其他第三方工具需要根据已安装版本核对全局快捷键；其账号、网络、识别和隐私策略由对应工具负责。
- 安装包尚未进行商业代码签名，请只从本仓库 Releases 下载并核对 SHA-256。

完整安装、配置、验收、问题排查和卸载步骤见 [中文使用教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v1.0.2/docs/USER_GUIDE_ZH.md)。

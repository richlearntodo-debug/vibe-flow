# Vibe Link V2.0 正式版安装器说明

> 当前安装器只交付 Vibe Flow 的语音输入、快捷键、语音、自检和设置页面。便签本、Notes Deck 及便签 AI 不属于当前安装包功能。

## 下载

- [VibeFlow-Setup.exe](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/VibeFlow-Setup.exe)
- [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/Vibe-Flow-Windows-x64.zip)
- [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/SHA256SUMS.txt)

以上是正式版入口。安装器**未签名**（未配置商业 Authenticode 签名），首次运行可能出现 SmartScreen「未知发布者」提示：点「更多信息」→「仍要运行」即可继续；请核对 SHA-256，不要绕过 Windows 安全机制。

## 安装前

- 支持目标：Windows 10 / 11 x64。
- 准备 RC003 / MI RC、蓝牙和一个语音工具；VB-CABLE 由应用内向导检测或由用户确认安装。
- 安装器保留现有目录和用户配置，不静默安装第三方语音工具、驱动或设备过滤器。
- 当前候选未配置商业代码签名。请核对 `SHA256SUMS.txt`，不要绕过系统安全机制。

## 页面与职责

安装器显示产品用途、按住说话流程、隐私边界、系统架构、旧版本、配置保护、安装目录和桌面快捷方式。安装进度区分应用文件、旧配置迁移、启动设置恢复和完成阶段。

完成页提供“启动并开始设置”和“打开使用教程”；教程打开候选包内的同版本文档。遥控器、VB-CABLE、语音工具和真实听写由应用内唯一一套五任务向导完成。

## 升级与卸载

- 升级前建议从托盘退出旧版。
- 配置迁移使用 Host JSON 解析器，并按中央主配置、中央备份、旧安装主配置、旧安装备份回退；不会删除历史用户数据。
- 启动项只按用户原设置恢复；失败会中止并显示原因，不伪造完成。
- Host 未能在协作退出期限内结束时，安装或卸载会停止，不覆盖运行中的文件。
- 卸载会询问是否保留本地数据，默认选择保留；选择删除时会明确说明按键、语音工具、Profile 和工作流无法由卸载器恢复。

全新安装、V1.5 升级、二次升级和卸载尚未在一次性 Windows 账户完成，详见[真机与系统测试清单](V2_0_HARDWARE_TEST_MATRIX_ZH.md)。

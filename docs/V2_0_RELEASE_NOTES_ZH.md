# 言灵 · Vibe Flow Remote V2.0.0 候选版 2 更新说明

发布日期：2026-09-12  ·  标签：`v2.0.0-candidate.2`  ·  状态：公开候选版  ·  Release status: candidate

> V2.0.0 candidate.2 已提供安装包和便携 ZIP，但还不是正式稳定版。完整硬件、VB-CABLE、DPI 和安装生命周期验收仍需在目标环境完成。

## 下载

- [VibeFlow-Setup.exe](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/VibeFlow-Setup.exe)
- [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/Vibe-Flow-Windows-x64.zip)
- [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/SHA256SUMS.txt)
- [GitHub Release 页面](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v2.0.0-candidate.2)

## 本候选版更新

### 1. 目标输入框和 Smart Focus

- 可以为 ChatGPT、浏览器、编辑器、终端等应用学习可编辑输入控件。
- 保存前必须立即测试；未验证的目标不会被标记为当前目标。
- 描述符只保留进程和控件识别信息，不保存输入框文字、窗口标题正文或屏幕坐标。
- 用户刚刚手动点击的有效输入框优先；录音过程中不弹窗、不抢焦点。

### 2. 录音与语音反馈

- 继续使用 V1.5 的按住录音、松开结束行为。
- 语音页显示最近一次真实收音电平，帮助区分“声音太小”和“语音工具未唤起”。
- 录音结束只表示 Capture 已结束收音，不显示为“转写完成”。
- 松开录音键不自动按 Enter；用户必须检查文字并手动按确认键。

### 3. 快捷键和 Profiles

- 保留 V1.5 的 Profiles、实体键映射和键盘组合录制。
- 可配置键支持短按、长按、双击；没有独立配置的层会明确显示跟随关系。
- 录音键仍固定在稳定语音链路，不参与普通映射或手势层。
- 电源键可配置，但其扫描码与蓝牙重连后的麦克风键形态存在冲突，重视录音可靠性时建议保持不执行动作。
- 配置写入后等待 Bridge revision ACK，再显示已生效。

### 4. 首页、HUD、Deck 和自检

- 首页集中显示设备、语音桥接、Profile、目标和下一步。
- Live HUD 只显示可验证的状态，不把派发写成外部完成。
- Context Deck 为只读说明面板，不执行第二次遥控器动作。
- 自检结果包含证据、原因、影响、错误码、修复入口和重新检测。

### 5. 首次设置

统一为五项任务：了解流程、连接遥控器、准备音频、选择语音工具、完成个性化。允许稍后处理并保留进度，不以点击按钮冒充硬件通过。

## 语音工具边界

- 可选语音工具为微信输入法（默认，`Ctrl + Win`，单击切换）、八哥说（右 Alt，单击切换）、讯飞语音输入法（`Ctrl + Shift + Alt + [`，按住触发）和其他语音工具。
- 讯飞语音输入法为本候选版新增支持。请在讯飞输入法「设置 → 语音」把语音快捷键设为 `Ctrl + Shift + Alt + [`（讯飞安装后的默认值），并选择「长按说话」；Vibe Flow 里保存的快捷键和触发方式必须与它完全一致。
- 微信输入法部分版本会把识别结果放到剪贴板，属于第三方工具行为。候选版可能在目标已验证且收到真实完成回执后派发一次 `Ctrl+V` 兼容动作，但不保证所有目标都能成功粘贴；“粘贴动作已派发”不等于 AI 已收到。
- Typeless 和 Windows 语音输入不再作为 V2.0 的语音工具选项，保存的旧值会在加载时迁移为微信输入法并按名称提示用户。豆包输入法同样不再作为 V2.0 自动配置项。

## 稳定边界

- Capture 版本 `1.2.1.0`、录音内核 `v1.0.3`、`gain=1.0`、`speech`、`drainMs=180` 和约 60 秒设备边界保持不变。
- 不新增 MIC_EXTEND、自动续接、点击切换录音、自动 Enter、网页正文读取或任意脚本执行。
- 不读取、保存或上传普通转写文字，不自动发送 AI 消息。
- Notes Deck、便签 AI、Project Spaces、Capture & Ask 和 Browser Remote Lite 不属于当前主导航功能，不作为本候选版的交付承诺。

## 验证状态

自动化构建、源码契约、Host/Bridge/Capture 自检、候选附件哈希和本机 UI 资源检查已有证据。以下项目仍需目标设备人工验证，当前均标记为“未验证”：RC003 长时间按住/松开、设备级 F5 隔离、真实 VB-CABLE 回路、蓝牙重连/睡眠恢复、Windows 10/11、125%/150%/200% DPI、全新安装/升级/卸载，以及第三方语音工具在实际 ChatGPT 输入框中的最终文字落点。

详细步骤见：[V2.0 图文使用教程](V2_0_USER_GUIDE_ZH.md) 和 [V2.0 FAQ](V2_0_FAQ_ZH.md)。

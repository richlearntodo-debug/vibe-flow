# 言灵 · Vibe Flow Remote V2.0 快速开始

日常操作只有一条主路径：**打开目标 APP → 点击或锁定输入框 → 按住录音键说话 → 松开 → 检查文字 → 按确认键发送**。

RC003 单段录音约 `60 秒`，提前松开会立即结束，不会自动创建第二个会话。

## 直接下载 V2.0

- [安装版 VibeFlow-Setup.exe](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/VibeFlow-Setup.exe)
- [便携版 Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/Vibe-Flow-Windows-x64.zip)
- [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/SHA256SUMS.txt)

V2.0.0 candidate.2 是公开候选版，不是正式稳定版。需要稳定版本时使用 [V1.5.0](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.5.0)。不要下载 GitHub 自动生成的源码 ZIP 代替应用程序。

> **录音键隔离（安装前请先读）**：未安装签名设备过滤器前，录音键使用“遥控器在线范围内”的钩子隔离——遥控器已连接时 F5 被拦截；遥控器**不在线**时普通键盘的 F5 **原样直通**；该机制不能与签名过滤器等同。

## 首次设置五项

1. 了解按住说话、松开结束、检查文字和手动发送；
2. 配对 `MI RC` / RC003，并按方向键验证真实遥控器事件；
3. 检查 VB-CABLE 的 `CABLE Input` / `CABLE Output` 方向；
4. 选择语音工具，设置与客户端一致的快捷键，并完成一次真实听写；
5. 选择后台、托盘和 Smart Profiles 行为，未完成项目可以稍后处理。

![首次设置](docs/images/00-setup-01-device.png)

## 推荐的第一次听写

1. 打开 ChatGPT，在消息输入框中点击出插入光标。
2. 在“工作流”页添加 ChatGPT，点击“学习目标输入框”。
3. 在 ChatGPT 输入框中点击一次，等待学习完成。
4. 点击“立即测试”和“设为当前”，看到目标已验证。
5. 按住 RC003 录音键说一句短句，松开等待语音工具处理。
6. 目视检查文字，按确认键发送一次。

需要排查“文字进剪贴板”时，优先用 Windows 语音输入（`Win+H`）做对照。微信输入法部分版本会把识别结果放入剪贴板，这是第三方行为；Vibe Flow 不读取转写文字，也不保证所有微信输入法目标都能直写。

## VB-CABLE 方向

```text
RC003 麦克风 -> Vibe Flow -> CABLE Input
                                  |
               语音工具麦克风 <- CABLE Output
```

Vibe Flow 播放端选择 `CABLE Input`，语音工具的麦克风输入选择 `CABLE Output`。自检显示“已安装”后仍需确认端点未静音、输入级别可用并完成真实听写。

## 快捷键

“快捷键”页保留 V1.5 的 Profiles 和映射，可为上/下/左/右、确认、Home、TV、功能键设置动作和短按/长按/双击。电源键（Windows 上报为 VK 0xFF / 扫描码 E0 5E）也有配置入口，但与蓝牙重连后的麦克风键存在扫描码取舍，重视录音可靠性时保持“不执行动作”。返回键和独立音量键在当前 RC003 / Windows 组合中没有稳定事件，不提供映射。录音键固定在稳定语音链路，不参与自定义。

![快捷键配置](docs/images/03-shortcuts.png)

## 出问题先做什么

1. 打开“自检”，从第一个警告或错误开始处理；
2. 查看语音页的最近一次收音电平；低于 10% 时靠近遥控器、对准麦克风孔并检查 `CABLE Output` 级别；
3. 确认目标输入框仍有光标或 Smart Focus 状态为“已验证”；
4. 保存快捷键后等待 Bridge ACK；
5. 仍无法解决时导出诊断，反馈版本、错误码、语音工具和最短复现步骤。

完整教程：[V2.0 图文使用教程](docs/V2_0_USER_GUIDE_ZH.md) · [V2.0 FAQ](docs/V2_0_FAQ_ZH.md) · [V2.0 已知限制](docs/V2_0_KNOWN_LIMITATIONS_ZH.md)

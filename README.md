# Vibe Link

<p align="center">
  <img src="docs/images/vibe-flow-community.png" alt="Vibe Link 用户社群二维码" width="100%">
</p>

<p align="center">
  <strong>按住遥控器说话，松开后检查文字，再手动确认发送</strong><br>
  把小米 RC003 / MI RC 变成 Windows 上可检查、可恢复的语音输入工具
</p>

## V2.0.0 离键闭环正式版

> 当前正式版本为 `v2.0.0`。

V2.0 正式版已经公开发布。它保留 V1.5 的录音、快捷键、Profiles 和配置保护，并增加了目标输入框学习、统一反馈、Live HUD、Context Deck、五项首次设置和更完整的自检。

> **更名说明**：应用现正式命名为 **Vibe Link**（原「言灵 Vibe Flow Remote」）。由于兼容旧版本，GitHub 仓库仍名为 `vibe-flow`，安装目录仍为 `Vibe Flow Remote`，部分历史文件名也会保留旧名称。搜索、下载和使用时请认准 Vibe Link。

> [!WARNING]
> 正式版使用已核验的 V2.0.0 出货构建；RC003 长时间回归、VB-CABLE 首次安装与重启恢复、完整 DPI 观感和第三方语音工具组合仍需在具体目标设备上复测。未完成的硬件项目请以“待验证”理解，不要把自动化门禁当作真机证明。
>
> **录音键隔离**：未安装签名设备过滤器时，录音键使用“遥控器在线范围内”的钩子隔离；遥控器在线时 F5 会被拦截，遥控器不在线时普通键盘 F5 原样直通。该机制不能与签名过滤器等同，详情见[已知限制](docs/V2_0_KNOWN_LIMITATIONS_ZH.md)。

### V2.0 下载

| 文件 | 适合谁 | 固定下载入口 |
| --- | --- | --- |
| **VibeFlow-Setup.exe** | 普通用户，推荐 | [下载 V2.0 安装包](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/VibeFlow-Setup.exe) |
| **Vibe-Flow-Windows-x64.zip** | 免安装、便携使用 | [下载 V2.0 便携版](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/Vibe-Flow-Windows-x64.zip) |
| **SHA256SUMS.txt** | 下载后校验完整性 | [下载 V2.0 校验清单](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/SHA256SUMS.txt) |
| Release 页面 | 查看正文、截图和全部附件 | [打开 V2.0.0 正式版](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v2.0.0) |

候选版归档（仅用于复现历史测试）：[V2.0.0 candidate.3](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v2.0.0-candidate.3)。日常使用请下载上面的正式版。

> 安装版和便携版均未配置商业 Authenticode 签名，Windows 可能显示 SmartScreen「未知发布者」提示：点「更多信息」→「仍要运行」即可继续。请只从上面的 GitHub Release 下载，并用同一批 `SHA256SUMS.txt` 核对文件。GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是可运行程序。

### 上一代稳定版 · V1.5.0

如果你需要已经完成 V1.5 稳定验收的版本，请使用下面的固定入口：

| 文件 | 固定下载入口 |
| --- | --- |
| 安装版 | [V1.5 VibeFlow-Setup.exe](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/VibeFlow-Setup.exe) |
| 便携版 | [V1.5 Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/Vibe-Flow-Windows-x64.zip) |
| 校验清单 | [V1.5 SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.5.0/SHA256SUMS.txt) |

## V2.0 能做什么

Vibe Link 不是代码编辑器，也不是 AI 客户端。它负责把遥控器、语音工具和你当前的输入框连接起来：

1. 先打开 ChatGPT、编辑器、终端或其他文本应用。
2. 通过 Smart Focus 学习并验证一个可编辑输入框，或者手动点击输入框。
3. 按住 RC003 录音键说话，松开结束。
4. 等待语音工具处理，目视检查文字。
5. 按确认键手动发送。松开录音键不会自动按 Enter。

V2.0 还提供快捷键 Profiles、三层手势、Live HUD、Context Deck、统一自检和首次设置向导，帮助你少找窗口、少猜状态，但它不会读取网页正文、自动运行命令、自动接受 AI 修改或代替你发送消息。

## 五分钟跑通第一次输入

1. 下载并安装 [V2.0 安装包](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0/VibeFlow-Setup.exe)。
2. 在首次设置中完成：了解流程、连接遥控器、准备音频、选择语音工具、完成个性化。
3. 配对 `MI RC` / `RC003`，按一次方向键确认 Windows 收到了遥控器事件。
4. 如果使用 RC003 麦克风，确认存在 `CABLE Input` 和 `CABLE Output`：播放端是 `CABLE Input`，语音工具的麦克风端是 `CABLE Output`。
5. 在“语音”页选择语音工具：微信输入法（默认，`Ctrl + Win`，单击切换）、网易八哥说（右 Alt，单击切换）或其他语音工具（默认右 Shift，单击切换）。应用里的快捷键和触发方式必须与所选工具自己的设置一致。讯飞语音输入法、搜狗输入法和豆包输入法在 V2.0 已不再提供；旧配置里的这些值会在加载时迁移为微信输入法并提示一次。微信输入法的语音面板行为由第三方工具决定，可能把文字放到剪贴板。
6. 打开 ChatGPT，在消息输入框中看到插入光标。
7. 在“工作流”页添加 ChatGPT，点击“学习目标输入框”，再点击“立即测试”和“设为当前”。
8. 按住遥控器录音键说话，松开后检查文字；确认无误后按确认键发送一次。

![首页：设备、语音和下一步](docs/images/01-overview.png)

完整步骤见：[V2.0 图文使用教程](docs/V2_0_USER_GUIDE_ZH.md)。

## 主要功能与教程入口

| 功能 | 作用 | 详细教程 |
| --- | --- | --- |
| 首次设置 | 逐步检查遥控器、音频、语音工具和后台设置 | [首次设置](docs/V2_0_USER_GUIDE_ZH.md#首次设置五项任务) |
| Smart Focus | 学习、测试并锁定正确的输入框 | [目标输入框](docs/V2_0_USER_GUIDE_ZH.md#smart-focus目标输入框) |
| 语音输入 | 按住录音、松开结束、目视检查、手动发送 | [日常语音输入](docs/V2_0_USER_GUIDE_ZH.md#日常语音输入闭环) |
| 快捷键与 Profiles | 保留 V1.5 映射，配置短按、长按、双击和 Profile | [快捷键页面](docs/V2_0_USER_GUIDE_ZH.md#快捷键与profiles) |
| Live HUD / Context Deck | 查看设备、Profile、目标输入框和最近动作，不替代主流程 | [状态反馈](docs/V2_0_USER_GUIDE_ZH.md#live-hud与context-deck) |
| 自检 | 显示证据、原因、影响、修复入口和错误码 | [自检排错](docs/V2_0_USER_GUIDE_ZH.md#自检与故障排查) |
| VB-CABLE | 检查录音端和语音工具端的方向 | [音频配置](docs/V2_0_USER_GUIDE_ZH.md#vb-cable与音频通道) |
| 常见问题 | 处理剪贴板、焦点、F5、无音频、按键不生效等问题 | [FAQ](docs/V2_0_FAQ_ZH.md) |

## 页面预览

| 首页 | 语音页 |
| --- | --- |
| ![首页](docs/images/01-overview.png) | ![语音页](docs/images/06-voice.png) |

| 快捷键 | 自检 |
| --- | --- |
| ![快捷键页](docs/images/03-shortcuts.png) | ![自检页](docs/images/04-diagnostics.png) |

| 设置 | 首次设置 |
| --- | --- |
| ![设置页](docs/images/05-settings.png) | ![首次设置](docs/images/00-setup-01-device.png) |

| 快捷动作选择 | Smart Profiles 应用绑定 |
| --- | --- |
| ![快捷动作选择](docs/images/07-shortcut-actions.png) | ![Smart Profiles 应用绑定](docs/images/09-smart-profile-apps.png) |

## 稳定边界与隐私

Capture 文件继续显示 `1.2.1.0`，这是刻意冻结的稳定语音组件。

- 录音固定为 `hold`：按住开始，松开结束；单段时长仍由 RC003 设备控制，约 60 秒。
- Capture 版本和稳定参数保持冻结：`1.2.1.0`、`v1.0.3`、`gain=1.0`、`speech`、`drainMs=180`。
- 录音键固定在稳定语音链路，不参与普通快捷键映射、手势分层、HUD 或 Smart Focus 动作。
- 返回键与独立音量键在当前 RC003 / Windows 组合中没有稳定事件，不提供映射。
- Vibe Link 不自研语音识别，不读取或保存普通转写文本，不读取网页正文，不自动发送 AI 消息。
- Smart Focus 只保存进程名、控件类型、AutomationId、类名和稳定结构指纹，不保存输入框当前文字、窗口标题正文或屏幕坐标。
- 微信输入法可能按第三方工具自己的方式把识别结果放入剪贴板；Vibe Link 不保证所有第三方输入法都能直写。需要完全绕开剪贴板时，请改用其他语音工具并完成实际测试。
- 未检测到 VB-CABLE（免驱动模式）时，主机能自行唤起网易八哥说和自定义工具；微信输入法需要它自己的语音面板，因此仍需要 VB-CABLE。
- 关闭 V2 新入口时，V1.5 的语音工具、Profiles、快捷键映射、主题和后台设置不应被重置。

## 文档与问题反馈

- [V2.0 图文使用教程](docs/V2_0_USER_GUIDE_ZH.md)
- [V2.0 FAQ 与故障排查](docs/V2_0_FAQ_ZH.md)
- [V2.0 功能看板](docs/FEATURES_ZH.md)
- [V2.0 更新说明](docs/V2_0_RELEASE_NOTES_ZH.md)
- [V2.0 已知限制](docs/V2_0_KNOWN_LIMITATIONS_ZH.md)
- [V2.0 真机测试矩阵](docs/V2_0_HARDWARE_TEST_MATRIX_ZH.md)
- [V1.5 图文教程](docs/V1_5_USER_GUIDE_ZH.md)
- [所有版本下载](docs/VERSION_ARCHIVE_ZH.md)

遇到问题时，先打开“自检”，从第一个警告或错误开始处理；反馈时附上版本号、错误码和去敏诊断日志，不要上传私人录音、转写文字或截图内容。

<p align="center">
  <img src="docs/images/vibe-flow-community.png" alt="扫码加入 Vibe Link 用户社区" width="760">
</p>

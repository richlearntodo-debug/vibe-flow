# 言灵 · Vibe Flow Remote

把小米 RC003 / MI RC 蓝牙语音遥控器变成 Windows 语音输入与快捷控制器：**聚焦输入框，按住录音键说话，松开结束，再按确认键发送。**

## v1.2.1 用户友好稳定版

**Windows 10/11 x64 · 默认白天模式 · 稳定性优先**

| 下载 | 链接 |
| --- | --- |
| 推荐安装版 | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/Vibe-Flow-Windows-x64.zip) |
| SHA-256 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/SHA256SUMS.txt) |

> [!IMPORTANT]
> 普通用户请选择 `VibeFlow-Setup.exe`。GitHub 的 `Source code (zip)` 和 `Source code (tar.gz)` 是源码，不是 Windows 安装程序。

[查看所有历史版本、亮点与固定 EXE 下载入口](docs/VERSION_ARCHIVE_ZH.md)

## 用户社区

安装、配对或语音工具配置遇到问题，可扫码加入 Vibe Flow 用户社区。

<img src="docs/images/vibe-flow-community.png" alt="Vibe Flow 用户社区二维码" width="640">

[快速开始](QUICK_START_ZH.md) · [v1.2.1 图文教程](docs/V1_2_1_TUTORIAL_ZH.md) · [完整教程](docs/USER_GUIDE_ZH.md) · [版本下载](docs/VERSION_ARCHIVE_ZH.md) · [更新说明](docs/RELEASE_NOTES_ZH.md)

![言灵 v1.2.1 首页](docs/images/01-overview.png)

## 本版变化

| 变化 | 结果 |
| --- | --- |
| **回到首个正式版录音内核** | 捕获组件使用已验证的 `v1.0.3` 实现；微信输入法保持 `Ctrl + Win`、切换触发和 `80 ms`。 |
| **可靠按住/松开** | RC003 自然 ATVV 流负责开始和结束；已移除长录音续接、`MIC_EXTEND` 和强制松开关闭逻辑。 |
| **直接写入输入框** | 先聚焦目标输入框，语音工具直接写入；言灵不读取文字，也不做剪贴板或粘贴回填。 |
| **四方向键自定义** | 上、下、左、右可分别选择一个已验证动作，新增 Windows 区域截图 `Win + Shift + S`。 |
| **TV 持久任务视图** | TV 打开 `Win + Tab` 任务视图，上下左右选择，确认键进入，再按 TV 关闭。 |
| **双主题** | 默认使用白色白天模式；夜间模式改为克制的中性色和低饱和状态色。 |
| **真实状态与自检** | 没有真实音频就不显示假波形；10 项自检提供原因和修复入口。 |

开机、返回和独立音量键没有稳定的 Windows 按键报告，因此 v1.2.1 不映射、不配置，也不宣传这些功能。

## 日常使用

1. 单击 ChatGPT、Codex、浏览器或其他应用的文本输入框，让插入光标出现。
2. 按住遥控器录音键并说话。
3. 松开后等待所选语音工具完成转译。
4. 检查文字，按中间确认键发送。

当前稳定模式遵循 RC003 的物理按键周期：单段最长约 `60 秒`。提前松开会立即结束；达到硬件边界时也会结束，不会自动创建第二个麦克风会话。

```text
RC003 microphone -> Bluetooth ATVV -> Vibe Flow
  -> CABLE Input -> CABLE Output -> selected voice tool -> focused text box
```

![语音工具与音频配置](docs/images/02-dictation.png)

## 首次设置 11 步

1. 了解按住说话、松开结束、确认发送。
2. 检查 Windows 蓝牙。
3. 配对并连接 RC003。
4. 验证真实按键事件。
5. 检查 RC003 麦克风服务。
6. 安装并检测 VB-CABLE。
7. 选择默认语音工具并核对快捷键。
8. 在内置文本框完成真实转译。
9. 保持或配置四个方向键，可将任一方向设为区域截图。
10. 选择是否随 Windows 启动。
11. 查看汇总并处理异常项。

![首次设置](docs/images/00-setup-01-intro.png)

## 语音工具

| 工具 | 推荐起点 | 说明 |
| --- | --- | --- |
| 微信输入法 | `Ctrl + Win` · 单击切换 | 默认稳定档案；可继续使用微信 AI 整理。 |
| Typeless | 常见为 `Right Alt` | 以客户端实际全局快捷键为准。 |
| 豆包输入法 | 客户端全局语音快捷键 | 两边必须完全一致。 |
| Windows 语音输入 | `Win + H` | 系统自带兼容选项。 |
| 其他语音工具 | 自定义全局快捷键 | 必须支持从全局快捷键开始和结束。 |

第三方工具不包含在安装包中，其账号、网络、识别质量和数据规则由对应软件负责。

## 已验证按键

| 实体按键 | 行为 |
| --- | --- |
| 录音键 | 按住收音，松开结束，约 60 秒硬件上限 |
| 功能键 | 短按复制，长按粘贴 |
| 上 / 下 / 左 / 右 | 默认标准方向；可分别配置一个动作或区域截图 |
| 中间确认键 | `Enter`，确认或发送 |
| Home | `Win + D`，显示桌面 |
| TV | 打开持久任务视图；方向选择，确认进入 |

![方向键配置](docs/images/03-shortcuts.png)

![区域截图配置](docs/images/03-shortcuts-screenshot.png)

## 自检、稳定性与隐私

- 自检覆盖组件、蓝牙、遥控器、按键、真实音频、VB-CABLE、稳定参数、语音工具、自启动和完整会话。
- 用户配置采用原子替换并保留 `.bak`；升级和重启不会主动覆盖配置。
- 蓝牙晚启动、睡眠恢复和解锁会触发受控重连。
- 普通日志不记录音频或转译文字，并自动限制大小。
- 诊断音频仅在用户明确确认后捕获下一段、最长 30 秒。

![一键自检](docs/images/04-diagnostics.png)

## 开发与验证

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

隔离候选包使用 `BUILD_HARDWARE_CANDIDATE.ps1`。真机验收通过前，不覆盖当前已安装稳定版、不发布安装器、不移动 Git 标签。

Vibe Flow Remote 以 [GPL-3.0](LICENSE) 发布。VB-CABLE 不包含在仓库中，由 [VB-Audio](https://vb-audio.com/Cable/) 提供。

# V2.0 Notes Deck 基线锁定

更新时间：2026-09-06

## 范围

本轮以附件 `VibeFlow_V2_0_Notes_Deck_Execution_Prompt_ZH.md`（规格标识 `V2-NOTES-DECK-20260906-R1`）为产品范围来源。V2.0 的主线是本地便签 Deck、APP 快捷入口、用户自带文本模型配置和真实状态反馈；旧 Project Space、截图提问、浏览器 Remote 和复杂工作流不再作为本轮新功能扩展。AGENTS.md 中的录音、输入、配置保护和发布约束继续有效。

## 源码基线

- 仓库：`C:\Users\Admin\Documents\ChatGPT\vibe -flow`
- 分支：`feature/v2-off-key-loop`
- 源 commit：`b47f7cdce8b753fade0c64c97332bebe80f17d2d`
- 工作区：存在用户未提交修改和未跟踪文件；本轮不执行 reset、clean、stash 或删除未知文件。
- 技术栈：WinForms + .NET Framework csc.exe；当前构建入口为 `BUILD_VIBE_MIC.cmd`、`BUILD_INPUT_BRIDGE.cmd` 和 `BUILD_DEVELOPMENT.ps1`。
- 主窗口：`scripts/VibeMic.cs` 的 `VibeMicForm`，页面通过 `scripts/ui/PageShell.cs` 分派。

## 已核对的稳定契约

- `voiceMode=hold`
- `captureSeconds=0`
- `gain=1.0`
- `autoLevel=true`
- `audioProcessingMode=speech`
- `drainMs=180`
- `autoRouteVirtualMicrophone=true`
- 默认微信工具：`ctrl+win`、`toggle`、`providerStartupDelayMs=80`
- 配置 schema：32；Bridge schema：7；稳定 Voice Profile：v11
- `DEFAULT_LONG_PRESS_MS=650`
- `HOLD_REPEAT_INITIAL_DELAY_MS=420`
- `HOLD_REPEAT_INTERVAL_MS=80`
- `VOICE_RESTART_GUARD_MS=500`
- `SMART_PROFILE_POLL_MS=250`
- `SMART_PROFILE_DEBOUNCE_MS=350`
- Capture 源码 SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`
- Capture 二进制 SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`

录音键继续走专用按住开始、松开结束路径，不进入普通映射、便签、Deck 或 Smart Focus；不读取或回填第三方转写文字，也不自动发送 Enter。

## 真实调用路径

- 配置：Host 配置加载/迁移 → Bridge 配置原子生成 → revision ACK。
- 录音：RC003 physical transition → 冻结 Capture 单 generation ATVV stream → CABLE 路由 → 第三方语音工具。
- 旧项目入口：`PageShell.BuildPage` → `BuildProjectsPage` → `ProjectSpaceStore` / `ProjectSpaceRunner` / `FocusTargetService`。
- 当前 Deck：`ContextDeckForm` 是只读遥控器状态面板；本轮应复用其状态展示原则，不让它成为第二套便签仓库。
- 首次设置：`ShowSetupWizard()` 为当前可达向导；Legacy 方法保留但不可达，不能凭名称删除。

## 基线测试证据

改造前已记录通过：

```text
npm test
BUILD_INPUT_BRIDGE.cmd
BUILD_VIBE_MIC.cmd
VibeMic.exe --self-test
VoxDeckInputBridge.exe --self-test
VibeMicAtvvCapture.exe --self-test
scripts/tests/Test-V2FeatureSuite.ps1
```

这些结果证明自动化基线，不等于已完成 RC003、VB-CABLE、微信输入法或 ChatGPT UIA 的真机验收。

## 当前已知限制

- ChatGPT 桌面客户端当前 UI Automation 树没有可验证的 `ControlType.Edit`，因此 Smart Focus 只能诚实显示“已打开/待用户确认”或“未验证”，不能显示输入框已就绪。
- 无真实 RC003、VB-CABLE 和第三方语音工具端到端证据时，不宣称麦克风、遥控器和转写落点通过。
- 旧 `project-spaces.json` 用户数据必须保留；迁移到 APP 快捷入口时不静默删除，也不再使用 `webflow` 作为产品文案。
- API Key 不进入仓库、普通 JSON、日志、导出或截图；没有 Key 时本地便签和导出必须仍可用。

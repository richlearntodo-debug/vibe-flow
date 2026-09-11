# Vibe Flow V2.0 Implementation Plan

> **历史归档（2026-09-08）：** 本文件记录早期 V2.0“离键闭环”计划，已不再作为当前产品范围。Notes Deck/便签阶段已经取消；当前执行结果和范围以 `docs/V2_0_PROGRESS.md` 及当前候选用户指南为准。

> **执行约束：** 主 Agent 逐阶段实施；每阶段修改前由 explorer 只读调查，修改后由 `vibeflow_auditor` 只读审查，并执行 `vibeflow-release-gate`。所有功能变更遵循测试先行。不得让 Subagent 修改 Capture、ATVV、Raw Input、Hook、设备过滤或稳定手势状态机。

**Goal:** 在不改变 V1.5 冻结录音、输入路由和用户配置的前提下，交付 V2.0“进入项目 → 锁定目标 → 按住说话 → 手动发送 → 浏览预览 → 截图粘贴 → 继续语音”的离键闭环候选版。

**Architecture:** 保留 .NET Framework WinForms 与现有 Host/Bridge/Capture 进程边界。先把 Host UI 外壳、状态结果与纯业务服务从 `VibeMic.cs` 增量拆开，再分别接入 Focus、Project、Capture & Ask 和浏览器模板；V2 配置放在用户数据目录的独立版本化文件中。录音键与 Capture 路径不进入任何新服务。

**Tech Stack:** C# / .NET Framework 4.x / WinForms / System.Drawing / Windows UI Automation / Win32 P/Invoke / JavaScriptSerializer / Node.js 静态门禁 / Inno Setup。

**Spec:** `docs/V2_0_UI_SPEC_ZH.md`；冻结基线见 `docs/V2_0_BASELINE_LOCK_ZH.md`。

## Global Constraints

- Capture 源码 SHA-256 固定为 `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`。
- Capture 二进制 SHA-256 固定为 `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`，版本固定 `1.2.1.0`。
- `voiceMode=hold`、`captureSeconds=0`、`gain=1.0`、`autoLevel=true`、`audioProcessingMode=speech`、`drainMs=180`。
- 输入时序固定为 650 / 420 / 80 / 500 / 250 / 350 ms。
- 不新增录音状态机、MIC_EXTEND、自动续接、自动 Enter、转写读取/存储/上传/剪贴板回填。
- 不重写 Raw Input、Hook、设备过滤、手势 generation 或修饰键清理。
- 新功能对升级用户默认不绑定、不启用、不运行；Smart Profiles 保持原值且默认关闭。
- 配置写入成功与 Bridge/runtime ACK 分开显示。
- 未经真机证据，不把 RC003、蓝牙、VB-CABLE、语音工具、浏览器或 UIA 适配器标为通过。
- 不自动下载依赖、安装驱动、修改系统音频/注册表/启动项、防火墙、签名、Push、PR 或 Release。

---

## 阶段总览

| 阶段 | 最小可独立验收交付 | 主要风险 |
| --- | --- | --- |
| 0 基线与保护 | 冻结证据、测试、计划、UI 规范、真实基线记录 | 把历史实现误当 V2 行为；误重建 Capture |
| 1 UI 基础与模块化 | 六页导航、DPI 友好壳、旧功能等价 | 单文件拆分导致生命周期/资源回归 |
| 2 统一反馈、HUD、Deck、自检 | 统一结果模型、非激活 HUD、只读 Deck | 抢焦点、双重执行、虚假成功 |
| 3 Smart Focus | 目标存储、学习、验证、执行、安全回退 | 错误目标输入、读取用户文字 |
| 4 Project Spaces | 模型/存储/向导/受约束线性执行器 | 任意命令注入、并发/取消错误 |
| 5 Capture & Ask | 当前窗口/区域截图、预览、验证目标、图片粘贴 | 隐私、剪贴板污染、盲目粘贴 |
| 6 Browser Remote Lite | 差异预览、显式应用、单步测试、撤销 | 静默覆盖 Profile、夸大浏览器能力 |
| 7 安装器与首次设置 | 清晰安装器、唯一 5 任务向导、重启恢复 | 旧用户被强制向导、配置/启动项丢失 |
| 8 候选构建 | 2.0.0 非 Capture 版本一致、候选 EXE/ZIP/SHA | 版本漂移、依赖/签名/硬件证据缺失 |

## Phase 0：基线与保护

**Files:**

- Create: `docs/V2_0_BASELINE_LOCK_ZH.md`
- Create: `docs/V2_0_IMPLEMENTATION_PLAN_ZH.md`
- Create: `docs/V2_0_UI_SPEC_ZH.md`
- Create: `docs/V2_0_PROGRESS.md`
- Modify: `README.md`（纠正录音结束与第三方转写完成的边界）
- Modify: `scripts/validate.js`
- Modify: `scripts/VibeMic.cs`（仅外围真实反馈文案与可执行会话结束分类测试）
- Forbidden: `scripts/VibeMicAtvvCapture.cs`、Bridge 输入路径

**Interfaces:** 继续使用现有 `RunHostSelfTests()`、Bridge/Capture `RunSelfTests()`、`HandleRuntimeFeedbackLine()` 和 `SetSessionFeedback()`；不新建运行时接口。

- [x] 记录 Git 身份、未提交内容、实际加载指令和远端恢复过程。
- [x] 计算冻结源码/二进制哈希并运行三个 self-test。
- [x] 构建 Host/Bridge，运行 `npm test` 与 UI 资源压力测试。
- [x] 由 explorer 确认录音/输入路径、活跃向导、配置/ACK 和构建路径。
- [x] 先用静态门禁捕获虚假完成文案，再用冻结 Capture 的真实日志样本执行 Host 分类红绿测试。
- [x] 只修正外围反馈为“等待语音工具处理 / 最终文字请目视确认”。
- [x] 完成 auditor、release gate 并把结果写入进度文档；结果为 `PASS WITH MANUAL HARDWARE CHECKS`。

**Rollback:** 对阶段 0 的 `README.md`、`scripts/validate.js`、`scripts/VibeMic.cs` 应用精确反向补丁；四份新增文档可单独移除。不得重置或覆盖未跟踪规则文件。

**Automatic acceptance:** `npm test`、Host/Bridge 构建、三个 self-test、`--ui-resource-test`；重新计算两项 Capture 哈希；确认 `git diff` 不含 Capture/Bridge。

**Hardware acceptance:** 本阶段只记录未验证，不尝试触发遥控器、音频路由或语音工具。

## Phase 1：UI 基础与模块化

**Files:**

- Create: `scripts/ui/DesignTokens.cs`
- Create: `scripts/ui/UiComponents.cs`
- Create: `scripts/ui/PageShell.cs`
- Create: `scripts/ui/HomePage.cs`
- Create: `scripts/ui/ProjectsPage.cs`
- Create: `scripts/ui/ControlsPage.cs`
- Create: `scripts/ui/VoicePage.cs`
- Create: `scripts/ui/DiagnosticsPage.cs`
- Create: `scripts/ui/SettingsPage.cs`
- Modify: `scripts/VibeMic.cs`（变为 partial；保留业务与生命周期入口）
- Modify: `BUILD_VIBE_MIC.cmd`（显式编译新增文件）
- Modify: `scripts/validate.js`
- Modify: `scripts/capture-ui-screenshots.ps1`

**Interfaces:**

```csharp
internal enum VibePageId { Home, Projects, Controls, Voice, Diagnostics, Settings }
internal sealed class UiThemePalette { /* 仅颜色、字体、间距 token */ }
internal sealed partial class VibeMicForm
{
    private Control BuildPage(VibePageId page);
    private void ShowPage(VibePageId page);
}
```

- [ ] 先写失败门禁：六页可构建、循环导航后 USER/GDI 资源受控、录音入口/Bridge 配置输出不变。
- [ ] 添加 Design Tokens 和通用卡片/按钮，不移动业务事件处理器。
- [ ] 把 `VibeMicForm` 改为 partial 并逐页迁移；每次只迁一页并运行构建/自检。
- [ ] 首页、按键、语音、自检、设置保持 V1.5 行为；项目页仅提供安全空状态，不运行新功能。
- [ ] 用布局容器替换首屏关键区域绝对坐标；保留最小尺寸、滚动和键盘导航。
- [ ] 更新截图脚本的六页定位与资源压力测试页面数。

**Risk:** 事件重复绑定、Timer/Font/Bitmap 未释放、导航索引错位、DPI 截断。

**Rollback:** 新 UI 文件独立删除；`VibeMic.cs`/构建脚本按页反向补丁恢复；不改数据 schema。

**Automatic acceptance:** `npm test`、Host 构建/self-test、`--ui-resource-test`；对六页执行 UI smoke；V2 功能未启用时 Bridge 文档 hash 与基线等价。

**Hardware acceptance:** 100/125/150/200% DPI、1366x768/1920x1080、浅/深/跟随系统；旧遥控器映射与语音链路真机回归。

## Phase 2：统一反馈、Live HUD、Context Deck 与自检

**Files:**

- Create: `scripts/features/ActionResult.cs`
- Create: `scripts/ui/LiveHudForm.cs`
- Create: `scripts/ui/ContextDeckForm.cs`
- Modify: `scripts/ui/HomePage.cs`
- Modify: `scripts/ui/DiagnosticsPage.cs`
- Modify: `scripts/VibeMic.cs`（把现有回执适配到统一模型）
- Modify: `BUILD_VIBE_MIC.cmd`、`scripts/validate.js`

**Interfaces:**

```csharp
internal enum ActionState { Idle, Checking, Running, Success, Warning, Error, Canceled }
internal sealed class ActionResult
{
    public string ActionId, ActionName, TargetId, Stage, Message, ErrorCode, RecoveryAction;
    public ActionState State;
    public DateTime StartedAtUtc, CompletedAtUtc;
}
internal interface IActionFeedbackSink { void Publish(ActionResult result); }
```

- [ ] 测试 warning/canceled 不能映射为 success，录音停止不能映射为转写完成。
- [ ] 建立单一 ActionResult 发布通道，页面、HUD、Deck 和日志只消费该通道。
- [ ] 实现非激活 HUD，验证 `ShowWithoutActivation`、窗口扩展样式和超时策略。
- [ ] 实现只读 Deck；只消费现有 Bridge 回执，不发送/重放遥控器输入。
- [ ] 把现有 10 项自检转换为原因/影响/修复/重试/错误码结构，不删除原检查。
- [ ] 录音开始时隐藏/收缩 Deck；HUD 不抢焦点。

**Risk:** HUD 激活窗口、Deck 导致双执行、状态源不一致、错误自动消失。

**Rollback:** 统一反馈由适配器包裹旧回执；关闭 HUD/Deck feature switch 后完全使用旧页面状态，不改变 Bridge/Capture。

**Automatic acceptance:** 结果状态映射单测；重复请求去重；HUD show/close 资源；Deck 输入零发送断言；Host 构建/self-test/resource test。

**Hardware acceptance:** HUD 在真实录音中不抢焦点；Deck 高亮不造成重复动作；断连/恢复/错误可见。

## Phase 3：Smart Focus

**Files:**

- Create: `scripts/features/FocusTargetModels.cs`
- Create: `scripts/features/FocusTargetStore.cs`
- Create: `scripts/features/FocusTargetService.cs`
- Create: `scripts/ui/FocusTargetDialog.cs`
- Modify: `scripts/ui/HomePage.cs`、`ProjectsPage.cs`、`ControlsPage.cs`
- Modify: `BUILD_VIBE_MIC.cmd`（加入 UIAutomationClient/UIAutomationTypes 引用）
- Modify: `scripts/validate.js`

**Interfaces:**

```csharp
internal sealed class FocusTargetDescriptor
{
    public string Id, Name, ProcessName, AutomationId, ControlType, ClassName, ParentFingerprint, Strategy;
    public DateTime? LastVerifiedUtc;
}
internal interface IRecordingPriority { bool IsRecording { get; } }
internal sealed class FocusTargetService
{
    public Task<ActionResult> ValidateAsync(string targetId, CancellationToken token);
    public Task<ActionResult> FocusAsync(string targetId, CancellationToken token);
}
```

- [ ] 测试拒绝非编辑控件、进程不匹配、失效 descriptor、超时、取消和录音优先。
- [ ] 测试日志不包含 Value、完整标题、文本或 RuntimeId。
- [ ] 实现版本化原子存储 `focus-targets.json`，备份恢复、二次迁移幂等、未知字段保留但不执行。
- [ ] 实现学习流程：隐藏主窗口、用户点击、稳定等待、捕获、立即测试，测试成功才保存。
- [ ] 执行时激活应用、查找/聚焦、验证焦点；失败不发送快捷键。
- [ ] 仅对真实 Computer Use/真机通过的 Cursor/VS Code 适配器显示“已验证”。

**Risk:** 错误进程/控件、读取敏感 Value、固定 sleep 假成功、录音中抢焦点。

**Rollback:** 删除/禁用 Focus 服务和页面入口；独立配置不影响 V1.5 config；未绑定状态下无后台工作。

**Automatic acceptance:** 纯 descriptor/存储/取消/并发/脱敏测试；UIA 使用测试替身只验证选择规则，不替代真机声明。

**Hardware acceptance:** Cursor 或 VS Code 至少一个目标连续 50 次；目标不存在不发送；录音开始中止 Focus。

## Phase 4：Project Spaces

**Files:**

- Create: `scripts/features/ProjectSpaceModels.cs`
- Create: `scripts/features/ProjectSpaceStore.cs`
- Create: `scripts/features/ProjectSpaceRunner.cs`
- Create: `scripts/ui/ProjectSpaceWizard.cs`
- Modify: `scripts/ui/ProjectsPage.cs`、`HomePage.cs`
- Modify: `BUILD_VIBE_MIC.cmd`、`scripts/validate.js`

**Interfaces:**

```csharp
internal enum ProjectStepKind { OpenOrActivateApp, OpenWorkspaceWithVerifiedAdapter, OpenUrl, SwitchProfile, FocusTarget, ShowNotification }
internal sealed class ProjectSpaceRunner
{
    public Task<ActionResult[]> RunAsync(ProjectSpace snapshot, CancellationToken token);
    public bool IsRunning { get; }
}
```

- [ ] 测试 schema、原子保存、损坏恢复、二次迁移、未知字段和删除确认。
- [ ] 测试 localhost HTTP/HTTPS 白名单，拒绝 javascript/data/file 和任意参数。
- [ ] 测试单实例、配置快照、失败停止、取消只阻止后续、录音优先。
- [ ] 实现 5 步向导和逐步资源测试；执行预览使用自然语言列出真实动作。
- [ ] Cursor/VS Code workspace 参数只来自已选择本地文件夹并正确引用；其他编辑器只打开/激活。
- [ ] 不运行开发服务器、测试、构建、部署或任何 shell。

**Risk:** URL/参数注入、并发执行、把打开终端误报成命令完成、取消伪回滚。

**Rollback:** Runner 由 feature switch 隔离；删除入口不删除用户独立配置；已打开应用不自动关闭。

**Automatic acceptance:** 存储与 URL/path table tests、执行器 fake adapter 顺序/停止/取消测试、Host build/self-test。

**Hardware acceptance:** 真实编辑器/终端/预览组合连续进入与取消；执行中录音立即优先。

## Phase 5：Capture & Ask

**Files:**

- Create: `scripts/features/CaptureAskModels.cs`
- Create: `scripts/features/CaptureAskService.cs`
- Create: `scripts/ui/CaptureAskForm.cs`
- Modify: `scripts/ui/HomePage.cs`、`LiveHudForm.cs`
- Modify: `BUILD_VIBE_MIC.cmd`、`scripts/validate.js`

**Interfaces:**

```csharp
internal enum CaptureSourceKind { ForegroundWindow, Region }
internal sealed class CaptureAskService
{
    public Task<ActionResult> CaptureAsync(CaptureSourceKind source, CancellationToken token);
    public Task<ActionResult> PasteToTargetAsync(string captureId, string focusTargetId, CancellationToken token);
    public void CleanupExpiredCaptures(DateTime utcNow);
}
```

- [ ] 测试截图源、预览/粘贴图像 hash 一致、目标未验证禁止粘贴、取消和清理。
- [ ] 测试只写本次图片、不读取剪贴板文字、不自动 Enter、不自动录音。
- [ ] 实现当前窗口和区域选取、预览、复制、重新截图、取消。
- [ ] 粘贴前调用 Focus 验证；失败显示“未执行粘贴”。
- [ ] 粘贴后只显示“动作已派发”或真实图像检测结果；提示用户手动语音和发送。

**Risk:** 捕获错误窗口、隐私残留、剪贴板覆盖范围过大、目标失效后仍粘贴。

**Rollback:** 临时目录可安全清理；功能关闭不注册全局快捷键、不启动后台任务。

**Automatic acceptance:** 生成固定图案 bitmap 做 hash/清理/剪贴板 image 测试；Focus failure 无 SendKeys 调用。

**Hardware acceptance:** 当前窗口/区域、错误目标安全停止、真实 AI 目标图片粘贴、截图后不自动发送。

## Phase 6：Browser Remote Lite

**Files:**

- Create: `scripts/features/BrowserProfileTemplate.cs`
- Create: `scripts/ui/BrowserProfileDialog.cs`
- Modify: `scripts/ui/ControlsPage.cs`、`ProjectSpaceWizard.cs`
- Modify: `scripts/validate.js`

**Interfaces:**

```csharp
internal sealed class BrowserProfileChangeSet
{
    public string ProfileId;
    public Dictionary<string, string> Before, Recommended;
}
internal ActionResult ApplyBrowserTemplate(BrowserProfileChangeSet changeSet);
internal ActionResult UndoBrowserTemplate(string profileId);
```

- [ ] 测试差异预览、显式确认、逐项测试、应用前快照和一键撤销。
- [ ] 测试不改变 TV/Home 长按/Smart Profiles，除非 change set 明确包含且用户确认。
- [ ] 使用现有 Bridge ACK；ACK 超时显示 warning，不显示已生效。
- [ ] 不加入扩展、DOM/网页内容读取或语义链接导航。

**Risk:** 覆盖现有 Browser AI Profile、撤销快照丢失、浏览器兼容性夸大。

**Rollback:** 一键恢复应用前 mappings；模板配置独立，不改语音配置。

**Automatic acceptance:** mapping diff/restore/ACK tests；旧配置 round-trip。

**Hardware acceptance:** Chrome/Edge 分别测试刷新、滚动、返回及选定长按动作；其他浏览器按实测标记。

## Phase 7：安装器与首次设置

**Files:**

- Modify: `installer/VibeFlow.iss`
- Create: `scripts/ui/SetupWizardForm.cs`
- Modify: `scripts/VibeMic.cs`（只保留唯一可达入口）
- Modify: `scripts/capture-ui-screenshots.ps1`
- Modify: `scripts/validate.js`
- Update: `docs/V2_0_USER_GUIDE_ZH.md`、安装器说明和教程截图

**Interfaces:** 保持 `onboardingVersion/onboardingStep/resumeSetupAfterRestart`，提升版本只能在迁移测试先覆盖后执行；步骤完成使用证据对象而不是按钮点击。

- [ ] 测试唯一可达 5 任务向导、旧用户不被强制重跑、进度保存和重启恢复。
- [ ] 为每任务加入目标/原因/状态/主操作/重试/修复/前后导航/保存状态。
- [ ] 安装器展示欢迎、前置检查、安装阶段和完成页；不静默安装第三方语音工具。
- [ ] 保持安装目录、升级配置迁移和 startup 恢复逻辑。
- [ ] Legacy 方法只有在调用点测试、截图脚本和回滚证据齐备后删除。

**Risk:** 老用户重新向导、重启循环、驱动安装越权、卸载误删用户数据。

**Rollback:** 保留 schema 迁移的旧字段映射；安装器升级前配置备份；回退包不覆盖中央用户数据。

**Automatic acceptance:** clean/legacy/malformed config、重启两次幂等、唯一调用链、Inno 编译和生命周期脚本。

**Hardware acceptance:** RC003/VB-CABLE/语音工具真实 5 任务；需要重启的 VB-CABLE 恢复；Win10/Win11 安装升级卸载。

## Phase 8：版本、候选构建与交付

**Files:**

- Modify: `package.json`
- Modify: `scripts/VibeMic.cs`、`scripts/VoxDeckInputBridge.cs`（只更新非 Capture assembly/product 版本）
- Modify: `BUILD_RELEASE.ps1` 版本门禁
- Modify: `installer/VibeFlow.iss`
- Update: `README.md`、`VIBE_MIC_VERSION.md`、`CHANGELOG.md`、`docs/V2_0_USER_GUIDE_ZH.md`、`docs/RELEASE_NOTES_ZH.md`、迁移/测试/限制/回滚文档
- Preserve unchanged: `scripts/VibeMicAtvvCapture.cs` 和冻结 Capture 二进制

- [ ] 在所有功能、迁移、UI 和 gate 通过后统一更新非 Capture 版本到 2.0.0 候选。
- [ ] 运行完整构建、三个 self-test、npm 门禁、安装/升级/卸载生命周期。
- [ ] 生成候选 EXE/ZIP/SHA256，同批记录哈希；不签名就明确写未签名。
- [ ] 对照实际完成/验证能力编辑更新说明，删除无证据声明。
- [ ] 最终 auditor 与 release gate；不 Push、不 PR、不发布、不签名。

**Risk:** 版本门禁遗漏、正式脚本触发依赖恢复、Capture 被误重建/签名、把候选写成稳定版。

**Rollback:** 保留 V1.5 安装包入口和用户配置；候选只写入 `release/`；回退不删除独立 V2 数据，禁用 V2 后旧行为保持。

**Automatic acceptance:** 所有自动测试、完整 release build、生命周期、同批 SHA；完整 Git diff 无无关修改。

**Hardware acceptance:** 完整黄金路径、录音/输入原门禁、Focus 50 次、DPI/主题、Browser、Capture & Ask、首次设置和中断恢复；没有证据的条目保留“未验证”。

## 每阶段固定结束动作

1. 重新计算 Capture 源码和冻结二进制 SHA-256。
2. `git status --short` 与完整 `git diff`，逐文件归因。
3. 构建所有受影响组件并运行旧/新测试。
4. 启动本轮真实构建产物；涉及 UI 时使用 Computer Use。
5. 新功能关闭后复测旧行为和配置。
6. 调用只读 `vibeflow_auditor`；处理 BLOCKER/HIGH。
7. 执行 `vibeflow-release-gate` 并记录 PASS、PASS WITH MANUAL HARDWARE CHECKS 或 FAIL。
8. 更新 `docs/V2_0_PROGRESS.md`；同一根因三次失败后停止扩大修改。

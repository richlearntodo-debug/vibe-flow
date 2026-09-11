# V2.0 Notes Deck 实施计划

## 目标

让用户在不改变稳定录音链路的前提下，能够用本地便签保存自己的表达，用一个清晰的 APP 快捷入口打开/激活工作目标，并在需要时使用用户自带的文本模型整理便签。

## KEEP / REUSE / REFACTOR / HIDE / ADD

### KEEP

- `VibeMicAtvvCapture.cs`、Capture 二进制、RC003 录音 generation、音频路由和按住/松开时序。
- `VoxDeckInputBridge.cs` 的 Raw Input、Hook、设备过滤、修饰键清理和现有映射兼容性。
- 既有配置迁移、Profiles、Smart Profiles 开关和首次设置真实检测边界。

### REUSE

- `ActionResult` 作为统一操作反馈模型。
- `DesignTokens`、`UiComponents`、`PageShell` 的 WinForms 样式和布局工具。
- `FocusTargetService` 作为用户明确选择的 APP 目标验证器；不扩展成全局窗口监听。
- `ContextDeckForm` 的状态只读展示原则。

### REFACTOR

- 主导航中的“项目”语义改为“快捷入口”；旧项目数据通过兼容读取迁移为入口数据。
- 首页最近项目卡改为“最近快捷入口”和“下一步建议”。
- 新增单一 `NotesStore` 与修订号，供主窗口和 Deck 共享。
- 统一保存、错误、取消和待人工确认文案。

### HIDE

- 主入口中的 Workspace、预览编排、批量启动和复杂 Project Space 任务看板。
- 旧版截图提问、Browser Remote、工作流编排的新增入口；保留数据/代码可回滚，但不在 Notes Deck 主导航宣传。

### ADD

- “便签”主页面和轻量独立 Deck：新建、编辑、保存、搜索、软删除、恢复、复制、TXT/Markdown 导出。
- APP 快捷入口三步设置：选择 APP、设置非录音快捷键、真实测试保存。
- AI 配置页：OpenAI-compatible Chat Completions 首个协议适配，DPAPI 凭据存储，整理/分类/汇总/翻译预览与取消。
- Notes Deck 的固定/置顶/关闭独立偏好，失焦不抢焦点。

## 阶段

### 阶段 0：基线与规则

本文件、`BASELINE.md`、`PROGRESS.md`、`UI_SPEC.md`、`APP_COMPATIBILITY.md`、`QA_REPORT.md`；不改稳定核心。

### 阶段 1：视觉切片和信息架构

修改 `PageShell.cs`、首页相关 `VibeMic.cs`、新增 `NotesPage.cs` 和快捷入口页面外壳；导航改为：首页、便签、快捷入口、自检、设置。确保主窗可构建、空态可理解、旧录音入口仍可达。

### 阶段 2：本地便签仓库与 Deck

新增 `scripts/features/NotesModels.cs`、`NotesStore.cs`、`scripts/ui/NotesPage.cs`、`NotesDeckForm.cs`；原子保存、备份、修订冲突、软删除/恢复、搜索和导出。加入 Host self-test 与独立存储测试。

### 阶段 3：APP 快捷入口

复用 `FocusTargetService` 和安全激活逻辑，新增轻量 `AppShortcutModels/Store`，把旧 Project 列表展示迁移为入口列表；ChatGPT 没有 UIA Edit 时只显示警告，不伪造成功。

### 阶段 4：用户自带 API 的 AI 文本操作

新增 AI 配置/凭据/协议适配器和四种操作结果模型；原文修订保护、取消、超时和错误分类；无配置时本地功能完整可用。真实云模型调用需用户提供 API 和预算，默认仅做固定无敏感测试请求。

### 阶段 5：安装、自检、迁移与候选构建

补齐首次设置中的便签/快捷入口说明、自检修复路径、升级迁移和安装说明，运行完整自动化、真实 UI 操作和只读审查，生成候选构建但不发布。

## 回滚

- 每阶段只提交/修改本阶段文件；回滚通过保留旧页面分派和独立新文件，必要时恢复对应导航分支。
- 不回滚或重写 Capture、Bridge Raw Input、Hook、设备过滤或用户配置。
- 配置/便签数据迁移必须保留 `.bak`，迁移失败回退到前一份有效文档。

## 阶段验收

每阶段必须运行对应构建和测试，启动实际 `VibeMic.exe`，完成 Computer Use 原生 UI 操作和截图，检查 `git diff`，调用只读 auditor 和 `vibeflow-release-gate`。任何硬件、外部 APP 或 API 未验证项单独记录，不写成通过。


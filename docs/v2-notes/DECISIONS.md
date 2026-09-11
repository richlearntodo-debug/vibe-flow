# V2 Notes Deck 决策记录

## 2026-09-06 · Notes Deck 替代旧 Project 范围

本轮产品对象是本地便签、独立 Deck、便签范围内的 BYOK 文本整理，以及“APP 快捷入口”。
不再把 Workspace、项目看板、截图反馈、浏览器 Remote 或工作流编排作为主产品入口。旧
`project-spaces.json` 保留并兼容读取，旧数据不删除、不自动执行复杂流程。

## 2026-09-06 · ChatGPT 定位

ChatGPT 是“对话工作台”，不是代码编辑器。Workspace 字段只能作为本地现场记录，不能
注入 ChatGPT、自动发送或伪称输入框已就绪。只有 UI Automation 发现同一进程内可编辑、可
聚焦控件并完成验证后，入口才显示“输入框已就绪”；否则显示“已打开，输入框待确认”。
本机 Windows 包当前登记为 `OpenAI.Codex_2p2nqsd0c76g0`，其进程名为 `ChatGPT.exe`；这只
是安装身份与进程证据，不表示 Vibe Flow 已完成 ChatGPT 输入框或对话发送验证。

## 2026-09-06 · 录音与 Smart Focus

原录音键保持按住开始、松开结束。Smart Focus 是用户主动动作，必须先锁定并验证输入目标，
再按住原录音键。录音开始后不执行抢焦点动作；不读取或回填第三方转写，也不自动发送。

## 2026-09-06 · AI

首版只支持用户主动选择便签的 OpenAI-compatible Chat Completions BYOK。原文和结果分离，
结果带源修订号；分类建议必须用户确认。没有 API 或断网时，本地便签、搜索和导出仍可用。

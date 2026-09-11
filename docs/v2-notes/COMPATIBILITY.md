# V2 Notes Deck 兼容性与证据

| 能力 | 当前状态 | 证据边界 |
|---|---|---|
| WinForms 主窗/便签/Deck | BUILD_TEST PASS，GUI 已检查 | 当前开发机；DPI/主题完整矩阵未完成 |
| 本地便签与导出 | BUILD_TEST PASS | 未替代磁盘故障和恶意路径人工验收 |
| ChatGPT 桌面端 | APP_OPENED_MANUAL_FOCUS | 本机安装包身份为 `OpenAI.Codex_2p2nqsd0c76g0`，运行进程为 `ChatGPT.exe`；可启动/激活，但当前没有可验证 `ControlType.Edit` |
| Cursor / VS Code | NOT_RUN | 未在本轮真实版本上学习并验证输入控件 |
| BYOK 供应商 | REAL_PROVIDER NOT_RUN | 未使用用户 Key，不做付费/联网声明 |
| RC003/音频/微信输入法 | HARDWARE NOT_RUN | 无真实硬件、VB-CABLE 和端到端语音工具证据 |
| 迁移/升级/卸载 | DATA_MIGRATION NOT_RUN | 仅有确定性存储测试与备份代码证据 |

能力等级只能由真实证据提升。打开 APP、发送快捷键或窗口前台成功不等于输入框已聚焦。

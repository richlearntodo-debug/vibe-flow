# V2.0 Notes Deck QA 记录

## 基线自动化

| 检查 | 结果 | 证据边界 |
| --- | --- | --- |
| `npm test` | PASS（基线） | 覆盖稳定配置、哈希和现有回归，不代表硬件通过 |
| `BUILD_INPUT_BRIDGE.cmd` | PASS（基线） | 仅证明 Bridge 可构建 |
| `BUILD_VIBE_MIC.cmd` | PASS（基线） | 仅证明 Host 可构建 |
| `VibeMic.exe --self-test` | PASS（基线） | 不代表外部 APP UIA 通过 |
| `VoxDeckInputBridge.exe --self-test` | PASS（基线） | 不代表 RC003 实体按键通过 |
| `VibeMicAtvvCapture.exe --self-test` | PASS（基线） | 不替代真实音频 |
| `scripts/tests/Test-V2FeatureSuite.ps1` | PASS | 包含 Notes Deck AI、便签保存/恢复、Smart Focus、Capture & Ask、HUD/Deck、快捷入口回滚测试 |

## 阶段门禁

每个阶段追加：构建命令、定向测试、真实启动路径、Computer Use 操作、截图路径、diff、auditor 发现、release gate 结果和未验证项。

## 当前未验证

- RC003 100 次按住/松开和普通键盘隔离。
- VB-CABLE、微信输入法及其他语音工具端到端文字落点。
- ChatGPT UIA 输入框聚焦。
- 双显示器、100–200% DPI、睡眠/唤醒、安装升级卸载。
- 真实云 API 请求、401/403/429、超时、取消和供应商费用提示。

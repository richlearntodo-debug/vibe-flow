# FlashVoice 替换微信输入法对照实验（2026-11-03）

## 结论

**本轮不能替代微信输入法，因此没有卸载 WeType。**

- 内存方面，FlashVoice 静置 5 分钟后仍有约 **723.5 MB 私有内存**，相比 WeType 本轮起点的 **821.8 MB** 只省下约 **98.3 MB（12.0%）**，不是约 821 MB。
- 功能方面，RC003 音频链路正常进入 `CABLE Output`，Vibe Link 也成功发送了右 Shift，但 FlashVoice 没有开始录音，目标输入框没有出现识别文字。
- 原因在触发层：FlashVoice 的 Windows 全局按键监听使用 **Raw Input**；Vibe Link 的“其他语音工具”槽使用 `keybd_event` 注入右 Shift。实测合成按键没有唤起 FlashVoice。
- 实验结束后已恢复 Vibe Link 的 `微信输入法 / Ctrl + Win / 单击切换`，并停止 FlashVoice；WeType 未改动。

## 环境与版本

| 项目 | 实测值 |
|---|---|
| 系统 | Windows 11 企业版 10.0.26200，x64 |
| 物理内存 | 16,353,316 KB（约 15.6 GB） |
| WeType | 3.0.0.38，实验开始时 16 个进程 |
| Vibe Link | 2.0.0，`H:\from-github-project\vibe-flow\VibeMic.exe --background` |
| 录音内核 | `v1.0.3`，Capture `1.2.1.0`，未修改 |
| FlashVoice | 1.4.5，官方 GitHub Release |
| FlashVoice 安装位置 | `C:\Users\46549\AppData\Local\FlashVoice` |
| FlashVoice 安装包 SHA-256 | `AF29385815573451BF920E582AE4E7E80953C34829CE0ABAD77072F4CF4B0DA3` |
| FlashVoice 数字签名 | Valid，签名主体 `Apowersoft Ltd` |
| 模型 | SenseVoice medium，239,233,841 字节 |
| 模型 SHA-256 | `C71F0CE00BEC95B07744E116345E33D8CBBE08CEF896382CF907BF4B51A2CD51` |
| 驱动 / 重启 | 不需要驱动；安装和配置过程中未重启 |

## FlashVoice 配置

FlashVoice 的 Windows 录音源只有“麦克风”和“系统音频”开关，没有设备下拉框：

- 麦克风模式读取 Windows 默认录音设备。
- 系统音频模式读取 Windows 默认播放设备的 loopback。

因此本次配置为：

- `record_mic = true`
- `record_system = false`
- `auto_inject_text = true`
- `save_audio = false`
- 自由说快捷键：`Right Shift`，单击切换

Vibe Link 在每次遥控器会话中先取得 `CABLE Output`，再发送右 Shift。日志证据：

```text
DEFAULT CAPTURE ROUTE ACQUIRED generation=15 target=CABLE_Output_(VB-Audio_Virtual_Cable) elapsed_ms=16
TRANSCRIPTION TRIGGER provider=custom generation=15 phase=start mode=toggle shortcut=rightshift sent=True
```

如果手动启动 FlashVoice 而不是由 Vibe Link 会话唤起，它会使用当时的 Windows 默认麦克风；这会是电脑麦克风，不是遥控器。因此“听起来像电脑声音”与下面的触发失败一致，不应把该样本当作遥控器识别结果。

## 内存实测

采样方式：每 2 秒统计 FlashVoice 根进程及其全部子进程（共 7 个，含 WebView2），持续 5 分钟；取预热 60 秒后的稳定区间。

| 指标 | WeType（实验起点） | FlashVoice（静置 5 分钟） | 差值 |
|---|---:|---:|---:|
| 进程数 | 16 | 7 | -9 |
| 私有内存 | 821.8 MB | 平均 723.5 MB（723.1–724.1） | **-98.3 MB / -12.0%** |
| 工作集合 | 1568.1 MB | 平均 955.4 MB | -612.7 MB / -39.1% |

真机说话尝试期间，FlashVoice 私有内存峰值为 **725.4 MB**，工作集合峰值为 **957.3 MB**。相对静置只增加约 1.9 MB，因为没有发生实际识别推理。

FlashVoice 源码里的 `unload_model` 只出现在文件转写流程；听写使用的全局 SenseVoice 客户端缓存没有静置卸载路径。实测 5 分钟内私有内存也没有下降。

## 端到端结果

测试靶：忽略 F5 的 WinForms 多行文本框，空白、已聚焦。

Vibe Link 侧证据：

- 遥控器长按产生了 5.265 秒真实音频。
- `CABLE Output` 路由取得成功。
- `TRANSCRIPTION TRIGGER provider=custom ... shortcut=rightshift sent=True`。
- 会话正常结束，`audio_delivered=True`。

FlashVoice 侧结果：

- 主界面一直停在“开始录音”。
- 目标文本框没有文字。
- 无法得到有效的“按下到出字延迟”；本次只能确认没有出字，不能把 Vibe Link 的 `trigger_to_ready_ms` 当作端到端延迟。

旁证：第一轮用 Notepad 作靶时，已知的“缺少签名过滤通道”导致 RC003 的 F5 直通，Notepad 执行了 F5“插入当前日期时间”。该污染样本已丢弃，不作为识别结果。

## 根因

1. FlashVoice 的 Windows 按键监听使用 Raw Input：
   `src-tauri/plugins/keyhook/src/windows.rs` 注册 `RegisterRawInputDevices` 并读取 `WM_INPUT`。
2. Vibe Link 的“其他语音工具”槽使用 `keybd_event` 注入配置的右 Shift：
   `scripts/VibeMic.cs::SendConfiguredHotkey`。
3. 实测该合成按键没有让 FlashVoice 进入录音状态；而 Vibe Link 的遥控器音频和路由本身正常。

结论是：**失败点是 Vibe Link 合成快捷键到 FlashVoice Raw Input 监听之间，不是 RC003 音频、CABLE 路由或 SenseVoice 模型。**

## 当前恢复状态

已恢复并核对：

- Vibe Link：`inputMethod=wechat`
- 快捷键：`ctrl+win`
- 触发：`toggle`
- 启动等待：`80 ms`
- 语音页显示：“微信输入法 / Ctrl + Win / 单击切换”
- `CABLE Input`、`CABLE Output` 均检测到
- FlashVoice 进程已停止
- WeType 未卸载

配置备份：

```text
C:\Users\46549\AppData\Local\Vibe Flow Remote\UserData\vibe-mic-config.json.bak_flashvoice_test_20261103-002217
```

## 后续选项

- **A（推荐）：保持现状。** 继续使用微信输入法；本次没有满足“省 821 MB 且不丢遥控器语音输入”的替换条件。
- **B：换一个候选工具再测。** 候选必须能接收常规全局快捷键（或低级键盘事件），并允许把录音设备固定到 `CABLE Output`；在端到端出字前保留 WeType。
- **C：继续用 FlashVoice。** 需要额外的适配层或修改 FlashVoice 的按键监听方式，让它接收 Vibe Link 的合成右 Shift。这超出“第三个槽只派发快捷键”的现有约束，实施前需要单独设计和批准。

## 关于卸载 WeType

本轮**不建议也不执行**卸载：

- 没有通过“RC003 → CABLE Output → FlashVoice → 焦点输入框”的端到端验收。
- 卸载会失去当前已验证可用的微信输入法语音链路。
- 只有在另一个候选工具完成真实出字、延迟和内存验收后，才应进入卸载步骤；卸载前先保留 WeType 3.0.0.38 的官方安装包和现有设置。

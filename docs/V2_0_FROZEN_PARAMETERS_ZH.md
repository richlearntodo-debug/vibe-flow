# V2.0 候选版 2 · 核心参数固化清单

> 本文件把本版本**不允许随意改动**的核心参数集中列出，并写明**在哪里强制**、**怎么验证**。
> 机器可读版本见仓库根目录的 `frozen-parameters.json`；两者内容必须一致，改动前先读最后一节。

固化时间：2026-09-12 · 版本 `V2.0.0` · 标签 `v2.0.0-candidate.2`

---

## 1. 版本与冻结件

| 参数 | 值 | 在哪里强制 | 怎么验证 |
| --- | --- | --- | --- |
| 产品版本 | `V2.0.0`（候选版 2） | `scripts/VibeMic.cs` 与 `scripts/VoxDeckInputBridge.cs` 的 `AssemblyFileVersion` | `scripts/validate.js` 断言桥为 `2.0.0.0` |
| Capture 文件版本 | **`1.2.1.0`** | `scripts/VibeMicAtvvCapture.cs` 的 `AssemblyFileVersion("1.2.1.0")` | 同上断言；`--self-test` |
| 录音内核 | **`v1.0.3`** | Capture 自身 | `capture-health.json` 的 `recording_kernel` |
| Capture 冻结哈希 | **`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`** | `scripts/Get-StableCaptureBinary.ps1`、`scripts/Prepare-DevelopmentRuntime.ps1`、`.github/workflows/validate.yml` | 三处都做 SHA-256 校验；不一致即**大声失败** |
| 必须同时存在的 5 个文件 | `VibeFlow.exe`、`VoxDeckInputBridge.exe`、`VibeMicAtvvCapture.exe`、`NAudio.Core.dll`、`NAudio.Wasapi.dll` | `Prepare-DevelopmentRuntime.ps1` | 缺一即 `VOICE-RUNTIME-INCOMPLETE` |
| NAudio 依赖哈希 | `NAudio.Core` `FCF493FC…B01A8`；`NAudio.Wasapi` 见脚本内常量 | `Prepare-DevelopmentRuntime.ps1` | 同上 |

## 2. 语音链路（录音行为，**不得改动**）

| 参数 | 值 | 说明 |
| --- | --- | --- |
| 触发方式 | **`hold`** —— 按住开始、松开结束 | 单击切换不属于当前产品 |
| 单段时长上限 | 约 **60 秒**（设备侧） | 提前松开立即结束，不自动续接 |
| 稳定语音档案版本 | **`v11`** | 与配置里的 `stableVoiceProfileVersion` 一致；界面显示「语音参数已应用（v11）」 |
| `gain` | **`1.0`** | `StableVoiceGain` |
| `autoLevel` | **`true`**（= 声音处理为 `speech`） | `StableVoiceProcessing = "speech"` |
| `drainMs` | **`180`** | 尾音排空 |
| 默认音频端点 | **`CABLE Input`** | `StableVoiceEndpoint`；录音端为 `CABLE Output` |
| 禁止项 | 无 MIC_EXTEND、无自动续接、无点击切换、无自动 Enter、无云端识别、无网页正文读取 | 由 `validate.js` 断言 |

## 3. 按键与手势

| 参数 | 值 | 在哪里强制 |
| --- | --- | --- |
| 手势层 | **短按 / 长按 / 双击**，未配置时按 双击 → 长按 → 短按 **跟随** | `scripts/features/GestureLayerPolicy.cs` |
| 长按判定 | **`LongPressMs = 600`** | 同上（界面文案写「按住约 0.65 秒」，为本值的口语化表述） |
| 双击窗口 | 跟随 Windows `GetDoubleClickTime()`，**下限 500 ms**（实测：写死 320 ms 时，自然的 378 ms 双击会被判成两次短按） | 同上 |
| 录音键 | 固定在稳定语音链路，**不参与自定义映射、不参与手势分层** | `validate.js` |
| 电源键（开机键） | `VK 0xFF` / 扫描码 `0x5E`，**未指派 = 交给 Windows**；指派后由言灵拦截（`enabled + suppress + tap`） | 桥映射 + Profile 投影固定键表 |
| 电源键已知取舍 | 同一扫描码也是**蓝牙重连后麦克风键**的形态 → 指派动作后，重连时的麦克风键可能被当作电源键。**更看重录音可靠时把电源键设为「不执行动作」** | 用户指南 / 更新说明 / 已知限制 / 发版文案 四处均已写明 |
| 不支持的键 | Back 与独立音量键：无稳定用户态事件，**不提供映射** | `validate.js` |
| 推荐手势表 | 仅在**尚无手势表的机器**写入一次；用户改过后不再覆盖 | `GestureBindingStore` |

## 4. 收音质量

| 参数 | 值 | 说明 |
| --- | --- | --- |
| 识别舒适区 | **10–30%** 输出 RMS | 低于 10% 即提示 |
| 界面提示阈值 | **`level.OutputRms < 10`** | `scripts/VibeMic.cs`；文案「收音偏小 · 上次 X%（建议 ≥10%）」 |
| 数据来源 | `UserData\remote-voice-session\vibe-mic-runtime.log` 最后一条 `REMOTE STREAM STOP`（尾部读 64 KB） | 只读，不修改采集链路 |
| 实测基线（本机 920 次会话） | 输出电平中位数 **5%**、**92%** 低于 10%；链路 `drops=0`；自动增益约 **4 倍**；约 1/3 会话峰值已打满 | **因此不得通过加大增益"修复"** —— 那只会连带放大底噪与削波 |
| 只读诊断脚本 | `scripts/tests/Get-CaptureLevels.ps1` | 打印最近 N 次会话的电平/增益/断档与按序排查步骤 |

## 5. 界面不变量

| 参数 | 值 | 在哪里强制 |
| --- | --- | --- |
| 正文字号下限 | **8.0 pt**（不得出现 7.x） | `scripts/ui/UiFonts.cs` + `validate.js` |
| 页面集合 | **首页 / 工作流 / 快捷键 / 语音 / 自检 / 设置** 六页 | `validate.js` |
| 界面矩阵 | **3 主题**（light / dark / system）× **4 尺寸**（默认 / 1366×768 / 1920×1080 / 880×500）= **12 例** | `scripts/check-ui-matrix.ps1`（发布链在每轮强制 12/12） |
| 几何门禁 | 同页面控件**不得重叠、不得裁切** | `scripts/check-ui-geometry.ps1` |
| 已测量的工具盲区 | 几何检查**只看控件矩形、看不到文字被省略/被底色压住** → 涉及文案或层叠的改动**必须截图目视** | 本轮实测：语音页电平行曾被状态带底色盖住，几何报 0/0 |
| 反馈口径 | 窗口在前 → 窗口内卡片；窗口不在前台 → Live HUD；**同一条结果不出现两次** | `validate.js` |

## 6. 构建与发布门禁（发布链每一步都必须过）

| 门禁 | 期望值 |
| --- | --- |
| `node scripts/validate.js` | exit 0（源码、文案、契约、构图与冻结哈希的断言） |
| Host / 桥自测 | `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test` 均通过 |
| 界面矩阵 | **12 case(s) passed** |
| 安装器 | 退出码 0 |
| 载荷逐文件一致 | **51 / 51** |
| 冻结件哈希 | 安装后仍为 `B62DE035…2E683` |
| 安装包附件 | `VibeFlow-Setup.exe`、`Vibe-Flow-Windows-x64.zip`、`SHA256SUMS.txt` 三者齐全且与本地逐字节一致 |

## 7. 运行时路径与产物

| 项 | 路径 |
| --- | --- |
| 用户态数据根 | `%LOCALAPPDATA%\Vibe Flow Remote\UserData` |
| 会话产物 | `…\UserData\remote-voice-session\`（`vibe-mic-runtime.log`、`vibe-flow-host.log`、`remote-voice-events.jsonl`、`capture-health.json`） |
| 配置 | `…\UserData\vibe-mic-config.json`；桥映射 `安装目录\voxdeck-shortcuts.json` |
| 安装目录 | `%LOCALAPPDATA%\Programs\Vibe Flow Remote` |
| 本机网络注意 | 桌面代理为 `127.0.0.1:7897`：**PowerShell 走系统代理**，而 **`git` 与 `gh` 不读系统代理**，必须显式 `-c http.proxy=http://127.0.0.1:7897` / `HTTPS_PROXY=http://127.0.0.1:7897` |

## 8. CI 现状（如实记录）

- 冻结采集件被 `.gitignore` 的 `*.exe` 排除 → 检出里没有它。现已通过**仓库 Secret**（`VIBE_FLOW_CAPTURE_B64_1..4`，base64 分 4 片）在工作流中还原并校验哈希 ✔；**第 6 步（V2 测试）已通过** ✔。
- **第 11 步「Build release」仍会失败** ✗：打包需要 `tools\VBCABLE_Driver_Pack45.zip`，同样被排除，且体积超过 Secret 的 48 KB 上限。**尚未解决** —— 需要把该驱动包作为发布附件交给 runner，或接受"CI 只跑源码与界面测试"。

## 9. 改动规则

1. **第 1–5 节的任何值都不得为了"让某个检查通过"而修改** —— 它们是产品的契约；要改必须先说明理由，并同步更新：本文件、`frozen-parameters.json`、`docs/V2_0_RELEASE_NOTES_ZH.md`、以及 `validate.js` 里对应的断言。
2. 断言钉的是**契约**，不是文案字面量：文案可以变，但 `validate.js` 对应字符串必须同步更新（本会话已多次因此被拦下，属预期保护）。
3. 采样/录音、蓝牙、按键钩子与隐私链路**不属于可自由重构范围**；UI 与文案改动同样必须过几何检查与目视截图两道。
4. 涉及多行代码的改动**使用编辑工具**，不要用脚本批量替换（本会话有两次因此误删行）。

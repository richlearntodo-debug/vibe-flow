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
| 可选语音工具 | 微信输入法（默认）/ Typeless / **八哥说（网易，默认快捷键右 Alt）** / Windows 语音输入 / 其他自定义工具；八哥说是否直写输入框**尚未实测**，因此**不启用**自动粘贴 | `ProviderIndex` 与 4 处下拉必须同序 |
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
| 页面集合 | **首页 / 语音 / 快捷键 / 工作流 / 自检 / 设置** 六页 | `validate.js` |
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

## 10. 本轮固化的按键与手势（2026-09-13 用户确认 ✔）

### 10.1 开机键（电源键）：三层全部留空 ✔

| 层 | 冻结值 | 理由 |
| --- | --- | --- |
| 短按 | **无动作** ✔ | 它与蓝牙重连后的**麦克风键共用扫描码 `0xFF/0x5E`** ✗ —— 一旦指派动作，按住录音键可能被当成电源键执行 ✔（本机实测过一次：长按层配了 `open-url` ✗，表现为"当前页面被刷掉" ✗）。 |
| 长按 | **无动作** ✔ | 同上 ✔ |
| 双击 | **无动作** ✔ | 同上 ✔ |

- 代码侧默认值 ✔：桥内置的 power 映射为 `enabled = false, suppress = false` ✔（未指派即完全交给 Windows ✔）。
- **动作有两个存储位置** ✗：Profile 映射（`vibe-mic-config.json` ✔）**与手势层覆盖（`UserData\gesture-layers.json` ✔，优先级更高 ✗）** —— 清空时必须**两处都清** ✔，并**从投影结果反查** ✔（`voxdeck-shortcuts.json` 里 `power` 的 `enabled=true` 计数应为 **0** ✔、且全文无 `open-url` ✔）。
- 备份命名 ✔：`*.before-power-fix-<时间戳>.bak` ✔。

### 10.2 录音键：**绝不刷新页面** ✔（三重保证）

| 保证 | 实现 | 验证方式 |
| --- | --- | --- |
| ① 共码不抢键 ✔ | 桥：**动作全是 `none`/`passthrough` 的映射一律视为未启用** ✔（`MappingHasNoAction` / `IsActionless` ✔，两处兜底判定均含 ✔） | `validate` 门禁 + 已安装二进制内含该符号 ✔ |
| ② 窗口内拦截 ✔ | "遥控器在线范围"隔离：抑制窗口 **5 秒** ✔ 与 Raw Input 健康探测 **5 秒** ✔ **同频** ✔（探测必须落在它自己喂的窗口内 ✗ 否则会话第一下会漏 ✗） | 桥日志启动行 `Raw Input health timer interval_ms=5000 presence_window_ms=5000` ✔ |
| ③ 录音键不可改 ✔ | 录音键（F5）固定在稳定语音链路 ✔，不参与自定义映射、不参与手势分层 ✔ | `validate` 门禁 ✔ |

- **正常日志** ✔：`Key 录音键 DOWN/UP vk=0x74 scan=0x3F source=rc003_present_hook` ✔（说明由钩子接管 ✔）。
- **异常信号** ✗：出现 `RAW KEY DOWN vk=0x74 …` 而无 `Key 录音键` 行 ✔ → 表示 F5 直通到了前台应用 ✔（页面被刷新 ✗）。

### 10.3 其他快捷键：常用情况 ✔

**本机实测可用（用户确认 ✔）**：

| 键 | 短按 | 长按 | 双击 |
| --- | --- | --- | --- |
| 上 | 空 | `pageup` ✔ | `ctrl+x`（剪切）✔ |
| 下 | 空 | `pagedown` ✔ | `ctrl+a`（全选）✔ |
| 左 | 空 | `browserback`（返回上一页）✔ | `ctrl+z`（撤销）✔ |
| 右 | 空 | `ctrl+shift+z`（重做）✔ | `ctrl+s`（保存）✔ |
| 确认 | 空 | `volumemute`（静音）✔ | `mediaplaypause`（播放-暂停）✔ |
| 菜单 | 空 | 空 | `volumeup` ✔ |
| Home | 空 | 空 | `launch-client:cursor` ✔ |
| TV | 空 | `launch-client:chatgpt` ✔ | `volumedown` ✔ |
| **电源** | **空** ✔ | **空** ✔ | **空** ✔ |

**内置推荐表**（仅在**还没有手势表的机器**上写入一次 ✔，用户改过后不再覆盖 ✔）：`up/down/left` 长按 = `pageup/pagedown/browserback` ✔、`ok` 长按 = `volumemute` ✔、`ok` 双击 = `mediaplaypause` ✔、`tv` 长按 = `launch-client:chatgpt` ✔（其余见 `scripts/features/GestureBindingStore.cs` 的 `UpsertLayer` 调用 ✔ 与 `docs/V2_0_USER_GUIDE_ZH.md` ✔）。

## 11. 界面与状态同步（2026-09-13 用户确认 ✔）

### 11.1 导航顺序 ✔

**首页 · 语音 · 快捷键 · 工作流 · 自检 · 设置** ✔ —— 定义在 `scripts/ui/PageShell.cs` 的**三个数组**（文案 ✔ / 图标 ✔ / 页面 id ✔）✔，三者必须**下标对齐** ✗。枚举 `VibePageId` 的数值**故意不变** ✔（按钮携带 id 而非序号 ✔，其余代码不依赖顺序 ✔）。

### 11.2 Fluent 留白（外壳间距）✔

| 参数 | 旧 | 新 | 说明 |
| --- | --- | --- | --- |
| `NavigationButtonHeight` | 48 | **52** ✔ | 导航更透气 ✔ |
| `NavigationGap` | 8 | **6** ✔ | **必须 ≤6** ✗：6×(52+6)=348 + 头部 104 = **452 < 500** ✔ → 最小窗口 **880×500** 下六项**全部可见** ✔（几何检查只看重叠/裁切 ✗，**看不到"被挤出窗口"** ✗，必须目视 ✔） |
| `ContentPaddingHorizontal` | 34 | **42** ✔ | 内容四周更从容 ✔ |
| `ContentPaddingVertical` | 26 | **34** ✔ | 同上 ✔ |

**页面内部仍是写死的绝对坐标** ✗（571 处 `new Point` ✔，仅 1 处用 `SpacingUnit` ✔）→ 因此**不要**试图靠 token 调整页内间距 ✗；页内改动必须逐页做并过 12 例矩阵 + 目视 ✔。

### 11.3 品牌：App 站内文案为 **Vibe Link** ✔

- **改** ✔：窗口标题 ✔、侧边栏品牌与副标题（`Vibe Link` / `VIBE LINK · V2.0.0` ✔）、自测输出 ✔、提示与对话框文案 ✔、`AssemblyTitle/Product/Company` ✔。
- **绝对不改** ✗（改动会让现有安装的数据迁移/卸载/开机启动失效 ✗）：
  - `%LOCALAPPDATA%\Vibe Flow Remote\UserData` ✔（用户数据根 ✔）
  - `%LOCALAPPDATA%\Programs\Vibe Flow Remote` ✔（安装目录 ✔）
  - `C:\Program Files\Vibe Flow` ✔（旧版候选安装路径，兼容检测用 ✔）
  - 开机启动的**注册表项名** `Vibe Flow` ✔
  - 可执行文件名 `VibeFlow.exe` / `VoxDeckInputBridge.exe` / `VibeMicAtvvCapture.exe` ✔
- **脚本注意** ✔：`scripts/check-ui-geometry.ps1` 按 **`Vibe`** 前缀 + **限定本进程** 找窗口 ✔（原先按「言灵」✗，改名后会报 `Window not found` ✗）。
- 仓库名 / 安装包名 / 下载链接**保持** ✗（对外标识不变 ✔）。

### 11.4 Live HUD 与 Context Deck 的状态同步 ✔

| 表面 | 显示 |
| --- | --- |
| **Live HUD** ✔ | 标题/详情 = **会话状态** ✔（录音中 → 「正在接收真实音频」✔；结束 → 「录音已结束，等待语音工具处理」✔）；上下文行 = 「**工具 · X ｜ 目标 · Y**」✔（未设置时显示「未选择」/「未设置」✔） |
| **Context Deck** ✔ | **语音工具 + 会话状态** 同一行 ✔（`语音工具 · 状态` ✔）✔；**当前目标** 独立一行 ✔（`targetValue` ✔）；应用 / Profile / 设备 / 最近动作各有其行 ✔ |

- 数据来自 `VibeUiStatusSnapshot` ✔（`VoiceToolName` 为本次新增字段 ✔，由 `ProviderDisplayName(config.inputMethod)` 填充 ✔）。
- **切换输入法或切换"当前"工作流目标后，下一次快照发布即同步** ✔（录音状态变化、动作回执、定时轮询都会发布 ✔）。
- **验证方式（无需硬件 ✔）**：`VibeMic.exe --self-test` 会构造快照并断言 HUD 里出现「工具 · 八哥说」与「目标 · Cursor Chat」✔（`ControlTreeContainsPartialText` ✔，因为该行是多个值的拼接 ✔ 而 `ControlTreeContainsText` 是**完全相等**比较 ✗）。

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

## 12. 语音工具快捷键分割与"共用形态"判别（2026-09-13 下午 ✔）

### 12.1 各语音工具的快捷键（**互不重复** ✔，必须与工具内部设置一致 ✗）

| 语音工具 | 应用内默认快捷键 | 触发方式 | 说明 |
| --- | --- | --- | --- |
| **微信输入法**（默认 ✔） | **`ctrl+win`** ✔ | 单击切换（稳定 ✔） | **冻结值** ✗，不得改（真机验证过的稳定参数 ✔） |
| **网易八哥说** ✔ | **`rightalt`** ✔ | 单击切换 ✔ | 用户确认其客户端即为**右 Alt** ✔；显示名按用户要求写成**网易八哥说** ✔ |
| **讯飞语音输入法** ✔ | **`f6`** ✔ | **按住触发** ✔（长按说话 ✔） | **用户实测：讯飞的语音栏只允许绑定 F6** ✔（"快捷键只能设置为 f6" ✔）→ 应用按 F6 存 ✔ |
| **搜狗输入法**（新增 ✔） | **`rightctrl`** ✔ | **按住触发** ✔（按住右 Ctrl 说话 ✔） | 用户本机配置 ✔：**右 Ctrl 激活** ✔，按住说话 ✔ → 应用按 `rightctrl` + 按住触发存 ✔ |
| **豆包输入法**（新增 ✔） | **`alt+space`** ✔ | **按住触发** ✔ | 早期适配时在本机实测到的语音快捷键 ✔；用户要求重新接入 ✔（§12.1.3 ✔） |
| **其他语音工具**（自定义 ✔） | **`rightshift`** ✔ | 单击切换 ✔ | **由 `ctrl+win` 改为 `rightshift`** ✗：此前与微信输入法**重复** ✗ |

- **不变式** ✔（已加门禁 + 自测 ✔）：以上**六个**工具的默认快捷键**两两不同** ✔（自测用双重循环穷举比对 ✔）；改动前请同时更新 `scripts/validate.js` 的断言 ✔。
- **注意** ✗：应用里存的快捷键**只是"我们发送什么"** ✔ —— 必须与**该工具自己的设置**完全一致 ✔，否则按下不会启动听写 ✔。
- **本机当前配置** ✔（读回 `vibe-mic-config.json` ✔）：迁移后为 `inputMethod = wechat` ✔、`inputMethodHotkey = ctrl+win` ✔、`inputMethodTrigger = toggle` ✔、`voiceMode = hold` ✔；改用讯飞时由应用把三个字段写成 `xunfei` / `f6` / `hold` ✔（备份 `vibe-mic-config.json.before-f6-switch.bak` ✔）。

### 12.1.3 搜狗与豆包（2026-09-13 深夜，用户要求新增 ✔）

| 工具 | 默认值 ✔ | 用户侧前提 ✔ | 未验证 ✗ |
| --- | --- | --- | --- |
| **搜狗输入法** ✔ | `rightctrl` + **按住触发** ✔ | 搜狗「设置 → 按键」里语音输入快捷键 = **右 Ctrl** ✔ 且为**按住说话** ✔ | 真机"按住录音键能出字"未测 ✔ |
| **豆包输入法** ✔ | `alt+space` + 按住触发 ✔ | 豆包设置里语音快捷键 = **Alt + 空格** ✔ | **两条历史风险**：其语音面板**可能不理会模拟按键** ✗；其麦克风列表**可能不含虚拟声卡 `CABLE Output`** ✗（早期记录未最终坐实 ✔）→ 实测决定去留 ✔ |

- **进程识别** ✔：搜狗 = `SGTool` / `SOGOUSmartAssistant` / `sogou_voice_assistant` / `SogouCloud` ✔；豆包 = `ImeService` / `DoubaoImeSet` / `ImeWatchdog` ✔。
- **TSF 身份** ✔（自检页会正确显示当前输入法 ✔）：搜狗 CLSID `{E7EA138E-…}` + profile `{E7EA138F-…}` ✔（本机注册表读得 ✔）。
- **豆包不再算"已下线"** ✗→✔：`IsRetiredProviderValue` 现在**只剩** `typeless` / `windows` ✔，自测与门禁都钉住这一点 ✔。

### 12.1.1 已下线：Typeless 与 Windows 语音输入（2026-09-13 深夜，用户判定 ✗）

| 已下线工具 | 原因（用户实测 ✔） | 迁移方式 ✔ |
| --- | --- | --- |
| **Typeless** | 选 Typeless 却**实际触发八哥说** ✗（两者默认快捷键都是 `rightalt` ✗） | 存有该值的配置 **自动迁移为微信输入法** ✔ 并**弹窗点名** ✔ |
| **Windows 语音输入** | Win+H 是**单击切换** ✔，被"按住"驱动 → **开始/结束反复触发** ✗、**停不下来** ✗ | 同上 ✔ |

- 两者已从**所有下拉框**、`NormalizeProviderKey`、默认快捷键/触发/延时、进程匹配、状态文案中**彻底移除** ✔；`IsRetiredProviderValue` 现在同时覆盖 `doubao` / `typeless` / `windows` ✔（自测 + 门禁钉住 ✔）。
- 迁移会记一行 `PROVIDER MIGRATED retired=<值> action=use_wechat_input_method defaults=applied` ✔ 并弹一次提示 ✔。

### 12.1.2 快捷键的"唯一归属"规则（讯飞用 F6，因此由**冻结采集件**发送 ✔）

**实测发现的问题** ✗（2026-09-13 日志 ✔）：旧版对 Typeless / Windows 是**主机与冻结采集件同时发送**快捷键 ✗（`PROVIDER HOTKEY SESSION action=down` ✔ 与 `TRANSCRIPTION TRIGGER … sent=True` ✔ 同时出现 ✔）——两个组件各按一次切换键 ✔ = 自己开始/自己停止 ✗。

**因此新增"唯一归属"规则** ✔（`ProviderHotkeyIsHostDriven` ✔）：

1. **微信输入法** ✔ → 面板由**冻结采集件**驱动 ✔，主机**从不**碰它的快捷键 ✔。
2. 其它工具 ✔ → **采集件能解析就该采集件发** ✔；只有**采集件发不出去**的快捷键 ✔（标点键 ✗ —— 冻结件只认字母/数字/F1–F24/少量具名键 ✗）或**免驱动模式**（根本没有采集件 ✔）才由**主机**发 ✔。
3. **讯飞的 F6 在采集件能力范围内** ✔ → **采集件发** ✔，主机**不发** ✔（切换值只需改一个常量 ✔，归属判断自动跟着走 ✔）。
4. 触发语义跟随**有效触发值** ✔：`hold` → 按下 KeyDown、松开发 KeyUp ✔；`toggle` → 按下与松开各 tap 一次 ✔。

**为什么保留标点支持** ✔：讯飞 3.0 安装时写进注册表的默认值是 `Ctrl + Shift + Alt + [` ✔（`iFlyImeVoiceShiftHotKey` ✔），但讯飞设置界面**只让绑 F6** ✗ → 应用最终用 F6 ✔；标点支持（`PunctuationVirtualKey` ✔ / 主机的 `0xDB` ✔ 与 `[` ✔ 等价 ✔）作为**通用能力**保留 ✔，并仍由自测 + 门禁钉住 ✔。

**证据** ✔：主机自测断言 ✔（`ProviderHotkeyIsHostDriven` 多组合 ✔、`FrozenCaptureCanSendShortcut("f6")=true` ✔ 而 `ctrl+shift+alt+[` 为 false ✔、镜像解析器 ✔、hold/tap 归属 ✔）；`scripts/validate.js` **逐字比对**主机镜像与冻结采集件的键名表 ✔（采集件冻结 ✔，单边改动会变成"没人发送" ✗）。

### 12.2 共用形态 `0xFF/0x5E` 的判别（电源键不再触发录音 ✔）

**实测结论** ✗：电源键与（重连后的）麦克风键**携带完全相同的字段** ✗ —— `vk=0xFF`、`scan=0x5E`、`flags=0x02`（按下 ✔）/ `0x03`（松开 ✔）→ **用户态无法从事件本身区分** ✗。

**因此改为按历史判别** ✔（`scripts/VoxDeckInputBridge.cs` 的 `IsVoiceRawCandidate` ✔）：

- 只要**最近 10 分钟**见过麦克风的**翻译形态**（`vk=0x74` 或 `0xF5` ✔）→ `0xFF/0x5E` 归**电源键** ✔，**不触发录音** ✔（并记一行日志 ✔：`Voice shared form … treated as the power key: the F5 form was seen Ns ago` ✔）。
- 若 **10 分钟**内没见过翻译形态 ✔（例如重连后遥控器只用这种形态上报 ✔）→ 才把它当**麦克风** ✔，保证重连后仍能听写 ✔。
- 实测依据 ✔：本机日志窗口内 F5 形态 **881** 次、共用形态 **373** 次，且共用形态此前被记为 `Key 录音键 … source=raw_input` ✗ —— 最近 12 次会话里 **4 次是 `audio=0ms` 的空录音** ✗，正是误触发 ✔。

### 12.3 收音质量（用户以微信输入法为质量基准 ✔）

- 最近 12 次会话实测 ✔：输出电平中位 **4%** ✗、`avg_gain` 已 **4.3–6.6 倍** ✗、多次**峰值 100%** ✗（碰触噪声 ✔）→ 与上一轮结论一致 ✔：**声源侧问题** ✗，软件侧不再加大增益 ✔。
- **不同工具的识别质量差异属工具自身能力** ✗（降噪/AGC/语言模型 ✔），言灵只负责把遥控器音频送进 `CABLE Output` ✔，不参与识别 ✔。
- 用户侧仍是那四步 ✔：**距离 10–20 cm 并对准顶部麦克风孔** ✔ → **Windows「CABLE Output → 属性 → 级别」100%** ✔ → **换新电池** ✔ → **握稳、减少摩擦** ✔。
- 只读核对 ✔：`powershell -File scripts\tests\Get-CaptureLevels.ps1 -Last 15` ✔。

### 12.4 最终规则（2026-09-13 晚，实测推翻了 §12.2 的窗口方案 ✗）

**现场证据** ✗：用户再次报告"按下开机键仍触发录音" ✔。日志显示共用形态**依旧**走了兜底 ✗：
`Voice raw VK fallback vk=0xFF scan=0x5E` → `Key 录音键 DOWN vk=0xFF scan=0x5E source=raw_input` ✔，而**判别日志一次都没出现** ✗。

**为什么窗口方案没用** ✗：桥启动后不久（01:33:26 启动、01:35:37 按下 ✔）**还没有任何 F5** ✗ → 规则判定"F5 不新鲜 ⇒ 当麦克风" ✗ → 10 分钟窗口在这种时序下**必然放行** ✗。

**本机实测的分布** ✔：F5 形态 **1571** 行 ✔、共用形态 **385** 行 ✔、兜底触发 **103** 次 ✗ —— **麦克风始终以 F5 上报** ✔，**共用形态就是电源键** ✔。

**因此最终规则** ✔（`scripts/VoxDeckInputBridge.cs` 的 `IsVoiceRawCandidate` ✔）：
- `vk=0x74` / `0xF5` → **是**麦克风 ✔（并记录时间戳 ✔）
- `vk=0xFF` + `scan=0x5E` → **不是**麦克风 ✔（记一行 `Voice shared form … belongs to the power key; the translated F5 form is the microphone` ✔）→ **不会开始录音** ✔
- 自测断言 ✔：F5 必须是语音候选 ✔、`0xFF/0x5E` 必须**不是** ✔

**可逆性** ✗：如果将来某台遥控器**只在重连后**用共用形态上报麦克风 ✔，把那一处 `return false` 改回语音候选即可 ✔（代码注释里已写明这是"唯一要改的一行" ✔）。

### 12.5 最终可用的分工（2026-09-13 晚，用户真机确认 ✔）

**两类必须同时成立** ✔（用户要求 ✔）：

| 键 | 上报形态 | 行为 | 隔离方式 |
| --- | --- | --- | --- |
| **录音键** | `vk=0x74` / `scan=0x3F`（翻译形态 ✔） | 按住说话、松开结束 ✔ | 钩子在"遥控器在场"时**拦截** ✔，不落给前台应用 ✔（浏览器不会再刷新 ✔） |
| **开机键（电源）** | `vk=0xFF` / `scan=0x5E` ✔ | **按普通映射键使用** ✔：短按 / 长按 / 双击各执行自己的动作 ✔ | 桥把该形态**判给电源键** ✗→✔（`IsVoiceRawCandidate` 返回 false ✔），因此**不会**开始录音 ✔，也不会被录音兜底抢走 ✔ |

**动作存在两个存储里，必须一致** ✗（本轮踩了两次 ✔）：
1. **映射表** ✔：`vibe-mic-config.json` → 顶层 `mappings` 与**当前激活 Profile** 的 `mappings`（键名 `电源键` ✔）
2. **手势层（优先级更高 ✗）** ✔：`UserData\gesture-layers.json` → `{"key":"power","shortAction":…,"longAction":…,"doubleAction":…}`

> **只改映射表不生效** ✗：手势层里的 `null` 会盖掉映射表里的动作 ✔（本轮实测：映射表已设 DeepSeek ✔，投影后 `shortShortcut` 仍是 `none` ✗；把手势层补齐后才生效 ✔）。
> **推荐做法** ✔：**在应用的「快捷键」页设置** ✔ —— 界面会同时写两个存储 ✔；手工改文件时**两处都要改** ✔，改完**重启应用** ✔，并**用投影结果反查** ✔：`%LOCALAPPDATA%\Programs\Vibe Flow Remote\voxdeck-shortcuts.json` 里 `"name":"power"` 的 `enabled` 应为 `true` ✔ 且 `shortShortcut` 为预期动作 ✔。

**本机当前值** ✔（用户原有设置，已按原样还原 ✔）：短按 = `open-url:https://platform.deepseek.com/usage` ✔；长按 = B 站视频页 ✔；双击 = `task-switcher` ✔。
备份 ✔：`vibe-mic-config.json.before-restore-*.bak`、`gesture-layers.json.before-restore-*.bak`。

### 12.6 触发方式的"有效值"（2026-09-13 深夜 ✔；当天更晚随 §12.1.1 下线而收窄 ✔）

**历史报告** ✗（两条 ✔，均已由下线处理 ✔）：

1. 选 **Typeless** 却**实际触发了八哥说** ✗ —— 因为两者的默认快捷键那时都是 `rightalt` ✗ → 现已**整条下线** ✔（§12.1.1 ✔）。
2. 选 **Windows 语音输入**后**停不下来** ✗、开始与结束提示音连成一片 ✗ —— 因为 Win+H 本身就是**单击切换** ✔，被"按住"驱动就会反复开始 / 结束 ✗ → 现已**整条下线** ✔（§12.1.1 ✔）。

**保留下来的规则** ✔（`scripts/VibeMic.cs` ✔）：

- `EffectiveTriggerForProvider(provider, trigger)` ✔：**微信输入法一律 `toggle`** ✔（**无论配置里存的是什么** ✔，因为它是冻结的稳定路径 ✔）；其余工具**尊重用户选择** ✔（`hold` / `toggle` ✔）。采集参数传**有效值** ✔：`SafeCaptureArgument(EffectiveTriggerForProvider(config.inputMethod, config.inputMethodTrigger))` ✔。
- `PopulateTriggerModeOptions` ✔：微信输入法**只给一个**选项 ✔「单击切换（稳定）」✔ —— 界面上就**不可能**给它选"按住触发" ✔。
- 讯飞默认 **`hold`** ✔（`DefaultTriggerForProvider` ✔），因为讯飞自己的语音栏是**长按说话 / 松手结束** ✔；用户在语音页可改成"单击切换" ✔。

**证据** ✔（含负控 ✔）：把 `toggle` 的 pin 临时改成 `if (false)` ✗ → `VibeMic.exe --self-test` **退出码 1** ✗，报错正是 `The effective trigger policy drifted from the frozen stable voice tool` ✔；还原后自检**通过** ✔（退出码 0 ✔）。

## 13. 按键固化（2026-09-13 晚，用户真机确认可用后 ✔）

以下值均为**实测自运行中的应用**（配置 / 手势层 / 桥投影 / 日志 ✔），不是凭记忆 ✔。

### 13.1 录音键 ✔（**不可映射、不可分层** ✗）

| 项 | 冻结值 |
| --- | --- |
| 上报形态 | `vk=0x74` / `scan=0x3F`（翻译后的 F5 ✔） |
| 行为 | 按住说话、松开结束 ✔（`voiceMode = hold` ✔） |
| 隔离 | 遥控器在场时由钩子**拦截** ✔，不落给前台应用 ✔（浏览器不再刷新 ✔） |
| 映射表里 | **没有**录音键映射 ✔（桥投影中查无此键 ✔ = 设计如此 ✔） |
| 正常日志 | `Key 录音键 DOWN vk=0x74 scan=0x3F source=rc003_present_hook` ✔ |
| 异常信号 | 出现 `RAW KEY DOWN vk=0x74 …` 而无 `Key 录音键` 行 ✗ = F5 直通到了前台应用 ✔ |

### 13.2 开机键（电源）✔（**当普通映射键使用** ✔，且**绝不触发录音** ✔）

| 项 | 冻结值 |
| --- | --- |
| 上报形态 | `vk=0xFF` / `scan=0x5E` ✔（与录音键的形态**完全不相交** ✔） |
| 语音候选 | **否** ✗（`IsVoiceRawCandidate` 返回 false ✔，并有自测断言 ✔）→ 按下**不会开始录音** ✔ |
| 桥投影 | `enabled=true`、`suppress=true`、`mode=shortlong` ✔ |
| **动作存储（两处必须一致 ✗）** | ① `vibe-mic-config.json` 的 `mappings["电源键"]`（全局 + **当前激活 Profile** ✔）② `UserData\gesture-layers.json` 的 `{"key":"power",…}`（**优先级更高 ✗**） |
| 本机当前值 ✔ | **短按** = `open-url:https://platform.deepseek.com/usage` ✔；**长按** = B 站视频页 ✔；**双击** = `task-switcher` ✔ |
| 成功日志 ✔ | `Gesture action executed label=电源键 phase=短按 action=… success=True` ✔ + `Action receipt button=电源键 … success=True` ✔ |
| 判别日志 ✔ | `Voice shared form vk=0xFF scan=0x5E belongs to the power key; the translated F5 form is the microphone` ✔ |
| 可逆性 ✗ | 若某遥控器**只在重连后**用该形态上报麦克风 ✔，把 `IsVoiceRawCandidate` 里那一处 `return false` 改回语音候选 ✔（注释已标注"唯一要改的一行" ✔） |

### 13.3 其他快捷键 ✔（本机**实测**现值，来自 `gesture-layers.json` ✔）

| 键 | 短按 | 长按 | 双击 |
| --- | --- | --- | --- |
| 上 | 空 | `pageup` ✔ | `ctrl+x`（剪切）✔ |
| 下 | 空 | `pagedown` ✔ | `ctrl+a`（全选）✔ |
| 左 | 空 | `browserback` ✔ | `ctrl+z`（撤销）✔ |
| 右 | 空 | `ctrl+shift+z`（重做）✔ | `ctrl+s`（保存）✔ |
| 确认 | 空 | `volumemute` ✔ | `mediaplaypause` ✔ |
| 菜单 | 空 | 空 | `volumeup` ✔ |
| Home | 空 | 空 | `launch-client:cursor` ✔ |
| TV | 空 | `launch-client:chatgpt` ✔ | `volumedown` ✔ |
| **电源** | 见 §13.2 ✔ | 见 §13.2 ✔ | 见 §13.2 ✔ |

**映射表（`mappings`）里另有** ✔：`确认键 = enter` ✔、`Home = win+d`（`Home:long = win+shift+s` ✔）、`TV = task-switcher` ✔、`功能键 = ctrl+c`（`:long = ctrl+v` ✔）、`上/下/左/右键 = up/down/left/right` ✔。

**注意** ✗：手势层（§13.3 表）**优先于**映射表 ✔ —— 同一键两层都有配置时，**手势层生效** ✔；要改就用「快捷键」页（会同时写两处 ✔）或**两处都改 + 重启 + 反查投影** ✔。

### 13.4 两类必须同时成立的验收清单 ✔

1. **按住录音键** → 正常录音 ✔，**前台页面不被刷新** ✔（日志见 13.1 ✔）
2. **短按开机键** → 执行你设的短按动作 ✔（日志 `success=True` ✔），**不开始录音** ✔
3. **长按 / 双击开机键** → 各自动作 ✔
4. 改完任何按键后 **必须**：`node scripts/validate.js` → `VibeMic.exe --self-test` / `VoxDeckInputBridge.exe --self-test` → `scripts/check-ui-matrix.ps1`（12 例）✔

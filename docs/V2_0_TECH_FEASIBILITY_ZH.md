# 言灵 Vibe Flow Remote 下一版技术调研报告（Windows 10/11 · WinForms/.NET Framework 4.x）

> 约束前提：采集端 `VibeMicAtvvCapture.exe` v1.2.1.0 冻结不可改；宿主为 .NET Framework 4.x WinForms 单进程 + 键盘桥接进程；音频链路为 `[采集程序] → CABLE Input → CABLE Output → [第三方 ASR/输入法] → 文字回填当前输入框`。
> 标注口径：**已验证可行** = 有官方文档/权威来源直接支撑；**需实验验证** = 原理成立但必须在目标机型实测（附 PoC）；**不可行** = 无受支持 API 或代价与收益严重不匹配。

## 1. 音频质量

### 1.1 (a) 冻结采集端的可调参数能改善多少

- **增益（gain=1.0）→ 需实验验证，收益取决于削顶发生的位置（见 1.4）**。若削顶发生在采集程序内部的浮点→整型/写设备阶段，把 gain 降到 0.7–0.8（≈ −3 dB）可留出余量并消除该级失真；若削顶发生在遥控器麦克风/ATVV 编码等上游，降 gain 只会整体变小、失真仍在。
- **speech 处理（AGC/降噪）→ 需实验验证，默认建议保留但做 A/B**。AGC 对 ASR 是双刃剑：安静环境下提升信噪比，但会抬高底噪与停顿段噪声、产生抽吸感，反而增加误识别。用同一段固定话术在"开/关 speech 处理"下各录 10 条，比较第三方 ASR 的识别字错率（CER）再定。
- **drainMs=180 → 需实验验证，建议扫 180/250/300/400 ms**。这是"松开按键到尾音截断"的直接相关项，尾字丢失是按住说话类产品的典型错误源；加大 drainMs 只增加约 0.1–0.2 s 尾部静音，对 ASR 基本无害。
- **约 60 s 上限 → 不可通过参数改变（硬限制）**。缓解只能做产品层：悬浮窗在 45 s/50 s 变色提示 + 轻提示音，超时后提示"已自动结束，可继续按住说下一句"；不要在 UI 里暗示能连续录制超过 60 s。
- **采样率/格式对齐 → 需实验验证，优先级最高、成本最低**。确认 CABLE Input（渲染端点）与 CABLE Output（捕获端点）的默认格式（16 kHz/44.1 kHz/48 kHz、16/24/32-bit float）与采集程序输出一致；VB-CABLE 走"Multi-Format Audio Engine"通常可自动适配（[VB-CABLE 官方页](https://vb-audio.com/Cable/index.htm)），但同类方案中确有"两端采样率必须一致"的型号（同页 Hi-Fi Cable："needs to be configured with the same samplerate on its Input and its Output"），说明采样率错配是本链路真实的无声/失真故障源，应在自检页显式校验并给出修复指引。

### 1.2 (b) 在 CABLE Input → CABLE Output 之间插入 DSP

- **方案 B1（推荐，改动最小）：用 Equalizer APO 在 CABLE 端点上挂 APO。已具备可行性条件，需实验验证。** Equalizer APO 支持输入设备的 **capture 阶段** 与按设备名/GUID 匹配（配置命令 `Device: ...`、`Stage: capture`、`Preamp: -6 dB`、`Filter: ON HP/NO ...`、`Convolution: xxx.wav`），并有成熟的"麦克风 + VST 降噪（如 RNNoise 类插件）"用法（[配置参考](https://sourceforge.net/p/equalizerapo/wiki/Configuration%20reference/)）。把 APO 装在 **CABLE Output（捕获端点）** 上时，冻结程序仍写 CABLE Input、第三方 ASR 仍从 CABLE Output 录音，**链路与用户侧配置零改动**，是实现"下游 −3～−6 dB 衰减 + 高通去低频噪声 + 降噪"的最短路径。限制与风险：Equalizer APO 是第三方用户态 APO，需管理员安装/重启音频服务，设备驱动更新或更换音频设备后需重新绑定；它作为系统效果 APO 注入，在受保护内容（DRM）播放路径会被拒绝加载（本产品链路不涉及）；它是外部依赖，应由安装程序静默配置 + 自检页校验"APO 是否生效"（放一段测试音看电平变化）。
- **方案 B2：Voicemeeter 作为中间混音台。可行，但代价更大。** `[采集程序] → CABLE Input → (Voicemeeter 以 CABLE Output 为硬件输入) → B1 → VoiceMeeter Output → [第三方 ASR 改录 VoiceMeeter Output]`。代价：多一级缓冲（典型 +20–60 ms）、多一个虚拟设备、必须让用户改第三方工具的麦克风选择、分发需遵守 [Voicemeeter 分发规则](https://vb-audio.com/Services/licensing.htm)（Potato 不允许捆绑）。仅当需要多路混音/压缩器/门限等更复杂处理时选它。
- **方案 B3：自研 APO / 自研虚拟声卡。不可行。** 官方文档要求 APO 与驱动包一同安装、通过 INF 把 APO 注册为该音频设备的 **PnP 子设备（组件化 APO）**，且明确"新的组件化 APO 设计不允许 APO 全局注册后被多个驱动复用，每个驱动必须注册自己的 APO"——即无法为 VB-CABLE 这个第三方驱动挂载自研 APO；即便自研虚拟声卡，也需要内核态驱动 + attestation/WHQL 签名（[实现音频处理对象](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects)）。
- **方案 B4：宿主侧 WASAPI 环回 + 再渲染回写。不可行。** 宿主与冻结程序同写一条虚拟线会产生双写/回声；改用另一条虚拟线则等价于 B2 但更脆弱。
- **分发合规提示（做安装包前必读）：** VB-CABLE 是 Donationware，官方 **允许把 VB-CABLE 安装包与你的应用一起分发、甚至静默安装**，条件是终端用户能识别它是 VB-Audio 的产品且有机会付费（需注明 www.vb-cable.com），并建议规模较大的分发方支付一笔可观的授权费；**VB-CABLE A+B / C+D 不允许捆绑分发**（[ licensing](https://vb-audio.com/Services/licensing.htm)）。

### 1.3 (c) 宿主侧"旁听"音量/削顶并实时提示

- **结论：已验证可行（API 层面），隐私影响极小，需实验验证虚拟设备上的读数是否可用。**
- **首选 `IAudioMeterInformation::GetPeakValue`**（[文档](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudiometerinformation-getpeakvalue)）：对 "CABLE Input" 渲染端点取 `IMMDevice → Activate(IAudioMeterInformation)`，20–30 Hz 轮询即可得到 0–1 的端点峰值，**完全不打开数据流、不读取任何音频样本**，只返回一个浮点数 → 与"不读取/保存/上传音频内容"的隐私原则完全兼容，可直接做成悬浮窗上的实时电平条 + "接近满刻度"黄色/红色提示。
- **备选 WASAPI 环回 `AUDCLNT_STREAMFLAGS_LOOPBACK`**（[环回录制](https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording)）：在 CABLE Input（渲染端点）上以共享模式初始化捕获流即可拿到引擎输出（Windows 10 1703 起支持事件驱动环回；**独占模式不支持环回**）。它能拿到真实样本，从而可算真峰值/削顶计数，但会读入音频数据 → 只应在"诊断模式"下由用户显式开启，且仅统计数值、不落盘、不上传。
- **风险**：虚拟声卡上的电平表读数依赖驱动实现的计量上报，若 `GetPeakValue` 恒为 0 或恒为 1，退化为环回方案；提示阈值需按设备校准（不同设备端点增益不同），建议在首次运行时做一次"正常说话校准"。

### 1.4 (d) 削顶（raw peak 100%）的诊断与缓解

- **诊断（一次性，0.5–1 人日）：** 用 Audacity 或 20 行 NAudio 代码从 CABLE Output 录成 **float32 WAV**，统计 ① `|x| ≥ 0.999` 的样本数；② **连续饱和样本长度 ≥ 3** 的片段数（整型栅格 ±32767/32768 说明削顶发生在 int16 环节）。
- **定位（关键判据）：** 同一句话分别用 gain = 1.0 / 0.8 / 0.6 录三遍。**饱和数量随 gain 下降而减少** → 削顶在采集程序之后（可救）；**饱和数量几乎不变** → 削顶在遥控器麦克风/ATVV 编码等上游（软件不可救，只能改声学条件）。
- **缓解优先级：**
  1. **下游先衰减、再交给 ASR**：在 B1 的 Equalizer APO 里设 `Preamp: -4 dB`（按实测取 −3～−6 dB）。ASR 模型对绝对电平不敏感、对削顶失真敏感，衰减比"提高音量"有效得多。
  2. **采集端 gain 降到 0.7–0.8**（若参数可调）留 3 dB 余量，需要时由 ASR 端或系统"麦克风加强"补偿。
  3. **上游削顶只能靠使用行为**：产品内给出"离遥控器 15–25 cm、不要正对出音孔"的图示引导，并在电平条红色时提示"声音过大，请稍微离远一点"。
- **不要做的事**：不要用"再放大 + 限幅器"掩盖——限幅器只会引入更多失真；也不要盲目加大 gain 去追求"更响"，那会直接推高削顶概率。

## 2. 蓝牙遥控器状态（电量 / 连接质量 / 断连）

- **已配对 BLE 外设的电量读取：需实验验证（API 路径已验证存在）。**
  - **主路径（官方推荐姿势）：** `Windows.Devices.Power.Battery.GetDeviceSelector()` → `DeviceInformation.FindAllAsync` → `Battery.FromIdAsync(device.Id)` → `GetReport()`（`Status` / `RemainingCapacityInMilliwattHours` / `FullChargeCapacityInMilliwattHours`），并订阅 `ReportUpdated` 事件（[FromIdAsync 文档](https://learn.microsoft.com/en-us/uwp/api/windows.devices.power.battery.fromidasync)、[获取电池信息示例](https://learn.microsoft.com/en-us/previous-versions/windows/apps/dn895210(v=win.10))）。**限制**：很多 BLE 外设只上报百分比，容量字段可能为 `null`（官方提示模拟器上必然为 null，实机也常见）→ 必须做"无数据即隐藏"的降级。
  - **回落路径：** 直读 GATT 电池服务 `0x180F` 的电量特征 `0x2A19`：`BluetoothLEDevice.FromIdAsync(...)` → `GetGattServicesForUuidAsync(0x180F, Uncached)` → `ReadValueAsync` + `ValueChanged` 订阅（[BluetoothLEDevice 文档](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothledevice.frombluetoothaddressasync)）。**限制**：Windows 需要能建立 GATT 会话；双模（Classic+BLE）HID 遥控器在 Windows 上可能只以经典 HID 呈现而拿不到 GATT；**若采集程序已长连接遥控器，宿主再连可能抢占或失败** → 只读、失败即降级，绝不能影响录音链路。
  - **HID 电池（Usage Page 0x06 / Usage 0x20 feature report）：需实验验证，成功概率低。** 系统键盘/鼠标类顶层集合会被 Windows 独占，普通用户态通常取不到；遥控器的厂商自定义集合有可能可读，值得用 5 行 P/Invoke（`HidD_GetFeature`）试一次。
- **RSSI / 链路质量：不可行。** 连接态没有受支持的 RSSI API：`RawSignalStrengthInDBm` 只在**扫描广播**时有效，而已连接设备通常停止广播；`BluetoothLEDevice` 不暴露 RSSI。可用的代理指标：`ConnectionStatus`（Connected/Disconnected）、GATT 读取往返耗时、HID 输入事件的间隔抖动。
- **断连事件：已验证可行（需去抖）。** `BluetoothLEDevice.ConnectionStatusChanged` + `DeviceWatcher`/`DeviceInformation`（按 AEP 或 `GetDeviceSelectorFromBluetoothAddress`）的增删更新；配合"HID 输入静默超时"判断。事件有数秒延迟且 BLE 会自动重连，需 2–3 s 去抖后再提示，避免状态闪烁。
- **在 .NET Framework 4.x 中调用 WinRT：已验证可行。** 官方为 .NET Framework 项目提供 `Microsoft.Windows.SDK.Contracts` NuGet 包（按最低目标系统选 10.0.19041 等版本），绝大多数 `Windows.Devices.*` API 在**未打包**桌面应用中可用；仅少数 API 需要包标识（MSIX），该清单中**不含** `Windows.Devices.Bluetooth` / `Windows.Devices.Power` / `Windows.Devices.Enumeration`（[在桌面应用中调用 WinRT API](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps)、[桌面应用不支持的 WinRT API](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-api-desktop-app-support)）。C# 侧用 `AsTask()` 包装；注意把 TFM/包版本与最低支持的 Windows 版本对齐，并对 `ApiInformation` 做版本守卫。
- **配对不能由本应用完成：** `DeviceInformationPairing.PairAsync` 被官方明确列为**桌面应用不支持**的成员 → 产品必须引导用户去"设置 → 蓝牙"完成配对，再回到应用。
- **备选（纯 Win32）：** SetupAPI/CfgMgr32 枚举 `BTHLE\` 设备、`bluetoothapis.h` 的 `BluetoothGetDeviceInfo` 只能拿到经典蓝牙侧的配对/服务信息，取不到 BLE GATT 电量 → 仅作"设备是否存在"的辅助判断。
- **权限：** 上述路径均为普通用户态、无需管理员；Printer/POS 等需要包标识的设备 API 与本场景无关。

## 3. 输入法 / 焦点集成边界

### 3.1 为什么多数中文输入法忽略注入输入

- **注入标记是可读的、且是公开契约：** 底层键盘钩子的 `KBDLLHOOKSTRUCT.flags` 含 `LLKHF_INJECTED`(bit4) 与 `LLKHF_LOWER_IL_INJECTED`(bit1)，官方文档直言 "Testing LLKHF_INJECTED (bit 4) will tell you whether the event was injected"；`SendInput` 与 `keybd_event` 都会置位（[KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)）。输入法/编辑器只需挂一个低级钩子即可**整体丢弃**注入按键——这足以解释豆包输入法对 `keybd_event`、`SendInput`、模拟点击一律不响应。
- **原始输入还能区分"伪设备"：** `WM_INPUT` 的 `RAWINPUTHEADER.hDevice` 对注入事件为 NULL/非物理设备，是社区广泛使用的第二种判据（配合扫描码/时间戳一致性、`GetAsyncKeyState` 不同步等）。
- **UIPI 是另一条独立边界：** 低完整性进程无法向高完整性（管理员）窗口注入，`SendInput` 静默失败（[UI Automation / winapp CLI 文档](https://learn.microsoft.com/en-us/windows/apps/dev-tools/winapp-cli/ui-automation) 亦说明消息投递可绕过 UIPI 而注入不行）。这解释了"以管理员运行的编辑器完全收不到字"的另一类失败，与豆包的过滤机制是两回事。
- **补充：** 现代输入法的上屏目标多为自绘窗口，用"模拟按键走上屏"在架构上本就不成立（上屏必须走其候选/云输入通道），这也是模拟输入方案的天然上限。

### 3.2 合规替代路径（按推荐顺序）

1. **剪贴板 + 受控 Ctrl+V（现状，微信输入法可用）** — 已验证可行。注意 Ctrl+V 本身仍是注入事件，能被过滤，因此它不是万能解；且需要处理剪贴板序列号抢占与"粘贴目标丢失焦点"。
2. **UI Automation 直接写值 — 需实验验证，是破解"反注入输入法"最有希望的方向。** 依次尝试 `ValuePattern.SetValue` → `LegacyIAccessiblePattern.SetValue` → `TextPattern`（Chromium 等目标的 `TextPattern` 常为只读，需回落；[UIA 自动化文档](https://learn.microsoft.com/en-us/windows/apps/dev-tools/winapp-cli/ui-automation) 也把 LegacyIAccessible 描述为仅支持 TextPattern 的编辑控件的回退方案）。**优点**：完全不产生键盘事件，天然绕过注入检测；且**只写不读**，符合隐私约束。**限制**：① 目标必须暴露可写的 UIA 提供者（自绘/部分 Electron 编辑器可能只有只读 TextPattern）；② 直接写值会绕过应用的输入事件，React 类受控组件可能不同步；③ 跨完整性（管理员窗口）受 UIPI 限制；④ 密码/安全输入框会被拒绝。
3. **消息级写入（标准控件兜底）— 需实验验证。** `PostMessage(WM_SETTEXT / EM_REPLACESEL / WM_CHAR)` 无注入标记，对经典 Win32 Edit/RichEdit 有效；对自绘编辑器与浏览器内核基本无效，且同样受 UIPI 限制。
4. **TSF 文本服务（`ITfThreadMgr` / `ITfContext` / `ITfRange::SetText`）— 技术可行，但成本极高，下一版不建议做。** 插入文本需要处在目标进程的 TSF 上下文中，必须把自己注册为系统级 TIP（HKLM COM 注册 + 管理员安装 + 出现在语言栏），开发量以"周"计，并会与用户现有输入法并排加载（[编辑上下文](https://learn.microsoft.com/en-us/windows/win32/tsf/edit-contexts)）。除非产品定位从"伴侣"升级为"输入法"，否则投入产出比不成立。
5. **uiAccess 签名应用 — 验证可行但解决不了本问题。** 它要求 exe 具备从受信任根 CA 链出的 Authenticode 签名并安装在安全位置（Program Files 等）、manifest 声明 `uiAccess="true"`，用于**向高完整性窗口注入**；但它不会移除 `LLKHF_INJECTED` 标记 → 对豆包这类"过滤注入"的输入法无效。仅在需要支持"以管理员运行的编辑器"时才启用，而它与"免管理员安装 + 自动更新"（%LocalAppData%）互相冲突，需明确取舍。
6. **虚拟 HID 驱动 / 硬件注入 — 不可行（作为产品决策）。** 需要内核驱动签名，行为与键盘记录/自动化木马高度相似，会被安全软件重点关照，且彻底背离"用户态伴侣程序"的定位与信任基础。
7. **合规性说明：** 上述 1)–3) 均满足"不读取第三方转写文字（剪贴板方案仅做内存内瞬时回填、不落盘不上传）、不自动回车、不自动发送"；建议在设置页写明这三点，作为隐私卖点而非技术细节。

### 3.3 产品化建议

- 实现一条**输入通道自动降级链**：`UIA 写值 → 剪贴板 + Ctrl+V → 明确提示用户更换输入法`，并把每次降级写入本地日志（不含文字内容）。
- 自检页增加**目标程序兼容性探测**：能否取得焦点、是否存在可写 `ValuePattern`、粘贴是否生效，结果缓存进配置，避免"失败后才知道不兼容"。

## 4. 交付与分发

### 4.1 Authenticode 代码签名

- **行业现状：** 2023 年 6 月起，公开可信代码签名证书的私钥必须存放于 FIPS 140-2/3 硬件（令牌或云 HSM），个人开发者很难再拿到"文件 + 口令"式的 PFX。
- **Azure Trusted Signing / Artifact Signing（最低成本的正规云签名）：已验证存在且可用。** Basic 约 **$9.99/月**（[实测过程与价格](https://textslashplain.com/2025/03/12/authenticode-in-2025-azure-trusted-signing/)），服务端保存私钥、用 SignTool + DLIB 或 Azure DevOps/GitHub Actions 集成；签名证书仅 3 天有效期，**必须带时间戳**否则签名很快过期；**不签发 EV 证书**（[官方 FAQ](https://learn.microsoft.com/en-us/azure/artifact-signing/faq)）。**地域限制（关键）：** Public Trust 证书面向组织开放的国家/地区含美、加、欧盟、英、澳、新、日、韩、新、瑞士、挪威、以色列，**个人开发者必须位于美国或加拿大**（[快速入门 - 先决条件](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)）。
- **中国大陆开发者的现实选项：** ① 以海外实体走 Artifact Signing/EV；② 购买 OV/EV 证书并使用 CA 提供的云签名或硬件令牌（OV 常见报价量级 ¥1500–4000/年、EV 约 ¥3000–8000/年，以各 CA/经销商报价为准）；③ **项目开源则走 [SignPath Foundation](https://signpath.org/)：对合格开源项目免费**，私钥存于其 HSM、由基金会背书"该二进制由你的仓库构建"，无需个人身份认证——对本产品这类开源伴侣工具是最省钱的方案（需满足其 OSS 条款，且签名主体是项目而非个人）。
- **SmartScreen 现实预期：** 新证书初期仍会弹"Windows 已保护你的电脑"；信誉按文件哈希的下载历史累积，可向 Microsoft Security Intelligence 提交加速（官方 FAQ 明示并链接 SmartScreen 信誉文档）。**不要承诺"签了就无警告"。**

### 4.2 自动更新

- **Squirrel.Windows：不建议新项目采用。** 其生态已迁移（原作者另立 Velopack，[Clowd.Squirrel](https://github.com/clowd/Clowd.Squirrel) 也已并入 Velopack），老项目虽能在 .NET Framework + WinForms 下跑，但依赖与维护状态是长期风险。
- **NetSparkle：最贴合当前技术栈。已验证可用。** 面向 .NET 4.6.2+ / WinForms（含预置 UI），使用 Ed25519 签名校验，自托管 appcast（[NetSparkleUpdater/NetSparkle](https://github.com/NetSparkleUpdater/NetSparkle)）。坑：签名密钥必须固化进发布流程（密钥丢失 = 老用户无法升级）；仍需自己实现"退出 → 替换文件 → 重启"，且**不能替换正在运行的程序集**（.NET 会锁定文件）。
- **Velopack：现代化方案，但会改变安装布局。** 支持 .NET Framework 目标、自带安装包/增量/回滚（[文档](https://docs.velopack.io/)），代价是引入 `%LocalAppData%` 下的版本目录 + stub launcher 模型 → 与"绿色便携版"体验冲突，需要双轨维护。
- **自研（推荐：NetSparkle 思路 + 自研落地）：**
  - 更新清单用 **Ed25519/RSA-PSS 签名**（裸 SHA256 只能防传输损坏，防不了篡改），下载后再对 exe 做一次 **WinVerifyTrust/Authenticode 校验**（双重校验）。
  - 落地用**版本目录 + 原子切换**：解压到 `...\app\<version>\`，`current.json` 决定加载哪一版；更新前必须退出全部进程（含键盘桥接进程）并等待文件句柄释放。
  - **回滚必须自动化**：新版本启动后 N 秒内未上报"健康"，下次启动自动切回上一版（保留最近 2–3 个版本目录）。
- **便携版 + 保留配置（兼容做法）：**
  - 配置目录策略：exe 同级存在 `portable.flag` → 用 `<exe>\data`；否则用 `%LOCALAPPDATA%\<Vendor>\<App>`（含音频设备 GUID 等本机相关项，**不建议漫游**）。
  - 升级保留：配置文件带 `schemaVersion` + 迁移函数 + 写前备份（`config.json.bak.<ver>`）；**使用版本目录布局时配置必须放在版本目录之外**，否则升级即丢配置。
  - 卸载时询问是否保留配置；不写 HKLM；不在 Program Files 内写配置。
  - **免管理员（%LocalAppData%）与 uiAccess（必须 Program Files）互斥**，这是安装位置决策上的硬取舍。

## 5. 性能与体验

### 5.1 高 DPI（100%–200%）

- **已验证的官方支持路径：** .NET Framework 4.7+ 提供 opt-in 的高 DPI 支持 —— manifest 声明 Windows 10 兼容 + `app.config` 中 `<System.Windows.Forms.ApplicationConfigurationSection><add key="DpiAwareness" value="PerMonitorV2"/>`，必要时用 `EnableWindowsFormsHighDpiAutoResizing=false` 精细控制；并提供 `DpiChanged` / `DpiChangedBeforeParent` / `DpiChangedAfterParent` 事件与 `LogicalToDeviceUnits` / `ScaleBitmapLogicalToDevice` / `DeviceDpi` 辅助成员。**要求 .NET Framework ≥ 4.7 且系统 ≥ Windows 10 1703；官方明确"用 manifest 声明 DPI 感知"已不再推荐（会覆盖 app.config）**（[Windows Forms 高 DPI 支持](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms?view=netframeworkdesktop-4.8)）。
- **实操清单：** 图标/位图按当前 `DeviceDpi` 重新加载或 `ScaleBitmapLogicalToDevice`（否则 150%/200% 必然发虚）；自绘控件禁止使用固定像素；托盘 `.ico` 提供 16/20/24/32 多尺寸；悬浮窗跨屏拖动时响应 `DpiChanged` 重新计算坐标（不要让它跳回主屏）；混合 DPI 双屏是问题最集中的场景。
- **PoC（0.5–1 人日，必做）：** 在 100%/125%/150%/200% 以及"混合 DPI 双屏"下逐屏拖动主窗口与悬浮窗，检查文字不裁切、图标清晰、悬浮窗不跳屏、托盘菜单位置正确。

### 5.2 启动时间与内存

- **冷启动优化（按收益排序）：** ① `app.config` 加 `<generatePublisherEvidence enabled="false"/>`（跳过发布者证书 CRL 检查，常省数百毫秒，离线机器上更明显）；② 安装包内做 NGen/预编译预热；③ **把音频设备枚举与 BLE 初始化移出 `Main`**（后台线程 + 启动后 1–2 s）；④ 自检/设置页延迟到首次可见时再构造控件；⑤ 自动更新检查改到空闲 30 s 后。
- **内存与句柄：** 长期驻留类应用的主要泄漏源是 Bitmap/Icon 未 `Dispose`（托盘、悬浮窗、页面切换）、自绘字体与 GDI 对象、事件订阅未解绑。做一次"运行 8 h + 反复切换页面/托盘菜单 500 次"的 GDI/User 对象计数与 GC 堆曲线回归，成本极低、收益极高；用 64 位（AnyCPU 优先 64 位）避免大图/大缓存下的地址空间压力。

### 5.3 Windows 11 托盘 / 悬浮窗兼容要点

- **托盘图标：** Win11 默认把新应用图标收进"隐藏的图标"溢出面板（非 bug，是默认策略）→ 首次运行引导里必须明确告知用户如何固定；**必须处理 Explorer 重启**（注册 `TaskbarCreated` 消息后重新 `NIM_ADD`，否则资源管理器崩溃/更新后图标永久消失）；Win11 亦存在休眠/平板模式切换后图标消失的系统级现象，自检页应提供"重新注册托盘图标"的一键修复（[Shell_NotifyIcon](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyicona)）。
- **悬浮窗（本产品的关键交互）：** 必须用 `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW` + `SetWindowPos(..., SWP_NOACTIVATE)` 保证"显示但不抢焦点"，否则会破坏现有的焦点锁定；视觉上可用 `DwmSetWindowAttribute` 设置圆角与背景（`DWMWA_WINDOW_CORNER_PREFERENCE` / `DWMWA_SYSTEMBACKDROP_TYPE`，Windows 11 22000/22621+，需运行时探测；**Mica 对分层窗口无效**，即"半透明"与"Mica"需二选一）；`SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`（Win10 2004+）可让悬浮窗不出现在屏幕共享中。
- **迁移到 WPF / WinUI 的评估：不建议迁移。** WPF 可保留 .NET Framework（4.6.2+ 原生 Per-Monitor DPI），但要重写全部界面并重做 DPI/焦点/托盘交互，净收益仅"更现代的观感"；WinUI 3 需要 Windows App SDK + .NET 8，等于"重写 + 换运行时 + 重做打包/更新/签名"，最贵的部分恰恰是已经调通的焦点/注入/托盘链路。**建议：保留 WinForms，用自绘 + DWM 圆角/背景 + 统一视觉规范完成观感升级，把预算投到音频链路与兼容性上。**

## 6. 其他值得做的平台能力

- **Windows 通知 / Toast：条件可行，注意管理员权限冲突。** 未打包桌面应用发 Toast 需要"带 AppUserModelID 的开始菜单快捷方式 + COM 激活器注册"，实践中用 CommunityToolkit（`Microsoft.Toolkit.Uwp.Notifications` / `ToastNotificationManagerCompat`）最省事。**硬限制：官方明确"应用通知不支持以管理员权限（提升）运行的应用"，`Show` 会静默失败**（[App notifications 快速入门](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-quickstart)）→ 与"可能需要提权以支持管理员窗口"的诉求冲突，需明确取舍。低风险兜底：`NotifyIcon.ShowBalloonTip`（Win11 会被并入通知中心、观感一般，但不挑应用身份）。
- **任务栏进度与缩略图工具栏：已验证可行。** `ITaskbarList3::SetProgressValue/SetProgressState`（更新下载/长任务进度）、`ThumbBarAddButtons/ThumbBarUpdateButtons/ThumbBarSetImageList`（把"开始/停止录音""切换模式"放进缩略图右键工具条），Win10/11 均支持；主窗口不要用纯 `WS_EX_TOOLWINDOW`（否则没有任务栏按钮）；配合 `FlashWindowEx` 做"需要用户注意"提示（[ITaskbarList3](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-itaskbarlist3)）。
- **全局快捷键：优先 `RegisterHotKey`，不要为了快捷键装低级钩子。** `RegisterHotKey`（配 `MOD_NOREPEAT`）由系统管理、无需钩子；失败要点是 `ERROR_HOTKEY_ALREADY_REGISTERED`（被别的程序占用时注册失败）→ 设置页必须做冲突检测并允许改键；UAC 安全桌面下不触发。低级钩子 `WH_KEYBOARD_LL` 仅在必须"观察/抑制任意按键"时使用：必须在**有消息循环的专用线程**上安装，且必须在 `LowLevelHooksTimeout`（Win10 1709 起系统上限 1000 ms）内返回，**超时后钩子会被系统静默移除且应用无从得知**；官方文档亦明确"大多数需要低级钩子的场景应改用原始输入（Raw Input）"（[LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)）。遥控器侧触发应走其自身 HID 输入事件，而不是全局钩子。
- **多用户 / 多会话：** 做"每会话单实例"（命名 Mutex 不带 `Global\` 前缀，或名中带会话 ID）；用 `WTSRegisterSessionNotification`(`WM_WTSSESSION_CHANGE`) 或 `SystemEvents.SessionSwitch` 处理锁屏/切换用户；`SystemEvents.SessionEnding` 在注销/关机前保存配置。**音频采集不能放进 Windows 服务**（会话 0 与用户会话音频隔离），必须留在用户会话进程内——这也是键盘桥接进程必须是用户态进程的原因。
- **睡眠 / 唤醒恢复：** 订阅 `SystemEvents.PowerModeChanged`（Suspend/Resume）+ 解锁事件；恢复后按序执行：等 2–3 s → 重新枚举音频端点（可配合 `IMMNotificationClient` 直接响应设备变化）→ 重建电平监视/捕获客户端 → 重连 BLE 与 GATT 订阅 → 重新 `NIM_ADD` 托盘图标（Explorer 可能已重启）→ 复核全局热键。把这一串动作封装成"自检/一键修复"，比事后解释"为什么没声音"省得多。
- **Windows 11 小组件：不可行（对当前架构）。** 官方明确目前只能由**打包的 Win32 桌面应用（MSIX）或 PWA** 实现 widget provider，并需 Windows App SDK 的 `Microsoft.Windows.Widgets.Providers` + COM 激活（[Widget providers](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-providers)）；未打包的 .NET Framework WinForms 应用要先做 MSIX 打包 + 换运行时，用一个"状态小卡片"换这些成本不划算。**快速设置（Quick Settings）目前没有面向第三方应用的公开扩展 API**，不建议投入。

## 7. 汇总表

| 技术点 | 结论 | 关键 API / 方案 | 工作量（人日） | 风险 |
| --- | --- | --- | --- | --- |
| 采样率/格式对齐校验 | 需实验验证（优先做） | WASAPI `IAudioClient::GetMixFormat`、VB-CABLE 控制面板 | 0.5–1 | 低；错配会导致无声/失真 |
| drainMs 尾音调优（180→300 ms） | 需实验验证 | 采集端配置参数 | 0.5 | 低；显著降低尾字丢失 |
| 削顶诊断（float32 录音 + 饱和统计） | 已验证可行 | NAudio + CABLE Output 录制、Audacity | 1 | 低；结论决定后续路线 |
| 下游 DSP 衰减/降噪（APO 方案 B1） | 需实验验证（推荐） | Equalizer APO 装 CABLE Output：`Preamp`/`Filter`/`Convolution`/capture 阶段 | 2–4（含安装器静默配置与自检） | 中；第三方 APO 需管理员安装、设备变更后需重绑 |
| 中间混音台（方案 B2） | 可行但不推荐 | Voicemeeter + 改 ASR 录音设备 | 3–5 | 中高；延迟、用户配置成本、分发条款 |
| 自研 APO / 虚拟声卡 | 不可行 | 需组件化 APO + INF + WHQL/attestation 签名；且不允许挂到第三方驱动 | — | 极高；投入与回报完全不成比例 |
| 实时电平/削顶提示 | 已验证可行（需校准） | `IAudioMeterInformation::GetPeakValue`（无音频数据读取，隐私友好） | 1–2 | 低；虚拟设备读数需实测校准 |
| WASAPI 环回诊断模式 | 已验证可行 | `AUDCLNT_STREAMFLAGS_LOOPBACK`（仅共享模式，1703+ 支持事件驱动） | 1 | 低；涉及读音频，仅在用户显式开启时用 |
| BLE 电量（Battery/GATT） | 需实验验证 | `Battery.GetDeviceSelector`+`FromIdAsync`+`GetReport`；回落 GATT `0x180F`/`0x2A19` | 2–3 | 中；容量字段常为 null，长连接可能被采集程序占用 |
| BLE RSSI / 链路质量 | 不可行 | 无受支持 API；可用 `ConnectionStatus` + 读往返耗时代理 | 1（代理指标） | 中；不要向用户承诺信号强度 |
| 断连事件与去抖 | 已验证可行 | `ConnectionStatusChanged` + `DeviceWatcher` + HID 静默超时 | 1–2 | 低；事件延迟/抖动需去抖 |
| .NET Framework 调 WinRT | 已验证可行 | `Microsoft.Windows.SDK.Contracts` NuGet；Bluetooth/Power 不需包标识 | 0.5（打通） | 低；注意 TFM/包版本与 ApiInformation 守卫 |
| 设备配对 | 不可行（应用内） | `DeviceInformationPairing.PairAsync` 桌面应用不支持 → 引导系统设置 | 0.5（引导 UI） | 低 |
| UIA 直接写值（破反注入 IME） | 需实验验证（最高价值实验） | `ValuePattern.SetValue` → `LegacyIAccessiblePattern` → `TextPattern` | 3–5 | 中；目标可写性不一，受 UIPI 限制 |
| 剪贴板 + 受控 Ctrl+V | 已验证可行（现状） | 剪贴板序列号观察 + `SendInput` | 已有 | 中；仍属注入，会被过滤型输入法忽略 |
| 消息级写入兜底 | 需实验验证 | `PostMessage(WM_SETTEXT/EM_REPLACESEL/WM_CHAR)` | 1–2 | 中；仅对经典控件有效 |
| TSF 文本服务 | 技术可行但成本过高 | `ITfThreadMgr`/`ITfContext`/`ITfRange::SetText` + TIP 注册 | 20–40 | 高；需管理员安装、与现有 IME 并排 |
| uiAccess 签名应用 | 可行但不解决反注入 | manifest `uiAccess="true"` + 受信任根签名 + Program Files | 3–5 | 高；与免管理员/自动更新冲突，且不去除注入标记 |
| 代码签名 | 已验证路径存在 | Artifact Signing（个人限美/加，$9.99/月起，不支持 EV）；开源走 SignPath 免费 | 2–5（含身份验证周期 1–20 工作日） | 中；SmartScreen 信誉需时间累积 |
| 自动更新 | 需实验验证 | NetSparkle（Ed25519 + WinForms UI）或自研：签名清单 + WinVerifyTrust + 版本目录切换 + 自动回滚 | 5–10 | 中高；回滚与文件占用是主要坑 |
| 便携版 / 配置保留 | 已验证可行 | `portable.flag` 决定 `%LOCALAPPDATA%` 或 `<exe>\data`；schemaVersion + 迁移 + 备份 | 2–3 | 低；配置必须放在版本目录之外 |
| WinForms 高 DPI（100–200%） | 已验证可行（需逐项修） | `PerMonitorV2` app.config + `DpiChanged` + `LogicalToDeviceUnits`（需 .NET FW ≥ 4.7 / Win10 1703+） | 3–6 | 中；混合 DPI 与自绘控件是主要工作 |
| 启动/内存优化 | 已验证可行 | `generatePublisherEvidence=false`、NGen、延迟初始化、GDI/句柄回归 | 2–4 | 低 |
| 托盘/悬浮窗 Win11 兼容 | 已验证可行 | `Shell_NotifyIcon` + `TaskbarCreated` 重注册；`WS_EX_NOACTIVATE`；DWM 圆角/背景；`WDA_EXCLUDEFROMCAPTURE` | 3–5 | 中；Explorer 重启与休眠后图标丢失需自愈 |
| 迁移 WPF / WinUI | 不建议 | 迁移成本远超观感收益；WinUI 3 还需换 .NET 8 + 重做打包 | 30–60（不建议） | 极高 |
| Toast 通知 | 条件可行 | CommunityToolkit `ToastNotificationManagerCompat` + AUMI 快捷方式；**提权运行时不支持** | 2–3 | 中；与提权需求互斥 |
| 任务栏进度 / 缩略图工具栏 | 已验证可行 | `ITaskbarList3`（进度、`ThumbBar*`）+ `FlashWindowEx` | 1–2 | 低 |
| 全局快捷键 | 已验证可行 | `RegisterHotKey` + `MOD_NOREPEAT` + 冲突检测；避免 `WH_KEYBOARD_LL` | 1–2 | 低；被占用时注册失败需引导改键 |
| 多会话 / 睡眠唤醒恢复 | 已验证可行 | `WTSRegisterSessionNotification`、`SystemEvents.SessionSwitch/PowerModeChanged/SessionEnding` | 2–4 | 中；恢复顺序不对会出现"无声/无图标" |
| Win11 小组件 / 快速设置 | 不可行 | 小组件需 MSIX 打包应用 + Windows App SDK；快速设置无第三方 API | — | — |

### 建议的下一版实施顺序（按"性价比 × 风险"）

1. **第 0 周（必做实验）：** 削顶诊断（1.4）→ 采样率/格式校验（1.1）→ 电平提示 PoC（1.3）→ UIA 写入 PoC（3.2-2）→ BLE 电量 PoC（2）。这五件事决定后续路线，总计约 5–8 人日。
2. **第 1 阶段（低风险高收益）：** DSP 衰减（方案 B1）+ dranMs/增益调优、输入通道自动降级链、托盘/悬浮窗 Win11 自愈、高 DPI 修补、启动优化。
3. **第 2 阶段（交付能力）：** 代码签名（尽早启动身份验证，有 1–20 工作日等待期）+ 自研/NetSparkle 更新与回滚 + 便携版配置保留。
4. **明确不做：** 自研 APO/虚拟声卡、TSF 文本服务、虚拟 HID 驱动、WPF/WinUI 重写、Windows 11 小组件与快速设置。

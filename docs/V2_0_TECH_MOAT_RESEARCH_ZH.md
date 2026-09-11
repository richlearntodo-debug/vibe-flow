# 言灵 Vibe Flow Remote · Windows 底层技术调研报告

> 结论标注约定：**[已验证可行]**＝本次调研在本机（Win11 26200，非管理员进程）实跑通过；**[需实验验证]**＝有依据但未实跑，附 PoC；**[不可行]**＝有权威依据判定走不通。
> 本报告所有"本机实测"均来自本次会话在目标机上真实执行的命令/API 调用，不是文献推断。

---

## 0. 先看这一条：本次调研最大的发现

**问题 2 的 form factor 死结已经实测打通。** 不需要管理员、不需要改注册表 ACL、不需要重装驱动：

```csharp
// C# / .NET Framework 4.x 用户态，普通完整性进程即可
// 1) CoCreateInstance(CLSID_MMDeviceEnumerator) -> IMMDeviceEnumerator
// 2) GetDevice(L"{0.0.1.00000000}.{c616f7cc-...}", out IMMDevice dev)
IPropertyStore ps;
dev.OpenPropertyStore(STGM_READWRITE /*=2*/, out ps);          // 实测 hr = 0x00000000
PROPERTYKEY k = PKEY_AudioEndpoint_FormFactor;                  // {1da5d803-...}, 0
PROPVARIANT v; v.vt = VT_UI4; v.ulVal = 4;                      // 4 = Microphone
ps.SetValue(ref k, ref v);                                      // 实测 hr = 0x00000000
ps.Commit();                                                    // 实测 hr = 0x00000000
```

- **实测结果**：`HKLM\...\MMDevices\Audio\Capture\{c616f7cc-...}\Properties\{1da5d803-...},0` 从 `2 (LineLevel)` **持久变成 `4 (Microphone)`**；重新 `GetDevice` + `OpenPropertyStore(STGM_READ)` 读回 `formFactor=4, vt=19 (VT_UI4)`。
- **不需要重启 audiosrv，不需要重新枚举端点**，端点 `DeviceState=1` 保持 Active。
- **关键洞察**：直接 `Set-ItemProperty` 写那个注册表值会被 ACL 拒绝（本机实测 `Requested registry access is not allowed`，因为 ACL 只给 `Audiosrv`/`AudioEndpointBuilder`/`SYSTEM`/`TrustedInstaller`）。但走 Core Audio 时是 **Audiosrv 代替调用方写入**，所以普通用户就能改。
- 微软文档写的是 "The Windows audio service sets the values of these properties. Clients can read these properties, but **should not** set them." —— 是 *should not*（不建议），不是 *cannot*。**这是可写但非契约行为**，必须写进产品兼容性风险。

> ⚠️ 本报告执行过程中确实改动了本机 CABLE Output 的 form factor（2→4）。还原方式：同一 API 设回 `2` 再 `Commit()`，或重装 VB-CABLE。

---

## 问题 1：能不能绕过"输入法过滤模拟输入"，把豆包这类 IME 的语音面板唤出来？

**结论一句话**：**按键注入路线大概率走不通，但存在一条更有希望的路——豆包自己有 `RegisterHotKey` 全局语音快捷键（本机配置默认关闭），系统级热键匹配与语音键的 LL 键盘钩子是两套机制，注入按键很可能能触发它【需实验验证，优先级最高】；MSAA 路线价值存在但概率偏低【需实验验证】。**

### 1.1 豆包的真实拦截层（本机取证）

本机 `C:\Program Files\DoubaoIME\versions\v0.9.0.0\ImeService.exe` 字符串取证，发现两套并存的机制：

| 字符串 | 含义 |
|---|---|
| `InstallVoiceKeyHook` / `VoiceKeyHook` / `VoiceKeyHookProc` / `VoiceKeyHookThreadProc` | 语音键走**低级键盘钩子**（LLKHF 可见 `LLKHF_INJECTED`，可直接丢弃注入） |
| `RegisterHotKey` / `UnregisterHotKey` / `global-hotkey` / `ProbeGlobalHotKey` / `shortcut_validator.cpp` | **全局语音快捷键走系统热键**，注册前先探测冲突 |
| `AVWindowVoiceShortcutConflictDialog` / `hasShortcutConflict` | 有冲突检测对话框 |
| `enableGlobalVoiceShortcut` / `enableVoiceShortcut` | 两个独立开关 |

本机 `%APPDATA%\DoubaoIme\conf\config.json` **原文**：
```json
"voice" : {
   "enableGlobalVoiceShortcut" : false,
   "enableVoiceShortcut" : true,
   "selectedMicrophoneId" : "{0.0.1.00000000}.{8e3b054a-70c1-4e5d-955b-9cf87b2846bc}",
   "voiceLongPressShortcut" : { "keyCode" : 68,  "modifierFlags" : 1025 },
   "voiceShortcut" :          { "keyCode" : 32,  "modifierFlags" : 2049 }
}
```
解码：`voiceShortcut` = vk 32（**Space**）+ 0x801（`MOD_ALT` + 自定义位 0x800）→ **Alt+Space 系**；`voiceLongPressShortcut` = vk 68（**D**）+ 0x401 → **Alt+D 系**（0x800/0x400 是豆包自己的修饰位，不建议硬编码）。

**判定**：现在注入无效，最可能是 `VoiceKeyHook` 这一侧把 `LLKHF_INJECTED` 丢了。而 `RegisterHotKey` 是**另一条**系统路径。

#### 关于「IME 用 RegisterHotKey 注册系统热键，注入按键是否也会触发」
**推断：会触发（高置信，但必须实测）**。理由是热键匹配发生在系统输入处理路径里（`RegisterHotKey` 文档："When a key is pressed, the system looks for a match against all hot keys"），不区分来源；而 `SendInput` 文档明确说它是"injecting a series of simulated input events into a device's input stream"，并举例"accessibility application can use SendInput to inject keystrokes corresponding to application launch shortcut keys that are **handled by the shell**"——说明注入键能走到系统级快捷键处理。
主要反例风险：部分 IME 会在 LL 钩子里同时吞掉按键，使 `TranslateMessage`/热键匹配都拿不到；这取决于钩子是否返回 1 阻断传递。**必须实测**。

### PoC 1-A：全局热键路线（最高优先级，1~2 小时）
1. 把 `config.json` 的 `enableGlobalVoiceShortcut` 改为 `true`（先退出豆包托盘/ImeService），重启 ImeService；或直接在豆包设置 UI 里打开"全局语音快捷键"。
2. 写最小 C#：`SendInput` 发 `Alt+Space`（`INPUT_KEYBOARD`，vk=`VK_MENU` 0x12 + vk=0x20，含 keyup），**不要**加 `KEYEVENTF_SCANCODE`。
3. 再试 `WM_HOTKEY` 直投：`EnumWindows` 找 `OimeMessageWindow`（本机实测存在，属 `ImeService` 进程），`PostMessage(hwnd, WM_HOTKEY /*0x0312*/, id, lParam)`（id 需要枚举/爆破 1..0xBFFF）。
4. **预期/判定**：语音面板（`OimeVoiceWaveWindow` 或状态栏麦克风按钮态）出现 → 路线成立，产品可直接用"注入 Alt+Space"开语音；若面板不出现但 ImeService 日志有 `[VHK] voice tryout active` 之类事件 → 半通。
5. **注意**：先手工按一次该组合键确认系统层面可用（排除被其他程序占用）。

### PoC 1-B：MSAA / IAccessible 路线
**结论：机制上可行、豆包侧概率偏低【需实验验证】。**

- **机制依据**：`AccessibleObjectFromWindow(hwnd, OBJID_CLIENT, IID_IAccessible, out ppv)` 是标准入口（[官方文档](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromwindow)，`oleacc.dll` + `Oleacc.lib`；C# 用 P/Invoke 或 `Accessibility.dll`/`System.Windows.Forms.AccessibleObject`）。窗口若实现 MSAA，必须响应 `WM_GETOBJECT` 并返回 `OBJID_CLIENT` 的可访问对象（[Handling the WM_GETOBJECT Message](https://learn.microsoft.com/en-us/windows/win32/winauto/handling-the-wm-getobject-message)）；`OBJID_CLIENT` 属标准 [object identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/object-identifiers)。
- **为什么概率偏低**：UIA 树 0 后代 + DirectUI 类 = 典型的"自绘窗口，未实现 UIA provider"；微软的 `DirectUIHWND` 本身能被 UIA 桥接（UIA 会 fallback 到 MSAA），所以**豆包 UIA 树为空，恰恰说明它连 MSAA 的 `OBJID_CLIENT` 也没实现**，而不是"只实现 MSAA"。这是本报告最关键的推断反转点。
- 另一个可能：`OimeDirectUIWindow` 的语音按钮在**另一个进程**（UIA/MSAA 跨进程需要窗口所在进程托管 provider），本机实测这些窗口都在 `ImeService`（PID 13920）。
- 开源先例：**没有找到可信的、专门对 DirectUI 做 `accDoDefaultAction` 的成功案例**。可参考的同类思路是 UI 自动化工具（Inspect.exe / Accessibility Insights）先用 UIA，再 fallback MSAA；以及 [我查证到的一批 MSAA 失效案例](https://stackoverflow.com/questions/48900989/windows-header-control-and-msaa) 都指向"自绘控件不实现 MSAA 就完全拿不到"。

**PoC 1-B 步骤**：
1. `EnumWindows` 找 class=`OimeDirectUIWindow` 的 hwnd（本机实测存在）。
2. `SendMessageTimeout(hwnd, WM_GETOBJECT, 0, OBJID_CLIENT /*= -4*/, SMTO_ABORTIFHUNG, 500ms)`；返回值非 0 才有戏（`0` / `E_FAIL` 直接判死）。
3. 有返回值则 `AccessibleObjectFromWindow(hwnd, OBJID_CLIENT, IID_IAccessible, out acc)`，`acc.accChildCount` → `acc.accName(i)` / `accRole(i)` 找"麦克风/mic"。
4. `accDoDefaultAction(child)`，或 `accSelect(SELFLAG_TAKEFOCUS|SELFLAG_TAKESELECTION, child)` + `accDoDefaultAction`。
5. **判定标准**：`accChildCount > 0` 且能枚举出可读名称 → 路线成立；`WM_GETOBJECT` 返回 0 → **不可行**（明确写死，不再投入）。
6. 关键优势（若成立）：`accDoDefaultAction` **不产生任何键盘/鼠标事件**，天然绕过所有注入过滤。

### PoC 1-C：命令行 / IPC 入口（自行排查方法 + 合规边界）
**方法（可复用于任何 IME）**：
- **字符串扫描**：读 exe 字节流，分别用 ASCII / UTF-16LE 解码后正则捞 `\\.\pipe\...`、`--switches`、URL、日志标签。本机实际产出：
  - 管道：**`\\.\pipe\DoubaoIme`**、`\\.\pipe\ObricIme`；日志原文 `settings_ipc_server started on \\.\pipe\DoubaoIme\settings-rpc`（即 **`\\.\pipe\DoubaoIme\settings-rpc`** 是设置 RPC 端点）。
  - 开关：`--activate-tsf`、`--install-tsf`、`--uninstall-tsf`、`--show-window`、`--open-tab`、`--wav`、`--version-dir`、`--update-feedback`、`--test-*`。
  - **`--wav` 值得单独验证**：若它支持把 wav 喂给 ASR，等于官方给了"离线音频进 ASR"的入口（但要走它的 UI/服务，不是通用）。
  - 无 `--voice` 之类的直启语音开关（本机扫描未发现）。
- **Procmon / ETW**：Process Monitor 过滤 `ImeService.exe` 的 `CreateFile`（pipe）+ `RegSetValue` + `TCP/UDP`，能直接看到它读麦克风配置的路径与时机。
- **窗口消息枚举**：`EnumWindows` + `GetClassNameW` 定位 `OimeMessageWindow`/`OmeTrayWindow`，用 `Spy++` 观察手工按语音键时哪个窗口收到什么消息——**这是最可能找到"合法触发消息"的方法**（若它走自定义 `WM_APP+` 消息）。
- **COM 注册表**：`HKLM\SOFTWARE\Classes\CLSID` 里筛 `InprocServer32` 指向 IME 目录的项；本机实测 WeType 命中 `{86598FB9-...} -> C:\WINDOWS\system32\wetype_tip.dll`。
- **合规边界（必须写进产品）**：只做**本机、只读、用户已安装软件**的排查；**不**绕过 IME 的鉴权/签名校验、**不**注入其进程内存、**不**伪造其 IPC 消息以规避其安全设计。用它的公开 CLI/管道属于"用户在自己机器上自动化自己装的软件"，边界相对安全；但一旦夹带"逆向私有协议以伪造请求"，就有触碰《计算机信息系统安全保护条例》/软件许可协议的风险。产品内应避免把这类路径作为**默认**行为，做成"实验性、用户显式开启、有免责说明"。

### 1.4 若都不可行：诚实的产品替代
1. **引导用户手工按一次**：把遥控器麦克风键**直接映射成豆包的全局语音快捷键**（若 1-A 证明快捷键可用且可被真人触发），提示"按住遥控器说话时，请在电脑上按一次 Alt+Space"。体验降级但 100% 可用。
2. **推荐可用引擎**：Windows 语音输入（Win+H）**已验证可被注入触发且直写上屏**（本机 Win11 26200，zh-CN/en-US 语言包在位）→ 作为默认推荐引擎；微信输入法（文字进剪贴板 → 受控 Ctrl+V）作第二选择。
3. **明确产品文案**：把"豆包输入法"标为"仅支持虚拟麦克风模式（见问题 2），不支持远程唤醒"。

---

## 问题 2：怎么让遥控器音频被任意输入法当作麦克风采到？

**结论一句话**：**通过 Core Audio 的 `OpenPropertyStore(STGM_READWRITE)` 把 CABLE Output 的 `PKEY_AudioEndpoint_FormFactor` 改成 4（Microphone）本机实测成功且持久，无需管理员——这是本报告最有护城河价值的技术点；但这属非契约行为，必须配"免驱动降级模式"兜底。**

### 2.1 为什么输入法看不到 CABLE Output —— 实测证据链

本机采集到的 form factor 全表（真实值）：

| 端点 | FormFactor | 枚举器 |
|---|---|---|
| **CABLE Output** | **2 (LineLevel) → 本次改为 4** | `ROOT` / `VB-Audio Virtual Cable` |
| Realtek 麦克风 / 麦克风 | 4 (Microphone) | HDAUDIO |
| HUAWEI FreeClip 2 Hands-Free | 5 (Headset) | `BTHHFENUM` |
| Line In / 线路输入 | 2 (LineLevel) | HDAUDIO |
| 立体声混音 | 10 (UnknownFormFactor) | — |

- 枚举值定义见官方 [EndpointFormFactor 枚举](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/ne-mmdeviceapi-endpointformfactor)（`Microphone=4`、`Headset=5`、`LineLevel=2`、`UnknownFormFactor=10`），属性含义见 [Audio Endpoint Properties](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-endpoint-properties)。
- **豆包侧的过滤证据**（本机二进制字符串）：`audio_device_enumerator_win.cpp`、`ListMicrophones`、`MicList`、`MicPick`、`ime_auto_detect_microphone_id`、`MicLevelMeterWin`、`mic_select`。→ 它**自己枚举麦克风**并存端点 ID，**不是**用系统默认设备。
- **豆包当前选中的设备**：`selectedMicrophoneId = {0.0.1.00000000}.{8e3b054a-...}` = **HUAWEI FreeClip 2 Hands-Free**（formFactor=5）。→ 佐证"它只列 Microphone/Headset 类"。
- VB-CABLE 的 INF（`vbMmeCable64_win10.inf`）**只声明** `PKEY_AudioEndpoint_Association`（`KSNODETYPE_LINE_CONNECTOR`）、`PKEY_AudioEndpoint_ControlPanelProvider`、`PKEY_AudioEngine_OEMFormat`，**不声明 FormFactor** → 该值来自 Windows 端点属性存储的默认值（LineLevel）。**这也意味着驱动不会在每次启动时回写覆盖**。

**判定**：**high confidence，但仍是推断**——"输入法按 formFactor ∈ {4,5} 过滤"尚未在豆包 UI 上目视确认。**PoC 见 2.3，5 分钟可定论。**

### 2.2 form factor 可写性 —— 本机实测结论（直接回答你的三个子问题）

| 问题 | 实测答案 |
|---|---|
| `OpenPropertyStore(STGM_READWRITE)` + `SetValue` + `Commit()` 能否改？ | **能。三个 HRESULT 全 0x0。** |
| 普通权限行不行？ | **行。本机进程为 Medium Mandatory Level（`whoami /groups` 显示 Administrators 为 `Group used for deny only`，即未提权），全程无 UAC。** |
| 需要重启 audiosrv / 重新枚举吗？ | **不需要。**Commit 后立即持久化到注册表，新实例读回即为 4。 |
| 直接改注册表行不行？ | **不行。**`Set-ItemProperty` 报 `Requested registry access is not allowed`（ACL 只给 Audiosrv/AudioEndpointBuilder/SYSTEM/TrustedInstaller）。 |
| 是 driver-owned 只读吗？ | **不是"只读"，但是"不该写"。**微软文档原文为 *"Clients can read these properties, but should not set them."* 属**未文档化的可写行为**。 |
| 会不会被驱动重装覆盖？ | 不会（INF 无 FormFactor）；但**重装 VB-CABLE / 端点 GUID 重建会回到 2**，产品需要在每次启动自检并重放。 |
| 有项目这么干过吗？ | 未找到公开的成熟先例（这本身就是护城河）。建议在报告外做一个 PoC 工具留档，作为"我们验证过"的内部证据。 |

### 2.3 PoC（5 分钟定论版）

**步骤**：
1. 用 2.0 节代码把 CABLE Output 的 formFactor 设为 4。
2. 打开豆包输入法 → 设置 → 语音 → 麦克风列表。
3. **判定标准**：
   - **`CABLE Output` 出现** → 过滤器就是 formFactor；把遥控器音频写入 CABLE Input、并把 CABLE Output 设为豆包的麦克风，**整条虚拟声卡路线打通**（此时远程唤醒甚至不再必要）。
   - **仍不出现** → 过滤器另有条件（端点类型/名称黑名单/`DeviceState`/是否为 Communications 设备）。下一步 PoC：把 `PKEY_AudioEndpoint_FormFactor` 之外，尝试把端点默认为通信设备（`SetDefaultAudioEndpoint` 不可行，需用户在"声音设置"里点"设为默认通信设备"），并在豆包设置里点"刷新"。
4. 真机验收：按住遥控器说话 → 豆包出字。
5. 建议同时留一份"还原脚本"（设回 2），写进产品自检页。

### 2.4 虚拟音频设备横向对比与分发许可

| 方案 | capture 端点形态 | 能否被"只列麦克风"的输入法看到 | 分发包许可 | 备注 |
|---|---|---|---|---|
| **VB-CABLE（免费版）** | CABLE Output，默认 **LineLevel(2)**，可改成 4 | 改完后**可以**（本机已验证可改） | **允许**随产品分发+静默安装，前提是 donationware 模式可适用（用户能看到/识别 VB-CABLE 且可自愿付费）；显著规模的公司被鼓励支付较高授权费（原文举例 500/1000/2000 USD）。**A+B / C+D 明确禁止**分发或捆绑。参见 [VB-Audio Licensing](https://vb-audio.com/Services/licensing.htm) | 安装需管理员+重启（Setup 明确要求 Run in administrator mode / Reboot）。是当前唯一"许可干净"的路线。 |
| **VB-CABLE A+B / C+D** | 同上 | 同上 | **禁止分发/捆绑**（授权页原文） | 只适合用户自行安装。 |
| **Voicemeeter（含 Banana）** | 有 `VoiceMeeter Output`(capture) / `VoiceMeeter Aux Output` 等 | 形态取决于其 INF 声明，**未在本机实测**，需验证 | 可免费分发（保留 donationware 可适用性，需标注来源 voicemeeter.com）；专业机构按量付费；**Potato 不可分发/捆绑，但可免费预装**。参考 [licensing](https://vb-audio.com/Services/licensing.htm) + [Voicemeeter](https://vb-audio.com/Voicemeeter/index.htm) | 比 VB-CABLE 重，但同样支持改 form factor。 |
| **Virtual Audio Cable (Muzychenko)** | `Line 1` 等，**形态偏 Line** | 大概率需同样改 form factor | **商业授权**，捆绑需与作者单独谈 | [vac.muzychenko.net](https://vac.muzychenko.net/en/manual/glossary.htm) |
| **VirtualDrivers/Virtual-Audio-Driver** | 自称"a virtual speaker **and mic**" | 若其 capture 端点直接声明为 Microphone，则**天然可见**——这是最值得验的方案 | 未取到 README（GitHub 抓取失败），**许可证必须人工核对**（含是否允许再分发签名驱动） | [GitHub](https://github.com/VirtualDrivers/Virtual-Audio-Driver)。测试签名/未签名驱动在普通用户机上装不上是个大坑。 |
| **AudioMirror（gmh5225 镜像）** | 虚拟音频线 | 同上 | 需核对 | [GitHub](https://github.com/gmh5225/Driver-audio-AudioMirror) |
| **Apple 生态参照** | BlackHole | macOS 侧等价物 | GPL-3.0 | 参照项目 [open-voice-bridge](https://github.com/nijez/open-voice-bridge) 正是 RC003 + BlackHole 的 macOS 实现，且**已明确踩过"豆包把麦克风固定为 BlackHole，物理 Fn 打开界面却没声音"这个坑**——值得直接读它的 FAQ。 |

**产品建议**：主推 VB-CABLE（许可最干净 + 我们已验证 form factor 可改写），把 Voicemeeter / Virtual-Audio-Driver 做成"高级选项"；**永远不要**随包分发 A+B/C+D。

### 2.5 不改驱动的替代路径（改端点属性）

- **可行**：`PKEY_AudioEndpoint_FormFactor` 走 Core Audio 可写（本机实测）。
- **不可行（直接改注册表）**：ACL 拦截。
- **官方"正规"路径**：音频驱动的 form factor 应在 **INF** 里通过 `PKEY_AudioEndpoint_FormFactor` 声明（参考 [PKEY_AudioEndpoint_FormFactor](https://learn.microsoft.com/en-us/windows/win32/coreaudio/pkey-audioendpoint-formfactor)）——即"自己有驱动"才是正解，这也是护城河所在。
- **风险清单**：① 非文档化行为，Windows 更新可能收紧；② 重装驱动/GUID 重建后失效；③ 若把 CABLE Output 标成 Microphone，可能让其它软件（会议/浏览器）把它当成真麦克风而误选；④ 卸载/还原必须可逆，产品需提供"一键恢复"。

### 2.6 免驱动降级模式（重点评估）

**设计**：不装虚拟声卡；遥控器只当"无线触发器"（按键走已有的 Bluetooth HID）；音频由**电脑自带麦克风/耳机麦克风**采集。

**(a) 是否需要捕获程序/CABLE 才能工作？**
**不需要。** 触发路径（HID 按键 → 注入/唤醒输入法）与音频路径（系统麦克风 → 输入法自采）完全解耦；把 `autoRouteVirtualMicrophone` 关掉后，本程序不再切换系统默认录音设备、不再向虚拟声卡写音频。**这是"零驱动安装、免重启、免管理员"的唯一可行形态**——而这对"配置便捷"是决定性的（VB-CABLE 安装必须管理员 + 重启）。

**(b) 音质与识别率损失量级（推断，需实测）**
| 维度 | 遥控器 ATVV 16 kHz ADPCM → CABLE | 电脑麦克风免驱动 |
|---|---|---|
| 采样率 | 16 kHz（ATVV 规格） | 44.1/48 kHz（更高） |
| 编码损失 | IMA/DVI ADPCM 有损 + BLE 丢包 | 无 BLE 丢包 |
| 距离/指向 | 手持贴近嘴，**近场、信噪比高** | 取决于坐姿/机位，通常 40–80 cm，**混响与环境噪声明显更差** |
| 结论 | 链路损失 vs 声学损失 | **近场优势通常压倒编码差异**：安静房间下识别率可能持平甚至更好；嘈杂环境/笔记本远场会明显下降（**需实验验证**：同一段话两种链路各跑 50 句，比较字准率） |

**(c) 对"只听自己麦克风的豆包"是否天然可用？**
**天然可用，且是当前唯一 100% 确定的豆包方案**（因为豆包列出的麦克风里有电脑麦克风/耳机麦克风）。代价：用户必须**把遥控器举到电脑附近**或电脑麦克风拾音足够好——产品定位上从"无线麦克风"降级为"无线触发器"。若 2.3 的 PoC 成功，**优先用虚拟麦克风模式**，免驱动模式作为 fallback。

**(d) 隐私披露要点（必须写）**
1. 免驱动模式下音频**由用户选定的输入法/系统直接采集**，本程序**不接触、不落盘、不上传**语音——这是隐私上的优点，应在 UI 明示。
2. 遥控器麦克风在免驱动模式下**不使用**（或仅本地电平指示），需明确告知"声音来自电脑麦克风，不是遥控器"。
3. 若提供"遥控器麦克风 + 本机麦克风"双源（参照 open-voice-bridge 的"兼容 Mac 键盘 Fn"），必须明示仅在按住时采集、松手即停、失败关闭以防回声环。
4. 关闭 `autoRouteVirtualMicrophone` 意味着**不再改系统默认录音设备**——这条要写进"我们不改你的系统设置"承诺里（竞品常在这里失分）。

---

## 问题 3：如何自动识别"当前前台应用正在用哪个输入法"？

**结论一句话**：**用 `ITfInputProcessorProfileMgr::GetActiveProfile(GUID_TFCAT_TIP_KEYBOARD)` 拿 profile GUID 作为第一判据（能区分微软拼音/微信/豆包），IMM32 只作降级补充；但要注意区分"系统当前激活的 TIP"与"目标输入框实际生效的 TIP"——这是最容易被坑的一点【需实验验证】。**

### 3.1 三条路径对比

| 路径 | 能拿到什么 | 对 TSF-only 新输入法（微信/豆包） | 可靠性 |
|---|---|---|---|
| **TSF：`ITfInputProcessorProfileMgr::GetActiveProfile`** | `TF_INPUTPROCESSORPROFILE{ dwProfileType, langid, clsid, guidProfile, catid, hkl, dwFlags }` | **有效**（它们本质是 TSF TIP） | **首选**。注意：`catid` 只支持 `GUID_TFCAT_TIP_KEYBOARD`；返回 `S_FALSE` 表示未找到/未激活。见 [官方文档](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfinputprocessorprofilemgr-getactiveprofile)。C# 通过 `CoCreateInstance(CLSID_TF_InputProcessorProfiles)` + `ITfInputProcessorProfileMgr`（`msctf.dll`）调用；**未打包桌面应用可用**（Vista+，desktop apps）。 |
| **IMM32 回退** | `GetKeyboardLayout(tid)` 拿 HKL；`ImmGetDescription(HKL)` 拿描述；`ImmGetIMEFileName(HKL)` 拿 IME 文件名；`GetKeyboardLayoutName` 拿 KLID | **对纯 TSF TIP 往往拿不到有意义结果**（HKL 可能只是替代布局，如豆包 `SubstituteLayout = 0x08040804`） | 仅作补充。见 [GetKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardlayout)、[ImmGetDescriptionW](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immgetdescriptionw)、[ImmGetIMEFileNameW](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immgetimefilenamew)。 |
| **窗口类名 / 进程名** | `wetype.*`、`OimeDirectUIWindow`、`MSCTFIME UI`、`CicLoaderWndClass` | **有效且直观**（本机实测清单见下） | 作为**交叉校验**，不能单独作为判据（输入法面板可能未创建） |

### 3.2 本机实测的识别特征库（可直接用）

- **TSF TIP 标识**（注册表 `HKLM\SOFTWARE\Microsoft\CTF\TIP\{TIP CLSID}\LanguageProfile\0x00000804\{Profile GUID}` 实测读取；**2026-09-10 更正：TIP CLSID 与 Profile GUID 是两个不同的值，此前文档把前者当成了后者**）：
  - 豆包输入法：TIP CLSID = **`{9D2B2E2B-3C93-4D2F-9D35-6EEB85F0D2B0}`**，Profile GUID = **`{2B4D4B3A-4D4F-4C0A-8E66-7F771A2B9C10}`**，`Description = 豆包输入法`，`IconFile = C:\WINDOWS\System32\tsf-oime.dll`，`SubstituteLayout = 0x08040804`，`Enable = 1`。
  - 微信输入法：TIP CLSID = **`{86598FB9-66A2-463E-B9C2-AEB906D477AD}`**，Profile GUID = **`{607FDF85-FCC8-4DBD-A365-41296F980C9C}`**，`Description = WeType`，`IconFile = C:\WINDOWS\system32\wetype_tip.dll`，`Enable = 1`。
  - 微软拼音：TIP CLSID = `{81d4e9c9-1d3b-41bc-9e6c-4b40bf79e35e}`，Profile GUID = `{FA550B04-5AD7-411f-A5AC-CA038EC515D7}`（本机 `Enable = 0`，即当前未在语言列表启用）。
  - **活跃输入法读取（2026-09-10 实测通过）**：`CLSID_TF_InputProcessorProfiles = {33C53A50-F456-4884-B049-85FD643ECFED}` → QI `ITfInputProcessorProfileMgr`（IID `{71C6E74C-0F28-11D8-A82A-00065B84435C}`）→ `GetActiveProfile(GUID_TFCAT_TIP_KEYBOARD = {34745C63-B2F0-4784-8B67-5E12C8701A31})`。**坑**：把 `TF_INPUTPROCESSORPROFILE` 作为 `out` 结构体封送时首次调用返回 `E_INVALIDARG`，且缓冲区小于服务端结构会**破坏进程堆**（`STATUS_HEAP_CORRUPTION`）；改为调用方 `AllocHGlobal(256)` 并 `Marshal.PtrToStructure` 后稳定通过。单次检测实测约 **0.02 ms**（200 次平均），活跃键盘布局另用 IMM32 `GetKeyboardLayout(0)` 交叉验证（本机 `0x08040804`）。本机读回：微信输入法（CLSID `86598FB9…` + Profile `607FDF85…`），与用户配置的默认语音工具一致。
  - **判定规则（已产品化，但“面板唤起条件”仍是假设）**：微信输入法与豆包输入法的语音面板只有在“它自己就是当前输入法”时才可用 —— **这是待验证假设，不要当作已验证事实**。`ActiveEngineBlocksProvider` 只对这两个 provider 生效，未知输入法不报冲突；Windows 语音输入（Win+H）与 Typeless 是系统级快捷键，与输入法无关。
  - **重要更正（2026-09-10 真机观察）**：`GetActiveProfile` 的“活跃配置”是**按线程**的——它反映调用线程的输入法上下文，不等于前台应用的输入法。本机同一进程内：启动时读到 `engine=doubao`，数分钟后（言灵窗口被激活过）读到 `engine=wechat`，而配置文件里的默认语音工具一直是微信输入法。因此产品里只把它当作**排查参考**（日志 `scope=thread`、文案“言灵所在输入法上下文”），不据此断言前台输入法或断言“这就是没出字的原因”；要判断前台应用的输入法需要额外机制（例如对目标窗口线程做 `GetKeyboardLayout`），尚未实现。
- **窗口类名 / 进程**（本机 `EnumWindows` 实测）：
  - 豆包：进程 `ImeService.exe` → `OimeDirectUIWindow`、`OimeVoiceWaveWindow`、`OmeMessageWindow`、`OmeTrayWindow`。
  - 微信：进程 `wetype_server.exe` → `wetype.server.window`；`wetype_renderer.exe` → `wetype.flutter.setting`(候选窗)；`wetype_update.exe` → `wetype.statusbar.window` / `wetype.flutter.setting`(语音输入/设置)。
  - 通用：`MSCTFIME UI`、`IME`、`CicLoaderWndClass`（属 `ctfmon`）。
- **注意**：`MSCTFIME UI` / `Default IME` 几乎每个有输入焦点的进程都有，**不能**用作判据。

### 3.3 可落地的检测优先级链（建议实现）

```
每个目标窗口（前台 hwnd）激活 / 焦点变化时：
1) tid = GetWindowThreadProcessId(hwnd)
2) TSF:  p = ITfInputProcessorProfileMgr::GetActiveProfile(GUID_TFCAT_TIP_KEYBOARD)
         if SUCCEEDED && p.dwFlags & TF_IPPMF_FORPROCESS(?)  → 命中已知 GUID 表（豆包/微信/微软/搜狗）
         → 命中即返回引擎策略（豆包=虚拟麦克风 or 不支持远程唤醒；微信=剪贴板+Ctrl+V；Win+H=可注入）
3) 降级 A：GetKeyboardLayout(tid) → ImmGetIMEFileName / ImmGetDescription → 关键字匹配
4) 降级 B：EnumWindows 找当前前台进程/线程相关的 IME 面板类名（wetype.* / Oime*）
5) 降级 C：全部失败 → 用"能力探测"：发送一次无害的注入探针（例如注入一个不会上屏的键），
         观察剪贴板序列号 / UIA 文本变化 → 推断"注入是否被接受"，而不是猜引擎名字
6) 缓存：按 (进程名, hwnd, tid) 缓存结果，监听 WM_INPUTLANGCHANGE / TSF 的
   ITfLanguageProfileNotifySink（或轮询 1s）失效缓存
```

**关键坑（需实验验证）**：`GetActiveProfile` 返回的是**系统/线程级当前 TIP**，而 TSF 允许每个文档/焦点有独立 profile。若用户在某 App 内切换过输入法，系统级 API 可能与目标框实际生效的不一致。**PoC**：在 Chrome 里切到微信输入法、在记事本切到豆包，分别调用 `GetActiveProfile` 并对比目标框实际出字引擎，记录不一致率；若不一致率高，改走"前台线程 HKL + 目标框焦点元素 UIA 属性"组合。

**PoC 最小步骤**：C# 里 `Type.GetTypeFromCLSID(new Guid("CLSID_TF_InputProcessorProfiles"))` → QI `ITfInputProcessorProfileMgr` → `GetActiveProfile(new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31") /*GUID_TFCAT_TIP_KEYBOARD*/, out profile)`；打印 `profile.clsid`/`guidProfile`/`hkl`/`langid`。**判定**：切换豆包/微信/微软拼音时 `clsid` 随之变化即为通过。

---

## 问题 4：连接稳定性诊断（遥控器 BLE）

**结论一句话**：**"连接态拿不到 RSSI"确实存在（Windows 公共 API 不暴露已连接设备的 RSSI），但可用"GATT 往返耗时 + ATVV 报文间隔抖动 + 断连计数"做断流预警；"低电量导致 BLE 音频断续"没有权威直接结论，0.5–1.1s 断流更可能是连接参数/重传/宿主调度问题，需相关性实验才能归因。**

| 可观测量 | 可行性 | 说明与来源 |
|---|---|---|
| **GATT 往返耗时（RTT）** | **[需实验验证]** 最推荐 | 定期对已连服务做一次读（如 Device Information 的 `0x2A19` 电量、`0x2A24` 型号）并计时。RTT 抬升是链路恶化最早、最灵敏的信号。参考 [GATT 服务发现/读写（WinRT）](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothledevice)。 |
| **ATVV 音频报文间隔抖动** | **[已验证可行方向]** | 本项目已在解码 ATVV 流，统计帧到达间隔的 P50/P95/P99 与丢帧/序号跳变，直接对应"音频断流"。参照项目 [open-voice-bridge](https://github.com/nijez/open-voice-bridge) 已在做"PCM 电平、帧/段计数、播放器回执"三类内存元数据诊断，Windows 侧可照抄思路。 |
| **断连/重连计数、连接参数** | **[需实验验证]** | 监听 `BluetoothLEDevice.ConnectionStatusChanged`；连接间隔（Connection Interval）本身在 Windows 公共 API 上通常**不可读/不可设**，只能靠 RTT 间接推断。 |
| **RSSI** | **[不可行（已连接态）]** | Windows 公共 API 一般只在广播/发现阶段给出 RSSI；已连接设备要靠厂商私有 GATT 服务或 HCI 抓包。参照项目也明确记录了"连接态拿不到 RSSI"这一限制。 |
| **电量** | **[需实验验证]** | 标准 GATT Battery Service `0x180F` / `0x2A19`，**只读百分比**，读得到就有预警价值。 |
| **低电量 → BLE 音频断续** | **无权威直接结论** | 只找到**专利级**论述（"低功耗自适应功率控制"、LE Audio 广播链路质量归因），非产品级实测：见 [US20250056429](https://patents.justia.com/patent/20250056429)、[US20240187137A1](https://patents.google.com/patent/US20240187137A1/en)。物理上电量下降→发射功率下调→丢包上升是合理机制，但**不足以解释 0.5–1.1s 这种量级的整段断流**（更像连接事件丢失/宿主 CPU 抢占/驱动重启）。 |

**PoC（断流预警）**：
1. 记录每次语音会话的：`GATT RTT 序列`、`ATVV 帧间隔序列`、`电量读数`、`断连事件`，同时由用户标注"这次断流了/没断"。
2. 先做相关性：把"帧间隔 P99 > 阈值"与"用户标注断流"做混淆矩阵；若 AUC 尚可，即为可用预警。
3. 电量归因实验：在**同一位置、同一姿势**下分别用满电与低电（<20%）遥控器各录 30 次，比较断流率；若低电组显著更差才算证实。
4. **判定标准**：能提前 ≥300ms 预警且误报率可接受（<10%）→ 做成"信号弱，请靠近电脑"的 UI 提示；否则只做事后诊断，不要做预警。

---

## 5. 汇总表：能力 · 结论 · 关键 API · 工作量 · 护城河价值

| 能力 | 结论 | 关键 API | 工作量（人日） | 对「护城河/体验」的价值 |
|---|---|---|---|---|
| **改虚拟声卡端点形态（CABLE Output → Microphone）** | **[已验证可行]** 普通用户即可，持久、免重启 | `IMMDeviceEnumerator` / `IMMDevice::OpenPropertyStore(STGM_READWRITE)` / `IPropertyStore::SetValue+Commit` (`{1da5d803-...},0`) | 1–2（含自检+还原） | **★★★★★ 最高护城河**：让所有"只列麦克风"的输入法（豆包、微信…）能用上遥控器音频，且竞品未发现此路；需配"非契约行为"免责与回退 |
| 端点形态自检与自动修复（驱动重装后重放） | [需实验验证] | 同上 + `IMMDevice::GetState` / 端点 GUID 缓存 | 1 | ★★★★☆ 决定"装完就能用"的口碑 |
| **免驱动降级模式（电脑麦克风 + 遥控器当触发器）** | **[已验证可行（架构上）]**，识别率需实测 | `RegisterHotKey`/`SendInput` + 关闭 `autoRouteVirtualMicrophone` | 2–3 | ★★★★★ 零驱动/免重启/免管理员；对豆包天然可用；隐私故事好讲 |
| **豆包全局语音快捷键注入唤醒** | **[需实验验证，最高优先级]** | `RegisterHotKey` 触发链 + `SendInput` / `PostMessage(WM_HOTKEY)` | 1–2 | ★★★★★ 若成立即"支持豆包远程唤醒"，直接改写产品能力边界 |
| 豆包 MSAA / `IAccessible` 触发麦克风按钮 | **[需实验验证，概率偏低]** | `SendMessage(WM_GETOBJECT, OBJID_CLIENT)` + `AccessibleObjectFromWindow` + `accDoDefaultAction` | 0.5–1（一次性判死） | ★★★☆☆ 成了是"零事件注入"的杀手锏，不成也要留档避免重复投入 |
| IME IPC/CLI 排查（管道、开关） | **[需实验验证]** | `\\.\pipe\DoubaoIme\settings-rpc`、`--activate-tsf/--show-window/--wav` | 2–3 | ★★★☆☆ 可能挖到官方入口；合规边界需产品文案约束 |
| **前台输入法识别与投递策略自动路由** | **[需实验验证]** | `ITfInputProcessorProfileMgr::GetActiveProfile(GUID_TFCAT_TIP_KEYBOARD)` + IMM32 降级 | 2–3 | ★★★★☆ 从"一套策略打天下"升级为"按引擎自适配"，是体验差异化的关键 |
| BLE 断流预警（RTT/抖动/电量） | **[需实验验证]** | `BluetoothLEDevice` GATT RTT + ATVV 帧间隔统计 + `0x2A19` | 2–3 | ★★☆☆☆ 提升口碑与售后成本，不是护城河但用户感知强 |
| Win+H（Windows 语音输入）作为默认可注入引擎 | **[已验证可行]** | `SendInput` 注入 Win+H；Win11 26200 实测语言包在位 | 0.5 | ★★★☆☆ 保底可用路径，保证"任何机器都有能用的引擎" |

---

## 6. 参考链接

- [AccessibleObjectFromWindow (oleacc.h)](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromwindow) · [Handling the WM_GETOBJECT Message](https://learn.microsoft.com/en-us/windows/win32/winauto/handling-the-wm-getobject-message) · [Object Identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/object-identifiers)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) · [Keyboard Input Overview / Hot-Key Support](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input) · [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) · [RAWINPUTHEADER](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputheader) · [GetMessageExtraInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessageextrainfo)
- [Audio Endpoint Properties](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-endpoint-properties) · [EndpointFormFactor](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/ne-mmdeviceapi-endpointformfactor) · [PKEY_AudioEndpoint_FormFactor](https://learn.microsoft.com/en-us/windows/win32/coreaudio/pkey-audioendpoint-formfactor)
- [VB-Audio Licensing / Distribution](https://vb-audio.com/Services/licensing.htm) · [VB-CABLE 产品页](https://vb-audio.com/Cable/index.htm) · [Virtual Audio Cable 术语表](https://vac.muzychenko.net/en/manual/glossary.htm) · [VirtualDrivers/Virtual-Audio-Driver](https://github.com/VirtualDrivers/Virtual-Audio-Driver) · [AudioMirror](https://github.com/gmh5225/Driver-audio-AudioMirror)
- [ITfInputProcessorProfileMgr](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itfinputprocessorprofilemgr) · [GetActiveProfile](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfinputprocessorprofilemgr-getactiveprofile) · [GetKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardlayout) · [ImmGetDescriptionW](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immgetdescriptionw) · [ImmGetIMEFileNameW](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immgetimefilenamew)
- [open-voice-bridge（RC003 参照实现，GPL-3.0）](https://github.com/nijez/open-voice-bridge) · [remote-bridge-hub（ATVV 参考，GPL-3.0）](https://github.com/xxb26553663-star/remote-bridge-hub) · [WinRT BluetoothLEDevice](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothledevice)
- 低电量/功率控制相关（专利级，非产品实证）：[US20250056429](https://patents.justia.com/patent/20250056429) · [US20240187137A1](https://patents.google.com/patent/US20240187137A1/en)

**本报告的性质说明**：问题 2 的核心结论（form factor 可写）与问题 1、3、4 的本机取证均来自对目标机的真实执行；引用文献除标注外均为微软官方文档。凡标注 **[需实验验证]** 的条目，请按对应 PoC 步骤实测后再写入产品承诺。

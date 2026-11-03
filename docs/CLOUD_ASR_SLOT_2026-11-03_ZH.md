# 云端 ASR 语音槽原型（2026-11-03）

## 结论（今天收尾时的状态）

**代码已经落地，"空闲零常驻"这条路已用实测数字验证；但"能不能替代微信输入法"今天还没有答案。**

- 已实现并推送到 `play`：`900e30d feat(v2.0): add opt-in cloud ASR voice slot`、`a89373a docs: disclose the optional cloud ASR upload path`。
- 内存：空闲私有内存 微信输入法槽 **57.4 MB** → 选中云端槽 **57.4 MB**（零增量），跑完一次会话后 58.1 MB；没有任何常驻进程，也没有到服务商的连接。
- 缺口：本机没有 ASR API Key，物理遥控器按键也无法由 Agent 代按，所以**"松开到出字"延迟和中文识别质量两个数字是空白**，本轮不拿估算顶替。
- 因此**没有卸载 WeType**；Vibe Link 已恢复 `微信输入法 / Ctrl+Win / 单击切换`，与实验前一致。

## 环境与版本

| 项目 | 实测值 |
|---|---|
| 系统 | Windows 11，x64，物理内存 15.6 GB |
| Vibe Link | 2.0.0，`H:\from-github-project\vibe-flow\VibeMic.exe --background` |
| 录音内核 | `v1.0.3`，Capture `1.2.1.0`，**未修改**（SHA-256 仍是 `B62DE035…2E683`） |
| 语音链路 | RC003 --ATVV--> `CABLE Input` --> `CABLE Output` --> 工具取音 |
| NAudio | 2.2.1（Host 新增引用，与冻结 Capture 用的是同一份固定版本） |
| 需要驱动 | 否 |
| 需要重启 | 不需要系统重启；改动生效需要重启 VibeMic.exe（已重启） |

## 选型：A 硅基流动

| 方案 | 价格 | 大陆可达 | 门槛 |
|---|---|---|---|
| **A 硅基流动** | `FunAudioLLM/SenseVoiceSmall` 标价免费；付费兜底 `Qwen/Qwen3-ASR-1.7B` 约 ¥0.00022/秒 ≈ ¥0.0132/分钟 | 好（本机实测直连，未用代理） | 短信/邮箱注册即可调 API；充值/开票才要实名；**不需要用户备案** |
| B OpenAI whisper | 约 $0.006/分钟（≈¥0.043/分钟） | 差，需要代理 | 外卡账号 |
| C 阿里云 / 讯飞 | 各家按秒计费 | 好 | SDK、账号、实名链条更长 |

选 A 的理由：接口是 OpenAI 风格 multipart（`POST /v1/audio/transcriptions`，`file` + `model`），国产链路直连，不需要本地模型，也不给"省内存"这件事再引入代理变量。

**未复核项（如实标注）**：上面两行价格来自厂商定价页（早前抓取），本轮没有联网复核；正式付费前请自行确认。

## 实现

### 新增与改动

| 文件 | 作用 |
|---|---|
| `scripts/features/CloudAsrService.cs`（新增） | 设置存储、同意文案、fail-closed 上传闸、WASAPI 采集、48 kHz 立体声 float → 16 kHz 单声道 16 bit（仅内存）、HTTPS multipart、Unicode 注入、一次按键一个会话对象 |
| `scripts/VibeMic.cs` | 第四个 provider key `cloud`、UI 选项、同意闸、按键时开会话、停 Capture 时取消、**不派发任何外部快捷键** |
| `BUILD_VIBE_MIC.cmd` | 引用固定版本 NAudio 2.2.1（并附带缺失时的 restore 保护） |
| `scripts/validate.js` + Host 自检 `RunCloudAsrSelfTests()` | 钉住：默认关闭、上传闸、同意文案、Key 只从本机文件读、音频转换 |
| `THIRD_PARTY_NOTICES.md`、`docs/FEATURES_ZH.md`、`docs/V2_0_USER_GUIDE_ZH.md` | 把"不上传"从无条件表述改为"三个外部槽照旧 + 这一个默认关闭的例外" |

### 会话生命周期

```
F5 按下（Bridge→Host VOICE WAKE held=True）
  → Host 新建 CloudAsrSession 线程，在 Host 进程内开 WASAPI 采集 CABLE Output
  → 冻结 Capture 同时照旧把 ATVV 音频写进 CABLE Input
F5 松开
  → 再等 240 ms 尾巴 → 停采 → 内存里转 16 kHz 单声道 WAV
  → 闸门检查（开启? 已同意? 有 Key? 时长 300 ms–60 s? 体积 ≤50 MB?）
  → 一次 HTTPS 上传 → 校验"按下时的那个输入框"仍是焦点可写
  → Unicode SendInput 落字 → 释放录音器/设备/内存 → 会话对象置 null
```

- 单次最长 60 秒，与冻结 Capture 的设备边界一致。
- 文字只在内存里存在到落字为止，不落盘；不自动回车、不自动发送。
- 焦点校验失败：不上屏、提示"焦点已改变"，不投递到别的窗口。

### 失败矩阵（都只给一句话，不自动重试）

| code | 用户看到 |
|---|---|
| `NOT_CONFIGURED` / `NO_KEY` | 云端 ASR 还没配置好 / 没读到 API Key |
| `AUDIO_DEVICE` | 没找到 CABLE Output |
| `TOO_SHORT` / `TOO_LONG` | 没录到足够声音 / 超过 60 秒，没上传 |
| `OFFLINE` | 网络不可用，这次不出字 |
| `AUTH` (401/403) | Key 被服务商拒绝 |
| `RATE_LIMIT` (429) / `OVERLOADED` (502–504) / `SERVER` | 限流 / 过载 / 服务端错误 |
| `EMPTY` / `FOCUS` / `INJECT` | 空结果 / 焦点已变 / 注入失败 |

### 冻结件兼容做法（原型痕迹，需知悉）

冻结 Capture 只在"快捷键可解析"时放行音频，因此 `cloud` 槽给它一个占位键 **F24**（Windows 无默认绑定，Host 自己绝不发送）。代价是每次会话冻结 Capture 会向前台应用注入一次 F24 按下/抬起，绝大多数程序无感。这是原型做法，不是最终方案。

## 隐私处理

- **默认关闭**：`%LOCALAPPDATA%\Vibe Flow Remote\UserData\cloud-asr.json` 当前内容就是 `{"enabled": false, "consentedAt": 0}`（代码里 `Default()` 也是这两个值）。
- **必须显式同意**：第一次在语音页选中「云端 ASR（实验）」会弹原生确认框，写明"音频会离开本机、只在松开录音键后上传、Vibe Link 不存录音也不存文字、按录音键前无任何请求"；选「否」自动退回原工具。
- **离开即关**：切回任何其他槽会把 `enabled` 写回 false。
- **Key 路径**（不写进配置、不进日志）：`VIBE_CLOUD_ASR_KEY_FILE` → `cloud-asr.json` 的 `keyPath` → `%USERPROFILE%\.codex\secrets\siliconflow_api_key.txt`。
- 未按住录音键时：不建录音器、不建 socket。实测空闲时进程上唯一的外连是既有的 GitHub 更新检查。

## 实测

| 项目 | 数字 | 来源 |
|---|---|---|
| 空闲私有内存（微信输入法槽，基线） | VibeMic **57.4 MB**，WS 85.6 MB（另 Capture 21.3、Bridge 29.9） | `Get-Process` 静置 50 s |
| 空闲私有内存（选中云端槽，无 Key） | VibeMic **57.4 MB**，WS 85.5 MB | 同上，重启后 45 s |
| 一次会话结束后空闲 | VibeMic **58.1 MB**，WS 86.6 MB（+0.7 MB，GC 噪声级） | 会话（被 `NO_KEY` 拦住）之后 |
| 采集链路 | `CABLE Output (VB-Audio Virtual Cable)`，48 kHz/2ch/float32 → **16 kHz/1ch/16 bit WAV**，采 1.2 s 得 1252 ms | 独立探针 exe 跑 `CloudAsrAudioRecorder` |
| 上传通道 | 用无效 Key POST 真接口 → **HTTP 401，337 ms**，分类 `AUTH` | 同上（证明地址、TLS 1.2、multipart 都通） |
| 会话生命周期 | `CLOUD ASR session begin → skipped reason=no_key → finished ok=false code=NO_KEY`，全程未开设备、未发请求 | `vibe-flow-host.log` |
| **松开到出字延迟** | **未测** | 需要 API Key + 物理按键 |
| **识别质量（统一测试句）** | **未测** | 同上 |
| 门禁 | `BUILD_VIBE_MIC.cmd`、`VibeMic.exe --self-test`、`node scripts/validate.js` 全绿；冻结 Capture 未变 | 本地 |

## 下一步（照做即可补齐剩下两个数字）

1. 建 Key 文件（一行，只有 Key）：
   `C:\Users\46549\.codex\secrets\siliconflow_api_key.txt`
2. 打开语音页 → 「云端 ASR（实验）」→ 同意提示 → 确认状态变成"已配置 · 松开录音键后上传识别"。
3. 点进一个输入框，**按住 F5** 说统一测试句：
   `明天下午三点提醒我开会，然后把会议链接发到邮箱。`
4. 收数就看这几行（`%LOCALAPPDATA%\Vibe Flow Remote\UserData\remote-voice-session\vibe-flow-host.log`）：
   - `CLOUD ASR capture_stopped bytes=… audio_ms=…`
   - `CLOUD ASR upload_end ok=true release_to_text_ms=… characters=…`
   - `CLOUD ASR inject ok=true … inject_ms=…`
5. 测完按需切回微信输入法（切回会自动把云端上传开关关掉）。

## 成本（按日志里的真实用量）

| 场景 | 免费 SenseVoiceSmall | 付费 Qwen3-ASR 兜底 |
|---|---|---|
| 5 分钟/天 | ¥0 | 约 ¥0.07/天 → **约 ¥2/月** |
| 15 分钟/天 | ¥0 | 约 ¥0.20/天 → **约 ¥6/月** |
| 60 分钟/天 | ¥0 | 约 ¥0.79/天 → **约 ¥23.8/月** |

## 回滚

| 层级 | 做法 |
|---|---|
| 只想停用 | 语音页切回微信输入法（上传开关自动关）；或把 `cloud-asr.json` 的 `enabled` 改回 false |
| 想彻底移除代码 | 按依赖顺序 `git revert a89373a 900e30d`（先回滚文档、再回滚特性） |
| 只是想清掉痕迹 | 删 `cloud-asr.json`，Host 下次读到就回到 `Default()` |

回滚**不需要**动冻结 Capture、不需要改任何哈希、不影响三个现有语音槽。

## 已知风险与限制

- 免费模型可能 429/503：已按 `RATE_LIMIT` / `OVERLOADED` 明确告知，不自动重试。
- Host 现在依赖 `NAudio.Core.dll` / `NAudio.Wasapi.dll`（这两个文件本来就随包发布）；缺失时只有云端槽报错，不影响程序启动与其他三个槽。
- 上传时进程内会把 TLS 默认协议抬到 1.2（只在真正上传的那一刻，沿用更新检查的做法）。
- 落字依赖"按下录音键时那个输入框仍是焦点且可写"；用户中途切窗口不会投递到别处。
- 单次 60 秒上限来自冻结 Capture 的设备边界，没有做长录音续接。

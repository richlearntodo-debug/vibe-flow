# 语音听写 / 语音输入类产品功能竞品与市场调研

**面向产品**：言灵 Vibe Flow Remote（Windows 10/11 桌面伴侣，RC003 蓝牙遥控器按住说话 → VB-CABLE → 第三方语音工具）
**调研日期**：2026-09 ｜ **方法**：公开检索 + 官方文档/帮助中心/定价页 + 第三方评测（web_search / web_fetch）
**证据标注**：未标注者为**有来源的事实**；标【推断】者为分析判断。定价为检索当时官网口径，可能变动。

---

## 0. 结论速览

1. **赛道已从「转写准确率」转向「输出质量与上下文适配」**。头部工具（Wispr Flow / Typeless / Willow / Diction）的差异化全在 AI 润色、按应用调性、自定义词表、语音命令，而非 ASR 本身。
2. **「按应用自动切换风格」已是标配而非加分项**：Typeless 连免费档都提供 "Different tones for each app"（[Typeless Pricing](https://www.typeless.com/pricing)）。本产品的 Smart Profiles 方向正确，但目前只切 **快捷键表**，未切 **输出风格/词表/模板**。
3. **「输入目标定位」是被验证的行业级痛点，也是本产品最被低估的资产**。官方与第三方都承认听写在「说完到落字」的间隙会因焦点漂移而把文字写进错误窗口（[Voice Keyboard Pro](https://voicekeyboardpro.com/blog/dictation-text-appears-wrong-place.html)）；竞品普遍**只能靠「用户自己先点好输入框」**，没有一个做「开始录音时锁定目标 + 会话内恢复」。Smart Focus 是稀缺能力。
4. **剪贴板投递是技术债而非可选方案**。2026-07 的调研显示开源听写工具**全部**走「存剪贴板→覆写→合成 Ctrl+V→sleep→恢复」，必然带来剪贴板污染、UIPI 静默失败、恢复竞态（[win-text-inject](https://github.com/emerson-d-lopes/win-text-inject)）。中文侧输入法走 IME/TSF 通道，是**真·直写上屏**。
5. **中文本地化的胜负手在标点/数字/口语清理，不在方言数量**。Win+H 官方确认把中文数字强制归一化为阿拉伯数字且**无开关**（[Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5626530/voice-typing-cant-input-chinese-numeral-hanzi-and)），这是本产品低成本可抢的差异化位。
6. **「语音听写 + 专用物理触发器」已被头部厂商用硬件验证**：Wispr Flow 官方售 **Wispr Pedal（$199）**，Elgato Stream Deck Pedal（$89.99）提供**按应用自动切换配置的 Smart Profiles**（[Elgato](https://www.elgato.com/eu/en/explorer/products/stream-deck/foot-pedals-for-ai-prompting/)）。**【推断】本产品"用一支廉价遥控器做同样的事"在成本上具备结构性优势，值得作为对外定位的核心叙事。**
7. **隐私是本产品最大的、且竞品正在自我削弱的差异点**：Wispr Flow 的 trial/standard 账号默认拿 audio/transcript/edits 训模型，Zero Data Retention 需两个开关同时满足（[调查](https://www.getvoibe.com/resources/wispr-canto-voice-model-training-data/)）；而本产品"不读、不存、不上传"是**架构事实**而非设置项。

---

## 1. 竞品清单与功能矩阵

### 1.1 桌面 AI 听写工具（国际）

| 产品 | 平台 | 定价 | 核心卖点 | **值得借鉴的具体功能** |
|---|---|---|---|---|
| **Wispr Flow** | Mac/Win/iOS/Android | 免费 2000 字/周；Pro $15/mo（年付 $12/mo）；Teams $10/人/mo | 系统级流式听写 + 云 LLM 自动编辑 | ① **Command Mode**：选中文本口述指令（"改正式些""转成要点""翻译成西语"）原地改写；② **Styles** 按场景调调性；③ **Personal Dictionary + Auto-add to Dictionary：监听你粘贴后的修改自动学词**；④ **Voice Snippets** 触发短语展开为长文本；⑤ **Context Awareness** 读当前窗口文本（默认开，**截屏 OCR 默认关**，可整体关闭）；⑥ 双击 Fn 常驻录音、Fn+Space 免手；⑦ Whisper Mode 悄悄话（[深潜报告](https://github.com/moona3k/macparakeet/blob/bddb2dad/docs/research/wisprflow-deep-dive.md?plain=1)） |
| **superwhisper** | Mac/Win/iPhone/iPad | 免费；Pro $8.49/mo、$84.99/yr、**$249.99 终身** | 本地模型 + BYOK 云 LLM 双轨 | ① **Modes**（Super/Voice/Message/Email/Note/Meeting/Custom）每个模式一套 AI 指令；② 内置模式的 prompt **可直接复制出来改成自定义模式**；③ History **重新处理**（换模型重跑）；④ 配置备份/FileSync；⑤ Windows 尚缺 Hold-Shift-Auto-Send、Simulate Keypresses、Agentic 集成（[官方 Windows 支持页](https://superwhisper.com/docs/get-started/windows)、[Pro 定价](https://superwhisper.com/docs/get-started/sw-pro)） |
| **VoiceInk** | macOS 14.4+ | GPL v3 开源；授权 $29–69 一次性 | 纯本地 whisper.cpp + Parakeet | ① **Power Mode 按前台应用（含浏览器 URL）自动切换**模型档位/自定义词典/自动大写/标点风格/prompt 上下文；② 录音时**自动暂停媒体播放**（[Starlog 解析](https://starlog.is/articles/developer-tools/beingpax-voiceink/)） |
| **MacWhisper** | macOS | Gumroad **€59 买断**；App Store $6.99/mo、$29.99/yr、$99.99 终身 | 本地 Whisper **文件转写**工具（实时听写是次要功能） | 本地全尺寸 Whisper 模型、批量文件夹、YouTube URL 转写、SRT/VTT 字幕、说话人分离(beta)、系统音频录制、监听文件夹、BYOK（[定价](https://www.getvoibe.com/resources/macwhisper-pricing/)） |
| **Aqua Voice** | Mac 桌面 | 免费 1000 字；Pro **$8/mo**；Max $24/mo；Team $12/人/mo | Avalon 模型 + 大词典 | ① **Realtime Mode 边说边看字**（Max）；② Expanded Custom Dictionary（约 800 词）；③ Custom Instructions；④ **"Send it" 语音指令**（[官方定价页](https://aquavoice.com/pricing)） |
| **Willow Voice** | Mac/Win/iOS/Android | 免费 2000 字/周；$15/mo 或 $144/yr；Team $10/人/mo（3 席起） | 跨四端 + 写作风格记忆 | ① **Smart writing style memory**：按 app 类别适配语气（Slack 口语 / Gmail 正式 / Cursor 技术），**从用户编辑中学习**；② **AI Mode** 把零散口述扩写成完整消息；③ 可选离线模式；④ iOS 键盘内可直接编辑（[实测评测](https://www.getvoibe.com/resources/willow-voice-review/)） |
| **Typeless** | Mac/Win/iOS/**Android** | 免费 **8000 词/周**（+30 天 Pro 试用）；Pro $30/mo 或 $144/yr（$12/mo） | 云 + 开箱即用的智能编辑 | ① **免费档即含 per-app tones**（官方定价页直列"Different tones for each app"）；② Translate；③ Ask anything；④ Personal dictionary；⑤ Whisper mode（[官方定价](https://www.typeless.com/pricing)、[对比](https://spokenly.app/comparison/typeless)）。**注意**：营销称 on-device，隐私政策确认音频在**云端**转写、无离线模式（[评测](https://www.getvoibe.com/resources/typeless-review/)） |
| **Spokenly** | Mac/Win/Linux/iOS | 本地模型**永久免费不限量**（BYOK 也免费）；Pro **$99.99/yr（$8.33/mo）** | 本地 Parakeet/Whisper + BYOK | ① 用户自选模型供应商（Parakeet/Whisper/OpenAI/Deepgram/Soniox）；② **AI Instructions 按目标 App 做格式清理**；③ **MCP server 供 Claude Code/Cursor/Codex**；④ Agent mode 语音操控；⑤ local-only 模式零网络请求（[官网](https://spokenly.app/)） |
| **Handy** | Win/Mac/Linux | 开源免费 | 完全离线 STT、可扩展 | 开源可审计、跨平台一致的按键听写范式（[GitHub](https://github.com/cjpais/Handy)） |
| **Whispering** | 桌面 + Web | MIT 开源免费 | local-first、数据自持 | ① **语音激活听写**：按一次开始，之后**自动按说话起停**，无需一直按住；② **多模型串联 pipeline**（转写→修语法→格式化，例：Groq→Claude→Gemini）；③ ~22MB 轻量、录音存本地 IndexedDB（[YC Launch](https://www.ycombinator.com/launches/OAh-whispering-local-first-open-source-speech-to-text-at-your-fingertips)） |
| **Diction** | iOS | 订阅制（Writing Tools） | 按应用调性 + 自托管 | ① **Tones：给每个 app 指定语气**（Professional/Casual/Friendly/Clean + 纯自然语言自定义）；② My Words；③ Context-Aware Text Editing；④ 支持 on-device / self-hosted / 云（[官方 Tones 文档](https://diction.one/features/tone-presets.html)） |
| **网易叭哥说** | Mac/Win | 永久免费 | AI 原生语音 Agent | 124 语种实时互译、热词命中率宣称 >99%、自研真流式 ASR 自动过滤停顿/改口/重复、**云端零留存**（[快科技](https://news.mydrivers.com/1/1150/1150219.htm)、[站长之家](https://www.chinaz.com/2026/0910/1776309.shtml)） |

### 1.2 中文侧

| 产品 | 平台 | 定价 | 核心卖点 | **值得借鉴的具体功能** |
|---|---|---|---|---|
| **微信输入法** | Win/Mac/iOS/Android/鸿蒙 | 免费 | 语音**自动上屏**、全局可用 | ① 输入框麦克风或 `Ctrl+Win` 唤起，**文字直接落到光标处（IME/TSF 通道，不碰剪贴板）**；② **"整理文字"自动剔除"嗯/啊/然后/那个"并精简逻辑**；③ 智能标点与断句、自动分段；④ 中英文混合；⑤ **"单机模式"离线/本地词库/不上传——但不支持语音转文字**（[微信 PC 语音](https://www.163.com/dy/article/KLL3USVH0511CPVM.html)、[单机模式](https://m.mydrivers.com/newsview/981326.html)） |
| **豆包输入法** | Win(V0.9.0, 2026-09)/Mac/iOS/Android/鸿蒙 | 免费 | 语音优先 + 五端打通 | ① **长按右 Alt 唤起语音**（把"按住说话"做成系统级手势，与本产品形态同构）；② 多方言、中英混输、**专业术语识别**；③ **弱网/无网仍可语音输入**；④ 账号云同步个人词库；⑤ **超级互传**跨端粘贴；⑥ 无广告弹窗；⑦ iOS 侧"智能文字整理"（排版/精简/润色/纠错）（[站长之家](https://www.chinaz.com/2026/0909/1775971.shtml)、[智能文字整理](https://www.chinaz.com/2026/0827/1773686.shtml)、[隐私双模式实测](https://www.thepaper.cn/newsDetail_forward_32037251)） |
| **讯飞输入法** | 全平台 | 免费 | 端侧大模型 + 方言护城河 | ① 14.0 星火端侧输入大模型：**202 种方言"免切换"自由说**、覆盖 288 个地级市、**离线识别率基本持平云端**；② 医疗/法律等行业词库（[大皖新闻](http://www.ahwang.cn/hefei/2024/1026/2763844.html)、[行业横评](https://blog.csdn.net/taotaocwl/article/details/159616794)） |
| **搜狗输入法** | 全平台 | 免费 | AI 服务总入口 | ① 长按空格语音，齿轮内可设**离线语音 / 标点 / 不限时语音**（把易错项做成显式开关）；② AI 帮写、文本翻译、Cola 闪记等 8 项统一入口（[官方 FAQ](https://shouji.sogou.com/wap/feedback/faqdetail?id=2000017)、[重做说明](https://www.chinaz.com/2026/0831/1774144.shtml)） |
| **Windows 语音输入 Win+H** | Win10/11 | 免费内置 | 零安装 | ① 走 **TSF 干净注入**；② 自动标点、脏话过滤开关；③ **语音访问（Voice Access）装语言包后完全本地运行**；④ Copilot+ 的"流畅听写"设备端去语气词——**仅英语区域**；⑤ **中文数字被强制归一化为阿拉伯数字且无开关**（[Microsoft Learn](https://learn.microsoft.com/zh-cn/training/modules/inclusive-software-surface/4-mobility-and-input-accessibility-features)、[数字问题](https://learn.microsoft.com/en-us/answers/questions/5626530/voice-typing-cant-input-chinese-numeral-hanzi-and)、[设置项](https://mstateit.minnesota.edu/TDClient/271/mstate/KB/Article/27857/Configuring-Windows-11-Voice-Typing-Settings)） |

---

## 2. 反复出现的功能模式（12 类）

1. **自定义词表/热词**——几乎是付费墙的第一道分界线：superwhisper / Wispr Flow 的 Personal Dictionary（**可从用户纠正中自动学习**）、Aqua Voice 约 800 词大词典、Typeless Personal dictionary、Diction My Words、讯飞行业词库、网易叭哥说热词 >99%。
2. **AI 润色与格式化**——去口语（um/uh；中文"嗯/啊/然后/那个"）、自动标点与断句、按语义列表化（"第一…第二…"→ 编号列表）、**改口纠正**（Wispr Flow："2 点… 其实 3 点"→"3 点"）、保留代码写法（camelCase/snake_case/缩进）。
3. **按应用自动切换风格（per-app profiles）**——**最普遍的共识功能**，已跨品类扩散：Typeless「Different tones for each app」（免费档即有）、Diction Tones（每 app 一个语气）、VoiceInk Power Mode（前台进程+浏览器 URL）、Willow style memory（按 app 类别）、**LotusQ app-aware formatting**（识别 Discord/Outlook/VS Code/Slack 用不同格式）、**TypeWhisper Workflows 按 app/网站/热键匹配**、superwhisper Modes 手动切、Wispr Flow Styles，甚至硬件侧 Elgato Stream Deck 的 **Smart Profiles 按应用切换整套配置**。**本产品已有 Smart Profiles 骨架，但只覆盖按键表，未覆盖输出风格/词表——这是最容易补齐的高价值缺口。**
4. **语音命令与片段（snippets）**——Wispr Flow Voice Shortcuts（"插入签名"→ 多行签名）、Command Mode 选中改写、Aqua Voice "Send it"、Spokenly AI Instructions、Whispering 多模型 pipeline。**这类功能全部在"转写之后、上屏之前"的文本加工层，不触碰录音链路。**
5. **翻译 / 双语**——Wispr Flow 104+ 语种、Typeless Translate、网易叭哥说 124 语种互译、讯飞/搜狗语音翻译。
6. **历史记录与检索**——superwhisper History + **重新处理**、Diction Transcription History、Wispr Flow 历史。这与本产品"不保存转写文字"的隐私约束**直接冲突**，**建议明确不做并把它写成隐私卖点**（见 §6 第 11 项）。
7. **团队/共享词库**——Wispr Flow Teams（共享词典 + 共享 snippets + 用量看板）、Willow Teams（共享词典 + 管理后台 + SOC 2/HIPAA）。本产品为单机形态，**建议不做**。
8. **本地模型 vs 云模型**——已分化为三种立场：纯云（Wispr Flow/Typeless，延迟受网络影响，高峰曾出现 20–30s 等待）、纯本地（VoiceInk/Handy/MacWhisper，隐私强、无网络延迟）、**双轨可切**（superwhisper、Spokenly BYOK、Diction 自托管、Willow 可选离线）。**本产品天然属于「复用第三方」的第四类，应把"模型可换"讲成卖点而非短板。**
9. **延迟与流式上屏**——Aqua Voice Realtime Mode、网易叭哥说真流式 ASR、Whispering 语音激活听写。注意：本产品录音链路冻结（松开后 drainMs=180 再交棒），**上屏延迟由所选第三方工具决定**，产品叙事应主动把这个事实转化成"我们不做二次延迟"。
10. **麦克风选择与降噪**——几乎所有工具都有输入设备选择；VoiceInk 还会在录音时暂停媒体播放。本产品用 VB-CABLE 固定链路，**"选错麦克风"是最高频支持工单**，自检页已是正确投入方向。
11. **快捷键/触发器设计（按住说 vs 点击切换）**——Wispr Flow 同时提供「按住 Fn」与「双击 Fn 常驻」；Whispering 提供「按一次后语音激活自动起停」；豆包用「长按右 Alt」；搜狗用「长按空格」；Whisper Mode 支持气声。**结论：按住说是主流默认（HN 上 Hush/Yapyap/TypeWhisper/LotusQ 等开源项目一律默认 hold-hotkey），但成熟产品都会给一个"免长按"的第二模式。** 本产品录音链路冻结，第二模式的最优解是 **RC003 语音键「长按=说话 / 双击=整体暂停」双模共存**（`nijez/open-voice-bridge` 已在同款遥控器上真机实现，见 §3）。
12. **隐私声明与开关**——分层明显，且**头部厂商正被公开质疑**：Wispr Flow 官方安全 FAQ 写明 **trial/standard 账号默认开启用 audio/transcript/edits 训练模型**（Enterprise/HIPAA 才默认关），设置项由 "Privacy Mode" 改名为 **"Improve the model for everyone"（开关极性相反）**，Zero Data Retention 需"Privacy Mode 开 + Cloud Sync 关"**两个开关同时满足**，而**转写始终在云端**，用量统计不受开关约束；其 2026-08 发布的 Canto 模型（$280M B 轮、$2B 估值）**未披露训练数据来源**（[Canto 调查](https://www.getvoibe.com/resources/wispr-canto-voice-model-training-data/)）。Typeless 营销称 "on-device"，但其隐私政策确认**音频在云端转写**，"本地"仅指历史记录，且无离线模式。正面样本：豆包"基础模式"全离线（代价是语音被关）、微信"单机模式"离线不上传（代价是语音不可用）、网易叭哥说云端零留存。【推断】**"隐私承诺依赖若干开关同时正确"是行业通病——这正是本产品"架构上就不存"的最大叙事机会。**

---

## 3. 硬件/遥控器形态的产品

**关键发现：头部云听写厂商已经开始卖"物理触发器硬件"——这直接验证了本产品的形态假设。**

- **Wispr Pedal（$199）**：Wispr Flow 官方推出的脚踏板，**出厂即预映射到 Flow**、随附 1 年 Pro、可编程 HID（[Elgato 报道](https://www.elgato.com/eu/en/explorer/products/stream-deck/foot-pedals-for-ai-prompting/)）。**这是"语音听写 + 专用物理触发器"被头部厂商真金白银验证的最强证据。**
- **Elgato Stream Deck Pedal（$89.99）**：三踏板 + **Smart Profiles 按当前应用自动切换配置**（同上）。**【推断】注意"Smart Profiles"这个名字与用法与本产品高度重合，说明"按前台应用切换行为"是跨品类的通用解法。**
- **RC003 的技术事实**：RC003 是**双 profile 设备**——按键走 Bluetooth HID，语音走 BLE GATT 的 **ATVV**（Android TV Voice-over-BLE，16 kHz IMA/DVI ADPCM）。**它不是标准蓝牙麦克风/HFP 设备**，必须由软件解码并回环到虚拟声卡。【推断】这解释了为什么 VB-CABLE + 自研 Capture 是必要路线，也解释了竞品难以快速复制。
- **同形态直接竞品**：`ZSTDJan/windows-remote-mic-app`（无线麦【Win版】）——把蓝牙语音遥控器按键与麦克风变成 Windows 快捷操作与语音输入桥接，**公开版本首先适配 RC003**；13 个实体键、单击/双击/长按/组合键、按住说话、动作支持键盘快捷键+系统操作+Quicker、虚拟音频用 CABLE Output、输入法适配搜狗/微信/Win+H/豆包/自定义程序、**需管理员权限、v0.2.0 candidate 未签名**（[GitHub](https://github.com/ZSTDJan/windows-remote-mic-app)）。macOS 侧 `nijez/open-voice-bridge` v0.4.0 有 RC003 真机验收：13 键映射、BlackHole 2ch 回环、**双击语音键 = 暂停整个桥接**（"长按说话 / 双击暂停"双模并存的现成范例）。**【推断】这是与本产品重合度最高的对手，但其定位偏"按键映射工具"，缺少 Smart Focus / 首次设置向导 / 自检体系这类成品化投入；且其未签名 + 需管理员权限，本产品在安装体验上有明确优势。**
- **语音输入设备（非遥控器）**：**讯飞智能语音鼠标 Lite M320 / AM50 Pro（约 ¥498）**——鼠标语音键**按住说话**，PC 上屏/翻译（[商品页](https://m.suning.com/product/0000000000/12409352472.html)、[品玩评测](https://www.pingwest.com/w/239838)）；**8BitDo Micro**（键盘模式）映射成 Ctrl、双按触发系统听写，用户的动因就是**"离桌"**——在动感单车/沙发上直接对 Claude Code 说话，不必走回桌前（[Zenn 案例](https://zenn.dev/ryu1maniwa25/articles/8bitdo-micro-voice-input-claude-code)）；Cheerdots AI 鼠标/简报器（触控板可拆卸 + 空中操作 + PPT 遥控 + 转写摘要，[Amazon](https://www.amazon.com/dp/B0DZ5S2WPC)）；OM SYSTEM 4 踏板 USB 听写脚踏（[RS31N](https://dictation.omsystem.com/en/dictation-transcription-accessories/rs31n-usb-foot-pedal-with-4-pedals/)）。**反例（不向 PC 注入文本）**：PLAUD NOTE/NotePin、Limitless Pendant、Rabbit R1 类 PTT 设备——它们按住录音但只在自家 App/云里成文（[PLAUD](https://support.plaud.ai/hc/en-us/articles/55643286546329-How-to-Record)、[Limitless](https://help.limitless.ai/en/articles/10619382-pendant-quick-start-guide)）。**【推断】"物理按键 + 语音"是已验证品类，但主流形态是自研硬件；本产品的机会恰是"软件定义、复用用户已有的廉价遥控器"，而讯飞鼠标证明了"按住说话"的硬件交互在中国市场已被消费者接受。**
- **按住说 vs 点击切换的行业证据**：Elgato 明确指出**系统自带听写（macOS 双击 Globe / Win+H）是 toggle 而非 hold-to-talk**，而脚踏板/物理触发器的价值在于**离键盘、手忙、RSI 缓解**（[Elgato](https://www.elgato.com/eu/en/explorer/products/stream-deck/foot-pedals-for-ai-prompting/)）。反向证据：Claude Code 有用户主动要求给语音模式**加 toggle**（issue #33025，长按累）。**【推断】结论是"双模共存"才是成熟做法；本产品录音链路冻结，可用"双击语音键 = 暂停/切换桥接"（open-voice-bridge 已示范）在不改内核的前提下提供第二模式。**
- **「物理按键 + 语音」相对纯快捷键的优势场景**【推断，基于上述形态共同点】：① 手已占用（Vibe Coding 打字中、拿东西、做演示）；② 离键盘远（沙发/会议/白板前）；③ 触发意图明确，不与编辑器快捷键冲突；④ 有触感反馈，不必看 HUD 确认"是否在录音"；⑤ 单手持握时拇指天然落在语音键。**③④ 与 ①（离键盘）是本产品可直接写进文案、且已被 Wispr Pedal/Stream Deck Pedal 的市场行为佐证的点。**

---

## 4. 中文语音输入的独特需求

1. **中文标点与断句**：微信（智能标点与断句、自动分段）、搜狗（显式"标点"开关）、讯飞/搜狗实时转写智能断句。**中文没有词间空格，标点缺失会导致整段不可读**，因此中文侧对标点的敏感度高于英文。
2. **阿拉伯数字 vs 汉字数字**：Win+H 官方确认强制归一化为阿拉伯数字且**无开关**（"一番唇舌"→"1 番唇舌"），官方回复建议改用第三方中文输入法；国内输入法普遍提供数字风格偏好。**这是本产品可做"风格提示/前置校验"的具体抓手。**
3. **中英混排**：微信 PC 支持中英文混合；豆包"中英混输"且在粤语夹英语场景表现良好。**中英之间的空格、专有名词大小写是高频返工点。**
4. **口语化清理**：微信"整理文字"剔除"嗯/啊/然后/那个"并精简逻辑；豆包"智能文字整理"（精简/润色/纠错）；网易叭哥说自动过滤停顿/改口/重复；**Windows 流畅听写能去语气词，但仅限英语区域**——中文用户在内置方案里拿不到这个能力。
5. **行业术语**：豆包专业术语识别；讯飞医疗/法律词库；网易叭哥说热词命中率宣称 >99%。**【推断】中文专业术语的痛点集中在"同音异义词"（如"实现/时限""注入/驻入"），单靠通用 ASR 无解，必须靠用户词表 + 纠错反馈闭环。**
6. **直写上屏 vs 剪贴板投递**：输入法（微信/豆包/讯飞/搜狗）以 IME 身份走 TSF/系统输入通道，是**真·直写上屏、不碰剪贴板**；而独立听写小工具**普遍**走"存剪贴板→覆写→合成 Ctrl+V→sleep→恢复"，带来四类缺陷：转写文本进入剪贴板历史与微软云剪贴板（Wispr Flow 官方文档即承认听写文本可能出现在 Windows 剪贴板管理器中）、按住修饰键时 `Ctrl+V` 被解释为 `AltGr+V`、向提权窗口注入被 UIPI 静默阻断、恢复时序竞态（固定延迟在目标卡顿时会贴入旧内容）（[win-text-inject](https://github.com/emerson-d-lopes/win-text-inject)）。**关键限制**：`CanIncludeInClipboardHistory=0` 等隐私 opt-out 格式只是**协作协议而非强制**，无视它的剪贴板管理器仍会捕获文本。**【推断】本产品"不读取、不保存转写文字"的定位，在中文竞品语境下是显著差异点——因为主流方案要么上云、要么要读回文本做润色。**

---

## 5. 差异化机会

### 5.1 竞品普遍有、本产品没有（按建议优先级）

1. **转写后的文本加工层（润色/去口语/结构化/改口纠正）** —— 全行业标配；本产品只能"原样落字"。【推断】可在**不读转写文字**的前提下，用"让用户自己在第三方工具里开润色"或"提供推荐配置卡片 + 一键预置第三方工具的润色开关"来间接补齐。
2. **自定义词表/热词的引导与同步** —— 竞品是内生功能；本产品只能引导用户在第三方工具里配置。可做**"术语包"管理与一键跳转到对应工具的词表设置页**。
3. **按应用切换输出风格（而非仅快捷键表）** —— per-app 已是标配；本产品 Smart Profiles 只切按键。可扩展为"按前台进程切换 **Profile + 推荐语音工具 + 推荐风格提示**"。
4. **语音片段 / snippets（触发短语展开）** —— 依赖文本层，本产品形态无法直接做。【推断】可用**遥控器按键触发的"常用文本"** 间接实现（但那会进入剪贴板路径，需评估隐私与污染成本）。
5. **历史记录与检索** —— 与"不保存转写文字"硬冲突。**建议明确声明"我们不做"并把它写成隐私卖点**，而不是回避。
6. **免长按的第二触发模式（点击切换/语音激活起停）** —— 成熟产品普遍提供（macOS 双击 Globe、Win+H 本来就是 toggle；Whispering 有语音激活自动起停；Claude Code 用户甚至反向要求给语音模式**加 toggle**，因为长段口述按住很累，[issue #33025](https://github.com/anthropics/claude-code/issues/33025)）。本产品录音链路冻结，但 **`nijez/open-voice-bridge` 已在 RC003 上示范"长按=说话 / 双击=整体暂停"的双模共存**（[GitHub](https://github.com/nijez/open-voice-bridge)）——**【推断】这是不改 Capture 内核就能拿到的第二模式，实现成本低而体验提升明显。**
7. **多语言/翻译入口** —— 竞品内建；本产品可做**"一键切到豆包/微信的翻译模式"的 Profile 化编排**。
8. **首屏可见的"上下文感知"卖点** —— Wispr Flow/Willow 都把它当旗舰宣传。本产品的 Smart Focus 是**同类但更保守**的能力（只锁输入目标、不读屏幕内容），**目前完全没有被当成卖点讲**。

### 5.2 竞品也没有、但本产品形态适合做

1. **「录音开始即锁定输入目标 + 会话内恢复」的显式可视化** —— 竞品普遍承认文字会跑错窗口，但没有任何一家把"目标锁定"做成可见、可测试、可回滚的功能（Voice Keyboard Pro 甚至把它当自己的卖点，见 [其说明](https://voicekeyboardpro.com/blog/dictation-text-appears-wrong-place.html)）。**Smart Focus 已有此能力，只需 UI 化 + 可解释化，即可成为品类首创。**
2. **遥控器作为"多工具调度台"** —— 竞品是"一个 App 打天下"，本产品天然支持微信输入法/豆包/Win+H/Typeless 多后端。**同一支遥控器上不同按键 = 不同语音后端 + 不同 Profile**，这是纯软件快捷键方案做不到的（键盘热键会被应用抢占，且数量有限）。
3. **「物理按键 → 语音工具」的完整可诊断链路（自检页）** —— 竞品把这类失败留给用户猜（选错麦克风、没聚焦、快捷键不一致）。**自检页 + 诊断包导出是同类产品中罕见的工程化投入，应升级为对外卖点（"语音输入也有体检报告"）。**
4. **零转写数据持有的可验证隐私叙事** —— 竞品的隐私是"承诺 + 几个开关"，且**头部厂商已因默认拿用户数据训模型被公开质疑**（Wispr Flow：trial/standard 默认开训练、Zero Data Retention 需两个开关同时满足、用量统计不受约束，[来源](https://www.getvoibe.com/resources/wispr-canto-voice-model-training-data/)）；本产品的隐私是**架构事实**（不读、不存、不上传）。可做**"隐私白皮书 + 日志字段清单"**，把"我们不存你的字"变成可审计的差异点。**【推断】对比话术示例：竞品需要你在设置里关掉三个开关，我们从来没有那个开关——因为服务器上根本没有你的文字。**
5. **方言/术语的"用户自建词包"跨工具迁移** —— 豆包/讯飞的行业词库是封闭的、绑在自家 IME 里；本产品可作为**中立的词表编排层**（生成各家词表导入格式），【推断】这是本产品在"不自建 ASR"约束下的最优生态位。

---

## 6. 按优先级排序的功能建议表

| # | 功能 | 价值 | 复杂度 | 触碰隐私/冻结约束 | 一句话实现思路 |
|---|---|---|---|---|---|
| 1 | **Smart Focus 可视化与可解释化**（录制前显示"已锁定：Chrome 地址栏"、失败时给原因与一键重试） | 高 | 低 | 否（已有能力，仅 UI 化） | 复用已有 UIA 学习结果，在 Live HUD 与首页回执区把"目标窗口/控件/锁定状态"显式渲染并给出失败原因码 |
| 2 | **按前台进程的 Smart Profiles 升级为「工作流卡片」**（切应用 = 切按键表 + 推荐语音工具 + 风格提示） | 高 | 中 | 否 | 在现有 Profile 结构里增加"推荐语音工具/风格提示"字段，切换时同时更新首页横幅与 HUD 文案，不触碰音频链路 |
| 3 | **录音前预检门禁强化**（焦点缺失、麦克风非 CABLE Output、快捷键不一致 → 阻断录音并给出修复入口） | 高 | 低 | 否 | 在现有"立即测试门禁"上扩项，复用自检页的检测函数，统一成"录音前三秒体检" |
| 4 | **中文输出风格引导卡**（数字风格/标点/中英混排/去口语 → 告诉用户在所选工具里怎么设） | 高 | 低 | 否 | 纯静态知识库 + 按当前所选语音工具过滤展示，配"一键打开该工具设置页" |
| 5 | **术语包（词表编排层）**：集中管理术语，导出为微信/豆包/讯飞/Typeless 各自的导入格式 | 中高 | 中 | 否（不读转写文字，只写用户主动录入的词） | 本地表格 + 各工具导入格式模板；仅存用户手输词，不含任何转写内容 |
| 6 | **遥控器多后端调度**（不同按键 = 不同语音工具/Profile，HUD 显示当前后端） | 中高 | 中 | 否（链路冻结但按键映射属允许范围） | 复用现有按键映射表，增加"切换到工具 X 并同步双方快捷键"的复合动作 |
| 7 | **「不保存转写文字」的隐私白皮书 + 本地可验证日志** | 中高 | 低 | 否（强化现有约束） | 出独立文档页 + 设置页入口，附"日志字段清单"证明不含音频与转写文本；对比话术直指竞品"需关多个开关" |
| 8 | **双击语音键 = 暂停/恢复桥接（第二触发模式）** | 中高 | 低 | 否（不触碰 Capture 内核，仅按键事件层） | 复用 open-voice-bridge 已验证的双击判定，与"按住说话"共存；注意避免双击判定引入长按起始延迟 |
| 9 | **诊断包一键导出与社区化排错**（含环境指纹、不含音频/文字） | 中 | 低 | 否 | 已有导出能力，补齐字段脱敏校验与"导出前预览" |
| 10 | **常用文本/按键片段**（遥控器按键触发预置文本） | 中 | 中 | **是——会引入剪贴板路径**（污染 + UIPI + 竞态） | 若做：必须挂 4 个剪贴板 opt-out 格式、发送前释放修饰键、比对完整性级别、用延迟渲染而非固定 sleep 恢复；**建议先做隐私评估再排期** |
| 11 | **历史记录与检索** | 低 | 中 | **是——与"不保存转写文字"直接冲突** | **建议不做**；把它写成隐私卖点（"我们没有你的历史，因为我们不存"） |
| 12 | **云同步/团队共享词库** | 低 | 高 | 是（上传用户数据） | **建议不做**，与 zh-CN 单机定位及隐私约束不符 |
| 13 | **长录音续接 / 突破 60 秒边界** | 中 | **高** | **是——触碰冻结的录音内核** | 需重开 Capture 内核，风险最高；【推断】用"分段录音 + 提示条"的用户教育替代 |

---

## 7. 主要来源

- Wispr Flow：[特性/定价深潜](https://github.com/moona3k/macparakeet/blob/bddb2dad/docs/research/wisprflow-deep-dive.md?plain=1)、[官方定价文档](https://docs.wisprflow.ai/articles/9559327591-flow-plans-and-what-s-included)、[Snippets](https://docs.wisprflow.ai/articles/5784437944-create-and-use-snippets)、[Dictionary](https://docs.wisprflow.ai/articles/4052411709-teach-flow-your-words-with-the-dictionary)、[Command Mode](https://docs.wisprflow.ai/articles/4816967992-how-to-use-command-mode)、[Context Awareness](https://docs.wisprflow.ai/articles/4678293671-Context-Awareness)
- superwhisper：[Pro 定价](https://superwhisper.com/docs/get-started/sw-pro)、[Windows 支持差异](https://superwhisper.com/docs/get-started/windows)、[自定义模式](https://ai.superwhisper.com/docs/modes/customizing-modes)
- [VoiceInk 架构解析](https://starlog.is/articles/developer-tools/beingpax-voiceink/) ｜ [MacWhisper 定价](https://www.getvoibe.com/resources/macwhisper-pricing/) ｜ [Aqua Voice 定价](https://aquavoice.com/pricing)
- [Willow Voice 实测评测](https://www.getvoibe.com/resources/willow-voice-review/) ｜ [Typeless 定价](https://www.typeless.com/pricing) ｜ [Spokenly vs Typeless](https://spokenly.app/comparison/typeless)
- [Handy (GitHub)](https://github.com/cjpais/Handy) ｜ [Whispering (YC Launch)](https://www.ycombinator.com/launches/OAh-whispering-local-first-open-source-speech-to-text-at-your-fingertips) ｜ [Diction Tones](https://diction.one/features/tone-presets.html)
- [微信 PC 语音输入](https://www.163.com/dy/article/KLL3USVH0511CPVM.html) ｜ [微信单机模式](https://m.mydrivers.com/newsview/981326.html) ｜ [豆包输入法 Windows 版](https://www.chinaz.com/2026/0909/1775971.shtml) ｜ [豆包智能文字整理](https://www.chinaz.com/2026/0827/1773686.shtml) ｜ [豆包隐私双模式实测](https://www.thepaper.cn/newsDetail_forward_32037251)
- [讯飞端侧大模型与方言](http://www.ahwang.cn/hefei/2024/1026/2763844.html) ｜ [搜狗语音官方 FAQ](https://shouji.sogou.com/wap/feedback/faqdetail?id=2000017) ｜ [搜狗 AI 服务总入口](https://www.chinaz.com/2026/0831/1774144.shtml)
- [Windows 输入无障碍功能（语音键入/语音访问/流畅听写）](https://learn.microsoft.com/zh-cn/training/modules/inclusive-software-surface/4-mobility-and-input-accessibility-features) ｜ [Win+H 中文数字无开关](https://learn.microsoft.com/en-us/answers/questions/5626530/voice-typing-cant-input-chinese-numeral-hanzi-and) ｜ [Win11 语音键入设置项](https://mstateit.minnesota.edu/TDClient/271/mstate/KB/Article/27857/Configuring-Windows-11-Voice-Typing-Settings)
- [网易叭哥说](https://news.mydrivers.com/1/1150/1150219.htm) ｜ [win-text-inject（剪贴板注入缺陷调研）](https://github.com/emerson-d-lopes/win-text-inject) ｜ [听写文字落错位置成因](https://voicekeyboardpro.com/blog/dictation-text-appears-wrong-place.html)
- 隐私争议：[Wispr Canto 训练数据调查](https://www.getvoibe.com/resources/wispr-canto-voice-model-training-data/) ｜ [Typeless 评测（云端 vs "on-device" 宣称）](https://www.getvoibe.com/resources/typeless-review/)
- 硬件形态：`ZSTDJan/windows-remote-mic-app`（[GitHub](https://github.com/ZSTDJan/windows-remote-mic-app)） ｜ `nijez/open-voice-bridge`（[GitHub](https://github.com/nijez/open-voice-bridge)，RC003 双模范式） ｜ [讯飞智能语音鼠标（苏宁）](https://m.suning.com/product/0000000000/12409352472.html) ｜ [讯飞 AI 语音鼠标评测](https://www.pingwest.com/w/239838) ｜ [8BitDo Micro 语音输入 Claude Code（Zenn）](https://zenn.dev/ryu1maniwa25/articles/8bitdo-micro-voice-input-claude-code) ｜ [Cheerdots AI 鼠标/简报器](https://www.amazon.com/dp/B0DZ5S2WPC) ｜ [Elgato：脚踏板用于 AI 提示（含 Wispr Pedal / Stream Deck Pedal）](https://www.elgato.com/eu/en/explorer/products/stream-deck/foot-pedals-for-ai-prompting/) ｜ [OM SYSTEM 听写脚踏板](https://dictation.omsystem.com/en/dictation-transcription-accessories/rs31n-usb-foot-pedal-with-4-pedals/) ｜ [Claude Code 语音模式 toggle 诉求 (issue #33025)](https://github.com/anthropics/claude-code/issues/33025)
- 其他 per-app 实现：[LotusQ app-aware formatting 讨论 (HN)](https://news.ycombinator.com/item?id=47236888)、[Spokenly 官网](https://spokenly.app/)
- 本产品基线：[言灵 Vibe Flow README](https://github.com/richlearntodo-debug/vibe-flow)

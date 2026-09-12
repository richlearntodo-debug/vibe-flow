# 言灵 · Vibe Flow Remote V2.0.0 候选版 2

**把遥控器变成桌面上最顺手的输入方式 —— 按住录音键说话，松开，文字就落在你指定的应用里。**

V2.0 把 V1.5 的「遥控器可以当语音键」做成了一台**可管理、可自证、可回退**的桌面工具：六个页面各司其职，文字可以固定送进你指定的应用，按键从"一个动作"扩展到**短按 / 长按 / 双击**，每个结论都有证据可查。

> **本版（候选版 2）相比上一个候选包新增**：**开机键（电源键）可配置**、**语音页显示最近一次收音的实测电平**、快捷键页按实体遥控器排布、六个页面与首次设置向导的文案整体简化。

> 当前状态：**候选版（candidate）**。构建、自检、界面矩阵、安装包逐文件一致性与本机实测均已通过；真机与安装生命周期验收仍在进行，见文末「已知限制」。

## 立即下载

| 文件 | 直接下载 |
| --- | --- |
| **推荐安装版** | [**VibeFlow-Setup.exe**](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/VibeFlow-Setup.exe) |
| 免安装版 | [Vibe-Flow-Windows-x64.zip](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/Vibe-Flow-Windows-x64.zip) |
| 完整性校验 | [SHA256SUMS.txt](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/SHA256SUMS.txt) |

> **普通用户请选择第一行安装版。** GitHub 自动生成的 `Source code (zip/tar.gz)` 是源码，不是 Windows 安装程序。当前构建**未配置商业代码签名**，首次运行可能出现 SmartScreen 提醒，请核对仓库地址与 SHA-256，不要从非项目所有者渠道获取安装包。

---

## ✨ 本次重点更新

V2.0 把 V1.5 的「遥控器可以当语音键」做成了一台**可管理、可自证、可回退**的桌面工具。**六件事**值得你升级：

| # | 重点更新 | 你会感受到什么 |
|---|---|---|
| 1 | **文字固定送进你指定的应用（工作流）** | 添加应用 → 学习它的输入框 → 保存。此后文字不再"跟着焦点乱跑"；每行还写明自身状态（还没学习 / 已学习未验证 / 已验证 / 目标已失效） |
| 2 | **六个页面各司其职** | 首页看状态、工作流管文字去向、快捷键改按键、语音看链路、自检排障、设置调外观与隐私 —— 不用在一个长页面里找 |
| 3 | **三层手势：短按 / 长按 / 双击** | 同一个键最多挂三种动作；没有单独配置的那层会写清**跟随哪一层**，不再让人猜 |
| 4 | **电源键现在可以配置** | 电源键（Windows 上报为 `VK 0xFF` / 扫描码 `E0 5E`）此前被识别却被丢弃；现在可以在快捷键页给它指派短按 / 长按 / 双击 |
| 5 | **首次设置不再卡住** | 新增「稍后再说」：关闭向导但**保留进度**，下次从同一任务继续；硬件没就绪也能先跳过去 |
| 6 | **每个结论都有证据** | 自检每项给结论 + 「详情」展开**正确状态 / 原因（含 `VF-…` 码）/ 下一步**，并提供重新检测与修复入口，而不是一句"失败" |

此外还有：**语音页显示实测收音电平**（低于 10% 会直接告诉你该怎么调）、**界面缩放**（100% / 110% / 125%）、**双击窗口跟随 Windows 双击速度**（不再写死 320ms）、**Store / UWP 应用可添加可启动且有图标**、**术语表**（把 `Profile`、`Smart Profiles`、`Raw Input`、`Live HUD` 等各配一句白话）、以及**六个页面 + 向导的文案整体简化**。

### V2.0 的其他能力

| 能力 | 说明 |
|---|---|
| **Smart Focus** | 学习、测试并验证可编辑的输入目标；验证失败或开始录音时安全停止，**未验证就不发送按键、不粘贴** |
| **Live HUD** | 不抢焦点的悬浮状态窗：主窗口不在前台时也能看到录音状态；有实际动作在跑或正在录音时不会自动消失 |
| **Context Deck** | 从托盘打开的只读详情面板：设备、语音、Profile、目标与最近一次动作 |
| **Capture & Ask** | 截图当前窗口或区域、预览、复制，并在目标验证后粘贴；**不上传、不自动录音、不自动发送** |
| **Browser Remote Lite** | 浏览器遥控的推荐配置：**先看差异**，再由你确认应用，并可一键撤销 |
| **用语片段** | 你自己写的短句，绑到某个键或某一层；逐字符注入，**不经过剪贴板**、不进入剪贴板历史，日志只记字符数 |
| **用量统计（仅元数据）** | 会话数、干净完成数、最长间隔；只读时间戳与成功标记，面板自述统计范围 |

---

## 📸 界面一览

| 首页：一眼看清设备与语音状态 | 工作流：文字固定进哪个应用 |
|---|---|
| ![首页](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/01-overview.png) | ![工作流](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/02-workflow.png) |

| 快捷键：按实物排布，电源键与录音键在最上 | 自检：结论 + 原因 + 下一步 |
|---|---|
| ![快捷键](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/03-shortcuts.png) | ![自检](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/04-diagnostics.png) |

| 设置：外观、后台、隐私与诊断 | 首次设置的 5 项任务 |
|---|---|
| ![设置](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/05-settings.png) | ![首次设置](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/00-setup-01-device.png) |

| 语音：显示最近一次收音的实测电平 | 开机键与录音键就在快捷键页最上一行 |
|---|---|
| ![语音页：收音电平](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/06-voice.png) | ![快捷键页顶格的两个键](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/03-shortcuts-screenshot.png) |

> 截图取自本候选构建（1280 × 840，白天模式），全部指向本候选版的标签 `v2.0.0-candidate.2`，所以发布页展示的就是这一版自己的界面。

---

## 🚀 5 分钟上手

### 1. 下载与安装
下载 `VibeFlow-Setup.exe`，按提示安装。**当前构建未配置商业 Authenticode 签名**，Windows 可能出现"未知发布者"提示 —— 请先用本批 `SHA256SUMS.txt` 核对文件，不要从非项目所有者渠道获取安装包。

### 2. 走完首次设置的 5 项任务
1. **确认设备与用法** —— Windows 10/11、RC003、按住说话；
2. **连接并测试遥控器** —— 配对 `MI RC` / RC003，并按一次方向键证明 Windows 真的收到了遥控器事件（普通键盘无法完成这一步）；
3. **准备本地音频通道** —— 安装或检查 VB-CABLE，确认 `CABLE Input` 与 `CABLE Output`；
4. **选择工具并完成听写** —— 选微信输入法 / Typeless / Windows 语音输入 / 其他，并**真的说一句**；
5. **开机即用** —— 选择后台行为；常用应用与 Smart Profiles 都可以稍后再说。

![首次设置](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/00-setup-01-device.png)

> 走不完也没关系：点「稍后再说」即可关闭向导并保留进度，下次从同一任务继续。

### 3. 语音工具推荐配置（以微信输入法为例）
- 全局快捷键：`Ctrl + Win`
- 触发方式：单击切换
- 麦克风输入：`CABLE Output`

其他工具以客户端实际快捷键为准 —— **言灵与工具两边必须完全一致**。

### 4. 配置按键（快捷键页）
- **三层手势**：每个可配置键都有「短 / 长 / 双」三行，点动作框选择即可；没有单独配置的那层会写明跟随哪一层。
- **电源键**：现在也有自己的卡片，可以指派动作；**未指派时按键交给 Windows**（实测轻触无系统动作），指派后才由言灵拦截并执行。
- **录音键（F5）固定**在稳定语音链路上，不可修改、也不参与分层 —— 这是保证语音链路稳定的硬约束。
- 破坏性操作（新建 / 重命名 / 删除 / 导入 / 导出）都收在「管理」菜单里。

![快捷键](https://github.com/richlearntodo-debug/vibe-flow/raw/v2.0.0-candidate.2/docs/images/03-shortcuts.png)

### 5. 日常听写路径
1. 打开或激活目标应用（例如 ChatGPT）；
2. 在「语音」页选择常用应用并学习它的输入框，或按提示手动点一下输入框；
3. **按住录音键说话，松开结束**；
4. **目视检查文字**，确认无误后手动确认发送。

单段录音约 `60 秒`（设备侧控制）；提前松开立即结束，不会自动创建第二个会话。

---

## 🔒 录音键隔离现状（安装前请先读）

在没有安装**签名的设备过滤器**之前，录音键使用的是"**遥控器在线范围内**"的钩子隔离，而不是逐事件的设备归属隔离：

- 遥控器**已连接**时，F5 会被拦截，**不会**再去触发浏览器刷新或页面动作；
- 遥控器**不在线**时，普通键盘的 F5 **原样直通**（断开后约数秒即恢复正常）；
- 用户态无法做到逐事件设备归属，所以它**不能与签名过滤器等同**；副作用是遥控器在线期间，**实体键盘的 F5 也会一并被拦截**。

安装后在「首页」「快捷键」「自检」「设置」四处都会显示当前状态；首次设置向导第一步也会说明。

**非语音键**（确认 / 电源 / 方向 / Home / 功能键 / TV）的原生效果仍会透传 —— 这是设计使然：如果钩子把事件吞掉，就会同时取消执行动作所需的输入包。

---

## 📦 下载与校验

本批共三个文件，发布前请以 `SHA256SUMS.txt` 逐一核对：

- `VibeFlow-Setup.exe`
- `Vibe-Flow-Windows-x64.zip`
- `SHA256SUMS.txt`

**不要下载 GitHub 自动生成的源码 ZIP。**

---

## 🧊 稳定边界（这些没有变）

- Capture 文件版本继续为 `1.2.1.0`，录音内核 `v1.0.3`；
- **按住**开始、**松开**结束，RC003 单段约 **60 秒**；
- `gain=1.0`、`autoLevel=true`、`speech`、`drainMs=180`；
- 语音工具直接写入已聚焦的输入框；**不读取、不保存普通转写文字**，不上传音频，不自动发送；
- 没有 MIC_EXTEND、没有自动续接、没有自动 Enter、没有网页正文读取；
- **电源键（VK 0xFF / 扫描码 E0 5E）可作为支持按键**；**返回与独立音量键**没有稳定的用户态事件，因此不提供映射。

---

## ⚠️ 已知限制

- **按设备精确隔离需要签名通道**：代码侧协议已就绪，缺的是内核驱动签名与分发；当前使用上文所述的替代机制。
- **返回键 / 独立音量键**：不在 Windows 普通输入栈内，用户态拿不到事件，不提供映射。
- **安装生命周期**：干净账户安装、V1.5 升级、二次升级、卸载仍待验收。
- **高 DPI 观感**：自动矩阵覆盖 880×500 / 1280×840 / 1366×768 / 1920×1080 与三种主题；125% / 150% / 200% 的实际观感仍需真机确认。
- **第三方工具**：微信输入法、Typeless、ChatGPT 桌面端等为第三方产品，行为可能随其版本变化。

---

## 🗺️ 路线图

| 优先级 | 计划 | 状态 |
|---|---|---|
| 高 | **签名设备通道**：把"遥控器在线范围"升级为真正的按设备隔离（键盘 F5 不再被牵连） | 代码协议就绪，待签名与分发 |
| 高 | **安装生命周期验收**：干净账户安装、V1.5 升级、二次升级、卸载 | 待真机 |
| 中 | **高 DPI 复核**：125% / 150% / 200% 的观感与文案排版 | 待真机 |
| 中 | **CI 恢复**：让自动门禁（构建、自检、界面矩阵、包哈希）在每次提交上运行 | 待推送分支 |
| 中 | **发布签名**：为安装包配置 Authenticode 签名，消除"未知发布者"提示 | 待证书 |
| 低 | **返回 / 音量键评估**：仅在用户态出现稳定事件、或引入可选增强组件时才做 | 已评估，暂不做 |

---

## 👥 加入用户社区

加入社群可以获得**配置答疑、设备兼容反馈、版本更新通知和 Vibe Coding 工作流分享**。遇到问题时，先在「自检」页从第一个警告开始处理，并把错误码一起带上。

<p align="center">
  <img src="https://raw.githubusercontent.com/richlearntodo-debug/vibe-flow/v2.0.0-candidate.2/docs/images/vibe-flow-community.png" alt="扫码加入 Vibe Flow 用户社区" width="760">
</p>

---

## 🔗 相关文档

- [完整功能看板](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/FEATURES_ZH.md)
- [使用教程](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/V2_0_USER_GUIDE_ZH.md)
- [快速开始](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/QUICK_START_ZH.md)
- [更新说明](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/V2_0_RELEASE_NOTES_ZH.md) · [V2.0 更新梳理（相对 V1.5）](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/V2_0_UPDATE_SUMMARY_ZH.md)
- [真机测试状态](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/V2_0_HARDWARE_TEST_MATRIX_ZH.md) · [已知限制](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/docs/V2_0_KNOWN_LIMITATIONS_ZH.md)
- [完整变更日志](https://github.com/richlearntodo-debug/vibe-flow/blob/v2.0.0-candidate.2/CHANGELOG.md)

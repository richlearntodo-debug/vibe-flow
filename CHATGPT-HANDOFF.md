# 交接简报 · Vibe Link（给另一个 AI / Agent 直接使用）

> 这份文件是**自包含**的：读完它就能接手本项目并完成发布，不需要读原始会话记录。
> 原始会话记录很大且是压缩格式（`~/.dsh/sessions/.../session.v3.jsonl.zstd`，19.6 MB 压缩），不建议交给模型；
> 人类可读的会话日志是仓库里的 `docs/V2_0_PROGRESS.md`（≈597 KB，逐轮记录 + 实测数据 + 纠正过程）。

## 一、这是什么

Windows 桌面工具「Vibe Link」：把小米蓝牙语音遥控器（RC003 / MI RC）变成语音输入与快捷键控制器。
- 技术栈：**C# 5 + WinForms，无 csproj**，用 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`）直接编译；
- 仓库：`https://github.com/richlearntodo-debug/vibe-flow`
- 分支：`main`（已含全部最新源码与文档）与 `feature/v2-off-key-loop`（同一提交）
- 版本：**V2.0.0 候选版 3** · 标签 **`v2.0.0-candidate.3`**

## 二、当前状态（接手前先确认，不要重复发布）

| 项 | 状态 |
| --- | --- |
| Release `v2.0.0-candidate.2` | 上一版，**已发布**（预发布）→ `https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v2.0.0-candidate.2`，不要再动它 |
| Release `v2.0.0-candidate.3` | **待发布**：正文、截图、标签与哈希都已在仓库里就位（标题 `Vibe Link V2.0.0 候选版 3`），按第四节命令创建即可 |
| 「Latest」标记 | 仍是 **v1.5.0**（候选版是预发布；如要改为正式 Latest 需显式操作） |
| 仓库 `main` | 已更新到最新提交（提交号见 `frozen-parameters.json` 的 `commit`） |
| CI | 第 1–6 步通过（含"从 Secret 还原冻结采集件并校验哈希"）；`Build release` 曾因 VB-CABLE 驱动包缺失失败，现在 `RESTORE_BUILD_DEPS.ps1` 会尽力获取、缺失时发布链不再中断（见第六节） |
| 安装生命周期 | 干净账户安装 / V1.5 升级 / 二次升级 / 卸载 **仍未真机验收** |

## 三、发布一条命令（如需再次发布或用新标签）

```powershell
$env:GH_TOKEN = '<有 contents:write 的 fine-grained token>'
$env:HTTPS_PROXY = 'http://127.0.0.1:7897'    # 本机 gh/git 不读 Windows 系统代理，必须显式设置
gh release create v2.0.0-candidate.3 `
  release/VibeFlow-Setup.exe release/Vibe-Flow-Windows-x64.zip release/SHA256SUMS.txt `
  --title "Vibe Link V2.0.0 候选版 3" `
  --notes-file docs/GITHUB_RELEASE_BODY_ZH.md --prerelease
```

发布后**必须核对四件事**：① 三个附件大小与 `release/` 一致；② 三条下载直链返回 200；
③ 正文中的截图能显示（都指向 `/raw/<tag>/docs/images/...`）；④ `SHA256SUMS.txt` 与 `frozen-parameters.json` 的 `releaseAssets` 一致。

**token 要求**：fine-grained，**Repository access = 仅本仓库**，**Contents: Read and write**（缺它会在 403 响应头里看到
`x-accepted-github-permissions = contents=write`）；如要读 CI 日志再加 **Actions: Read**。
**不要把 token 贴进聊天**：在终端里 `setx GH_TOKEN "..."` 或 `gh auth login --with-token` 即可。

## 四、核心参数已固化（改前必读）

- 人读：`docs/V2_0_FROZEN_PARAMETERS_ZH.md`
- 机器读：`frozen-parameters.json`
- 要点：冻结采集件哈希 `B62DE035…2E683`（**三处**强制校验）；采样链路 `hold` / `gain=1.0` / `autoLevel=true` / `speech` /
  `drainMs=180` / 端点 `CABLE Input` / 单段约 60 秒；正文下限 8.0 pt；界面矩阵 3 主题 × 4 尺寸 = **12 例**；
  发布链要求安装包载荷 **51/51** 逐文件一致。
- **规则**：不要为了"让某个检查通过"而修改这些值或放宽校验。它们是产品的契约。

## 五、构建与验证

```powershell
.\RESTORE_BUILD_DEPS.ps1          # 还原 NAudio 到 tools\（需要网络）
.\BUILD_VIBE_MIC.cmd              # 只重建 Host
.\BUILD_INPUT_BRIDGE.cmd          # 只重建桥
.\BUILD_RELEASE.ps1               # 完整发布链：validate → 自测 → 12 例界面矩阵 → 构建 → 安装包
node scripts/validate.js          # 单独跑源码/文案/契约断言（提交前必须 exit 0）
.\scripts\check-ui-geometry.ps1 -Exe .\VibeMic.exe -ForceSize 1280x900   # 控件重叠/裁切
```

## 六、CI 现状与最后一个阻塞

- 冻结采集件（`VibeMicAtvvCapture.exe`，94.5 KB）被 `.gitignore` 的 `*.exe` 排除 → 检出里没有它。
  现已用**仓库 Secret**（`VIBE_FLOW_CAPTURE_B64_1..4`，base64 分 4 片，因为单个 Secret 上限 48 KB 而它编码后约 126 KB）
  在工作流中还原并校验哈希 ✔。
- **第 11 步仍失败**：打包需要 `tools\VBCABLE_Driver_Pack45.zip`，同样被排除，且**体积超过 Secret 上限**。
  两种处理方式：① 把该驱动包作为**发布附件**交给 runner（工作流下载它）；② 接受"CI 只跑源码与界面测试"。
  **不要为了让 CI 变绿而放宽冻结件或驱动的校验。**

## 七、踩过的坑（照做可省数小时）

1. **代理**：本机桌面代理是 `127.0.0.1:7897`。PowerShell 走系统代理，但 **`git` 与 `gh` 不读系统代理** →
   `git -c http.proxy=http://127.0.0.1:7897 push …`、`$env:HTTPS_PROXY='http://127.0.0.1:7897'`。否则表现为"网络时通时断"。
2. **几何检查看不到文字问题**：它只量控件矩形。本轮实测：语音页新增的一行文案被上方状态带（`Panel(30,64) 900×62`）的底色盖住，
   几何仍报 0 重叠 —— **涉及文案或层叠的改动必须截图目视**。另外 WinForms 中**先加入的控件在 z 序更前**。
3. **多行代码改动必须用编辑工具**，不要用脚本批量替换（曾两次静默删行）。
4. **PowerShell 会把嵌套数组展平**：`@(@(a,b),@(c,d))` 遍历时元素是字符串，导致替换静默失效 —— 结构性改动不要用脚本。
5. **门禁钉的是契约而非文案**：改了文案就要同步更新 `scripts/validate.js` 里对应的字符串断言（本轮被拦下多次，属预期保护）。
6. **`Compress-Archive` 写出的 zip 用反斜杠路径**（跨平台可能解不开）；用 `ZipFile::Open(...,Create)` + `CreateEntry` 手工写入 `/` 路径。
7. **Release 正文里的相对路径会解析到默认分支**（展示旧截图）→ 一律写成指向标签的绝对地址。

## 八、给"想在 ChatGPT 里用"的直接做法

**网页版 ChatGPT 无法操作你的电脑或 GitHub** —— 它只能读你粘贴的内容并给建议。真正能"直接发布"的是能在本机执行命令的 Agent
（ChatGPT Codex CLI / 其他本地 Agent）。因此：

- **要它帮你发布** → 用**本地 Agent**，把仓库路径 `C:\Users\Admin\Documents\ChatGPT\vibe -flow` 交给它，
  让它读本文件 + `HANDOFF-README.md`，并在环境里准备好 `GH_TOKEN` 与 `HTTPS_PROXY`，然后执行第三节那条命令。
- **只想让网页版 ChatGPT 复核** → 把 `HANDOFF-README.md` + `docs/V2_0_FROZEN_PARAMETERS_ZH.md` + 你要发布的那段正文粘贴给它，
  让它核对"发布后四件事"清单。
- **可直接粘贴给 Agent 的起手提示词**：

```
你是本项目（Vibe Link，仓库 C:\Users\Admin\Documents\ChatGPT\vibe -flow）的发布执行者。
第一步：读 CHATGPT-HANDOFF.md 与 HANDOFF-README.md，不要改动 docs/V2_0_FROZEN_PARAMETERS_ZH.md 里的任何固化参数。
第二步：node scripts/validate.js 必须 exit 0；再跑 .\VibeMic.exe --self-test 与 .\VoxDeckInputBridge.exe --self-test。
第三步：确认 GH_TOKEN（contents:write）与 HTTPS_PROXY=http://127.0.0.1:7897 已就绪后，按第三节的命令发布；
        若标签已存在，先确认是否已发布，不要重复创建。
第四步：发布后逐项核对第四节/第三节的四件事，并把结果贴回来。
```

## 九、包内导航

| 需要什么 | 看哪里 |
| --- | --- |
| 交接与发布步骤 | `HANDOFF-README.md`、`PACKAGE-README.txt`、本文件 |
| 固化参数 | `docs/V2_0_FROZEN_PARAMETERS_ZH.md`、`frozen-parameters.json` |
| 发版正文（发布用） | `docs/GITHUB_RELEASE_BODY_ZH.md` |
| 使用教程（含最新截图） | `docs/V2_0_USER_GUIDE_ZH.md`、`QUICK_START_ZH.md` |
| 本次会话全过程 | `docs/V2_0_PROGRESS.md`（首选）、`docs/V2_0_UPDATE_SUMMARY_ZH.md`（V1.5→V2.0 汇总） |
| 构建产物 | `artifacts/VibeFlow-Setup.exe`、`artifacts/Vibe-Flow-Windows-x64.zip`、`artifacts/SHA256SUMS.txt` |
| 完整性校验 | `MANIFEST-SHA256.txt` |

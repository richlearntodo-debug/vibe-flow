# 交接说明 · 发布 V2.0.0 候选版 3

本包是**可发布状态**的完整快照，供**有 GitHub 权限的 Agent** 直接发布。核心参数已固化，**不要为了通过检查而改动它们**
（清单见 `docs/V2_0_FROZEN_PARAMETERS_ZH.md` 与 `frozen-parameters.json`）。

## 一、要发布什么

| 项 | 值 |
| --- | --- |
| 标签 | `v2.0.0-candidate.3`（已存在于仓库；提交见 `frozen-parameters.json` 的 `commit`） |
| 发布标题 | `Vibe Link V2.0.0 候选版 3` |
| 正文文件 | `docs/GITHUB_RELEASE_BODY_ZH.md`（已把图片与文档链接指向本标签的绝对地址，粘贴即显示） |
| 附件 | `artifacts/VibeFlow-Setup.exe`、`artifacts/Vibe-Flow-Windows-x64.zip`、`artifacts/SHA256SUMS.txt` |
| 发布类型 | **预发布（pre-release）** —— 正文写明候选状态与未验证项；普通用户仍被引导到稳定版 V1.5.0 |

## 二、一条命令发布（需要 `contents: write` 的 token）

```powershell
$env:GH_TOKEN = '<有 contents:write 的 token>'
$env:HTTPS_PROXY = 'http://127.0.0.1:7897'   # 本机 gh 不读 Windows 系统代理，必须显式设置
gh release create v2.0.0-candidate.3 
  artifacts/VibeFlow-Setup.exe artifacts/Vibe-Flow-Windows-x64.zip artifacts/SHA256SUMS.txt 
  --title "Vibe Link V2.0.0 候选版 3" 
  --notes-file docs/GITHUB_RELEASE_BODY_ZH.md --prerelease
```

也可以用接口：`POST /repos/richlearntodo-debug/vibe-flow/releases`（`tag_name` / `name` / `body` / `prerelease=true`），
附件走 `uploads.github.com`。

## 三、发布后必须核对的四件事

1. **三个附件大小与 `artifacts/` 一致**（本次基线：EXE 8.27 MB、ZIP 5.39 MB）。
2. **三条下载直链返回 200**：`…/releases/download/v2.0.0-candidate.3/<文件名>`。
3. **正文里的截图能显示**（8 处图片引用都应指向 `/raw/v2.0.0-candidate.3/docs/images/…`）。
4. **`SHA256SUMS.txt` 与 `frozen-parameters.json` 的 `releaseAssets` 三个哈希一致**。

## 四、仓库与分支现状

- 默认分支 `main` 已包含本版本全部源码与文档（本包即该提交的完整快照）。
- **CI 现状**：前 5 步已通过（含"从 Secret 还原冻结采集件并校验哈希"）；
  **第 11 步 `Build release` 仍失败** —— 需要 `tools/VBCABLE_Driver_Pack45.zip`，它被 `.gitignore` 排除且超过 Secret 的 48 KB 上限。
  处理方式二选一：把该驱动包作为**发布附件**交给 runner，或接受"CI 只跑源码与界面测试"。
  **不要为了让 CI 变绿而放宽冻结件与驱动的校验。**

## 五、本包内容

- **源码**：`scripts/`（Host `VibeMic.cs`、桥 `VoxDeckInputBridge.cs`、采集 `VibeMicAtvvCapture.cs`、`features/`、`ui/`、`tests/`）、`installer/`、`driver/`
- **文档与教程**：`docs/`（含 `docs/images/` 全部截图，含本轮新增的语音页收音电平图）、`README.md`、`QUICK_START_ZH.md`、`CHANGELOG.md`
- **固化清单**：`docs/V2_0_FROZEN_PARAMETERS_ZH.md`（人读）、`frozen-parameters.json`（机器读）
- **构建产物**：`artifacts/` 三个文件
- **重建所需**：`VibeMicAtvvCapture.exe`（冻结件，哈希固定）、`RESTORE_BUILD_DEPS.ps1`（还原 NAudio）
- **校验**：`MANIFEST-SHA256.txt`（本包内每个文件的 SHA-256）

## 六、重建步骤

```powershell
.\RESTORE_BUILD_DEPS.ps1        # 还原 NAudio 到 tools\
.\BUILD_VIBE_MIC.cmd            # 只重建 Host
.\BUILD_INPUT_BRIDGE.cmd        # 只重建桥
.\BUILD_RELEASE.ps1             # 完整发布链：校验 → 自测 → 12 例界面矩阵 → 构建 → 安装包
```
需要：.NET Framework 的 `csc.exe`、Inno Setup、以及 `tools/VBCABLE_Driver_Pack45.zip`（生成安装包时）。

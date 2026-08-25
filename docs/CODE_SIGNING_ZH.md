# Windows 代码签名与安全更新

本项目支持可选的 Authenticode 发布签名。普通开发者无需证书也能构建；对外发布的安装包建议使用受信任的 Windows 代码签名证书。

## 发布签名

`BUILD_RELEASE.ps1` 会按以下顺序处理发布文件：

1. 构建 `VibeMic.exe`、`VibeMicAtvvCapture.exe` 和 `VoxDeckInputBridge.exe`。
2. 若已配置证书，逐个签名并使用 `signtool verify /pa /all` 验证。
3. 生成 ZIP 与 Inno Setup 安装包。
4. 签名并验证 `VibeFlow-Setup.exe`。
5. 最后生成 `SHA256SUMS.txt`，因此校验值对应签名后的最终文件。

### 使用证书指纹

```powershell
$env:VIBE_FLOW_SIGN_THUMBPRINT = "证书 SHA1 指纹"
$env:VIBE_FLOW_SIGN_STORE = "user" # 机器证书库使用 machine
.\BUILD_RELEASE.ps1
```

### 使用 PFX

```powershell
$env:VIBE_FLOW_SIGN_PFX = "C:\secure\vibe-flow-signing.pfx"
$env:VIBE_FLOW_SIGN_PFX_PASSWORD = "PFX 密码"
.\BUILD_RELEASE.ps1
```

可通过 `VIBE_FLOW_TIMESTAMP_URL` 更换 RFC 3161 时间戳服务；默认使用 DigiCert。不要把 PFX、密码或证书私钥提交到仓库。

## GitHub Actions

仓库工作流识别以下 Actions Secrets：

- `WINDOWS_SIGNING_PFX_BASE64`：PFX 文件的 Base64 内容。
- `WINDOWS_SIGNING_PFX_PASSWORD`：PFX 密码。

未配置 Secrets 时，CI 会生成可测试的未签名构建并显示明确警告。配置任一签名方式后，签名或验签失败会立即终止发布。

## 应用内安全更新

言灵只检查项目官方 GitHub 的最新正式版。更新流程不会静默运行未知文件：

1. 比较语义版本；GitHub API 限流时自动使用 `releases/latest` 官方重定向。
2. 同时下载 `VibeFlow-Setup.exe` 与 `SHA256SUMS.txt`。
3. 校验安装包 SHA-256，复制或下载异常时立即停止。
4. 校验通过后再次询问用户，确认后才启动安装。
5. 安装时保留现有配置，并在完成后重新打开言灵。

发布者应保证每个正式 Release 都包含安装包和同批生成的校验清单。获得代码签名证书后，还应确认 GitHub Release 中的 EXE 均显示有效的 Authenticode 签名。

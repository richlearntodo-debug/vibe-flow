# V2.0 自动化测试报告

> **范围提示（2026-09-08 起）：** 本报告仅记录当前 Host 的稳定语音、快捷键、工作流和自检门禁。Notes Deck/便签相关记录已经归档，不属于当前测试范围；`docs/v2-notes/` 下的历史报告不能作为当前功能或发布证据。

报告日期：2026-09-05。目标版本：`2.0.0` candidate。

## 已执行并通过

| 范围 | 命令 |
| --- | --- |
| 静态与冻结契约 | `npm test` |
| 开发运行布局 | `BUILD_DEVELOPMENT.ps1`、`Test-DevelopmentBuild.ps1`、`Test-DevelopmentRuntime.ps1` |
| 组件自检 | `VibeMic.exe --self-test`、`VoxDeckInputBridge.exe --self-test`、冻结 Capture `--self-test` |
| V2 功能 | `Test-V2FeatureSuite.ps1`：Smart Focus、录音优先保护、快捷键、配置兼容与 UI 表面 |
| 安装器外围 | `Test-InstallerRequirements.ps1`、`Test-InstallerConfigMigration.ps1`、`Test-ReleaseDependencyPreflight.ps1` |
| 版本与候选包 | `Test-ReleaseIdentity.ps1`、`Test-ReleaseArtifacts.ps1` |
| UI 资源 | `VibeMic.exe --ui-resource-test`，300 次六页切换 |

最终等待式资源报告：USER `128 -> 151`（+23），GDI `42 -> 54`（+12），88,516 ms，进程 exit 0。

## 最终候选制品证据

- `BUILD_RELEASE.ps1`：PASS，Inno Setup 6.7.3 成功生成未签名候选安装器、ZIP、SHA-256 和发布说明。
- 打包目录中的 `VibeFlow.exe`、`VoxDeckInputBridge.exe`、冻结 Capture `--self-test` 均 exit 0。
- `Test-ReleaseIdentity.ps1`：PASS；产品/Host/Bridge/Installer 为 `2.0.0` 候选，Capture 为 `1.2.1.0`。
- `Test-ReleaseArtifacts.ps1`：PASS；根目录、候选目录和 ZIP 内 Host/Bridge/Capture/NAudio 一致，`SHA256SUMS.txt` 恰好匹配安装器和 ZIP。
- 安装器和 ZIP 的最终大小与 SHA-256 以同批生成的 `release/SHA256SUMS.txt` 为准；本报告会被打包进两项制品，因此不在报告内记录会造成自引用失效的制品哈希。
- 根目录、候选目录与 ZIP 内 Host/Bridge 的最终 SHA-256 由 `Test-ReleaseArtifacts.ps1` 逐文件核对；具体值只记录在不进入候选载荷的阶段进度中，避免重新编译后报告失效。

Computer Use 已实际启动打包目录 Host，检查六页导航、快捷键页、语音页、自检页和五任务首次设置。该证据只证明本机 UI 行为，不替代实体遥控器、第三方应用、DPI 或安装生命周期。

自动测试覆盖状态模型、目标验证、录音优先取消、快捷键 ACK、单运行实例、配置快照、五任务向导进度以及配置迁移。

本轮额外回归门禁确认：

- 录音取消先于 reservation 时不执行外部动作；reservation 已成立时录音取消不等待；同一时间只允许一个窗口激活、进程启动或粘贴动作持有准入。
- 重新打开首次设置后，普通进度保存不会提交尚未显式测试的语音工具、快捷键或触发方式。
- 当前按键页只有保存成功且同一次配置产生的 Bridge revision 已确认时才显示“已生效”。
- 安装目录变化时，安装器从旧卸载记录的 `InstallLocation` 读取旧配置根；Host 迁移保持原子、备份和幂等。
- VB-CABLE 下载、校验、安装中和失败状态来自本地状态文件；只有 `installing` 或 `installed` 才建立重启恢复，失败会清理本次临时恢复标记。
- Host 未在协作退出期限内结束时，安装或卸载被阻止；卸载数据选择默认保留并由用户明确确认。

## 冻结身份

- Capture source SHA-256：`736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`
- Capture binary SHA-256：`B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`
- Capture file version：`1.2.1.0`
- 输入时序：`650 / 420 / 80 / 500 / 250 / 350`

## 不属于自动测试结论

自动测试不能证明实体 RC003、蓝牙恢复、VB-CABLE 安装、第三方语音工具、Chrome/Edge 页面响应、Windows 10/11 或完整 DPI 矩阵。实际状态见 [真机测试清单](V2_0_HARDWARE_TEST_MATRIX_ZH.md)。

## 2026-09-09 增量复测

- 当前根目录构建：`BUILD_DEVELOPMENT.ps1`、`npm test`、`Test-V2FeatureSuite.ps1`、Host/Bridge/Capture `--self-test`、`Test-InstallerConfigMigration.ps1`、`Test-InstallerRequirements.ps1`、`Test-ReleaseDependencyPreflight.ps1` 和 `git diff --check` 均通过。
- 原生 WinForms UI：使用当前根目录 Host 生成首页、语音、快捷键、自检、设置截图；未发现文本遮挡或快捷键页丢失。Computer Use 通道不可用，ChatGPT/WeType/RC003 真实操作保持 `NOT_RUN`。
- 因旧 `release\\Vibe-Flow-Windows-x64` 仍有运行中的用户进程，本轮未覆盖该目录；当前代码已在隔离候选目录生成并通过 `Test-ReleaseArtifacts.ps1 -Root`。候选安装器 SHA-256：`31ABFD1EEE2315414BF26E200E6D16DC81258A5014628178EC4F775EE11C225F`；候选 ZIP SHA-256：`171424E085B53B08C4D4EB10F9F13B6DA0B8F456E3E6663F767AD213EF863F77`。
- 文字自动入框仍需真机闭环确认：Vibe Flow 不读取或回填第三方转写剪贴板；若 WeType 在 ChatGPT 目标中继续落到剪贴板，应按“目标焦点/权限级别/工具输出模式”判别，不能以自动化测试替代。

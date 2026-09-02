# RC003 Driver Lab 独立测试手册

本文只面向 Vibe Flow 维护者。普通用户不需要安装本页中的候选驱动。

候选驱动用于解决一个明确的 Windows 限制：低级键盘 Hook 看不到输入来自哪台设备，因此无法在保留实体键盘按键的同时，只拦截 RC003 的同名按键。Vibe Flow 采用“精确设备 KMDF 上层过滤器”，不修改键盘 Class Filter。

## 1. 安全边界

必须使用一台独立 Windows 测试电脑，并同时满足：

- 不作为日常办公电脑，不保存个人数据；
- 已准备系统恢复介质和 BitLocker 恢复密钥；
- 至少连接一把独立 USB 键盘，便于异常时恢复；
- 安装 Visual Studio 2022 C++ Build Tools、Windows 11 SDK 和 WDK；
- GitHub 自托管 Runner 使用自定义标签 `vibe-flow-driver-lab`；
- 系统环境变量设置为 `VIBE_FLOW_DRIVER_LAB=1`；
- GitHub Environment `driver-lab` 配置人工审批。

严禁在日用电脑启用测试签名、关闭安全启动或安装未签名候选驱动。

## 2. 生成候选包

推荐先在 GitHub Actions 手动运行 `RC003 Driver Lab candidate`：

1. 打开仓库的 **Actions**；
2. 选择 **RC003 Driver Lab candidate**；
3. 点击 **Run workflow**；
4. 勾选测试用途确认；
5. 先不勾选 `run_driver_lab`，完成 `windows-2022` 云端只编译；
6. 下载保留 1 天的 `VibeFlow-RC003-CloudCompile-*`，检查编译和清单；
7. 准备好独立测试机后，再勾选 `run_driver_lab`；
8. 通过 `driver-lab` Environment 的人工审批；
9. 下载保留 3 天的 `VibeFlow-RC003-DriverLab-*` Artifact。

云端只编译只能证明 C、INF、InfVerif 和 Inf2Cat 通过，不能证明驱动可以安全安装，也不能替代实体键盘与 RC003 真机验收。

也可以在测试机仓库目录执行：

```powershell
.\driver\rc003-filter\New-DriverCandidate.ps1 `
  -Configuration Release `
  -Platform x64
```

候选目录必须包含：

- `VibeFlowRc003Filter.inf`；
- `VibeFlowRc003Filter.sys`；
- `VibeFlowRc003Filter.cat`；
- `DRIVER_CANDIDATE_MANIFEST.json`；
- `SHA256SUMS.txt`；
- `TEST_ONLY.txt`。

清单中的 `productionInstallApproved` 和 `releaseApproved` 必须都是 `false`。

## 3. 安装前核验

安装前先确认 INF 只匹配以下硬件 ID：

```text
HID\{00001812-0000-1000-8000-00805F9B34FB}_Dev_VID&012717_PID&32B8_REV&00A4
```

并确认：

- `UpperFilters` 使用 `0x00010008` 追加语义；
- INF 不包含键盘 Class Registry 路径；
- `SHA256SUMS.txt` 与候选文件一致；
- 当前 RC003 在设备管理器中无错误；
- 当前实体键盘、屏幕键盘和恢复介质都可用。

保存安装前证据：

```powershell
Get-PnpDevice -Class Keyboard |
  Select-Object Status, FriendlyName, InstanceId |
  Out-File .\keyboard-before.txt

pnputil /enum-drivers /class Keyboard > .\keyboard-drivers-before.txt
```

测试签名只允许在这台隔离电脑按 Microsoft 的驱动测试文档人工启用。最终发布验收必须换用 Microsoft 签名包，并重新开启 Secure Boot 与 Memory Integrity。

## 4. 安装与精确范围核验

在测试机管理员终端中进入候选目录，再执行：

```powershell
pnputil /add-driver .\VibeFlowRc003Filter.inf /install
```

按系统提示重启或重新连接 RC003。安装后必须确认：

1. 只有目标 RC003 键盘设备节点附加了 `VibeFlowRc003Filter`；
2. 键盘 Class Registry 没有新增该过滤器；
3. 其他 USB、蓝牙和笔记本内置键盘完全不受影响；
4. 未启动 Vibe Flow 时，RC003 的所有按键仍原样传给 Windows；
5. Bridge 心跳停止后不超过 2 秒恢复直通。

任何一项不成立，都必须停止测试并卸载候选驱动。

## 5. 必测场景

按以下顺序记录通过、失败、日志和复现步骤：

| 测试 | 通过标准 |
| --- | --- |
| 实体键盘回归 | 与 RC003 同名的方向键、Home、F5 等均保持原功能 |
| RC003 单击 | 每次只执行一份配置动作，原始字符不进入前台应用 |
| RC003 长按 | DOWN 重复不会创建重复动作，UP 只结束一次 |
| 语音键 | 保持冻结的按住说话、松开结束链路 |
| Bridge 崩溃 | 最迟 2 秒后 RC003 恢复直通，系统键盘始终可用 |
| 配置热更新 | 旧 generation 的事件不执行 |
| 蓝牙断开重连 | 过滤器重新附加且不会重复执行动作 |
| 睡眠与唤醒 | 系统、键盘、RC003 和 Bridge 全部恢复 |
| 10,000 事件压力 | 无死锁、蓝屏、失控抑制；丢包计数有据可查 |
| 卸载与升级 | 重启前后所有键盘均可用，旧过滤项无残留 |

应用侧语音验收继续使用 [V1.3 真机验收流程](V1_3_HARDWARE_ACCEPTANCE_ZH.md)，不得调整冻结 Capture 参数。

## 6. 回退

先退出 Vibe Flow 并等待至少 2 秒，确认 RC003 已恢复直通。然后查找候选 INF 的 Published Name：

```powershell
pnputil /enum-drivers /class Keyboard
```

确认名称后执行以下命令，其中 `oemXX.inf` 必须替换为刚才核实的候选项：

```powershell
pnputil /delete-driver oemXX.inf /uninstall
```

不要使用 `/force`。完成后重启并复核：

- RC003 状态为 `CM_PROB_NONE`；
- 目标设备的 `UpperFilters` 不再包含候选服务；
- 键盘 Class Registry 从未出现候选服务；
- 所有实体键盘都能正常输入；
- `pnputil /enum-drivers /class Keyboard` 不再列出候选包。

如果键盘输入异常，停止继续操作，使用预先准备的恢复介质或 Windows 恢复环境处理，不在故障状态下反复安装。

## 7. 发布门禁

Driver Lab 全部通过仍不等于可以发布。还必须完成：

1. Static Driver Verifier、Driver Verifier 和适用的 HLK 测试；
2. Microsoft 驱动签名；
3. Secure Boot 与 Memory Integrity 开启状态下重新验收；
4. 多种实体键盘和至少两台 Windows 11 电脑回归；
5. 安装、升级、卸载和系统恢复演练；
6. 普通安装器只在全部门禁通过后才允许引入驱动。

在此之前，公开 Release、安装器和用户教程必须继续把该能力标记为“开发中”。

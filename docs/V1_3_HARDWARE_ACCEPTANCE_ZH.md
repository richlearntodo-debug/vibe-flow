# Vibe Flow v1.3 真机验收与证据说明

本流程只用于 `hardware-candidate` 候选包。它不会修改用户配置，不会重建录音核心，也不会自动批准发布。

## 固定基线

| 项目 | 固定值 |
| --- | --- |
| Capture 来源 | `v1.2.1` 已验证二进制 |
| Capture SHA-256 | `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683` |
| 录音状态机 | `v11` |
| 交互 | 按住收音，松开结束 |
| 单段边界 | RC003 自然边界，约 60 秒 |
| 增益 | `1.0` |
| 处理 | `speech` |
| 尾音排空 | `180 ms` |
| 播放端 | `CABLE Input` |
| 微信快捷键 | `Ctrl + Win`，`toggle`，等待 `80 ms` |

验收期间不得调整以上参数。开机、返回和独立音量键没有稳定 Windows 事件，不纳入正式能力，也不得用 UI“立即测试”代替实体按键证据。

## 开始采集

在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Measure-HardwareAcceptance.ps1 `
  -Mode Begin `
  -InstallRoot "D:\path\to\candidate" `
  -ExpectedVoiceCycles 100
```

脚本会记录以下基线：

- 四个运行组件的路径、进程数和 SHA-256；
- Input Bridge 健康状态；
- 用户配置与 Bridge 配置 revision；
- 四份运行日志的字节偏移；
- 冻结 Capture 哈希。

它不会复制音频或转译文字。

## 实体遥控器测试

开始采集后，在同一候选版上完成：

1. 录音键连续 100 次按下与松开；每次都应开始、收到真实音频、结束并提交转译。
2. 上、下、左、右、确认、Home、TV、功能键各按一次并松开。
3. Home 短按确认显示桌面。
4. Home 长按约 `650 ms`，确认打开或切换配置的本机 APP。
5. 功能键短按和长按各验证一次。
6. 聚焦 ChatGPT、Codex 和浏览器输入框，确认文字留在原输入框，确认键才发送。

按住期间产生的重复 DOWN 可以存在，但必须被状态机忽略；不得创建第二个语音会话。

## 生成报告

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Measure-HardwareAcceptance.ps1 `
  -Mode Complete `
  -InstallRoot "D:\path\to\candidate"
```

输出位于：

```text
artifacts/hardware-acceptance/<session>/hardware-acceptance-report.json
artifacts/hardware-acceptance/<session>/hardware-acceptance-report.md
```

自动报告检查：

- 每个受支持实体键都有 Raw Input DOWN/UP；
- 录音 DOWN、UP、Stream Start、Stream Stop、Audio Start、Audio Stop 一一对应；
- 每段会话确实包含音频并提交转译；
- Home 长按 APP 动作真实启动或切到前台；
- 配置 revision 未漂移且 Bridge 已确认；
- 仅运行安装目录中的单实例组件；
- Capture 哈希保持冻结；
- 没有 `delivered=False`、无音频、队列丢包、假前台成功或动作失败。

## 自动报告不能替代的门禁

以下项目必须人工记录结果：

- APP 重启后配置保留；
- 蓝牙断开与重连后恢复；
- RC003 休眠与唤醒后恢复；
- Windows 睡眠、唤醒和重启后恢复；
- 从旧版本升级后配置保留；
- Windows `125%`、`150%`、`200%` 缩放下五个页面可用；
- 微信语音输入与至少一个其他语音工具完成真实转译；
- 安装器和二进制完成 Authenticode 签名。

即使自动报告为 `automatic-evidence-pass`，报告中的 `releaseApproved` 仍固定为 `false`。只有自动证据和全部人工门禁都有记录后，才可以更新候选清单并决定是否发布。

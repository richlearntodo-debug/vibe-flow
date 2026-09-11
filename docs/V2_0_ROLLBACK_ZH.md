# V2.0 候选版回滚说明

## 回到 V1.5.0

1. 从系统托盘退出 Vibe Flow，并确认没有 Host、Bridge 或 Capture 进程。
2. 备份当前用户 `UserData` 目录；不要只复制安装目录中的旧版配置。
3. 使用项目官方 V1.5.0 安装包覆盖安装，或完整解压 V1.5.0 ZIP 到独立目录。
4. 首次启动先保持 Smart Profiles 关闭，核对语音工具、快捷键、Profiles、主题、开机启动和托盘设置。
5. 完成一次真实方向键和按住/松开录音验证。

V2 的 `focus-targets.json` 与 `project-spaces.json` 独立于录音配置。回滚不需要删除它们；V1.5 不会执行这些文件。若必须清理，应先备份并由用户明确删除。

## 功能级回退

- 删除或停用 Project Space / Focus Target 不会修改 Capture。
- Browser Remote Lite 可使用“一键撤销”恢复应用前映射快照。
- Smart Profiles 可关闭并手动选择原 Profile。
- HUD 可关闭，Context Deck 不会自动常驻。
- 不要通过改 Capture 哈希、重建 Capture、切换录音模式或加入自动 Enter 来规避问题。

## 失败恢复

配置主文件损坏时程序尝试 `.bak`；未来 schema 会进入只读保护。安装或迁移异常时保留日志、错误码和备份，不重复运行破坏性迁移。安装生命周期验证必须在一次性账户执行。

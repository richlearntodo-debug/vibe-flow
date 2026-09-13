# Vibe Flow Remote / 言灵 2.0.0 candidate

Vibe Flow turns a Xiaomi RC003 / MI RC Bluetooth voice remote into a Windows dictation and shortcut controller.

The stable voice contract remains: **lock a verified editable target, hold Record to speak, release to finish, review the text, then press Center/Enter to send.** The pinned Capture binary uses recording kernel `v1.0.3`, voice profile `v11`, gain `1.0`, `speech` processing, a `180 ms` drain, and an approximately 60-second RC003 segment.

## Highlights

- Open or activate user-selected apps through Quick Entries; arbitrary shell commands are not supported.
- Learn and verify editable UI Automation targets with Smart Focus.
- Inspect device, Profile, target, recording, and action states through a non-activating HUD and read-only Context Deck.
- Preview and paste a user-triggered screenshot only after target validation; recording and sending remain manual.
- Apply or undo the Browser Remote Lite template after reviewing every mapping difference; it is optional and does not change the recording path.
- Record a custom keyboard shortcut by pressing the physical chord instead of typing key names.
- Create, import, export, and manually switch shortcut Profiles.
- Optionally bind Profiles to foreground applications with Smart Profiles; this is off by default.
- Discover running and installed Windows applications with names and icons.
- Use a dedicated Browser Back event and verify real action execution receipts.
- Configure applications, HTTPS URLs, screenshots, editing, system, media, and keyboard actions.
- Complete a five-task first-run setup and ten-item self-check.
- Use Light, Dark, or Follow Windows themes.

V2.0.0 candidate.2 is an unsigned public pre-release and has not completed the full hardware, Windows, DPI, and installer lifecycle matrix. V1.5.0 remains the latest fully verified stable release.

## Downloads

- [V2.0.0 candidate.2 installer](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/VibeFlow-Setup.exe)
- [V2.0.0 candidate.2 portable ZIP](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/Vibe-Flow-Windows-x64.zip)
- [V2.0.0 candidate.2 SHA256SUMS](https://github.com/richlearntodo-debug/vibe-flow/releases/download/v2.0.0-candidate.2/SHA256SUMS.txt)
- [V1.5.0 stable release](https://github.com/richlearntodo-debug/vibe-flow/releases/tag/v1.5.0)

The installer is the recommended choice for ordinary users. Verify the downloaded files with the matching `SHA256SUMS.txt`; GitHub's generated source archive is not an application package.

## Voice and privacy

RC003 audio travels through Bluetooth ATVV to `CABLE Input`; the selected voice tool reads `CABLE Output` and writes to the focused field. Vibe Flow does not read or store transcription text. Some WeChat Input Method versions put their own transcription result on the clipboard; candidate.2 may dispatch one controlled `Ctrl+V` only after the target is verified and a provider completion receipt exists, but this is not guaranteed for every target. Normal operation does not save audio or press Enter automatically.

Supported providers, in the order the Voice page lists them, are WeChat Input Method (default: `Ctrl + Win`, toggle, `80 ms`), NetEase Bage (网易八哥说: Right Alt, toggle), and one custom slot (其他语音工具: Right Shift, toggle). The shortcut and trigger saved in Vibe Link must match the selected tool's own settings. Typeless, Windows Voice Typing, iFlytek Voice Input (讯飞语音输入法), Sogou Input Method (搜狗输入法), Doubao Input Method (豆包输入法) and any other unrecognised legacy value are no longer selectable; they are migrated to WeChat Input Method with the stable defaults and a one-time notice. In trigger-only mode (no VB-CABLE, so no capture process runs) the host can wake NetEase Bage and a custom tool by itself; WeChat Input Method needs its own voice panel and therefore still requires VB-CABLE.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
powershell -ExecutionPolicy Bypass -File .\BUILD_DEVELOPMENT.ps1
npm test
```

The development build assembles Host, Bridge, the exact pinned v1.2.1 Capture binary, and NAudio 2.2.1 into one runnable directory, then runs all three component self-tests. Formal and development builds never rebuild or re-sign Capture.

See [QUICK_START_ZH.md](QUICK_START_ZH.md), the [V2 candidate guide](docs/V2_0_USER_GUIDE_ZH.md), the [V1.5 stable guide](docs/V1_5_USER_GUIDE_ZH.md), and the [version archive](docs/VERSION_ARCHIVE_ZH.md). Notes Deck is not part of the current Host surface; historical Notes files remain only as migration/archive evidence.

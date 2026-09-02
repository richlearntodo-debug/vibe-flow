# Vibe Flow Remote / 言灵 1.5.0

Vibe Flow turns a Xiaomi RC003 / MI RC Bluetooth voice remote into a Windows dictation and shortcut controller.

The stable voice contract remains: **focus an editable field, hold Record to speak, release to finish, review the text, then press Center/Enter to send.** The pinned Capture binary uses recording kernel `v1.0.3`, voice profile `v11`, gain `1.0`, `speech` processing, a `180 ms` drain, and an approximately 60-second RC003 segment.

## Highlights

- Record a custom keyboard shortcut by pressing the physical chord instead of typing key names.
- Create, import, export, and manually switch shortcut Profiles.
- Optionally bind Profiles to foreground applications with Smart Profiles; this is off by default.
- Discover running and installed Windows applications with names and icons.
- Use a dedicated Browser Back event and verify real action execution receipts.
- Configure applications, HTTPS URLs, screenshots, editing, system, media, and keyboard actions.
- Complete a five-task first-run setup and ten-item self-check.
- Use Light, Dark, or Follow Windows themes.

V1.4 is retained only as an incomplete preview archive. V1.5 merges and completes that shortcut workflow.

## Voice and privacy

RC003 audio travels through Bluetooth ATVV to `CABLE Input`; the selected voice tool reads `CABLE Output` and writes directly into the focused field. Vibe Flow does not read transcription text and has no clipboard or synthetic-paste fallback. Normal operation does not save audio.

Supported providers include WeChat Input Method, Typeless, Doubao Input Method, Windows Voice Typing, and tools with a global start/stop hotkey. The default WeChat profile remains `Ctrl + Win`, toggle, `80 ms`.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

Formal builds resolve the exact pinned v1.2.1 Capture binary by SHA-256; they do not rebuild or re-sign it.

See [QUICK_START_ZH.md](QUICK_START_ZH.md), the [V1.5 illustrated guide](docs/V1_5_USER_GUIDE_ZH.md), and the [version archive](docs/VERSION_ARCHIVE_ZH.md).

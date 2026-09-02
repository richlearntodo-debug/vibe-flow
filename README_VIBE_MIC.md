# Vibe Flow Remote / 言灵 1.3.0 Preview

Vibe Flow turns a Xiaomi RC003 / MI RC Bluetooth voice remote into a device-scoped Windows dictation and shortcut controller.

The recording contract is unchanged from the stable v1.2.1 baseline: **focus an editable field, hold Record to speak, release to finish, review the text, then press Center/Enter to send.** The pinned capture binary uses recording kernel `v1.0.3`, voice profile `v11`, gain `1.0`, `speech` processing, a `180 ms` drain, and an approximately 60-second RC003 physical segment.

## What v1.3 adds

- Five persisted first-run tasks for RC003, real key input, VB-CABLE, a real provider dictation, and startup behavior.
- Device-scoped Raw Input action routing: matching keys on a physical keyboard remain unchanged, and diagnostics prove whether a real RC003 action reached the executor.
- Optional exact-device filter support; without a signed filter, shortcuts use a transparent native-passthrough fallback and may retain the remote key's original effect.
- Graphical mapping for Directions, Center, Home, TV, and Function. Power, Back, and independent Volume stay hidden because no stable Windows events were observed.
- Real action results for applications, HTTPS pages, editing, system, media, screenshots, and custom keyboard shortcuts.
- Local-app resolution in three stages: focus a running process, launch a valid EXE, then use a Windows Start AppID.
- General, Vibe Coding, and Media single-action presets.
- Configuration export, import, and latest-backup restore. Imports retain the frozen voice profile.
- Light, Dark, and Follow Windows themes.
- Ten-item self-check and privacy-redacted diagnostic export.

## Voice and privacy

RC003 audio travels through Bluetooth ATVV to `CABLE Input`; the selected local voice tool reads `CABLE Output` and writes directly into the focused field. Vibe Flow does not read transcription text and has no clipboard or synthetic-paste delivery fallback. Normal operation does not save audio. A one-shot diagnostic recording requires explicit confirmation and is limited to 30 seconds.

Supported providers include WeChat Input Method, Typeless, Doubao Input Method, Windows Voice Typing, and tools with a global start/stop hotkey. The default WeChat profile remains `Ctrl + Win`, toggle, `80 ms`.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

Formal and hardware-candidate builds resolve the exact pinned v1.2.1 capture binary by SHA-256; they do not rebuild or re-sign it.

See [QUICK_START_ZH.md](QUICK_START_ZH.md), the [v1.3 illustrated guide](docs/V1_3_USER_GUIDE_ZH.md), and the [hardware acceptance guide](docs/V1_3_PREVIEW_ZH.md).

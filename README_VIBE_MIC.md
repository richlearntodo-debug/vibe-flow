# Vibe Flow Remote / 言灵 1.2.1

Vibe Flow turns a Xiaomi RC003 / MI RC Bluetooth voice remote into a Windows dictation and navigation controller.

The interaction is deliberately fixed: **focus an editable text field, hold Record to speak, release to finish, review the text, then press the center Enter key to send.**

## User-friendly stable release

- Windows 10/11 x64.
- RC003 Bluetooth ATVV audio routed through VB-CABLE.
- WeChat Input Method, Typeless, Doubao Input Method, Windows Voice Typing, or another global-hotkey provider.
- Provider-direct text delivery to the focused field.
- No application clipboard or synthetic-paste delivery path.
- One recording session at a time.
- Approximately 60 seconds per stable RC003 physical segment.

The default WeChat profile is the first formal-release baseline: `Ctrl + Win`, toggle trigger, and an `80 ms` startup delay. Audio remains locked to gain `1.0`, `speech` processing, `180 ms` drain, and automatic reversible `CABLE Output` capture routing.

The capture binary uses the exact `v1.0.3` recording kernel with only V1.2.1 heartbeat, cue, and file-version compatibility hooks. The later long-dictation, MIC_EXTEND, forced release-close, and segment-continuation paths are not present.

## Verified remote controls

| Control | Behavior |
| --- | --- |
| Record | Hold to capture; release to finish |
| Function | Short Copy; long Paste |
| Direction pad | Native arrows by default; one verified action per direction, with a one-click `Win+Shift+S` capture option |
| Center | Enter |
| Home | Show Desktop |
| TV | Open persistent Windows Task View; arrows navigate; Enter confirms |

Power, Back, and independent Volume controls are intentionally not mapped because the tested remote does not expose stable Windows reports for them.

## Recording safety

- The input bridge deduplicates physical DOWN and UP edges.
- The RC003 natural ATVV stream-start and stream-stop controls own the recording lifetime.
- Only one stream generation can be active; duplicate starts and stale stops are ignored.
- The selected provider writes directly to the current Windows focus, so focus the target field before recording.
- The application has no clipboard or synthetic-paste delivery fallback.
- Disconnect, sleep recovery, and process shutdown release the previous session and audio route.

## First run

The application provides an eleven-step setup for Bluetooth, pairing, key verification, microphone service, VB-CABLE, provider selection, real dictation, four direction mappings, startup consent, and a final summary. A ten-item self-check provides direct repair links and privacy-safe diagnostics, and validates the actual stable v1.0.3/v11 runtime without requiring removed long-dictation markers. Light and Dark buttons switch the complete interface immediately.

See [QUICK_START_ZH.md](QUICK_START_ZH.md), the [V1.2.1 illustrated tutorial](docs/V1_2_1_TUTORIAL_ZH.md), and the [immutable version download archive](docs/VERSION_ARCHIVE_ZH.md).

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\RESTORE_BUILD_DEPS.ps1
cmd /c BUILD_INPUT_BRIDGE.cmd
cmd /c BUILD_VIBE_MIC_CAPTURE.cmd
cmd /c BUILD_VIBE_MIC.cmd
npm test
```

Version 1.2.1 release assets must pass 100 physical hold/release cycles, the approximately 60-second boundary, focused provider-direct delivery, screenshot invocation, Task View navigation, reconnect, sleep/wake recovery, and fixed-version download verification.

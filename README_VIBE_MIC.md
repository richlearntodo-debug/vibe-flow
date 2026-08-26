# Vibe Flow Remote / 言灵

Vibe Flow Remote turns the microphone in a Xiaomi RC003 / MI RC Bluetooth remote into a Windows voice-input source. It routes remote audio through VB-CABLE to WeChat Input Method, Typeless, Windows Voice Typing, Voquill, or another global-hotkey dictation client. The selected client owns speech-to-text and text structuring.

## v1.2.0 user flow

1. Select a transcription client.
2. Install and verify both VB-CABLE endpoints.
3. Pair RC003 and wait for the voice bridge.
4. Match and test the provider hotkey and trigger mode.
5. Focus a text field, click Record once to start, and click it again to finish.

Continuous dictation is the recommended new-install mode. Releasing the first physical key does not end the logical session: Vibe Flow opens a host-owned ATVV audio stream, renews its exact session every eight seconds, and stops it on the next Record press. The release has passed 124-second, six-minute, and 15-minute hardware regressions with zero queue drops. A 30-minute software guard prevents forgotten sessions from running indefinitely.

Hold-to-talk remains available as a compatibility mode. It ends on physical release and never opens a continuation stream. RC003 firmware forces a key-up and ATVV stop after roughly 60 seconds of one physical hold, so longer input should use continuous mode.

Vibe Flow records the exact Windows UI Automation text control before opening the WeChat voice panel, then verifies that it remains focused without activating windows or calling UI Automation `SetFocus`. The default WeChat AI profile taps `Ctrl+Win+Shift` after the virtual microphone is routed and again after final audio drain. Toolbar activation is prohibited. The WeChat path does not monitor or modify the clipboard and never sends a synthetic paste; WeChat writes directly into the original editor.

Recording start stays silent with visual feedback. Stop uses the proven short two-note completion cue; failures retain an alert. Physical and host ATVV transitions do not replay the stop cue.

VB-CABLE is the only required extra local driver and is not bundled. Vibe Flow writes audio to `CABLE Input`, temporarily routes the Windows default capture roles to the corresponding `CABLE Output`, then restores the previous endpoints after drain.

## Default buttons

| Remote button | Default action |
| --- | --- |
| Record | Click to start; click again to finish |
| OK | Enter / confirm |
| Direction pad | Native arrows; hold Up/Down for system volume |
| Home | `Win + D` |
| TV | Open task switcher; Left/Right select, OK confirms |
| Menu | Open or focus ChatGPT; configurable for other clients and shortcuts |

Configurable actions include common edit commands, save, select all, command palette, quick file open, new terminal, delete line, run/debug, tab navigation, and installed Agent/development clients. Duplicate assignments remain valid but produce a visible warning.

The independent Back and Volume +/- buttons expose no stable Keyboard, Raw Input, or Consumer HID event on the validated RC003 Windows stack. Vibe Flow intentionally does not claim them or unsupported multi-key combinations.

## Stable voice baseline

- Voice state machine profile v11 and continuous state machine v2.
- Gain `1.0`, speech processing, and `180 ms` drain.
- WeChat `Ctrl + Win + Shift`, AI toggle trigger, `180 ms` startup profile.
- Automatic reversible `CABLE Output` routing.
- Exact-session `MIC_EXTEND` every eight seconds for the host-owned stream.
- 30-minute continuous safety guard.
- Natural release wait `260 ms` and bounded fallback for compatibility hold mode.

## Privacy and diagnostics

- Normal dictation audio is decoded and forwarded locally without being saved by Vibe Flow.
- Vibe Flow does not inspect recognized text or provide its own cloud transcription.
- Normal logs contain timing, level, coverage, queue, route, BLE, and bounded memory metadata, not audio payloads or recognized text.
- A seven-part self-check covers components, VB-CABLE, stable-profile drift, bridge services, RC003/ATVV, provider configuration, and the latest end-to-end session.
- One-shot WAV diagnostics require explicit confirmation, capture only the next session for up to 30 seconds, and remain local.

## Build

```bat
BUILD_INPUT_BRIDGE.cmd
BUILD_VIBE_MIC_CAPTURE.cmd
BUILD_VIBE_MIC.cmd
npm test
```

The release outputs `VibeFlow.exe`, `VibeMicAtvvCapture.exe`, and `VoxDeckInputBridge.exe`. Build the installer and portable package with:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_RELEASE.ps1
```

The in-app updater decodes official GitHub Release metadata as UTF-8, falls back to the official release redirect, verifies `VibeFlow-Setup.exe` against `SHA256SUMS.txt`, and requires user confirmation before installation. Release builds optionally sign and verify all first-party EXEs and the installer when a certificate thumbprint or PFX is configured.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/CONTINUOUS_DICTATION_ZH.md](docs/CONTINUOUS_DICTATION_ZH.md), [docs/CODE_SIGNING_ZH.md](docs/CODE_SIGNING_ZH.md), and [docs/USER_GUIDE_ZH.md](docs/USER_GUIDE_ZH.md).

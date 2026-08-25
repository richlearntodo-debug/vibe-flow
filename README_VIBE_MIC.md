# Vibe Flow Remote / 言灵

Vibe Flow Remote turns the microphone in a Xiaomi RC003 / MI RC Bluetooth remote into a Windows voice-input source. It routes remote audio through VB-CABLE to WeChat Input Method, Typeless, Windows Voice Typing, Voquill, or another global-hotkey dictation client. The selected client owns speech-to-text and text structuring.

## v1.1.0 user flow

1. Select a transcription client.
2. Install and verify both VB-CABLE endpoints.
3. Pair RC003 and wait for the voice bridge.
4. Match and test the provider hotkey and trigger mode.
5. Focus a text field, hold Record while speaking, then release it to finish.

Hold-to-talk is the default stable mode. A dedicated release event ends exactly one provider session and never reopens the microphone after release. RC003 firmware forces a key-up and ATVV stop after roughly 60 seconds of one physical hold, so stable hold sessions use that hardware boundary. Short-press continuous dictation remains experimental and non-default.

Vibe Flow records the exact Windows UI Automation text control before opening the WeChat voice panel, then verifies that it remains focused without activating windows or calling UI Automation `SetFocus`. The default WeChat AI profile taps `Ctrl+Win+Shift` after the virtual microphone is routed and again after the final audio drain. Toolbar activation is prohibited. The WeChat path does not monitor or modify the clipboard and never sends a synthetic paste; WeChat writes directly into the original editor.

Recording start stays silent with visual feedback. Stop uses the proven short two-note completion cue; failures retain an alert. Physical RC003 segment renewal does not replay the stop cue.

VB-CABLE is the only required extra local driver and is not bundled. Vibe Flow writes audio to `CABLE Input`, temporarily routes the Windows default capture roles to the corresponding `CABLE Output`, then restores the previous endpoints after drain.

## Default buttons

| Remote button | Default action |
| --- | --- |
| Record | Hold to speak; release to finish |
| OK | Enter / confirm |
| Direction pad | Native arrows; hold Up/Down for system volume |
| Home | `Win + D` |
| TV | Open task switcher; Left/Right select, OK confirms |
| Menu | Open or focus ChatGPT; configurable for other clients and shortcuts |

Configurable actions include common edit commands, save, select all, command palette, quick file open, new terminal, delete line, run/debug, tab navigation, and installed Agent/development clients. Duplicate assignments remain valid but produce a visible warning.

The independent Back and Volume +/- buttons expose no stable Keyboard, Raw Input, or Consumer HID event on the validated RC003 Windows stack. Vibe Flow intentionally does not claim them or unsupported multi-key combinations.

## Stable voice baseline

- Voice state machine profile v11.
- Gain `1.0`.
- Speech processing enabled.
- Drain `180 ms`.
- WeChat `Ctrl + Win + Shift`, AI toggle trigger, `180 ms` startup profile.
- Automatic reversible `CABLE Output` routing.
- Hold-to-talk enabled for new installations and schema 18 upgrades.
- Natural release wait `260 ms`; bounded close fallback `700 ms`.
- Approximately 60-second RC003 physical-hold firmware boundary.

The v1.1.0 UI and documentation release does not change the validated capture, BLE retry, packet ordering, WASAPI clock, or endpoint recovery behavior.

## Privacy and diagnostics

- Normal dictation audio is decoded and forwarded locally without being saved by Vibe Flow.
- Vibe Flow does not inspect recognized text or provide its own cloud transcription.
- Normal logs contain timing, level, queue, route, and BLE metadata, not audio payloads or recognized text.
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

The in-app updater reads the official latest GitHub Release, falls back from API rate limits to the official release redirect, verifies `VibeFlow-Setup.exe` against `SHA256SUMS.txt`, and requires user confirmation before installation. Release builds optionally sign and verify all first-party EXEs and the installer when a certificate thumbprint or PFX is configured.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/CONTINUOUS_DICTATION_ZH.md](docs/CONTINUOUS_DICTATION_ZH.md), [docs/CODE_SIGNING_ZH.md](docs/CODE_SIGNING_ZH.md), and [docs/USER_GUIDE_ZH.md](docs/USER_GUIDE_ZH.md).

# Vibe Flow Remote / 言灵

Vibe Flow Remote turns the microphone in a Xiaomi RC003 / MI RC Bluetooth remote into a Windows voice-input source. It sends remote audio through VB-CABLE to WeChat Input Method, Typeless, Windows Voice Typing, Voquill, or a configurable hotkey-driven dictation client. The selected client continues to own speech-to-text and text structuring. Its Chinese product name is 言灵.

## User flow

1. Select the transcription client used every day.
2. Install and verify both VB-CABLE endpoints.
3. Pair `MI RC` or `RC003` and wait for the live voice bridge.
4. Match and test the provider shortcut and trigger mode.
5. Complete one real remote dictation in the setup window, then focus any text field and hold the record button to speak.

VB-CABLE is the only required extra local driver. It is not bundled; the setup
window opens the official download only when `CABLE Input` or `CABLE Output` is
missing.

Vibe Flow writes decoded remote audio to the playback endpoint named `CABLE Input`. Before dictation it temporarily makes the corresponding `CABLE Output` recording endpoint the Windows default for all capture roles, then restores the user's original endpoints after the audio drains. Manual client configuration is needed only when automatic routing is disabled.

## Default buttons

| Remote button | Default action |
| --- | --- |
| Record | Hold to capture remote audio; release to transcribe through the selected client |
| OK | Enter / confirm |
| Direction pad | Native arrows; hold Up/Down for system volume |
| Home | `Win + D` |
| TV | Open task switcher; Left/Right select, OK confirms |
| Menu | Open or focus the selected Agent/development client, configurable in the app |

Only distinctive RC003 keys are remapped by default. Vibe Flow uses a Windows low-level keyboard hook and device-scoped Raw Input without installing a driver. Short direction presses remain native; only RC003-scoped repeated Up/Down events activate the volume fallback. On the validated RC003 Windows stack, the independent Back and Volume +/- buttons expose no Keyboard, Raw Input, or Consumer HID event. Vibe Flow intentionally exposes only hardware-validated single-tap and long-press controls; Menu is a single-tap shortcut, not a combo leader.

## Privacy and safety

- Audio is decoded locally and routed directly to the selected Windows audio endpoint.
- Vibe Flow does not perform cloud transcription, inspect the resulting text, or upload audio.
- Normal diagnostic logs contain only BLE event metadata and aggregate timing/level metrics, never audio payloads or recognized text.
- A shareable health summary translates trigger latency, signal level, BLE gaps, queue drops, drain timing, and endpoint restoration into a user-facing conclusion.
- A seven-part local self-check validates packaged components, both VB-CABLE endpoints, v11 stable-profile drift, bridge services, RC003/ATVV readiness, the provider shortcut, and the latest end-to-end session. Failed checks link directly to the relevant repair action.
- Audio is saved only when the user explicitly confirms **Capture next audio segment**. That one-shot diagnostic writes the next session's decoded, processed, and `CABLE Output` WAV files locally for comparison, caps them at 30 seconds, and then disables itself. The files can be deleted from `remote-voice-session`.
- The app runs without administrator rights and does not inject code into WeChat or other processes. The separate VB-CABLE installer requests administrator approval for its virtual-audio driver.
- Automatic microphone routing records the original Windows Console, Multimedia, and Communications capture endpoints in a local recovery marker. It restores them after every session and retries restoration on the next start after an unexpected exit.
- Startup registration uses the current user's `HKCU` Run key and can be disabled in Settings.

## Build

Requirements: Windows 10/11, .NET Framework 4.x compiler, Node.js for repository validation, and the Windows SDK metadata already referenced by the build script.

```bat
BUILD_INPUT_BRIDGE.cmd
BUILD_VIBE_MIC_CAPTURE.cmd
BUILD_VIBE_MIC.cmd
npm test
```

The development build outputs are `VibeMic.exe`, `VibeMicAtvvCapture.exe`, and `VoxDeckInputBridge.exe`. The public release packages the main app as `VibeFlow.exe`; all three components run without a console window.

## Architecture

```text
RC003 ATVV BLE notifications
  -> VibeMicAtvvCapture (120-byte framing and ADPCM decode)
  -> one generation-aware provider session coordinator
  -> ordered BLE packet worker + robust quiet-speech leveling or transparent mode
  -> bounded live PCM output after the selected client is ready
  -> event-driven WASAPI virtual microphone output (48 kHz stereo, 20 ms blocks)
  -> CABLE Input / CABLE Output
  -> selected transcription client
  -> focused editor
```

The ATVV protocol behavior was validated against real RC003 hardware and informed by the open-source `HD838A/remote-mic-app` implementation. See that project's license before reusing its source directly. VB-CABLE is third-party software and is not bundled; its own license applies.

## Status

Version 1.0.3 is the current stable Windows release. It fixes the Windows-login race between the input bridge, RC003 ATVV readiness, and the configured transcription client. Early recording requests are recovered only while the physical key remains held, and the selected local provider is warmed in the background. It also adds a five-step onboarding flow, actionable seven-part self-check, persistent recovery diagnostics, and a redesigned RC003 overview and shortcut workspace. The v11 audio pipeline remains unchanged. Real RC003 audio capture, ADPCM decoding, VB-CABLE timing, endpoint recovery, and the WeChat and Typeless paths have been validated on physical hardware. Windows Voice Typing and generic hotkey clients use the documented system path. Voquill is implemented from its current open-source Windows default shortcut and still requires validation with the user's installed client version.

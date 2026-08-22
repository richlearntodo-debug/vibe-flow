# Vibe Flow / 言灵

Vibe Flow turns the microphone in a Xiaomi RC003 / MI RC Bluetooth remote into a Windows voice-input source. It sends remote audio to WeChat Input Method through VB-CABLE, while WeChat continues to handle speech-to-text and text structuring. Its Chinese product name is 言灵.

## User flow

1. Pair `MI RC` or `RC003` in Windows Bluetooth settings.
2. Install [VB-CABLE](https://vb-audio.com/Cable/) from its official site.
3. In Windows or WeChat Input Method, select `CABLE Output` as the microphone.
4. Complete the three-step Vibe Flow setup and focus any text field.
5. Hold the remote record button and speak. Release it to finish.

Vibe Flow writes the decoded remote audio to the playback endpoint named `CABLE Input`. The corresponding recording endpoint visible to WeChat is named `CABLE Output`.

## Default buttons

| Remote button | Default action |
| --- | --- |
| Record | Remote microphone + hold `Ctrl + Win` |
| OK | Enter / confirm |
| Direction pad | Native arrows; hold Up/Down for system volume |
| Home | `Win + D` |
| TV | Open task switcher; Left/Right select, OK confirms |
| Menu | Single tap for `Ctrl + Shift + P`, configurable in the app |

Only distinctive RC003 keys are remapped by default. Vibe Flow uses a Windows low-level keyboard hook and device-scoped Raw Input without installing a driver. Short direction presses remain native; only RC003-scoped repeated Up/Down events activate the volume fallback. On the validated RC003 Windows stack, the independent Back and Volume +/- buttons expose no Keyboard, Raw Input, or Consumer HID event. Vibe Flow intentionally exposes only hardware-validated single-tap and long-press controls; Menu is a single-tap shortcut, not a combo leader.

## Privacy and safety

- Audio is decoded locally and routed directly to the selected Windows audio endpoint.
- Vibe Flow does not perform cloud transcription, inspect the resulting text, or upload audio.
- Diagnostic event logs contain BLE packet metadata and payload bytes. They can be cleared from `remote-voice-session`.
- The app runs without administrator rights and does not inject code into WeChat or other processes.
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
  -> VibeMicAtvvCapture (ADPCM decode, 16 kHz mono)
  -> WinMM resample/output (48 kHz stereo)
  -> CABLE Input / CABLE Output
  -> WeChat Input Method voice typing
  -> focused editor
```

The ATVV protocol behavior was validated against real RC003 hardware and informed by the open-source `HD838A/remote-mic-app` implementation. See that project's license before reusing its source directly. VB-CABLE is third-party software and is not bundled; its own license applies.

## Status

This is an alpha Windows build. Real RC003 audio capture and ADPCM decoding have been hardware validated. The final WeChat text-insertion flow still depends on the user's installed WeChat Input Method version and one-time `CABLE Output` microphone selection.

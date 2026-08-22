# Vibe Flow Architecture

## Runtime components

```text
VibeFlow.exe
  -> manages settings, onboarding, diagnostics and process lifecycle
  -> starts VibeMicAtvvCapture.exe
  -> starts VoxDeckInputBridge.exe

RC003 BLE ATVV notifications
  -> IMA ADPCM decode at 16 kHz mono
  -> WinMM output at 48 kHz stereo
  -> CABLE Input / CABLE Output
  -> WeChat Input Method

RC003 keyboard events
  -> low-level keyboard hook for distinctive keys
  -> device-scoped Raw Input for direction hold detection
  -> SendInput for configured shortcuts
```

## Safety boundaries

- No administrator permission or kernel driver is required.
- The app does not inject code into WeChat or editors.
- Generated configuration keeps unsupported Back and Volume +/- mappings disabled.
- Ordinary direction keys pass through; only RC003-scoped repeated Up/Down events activate volume control.
- Runtime logs do not intentionally write Bluetooth MAC addresses or complete HID paths.

## Configuration

- `vibe-mic-config.json`: application settings and user-visible mappings.
- `voxdeck-shortcuts.json`: generated input bridge mappings.
- `remote-voice-session/`: local runtime diagnostics, ignored by Git.

Public releases start from `vibe-mic-config.default.json`; local configuration and logs are never packaged.

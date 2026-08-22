# Contributing

Keep changes focused and test on Windows 10 or 11. Do not add driver installation, process injection, automatic default-audio endpoint mutation through undocumented APIs, or bundled third-party binaries without a documented security and licensing review.

Before submitting a change:

```bat
powershell -ExecutionPolicy Bypass -File RESTORE_BUILD_DEPS.ps1
BUILD_INPUT_BRIDGE.cmd
BUILD_VIBE_MIC_CAPTURE.cmd
BUILD_VIBE_MIC.cmd
npm test
```

For hardware changes, include the remote model, Windows version, Bluetooth name, ATVV event sequence, and whether the test used the remote microphone or the PC microphone. State clearly which behaviors were verified on real hardware.

Never attach recordings, typed text, Bluetooth addresses, complete HID paths, or diagnostics containing private speech. Redact local paths and identifiers before opening an issue.

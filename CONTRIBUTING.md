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

Use the structured Bug or Feature Issue Form whenever possible. Blank issues remain enabled so GitHub users are not blocked when a form does not fit. Hardware compatibility reports should follow `docs/COMPATIBILITY_MATRIX_ZH.md`; release candidates must follow `docs/RELEASE_QUALITY_GATE_ZH.md`.

To refresh the six documentation screenshots, start a local Vibe Flow build and
run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\capture-ui-screenshots.ps1
```

The script opens each real WinForms page and the setup dialog, then writes PNGs
to `docs/images`. Review every image for clipping, overlap, stale diagnostics,
or private data before committing it.

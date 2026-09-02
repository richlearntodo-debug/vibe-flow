# 言灵 · Vibe Flow Remote 1.4.0 Preview

Preview date: 2026-09-02

## Release identity

- Product version: `1.4.0-preview`
- Windows host/bridge file version: `1.4.0.0`
- Stable capture file version: `1.2.1.0`
- Configuration schema: `31`
- Bridge configuration schema: `6`
- Onboarding version: `9`
- Stable voice profile: `v11`
- Recording kernel: `v1.0.3`
- Voice mode: `hold`
- Onboarding tasks: `5`
- Active self-check items: `10`

## Locked voice baseline

- Gain: `1.0`
- Processing: `speech`
- Drain: `180 ms`
- Playback endpoint: `CABLE Input`
- Provider microphone endpoint: `CABLE Output`
- Automatic reversible routing: enabled
- WeChat profile: `Ctrl + Win`, toggle, `80 ms`
- Stable RC003 segment limit: approximately `60 seconds`

## Recording contract

- The RC003 natural ATVV stream-start begins one stream generation.
- Duplicate starts while that generation is active are ignored.
- Release normally produces the RC003 stream-stop event and finalizes that generation once.
- Stale stop events cannot finalize a newer generation.
- The capture binary contains no `LONG DICTATION`, `MIC_EXTEND`, forced release-close, or continuation path.
- The target text field must be focused before recording so the selected provider can deliver into it.
- Text delivery belongs to the selected provider; the app has no clipboard/paste fallback.
- Disconnect, sleep recovery, or process exit releases the old generation and audio route.

## Remote contract

- Record: hold to capture, release to finish.
- Function: short copy, long paste by default; both actions are configurable.
- Directions: native arrows by default; each can open an app or URL, run a system/media action, or send a custom shortcut.
- Screenshot: any configurable key can invoke Windows `Win + Shift + S`.
- Center: Enter by default and configurable.
- Home: short press is Win+D by default; short and long press are independently configurable.
- TV: Win+Tab Task View by default and configurable.
- Browser AI maps physical Left to Windows' dedicated Browser Back key instead of injecting Alt+Left while Left is still held.
- The application picker merges running windows, Windows AppsFolder, Start Menu shortcuts, and valid installed-app registry entries; product names and application icons are shown when Windows exposes them.
- Existing-app actions report success only after the target window is confirmed as the Windows foreground window.
- A legacy Power APP/URL action migrates to an unused Home long press; Power itself is absent from runtime mappings.
- Power, Back, and independent Volume controls are intentionally unavailable because this RC003/Windows combination has not produced stable input events for them.
- Non-voice actions are resolved only from device-scoped RC003 Raw Input or the optional exact-device filter. The low-level Hook never executes or suppresses a non-voice candidate in the user-mode fallback, so matching physical-keyboard keys remain native.
- Real hardware proved that suppressing a key in the device-blind Hook can prevent the matching Raw Input packet from being delivered. Historical `compatibility` settings still migrate to `strict`, but runtime health now reports the actual isolation level: `native_passthrough` or `exact_device`.
- The Microsoft-signed RC003 filter remains the only planned path for suppressing the remote's original key effect without touching an ordinary keyboard. Its absence is an optional-enhancement state, not a failed action route.
- The in-app test button reports the bridge's real success or failure result for app, URL, screenshot, system, media, and custom-shortcut actions.

## UI and diagnostics contract

- Light, Dark, and Follow Windows apply immediately without restarting capture or input services.
- The host is Per-Monitor V2 DPI aware, uses a 96-DPI layout baseline, and clamps oversized windows to the active working area.
- Configuration can be exported, imported, or restored from the latest atomic backup. Imports preserve the frozen voice profile.
- Diagnostics redact user paths, device identities, addresses, URLs, and application targets.
- Core-component self-check validates `recording_kernel=v1.0.3` and `voice_state_machine=v11`.
- Removed long-dictation markers are never required for a healthy result.

## Release gate

This preview must not replace the locally frozen V1.3 baseline or public `v1.2.1` release until it passes the existing voice gate plus Profile switching, real action receipts, RC003-only mapping, physical-keyboard conflict, app/URL launch, reconnect, sleep/wake, configuration persistence, and high-DPI checks in `docs/V1_4_PREVIEW_ZH.md`.

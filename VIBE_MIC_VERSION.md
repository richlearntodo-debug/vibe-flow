# 言灵 · Vibe Flow Remote 1.1.0

Release date: 2026-08-26

## Release identity

- Product version: `1.1.0`
- Configuration schema: `19`
- Onboarding version: `5`
- Stable voice profile: `v11`
- New-install voice mode: `hold`

## Voice path

```text
RC003 ATVV
  -> ordered 16 kHz ADPCM decode
  -> robust speech leveling
  -> event-driven 48 kHz stereo WASAPI
  -> CABLE Input / CABLE Output
  -> selected transcription client
```

## Validated baseline

- Gain: `1.0`
- Processing: `speech`
- Drain: `180 ms`
- WeChat provider startup: `180 ms`
- WeChat trigger: `Ctrl + Win + Shift`, AI toggle
- Automatic reversible `CABLE Output` routing: enabled
- Hold-release natural-stop window: `260 ms`
- Hold-release bounded close fallback: `700 ms`
- RC003 physical hold boundary: approximately 60 seconds

## v1.1.0 changes

- Hold Record to start and release it to finish.
- Emit a dedicated release event and never reopen the microphone after a hold-mode release.
- Prefer the natural RC003 stop, then use one generation-safe bounded close fallback.
- Keep short-press continuous dictation as an experimental, non-default mode.
- Display logical total duration and segment count in the UI.
- Add high-frequency coding shortcuts and duplicate-assignment feedback.
- Retain the latest editable UI Automation target, also inspect the editor under
  the pointer, and reject non-text page containers as delivery targets.
- Use the validated WeChat AI profile, tapping `Ctrl+Win+Shift` after routing is
  ready and again after final audio drain; stop the provider before restoring the
  original microphone, and never use toolbar or clipboard delivery.
- Keep recording start silent with visual feedback and play the proven short
  two-note cue only when recording stops. Cue playback is driven by named state
  events rather than runtime-log polling.
- Add verified in-app updates and an optional Authenticode release-signing pipeline.
- Refresh onboarding, release docs, screenshots, and troubleshooting guidance.

The capture timing, BLE retry policy, audio processing, endpoint routing, and validated single-key defaults remain unchanged from the stable v11 baseline.

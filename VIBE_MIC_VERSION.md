# 言灵 · Vibe Flow Remote 1.2.0

Release date: 2026-08-26

## Release identity

- Product version: `1.2.0`
- Configuration schema: `20`
- Onboarding version: `6`
- Stable voice profile: `v11`
- New-install voice mode: `continuous`
- Continuous safety limit: `30 minutes`

## Voice path

```text
RC003 ATVV
  -> ordered 16 kHz ADPCM decode
  -> robust speech leveling
  -> host-owned continuous ATVV session
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
- Host-session `MIC_EXTEND`: exact session every `8 seconds`
- Hold-release natural-stop window: `260 ms`
- Hold-release bounded close fallback: `700 ms`
- RC003 physical-hold boundary: approximately `60 seconds`

## v1.2.0 changes

- Promote click-to-start, click-to-stop continuous dictation to the recommended new-install mode.
- Keep one logical transcription session while replacing the released physical RC003 stream with a host-opened stream.
- Renew only the exact host-opened ATVV session and monitor real audio packets, WASAPI, endpoint, routing, queue, and memory health.
- Require real audio arrival before displaying the recording state; expose recovery and transport failure separately.
- Aggregate duration, coverage, maximum packet gap, level, queue drops, and lease writes across the whole logical session.
- Retain hold-to-talk as a compatibility mode with generation-safe release finalization and no post-release reopen.
- Preserve provider-direct text delivery, the proven stop cue, v11 processing, stable single-key mappings, and reversible audio routing.
- Decode GitHub Release metadata as UTF-8 and fall back to the official release redirect after API transport or metadata parse failure.

## Hardware validation

- `127.6 s` logical / `124.1 s` audio / `97.2%` coverage / `15/15` lease writes.
- `382.3 s` logical / `378.8 s` audio / `99.1%` coverage / `45/45` lease writes.
- `921.8 s` logical / `918.2 s` audio / `99.6%` coverage / `114/114` lease writes.
- Zero BLE queue drops, zero VB-CABLE queue drops, no transport stall, and no sustained memory growth in the longest run.
- Provider-direct delivery and route restoration passed at final submission.
- Hold-mode release regression passed without host renewal or a second microphone.

# 言灵 · Vibe Flow Remote 1.2.1

Release date: 2026-08-28

## Release identity

- Product version: `1.2.1`
- Windows file version: `1.2.1.0`
- Configuration schema: `25`
- Bridge configuration schema: `4`
- Onboarding version: `8`
- Stable voice profile: `v11`
- Recording kernel: `v1.0.3`
- Voice mode: `hold`
- Onboarding steps: `11`
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
- Function: short copy, long paste.
- Directions: native arrows by default; each can be assigned one verified keyboard action.
- Screenshot: any one direction can invoke Windows `Win + Shift + S`.
- Center: Enter.
- Home: Win+D.
- TV: Win+Tab Task View; arrows navigate and Enter confirms.
- Power, Back, and independent Volume controls are intentionally unmapped.

## Release gate

Release assets must pass 100 physical hold/release cycles, focused provider-direct delivery, the approximately 60-second boundary, screenshot invocation, TV Task View navigation, reconnect, sleep/wake, configuration persistence, and fixed-version download verification.

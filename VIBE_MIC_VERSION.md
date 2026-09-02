# 言灵 · Vibe Flow Remote 1.5.0

Release date: 2026-09-02

## Release identity

- Product version: `1.5.0`
- Windows host/bridge file version: `1.5.0.0`
- Stable Capture file version: `1.2.1.0`
- Configuration schema: `32`
- Bridge configuration schema: `7`
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
- The Capture binary contains no long-dictation continuation, forced release-close, or clipboard delivery path.
- The target text field must be focused before recording so the selected provider can deliver into it.
- Disconnect, sleep recovery, or process exit releases the old generation and audio route.

## Remote contract

- Record: hold to capture, release to finish.
- Function: short copy and long paste by default; both actions are configurable.
- Directions: native arrows by default; each can run one validated action.
- Center: Enter by default and configurable.
- Home: Win+D by default; short and long press are independently configurable.
- TV: Task View by default and configurable.
- Browser AI maps physical Left to Windows Browser Back.
- Power, Back, and independent Volume are unavailable because this RC003/Windows combination has not produced stable events.
- Real action receipts report the effective Profile and executor result.

## Shortcut recorder contract

- Users press the physical keyboard combination; no key-name text entry is exposed.
- The temporary recorder suppresses non-injected keys only while its modal session is active.
- Host normalization and Bridge parsing enforce the same validation rules.
- Unknown, partial, multiple-main-key, and `Ctrl+Alt+Delete` combinations are rejected.

## Smart Profile contract

- Smart Profiles are opt-in and disabled by default.
- Each normalized process name belongs to at most one Profile.
- Foreground polling uses `250 ms` with a `350 ms` debounce.
- Window titles and user text are never read or stored.
- Unmatched applications use an explicit fallback Profile.
- Runtime switching only replaces validated non-voice actions; it does not restart or mutate Capture.

## Release status

V1.5.0 is the recommended public release. V1.4.0 is retained as an incomplete preview archive. The frozen Capture source SHA-256 is `736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2`; the pinned Capture binary SHA-256 is `B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683`.

# Vibe Flow Remote Architecture

## Runtime components

```text
VibeFlow.exe
  -> manages settings, onboarding, actionable self-checks and process lifecycle
  -> guides new users through provider, VB-CABLE, Bluetooth, shortcut and first-dictation validation
  -> converts session metrics into a privacy-safe Chinese health summary and optional sound feedback
  -> compares audio settings with the recoverable v11 stable profile without silently overwriting custom choices
  -> turns RC003 key logs into short-lived highlights on the overview remote and shortcut map
  -> starts VibeMicAtvvCapture.exe
  -> starts VoxDeckInputBridge.exe

RC003 BLE ATVV notifications
  -> ordered codec-sync/audio queue, 120-byte framing, and IMA ADPCM decode at 16 kHz mono
  -> BLE notifications enter a bounded queue and are decoded by one ordered worker, never on the WinRT callback
  -> robust speech leveling estimates level below the 95th-percentile ceiling so isolated ADPCM spikes do not suppress an entire frame
  -> transparent mode bypasses speech enhancement and applies only the user-selected fixed sensitivity
  -> one generation-aware provider controller supports WeChat, Typeless, Windows Voice Typing, Voquill and custom clients
  -> before provider activation, a generation-aware endpoint lease temporarily assigns CABLE Output to the Windows Console, Multimedia and Communications capture roles
  -> a UI Automation focus listener retains the most recent editable control; session start prefers the focused editor, an editor under the pointer, then a recent editor from the same application
  -> focus verification is passive: the provider path never activates the target window or calls UI Automation `SetFocus`, preserving the text-service context used by direct insertion
  -> non-text containers such as Chromium `Group` controls are never accepted as writable delivery targets
  -> WeChat defaults to the validated AI profile: the long-lived host taps `Ctrl+Win+Shift` once after routing is ready and once after the final audio drain
  -> WeChat writes directly into the preserved editor; toolbar activation, clipboard monitoring, synthetic paste, and delayed replay are prohibited
  -> the compatibility profile may hold `Ctrl+Win` for older WeChat builds, while generic providers continue to use SendInput
  -> generic clients use SendInput with configurable toggle or hold semantics and a bounded startup delay
  -> physical stream START invokes the provider and buffers only the short interval until the provider is ready
  -> an audio notification that races ahead of STREAM_START creates the same generation instead of losing the first words
  -> decoded PCM enters a non-blocking 30-second safety ring in fixed 20 ms source blocks
  -> physical stream STOP accepts an 80 ms Bluetooth tail, appends the configured 180 ms silence tail and drains output
  -> after virtual audio drains, the provider is stopped first; WeChat gets a 350 ms completion window before the endpoint lease restores all original default microphones, with a local marker for crash recovery
  -> if the WeType panel cannot open, that session is discarded and logged; audio is never replayed later
  -> stale generations cannot close or deliver audio into a newer session
  -> event-driven WASAPI follows the Windows endpoint clock while linearly converting 16 kHz mono to 48 kHz stereo for VB-CABLE
  -> CABLE Input / CABLE Output
  -> selected transcription client

Recording state transitions
  -> capture signals named start/stop events at the accepted stream transitions
  -> a dedicated VibeFlow.exe sound worker consumes both events, suppresses start audio, and plays the preloaded stop cue synchronously
  -> runtime-log polling updates visual state only and never schedules recording cues

RC003 keyboard events
  -> low-level keyboard hook for distinctive keys
  -> the physical F5 event is suppressed while ATVV controls the voice lifecycle
  -> device-scoped Raw Input provides redundant F5 detection and direction hold detection
  -> SendInput for configured shortcuts
  -> allowlisted client actions focus or start installed Agent and development applications
```

## Safety boundaries

The voice transport rationale and release gates are documented in
[`VOICE_PIPELINE_RESEARCH.md`](VOICE_PIPELINE_RESEARCH.md).

- Vibe Flow itself does not require administrator permission. The separately
  installed VB-CABLE virtual-audio driver requires administrator approval during
  its own installation and may require a Windows restart.
- The app does not inject code into WeChat or editors.
- Client quick launch accepts only built-in targets, never opens web fallbacks and never executes arbitrary configuration commands.
- Generated configuration keeps unsupported Back and Volume +/- mappings disabled.
- Ordinary direction keys pass through; only RC003-scoped repeated Up/Down events activate volume control.
- Runtime logs do not intentionally write Bluetooth MAC addresses or complete HID paths.
- Audio packet payloads are never written to release logs; only per-session timing and level statistics are recorded.
- Provider logs record trigger-to-ready timing and shortcut names, but never recognized text.
- Delayed STOP events from an older ATVV generation cannot release a newer stream.
- Audio tagged with a stopped generation cannot create an implicit replacement stream.
- Starting a build from a different directory asks the existing Vibe Flow instance to exit before taking over.
- Automatic default-microphone routing snapshots all three Windows capture roles before changing any role. A failed apply is rolled back, superseded generations cannot restore over a newer session, and an unexpected exit leaves a local recovery marker for the next start.

The following v11 order is a release invariant and must not be shortened or
reordered by UI, provider, or performance changes:

```text
acquire CABLE Output as all default capture roles
-> activate selected transcription provider
-> deliver RC003 audio in real time
-> append silence and drain all virtual-microphone blocks
-> submit/end the provider session
-> wait for provider completion, then restore every original capture role
```

## Configuration

- `vibe-mic-config.json`: application settings and user-visible mappings.
  Schema 19 stores the transcription provider, shortcut, toggle/hold mode, startup delay, audio-processing mode, automatic virtual-microphone routing, onboarding version, sound-feedback preference, update preference and stable-profile version. It migrates the default recording interaction to release-driven hold-to-talk.
- `voxdeck-shortcuts.json`: generated input bridge mappings.
- `remote-voice-session/`: local runtime diagnostics, ignored by Git.

Public releases start from `vibe-mic-config.default.json`; local configuration and logs are never packaged.

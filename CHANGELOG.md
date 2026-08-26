# Changelog

## 1.2.0 - 2026-08-26

- Promoted continuous dictation to the recommended default for new installs:
  press Record once to start, release it, and press it again to finish. Existing
  installations retain their selected voice mode during schema 20 migration.
- Rebuilt the long-session controller around one logical generation. After the
  RC003 `start_reason=0x03` physical stream ends, the host opens a
  `start_reason=0x00` stream and sends `MIC_EXTEND 0x0E` every eight seconds to
  the exact active session. First-open control-only responses use bounded retry.
- Added transport-health monitoring for real audio coverage, packet intervals,
  BLE and VB-CABLE queue loss, WASAPI and endpoint state, route ownership, and
  process memory. The UI enters its live state only after real audio arrives;
  stalled streams receive one bounded recovery and fail visibly if it cannot
  restore audio. Sub-700 ms accidental sessions now require a retest instead of
  being counted as a complete 7/7 self-check pass.
- Added a 30-minute safety limit for forgotten continuous sessions. This is a
  protection boundary, not a claim that every third-party transcription tool has
  the same duration support.
- Completed RC003 hardware regressions at 124 seconds, 6 minutes, and 15 minutes
  22 seconds. The longest run delivered 918.2 seconds of real audio over a
  921.8-second logical session (99.6% coverage), renewed 114/114 times, dropped
  zero BLE or VB-CABLE packets, and held private memory near 49-51 MB.
- Revalidated hold-to-talk compatibility independently: a 6.66-second hold
  stopped naturally within 80 ms of release, sent no MIC_EXTEND commands,
  opened one transcription microphone, restored routing, and retained native
  provider input without clipboard or synthetic paste behavior.
- Preserved stable voice profile v11, gain `1.0`, speech processing, 180 ms
  startup and drain timing, WeChat `Ctrl+Win+Shift` AI toggle, automatic reversible
  `CABLE Output` routing, completion sound, and verified shortcut mappings.
- Fixed GitHub update metadata decoding by using UTF-8 explicitly. API metadata
  parse failures now fall back to GitHub's official `releases/latest` redirect;
  downloads still require HTTPS, user confirmation, and matching SHA-256 data.
- Updated the overview, dictation settings, onboarding, diagnostics, beginner
  tutorial, quick start, release notes, and recording-mode guide for continuous
  dictation. Bumped all components to 1.2.0, schema 20, and onboarding 6.

## 1.1.0 - 2026-08-26

- Made hold-to-talk the stable default: press and hold Record to capture, then
  release to submit. Existing schema 18 continuous defaults migrate to schema 19
  hold mode without changing the validated voice profile or button mappings.
- Added a dedicated `VibeMicVoiceKeyReleased` event. Release first waits 260 ms
  for the RC003 natural stop, then uses one generation-safe close and a bounded
  700 ms fallback. A hold-mode release can never enter the reopen path, preventing
  a second transcription microphone after the user has finished.
- Confirmed and documented the RC003 firmware boundary: one physical hold emits
  F5 UP and ATVV stop at roughly 60 seconds. Host MIC_OPEN, MIC_EXTEND, exact
  close/reopen, and wildcard close/reopen cannot restore real audio within that
  same physical hold.
- Kept short-press continuous dictation as an experimental, non-default option
  with a 10-minute safety limit. Removed the unsupported claim that one sustained
  physical hold can reliably exceed 60 seconds.
- Preserved stable profile v11, gain `1.0`, speech processing, 180 ms drain,
  provider timing, endpoint routing, BLE retry behavior, and verified single-key
  defaults unchanged.
- Updated the overview, dictation screen, onboarding, and diagnostics for the
  hold/release interaction, with duration, output-level, remote-light, toast, and
  state feedback. Experimental sessions retain logical total and segment metrics.
- Added Save, Select All, Quick Open File, New Terminal, Delete Line,
  Run/Debug, and Close Tab shortcut choices.
- Added visible duplicate-assignment warnings while preserving intentional
  duplicate mappings.
- Preserved the exact Windows UI Automation text control around WeChat sessions,
  retaining the latest editable focus and checking the editor under the pointer
  so a Chromium page `Group` cannot replace the real input target. The default
  WeChat AI profile now taps `Ctrl+Win+Shift` after routing is ready and again after
  virtual audio has drained, without entering the toolbar clipboard mode. The provider path passively
  verifies the existing editor focus without activating windows, calling UI
  Automation `SetFocus`, monitoring the clipboard, or synthesizing paste.
- Confirmed the WeChat trigger root cause with the same captured RC003 audio: local
  Whisper recovered the full sentence from raw and processed audio, while the legacy
  `Ctrl+Win` hold path omitted its tail. The `Ctrl+Win+Shift` AI toggle preserved the
  complete sentence and applied WeChat's structured organization. Provider shutdown
  now precedes microphone-route restoration, with a 350 ms completion window.
- Prevented WeChat communication sessions from temporarily lowering the Windows
  playback volume. A recoverable per-session lease applies the user's "do nothing"
  communications-ducking policy during provider startup, broadcasts the change
  before the hotkey is pressed, and restores the exact prior preference after the
  session. Startup recovery handles an interrupted lease and never overwrites a
  preference that the user changed while dictation was active.
- Kept recording start silent with immediate visual feedback, and restored the
  proven short two-note completion cue for recording stop. Normal completion
  does not add a second sound; errors retain an alert cue. State transitions use
  named events rather than 500 ms log polling, and long-dictation segment
  renewal cannot replay recording cues.
- Added user-confirmed in-app updates from GitHub Releases with semantic version
  comparison, API rate-limit fallback, HTTPS-only assets, and SHA-256 verification.
- Added optional Authenticode signing and verification for all first-party EXEs
  and the installer, including GitHub Actions PFX secret support.
- Bumped the app, capture helper, input bridge, installer, configuration schema,
  and onboarding metadata to 1.1.0 / schema 19 / onboarding 5.
- Rebuilt the beginner tutorial, quick start, release notes, recording-mode guide,
  reusable screenshots, and community QR section for GitHub publication.

## 1.0.3 - 2026-08-25

- Fixed the Windows-login race where the remote key bridge could start before
  the RC003 ATVV capture service or selected transcription client was ready.
- Added a release-safe held-key handoff: an early recording request is recovered
  only while the physical key remains held, so transcription never starts after
  the user has already released it.
- Added background host recovery when the capture helper is missing or stalled,
  plus startup warm-up for the configured WeChat, Typeless, or Voquill client.
- Prevented a completed, superseded capture process from clearing the readiness
  and startup timer of the newly started capture process after settings changes.
- Added matching `1.0.3` Windows file metadata to both background helpers so
  diagnostics can identify mixed-version installations.
- Reduced paired-RC003 startup latency with cached GATT service and characteristic
  discovery, retaining an uncached fallback whenever Windows has no valid cache.
- Added a persistent `vibe-flow-host.log` with provider readiness, capture startup,
  wake requests, and automatic recovery decisions for post-reboot diagnosis.
- Reworked the remote preview as a code-rendered silver RC003 reference with a
  taller physical proportion, accurate upper-button layout, restrained icons,
  lower-body whitespace, recording ripples, and state highlights; the source
  reference photo is not distributed.
- Rebuilt the shortcut screen as a clean two-column configuration and hardware
  preview workspace. Hovering or selecting a mapping highlights its physical
  position without changing any validated button behavior.
- Protected the validated audio profile behind an explicit advanced-settings
  confirmation and changed the overview action to manage, rather than
  accidentally pause, an already-running bridge.
- Made Windows startup an explicit first-run opt-in, added contextual Chinese
  progress feedback, fixed the stale connection-button label, and documented
  every onboarding step with a reusable screenshot.
- Kept schema 15, voice state machine v11, `1.0x` gain, clear speech processing,
  180 ms drain, automatic reversible endpoint routing, and verified mappings unchanged.

## 1.0.2 - 2026-08-24

- Made `VibeFlow-Setup.exe` the unmistakable recommended download and warned
  non-developers that GitHub's generated source archives are not installers.
- Added direct tutorial paths for first installation, upgrade validation,
  shortcut lookup, and symptom-based troubleshooting.
- Replaced two version-specific release-note files with one current
  `docs/RELEASE_NOTES_ZH.md` in both the repository and release packages;
  upgrades remove legacy `RELEASE_NOTES_V*.md` files from the install folder.
- Tightened release validation around user-facing download guidance and package
  contents while keeping schema 15, voice state machine v11, `1.0x` gain,
  clear speech processing, 180 ms drain, and automatic endpoint routing intact.
- Replaced the installer's fixed 1.8-second shutdown delay with a bounded wait
  for the running app to finish stopping its capture and input-bridge children,
  preventing silent upgrades from aborting while normal cleanup is still active.

## 1.0.1 - 2026-08-24

- Reworked the Chinese user guide around a beginner-first five-step setup, with
  provider-specific instructions for WeChat Input Method, Typeless, Windows
  Voice Typing, Voquill, and configurable global-hotkey clients.
- Added a symptom-to-repair troubleshooting matrix covering installation,
  VB-CABLE endpoints, Bluetooth pairing, provider startup, missing text, delay,
  quiet speech, stale versions, and unsupported RC003 physical buttons.
- Added a real expanded provider-selector screenshot and upgraded the shortcut
  page with a compact hardware-validated reference for Home, direction/volume,
  Function, and TV behavior.
- Extended deterministic screenshot capture and release validation to include
  all tutorial images in the installer and portable ZIP.
- Kept the validated schema 15 / voice state machine v11 pipeline unchanged:
  `1.0x`, clear speech processing, 180 ms drain, ordered audio delivery, and
  automatic reversible `CABLE Output` capture routing.

## 1.0.0 - 2026-08-24

- Promoted the hardware-validated v11 voice pipeline and schema 15 defaults to
  the first stable Windows release without changing its gain, routing, drain,
  packet ordering, or endpoint-recovery behavior.
- Polished the native interface with correctly rendered navigation icons, a
  clearer V1 release identity, refined status colors, and live signal feedback
  for connecting, listening, processing, completion, and error states.
- Added a per-user Windows installer with Start Menu integration, clean upgrade
  behavior, optional desktop shortcut, uninstaller, and preserved user config.
- Expanded the five-step first-run guide and Chinese tutorial, then refreshed
  the reusable 1280 x 840 screenshots used by GitHub and support documentation.
- Kept the portable ZIP for users who prefer a no-install distribution and added
  release checksums for both public artifacts.

## 0.4.0-alpha - 2026-08-24

- Pinned the verified v11 voice settings as a named, recoverable stable profile:
  `1.0x`, robust speech processing, 180 ms drain, `CABLE Input`, automatic
  default-capture routing, and provider-specific startup timing.
- Replaced the technical diagnostics view with a seven-part self-check covering
  packaged components, both VB-CABLE endpoints, profile drift, bridge services,
  RC003/ATVV readiness, provider shortcut setup, and the latest end-to-end
  dictation. Each warning or failure has a direct repair action.
- Added self-check results to the privacy-safe issue summary without collecting
  recognized text, audio payloads, Bluetooth addresses, or complete device paths.
- Added real RC003 button highlights to the overview remote and an interactive
  remote control map beside shortcut configuration.
- Clarified that VB-CABLE is the only required extra local driver and made a
  successful real-device dictation mandatory before first-run setup completes.
- Refined navigation, workspace texture, status hierarchy, button feedback, and
  success feedback for a release-ready desktop experience.
- Upgraded configuration to schema 15 while preserving existing provider and
  verified remote-button choices.

## 0.3.0-alpha - 2026-08-24

- Renamed the visible product to **言灵 · Vibe Flow Remote** while preserving
  executable names, package names, update behavior, and existing user settings.
- Replaced the dense first-run dialog with a five-step guide: transcription
  provider, VB-CABLE endpoints, RC003 connection, provider shortcut, and first
  real-device dictation.
- Added provider-specific guidance and recommended profiles for WeChat Input
  Method, Typeless, Windows Voice Typing, Voquill, and custom tools.
- Added recording, processing, completion, and error visual states plus a subtle
  completion/error sound that can be disabled in Settings.
- Added a Chinese session-health summary covering trigger latency, recording
  duration, signal level, BLE gaps, queue drops, virtual-microphone drain, and
  default-microphone restoration.
- Added one-click, privacy-safe problem-summary copying, log-folder access, and
  bounded local log rotation. Normal diagnostics never include audio or
  recognized text.
- Upgraded configuration to schema 14 without changing the validated v11 voice
  route or RC003 button mappings.

## 0.2.1-alpha - 2026-08-24

- Added automatic per-dictation routing from the user's Windows default
  microphone to `CABLE Output`, so clients configured for `default` consume the
  RC003 stream instead of an unrelated physical microphone.
- Restores the original Console, Multimedia, and Communications capture endpoints
  after audio drains, rolls back partial failures, and preserves a local recovery
  marker for restoration after an unexpected exit.
- Added generation ownership so an older transcription completion cannot restore
  the microphone while a newer recording is active.
- Upgraded configuration to schema 13 and enabled automatic routing for existing
  users without changing verified remote-button mappings.

## 0.2.0-alpha - 2026-08-24

- Added provider profiles for WeChat Input Method, Typeless, Windows Voice Typing,
  Voquill, and configurable hotkey-driven transcription clients.
- Changed WeChat startup to toolbar-first with a bounded shortcut fallback,
  removing the measured 1.2-second dead wait and large unrecoverable pre-roll.
- Replaced frame-wide peak limiting with robust speech-level estimation and
  per-sample limiting; added a transparent fixed-gain mode.
- Reduced the fixed 400 ms end tail to the configured 180 ms default and added
  `trigger_to_ready_ms`, queue depth, drain time, and before/after level metrics.
- Added provider selection, shortcut, toggle/hold mode, and audio processing to
  the main UI and first-run setup.
- Upgraded configuration to schema 12 while preserving existing user mappings.
- Prevented a new recording from waiting behind the previous WeChat result panel;
  superseded virtual-microphone audio is discarded instead of delivered late.
- Added an explicit, one-shot audio diagnostic that captures decoded 16 kHz PCM,
  processed PCM, and `CABLE Output` for the next session only (30-second cap).

Validation on the development machine:

- RC003 ordered decode and audio processing self-tests pass.
- Five-second `CABLE Input` WASAPI clock test: 5047 ms, zero drops, zero pending.
- WeChat toolbar panel trigger is verified.
- Typeless 2.3.1 `RightAlt` start/stop is verified. Its end-to-end `CABLE Output`
  route still requires confirmation after that microphone is selected in Typeless.
- Voquill requires installed-client end-to-end validation.

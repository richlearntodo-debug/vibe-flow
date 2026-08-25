# RC003 voice pipeline research

## Problem statement

The RC003 microphone sends 16 kHz mono IMA/DVI ADPCM over Bluetooth ATVV. Desktop
dictation clients expect a normal Windows recording endpoint and own speech-to-text.
Vibe Flow must therefore behave like a stable microphone transport and provider
controller rather than a recorder, transcription service, or delayed audio player.

## Evidence from the test machine

- Both `CABLE Input` and `CABLE Output` advertise a 48 kHz, stereo, 24-bit PCM
  Windows audio-engine format.
- The earlier Vibe Flow pipeline opened `CABLE Input` as 16 kHz mono and wrote
  audio only after a voice session began.
- Hardware sessions delivered complete 120-byte ATVV frames with `queue_drops=0`
  and `partial_frame_bytes=0`. BLE framing was not the main recognition failure.
- The old WeChat controller tapped `Ctrl+Win`, waited 1200 ms, and only then clicked
  the toolbar. On this machine the injected shortcut usually failed while the toolbar
  opened the panel about 30 ms after the click.
- The resulting pre-roll was typically 21,120-21,600 samples (1.32-1.35 seconds).
  Input and output both run in real time, so this backlog could not catch up while
  the user spoke. Stops regularly left 61-62 queued 20 ms blocks, and draining those
  blocks plus a fixed 400 ms tail took 1.68-1.71 seconds.
- Raw sessions commonly measured 0.4-2.5 percent RMS with isolated 92-100 percent
  peaks. The previous frame-wide limiter reacted to those peaks by reducing gain for
  all samples in the frame, producing pumping while still amplifying noise 5-8x.

## Confirmed root cause of the major recognition failure

The decisive failure was an endpoint-routing mismatch, not BLE decoding, gain, or
the transcription engine. Vibe Flow wrote RC003 audio to the playback endpoint
named `CABLE Input`, while WeChat Input Method and Typeless were configured to use
the Windows default recording device and therefore continued listening to the
physical computer microphone. VB-CABLE forwards `CABLE Input` to `CABLE Output`,
but it does not make applications select `CABLE Output` automatically.

The validated v11 fix acquires a generation-owned lease before provider startup,
temporarily assigns `CABLE Output` to the Console, Multimedia, and Communications
default capture roles, drains all virtual-microphone audio, and then restores the
three original endpoints. A recovery marker handles unexpected exits, and an old
generation cannot restore over a newer recording. This sequence is a permanent
release invariant; changing gain or buffering cannot substitute for it.

## Confirmed root cause of missing sentence tails and absent AI organization

The later sentence-tail failure was a provider-control mismatch, not an audio-quality
failure. A 19.755-second RC003 diagnostic measured `queue_drops=0`, a maximum BLE gap
of 58 ms, 1.0 percent raw RMS, and 3.7 percent processed RMS. Local Whisper recovered
the complete test sentence independently from both the raw decoded WAV and the
processed WAV. This proved that Bluetooth transport, ADPCM decoding, gain, and
VB-CABLE all retained the missing words.

An A/B replay of that same captured audio isolated the provider behavior:

- The legacy WeChat `Ctrl+Win` hold profile returned an unstructured sentence and
  omitted its final clause.
- The WeChat `Ctrl+Win+Shift` toggle profile returned the complete sentence and
  applied the client's AI organization.

The release default is therefore `Ctrl+Win+Shift` with toggle semantics. Completion
order is a strict invariant: drain VB-CABLE, tap the provider shortcut a second time,
wait 350 ms for WeChat to finalize, and only then restore the original Windows capture
roles. `Ctrl+Win` hold remains an explicit compatibility profile for older client
versions; it must not be silently selected or described as the AI profile. Toolbar
and clipboard delivery remain prohibited.

## Confirmed root cause of Windows playback-volume reduction

WeChat starts a Windows communications audio session when its dictation shortcut
activates. With no explicit `UserDuckingPreference`, Windows applied its default
communications ducking policy: the active speaker endpoint visibly moved from
81 percent to 72 percent and returned to 81 percent after the provider session.
Every capture endpoint, including `CABLE Output`, stayed unchanged at the same
time. The microphone route and speech gain were therefore not the cause.

The release fix acquires a host-owned ducking-preference lease as soon as the
remote wake request arrives, before provider hotkey injection. It writes the
Windows "do nothing" preference, broadcasts `WM_SETTINGCHANGE`, retains the lease
through the complete provider session, and restores the exact previous value after the
session. A JSON marker recovers an interrupted lease on the next launch. Restore
is skipped if the user or another system component changed the preference while
dictation was active. In the matching Control Center test, no render or capture
endpoint changed during the complete provider session.

## Industry-aligned design

1. Keep the virtual microphone playback endpoint open and clocked continuously.
2. Use fixed 20 ms blocks so downstream capture sees stable timing.
3. Convert RC003 PCM to the endpoint's 48 kHz stereo format before VB-CABLE.
4. Buffer only for the bounded provider startup interval; never replay a completed
   session later.
5. Drain all queued speech and a short silence tail before submitting transcription.
6. Preserve the editor's existing focus context and verify it passively; do not
   activate the window, call UI Automation `SetFocus`, or relay results through
   the clipboard.
7. For WeChat AI organization, route input first, tap `Ctrl+Win+Shift` to start,
   drain all virtual audio, tap again to stop, wait for provider finalization, and
   only then restore the original microphone. Do not enter toolbar or clipboard
   delivery.
8. Use `SendInput` for Typeless, Windows Voice Typing, Voquill and custom shortcuts.
9. Support both toggle clients (tap to start/stop) and hold clients (keydown until
   all audio has drained, then keyup).
10. Estimate speech gain from a winsorized frame level so an isolated codec spike
   cannot turn down an entire frame. Apply the limiter per sample, not per frame.
11. Preserve a transparent fixed-gain mode for diagnosis and users who prefer no
   speech enhancement.
12. Log trigger-to-ready timing and signal statistics, never audio payloads or
    recognized text.

Three interaction rules are also release invariants. WeChat hotkey taps or hold
transitions are sent by the long-lived host because capture-process `SendInput` can
report success while WeChat ignores the shortcut, and the toolbar path can switch to
clipboard delivery.
Input delivery may target only a writable UI Automation `Edit` or
equivalent text control; a focusable Chromium `Group` is not sufficient. Recording
state cues are emitted directly from accepted start/stop transitions through named
events, not inferred later by polling runtime log lines. The host intentionally keeps
start silent and plays only the accepted stop transition so speech begins without an
audible interruption while the user still gets a clear completion signal.

The endpoint writer uses shared-mode, event-driven WASAPI. The Windows Audio Engine
requests each buffer from `BufferedWaveProvider` on the endpoint clock; `ReadFully`
supplies silence between sessions without polling or restarting the endpoint. The
previous WinMM writer was removed after its deterministic 5-second endpoint test
took 5.7-5.8 seconds and accumulated multiple seconds of pending speech.

On the validation machine, the replacement completed the same endpoint test in
5.050 seconds with zero dropped blocks and zero pending blocks. This benchmark is
repeatable with `VibeMicAtvvCapture.exe --sink-clock-test "CABLE Input"` and is a
release gate, not a simulated audio-unit test.

The production implementation uses linear 3x conversion because the source and
destination rates have an exact integer ratio. It preserves the 16 kHz speech
band while avoiding the sample-and-hold imaging of simple sample repetition.

Provider defaults are profiles, not hard dependencies:

- WeChat Input Method: `Ctrl+Win+Shift`, toggle start/stop, AI organization enabled;
  `Ctrl+Win` hold is available only as an explicit compatibility profile.
- Typeless: `Right Alt`, toggle start/stop.
- Windows Voice Typing: `Win+H`, toggle start/stop.
- Voquill: `Ctrl+Win`, hold until release, matching its current open-source Windows
  default. Users can override this when their client configuration differs.
- Custom: user-defined shortcut and toggle/hold mode.

## Alternatives considered

- A custom Windows virtual microphone driver offers the strongest product control,
  but public distribution requires driver signing, installation privileges, update
  handling, and a substantially larger security surface.
- Direct transcription with a cloud or local ASR engine would bypass WeChat's text
  organization and does not satisfy the product requirement.
- Per-session virtual audio output is simpler but produces endpoint startup races,
  format conversion churn, and first-word loss.

VB-CABLE remains the practical release dependency for the current open-source
Windows build. Its vendor describes the product as a Windows virtual audio driver
that forwards playback input to recording output and supports MME, KS, DirectSound,
and WASAPI clients.

## Release gates

Do not mark the voice path release-ready until one real RC003 completes all of the
following on the first attempt:

- three normal-volume phrases;
- three deliberately quiet phrases;
- three phrases at 40-60 cm distance;
- zero ATVV queue drops and zero partial frames;
- one transcription submit per physical recording;
- WeChat `trigger_to_ready_ms` below 700 ms on the validated machine (recent
  successful sessions measured about 317-352 ms);
- startup pre-roll below 4000 samples and normal stop queue near 0-10 blocks;
- virtual-microphone drain below 600 ms including the configured silence tail;
- no missing first or final phrase segment;
- no delayed replay into the next recording.
- no Windows render-endpoint volume change while the provider communication session
  is active, and no ducking recovery marker after completion;

## References

- VB-Audio, VB-CABLE: https://vb-audio.com/Cable/
- Microsoft, Rendering a Stream: https://learn.microsoft.com/windows/win32/coreaudio/rendering-a-stream
- Microsoft, Disabling the Default Ducking Experience: https://learn.microsoft.com/windows/win32/coreaudio/disabling-the-ducking-experience
- Microsoft, IAudioSessionControl2::SetDuckingPreference: https://learn.microsoft.com/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol2-setduckingpreference
- Microsoft, SysVAD virtual audio device sample: https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad
- HD838A, remote-mic-app ATVV implementation: https://github.com/HD838A/remote-mic-app
- Typeless first dictation: https://www.typeless.com/zh-cn/help/quickstart/first-dictation
- Typeless microphone selection: https://www.typeless.com/zh-cn/help/troubleshooting/microphone-unavailable
- Voquill open-source desktop dictation: https://github.com/voquill/voquill

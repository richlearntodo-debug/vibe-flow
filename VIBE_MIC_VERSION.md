# 言灵 · Vibe Flow Remote 1.0.2

Vibe Flow Remote turns the RC003 / MI RC microphone into a live Windows input source.
The release keeps transcription ownership in the user's selected client and
does not record audio or inspect recognized text during normal use. An explicit
one-shot diagnostic can save only the next session locally for stage comparison.

Voice path:

```text
RC003 ATVV -> ordered 16 kHz ADPCM decode -> optional robust speech leveling
-> event-driven WASAPI -> CABLE Input / CABLE Output -> transcription client
```

Supported provider profiles are WeChat Input Method, Typeless, Windows Voice
Typing, Voquill, and a configurable hotkey-driven client. Configuration schema
15 stores the provider, shortcut, toggle/hold trigger, startup delay, audio
processing mode, automatic virtual-microphone routing preference, onboarding
version, completion-sound preference, and the validated voice-profile version.

The capture helper uses voice state machine v11. Before a provider starts, Vibe
Flow temporarily assigns `CABLE Output` to all three Windows default capture
roles. It restores the original endpoints after the audio drains and uses a local
recovery marker after an unexpected exit. WeChat opens through its validated
toolbar first instead of waiting 1.2 seconds for a failing injected shortcut.
Generic providers use `SendInput`, and all audio is drained before the provider is
stopped or submitted. A newer recording preempts an older WeChat completion wait
so buffered speech is never replayed seconds late.

The 1.0.2 release keeps the current v11 settings as a recoverable stable profile
and does not change voice transport, timing, or hardware mappings. It makes the
recommended Windows installer explicit, warns ordinary users away from GitHub's
auto-generated source archives, gives first-time and troubleshooting users short
paths through the Chinese guide, and packages only one current release-notes file.
Existing users keep their voice-provider and button choices; non-default audio
tuning is reported as a warning and can be restored with one click.

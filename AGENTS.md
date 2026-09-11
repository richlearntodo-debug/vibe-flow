# Vibe Flow Agent Rules

## 1. General working rules

- Work incrementally in the existing repository.
- Inspect the real source code and execution path before modifying behavior.
- Preserve all existing uncommitted user changes.
- Do not perform unrelated refactoring or repository-wide formatting.
- Do not automatically push commits, publish releases, modify GitHub settings, or sign binaries.
- Do not install drivers, modify Windows audio devices, modify the registry, or install system software without explicit user approval.
- Mark all hardware behavior that has not been tested with a real device as unverified.

## 2. Frozen recording contract

The existing stable RC003 recording path has higher priority than every V2.0 feature.

Before implementation, read and verify the actual baseline from:

- VIBE_MIC_VERSION.md
- vibe-mic-config.default.json
- docs/RELEASE_QUALITY_GATE_ZH.md
- docs/ISSUE_2_REGRESSION_ZH.md
- scripts/VibeMicAtvvCapture.cs
- scripts/VoxDeckInputBridge.cs
- scripts/VibeMic.cs

Preserve these stable behaviors:

- Recording mode remains hold-to-talk.
- Press and hold starts recording.
- Releasing ends recording.
- voiceMode remains hold.
- captureSeconds remains 0.
- gain remains 1.0.
- autoLevel remains true.
- audioProcessingMode remains speech.
- drainMs remains 180.
- RC003 single-session duration remains device-controlled, with the existing approximately 60-second boundary.
- The existing third-party speech-tool integration remains unchanged.
- Recording completion must not be reported as transcription completion.

Do not:

- modify, rebuild, replace, resign, or patch the frozen Capture component;
- change the expected Capture hash merely to make a changed binary pass validation;
- add MIC_EXTEND;
- add automatic long-recording continuation;
- change hold-to-talk into click-to-toggle;
- automatically press Enter after recording;
- read, store, upload, or reinsert third-party transcription text;
- add a second recording state machine;
- route the recording button through ordinary key mappings, Deck navigation, Smart Focus, Project Spaces, or workflows.

If the actual checked-out baseline differs from a value above, stop only the conflicting modification, record the evidence, and preserve the currently verified stable implementation.

## 3. Frozen input and gesture contract

Verify and preserve the current implementation values:

- DEFAULT_LONG_PRESS_MS = 650
- HOLD_REPEAT_INITIAL_DELAY_MS = 420
- HOLD_REPEAT_INTERVAL_MS = 80
- VOICE_RESTART_GUARD_MS = 500
- SMART_PROFILE_POLL_MS = 250
- SMART_PROFILE_DEBOUNCE_MS = 350

Do not:

- globally intercept the physical keyboard;
- rewrite Raw Input, keyboard Hook, device filtering, or gesture recognition for UI work;
- add double-click, triple-click, or hidden gesture behavior in V2.0;
- overwrite existing user mappings;
- automatically assign Home long-press;
- automatically enable Smart Profiles for existing users;
- send a shortcut until the correct target application has been activated and validated;
- claim unsupported Power, Back, or independent volume keys are supported.

Temporary interception used by an existing shortcut-recording dialog must remain scoped to that dialog and must be removed after success, cancellation, or error.

## 4. UI and UX rules

- Keep the existing Windows technology stack for V2.0.
- Do not migrate the product to Electron, WPF, WinUI, or another framework as part of this release.
- Refactor incrementally instead of rewriting the entire UI at once.
- The visual style must be simple, clear, modern, restrained, technology-oriented, and user-friendly.
- Maintain obvious visual hierarchy and emphasize the current status and next recommended action.
- Avoid unnecessary decoration, excessive gradients, excessive glow, dense text, and decorative animation.
- Every operation must expose a real state:
  idle, checking, running, success, warning, error, or canceled.
- Every failure must explain:
  what failed, what was not performed, the likely reason, and the next repair action.
- Never display unverified success states such as:
  “transcription completed”, “AI received”, “test passed”, or “development server started”.
- UI changes must support normal Windows scaling and keyboard navigation.
- HUD or Deck UI must not steal focus while recording.
- Opening and closing a new UI must not leave mapped keys, modifiers, or input interception active.

## 5. Installation and onboarding rules

The first-run experience must clearly guide users through:

1. Understanding hold-to-talk and privacy boundaries.
2. Pairing and verifying the remote.
3. Preparing and checking the local audio route.
4. Selecting and testing a speech tool.
5. Configuring the first practical Vibe Flow use case.

For each step:

- show why the step is needed;
- show its current status;
- provide a clear primary action;
- provide retry and troubleshooting actions;
- allow safe back navigation;
- preserve completed progress after an expected restart;
- never mark a step complete only because a button was clicked;
- distinguish automatic detection from user visual confirmation.

## 6. Configuration and migration rules

- Preserve all valid existing user settings.
- New features must be disabled or unbound by default for existing users unless explicitly safe.
- Do not silently replace old mappings, speech-provider settings, Profiles, or Smart Profile state.
- Use atomic saves where the current architecture supports them.
- Back up configuration before a destructive migration.
- Migration must be idempotent.
- Preserve unknown valid fields where possible.
- A saved configuration is not considered active until the relevant runtime acknowledges it.

## 7. Development loop

For each feature:

1. Trace the real execution path.
2. Define the smallest complete vertical slice.
3. Implement only that slice.
4. Build the affected components.
5. Run existing tests.
6. Run new focused tests.
7. Start the real application.
8. Use Computer Use for relevant UI flows when available.
9. Inspect logs and Git diff.
10. Verify the frozen recording files and expected hashes remain unchanged.
11. Test the old behavior with the new feature disabled.
12. Record unverified hardware requirements honestly.

Do not repeat an unsuccessful broad fix indefinitely.

After three failed attempts on the same root issue:

- stop expanding the modification;
- revert unsafe experiments;
- preserve independently completed work;
- document evidence and remaining uncertainty;
- do not modify the frozen recording core as a workaround.

## 8. Completion requirements

Before declaring any phase complete:

- affected components build successfully;
- existing tests have no new failures;
- new tests pass;
- frozen Capture files and validation expectations remain unchanged;
- existing user configuration remains compatible;
- the feature-disabled path preserves old behavior;
- the real UI flow has been inspected when Computer Use is available;
- error and recovery states are implemented;
- untested real-device behavior is explicitly marked;
- the Git diff contains no unrelated modifications.

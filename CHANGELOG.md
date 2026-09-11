# Changelog

## 2.0.0 candidate - 2026-09-05

- Added Quick Entries for opening or activating user-selected apps, verified Smart Focus targets, a non-activating Live HUD, a read-only Context Deck, Capture & Ask, and Browser Remote Lite.
- Unified the main shell into Home, Quick Entries, Controls, Voice, Diagnostics, and Settings, and kept a single reachable five-task onboarding flow.
- Added versioned atomic stores for Focus Targets and Quick Entries, safe URL/path validation, cancellation, recording-priority gates, and truthful external-action receipts.
- Added user-authored phrase packs ("用语片段"): the user writes their own short phrases, binds one to a remote key or a gesture layer, and the Bridge types it into whatever holds focus. Text is kept only in the local snippet store and referenced from the mapping table by an opaque id; injection is per-character Unicode key events, so a phrase never touches the clipboard, cannot land in clipboard history, and cannot be replayed into the wrong window by a paste race. The Bridge logs only the character count, never the phrase, and the manager dialog states that these are the user's own words kept on this machine.
- Completed the 工作流 page so a saved entry is something you manage rather than a name and a delete button. Each row now states where it stands (还没学习过 / 已学习未验证 / 已验证 / 目标已失效) and offers 设为当前, 编辑 and the state-appropriate primary action; 编辑 opens a dialog for renaming, switching mode, inspecting the learned input box (strategy, control type, last verification) and testing its focus without re-learning.
- Fixed the two favourite modes, which contradicted the code and each other. The mode now names the runtime contract: a 工作流 entry can be made the current app and has its learned input box focused before speaking, so the text is fixed to it, while a 快捷键 entry is only summoned by 打开 and never takes the text — switching a current entry back to 快捷键 now drops the selection instead of leaving text routed to an app that no longer claims it. Newly learned entries, and entries mirrored from verified targets, now get an explicit mode; before this every learned application was written with an empty mode, which is exactly why a freshly saved app could never be made the one that receives text.
- Fixed focus-only targets (terminals, consoles, and the text surface of packaged editors such as Notepad) being unverifiable after they were learned. Matching demanded `ControlType.Edit` for every target and a writable `ValuePattern` for every target, while a focus-only target is stored as `Text`/`Document` and by design has no writable pattern — so it could be learned, saved and displayed, and then reported `FOCUS-TARGET-STALE` on every observation while the text was never delivered. The accepted control type and the proof of a match now follow the stored strategy, the candidate scan enumerates the control types that strategy is stored with, and the writable-target rules are unchanged (a `TextPattern` still does not count as writable evidence). Verified on the affected real entry: `SMART FOCUS strategy=uia_focus state=error code=FOCUS-TARGET-STALE` became `state=success code=OK`, with `VOICE FOCUS LOCK armed=true` and `VOICE INPUT TARGET ready=true`.
- Fixed adding and starting packaged (Store/UWP) applications. The AppUserModelID was used as if it were a process name, so a Store app was invisible to `Process.GetProcessesByName`, was started again on every summon, and could never be attached to; and `File.Exists` rejected its `shell:AppsFolder\` launch identifier, because a shell parsing path is not a file on disk, so a Store app added while it was not running was never started at all. The real process name is now read back from the running process with `GetApplicationUserModelId`, the launch identifier is accepted as startable, and the picker's launch target is remembered instead of being discarded after one use. Learning is refused with an explanation rather than saved under a name nothing will ever match. Verified end to end on a real packaged app.
- Added icons for Store/UWP applications in the add-application list, which were previously listed with a blank gap: a packaged app has no executable on disk, so its icon is requested from the shell for the AppsFolder item itself through `IShellItemImageFactory`.
- Moved the home page's 工作流 entry above the fold. It sat at y=900 in a content viewport about 744px tall, so the entry to the page that decides where the text goes was 156px below the fold and only reachable by scrolling. Its position is now pinned by a host self-test, checked against the content viewport height rather than through UI Automation, because the scrolling panel reports children below the fold as on-screen — a false green that was measured before the assertion replaced it.
- Recorded, rather than papered over, that a running application is reused instead of re-launched with its remembered arguments: Windows activates the instance that is already open, so the arguments are not applied again, and the Host now logs `FAVORITE APP AUTOSTART reused=true arguments_applied=false` instead of implying a launch happened.
- Fixed the installed-application catalogue enumerating the shell's AppsFolder once per start-menu root, which re-walked the listing and duplicated its own diagnostic.
- Added metadata-only usage statistics to the settings page: how many dictation sessions ran, how many finished cleanly and the longest gap between two of them. They are aggregated from the runtime receipt log the Host already writes, read only timestamps and success markers, store nothing new, and the panel states its own scope so the numbers can never imply more than they cover.
- Fixed a record key that is reported held with no release edge wedging the voice chain. The bridge's stuck-hold watchdog compared "time since last activity", but a repeating key refreshes that stamp about thirty times a second, so the test could never be satisfied and the hold was never released — precisely in the case the watchdog was written for. The held event therefore stayed set, and because the frozen Capture re-arms a session from it on every ATVV reconnect, the machine settled into a self-sustaining loop of sessions that could not deliver audio: one real log recorded 52 recovered-at-ready sessions and 50 no-audio failures. The watchdog now also measures the age of the hold itself, which a stuck key cannot hide, releases it once past the remote's own session boundary, and latches so the repeats that keep arriving cannot be mistaken for a new press. A quiet gap, or the release edge finally arriving, lifts the latch.
- Stopped the recording-key isolation log writing one line per repeat. A held key repeats at the keyboard rate, so about thirty lines a second were written; a 2 MB log was consumed in roughly ten minutes and rotated away the history that makes it worth keeping (13862 isolation lines in one log plus 17486 in its predecessor, about four fifths of everything recorded). The start of a hold, a periodic repeat count and a closing summary are kept, which preserves the diagnostic value: a measurement of 50 held-key repeats now produces one line instead of fifty.
- The self-check now reports a record key that never reports being released, both while it is latched and after the bridge has released it, with the recovery step (press and release the key once, or power-cycle the remote) instead of leaving the remote looking unresponsive.
- Consolidated the two feedback surfaces so one message leaves through one outlet. `ShowActionToast` drove both the in-window card and the always-on-top Live HUD from the same `ActionResult`, so every save or learn put the same sentence on screen twice in two visual languages; and because the HUD is `TopMost` and pinned 18 px from the working area's bottom-right while the card sits 24 px from the window's bottom-right, a window in that corner had its card covered by the HUD. The card now carries the message while the app window is in front, and the HUD carries it when the window is behind, minimised, or hidden to tray; an explicitly requested HUD (tray ▸ 显示 Live HUD) is additive and no longer suppresses the card. Both surfaces now read their accents, glyphs, type steps, radii and lifetimes from `UiDesignTokens` — the colours already happened to agree, but the lifetimes had drifted, and the HUD kept a cancelled result on screen for 12 s while the card dropped it after 2.8.
- Fixed the Live HUD being able to pin itself on screen permanently, which is what made it read as a fixed dock bar rather than a popup. `LiveHudDurationMilliseconds` returned "never auto-hide" for the `Running`/`Checking` states, and `ActionResult.FromLegacyFeedback` classifies any message whose text begins with 「正在」 as `Checking` — so an ordinary informational toast ("正在检查蓝牙和遥控器语音通道") left an always-on-top panel sitting in the bottom-right corner indefinitely; it was observed still there minutes later with no activity. Action messages now carry a bounded lifetime of their own, while the live-state rule is untouched: an operation genuinely in flight, and live audio, still do not auto-hide (that contract stays pinned by `LiveHudUiTests`). Verified live on the installed build: the panel now appears and clears after 12 s instead of never.
- Corrected the V2.0 user guide's stale page count (the heading said 五个页面 while the navigation has six items) and documented the single-outlet feedback rule there.
- Added an exhaustive audit of every shortcut action the pickers can offer (`ShortcutActionTests`, run by the V2 suite). For each of the 40 distinct actions it asserts that the action is supported, renders a label, cannot capture the record key, is not offered twice under one name, and — the decisive property — is stored as chosen: picking an action and saving must not silently store that key's default instead, which is what would happen to any action the support gate rejected. It also asserts the four starter profiles are complete, fully supported, and claim disjoint applications. The bridge self-test gained the matching half: every shortcut the Host can store must parse into injectable keys with exactly one non-modifier, every launcher must be recognised, and `task-switcher`/`none`/`passthrough` must not resolve as chords. The audit found no defect in the action set itself; it exists so the next edit cannot introduce one silently.
- The double-tap window now follows the user's own Windows double-click speed (`GetDoubleClickTime()`, floored at the 500 ms platform default, capped at 900 ms) instead of a hard-coded 320 ms. A hard-coded window failed in both directions on real hardware: at 320 ms a natural double tap measured 378 ms apart and was rejected as two separate short presses, while asking for faster taps produced taps so short that the remote never reported the press at all — the log held two key-up edges and no key-down, so no gesture could be formed. Matching the platform setting means the remote follows the double-click speed the user already tuned, and the value is pinned by a self-test and a gate so it cannot be tightened again.
- The gesture card on the shortcuts page now states the double-tap window it actually uses instead of a stale hard-coded "0.32 秒", and the long-press line reads its duration from the same policy constant. The card also no longer advertises attaching a macro to a double tap — macros were removed, and the line had survived the removal.
- Fixed a client cold start that had no fallback left. Launching an AI client had exactly two strategies — an exact display-name match in `Get-StartApps`, then a bare executable name resolved through `App Paths`/PATH — and nothing else when both missed. On a real machine the first reported `Client launcher unavailable target=cursor` while opening the same Start-menu shortcut by hand took under five seconds: Cursor is installed outside Program Files with no `App Paths` entry, so the bare-name strategy can never start it, and the Win32 start-app probe's 6 s budget sat on the edge of a measured 4766 ms cold start. The launcher now tries the Start-menu shortcut's own target (resolved through the shell link object, the way Explorer starts it) between the packaged-app identifier and the bare name, and the probe's budget is 12 s. Verified on the installed build: the cold start that reported `unavailable` now starts Cursor twice in a row in about 530 ms, after which Smart Profiles switches to the bound profile. The shortcut search was also the one part of the launcher that no test had ever executed, so it now takes its search roots as an argument and the bridge self-test points it at a directory it creates — a nested match plus a near-miss decoy — instead of trusting the walk.
- Fixed a bound phrase being rejected as unknown whenever Smart Profiles switched. Switching a Profile rebuilt the bridge configuration with a hand-written field copy, and that copy never carried `snippets` — so with Smart Profiles enabled, which is the normal case, the first foreground match emptied the phrase table and every `snippet:` action failed with `unknown_snippet` even though the Host had shipped it correctly and the revision matched. The projection is now a named function whose every field is checked in the bridge self-test, so adding a configuration field without carrying it over fails loudly instead of silently disabling a feature; the phrase table's JSON round trip is pinned too, because the previous fixtures were built in memory and could not catch a document this side fails to read back. Verified end to end against a purpose-built target window: all 16 characters of a probe phrase, CJK included, arrived in the focused control while the log recorded only the character count.
- A fresh install now comes with a recommended gesture table instead of an empty one. The layer table used to start empty with no seeding path anywhere, so the whole three-layer gesture surface shipped invisible: every 长按 and 双击 row read 未配置 and nothing happened until the user authored the table by hand. The recommended set is written exactly once, only when no table exists at all — a table the user edited, or one they cleared on purpose, is never touched — and it binds long presses for page scrolling, back, redo and mute, plus double taps for cut, select-all, undo, save, play/pause and volume. Home is deliberately absent: its short press is 显示桌面, and the first tap of a double tap executes the short layer, so a Home double tap could not be completed at double-tap speed (measured on real hardware, the second tap arrived 2235 ms later). The seeded table is verified live on a machine with no table: seven entries, no Home, reaching the bridge as layered mappings.
- The self-check can now undo the USB selective-suspend repair, not only apply it. The restore action existed in the dispatcher but no surface ever emitted it. It is offered only when the repair script's own state file records that this app applied the disable — a 0 value set by a corporate image or by the user's own tuning does not invite a change to their power policy.
- The foreground window's own input layout is now read alongside the per-thread input method. The active input method was only ever read for this process's thread, which answers a different question from "which input method is the application being typed into using" — the limitation was documented rather than fixed. The foreground window's thread is now queried with its input queue attached, and its keyboard layout is reported as a language tag and compared with ours, so a missing provider panel has a checkable explanation. It is described as a layout rather than as a named input method, because another process's TSF profile cannot be read from here; the reader never throws and never returns null, since it sits on the voice path's logging.
- The double-tap window documented in the user guide no longer states a hard-coded 0.32 s; it now describes the window the app actually uses, which follows the user's own Windows double-click speed with the 500 ms platform default as its floor.
- Removed the retired Smart Focus dialog source (`scripts/ui/FocusTargetDialog.cs`). Its still-live pieces were relocated first — the UI-thread posting guard now sits on the Host form, where `DispatchUi` actually uses it — and the gates that used to read the dead file now assert the shipped favourite-app learning path (bounded capture, service-level stability and editability rejection, pending-then-save, and no clipboard/input dispatch) instead.
- Added three-layer gesture bindings on the Shortcuts page: every configurable physical key now carries independent 短按 / 长按 / 双击 rows, each layer binding exactly one action, and a layer without its own binding says so and names the layer it falls back to instead of implying it does something else. Layers persist through the shared store (`GestureBindingStore`) and the Bridge dispatches through `GestureLayerPolicy.Classify` → `GestureBindingStore.ResolveSteps` → `GestureMacroRunner.RunSteps`. Short presses and the Home/function-key long presses stay owned by the per-Profile mapping table, so Smart Profiles keeps working; the double layer (and the long layer for keys whose table has no long key) is owned by the store. The record key (F5) stays on the stable voice chain and never enters layering, and a key without an extra layer keeps the press-time behavior it shipped with.
- Note on gestures: a key that carries a double-tap layer now fires its short action on release rather than on press, because a double tap can only be recognised by watching for the second press. Clearing that key's double layer restores press-time dispatch.
- Built, then removed, gesture macros. A macro could only ride the double-tap layer (the frozen store resolves macro steps for the double layer alone), and the first tap of a double tap also fired the layer's short action — so invoking a macro ran it on top of an action the user had not asked for, and the sequence itself had no waits between steps, which made any app-switching macro act on the previous window. With no defensible use case left, the macro authoring UI and its host/Bridge plumbing were removed; the store's macro fields and the layer runner remain dormant and self-tested, so re-adding it later is a UI-plus-plumbing change.
- Removed Notes Deck and Notes navigation from the Host surface. Historical Notes data and source files remain only for migration and audit evidence; they are not compiled or user-reachable.
- Added a complete development runtime builder so Host, Bridge, the exact frozen Capture binary, and pinned NAudio libraries are colocated before microphone testing.
- Restored the verified RC003 F5 Hook fallback outside the frozen Capture path. Final RC003 keyboard isolation and full hardware acceptance remain required.
- Preserved Capture file version `1.2.1.0`, recording kernel `v1.0.3`, hold-to-talk, the device-controlled approximately 60-second boundary, stable voice parameters, and provider-direct text delivery.
- This is an unsigned candidate, not a formal stable release. Windows, DPI, VB-CABLE, browser, installer lifecycle, and full hardware matrices remain explicitly unverified.

- Fixed the add-application picker listing every running application without its icon. The running rows were built on a path that never assigned one, so what the user saw was a column of blank gaps beside the installed rows that did have icons — while nine of the ten running applications on the measured machine could produce an icon from their own executable. The same rows were also labelled with the raw process name ("catprox", "windowsterminal") for every application outside a curated map of six, and Vibe Flow's own process was offered as something to learn. Running rows now take their name and icon from the machine's catalogue when it has them, then from what the executable says about itself, then from the curated name, and the product's own processes are excluded; the icon lookup falls back to the shell for executables that carry no icon resource of their own (measured: Steam and BOOTICE). Verified by capturing the real dialog before and after: all seven running rows now show their icon and a readable name, and the application itself is gone from the list.
- Fixed the installed-application catalogue listing one application twice. The shell's AppsFolder contains desktop applications as well as packaged ones, so an application that already arrived through its start-menu shortcut was offered a second time under its shell identity — measured on this machine, CatProX appeared twice, once as `catprox` and once as `org.erb.vortex`, a registered AppUserModelID that is not a process name. Both sources are now keyed by process name, and an entry whose AppUserModelID is not a package resolves through the executable the shell exposes for it (`System.Link.TargetParsingPath`, empty for a real packaged application). The picker went from 167 rows to 99 on this machine, with no application lost.
- Verified on real hardware that dictation lands in the applications that matter, with the frozen Capture's own receipts: a Notepad session held for 7.7 s delivered `audio_delivered=True submitted=True` with zero dropped frames, and a ChatGPT desktop session held for 8.4 s did the same (`target_id=foreground-chatgpt code=OK`, `focused_edit=True` sampled three times, the provider panel never taking focus). Both delivered the text directly — the clipboard was never involved (`WETYPE PASTE FALLBACK payload_ready=False`), which is the path the product claims. This also corrects an earlier record: ChatGPT's composer can be validated as an editable target, so the input target is no longer unverified for it.
- Recorded as a deliberate non-goal, rather than an open question, that frame-hosted UWP applications are not supported as input targets. The class is real (measured on this machine: 设置's visible top-level window belongs to `ApplicationFrameHost.exe` while its content belongs to `SystemSettings.exe`), but the applications worth dictating into are not in it, and learning one fails closed: a capture that does not succeed within ten seconds logs `FAVORITE LEARN failed=true` and saves nothing, so the user gets "没有学到输入框，请重试" instead of a target that never matches.

## 1.5.0 - 2026-09-02

- Replaced custom-shortcut text entry with a guarded keyboard recorder. Users
  press the real combination, release all keys, review the normalized chord,
  and save it without typing names such as `control`.
- Added symmetric Host/Bridge validation for left/right modifiers,
  modifier-only chords, function/navigation/OEM keys, a five-key limit, and one
  non-modifier key. Unknown, partial, multi-main-key, and `Ctrl+Alt+Delete`
  combinations are rejected before persistence or execution.
- Added opt-in Smart Profiles. A Profile can bind installed or running
  applications by normalized process name and switch automatically when the
  foreground application remains stable for the debounce window.
- Added explicit fallback and lock behavior, deterministic duplicate-binding
  removal, Vibe Flow process exclusion, runtime health fields, homepage Profile
  feedback, and Profile-format v2 import/export for application bindings.
- Kept manual Profiles as the default. Multi Action, macros, app-specific
  conditional execution, and marketplace features remain deferred to protect
  the verified single-action routing model.
- Advanced application configuration to schema 32 and bridge configuration to
  schema 7. Shortcut recording, Profile switching, persistence, resource, UI,
  build, and frozen-voice gates passed before promotion to the public release.
- Preserved the exact validated `v1.0.3` recording kernel, stable voice profile
  v11, gain `1.0`, speech processing, `180 ms` drain, `CABLE Input`, WeChat
  `Ctrl+Win` toggle at `80 ms`, hold-to-talk behavior, and clipboard-free text
  delivery. The frozen Capture source and binary are unchanged.

## 1.4.0 incomplete preview archive - 2026-09-02

- Published only as a traceable preview archive. V1.5 completes and supersedes
  this intermediate shortcut workflow; ordinary users should not install V1.4.

- Kept the exact validated `v1.0.3` recording kernel, stable voice profile v11,
  WeChat `Ctrl+Win` toggle, and the approximately 60-second RC003 hardware
  boundary unchanged.
- Added manually selected shortcut Profiles for General navigation, Vibe
  Coding, Browser AI, and Terminal Agent workflows. Profiles contain shortcut
  mappings only and cannot modify microphone, audio routing, or transcription
  settings.
- Preserved every V1.3 user's active mapping as `My Shortcuts` during Profile
  migration instead of replacing it with an official template.
- Added Profile create, rename, delete, import, and export operations. Imported
  Profiles use a versioned, validated format and do not carry voice settings.
- Added an execution receipt from the device-scoped bridge to the home page,
  including the physical button, trigger, resolved action, active Profile,
  configuration revision, source, time, and real success or failure result.
- Changed Task View feedback to report the real `SendInput` result rather than
  treating a queued request as proof of execution.
- Isolated UI smoke tests from the real keyboard bridge so visual and resource
  tests cannot start hardware services or touch the user's production state.
- Rebuilt the local application picker around running windows, Windows
  AppsFolder, Start Menu shortcuts, App Paths, and valid installed-app registry
  entries. The picker now shows product names and resolves both EXE and packaged
  application icons while keeping manual EXE browsing as a fallback.
- Replaced Browser AI's physical-Left `Alt+Left` injection with the dedicated
  Windows Browser Back key. Existing schema-30 Left mappings are migrated so the
  physical Left key can no longer collide with the synthetic navigation action.
- Advanced application configuration to schema 31 and bridge configuration to
  schema 6. The missing direct shortcut recorder and Smart Profiles are
  completed in V1.5 rather than backported to this incomplete archive.

## 1.3.0 preview - 2026-09-01

- Kept the exact validated `v1.0.3` recording kernel and all stable voice parameters unchanged.
- Replaced the 11-step onboarding with five persisted user tasks covering the device, real RC003 input, VB-CABLE, a real provider dictation, and startup behavior.
- Replaced the failed Hook-to-Raw confirmation design after real hardware logs proved that suppressing an event in `WH_KEYBOARD_LL` prevents this Windows Bluetooth stack from delivering the corresponding `WM_INPUT` packet. UI action tests had bypassed that route and therefore produced false positives.
- Made device-scoped Raw Input the sole user-mode authority for non-voice actions. The device-blind Hook now passes those candidates through, so matching keys on an ordinary keyboard are never remapped and RC003 actions actually reach the executor.
- Kept the exact-device signed filter as an optional zero-side-effect path. Without it, V1.3 uses an explicit native-passthrough fallback: configured actions execute, ordinary keyboards remain unchanged, and the remote's original key effect may also occur.
- Added routing telemetry and self-check evidence for authority, isolation mode, RC003 edges, executed action edges, last action, Hook passthroughs, and optional filter state. The UI no longer treats process health or a direct action test as proof that the hardware route worked.
- Expanded the graphical shortcut page to the eight verified controls: four directions, Center, Home, TV, and Function short/long.
- Added actions for installed/running applications, HTTPS pages, editing, system, media, screenshots, and custom keyboard shortcuts.
- Made local application launch use a three-stage resolver: focus a running process, launch a valid EXE, then fall back to a Windows Start AppID.
- Fixed running APP activation so success is reported only after the target window is truly foreground; added an attached-input fallback for Windows focus restrictions.
- Prevented a failed foreground activation from falling through to EXE launch and creating a duplicate APP instance.
- Fixed hardware-candidate packaging so the pinned Capture binary is always named `VibeMicAtvvCapture.exe`, matching the Host runtime contract in a clean extraction.
- Retired Power from generated runtime mappings. Upgrades move an existing Power APP/URL action to an unused Home long press instead of silently losing it.
- Added General, Vibe Coding, and Media presets using only verified single actions.
- Added configuration import and latest-backup recovery while forcing the frozen stable voice profile during migration.
- Added privacy redaction for diagnostic exports, including user paths, device identities, addresses, URLs, and application targets.
- Added Light, Dark, and Follow Windows themes with live system-theme refresh.
- Enabled Per-Monitor V2 DPI awareness, 96-DPI design scaling, and working-area clamping so high Windows scaling uses real layout scaling instead of bitmap virtualization.
- Preserved all existing defaults. Power, Back, and independent Volume remain unavailable because this Windows/RC003 combination has not produced stable reports for them.
- Advanced app configuration to schema 29, onboarding to version 9, and bridge configuration to schema 5. The unsafe experimental compatibility route is retired and historical settings normalize to strict mode. A device-specific, heartbeat-guarded RC003 filter is under development for conflict-free mappings, but is excluded from user packages until WDK, signing, Secure Boot, and hardware gates pass. The preview remains isolated from the published `v1.2.1` release pending physical hardware acceptance.

## 1.2.1 user-friendly stable release - 2026-08-28

- Replaced the recording implementation with the exact `v1.0.3` capture kernel,
  while retaining the V1.2.1 UI heartbeat and recording-cue events.
- Restored the first formal-release WeChat profile: `Ctrl+Win`, toggle trigger,
  `80 ms` startup delay, toolbar-first activation, and provider-direct delivery.
- Returned session ownership to the RC003 natural ATVV stream: hold Record to
  start, release to stop, with the remote's approximately 60-second boundary.
- Removed the later `LONG DICTATION`, `MIC_EXTEND`, forced release-close, and
  physical-segment continuation paths from the capture binary.
- Kept capture single-instance locking, active-generation rejection, ordered
  audio decoding, stable audio parameters, and clipboard-free delivery.
- Removed Power, Back, and independent Volume mappings from defaults, migration,
  onboarding, the active shortcut UI, generated bridge configuration, and
  public documentation.
- Replaced the three legacy configurable controls with four single-action
  direction mappings arranged as a physical direction pad.
- Added Windows region capture (`Win+Shift+S`) as a tested optional action for
  any one of the four direction keys; defaults remain native directions.
- Added a visible one-click region-capture action in the direction editor while
  keeping the existing action list and native-direction reset.
- Replaced held-Alt task switching with persistent Windows Task View: TV opens
  Win+Tab, all four directions navigate, Enter confirms, and TV or timeout
  cancels safely.
- Made the white light theme the default and reduced saturation in the explicit
  dark palette.
- Replaced the theme drop-down with immediate Light/Dark buttons and rebuilt the
  active shell safely without restarting background capture or input services.
- Fixed a self-check false failure that still required the removed long-dictation
  runtime marker; diagnostics now validate the stable v1.0.3/v11 runtime.
- Advanced configuration to schema 25, onboarding to version 8, bridge config
  to schema 4, and all component file versions to 1.2.1.0.
- Rewrote validation and release documentation around the approximately
  60-second RC003 physical boundary and the reduced verified feature set.
- Added a release-specific illustrated tutorial and immutable EXE/ZIP/checksum
  links for every public version from V1.0.0 onward.

## 1.2.0 stabilization candidate - 2026-08-28

- Replaced the selectable record interaction with one enforced physical
  hold-to-talk flow: fresh DOWN starts, repeated DOWN is ignored, fresh UP
  stops once, and repeated UP is ignored.
- Added a deterministic `PushToTalkSessionModel` covering 100 start/stop cycles,
  quick release before audio, and exactly-once provider lifecycle invariants.
- Delayed provider startup until sustained decoded RC003 audio is present. A
  control-only stream or quarantined stop tail cannot open another provider
  microphone.
- Serialized the decoded-audio start commit with the release transition and
  rechecked session validity after input routing. A release can no longer be
  followed by a delayed provider trigger or a second microphone window.
- Kept physical ATVV segment continuation inside one logical provider
  generation while preserving generation-safe finalization, bounded transport
  recovery, disconnect cleanup, and the 30-minute software safety guard.
- Preserved stable profile v11: gain `1.0`, speech processing, `180 ms` drain,
  `CABLE Input`, automatic reversible routing, and the WeChat
  `Ctrl+Win+Shift` AI provider profile.
- Added schema 24 defaults and bridge schema 3 mappings for Function copy/paste,
  Power launcher, native directions, Enter, Backspace/browser back, Win+D,
  independent HID/Consumer volume with controlled repeat, and TV app/media
  actions.
- Added a graphical Power/Back/TV editor with short and long actions, physical
  key learning, app/URL/system/shortcut choices, immediate test, disable, and
  reset.
- Rebuilt onboarding as an 11-step saved flow with VB-CABLE reboot resume and a
  required real end-to-end dictation.
- Rebuilt diagnostics as 10 expected/actual/cause/next-action checks. Moved
  Windows hardware probing off the UI thread so the page renders immediately
  and refreshes after the probe or a Settings return.
- Changed user and bridge configuration writes to same-directory atomic
  replacement with `.bak` recovery, preventing reconnect-time partial JSON
  reads and preserving settings across restarts and upgrades.
- Added controlled held repeat for independent volume events and deterministic
  gesture tests at the fixed 650 ms threshold.
- Removed unwanted horizontal scrollbars, aligned page naming and version
  labels, and visually verified all five pages in dark and light themes plus all
  eleven onboarding steps.
- Rewrote the beginner guide, quick start, release notes, architecture, version
  metadata, and release copy around the hold-to-talk candidate. Physical 100
  cycle and 5-minute hold tests remain mandatory before publication.

## 1.2.0 initial candidate - 2026-08-26 (superseded)

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

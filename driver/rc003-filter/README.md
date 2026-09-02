# Vibe Flow RC003 input filter

This directory contains the device-specific Windows keyboard filter planned for
RC003 (`VID 0x2717`, `PID 0x32B8`). It exists to solve a Windows API boundary:
low-level keyboard hooks do not expose the originating device, while Raw Input
cannot suppress a device event globally after it has already reached the
foreground application.

The filter is deliberately narrow:

- The INF matches only the RC003 Bluetooth HID keyboard hardware ID.
- It is a per-device upper filter, never a keyboard class filter.
- It reports scan-code edges to user mode and suppresses only scan codes chosen
  by Vibe Flow.
- It does not launch applications, synthesize shortcuts, parse configuration,
  or process audio in kernel mode.
- Suppression is fail-open. A user-mode heartbeat must remain fresh; after two
  seconds without a heartbeat every key is passed to Windows unchanged.
- Closing the control handle or unloading the app disarms suppression.

The frozen ATVV audio Capture is outside this driver and must remain unchanged.

## Runtime contract

`VoxDeckInputBridge` treats this driver as optional. It opens
`\\.\VibeFlowRc003Filter`, validates the protocol and attached-device count,
arms a scan-code suppression policy, polls event batches, and sends a heartbeat
every 250 ms. Event I/O and action execution use separate threads so launching
an application cannot starve the kernel heartbeat.

While that channel is healthy, matching low-level-hook events are known to be
from an ordinary keyboard and pass through unchanged. If open, protocol,
heartbeat, queue, or device checks fail, the bridge disarms and closes the
driver handle and returns to its existing strict Raw Input fallback. A policy
generation change clears the kernel queue and invalidates queued user-mode
events before the new mapping becomes active.

The control protocol is defined in `src/public.h`. Its packed structure sizes
and IOCTL values are mirrored by bridge regression tests in
`scripts/VoxDeckInputBridge.cs` and `scripts/validate.js`.

## Build status

The source is an implementation candidate, not a distributable driver. The
current development computer does not have Visual Studio C++ Build Tools or the
Windows Driver Kit, so the driver has not yet passed compilation, Static Driver
Verifier, Driver Verifier, HLK, Secure Boot, or real-hardware installation.

The retired exclusive-GATT experiment must not be used as an alternative. On
this hardware Windows classifies the keyboard child as critical and `/force`
creates a reboot-pending disable state instead of a safe live handoff.

Run `Build-Driver.ps1` from a Visual Studio Developer PowerShell with:

- Visual Studio 2022 C++ Build Tools;
- Windows 11 SDK and WDK;
- KMDF support;
- `msbuild.exe`, `infverif.exe`, and `inf2cat.exe` available.

The project pins Microsoft's official `Microsoft.Windows.WDK.x64` and
`Microsoft.Windows.SDK.cpp.x64` NuGet packages at `10.0.26100.6584`. The
packages supply deterministic headers, libraries, and validation tools; the
Visual Studio WDK extension still provides the driver MSBuild targets.

```powershell
.\Build-Driver.ps1 -Configuration Release -Platform x64
```

`Build-Driver.ps1` restores the pinned package, discovers MSBuild through
`vswhere.exe`, and resolves validation tools from either the NuGet cache or the
Windows Kits registry. A CI runner therefore does not depend on an interactive
Developer PowerShell profile.

## Isolated Driver Lab workflow

The repository includes a manual-only workflow at
`.github/workflows/driver-candidate.yml`. Its first job runs on the disposable
GitHub-hosted `windows-2022` image and only proves that the source, INF, and
catalog compile. It does not install or sign the output, and its artifact is
retained for one day.

An optional second job runs on the physical Driver Lab computer. That runner
must have all of these safeguards:

- the standard labels `self-hosted`, `Windows`, and `X64`;
- the custom label `vibe-flow-driver-lab`;
- the machine environment variable `VIBE_FLOW_DRIVER_LAB=1`;
- Visual Studio 2022 C++ Build Tools, Windows 11 SDK, and WDK installed;
- no personal data and no use as a daily workstation.

Both jobs require a manual test-only acknowledgement, run source validation,
build the driver, and run InfVerif and Inf2Cat. Artifacts contain hashes,
provenance, signature state, and a conspicuous test-only warning. Neither job
installs the driver, submits it for signing, updates a GitHub Release, or alters
the normal app package.

The same candidate can be created interactively in the lab:

```powershell
.\New-DriverCandidate.ps1 -Configuration Release -Platform x64
```

The full Chinese setup, installation, rollback, and acceptance runbook is in
`docs/RC003_DRIVER_LAB_ZH.md`.

Do not enable Windows test-signing on a normal user machine. Public release
packages must use a Microsoft-signed catalog and must be validated with Secure
Boot and Memory Integrity enabled.

## Required release gates

1. Build with warnings treated as errors.
2. Run CodeQL, Static Driver Verifier, InfVerif, and Inf2Cat.
3. Install only on a separate driver-test computer first.
4. Verify every physical keyboard remains unaffected.
5. Verify RC003 press, release, hold, reconnect, sleep, wake, and Bluetooth
   restart behavior.
6. Kill the user-mode bridge during every gesture and confirm fail-open within
   two seconds.
7. Stress at least 10,000 packets and inspect queue overflow accounting.
8. Obtain Microsoft driver signing before packaging or advertising the feature.
9. Confirm the installer appends the service only to the exact RC003 device
   node and preserves every existing `UpperFilters` entry.
10. Uninstall and upgrade repeatedly, confirming that the RC003 keyboard and
    every physical keyboard continue working before, during, and after reboot.

## Reference boundary

The design follows the documented keyboard-connect callback and side-band
communication pattern described by Microsoft's `Windows-driver-samples`
`input/kbfiltr` sample. This implementation is written for Vibe Flow's RC003
contract and does not copy the Microsoft sample into the product tree.

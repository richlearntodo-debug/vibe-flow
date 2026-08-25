# Security Policy

Use GitHub's private vulnerability reporting for this repository when available. Include the affected version, reproduction steps, and impact. Do not include personal recordings, typed text, Bluetooth identifiers, or other sensitive data unless they are necessary and redacted.

Vibe Flow is designed to run as a standard user. Requests to add elevation, kernel drivers, process injection, silent downloads, or automatic execution of arbitrary commands require explicit threat modeling and review.

The in-app updater only accepts the official GitHub Release HTTPS path. It downloads `VibeFlow-Setup.exe` together with `SHA256SUMS.txt`, verifies SHA-256, and asks for confirmation again before launching the installer. A checksum mismatch or missing release asset is a hard failure. Existing configuration is preserved.

Formal release builds support optional Windows Authenticode signing. When signing is configured, every first-party executable and the installer must pass `signtool verify /pa /all`; a signing or verification failure stops packaging. Unsigned development builds remain supported and must be labeled as such.

Third-party components such as VB-CABLE must be downloaded from their official publisher. Vibe Flow does not redistribute VB-CABLE.

For WeChat Input Method, Vibe Flow first restores the exact text control through Windows UI Automation. If WeChat changes the clipboard instead of inserting text, Vibe Flow checks only the clipboard sequence number and whether a text format is available, then sends `Ctrl + V` to the captured text control. It does not open the clipboard, read recognized text, write it to logs, retain it, or upload it.

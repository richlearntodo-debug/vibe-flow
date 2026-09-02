# Security Policy

Use GitHub's private vulnerability reporting for this repository when available. Include the affected version, reproduction steps, and impact. Do not include personal recordings, typed text, Bluetooth identifiers, or other sensitive data unless they are necessary and redacted.

Vibe Flow is designed to run as a standard user. Requests to add elevation, kernel drivers, process injection, silent downloads, or automatic execution of arbitrary commands require explicit threat modeling and review.

The in-app updater only accepts the official GitHub Release HTTPS path. It downloads `VibeFlow-Setup.exe` together with `SHA256SUMS.txt`, verifies SHA-256, and asks for confirmation again before launching the installer. A checksum mismatch or missing release asset is a hard failure. Existing configuration is preserved.

Formal release builds support optional Windows Authenticode signing. When signing is configured, every first-party executable and the installer must pass `signtool verify /pa /all`; a signing or verification failure stops packaging. Unsigned development builds remain supported and must be labeled as such.

Third-party components such as VB-CABLE must be downloaded from their official publisher. Vibe Flow does not redistribute VB-CABLE.

The selected voice provider owns transcription and writes directly into the focused text field. Vibe Flow does not read transcript text, inspect the clipboard, synthesize paste, retain recognized content, or upload it. Users should review the privacy policy of their selected third-party provider separately.

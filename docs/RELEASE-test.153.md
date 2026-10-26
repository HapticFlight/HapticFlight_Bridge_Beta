# HapticFlight Bridge 0.1.0-test.153

## Installation

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.153.exe`.
2. Open the installed app and choose **Update components** if your DCS and SimHub paths are already configured.

## Change log

- **Fix:** Retired documentation left by an earlier Setup no longer blocks **Update components** as an unsigned package file.
- **Improves:** Existing installations can update their Hook and SimHub plugin without manually deleting old AH-6J guide files.
- **Test:** Upgrade over a prior installation containing `Docs\AH6-v1.2-FLIGHT-CUES.md`, then run **Update components**.

The downloaded package remains fully manifest-verified. The installed-package check permits only leftover Markdown documents and existing profile rollback files.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run.

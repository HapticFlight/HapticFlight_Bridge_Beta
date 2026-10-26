# HapticFlight Bridge 0.1.0-test.144

## Installation instructions

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.144.exe`.
2. Launch HapticFlight Bridge from the final Setup page or the Start menu.
3. The installer updates only HapticFlight Bridge. DCS Hook and SimHub plugin installation remain separate in-app choices.

## Change log

- New flight recordings use lossless gzip compression (`.jsonl.gz`) while retaining every raw 60 Hz telemetry packet.
- Existing `.jsonl` recordings remain available, importable, and analyzable.
- Stopping a recording now asks the recorder to finalize the compressed file before using a forced-stop fallback.
- Fixed the saved-flights list so it has a bounded, mouse-wheel-scrollable viewport and includes every compatible recording.

## Beta notice

This local test build is unsigned. Windows App Control may block it on protected PCs; do not disable security protections to force it to run.

# HapticFlight Bridge 0.1.0-test.141

## Installation instructions

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.141.exe`.
2. Launch HapticFlight Bridge from the final Setup page or the Start menu.
3. The installer updates only HapticFlight Bridge. DCS Hook and SimHub plugin installation remain separate in-app choices.

## Change log

- Fixed the Flights analyzer resize crash caused by forcing an infinite-height layout measurement while charts were rendering.
- The analysis page now uses the available work-area height and scrolls its content, avoiding invalid child sizes during a resize.
- Added a UI contract check that rejects the unsafe layout pattern.

## Beta notice

This local test build is unsigned. Windows App Control may block it on protected PCs; do not disable security protections to force it to run.

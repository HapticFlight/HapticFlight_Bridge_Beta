# HapticFlight Bridge 0.1.0-test.150

## Installation instructions

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.150.exe`.
2. The Setup update replaces HapticFlight Bridge only. Use the in-app controls
   to refresh the DCS Hook and SimHub plugin when the dashboard requests it.
3. Import the current profile from the top-level `Profiles` folder. Use
   `Profiles\Archive\AH-6J` only to deliberately roll back an AH-6J profile.

## Change log

- Keeps current profiles at the top-level `Profiles` folder.
- Moves the previous AH-6J v1.1 calibration into `Profiles\Archive\AH-6J`.
- Replaces the misleading top-right “Updates available” label with
  “Refresh integrations” when only the installed DCS Hook or SimHub plugin
  differs from the current app package.
- The GitHub updater still separately reports a newer application release.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run.

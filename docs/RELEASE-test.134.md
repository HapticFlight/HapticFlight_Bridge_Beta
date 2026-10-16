# HapticFlight Bridge 0.1.0-test.134 — unsigned Setup beta

Download **`HapticFlight-Bridge-Setup-0.1.0-test.134.exe`** and run it directly.
There is no ZIP to extract and no source-code folder to navigate. Setup asks
for your DCS Saved Games profile and SimHub folder, installs HapticFlight Bridge
and its integration, and offers **Launch HapticFlight Bridge** on the final page.
Close DCS and SimHub before installing. The separate `.exe.sha256` asset lets
you verify the downloaded installer.

**This beta is unsigned.** Windows App Control may block Setup or the installed
app on protected PCs. Do not disable security protections to force it to run.
This release does not have a verified publisher signature.

The previous test.133 ZIP remains available for existing ZIP users, but this
Setup EXE is the intended new-user download. Existing ZIP installations must
download Setup manually to switch formats; the legacy ZIP updater does not
automatically migrate to Setup.

Setup includes a dashboard uninstall action and keeps DCS-to-SimHub telemetry
working while the launcher is closed. Offline package, installer, integration,
and updater checks were run; live DCS/SimHub hardware output and protected-PC
installation remain unverified for this release.

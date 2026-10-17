# HapticFlight Bridge 0.1.0-test.136 — corrected Setup installer

Download HapticFlight-Bridge-Setup-0.1.0-test.136.exe and run it directly.
There is no ZIP to extract. Setup installs HapticFlight Bridge and offers
Launch HapticFlight Bridge on the final page.

This replaces the withdrawn test.134 and test.135 Setup candidates. test.134
crashed before the wizard opened because of an unsupported path constant.
test.135 opened the wizard, but the elevated integration helper mishandled
a folder path ending in a backslash. Both issues are corrected here.

Close DCS and SimHub before installing. Choose the DCS Saved Games profile you
use and the folder containing SimHubWPF.exe. The bridge app can be closed while
DCS and SimHub exchange telemetry.

This beta is unsigned. Windows App Control may block Setup or the installed
app on protected PCs. Do not disable security protections to force it to run.
Live DCS/SimHub hardware output and protected-PC installation remain unverified.

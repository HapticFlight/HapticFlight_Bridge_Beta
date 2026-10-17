# HapticFlight Bridge 0.1.0-test.135 — Setup startup fix

Download **`HapticFlight-Bridge-Setup-0.1.0-test.135.exe`** and run it directly;
there is no ZIP to extract. This replaces the broken test.134 Setup EXE.

test.134 could show `Internal error: Unknown constant "userprofile"` before its
wizard opened. This release fixes the DCS Saved Games default path lookup and
was tested by opening the actual Setup wizard before publication.

Close DCS and SimHub before installing. Setup asks for your DCS Saved Games
profile and SimHub folder, installs the integration, and offers **Launch
HapticFlight Bridge** on the final page. The app can be closed while DCS and
SimHub continue exchanging telemetry.

**This beta is unsigned.** Windows App Control may block Setup or the installed
app on protected PCs. Do not disable security protections to force it to run.
Live DCS/SimHub hardware output and protected-PC installation remain unverified.

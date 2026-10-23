# HapticFlight Bridge

The app/installer release number is shown on the About panel and in
`package-info.json`. The built-in haptic engine and SimHub aircraft profiles
have separate revision numbers; those do not represent additional app
installers or downloads.

HapticFlight Bridge connects DCS World telemetry to SimHub and ShakeIt Bass
Shakers. Setup installs only the application. In the application, choose your
DCS Saved Games profile and SimHub folder, then install or remove the isolated
DCS GameGUI Hook and SimHub plugin independently. It does not modify `Export.lua`.

Start the application from the Start menu to check setup status, record flights,
repair the integration, or check for updates. During a flight, the application
may be closed: DCS sends directly to the plugin running inside SimHub.

To use tactile output, start SimHub and DCS, import the matching aircraft
profile from the installed `Profiles` folder, and enable that profile in
ShakeIt Bass Shakers. Select one aircraft profile at a time and begin with
the amplifier and SimHub gain low. See `Docs\SIMHUB-PROFILES.md` for the profile
map and limitations.

To repair the integration, close DCS and SimHub, open HapticFlight Bridge, and
choose **Repair setup**. To remove a component, use its **Uninstall** button in
the app. To remove the application and any remaining integration, close both
programs and choose **Uninstall HapticFlight Bridge** on the main dashboard,
or use Windows **Installed apps**. Recordings in Documents and
imported SimHub profiles are preserved.

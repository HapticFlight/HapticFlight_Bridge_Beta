# DCS Extra Telemetry 0.1.0-test.129

This complete test package contains Haptic Engine 1.7.1 and the same six-aircraft
`v2.0.0-test.19` SimHub profiles as test.128. The haptics are unchanged; this
release replaces the launcher's interface.

## New launcher interface

- A dark glass dashboard replaces the light sidebar layout. The top bar holds
  Dashboard, Flights, Aircraft, Setup and Support, a flight search and the
  setup status.
- The dashboard summarises your recordings: flights recorded, flight time,
  effects fired and peak overlap for this week, this month or all time, with
  flight time per day, effects by category and the last flight's intensity.
- A Setup card on the dashboard shows each component's state and the one next
  action needed.
- Flight analysis now opens on the Flights page instead of a separate window.
  Previous and next effect navigation and the effect report export moved with it.
- Dashboard summaries are cached in
  `%LocalAppData%\DcsExtraTelemetry\dashboard-summaries.json`, so each recording
  is analysed once. Deleting the file only makes the next launch re-analyse.
- The launcher now has a single dark theme; the appearance setting was removed.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
test.19 profile. Disable overlapping older profiles. The launcher's retained
rollback baseline is `0.1.0-test.121`; test.18 and earlier profiles remain in
`Profiles\History`.

This normal ZIP contains no self-extracting wrapper. The launcher and monitor
are unsigned test binaries; Windows may still require an allow or trust
decision until trusted Authenticode signing is configured.

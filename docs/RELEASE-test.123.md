# DCS Extra Telemetry 0.1.0-test.123

This build corrects the Home screen layout while retaining Haptic Engine 1.3.0
and the separate Haptic Engine v2.0.0-test.12 Realism profiles from test.122.

## Home screen correction

- Removed the star-sized Home row that expanded into a large empty panel as the
  window grew.
- Replaced the recursive list of all 37 bundled profile files with a compact
  summary of the current and rollback profile counts.
- Kept the current package, Haptic Engine, installed plugin, and rollback
  versions visible.
- Preserved the existing install, repair, folder, navigation, and accessibility
  handlers.

## Additional UH-60 validation

A second test.120 recording was replayed through Engine 1.3.0: 38,234 valid
packets over 898.541 seconds at 42.551 Hz, with three touchdown and three
liftoff episodes. It adds both UH-60 door-gun channels, 21 gun event episodes,
seven rocket events, five derived ground-bump events, and eight settling events.
The replay passed without malformed packets and exercised the priority mixer.

No profile gains were changed from formula replay alone. Seat-level calibration
still requires repeatable hardware testing. The newest profiles remain test.12;
test.7 through test.11 remain bundled as separate rollback profiles.

## Package

Use the normal `DcsExtraTelemetry-0.1.0-test.123.zip` asset. The withdrawn
self-extracting wrapper is not included. Application rollback remains
`0.1.0-test.121`.

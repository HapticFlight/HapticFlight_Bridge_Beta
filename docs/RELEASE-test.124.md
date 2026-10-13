# DCS Extra Telemetry 0.1.0-test.124

This build repairs Flights navigation and includes the compact Home layout first
released in test.123. Haptic Engine 1.3.0 and the separate Haptic Engine
v2.0.0-test.12 Realism profiles are unchanged.

## Flights fixes

- Added a visible **New recording** action to the recording analyzer and the
  embedded flight dashboard.
- Closing or leaving the analyzer now clears the loaded analysis and restores
  the recorder controls, including the **Start recording** button.
- Restored the previous and next effect arrows on the analyzer's **Combined
  load** tab.
- Kept the arrows fixed in place and limited navigation to effects currently
  shown by the active filter.

## Home layout included

- The Home page sizes to its content instead of stretching an empty panel to
  fill the window.
- Bundled profiles are summarized by current and rollback counts rather than
  showing the entire profile-history file list.
- The package version remains visible in the title bar and Components card.

## Supplied UH-60 recording

The supplied recording from September 20 contains 54,052 valid UH-60L DAP
packets over 1,440.589 seconds at 37.521 Hz, with no malformed lines, sequence
gaps, duplicate sequences, or contact-state mismatches. It includes seven
touchdown pulses, eight liftoff pulses, one light-gun event, eleven chaff
events, and one door-movement event. All declared capability groups were
present throughout the recording.

No profile gains were changed from formula replay alone. Seat-level calibration
still requires repeatable hardware testing. The newest profiles remain test.12;
test.7 through test.11 remain bundled as separate rollback profiles.

## Package

Use the normal `DcsExtraTelemetry-0.1.0-test.124.zip` asset. No self-extracting
wrapper is included. Application rollback remains `0.1.0-test.121`.

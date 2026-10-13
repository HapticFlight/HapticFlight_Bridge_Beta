# DCS Extra Telemetry 0.1.0-test.131

This portable test release includes the latest application, launcher, monitor,
the shared Haptic Engine aircraft profiles, and the CH-47F integration profile
and telemetry mapping.

## CH-47F

- Adds CH-47F (`CH-47Fbl1`) aircraft-family identification and a provisional
  ground-contact model.
- Adds CH-47F rotor-speed properties from the mean of the two engine RPM
  percentages, scaled by the module SFM nominal 225 RPM. This is explicitly an
  engine-RPM proxy, not a direct rotor sensor.
- Configures the six-blade blade-pass cue and reads collective from draw
  argument 396. The supplied flight log did not contain rotor or collective
  outputs, so those mappings still need confirmation from the installed Hook.
- Includes `DCS-CH-47F-Haptic-Engine-v2.0.0-test.2.siprofile` for manual
  import. Use it with this release's Hook and SimHub plugin; the profile alone
  cannot produce the new rotor properties.

The older CH-47F compatibility profile remains available as a rollback option.
Use only one CH-47F profile at a time and disable any overlapping profiles.

## Other changes

Includes the existing `0.1.0-test.130` application and launcher changes, all
existing aircraft profiles, and the full portable setup and rollback tools.
Unrelated uncommitted output and worktree files are not part of this release.

## Validation limits

The Release production build passed. No automated test suite or live DCS/seat
test was run for this package. The CH-47F rotor mapping is derived from engine
RPM, and the user-provided log contains neither direct rotor RPM nor
collective fields. Verify the live properties and tactile result on the DCS
test computer before relying on those effects.

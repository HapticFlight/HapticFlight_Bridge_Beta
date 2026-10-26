# AH-6J/MH-6J v1.1 calibrated profile

This profile requires Haptic Engine 1.8.0, the matching DCS Hook, and the
CKDesigns AH-6J/MH-6J v1.1 module. It is intentionally module-specific.

## Confirmed module inputs

- Main rotor: 470 RPM, five blades; governed blade pass is 39.17 Hz.
- External argument 37: main-rotor rotation/blur. The Hook derives rotor RPM
  only when this argument advances as a plausible phase signal.
- Cockpit argument 311: torque. It is used for power/load and never as rotor
  speed.
- Cockpit argument 210: collective.
- Skid arguments: FL 202, FR 204, RL 201, RR 203.
- M134 left/right subtypes: `4:6:10:13600` / `4:6:10:13601`, 4,000 rpm.

## Recording completeness

The raw recorder mirrors every Hook packet. Engine 1.8.0 additionally records
`values.dynamics.ah6RotorPhaseArgument`, `ah6TorqueArgument`, and
`ah6CollectiveArgument`, alongside their derived rotor RPM/source, the four
skid compressions, AGL, speed, collective, torque, weapon subtype, and all
haptic envelopes. Once one flight is recorded with this update, later profile
changes can be evaluated against that recording without flying the route again.

Existing recordings cannot contain fields that their older Hook did not send;
they remain valid for ETL, skid, touchdown, and weapon-envelope comparison but
cannot prove the newly direct rotor-phase path.

## Installation

Close DCS and SimHub. Install only the package's Hook and SimHub DLL/PDB using
the normal HapticFlight Bridge workflow, restart both applications, then import
the supplied AH-6J profile. Do not stack it with an older AH-6J profile.

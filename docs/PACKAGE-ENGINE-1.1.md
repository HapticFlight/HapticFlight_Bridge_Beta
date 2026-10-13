# Engine 1.1.0 plugin and profiles

This package contains the Engine 1.1.0 SimHub plugin DLL/PDB, six matching
ground-effects test.6 profiles, documentation, and a file manifest. It contains
no executable wrapper, installer, application launcher, monitor, or DCS Hook.

Close SimHub. Back up the existing project DLL/PDB from the SimHub folder, then
copy this package's `SimHub` folder contents into that folder. Restart SimHub,
verify `DCSExtra.Haptic.EngineVersion` reports `1.1.0`, then import one matching
test.6 profile and disable overlapping older profiles.

The former self-extracting test.121 application was withdrawn after a Defender
detection. Do not restore or bypass it. Ground-effect tuning remains provisional
pending live DCS and shaker calibration.

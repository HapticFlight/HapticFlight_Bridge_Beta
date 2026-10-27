# AH-6J and MH-6J ShakeIt profile

The current AH-6J profile is
`DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile`.
It is an AH-6J-specific tune; MH-6J retains its separately named profile.

For each revision's concise Change, Improves, and Test summary, see the
[profile change log](../../docs/PROFILE-CHANGELOG.md).

It uses the existing AH/MH-6 adapter's dynamic rotor frequency, with 1-rev,
2-rev, and five-rev blade-pass layers. Ground feedback comes from the four
native skid contacts—not the wheel or tailwheel effects used by the UH-60. A
restrained single-skid cue covers low-hover contact, including the normal
left-skid-first set-down, while firm full-airframe contact uses the main
touchdown envelope.

This is a flight-test baseline. The adapter uses the available live rotor phase
source when present; capture a normal startup, hot idle, hover, cruise,
descent, and landing before making gain changes.

## Generate and validate

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Ah6Profile.ps1
Get-Content -Raw '.\profiles\ah6\DCS World - AH-6J MH-6J - DCSExtra Baseline v0.1.siprofile' | ConvertFrom-Json
```

Import the generated `.siprofile` into SimHub ShakeIt. It requires the
`DcsExtraTelemetryPlugin.DCSExtra.*` property namespace and a current plugin
build with AH/MH-6 support.

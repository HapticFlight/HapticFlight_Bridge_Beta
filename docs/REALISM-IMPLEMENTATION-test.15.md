# Realism test.15: derived VRS-risk

Test.15 requires Haptic Engine 1.6.0 and is a separate importable profile set.
It does not replace test.14 or any earlier profile.

## VRS-risk model

`DCSExtra.VrsRiskIntensity` is derived from a sustained coupling of low forward
speed, descent rate, collective and rotor-load demand, governed rotor speed,
optional AGL ground-effect attenuation, and observed linear/angular motion. The
model delays entry, decays on recovery, and resets with the aircraft/session
state. `DCSExtra.SettlingWithPowerCue` publishes the same value for formula
compatibility.

DCS does not export a vortex-ring-state flag. The signal therefore represents
VRS **risk**, never confirmed VRS. The test.15 VRS-risk texture is disabled by
default and labelled optional. If enabled, it supplies a restrained irregular
airframe texture rather than a touchdown, failure, or directional-control cue.

## Validation

- `node tests\Test-RealismStages.js`
- `dotnet msbuild tests\DcsExtraTelemetry.TransportSmokeTest\DcsExtraTelemetry.TransportSmokeTest.csproj /t:Rebuild /p:Configuration=Release`
- `tests\DcsExtraTelemetry.TransportSmokeTest\bin\Release\DcsExtraTelemetry.TransportSmokeTest.exe`
- `dotnet msbuild tests\DcsExtraTelemetry.HapticEngineTests\DcsExtraTelemetry.HapticEngineTests.csproj /t:Rebuild /p:Configuration=Release`
- `tests\DcsExtraTelemetry.HapticEngineTests\bin\Release\DcsExtraTelemetry.HapticEngineTests.exe`

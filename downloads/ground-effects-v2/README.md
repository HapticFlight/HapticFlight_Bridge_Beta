# Ground-effects test update

Haptic Engine **1.1.0** and six profiles **v2.0.0-test.6**.

The prior root-level test.121 self-extracting EXE was withdrawn after a Windows
Defender detection. The complete `DcsExtraTelemetry-0.1.0-test.121.zip` is
attached to the test.121 release and contains the standard launcher and monitor
under `Tools`, alongside the DCS Hook, SimHub plugin, profiles, and scripts.
Microsoft Defender scanned the ZIP with no threats found using definition
`1.459.273.0` on September 18, 2026.

The profiles below are importable and require the Engine 1.1.0 plugin from that
complete ZIP. Keep older profiles disabled while testing. No output-device
routing is included in profiles.

| Aircraft | Profile |
| --- | --- |
| UH-60L / DAP | [test.6](DCS-UH60L-Haptic-Engine-v2.0.0-test.6.siprofile) |
| AH-64D | [test.6](DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.6.siprofile) |
| AH-6J | [test.6](DCS-AH-6J-Haptic-Engine-v2.0.0-test.6.siprofile) |
| MH-6J | [test.6](DCS-MH-6J-Haptic-Engine-v2.0.0-test.6.siprofile) |
| UH-1H | [test.6](DCS-UH-1H-Huey-Haptic-Engine-v2.0.0-test.6.siprofile) |
| OH-58D, provisional | [test.6](DCS-OH-58D-Kiowa-Haptic-Engine-v2.0.0-test.6.siprofile) |

Adds compression-based ground bumps, shaped contact/settling, separate rolling
and roughness, real-bounce rearming, and longitudinal skid sliding. Preserves
all non-ground tuning from each aircraft's latest published baseline, including
AH-6J rotor test.4 and Apache test.5 gun balance.

[Implementation, source limits, local-build and live-test checklist](../../docs/GROUND_EFFECTS.md).
Synthetic engine tests and formula evaluation are not physical shaker calibration.

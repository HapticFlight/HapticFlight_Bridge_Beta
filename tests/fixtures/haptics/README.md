# Haptic engine fixtures

`touchdown-scenarios.json` is a compact, synthetic detector contract. It does
not replace recorded DCS telemetry or live seat validation. Recorded fixtures
must be sanitized excerpts from controlled flights and added per aircraft only
after their module/source versions are documented.

`property-manifest.txt` is the ordered, machine-checked public signal-family
manifest. Each family expands to the common state fields and a matching
capability property; intentional contract changes must update the registration,
manifest, documentation, and contract test together.

Required recorded coverage before profile promotion:

- parked start, takeoff, gentle/firm/asymmetric/bounce landing;
- taxi, braking, wheel or skid detail, pause, packet gap, and aircraft change;
- rotor lifecycle, steady-flight noise, maneuver, weapons, countermeasures,
  and supported mechanical movement.

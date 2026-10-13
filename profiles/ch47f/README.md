# CH-47F Haptic Engine profile

The version 2 profile uses the same DCSExtra DD Haptic Engine telemetry contract as the other V2 helicopter profiles. Its rotor-related cues are explicitly marked as proxies and remain provisional until live properties and tactile output are checked.

## Evidence used

- `CH-47F/CH-47F.lua` defines `rotor_RPM = -225.0`, `tail_rotor_RPM = 225.0`, `blades_number = 6`, and two rotor locations. The RPM sign describes direction; the nominal magnitude is 225 RPM.
- `CH-47F/CH-47F.lua` identifies control draw arguments 395–398 as stick, collective, pedals, and toe brakes; the Hook reads collective argument 396.
- The full flight recording `DcsExtraTelemetry-20261004-205527.jsonl` contains 6,281 packets with DCS type `CH-47Fbl1` over 206.6 seconds of model time. Both engine RPM percentage channels are present and match in every packet; their mean ranges from 73.97% to 94.42% (median 82.66%). Grounded samples have a median of 78.11%; airborne samples have a median of 89.53%.
- That recording contains no rotor RPM, main-rotor Hz, blade-pass Hz, rotor-source, collective-position, or collective-movement-rate fields. `rotorDynamics`, `powertrainEffects`, and `controlPositions` are false in every packet. It therefore validates the engine RPM source, but does not validate a rotor or collective mapping.

## Mapping and profile behavior

The Hook averages the two engine RPM percentage readings, scales them by the module's nominal 225 RPM, and labels the result `CH47EngineRpmProxy`. It reads collective from draw argument 396. The module's configured six-blade count is used to derive blade-pass frequency. The haptic model marks the aircraft provisional and uses the verified three-point wheel data path from the recording for generic ground interaction. The draw-argument read still needs confirmation in a recording made with the updated Hook.

Version 2 includes the same DD 1-rev, 2-rev, and blade-pass bass layers plus shared powertrain, flight-motion, wheel/landing, generic weapon, and countermeasure effects. Module-specific starter/APU signals and other aircraft's weapon effects are excluded.

Select the profile manually in SimHub and pair it with the matching updated Hook/plugin. The RPM-to-rotor relationship, six-blade blade-pass interpretation, draw-argument availability, and final seat output still need live verification. The earlier test.1 compatibility profile is retained for rollback.

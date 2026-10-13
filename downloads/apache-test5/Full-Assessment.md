# Full Apache recording assessment

See README.md for findings, source limitations and implementation details. All effect levels below are reconstructed, gain-weighted profile amplitudes before downstream audio processing. Event-envelope estimates are provisional.

## All 38 effects

| Effect | Gain before / after | Maximum before / after | Firing maximum before / after | Carrier Hz before / after (median) |
|---|---:|---:|---:|---:|
| APU machinery | 16 / 16 | 0 / 0 | 0 / 0 | 55 / 55 |
| Engine starters | 24 / 24 | 0 / 0 | 0 / 0 | 48 / 48 |
| Rotor spool transition | 26 / 26 | 5.33712 / 5.33712 | 0 / 0 | 18.2739 / 18.2739 |
| AH-64 rotor 1-rev airframe pulse | 30 / 30 | 18 / 18 | 17.3332 / 17.3332 | 4.56849 / 4.56849 |
| AH-64 rotor 2-rev structural harmonic | 24 / 24 | 12.48 / 12.48 | 11.2676 / 11.2676 | 9.13697 / 9.13697 |
| AH-64 main rotor blade-pass 4-rev | 36 / 36 | 22.32 / 22.32 | 22.2977 / 22.2977 | 18.2739 / 18.2739 |
| AH-64 blade slap (descent BVI cue) | 18 / 18 | 5.58304 / 5.58304 | 0 / 0 | 18.2739 / 18.2739 |
| Transmission load | 16 / 16 | 2.08683 / 2.08683 | 1.91456 / 1.91456 | 22 / 22 |
| Engine governor load response | 14 / 14 | 6.14381 / 6.14381 | 0 / 0 | 22 / 22 |
| Collective movement thump | 14 / 14 | 5.97409 / 5.97409 | 0.504394 / 0.504394 | 22 / 22 |
| Low rotor droop roughness | 20 / 20 | 1.61984 / 1.61984 | 0.957792 / 0.957792 | 15 / 15 |
| Maneuver load and vertical energy | 26 / 26 | 12.3522 / 12.3522 | 7.5724 / 7.5724 | 18 / 18 |
| Translational lift transition | 11 / 11 | 2.71803 / 2.71803 | 0 / 0 | 13 / 13 |
| Turbulence body thump | 16 / 16 | 0.738678 / 0.738678 | 0 / 0 | 16 / 16 |
| Airframe buffet texture | 5 / 5 | 0 / 0 | 0 / 0 | 22 / 22 |
| High-speed airframe texture | 10 / 10 | 0 / 0 | 0 / 0 | 30 / 30 |
| Powered low-speed descent cue | 14 / 14 | 0.00247383 / 0.00247383 | 0 / 0 | 12 / 12 |
| Hard maneuver pulse | 18 / 18 | 0 / 0 | 0 / 0 | 18 / 18 |
| Airframe impact pulse | 30 / 30 | 0 / 0 | 0 / 0 | 18 / 18 |
| Touchdown body impact | 34 / 34 | 6.72571 / 6.72571 | 0 / 0 | 22 / 22 |
| Main wheel contact detail | 17 / 17 | 8.16 / 8.16 | 0 / 0 | 30 / 30 |
| Tailwheel settle tick | 18 / 18 | 9.9 / 9.9 | 0 / 0 | 22 / 22 |
| Liftoff unloading | 12 / 12 | 4.08 / 4.08 | 0 / 0 | 14 / 14 |
| Ground roll base | 18 / 18 | 0.198482 / 0.198482 | 0 / 0 | 38 / 38 |
| Suspension surface roughness | 18 / 18 | 8.99843 / 8.99843 | 0 / 0 | 23 / 23 |
| Wheel shimmy | 10 / 10 | 0.0533771 / 0.0533771 | 0 / 0 | 38 / 38 |
| Tailwheel caster | 9 / 9 | 0 / 0 | 0 / 0 | 17 / 17 |
| Gear-borne rotor vibration | 8 / 8 | 0.632618 / 0.632618 | 0 / 0 | 13 / 13 |
| Ground support rocking | 10 / 10 | 2.945 / 2.945 | 0 / 0 | 16 / 16 |
| Wheel braking load | 24 / 24 | 0 / 0 | 0 / 0 | 22 / 22 |
| AH-64 M230 chain-gun rhythm | 24 / 12 | 15.6727 / 5.5974 | 15.6727 / 5.5974 | 25 / 10.4167 |
| AH-64 M230 low-body thump | 10 / 4 | 0 / 0.677985 | 0 / 0.677985 | 12.5 / 5.20833 |
| AH-64 Hydra rocket launch kick | 32 / 32 | 0 / 0 | 0 / 0 | 32 / 32 |
| AH-64 Hydra rocket body reaction | 20 / 20 | 0 / 0 | 0 / 0 | 18 / 18 |
| AH-64 missile launch body | 34 / 34 | 0 / 0 | 0 / 0 | 20 / 20 |
| AH-64 missile motor ignition | 18 / 18 | 0 / 0 | 0 / 0 | 28 / 28 |
| Flare dispenser pulse | 11 / 11 | 0 / 0 | 0 / 0 | 45 / 45 |
| Chaff dispenser pulse | 9 / 9 | 0 / 0 | 0 / 0 | 35 / 35 |

## All 178 observed fields

Numeric values use the recording field names and raw units; an existing unit label is not independently validated. Text/Boolean values are listed exactly. Missing counts are relative to the 6,885 packets.

| Field | Min / observed values | Median | 95th percentile | Max | Missing |
|---|---|---:|---:|---:|---:|
| values.angleOfAttackRad | -179.988 | -2.39877 | 156.05 | 179.999 | 0 |
| values.acceleration.y | 0.891678 | 0.996909 | 1.17317 | 1.56207 | 0 |
| values.acceleration.x | -0.300081 | 0.0471845 | 0.091976 | 0.487421 | 0 |
| values.acceleration.z | -0.138476 | 0.00312752 | 0.0483176 | 0.267236 | 0 |
| values.verticalVelocityMps | -14.8715 | 0.0000127618 | 5.04406 | 10.1629 | 0 |
| values.indicatedAirSpeedMps | 1.6369 | 22.6534 | 42.3481 | 45.6315 | 0 |
| values.dynamics.normalGDeviation | -0.108322 | -0.00309074 | 0.17317 | 0.562069 | 0 |
| values.dynamics.rotorRunning | [true] |  |  |  | 0 |
| values.dynamics.collectivePosition | 0.0620575 | 0.487473 | 0.599106 | 0.599106 | 0 |
| values.dynamics.bladeSlapIntensity | 0 | 0 | 0.298254 | 0.595108 | 0 |
| values.dynamics.mainRotorRpm | 245.661 | 274.109 | 284.997 | 286.127 | 0 |
| values.dynamics.transmissionLoadIntensity | 0.0385659 | 0.338831 | 0.43162 | 0.46581 | 0 |
| values.dynamics.rotorLoadIntensity | 0.0452586 | 0.357487 | 0.439949 | 0.474412 | 0 |
| values.dynamics.engine2StarterActive | [false] |  |  |  | 0 |
| values.dynamics.turbulenceIntensity | 0 | 0 | 0 | 0.255484 | 0 |
| values.dynamics.mainRotorHz | 4.09436 | 4.56849 | 4.74994 | 4.76878 | 0 |
| values.dynamics.airframeBuffetIntensity | 0 | 0 | 0 | 0.128296 | 0 |
| values.dynamics.bladePassHz | 16.3774 | 18.2739 | 18.9998 | 19.0751 | 0 |
| values.dynamics.maneuverLoadIntensity | 0.00000715256 | 0.00885812 | 0.115447 | 0.374713 | 0 |
| values.dynamics.lateralManeuverIntensity | 0 | 3.04594e-16 | 0.260573 | 0.999956 | 0 |
| values.dynamics.mainRotorRpmPct | 85.004 | 94.8475 | 98.6148 | 99.0057 | 0 |
| values.dynamics.engine1StarterActive | [false] |  |  |  | 0 |
| values.dynamics.apuRunning | [false] |  |  |  | 0 |
| values.dynamics.lowRotorRpmWarning | [false] |  |  |  | 0 |
| values.dynamics.airframeImpactPulse | 0 | 0 | 0 | 0 | 0 |
| values.dynamics.hardManeuverPulse | 0 | 0 | 0 | 0 | 0 |
| values.dynamics.rotorDroopIntensity | 0 | 0.0344183 | 0.10198 | 0.136279 | 0 |
| values.dynamics.translationalLiftTransition | 0 | 0 | 0 | 0.588318 | 0 |
| values.dynamics.negativeGIntensity | 0 | 0.00618148 | 0.0943581 | 0.216643 | 0 |
| values.dynamics.settlingWithPowerCue | 0 | 0 | 0 | 0.000519713 | 0 |
| values.dynamics.normalG | 0.891678 | 0.996909 | 1.17317 | 1.56207 | 0 |
| values.dynamics.maneuverResponseIntensity | 0 | 3.07314e-56 | 0.0227936 | 0.382941 | 0 |
| values.dynamics.positiveGIntensity | 0 | 0 | 0.247386 | 0.802956 | 0 |
| values.dynamics.rotorSource | ["AH64EngineRpmProxy"] |  |  |  | 0 |
| values.dynamics.controlResponseIntensity | 0 | 0.000686787 | 0.0896077 | 0.843081 | 0 |
| values.dynamics.rotorSpoolIntensity | 0 | 0 | 0 | 0.255967 | 0 |
| values.dynamics.engineGovernorResponseIntensity | 0 | 0.00605411 | 0.194935 | 1 | 0 |
| values.dynamics.collectiveMovementRate | 0 | 0 | 0.110176 | 1.05385 | 0 |
| values.mechanical.gearPosition | 1 | 1 | 1 | 1 | 0 |
| values.mechanical.flapsPosition | 0 | 0 | 0 | 0 | 0 |
| values.mechanical.speedbrakePosition | 0 | 0 | 0 | 0 | 0 |
| values.angularVelocity.y | -0.375362 | 0.00000367358 | 0.147826 | 0.292109 | 0 |
| values.angularVelocity.x | -0.227279 | -0.00000445709 | 0.0866756 | 0.251995 | 0 |
| values.angularVelocity.z | -0.107 | 0.00000125009 | 0.111662 | 0.176168 | 0 |
| values.cockpit.countermeasureArmed | [false] |  |  |  | 0 |
| values.cockpit.masterArmOn | [false,true] |  |  |  | 0 |
| values.cockpit.rocketPairMode | [false] |  |  |  | 0 |
| values.cockpit.weaponSelectorName | ["NONE","GUN ACTION","RKT ACTION","MSL ACTION","ATA ACTION"] |  |  |  | 0 |
| values.cockpit.weaponSelectorPosition | 0 | 0 | 0 | 4 | 0 |
| values.cockpit.countermeasureMode | 1 | 1 | 1 | 1 | 0 |
| values.cockpit.rocketRippleMode | [false] |  |  |  | 0 |
| values.countermeasures.flareReleasePulse | 0 | 0 | 0 | 0 | 0 |
| values.countermeasures.chaffReleasedCount | 0 | 0 | 0 | 0 | 0 |
| values.countermeasures.flareReleasedCount | 0 | 0 | 0 | 0 | 0 |
| values.countermeasures.chaffReleasePulse | 0 | 0 | 0 | 0 | 0 |
| values.countermeasures.chaffRemaining | 30 | 30 | 30 | 30 | 0 |
| values.countermeasures.flareRemaining | 60 | 60 | 60 | 60 | 0 |
| values.trueAirSpeedMps | 0.00000138768 | 21.0031 | 43.5325 | 46.0306 | 0 |
| values.effects.weaponRecoilEnvelope | 0 | 0 | 0 | 1 | 0 |
| values.effects.combatDamageEnvelope | 0 | 0 | 0 | 0 | 0 |
| values.effects.brakeEnvelope | 0 | 0 | 0 | 0 | 0 |
| values.effects.doorMotionEnvelope | 0 | 0 | 0 | 0 | 0 |
| values.effects.touchdownEnvelope | 0 | 0 | 0 | 0.232724 | 0 |
| values.mach | 0.00482134 | 0.066793 | 0.125035 | 0.135137 | 0 |
| values.shakeAmplitude | 0 | 0 | 0 | 0 | 0 |
| values.weapons.selectedWeaponName | [""] |  |  |  | 0 |
| values.weapons.missileLaunchIntensity | 0 | 0 | 0 | 0 | 0 |
| values.weapons.cannonShells | 247 | 285 | 300 | 300 | 0 |
| values.weapons.crewGunFirePulse | 0 | 0 | 0 | 0 | 0 |
| values.weapons.rightCrewGunRoundsFired | 0 | 0 | 0 | 0 | 0 |
| values.weapons.weaponFirePulse | 0 | 0 | 0 | 1 | 0 |
| values.weapons.leftCrewGunFirePulse | 0 | 0 | 0 | 0 | 0 |
| values.weapons.rocketLaunchIntensity | 0 | 0 | 0 | 0 | 0 |
| values.weapons.selectedStation | 0 | 0 | 0 | 0 | 0 |
| values.weapons.gunFirePulse | 0 | 0 | 0 | 1 | 0 |
| values.weapons.gunRoundsFired | 0 | 0 | 0 | 1 | 0 |
| values.weapons.stationGunRoundsFired | 0 | 0 | 0 | 0 | 0 |
| values.weapons.weaponBurstActive | 0 | 0 | 0 | 1 | 0 |
| values.weapons.lastFiredWeaponStation | 0 | 0 | 0 | 0 | 0 |
| values.weapons.loadoutSummary | ["ROCKET,MISSILE"] |  |  |  | 0 |
| values.weapons.weaponBurstDurationMs | 0 | 0 | 0 | 770.434 | 0 |
| values.weapons.totalPayloadCount | 48 | 48 | 48 | 48 | 0 |
| values.weapons.weaponImpulseStrength | 0 | 0 | 0 | 0.745 | 0 |
| values.weapons.stationGunFirePulse | 0 | 0 | 0 | 0 | 0 |
| values.weapons.leftCrewGunRoundsFired | 0 | 0 | 0 | 0 | 0 |
| values.weapons.crewGunRoundsFired | 0 | 0 | 0 | 0 | 0 |
| values.weapons.heavyGunIntensity | 0 | 0 | 0 | 1 | 0 |
| values.weapons.rightCrewGunFirePulse | 0 | 0 | 0 | 0 | 0 |
| values.weapons.lightGunIntensity | 0 | 0 | 0 | 0 | 0 |
| values.weapons.nominalWeaponRateHz | 0 | 0 | 0 | 1788810000 | 0 |
| values.weapons.weaponRateHz | 0 | 0 | 0 | 21 | 0 |
| values.weapons.weaponClass | ["NONE","HEAVY_GUN"] |  |  |  | 0 |
| values.weapons.payloadReleasePulse | 0 | 0 | 0 | 0 | 0 |
| values.weapons.weaponEventCounter | 2472 | 2487 | 2525 | 2525 | 0 |
| values.weapons.lastFiredWeaponName | ["","Cannon"] |  |  |  | 0 |
| values.weapons.payloadReleasedCount | 0 | 0 | 0 | 0 | 0 |
| values.ground.groundResonanceIntensity | 0 | 0 | 0.20033 | 0.304143 | 0 |
| values.ground.rightStrutCompression | 0 | 0 | 0.7085 | 0.731478 | 0 |
| values.ground.wheelBrakesPosition | 0 | 0 | 0 | 0 | 0 |
| values.ground.brakeSource | ["LoGetMechInfo"] |  |  |  | 0 |
| values.ground.skidRearRightContactPulse | 0 | 0 | 0 | 0 | 0 |
| values.ground.brakingIntensity | 0 | 0 | 0 | 0 | 0 |
| values.ground.skidScrubIntensity | 0 | 0 | 0 | 0 | 0 |
| values.ground.strutCompressionRate | -0.815561 | 0 | 0 | 1.05653 | 0 |
| values.ground.skidFrontLeftContactPulse | 0 | 0 | 0 | 0 | 0 |
| values.ground.strutSource | ["LoGetMechInfo"] |  |  |  | 0 |
| values.ground.onGround | 0 | 0 | 1 | 1 | 0 |
| values.ground.longitudinalGroundSpeedMps | -0.664558 | 20.2161 | 42.6435 | 45.703 | 0 |
| values.ground.groundSpeedMps | 1.81806e-7 | 20.8511 | 42.5656 | 45.9795 | 0 |
| values.ground.leftStrutCompression | 0 | 0 | 0.651183 | 0.713991 | 0 |
| values.ground.tailWheelContact | 0 | 0 | 1 | 1 | 0 |
| values.ground.groundRollRockIntensity | 0 | 0 | 0.0307411 | 1 | 0 |
| values.ground.skidRearLeftContact | 0 | 0 | 0 | 0 | 0 |
| values.ground.weightOnGroundIntensity | 0 | 0 | 0.749238 | 0.749708 | 0 |
| values.ground.groundRollIntensity | 0 | 0 | 0.000724545 | 0.0339771 | 0 |
| values.ground.groundPitchRockIntensity | 0 | 0 | 0.0329271 | 1 | 0 |
| values.ground.skidFrontLeftContact | 0 | 0 | 0 | 0 | 0 |
| values.ground.tailWheelCasterIntensity | 0 | 0 | 0 | 0 | 0 |
| values.ground.tailStrutCompression | 0 | 0 | 0.88816 | 0.888768 | 0 |
| values.ground.wheelShimmyIntensity | 0 | 0 | 0.000507783 | 0.0415532 | 0 |
| values.ground.lateralGroundSlipMps | -13.4183 | 1.82513 | 6.46027 | 9.823 | 0 |
| values.ground.brakeDecelerationIntensity | 0 | 0 | 0 | 0 | 0 |
| values.ground.suspensionMovementIntensity | 0 | 0 | 0.0780133 | 1 | 0 |
| values.ground.touchdownPulse | 0 | 0 | 0 | 1 | 0 |
| values.ground.leftWheelContact | 0 | 0 | 1 | 1 | 0 |
| values.ground.liftoffPulse | 0 | 0 | 0 | 1 | 0 |
| values.ground.skidRearRightContact | 0 | 0 | 0 | 0 | 0 |
| values.ground.leftWheelContactPulse | 0 | 0 | 0 | 1 | 0 |
| values.ground.touchdownStrength | 0 | 0.142918 | 0.142918 | 0.232724 | 0 |
| values.ground.skidFrontRightContact | 0 | 0 | 0 | 0 | 0 |
| values.ground.tailWheelContactPulse | 0 | 0 | 0 | 1 | 0 |
| values.ground.parkingBrakeOn | [false] |  |  |  | 0 |
| values.ground.rightWheelContactPulse | 0 | 0 | 0 | 1 | 0 |
| values.ground.skidFrontRightContactPulse | 0 | 0 | 0 | 0 | 0 |
| values.ground.rightWheelContact | 0 | 0 | 1 | 1 | 0 |
| values.ground.skidRearLeftContactPulse | 0 | 0 | 0 | 0 | 0 |
| values.ground.touchdownVerticalSpeedMps | 0 | 0.571674 | 0.571674 | 0.930895 | 0 |
| values.engineRpmPct.left | 85.0209 | 94.8667 | 98.6357 | 99.0268 | 0 |
| values.engineRpmPct.right | 84.987 | 94.8282 | 98.5938 | 98.9847 | 0 |
| capabilities.doors | [false] |  |  |  | 0 |
| capabilities.cockpitStates | [true] |  |  |  | 0 |
| capabilities.crewGuns | [false] |  |  |  | 0 |
| capabilities.weaponEffects | [true] |  |  |  | 0 |
| capabilities.engineRpm | [true] |  |  |  | 0 |
| capabilities.angularVelocity | [true] |  |  |  | 0 |
| capabilities.brakes | [true] |  |  |  | 0 |
| capabilities.countermeasures | [true] |  |  |  | 0 |
| capabilities.engineLifecycle | [false] |  |  |  | 0 |
| capabilities.stationGuns | [false] |  |  |  | 0 |
| capabilities.detailedGroundEffects | [true] |  |  |  | 0 |
| capabilities.gear | [true] |  |  |  | 0 |
| capabilities.skids | [false] |  |  |  | 0 |
| capabilities.bladeSlap | [true] |  |  |  | 0 |
| capabilities.flaps | [true] |  |  |  | 0 |
| capabilities.shakeAmplitude | [true] |  |  |  | 0 |
| capabilities.acceleration | [true] |  |  |  | 0 |
| capabilities.powertrainEffects | [true] |  |  |  | 0 |
| capabilities.gearStruts | [true] |  |  |  | 0 |
| capabilities.airData | [true] |  |  |  | 0 |
| capabilities.weaponEvents | [true] |  |  |  | 0 |
| capabilities.engineTorque | [false] |  |  |  | 0 |
| capabilities.rotorWarning | [false] |  |  |  | 0 |
| capabilities.controlPositions | [true] |  |  |  | 0 |
| capabilities.wheelRotation | [false] |  |  |  | 0 |
| capabilities.groundMotion | [true] |  |  |  | 0 |
| capabilities.rotorDynamics | [true] |  |  |  | 0 |
| capabilities.torqueGauge | [false] |  |  |  | 0 |
| capabilities.weaponPayload | [true] |  |  |  | 0 |
| capabilities.aerodynamicEffects | [true] |  |  |  | 0 |
| capabilities.speedbrake | [true] |  |  |  | 0 |
| protocol | 1 | 1 | 1 | 1 | 0 |
| aircraft.family | ["AH64"] |  |  |  | 0 |
| aircraft.dcsType | ["AH-64D_BLK_II"] |  |  |  | 0 |
| aircraft.groundType | ["WHEELS"] |  |  |  | 0 |
| sessionId | ["1788809128.535"] |  |  |  | 0 |
| modelTime | 2390.79 | 2520.45 | 2630.62 | 2642.22 | 0 |
| sentAtMs | 2653130 | 2782770 | 2892950 | 2904590 | 0 |
| sequence | 89705 | 93147 | 96244 | 96589 | 0 |
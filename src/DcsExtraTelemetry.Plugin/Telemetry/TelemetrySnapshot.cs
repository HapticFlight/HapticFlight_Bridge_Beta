using System;
using DcsExtraTelemetry.Plugin.Haptics;

namespace DcsExtraTelemetry.Plugin.Telemetry
{
    internal sealed class TelemetrySnapshot
    {
        public static readonly TelemetrySnapshot Empty = new TelemetrySnapshot();

        private TelemetrySnapshot()
        {
            AircraftType = string.Empty;
            AircraftFamily = string.Empty;
            GroundType = string.Empty;
            SelectedWeaponName = string.Empty;
            LastFiredWeaponName = string.Empty;
            WeaponClass = string.Empty;
            StrutSource = string.Empty;
            BrakeSource = string.Empty;
            LoadoutSummary = string.Empty;
            WeaponSelectorName = string.Empty;
            RotorSource = string.Empty;
            Haptics = HapticStateSnapshot.Empty;
        }

        public static TelemetrySnapshot FromPacket(
            TelemetryPacket packet,
            DateTime receivedAtUtc,
            double packetRateHz,
            long invalidPackets,
            long droppedPackets,
            HapticStateSnapshot haptics)
        {
            TelemetryValues values = packet.values;
            TelemetryCapabilities caps = packet.capabilities;
            Vector3 acceleration = values.acceleration;
            Vector3 angular = values.angularVelocity;
            ScalarPair rpm = values.engineRpmPct;
            MechanicalValues mechanical = values.mechanical;
            WeaponValues weapons = values.weapons;
            GroundValues ground = values.ground;
            CountermeasureValues countermeasures = values.countermeasures;
            CockpitValues cockpit = values.cockpit;
            DynamicsValues dynamics = values.dynamics;
            EffectEnvelopeValues effects = values.effects;

            return new TelemetrySnapshot
            {
                ReceivedAtUtc = receivedAtUtc,
                Haptics = haptics ?? HapticStateSnapshot.Empty,
                AircraftType = packet.aircraft == null ? string.Empty : packet.aircraft.dcsType ?? string.Empty,
                AircraftFamily = packet.aircraft == null ? string.Empty : packet.aircraft.family ?? string.Empty,
                GroundType = packet.aircraft == null ? string.Empty : packet.aircraft.groundType ?? string.Empty,
                AccelerationXG = V(acceleration == null ? null : acceleration.x),
                AccelerationYG = V(acceleration == null ? null : acceleration.y),
                AccelerationZG = V(acceleration == null ? null : acceleration.z),
                AngularXRadS = V(angular == null ? null : angular.x),
                AngularYRadS = V(angular == null ? null : angular.y),
                AngularZRadS = V(angular == null ? null : angular.z),
                IndicatedAirSpeedMps = V(values.indicatedAirSpeedMps),
                TrueAirSpeedMps = V(values.trueAirSpeedMps),
                VerticalVelocityMps = V(values.verticalVelocityMps),
                AngleOfAttackRad = V(values.angleOfAttackRad),
                Mach = V(values.mach),
                ShakeAmplitude = V(values.shakeAmplitude),
                EngineRpmLeftPct = V(rpm == null ? null : rpm.left),
                EngineRpmRightPct = V(rpm == null ? null : rpm.right),
                GearPosition = V(mechanical == null ? null : mechanical.gearPosition),
                FlapsPosition = V(mechanical == null ? null : mechanical.flapsPosition),
                SpeedbrakePosition = V(mechanical == null ? null : mechanical.speedbrakePosition),
                SelectedWeaponStation = V(weapons == null ? null : weapons.selectedStation),
                SelectedWeaponName = S(weapons == null ? null : weapons.selectedWeaponName),
                WeaponPayloadCount = V(weapons == null ? null : weapons.totalPayloadCount),
                CannonShells = V(weapons == null ? null : weapons.cannonShells),
                WeaponFirePulse = V(weapons == null ? null : weapons.weaponFirePulse),
                GunFirePulse = V(weapons == null ? null : weapons.gunFirePulse),
                StationGunFirePulse = V(weapons == null ? null : weapons.stationGunFirePulse),
                CrewGunFirePulse = V(weapons == null ? null : weapons.crewGunFirePulse),
                LeftCrewGunFirePulse = V(weapons == null ? null : weapons.leftCrewGunFirePulse),
                RightCrewGunFirePulse = V(weapons == null ? null : weapons.rightCrewGunFirePulse),
                PayloadReleasePulse = V(weapons == null ? null : weapons.payloadReleasePulse),
                GunRoundsFired = V(weapons == null ? null : weapons.gunRoundsFired),
                StationGunRoundsFired = V(weapons == null ? null : weapons.stationGunRoundsFired),
                CrewGunRoundsFired = V(weapons == null ? null : weapons.crewGunRoundsFired),
                LeftCrewGunRoundsFired = V(weapons == null ? null : weapons.leftCrewGunRoundsFired),
                RightCrewGunRoundsFired = V(weapons == null ? null : weapons.rightCrewGunRoundsFired),
                StationGunAmmoRemaining = V(weapons == null ? null : weapons.stationGunAmmoRemaining),
                LeftCrewGunAmmoRemaining = V(weapons == null ? null : weapons.leftCrewGunAmmoRemaining),
                RightCrewGunAmmoRemaining = V(weapons == null ? null : weapons.rightCrewGunAmmoRemaining),
                PayloadReleasedCount = V(weapons == null ? null : weapons.payloadReleasedCount),
                LastFiredWeaponStation = V(weapons == null ? null : weapons.lastFiredWeaponStation),
                LastFiredWeaponName = S(weapons == null ? null : weapons.lastFiredWeaponName),
                WeaponEventCounter = weapons == null ? 0L : weapons.weaponEventCounter,
                WeaponClass = S(weapons == null ? null : weapons.weaponClass),
                WeaponBurstActive = V(weapons == null ? null : weapons.weaponBurstActive),
                WeaponBurstDurationMs = V(weapons == null ? null : weapons.weaponBurstDurationMs),
                WeaponRateHz = V(weapons == null ? null : weapons.weaponRateHz),
                NominalWeaponRateHz = V(weapons == null ? null : weapons.nominalWeaponRateHz),
                WeaponImpulseStrength = V(weapons == null ? null : weapons.weaponImpulseStrength),
                LightGunIntensity = V(weapons == null ? null : weapons.lightGunIntensity),
                HeavyGunIntensity = V(weapons == null ? null : weapons.heavyGunIntensity),
                RocketLaunchIntensity = V(weapons == null ? null : weapons.rocketLaunchIntensity),
                MissileLaunchIntensity = V(weapons == null ? null : weapons.missileLaunchIntensity),
                LoadoutSummary = S(weapons == null ? null : weapons.loadoutSummary),
                TouchdownEnvelope = V(effects == null ? null : effects.touchdownEnvelope),
                CombatDamageEnvelope = V(effects == null ? null : effects.combatDamageEnvelope),
                DoorMotionEnvelope = V(effects == null ? null : effects.doorMotionEnvelope),
                BrakeEnvelope = V(effects == null ? null : effects.brakeEnvelope),
                WeaponRecoilEnvelope = V(effects == null ? null : effects.weaponRecoilEnvelope),
                GroundSpeedMps = V(ground == null ? null : ground.groundSpeedMps),
                AltitudeAglM = V(ground == null ? null : ground.altitudeAglM),
                WheelBrakesPosition = V(ground == null ? null : ground.wheelBrakesPosition),
                BrakeSource = S(ground == null ? null : ground.brakeSource),
                ParkingBrakeOn = ground != null && ground.parkingBrakeOn,
                TailStrutCompression = V(ground == null ? null : ground.tailStrutCompression),
                LeftStrutCompression = V(ground == null ? null : ground.leftStrutCompression),
                RightStrutCompression = V(ground == null ? null : ground.rightStrutCompression),
                StrutCompressionRate = V(ground == null ? null : ground.strutCompressionRate),
                TailWheelRotation = V(ground == null ? null : ground.tailWheelRotation),
                LeftWheelRotation = V(ground == null ? null : ground.leftWheelRotation),
                RightWheelRotation = V(ground == null ? null : ground.rightWheelRotation),
                SkidFrontLeftCompression = V(ground == null ? null : ground.skidFrontLeftCompression),
                SkidFrontRightCompression = V(ground == null ? null : ground.skidFrontRightCompression),
                SkidRearLeftCompression = V(ground == null ? null : ground.skidRearLeftCompression),
                SkidRearRightCompression = V(ground == null ? null : ground.skidRearRightCompression),
                LeftSkidCompression = V(ground == null ? null : ground.leftSkidCompression),
                RightSkidCompression = V(ground == null ? null : ground.rightSkidCompression),
                SkidFrontLeftContact = V(ground == null ? null : ground.skidFrontLeftContact),
                SkidFrontRightContact = V(ground == null ? null : ground.skidFrontRightContact),
                SkidRearLeftContact = V(ground == null ? null : ground.skidRearLeftContact),
                SkidRearRightContact = V(ground == null ? null : ground.skidRearRightContact),
                SkidFrontLeftContactPulse = V(ground == null ? null : ground.skidFrontLeftContactPulse),
                SkidFrontRightContactPulse = V(ground == null ? null : ground.skidFrontRightContactPulse),
                SkidRearLeftContactPulse = V(ground == null ? null : ground.skidRearLeftContactPulse),
                SkidRearRightContactPulse = V(ground == null ? null : ground.skidRearRightContactPulse),
                StrutSource = S(ground == null ? null : ground.strutSource),
                OnGround = V(ground == null ? null : ground.onGround),
                TailWheelContact = V(ground == null ? null : ground.tailWheelContact),
                LeftWheelContact = V(ground == null ? null : ground.leftWheelContact),
                RightWheelContact = V(ground == null ? null : ground.rightWheelContact),
                TailWheelContactPulse = V(ground == null ? null : ground.tailWheelContactPulse),
                LeftWheelContactPulse = V(ground == null ? null : ground.leftWheelContactPulse),
                RightWheelContactPulse = V(ground == null ? null : ground.rightWheelContactPulse),
                TouchdownPulse = V(ground == null ? null : ground.touchdownPulse),
                TouchdownVerticalSpeedMps = V(ground == null ? null : ground.touchdownVerticalSpeedMps),
                TouchdownStrength = V(ground == null ? null : ground.touchdownStrength),
                SuspensionMovementIntensity = V(ground == null ? null : ground.suspensionMovementIntensity),
                GroundRollIntensity = V(ground == null ? null : ground.groundRollIntensity),
                GroundResonanceIntensity = V(ground == null ? null : ground.groundResonanceIntensity),
                BrakingIntensity = V(ground == null ? null : ground.brakingIntensity),
                BrakeDecelerationIntensity = V(ground == null ? null : ground.brakeDecelerationIntensity),
                WheelSkidIntensity = V(ground == null ? null : ground.wheelSkidIntensity),
                GroundPitchRockIntensity = V(ground == null ? null : ground.groundPitchRockIntensity),
                GroundRollRockIntensity = V(ground == null ? null : ground.groundRollRockIntensity),
                LongitudinalGroundSpeedMps = V(ground == null ? null : ground.longitudinalGroundSpeedMps),
                LateralGroundSlipMps = V(ground == null ? null : ground.lateralGroundSlipMps),
                SkidScrubIntensity = V(ground == null ? null : ground.skidScrubIntensity),
                WheelShimmyIntensity = V(ground == null ? null : ground.wheelShimmyIntensity),
                TailWheelCasterIntensity = V(ground == null ? null : ground.tailWheelCasterIntensity),
                LiftoffPulse = V(ground == null ? null : ground.liftoffPulse),
                WeightOnGroundIntensity = V(ground == null ? null : ground.weightOnGroundIntensity),
                ChaffRemaining = V(countermeasures == null ? null : countermeasures.chaffRemaining),
                FlareRemaining = V(countermeasures == null ? null : countermeasures.flareRemaining),
                ChaffReleasePulse = V(countermeasures == null ? null : countermeasures.chaffReleasePulse),
                FlareReleasePulse = V(countermeasures == null ? null : countermeasures.flareReleasePulse),
                ChaffReleasedCount = V(countermeasures == null ? null : countermeasures.chaffReleasedCount),
                FlareReleasedCount = V(countermeasures == null ? null : countermeasures.flareReleasedCount),
                MasterArmOn = cockpit != null && cockpit.masterArmOn,
                WeaponSelectorPosition = V(cockpit == null ? null : cockpit.weaponSelectorPosition),
                WeaponSelectorName = S(cockpit == null ? null : cockpit.weaponSelectorName),
                RocketPairMode = cockpit != null && cockpit.rocketPairMode,
                RocketRippleMode = cockpit != null && cockpit.rocketRippleMode,
                CountermeasureArmed = cockpit != null && cockpit.countermeasureArmed,
                CountermeasureMode = V(cockpit == null ? null : cockpit.countermeasureMode),
                PilotDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.pilotDoorPosition),
                CopilotDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.copilotDoorPosition),
                LeftCargoDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.leftCargoDoorPosition),
                RightCargoDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.rightCargoDoorPosition),
                LeftGunnerDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.leftGunnerDoorPosition),
                RightGunnerDoorPosition = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.rightGunnerDoorPosition),
                DoorMovementIntensity = V(cockpit == null || cockpit.doors == null ? null : cockpit.doors.doorMovementIntensity),
                MainRotorRpmPct = V(dynamics == null ? null : dynamics.mainRotorRpmPct),
                MainRotorRpm = V(dynamics == null ? null : dynamics.mainRotorRpm),
                MainRotorHz = V(dynamics == null ? null : dynamics.mainRotorHz),
                BladePassHz = V(dynamics == null ? null : dynamics.bladePassHz),
                RotorSource = S(dynamics == null ? null : dynamics.rotorSource),
                CollectivePosition = V(dynamics == null ? null : dynamics.collectivePosition),
                CollectiveMovementRate = V(dynamics == null ? null : dynamics.collectiveMovementRate),
                EngineTorqueLeftPct = V(dynamics == null ? null : dynamics.engineTorqueLeftPct),
                EngineTorqueRightPct = V(dynamics == null ? null : dynamics.engineTorqueRightPct),
                TorqueGaugeNormalized = V(dynamics == null ? null : dynamics.torqueGaugeNormalized),
                RotorLoadIntensity = V(dynamics == null ? null : dynamics.rotorLoadIntensity),
                TransmissionLoadIntensity = V(dynamics == null ? null : dynamics.transmissionLoadIntensity),
                EngineGovernorResponseIntensity = V(dynamics == null ? null : dynamics.engineGovernorResponseIntensity),
                RotorDroopIntensity = V(dynamics == null ? null : dynamics.rotorDroopIntensity),
                RotorSpoolIntensity = V(dynamics == null ? null : dynamics.rotorSpoolIntensity),
                TurbulenceIntensity = V(dynamics == null ? null : dynamics.turbulenceIntensity),
                ControlResponseIntensity = V(dynamics == null ? null : dynamics.controlResponseIntensity),
                AirframeBuffetIntensity = V(dynamics == null ? null : dynamics.airframeBuffetIntensity),
                ManeuverLoadIntensity = V(dynamics == null ? null : dynamics.maneuverLoadIntensity),
                NormalG = V(dynamics == null ? null : dynamics.normalG),
                NormalGDeviation = V(dynamics == null ? null : dynamics.normalGDeviation),
                PositiveGIntensity = V(dynamics == null ? null : dynamics.positiveGIntensity),
                NegativeGIntensity = V(dynamics == null ? null : dynamics.negativeGIntensity),
                ManeuverResponseIntensity = V(dynamics == null ? null : dynamics.maneuverResponseIntensity),
                LateralManeuverIntensity = V(dynamics == null ? null : dynamics.lateralManeuverIntensity),
                SettlingWithPowerCue = V(dynamics == null ? null : dynamics.settlingWithPowerCue),
                VrsRiskIntensity = V(dynamics == null ? null : dynamics.vrsRiskIntensity),
                TranslationalLiftTransition = V(dynamics == null ? null : dynamics.translationalLiftTransition),
                BladeSlapIntensity = V(dynamics == null ? null : dynamics.bladeSlapIntensity),
                HardManeuverPulse = V(dynamics == null ? null : dynamics.hardManeuverPulse),
                AirframeImpactPulse = V(dynamics == null ? null : dynamics.airframeImpactPulse),
                RotorRunning = dynamics != null && dynamics.rotorRunning,
                ApuRunning = dynamics != null && dynamics.apuRunning,
                Engine1StarterActive = dynamics != null && dynamics.engine1StarterActive,
                Engine2StarterActive = dynamics != null && dynamics.engine2StarterActive,
                LowRotorRpmWarning = dynamics != null && dynamics.lowRotorRpmWarning,
                HasAcceleration = caps.acceleration,
                HasAngularVelocity = caps.angularVelocity,
                HasAirData = caps.airData,
                HasEngineRpm = caps.engineRpm,
                HasGear = caps.gear,
                HasFlaps = caps.flaps,
                HasSpeedbrake = caps.speedbrake,
                HasShakeAmplitude = caps.shakeAmplitude,
                HasWeaponPayload = caps.weaponPayload,
                HasWeaponEvents = caps.weaponEvents,
                HasStationGuns = caps.stationGuns,
                HasCrewGuns = caps.crewGuns,
                HasGearStruts = caps.gearStruts,
                HasGroundMotion = caps.groundMotion,
                HasWheelRotation = caps.wheelRotation,
                HasSkids = caps.skids,
                HasCountermeasures = caps.countermeasures,
                HasCockpitStates = caps.cockpitStates,
                HasRotorDynamics = caps.rotorDynamics,
                HasControlPositions = caps.controlPositions,
                HasEngineTorque = caps.engineTorque,
                HasTorqueGauge = caps.torqueGauge,
                HasRotorWarning = caps.rotorWarning,
                HasPowertrainEffects = caps.powertrainEffects,
                HasAerodynamicEffects = caps.aerodynamicEffects,
                HasBladeSlap = caps.bladeSlap,
                HasDetailedGroundEffects = caps.detailedGroundEffects,
                HasWeaponEffects = caps.weaponEffects,
                HasEngineLifecycle = caps.engineLifecycle,
                HasBrakes = caps.brakes,
                HasDoors = caps.doors,
                PacketRateHz = packetRateHz,
                InvalidPackets = invalidPackets,
                DroppedPackets = droppedPackets
            };
        }

        private static double V(double? value) { return value ?? 0.0; }
        private static string S(string value) { return value ?? string.Empty; }

        public DateTime ReceivedAtUtc { get; private set; }
        public string AircraftType { get; private set; }
        public string AircraftFamily { get; private set; }
        public string GroundType { get; private set; }
        public double AccelerationXG { get; private set; }
        public double AccelerationYG { get; private set; }
        public double AccelerationZG { get; private set; }
        public double AngularXRadS { get; private set; }
        public double AngularYRadS { get; private set; }
        public double AngularZRadS { get; private set; }
        public double IndicatedAirSpeedMps { get; private set; }
        public double TrueAirSpeedMps { get; private set; }
        public double VerticalVelocityMps { get; private set; }
        public double AngleOfAttackRad { get; private set; }
        public double Mach { get; private set; }
        public double ShakeAmplitude { get; private set; }
        public double EngineRpmLeftPct { get; private set; }
        public double EngineRpmRightPct { get; private set; }
        public double GearPosition { get; private set; }
        public double FlapsPosition { get; private set; }
        public double SpeedbrakePosition { get; private set; }
        public double SelectedWeaponStation { get; private set; }
        public string SelectedWeaponName { get; private set; }
        public double WeaponPayloadCount { get; private set; }
        public double CannonShells { get; private set; }
        public double WeaponFirePulse { get; private set; }
        public double GunFirePulse { get; private set; }
        public double StationGunFirePulse { get; private set; }
        public double CrewGunFirePulse { get; private set; }
        public double LeftCrewGunFirePulse { get; private set; }
        public double RightCrewGunFirePulse { get; private set; }
        public double PayloadReleasePulse { get; private set; }
        public double GunRoundsFired { get; private set; }
        public double StationGunRoundsFired { get; private set; }
        public double CrewGunRoundsFired { get; private set; }
        public double LeftCrewGunRoundsFired { get; private set; }
        public double RightCrewGunRoundsFired { get; private set; }
        public double StationGunAmmoRemaining { get; private set; }
        public double LeftCrewGunAmmoRemaining { get; private set; }
        public double RightCrewGunAmmoRemaining { get; private set; }
        public double PayloadReleasedCount { get; private set; }
        public double LastFiredWeaponStation { get; private set; }
        public string LastFiredWeaponName { get; private set; }
        public long WeaponEventCounter { get; private set; }
        public string WeaponClass { get; private set; }
        public double WeaponBurstActive { get; private set; }
        public double WeaponBurstDurationMs { get; private set; }
        public double WeaponRateHz { get; private set; }
        public double NominalWeaponRateHz { get; private set; }
        public double WeaponImpulseStrength { get; private set; }
        public double LightGunIntensity { get; private set; }
        public double HeavyGunIntensity { get; private set; }
        public double RocketLaunchIntensity { get; private set; }
        public double MissileLaunchIntensity { get; private set; }
        public string LoadoutSummary { get; private set; }
        public double TouchdownEnvelope { get; private set; }
        public double CombatDamageEnvelope { get; private set; }
        public double DoorMotionEnvelope { get; private set; }
        public double BrakeEnvelope { get; private set; }
        public double WeaponRecoilEnvelope { get; private set; }
        public double GroundSpeedMps { get; private set; }
        public double AltitudeAglM { get; private set; }
        public double WheelBrakesPosition { get; private set; }
        public string BrakeSource { get; private set; }
        public bool ParkingBrakeOn { get; private set; }
        public double TailStrutCompression { get; private set; }
        public double LeftStrutCompression { get; private set; }
        public double RightStrutCompression { get; private set; }
        public double StrutCompressionRate { get; private set; }
        public double TailWheelRotation { get; private set; }
        public double LeftWheelRotation { get; private set; }
        public double RightWheelRotation { get; private set; }
        public double SkidFrontLeftCompression { get; private set; }
        public double SkidFrontRightCompression { get; private set; }
        public double SkidRearLeftCompression { get; private set; }
        public double SkidRearRightCompression { get; private set; }
        public double LeftSkidCompression { get; private set; }
        public double RightSkidCompression { get; private set; }
        public double SkidFrontLeftContact { get; private set; }
        public double SkidFrontRightContact { get; private set; }
        public double SkidRearLeftContact { get; private set; }
        public double SkidRearRightContact { get; private set; }
        public double SkidFrontLeftContactPulse { get; private set; }
        public double SkidFrontRightContactPulse { get; private set; }
        public double SkidRearLeftContactPulse { get; private set; }
        public double SkidRearRightContactPulse { get; private set; }
        public string StrutSource { get; private set; }
        public double OnGround { get; private set; }
        public double TailWheelContact { get; private set; }
        public double LeftWheelContact { get; private set; }
        public double RightWheelContact { get; private set; }
        public double TailWheelContactPulse { get; private set; }
        public double LeftWheelContactPulse { get; private set; }
        public double RightWheelContactPulse { get; private set; }
        public double TouchdownPulse { get; private set; }
        public double TouchdownVerticalSpeedMps { get; private set; }
        public double TouchdownStrength { get; private set; }
        public double SuspensionMovementIntensity { get; private set; }
        public double GroundRollIntensity { get; private set; }
        public double GroundResonanceIntensity { get; private set; }
        public double BrakingIntensity { get; private set; }
        public double BrakeDecelerationIntensity { get; private set; }
        public double WheelSkidIntensity { get; private set; }
        public double GroundPitchRockIntensity { get; private set; }
        public double GroundRollRockIntensity { get; private set; }
        public double LongitudinalGroundSpeedMps { get; private set; }
        public double LateralGroundSlipMps { get; private set; }
        public double SkidScrubIntensity { get; private set; }
        public double WheelShimmyIntensity { get; private set; }
        public double TailWheelCasterIntensity { get; private set; }
        public double LiftoffPulse { get; private set; }
        public double WeightOnGroundIntensity { get; private set; }
        public double ChaffRemaining { get; private set; }
        public double FlareRemaining { get; private set; }
        public double ChaffReleasePulse { get; private set; }
        public double FlareReleasePulse { get; private set; }
        public double ChaffReleasedCount { get; private set; }
        public double FlareReleasedCount { get; private set; }
        public bool MasterArmOn { get; private set; }
        public double WeaponSelectorPosition { get; private set; }
        public string WeaponSelectorName { get; private set; }
        public bool RocketPairMode { get; private set; }
        public bool RocketRippleMode { get; private set; }
        public bool CountermeasureArmed { get; private set; }
        public double CountermeasureMode { get; private set; }
        public double PilotDoorPosition { get; private set; }
        public double CopilotDoorPosition { get; private set; }
        public double LeftCargoDoorPosition { get; private set; }
        public double RightCargoDoorPosition { get; private set; }
        public double LeftGunnerDoorPosition { get; private set; }
        public double RightGunnerDoorPosition { get; private set; }
        public double DoorMovementIntensity { get; private set; }
        public double MainRotorRpmPct { get; private set; }
        public double MainRotorRpm { get; private set; }
        public double MainRotorHz { get; private set; }
        public double BladePassHz { get; private set; }
        public string RotorSource { get; private set; }
        public double CollectivePosition { get; private set; }
        public double CollectiveMovementRate { get; private set; }
        public double EngineTorqueLeftPct { get; private set; }
        public double EngineTorqueRightPct { get; private set; }
        public double TorqueGaugeNormalized { get; private set; }
        public double RotorLoadIntensity { get; private set; }
        public double TransmissionLoadIntensity { get; private set; }
        public double EngineGovernorResponseIntensity { get; private set; }
        public double RotorDroopIntensity { get; private set; }
        public double RotorSpoolIntensity { get; private set; }
        public double TurbulenceIntensity { get; private set; }
        public double ControlResponseIntensity { get; private set; }
        public double AirframeBuffetIntensity { get; private set; }
        public double ManeuverLoadIntensity { get; private set; }
        public double NormalG { get; private set; }
        public double NormalGDeviation { get; private set; }
        public double PositiveGIntensity { get; private set; }
        public double NegativeGIntensity { get; private set; }
        public double ManeuverResponseIntensity { get; private set; }
        public double LateralManeuverIntensity { get; private set; }
        public double SettlingWithPowerCue { get; private set; }
        public double VrsRiskIntensity { get; private set; }
        public double TranslationalLiftTransition { get; private set; }
        public double BladeSlapIntensity { get; private set; }
        public double HardManeuverPulse { get; private set; }
        public double AirframeImpactPulse { get; private set; }
        public bool RotorRunning { get; private set; }
        public bool ApuRunning { get; private set; }
        public bool Engine1StarterActive { get; private set; }
        public bool Engine2StarterActive { get; private set; }
        public bool LowRotorRpmWarning { get; private set; }
        public bool HasAcceleration { get; private set; }
        public bool HasAngularVelocity { get; private set; }
        public bool HasAirData { get; private set; }
        public bool HasEngineRpm { get; private set; }
        public bool HasGear { get; private set; }
        public bool HasFlaps { get; private set; }
        public bool HasSpeedbrake { get; private set; }
        public bool HasShakeAmplitude { get; private set; }
        public bool HasWeaponPayload { get; private set; }
        public bool HasWeaponEvents { get; private set; }
        public bool HasStationGuns { get; private set; }
        public bool HasCrewGuns { get; private set; }
        public bool HasGearStruts { get; private set; }
        public bool HasGroundMotion { get; private set; }
        public bool HasWheelRotation { get; private set; }
        public bool HasSkids { get; private set; }
        public bool HasCountermeasures { get; private set; }
        public bool HasCockpitStates { get; private set; }
        public bool HasRotorDynamics { get; private set; }
        public bool HasControlPositions { get; private set; }
        public bool HasEngineTorque { get; private set; }
        public bool HasTorqueGauge { get; private set; }
        public bool HasRotorWarning { get; private set; }
        public bool HasPowertrainEffects { get; private set; }
        public bool HasAerodynamicEffects { get; private set; }
        public bool HasBladeSlap { get; private set; }
        public bool HasDetailedGroundEffects { get; private set; }
        public bool HasWeaponEffects { get; private set; }
        public bool HasEngineLifecycle { get; private set; }
        public bool HasBrakes { get; private set; }
        public bool HasDoors { get; private set; }
        public double PacketRateHz { get; private set; }
        public long InvalidPackets { get; private set; }
        public long DroppedPackets { get; private set; }
        public HapticStateSnapshot Haptics { get; private set; }
    }
}

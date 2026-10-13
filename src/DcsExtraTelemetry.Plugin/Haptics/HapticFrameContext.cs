using DcsExtraTelemetry.Plugin.Telemetry;
using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal sealed class HapticFrameContext
    {
        public string SessionId;
        public long Sequence;
        public double ModelTime;
        public DateTime ReceivedAtUtc;
        public string AircraftType;
        public string AircraftFamily;
        public string GroundType;
        public string StrutSource;
        public string RotorSource;
        public string BrakeSource;
        public bool HasAcceleration;
        public bool HasAngularVelocity;
        public bool HasAirData;
        public bool HasEngineRpm;
        public bool HasGear;
        public bool HasFlaps;
        public bool HasSpeedbrake;
        public bool HasGearStruts;
        public bool HasGroundMotion;
        public bool HasDetailedGroundEffects;
        public bool HasBrakes;
        public bool HasRotorDynamics;
        public bool HasControlPositions;
        public bool HasEngineTorque;
        public bool HasTorqueGauge;
        public bool HasRotorWarning;
        public bool HasPowertrainEffects;
        public bool HasAerodynamicEffects;
        public bool HasBladeSlap;
        public bool HasEngineLifecycle;
        public bool HasDoors;
        public bool HasWeaponEffects;
        public bool HasCountermeasures;
        public double AccelXG;
        public double AccelYG;
        public double AccelZG;
        public double AngularX;
        public double AngularY;
        public double AngularZ;
        public double TrueAirSpeed;
        public double VerticalVelocity;
        public double GroundSpeed;
        public double StrutCompressionRate;
        // Raw Hook contact evidence is retained separately from the engine's
        // confirmed touchdown signal.  A tail-first rolling landing can be a
        // real impact before both mains report sustained support.
        public double RawTouchdownPulse;
        public double RawTouchdownStrength;
        public double TailCompression;
        public double LeftCompression;
        public double RightCompression;
        public bool OnGround;
        public bool TailContact;
        public bool LeftContact;
        public bool RightContact;
        public bool SkidFrontLeft;
        public bool SkidFrontRight;
        public bool SkidRearLeft;
        public bool SkidRearRight;
        public double BrakePosition;
        public bool ParkingBrake;
        public double RotorRpmPct;
        public double BladePassHz;
        public double Collective;
        public double CollectiveRate;
        public double GearPosition;
        public double FlapsPosition;
        public double SpeedbrakePosition;
        public double DoorMovement;
        public double DoorPosition;
        public bool WeaponActive;
        public string WeaponClass;
        public double WeaponImpulse;
        public double LightGun;
        public double HeavyGun;
        public double Rocket;
        public double Missile;
        public double LeftCrewGunPulse;
        public double RightCrewGunPulse;
        public string LastFiredWeaponName;
        public double ChaffPulse;
        public double FlarePulse;
        public bool RotorRunning;
        public bool ApuRunning;
        public bool Starter1;
        public bool Starter2;
        public bool LowRotorWarning;
        public GroundValues Ground;
        public DynamicsValues Dynamics;

        public static HapticFrameContext FromPacket(TelemetryPacket packet, DateTime receivedAtUtc)
        {
            TelemetryValues values = packet.values;
            TelemetryCapabilities caps = packet.capabilities;
            GroundValues ground = values.ground;
            DynamicsValues dynamics = values.dynamics;
            WeaponValues weapons = values.weapons;
            CountermeasureValues countermeasures = values.countermeasures;
            MechanicalValues mechanical = values.mechanical;
            DoorValues doors = values.cockpit == null ? null : values.cockpit.doors;
            Vector3 acceleration = values.acceleration;
            Vector3 angular = values.angularVelocity;
            return new HapticFrameContext
            {
                SessionId = packet.sessionId ?? string.Empty,
                Sequence = packet.sequence,
                ModelTime = packet.modelTime,
                ReceivedAtUtc = receivedAtUtc,
                AircraftType = packet.aircraft == null ? string.Empty : packet.aircraft.dcsType ?? string.Empty,
                AircraftFamily = packet.aircraft == null ? string.Empty : packet.aircraft.family ?? string.Empty,
                GroundType = packet.aircraft == null ? string.Empty : packet.aircraft.groundType ?? string.Empty,
                StrutSource = ground == null ? string.Empty : ground.strutSource ?? string.Empty,
                RotorSource = dynamics == null ? string.Empty : dynamics.rotorSource ?? string.Empty,
                BrakeSource = ground == null ? string.Empty : ground.brakeSource ?? string.Empty,
                HasAcceleration = caps.acceleration,
                HasAngularVelocity = caps.angularVelocity,
                HasAirData = caps.airData,
                HasEngineRpm = caps.engineRpm,
                HasGear = caps.gear,
                HasFlaps = caps.flaps,
                HasSpeedbrake = caps.speedbrake,
                HasGearStruts = caps.gearStruts,
                HasGroundMotion = caps.groundMotion,
                HasDetailedGroundEffects = caps.detailedGroundEffects,
                HasBrakes = caps.brakes,
                HasRotorDynamics = caps.rotorDynamics,
                HasControlPositions = caps.controlPositions,
                HasEngineTorque = caps.engineTorque,
                HasTorqueGauge = caps.torqueGauge,
                HasRotorWarning = caps.rotorWarning,
                HasPowertrainEffects = caps.powertrainEffects,
                HasAerodynamicEffects = caps.aerodynamicEffects,
                HasBladeSlap = caps.bladeSlap,
                HasEngineLifecycle = caps.engineLifecycle,
                HasDoors = caps.doors,
                HasWeaponEffects = caps.weaponEffects,
                HasCountermeasures = caps.countermeasures,
                AccelXG = V(acceleration == null ? null : acceleration.x),
                AccelYG = V(acceleration == null ? null : acceleration.y),
                AccelZG = V(acceleration == null ? null : acceleration.z),
                AngularX = V(angular == null ? null : angular.x),
                AngularY = V(angular == null ? null : angular.y),
                AngularZ = V(angular == null ? null : angular.z),
                TrueAirSpeed = V(values.trueAirSpeedMps),
                VerticalVelocity = V(values.verticalVelocityMps),
                GroundSpeed = V(ground == null ? null : ground.groundSpeedMps),
                StrutCompressionRate = V(ground == null ? null : ground.strutCompressionRate),
                RawTouchdownPulse = V(ground == null ? null : ground.touchdownPulse),
                RawTouchdownStrength = V(ground == null ? null : ground.touchdownStrength),
                TailCompression = V(ground == null ? null : ground.tailStrutCompression),
                LeftCompression = V(ground == null ? null : ground.leftStrutCompression),
                RightCompression = V(ground == null ? null : ground.rightStrutCompression),
                OnGround = B(ground == null ? null : ground.onGround),
                TailContact = B(ground == null ? null : ground.tailWheelContact),
                LeftContact = B(ground == null ? null : ground.leftWheelContact),
                RightContact = B(ground == null ? null : ground.rightWheelContact),
                SkidFrontLeft = B(ground == null ? null : ground.skidFrontLeftContact),
                SkidFrontRight = B(ground == null ? null : ground.skidFrontRightContact),
                SkidRearLeft = B(ground == null ? null : ground.skidRearLeftContact),
                SkidRearRight = B(ground == null ? null : ground.skidRearRightContact),
                BrakePosition = V(ground == null ? null : ground.wheelBrakesPosition),
                ParkingBrake = ground != null && ground.parkingBrakeOn,
                RotorRpmPct = V(dynamics == null ? null : dynamics.mainRotorRpmPct),
                BladePassHz = V(dynamics == null ? null : dynamics.bladePassHz),
                Collective = V(dynamics == null ? null : dynamics.collectivePosition),
                CollectiveRate = V(dynamics == null ? null : dynamics.collectiveMovementRate),
                GearPosition = V(mechanical == null ? null : mechanical.gearPosition),
                FlapsPosition = V(mechanical == null ? null : mechanical.flapsPosition),
                SpeedbrakePosition = V(mechanical == null ? null : mechanical.speedbrakePosition),
                DoorMovement = V(doors == null ? null : doors.doorMovementIntensity),
                DoorPosition = MaxDoorPosition(doors),
                WeaponActive = B(weapons == null ? null : weapons.weaponBurstActive),
                WeaponClass = weapons == null ? string.Empty : weapons.weaponClass ?? string.Empty,
                WeaponImpulse = V(weapons == null ? null : weapons.weaponImpulseStrength),
                LightGun = V(weapons == null ? null : weapons.lightGunIntensity),
                HeavyGun = V(weapons == null ? null : weapons.heavyGunIntensity),
                Rocket = V(weapons == null ? null : weapons.rocketLaunchIntensity),
                Missile = V(weapons == null ? null : weapons.missileLaunchIntensity),
                LeftCrewGunPulse = V(weapons == null ? null : weapons.leftCrewGunFirePulse),
                RightCrewGunPulse = V(weapons == null ? null : weapons.rightCrewGunFirePulse),
                LastFiredWeaponName = weapons == null ? string.Empty : weapons.lastFiredWeaponName ?? string.Empty,
                ChaffPulse = V(countermeasures == null ? null : countermeasures.chaffReleasePulse),
                FlarePulse = V(countermeasures == null ? null : countermeasures.flareReleasePulse),
                RotorRunning = dynamics != null && dynamics.rotorRunning,
                ApuRunning = dynamics != null && dynamics.apuRunning,
                Starter1 = dynamics != null && dynamics.engine1StarterActive,
                Starter2 = dynamics != null && dynamics.engine2StarterActive,
                LowRotorWarning = dynamics != null && dynamics.lowRotorRpmWarning,
                Ground = ground,
                Dynamics = dynamics
            };
        }

        private static double V(double? value) { return value.HasValue ? value.Value : 0.0; }
        private static bool B(double? value) { return value.HasValue && value.Value >= 0.5; }
        private static double MaxDoorPosition(DoorValues doors)
        {
            if (doors == null) return 0.0;
            return Math.Max(V(doors.pilotDoorPosition), Math.Max(V(doors.copilotDoorPosition),
                Math.Max(V(doors.leftCargoDoorPosition), Math.Max(V(doors.rightCargoDoorPosition),
                Math.Max(V(doors.leftGunnerDoorPosition), V(doors.rightGunnerDoorPosition))))));
        }
    }
}

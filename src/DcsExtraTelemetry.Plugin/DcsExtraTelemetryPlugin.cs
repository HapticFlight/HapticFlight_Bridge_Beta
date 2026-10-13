using DcsExtraTelemetry.Plugin.Telemetry;
using DcsExtraTelemetry.Configuration;
using GameReaderCommon;
using SimHub.Plugins;
using DcsExtraTelemetry.Plugin.Haptics;
using System;

namespace DcsExtraTelemetry.Plugin
{
    [PluginDescription("Additional DCS telemetry properties for SimHub and ShakeIt Bass Shakers")]
    [PluginAuthor("DCS Extra Telemetry")]
    [PluginName("DCS Extra Telemetry")]
    public sealed class DcsExtraTelemetryPlugin : IPlugin, IDataPlugin
    {
        private UdpTelemetryReceiver receiver;

        public PluginManager PluginManager { get; set; }

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            TelemetryPortSettings portSettings = TelemetryPortSettings.Load();
            receiver = new UdpTelemetryReceiver(
                portSettings.SimHubPort,
                message => SimHub.Logging.Current.Error(message));

            this.AttachDelegate("DCSExtra.Connected", () => receiver.IsConnected);
            this.AttachDelegate("DCSExtra.DataAgeMs", () => receiver.DataAgeMilliseconds);
            this.AttachDelegate("DCSExtra.PacketRateHz", () => receiver.PacketRateHz);
            this.AttachDelegate("DCSExtra.InvalidPackets", () => receiver.InvalidPackets);
            this.AttachDelegate("DCSExtra.DroppedPackets", () => receiver.DroppedPackets);
            this.AttachDelegate("DCSExtra.AircraftType", () => receiver.Current.AircraftType);
            this.AttachDelegate("DCSExtra.AircraftFamily", () => receiver.Current.AircraftFamily);
            this.AttachDelegate("DCSExtra.GroundType", () => receiver.Current.GroundType);
            this.AttachDelegate("DCSExtra.AccelXG", () => receiver.Current.AccelerationXG);
            this.AttachDelegate("DCSExtra.AccelYG", () => receiver.Current.AccelerationYG);
            this.AttachDelegate("DCSExtra.AccelZG", () => receiver.Current.AccelerationZG);
            this.AttachDelegate("DCSExtra.AngularXRadS", () => receiver.Current.AngularXRadS);
            this.AttachDelegate("DCSExtra.AngularYRadS", () => receiver.Current.AngularYRadS);
            this.AttachDelegate("DCSExtra.AngularZRadS", () => receiver.Current.AngularZRadS);
            this.AttachDelegate("DCSExtra.IndicatedAirSpeedMps", () => receiver.Current.IndicatedAirSpeedMps);
            this.AttachDelegate("DCSExtra.TrueAirSpeedMps", () => receiver.Current.TrueAirSpeedMps);
            this.AttachDelegate("DCSExtra.VerticalVelocityMps", () => receiver.Current.VerticalVelocityMps);
            this.AttachDelegate("DCSExtra.AngleOfAttackRad", () => receiver.Current.AngleOfAttackRad);
            this.AttachDelegate("DCSExtra.Mach", () => receiver.Current.Mach);
            this.AttachDelegate("DCSExtra.ShakeAmplitude", () => receiver.Current.ShakeAmplitude);
            this.AttachDelegate("DCSExtra.EngineRpmLeftPct", () => receiver.Current.EngineRpmLeftPct);
            this.AttachDelegate("DCSExtra.EngineRpmRightPct", () => receiver.Current.EngineRpmRightPct);
            this.AttachDelegate("DCSExtra.GearPosition", () => receiver.Current.GearPosition);
            this.AttachDelegate("DCSExtra.FlapsPosition", () => receiver.Current.FlapsPosition);
            this.AttachDelegate("DCSExtra.SpeedbrakePosition", () => receiver.Current.SpeedbrakePosition);
            this.AttachDelegate("DCSExtra.SelectedWeaponStation", () => receiver.Current.SelectedWeaponStation);
            this.AttachDelegate("DCSExtra.SelectedWeaponName", () => receiver.Current.SelectedWeaponName);
            this.AttachDelegate("DCSExtra.WeaponPayloadCount", () => receiver.Current.WeaponPayloadCount);
            this.AttachDelegate("DCSExtra.CannonShells", () => receiver.Current.CannonShells);
            this.AttachDelegate("DCSExtra.WeaponFirePulse", () => receiver.Current.WeaponFirePulse);
            this.AttachDelegate("DCSExtra.GunFirePulse", () => receiver.Current.GunFirePulse);
            this.AttachDelegate("DCSExtra.StationGunFirePulse", () => receiver.Current.StationGunFirePulse);
            this.AttachDelegate("DCSExtra.CrewGunFirePulse", () => receiver.Current.CrewGunFirePulse);
            this.AttachDelegate("DCSExtra.LeftCrewGunFirePulse", () => receiver.Current.LeftCrewGunFirePulse);
            this.AttachDelegate("DCSExtra.RightCrewGunFirePulse", () => receiver.Current.RightCrewGunFirePulse);
            this.AttachDelegate("DCSExtra.PayloadReleasePulse", () => receiver.Current.PayloadReleasePulse);
            this.AttachDelegate("DCSExtra.GunRoundsFired", () => receiver.Current.GunRoundsFired);
            this.AttachDelegate("DCSExtra.StationGunRoundsFired", () => receiver.Current.StationGunRoundsFired);
            this.AttachDelegate("DCSExtra.CrewGunRoundsFired", () => receiver.Current.CrewGunRoundsFired);
            this.AttachDelegate("DCSExtra.LeftCrewGunRoundsFired", () => receiver.Current.LeftCrewGunRoundsFired);
            this.AttachDelegate("DCSExtra.RightCrewGunRoundsFired", () => receiver.Current.RightCrewGunRoundsFired);
            this.AttachDelegate("DCSExtra.StationGunAmmoRemaining", () => receiver.Current.StationGunAmmoRemaining);
            this.AttachDelegate("DCSExtra.LeftCrewGunAmmoRemaining", () => receiver.Current.LeftCrewGunAmmoRemaining);
            this.AttachDelegate("DCSExtra.RightCrewGunAmmoRemaining", () => receiver.Current.RightCrewGunAmmoRemaining);
            this.AttachDelegate("DCSExtra.PayloadReleasedCount", () => receiver.Current.PayloadReleasedCount);
            this.AttachDelegate("DCSExtra.LastFiredWeaponStation", () => receiver.Current.LastFiredWeaponStation);
            this.AttachDelegate("DCSExtra.LastFiredWeaponName", () => receiver.Current.LastFiredWeaponName);
            this.AttachDelegate("DCSExtra.WeaponEventCounter", () => receiver.Current.WeaponEventCounter);
            this.AttachDelegate("DCSExtra.WeaponClass", () => receiver.Current.WeaponClass);
            this.AttachDelegate("DCSExtra.WeaponBurstActive", () => receiver.Current.WeaponBurstActive);
            this.AttachDelegate("DCSExtra.WeaponBurstDurationMs", () => receiver.Current.WeaponBurstDurationMs);
            this.AttachDelegate("DCSExtra.WeaponRateHz", () => receiver.Current.WeaponRateHz);
            this.AttachDelegate("DCSExtra.DetectedWeaponRateHz", () => receiver.Current.WeaponRateHz);
            this.AttachDelegate("DCSExtra.NominalWeaponRateHz", () => receiver.Current.NominalWeaponRateHz);
            this.AttachDelegate("DCSExtra.WeaponImpulseStrength", () => receiver.Current.WeaponImpulseStrength);
            this.AttachDelegate("DCSExtra.LightGunIntensity", () => receiver.Current.LightGunIntensity);
            this.AttachDelegate("DCSExtra.HeavyGunIntensity", () => receiver.Current.HeavyGunIntensity);
            this.AttachDelegate("DCSExtra.RocketLaunchIntensity", () => receiver.Current.RocketLaunchIntensity);
            this.AttachDelegate("DCSExtra.MissileLaunchIntensity", () => receiver.Current.MissileLaunchIntensity);
            this.AttachDelegate("DCSExtra.LoadoutSummary", () => receiver.Current.LoadoutSummary);
            this.AttachDelegate("DCSExtra.TouchdownEnvelope", () => receiver.Current.TouchdownEnvelope);
            this.AttachDelegate("DCSExtra.CombatDamageEnvelope", () => receiver.Current.CombatDamageEnvelope);
            this.AttachDelegate("DCSExtra.DoorMotionEnvelope", () => receiver.Current.DoorMotionEnvelope);
            this.AttachDelegate("DCSExtra.BrakeEnvelope", () => receiver.Current.BrakeEnvelope);
            this.AttachDelegate("DCSExtra.WeaponRecoilEnvelope", () => receiver.Current.WeaponRecoilEnvelope);
            this.AttachDelegate("DCSExtra.GroundSpeedMps", () => receiver.Current.GroundSpeedMps);
            this.AttachDelegate("DCSExtra.AltitudeAglM", () => receiver.Current.AltitudeAglM);
            this.AttachDelegate("DCSExtra.WheelBrakesPosition", () => receiver.Current.WheelBrakesPosition);
            this.AttachDelegate("DCSExtra.BrakeSource", () => receiver.Current.BrakeSource);
            this.AttachDelegate("DCSExtra.ParkingBrakeOn", () => receiver.Current.ParkingBrakeOn);
            this.AttachDelegate("DCSExtra.TailStrutCompression", () => receiver.Current.TailStrutCompression);
            this.AttachDelegate("DCSExtra.LeftMainStrutCompression", () => receiver.Current.LeftStrutCompression);
            this.AttachDelegate("DCSExtra.RightMainStrutCompression", () => receiver.Current.RightStrutCompression);
            this.AttachDelegate("DCSExtra.StrutCompressionRate", () => receiver.Current.StrutCompressionRate);
            this.AttachDelegate("DCSExtra.TailWheelRotation", () => receiver.Current.TailWheelRotation);
            this.AttachDelegate("DCSExtra.LeftWheelRotation", () => receiver.Current.LeftWheelRotation);
            this.AttachDelegate("DCSExtra.RightWheelRotation", () => receiver.Current.RightWheelRotation);
            this.AttachDelegate("DCSExtra.SkidFrontLeftCompression", () => receiver.Current.SkidFrontLeftCompression);
            this.AttachDelegate("DCSExtra.SkidFrontRightCompression", () => receiver.Current.SkidFrontRightCompression);
            this.AttachDelegate("DCSExtra.SkidRearLeftCompression", () => receiver.Current.SkidRearLeftCompression);
            this.AttachDelegate("DCSExtra.SkidRearRightCompression", () => receiver.Current.SkidRearRightCompression);
            this.AttachDelegate("DCSExtra.LeftSkidCompression", () => receiver.Current.LeftSkidCompression);
            this.AttachDelegate("DCSExtra.RightSkidCompression", () => receiver.Current.RightSkidCompression);
            this.AttachDelegate("DCSExtra.SkidFrontLeftContact", () => receiver.Current.SkidFrontLeftContact);
            this.AttachDelegate("DCSExtra.SkidFrontRightContact", () => receiver.Current.SkidFrontRightContact);
            this.AttachDelegate("DCSExtra.SkidRearLeftContact", () => receiver.Current.SkidRearLeftContact);
            this.AttachDelegate("DCSExtra.SkidRearRightContact", () => receiver.Current.SkidRearRightContact);
            this.AttachDelegate("DCSExtra.SkidFrontLeftContactPulse", () => receiver.Current.SkidFrontLeftContactPulse);
            this.AttachDelegate("DCSExtra.SkidFrontRightContactPulse", () => receiver.Current.SkidFrontRightContactPulse);
            this.AttachDelegate("DCSExtra.SkidRearLeftContactPulse", () => receiver.Current.SkidRearLeftContactPulse);
            this.AttachDelegate("DCSExtra.SkidRearRightContactPulse", () => receiver.Current.SkidRearRightContactPulse);
            this.AttachDelegate("DCSExtra.StrutSource", () => receiver.Current.StrutSource);
            this.AttachDelegate("DCSExtra.OnGround", () => receiver.Current.OnGround);
            this.AttachDelegate("DCSExtra.TailWheelContact", () => receiver.Current.TailWheelContact);
            this.AttachDelegate("DCSExtra.LeftWheelContact", () => receiver.Current.LeftWheelContact);
            this.AttachDelegate("DCSExtra.RightWheelContact", () => receiver.Current.RightWheelContact);
            this.AttachDelegate("DCSExtra.TailWheelContactPulse", () => receiver.Current.TailWheelContactPulse);
            this.AttachDelegate("DCSExtra.LeftWheelContactPulse", () => receiver.Current.LeftWheelContactPulse);
            this.AttachDelegate("DCSExtra.RightWheelContactPulse", () => receiver.Current.RightWheelContactPulse);
            this.AttachDelegate("DCSExtra.TouchdownPulse", () => receiver.Current.TouchdownPulse);
            this.AttachDelegate("DCSExtra.TouchdownVerticalSpeedMps", () => receiver.Current.TouchdownVerticalSpeedMps);
            this.AttachDelegate("DCSExtra.TouchdownStrength", () => receiver.Current.TouchdownStrength);
            this.AttachDelegate("DCSExtra.SuspensionMovementIntensity", () => receiver.Current.SuspensionMovementIntensity);
            this.AttachDelegate("DCSExtra.GroundRollIntensity", () => receiver.Current.GroundRollIntensity);
            this.AttachDelegate("DCSExtra.GroundResonanceIntensity", () => receiver.Current.GroundResonanceIntensity);
            this.AttachDelegate("DCSExtra.BrakingIntensity", () => receiver.Current.BrakingIntensity);
            this.AttachDelegate("DCSExtra.BrakeDecelerationIntensity", () => receiver.Current.BrakeDecelerationIntensity);
            this.AttachDelegate("DCSExtra.WheelSkidIntensity", () => receiver.Current.WheelSkidIntensity);
            this.AttachDelegate("DCSExtra.GroundPitchRockIntensity", () => receiver.Current.GroundPitchRockIntensity);
            this.AttachDelegate("DCSExtra.GroundRollRockIntensity", () => receiver.Current.GroundRollRockIntensity);
            this.AttachDelegate("DCSExtra.LongitudinalGroundSpeedMps", () => receiver.Current.LongitudinalGroundSpeedMps);
            this.AttachDelegate("DCSExtra.LateralGroundSlipMps", () => receiver.Current.LateralGroundSlipMps);
            this.AttachDelegate("DCSExtra.SkidScrubIntensity", () => receiver.Current.SkidScrubIntensity);
            this.AttachDelegate("DCSExtra.WheelShimmyIntensity", () => receiver.Current.WheelShimmyIntensity);
            this.AttachDelegate("DCSExtra.TailWheelCasterIntensity", () => receiver.Current.TailWheelCasterIntensity);
            this.AttachDelegate("DCSExtra.LiftoffPulse", () => receiver.Current.LiftoffPulse);
            this.AttachDelegate("DCSExtra.WeightOnGroundIntensity", () => receiver.Current.WeightOnGroundIntensity);
            this.AttachDelegate("DCSExtra.ChaffRemaining", () => receiver.Current.ChaffRemaining);
            this.AttachDelegate("DCSExtra.FlareRemaining", () => receiver.Current.FlareRemaining);
            this.AttachDelegate("DCSExtra.ChaffReleasePulse", () => receiver.Current.ChaffReleasePulse);
            this.AttachDelegate("DCSExtra.FlareReleasePulse", () => receiver.Current.FlareReleasePulse);
            this.AttachDelegate("DCSExtra.ChaffReleasedCount", () => receiver.Current.ChaffReleasedCount);
            this.AttachDelegate("DCSExtra.FlareReleasedCount", () => receiver.Current.FlareReleasedCount);
            this.AttachDelegate("DCSExtra.MasterArmOn", () => receiver.Current.MasterArmOn);
            this.AttachDelegate("DCSExtra.WeaponSelectorPosition", () => receiver.Current.WeaponSelectorPosition);
            this.AttachDelegate("DCSExtra.WeaponSelectorName", () => receiver.Current.WeaponSelectorName);
            this.AttachDelegate("DCSExtra.RocketPairMode", () => receiver.Current.RocketPairMode);
            this.AttachDelegate("DCSExtra.RocketRippleMode", () => receiver.Current.RocketRippleMode);
            this.AttachDelegate("DCSExtra.CountermeasureArmed", () => receiver.Current.CountermeasureArmed);
            this.AttachDelegate("DCSExtra.CountermeasureMode", () => receiver.Current.CountermeasureMode);
            this.AttachDelegate("DCSExtra.PilotDoorPosition", () => receiver.Current.PilotDoorPosition);
            this.AttachDelegate("DCSExtra.CopilotDoorPosition", () => receiver.Current.CopilotDoorPosition);
            this.AttachDelegate("DCSExtra.LeftCargoDoorPosition", () => receiver.Current.LeftCargoDoorPosition);
            this.AttachDelegate("DCSExtra.RightCargoDoorPosition", () => receiver.Current.RightCargoDoorPosition);
            this.AttachDelegate("DCSExtra.LeftGunnerDoorPosition", () => receiver.Current.LeftGunnerDoorPosition);
            this.AttachDelegate("DCSExtra.RightGunnerDoorPosition", () => receiver.Current.RightGunnerDoorPosition);
            this.AttachDelegate("DCSExtra.DoorMovementIntensity", () => receiver.Current.DoorMovementIntensity);
            this.AttachDelegate("DCSExtra.MainRotorRpmPct", () => receiver.Current.MainRotorRpmPct);
            this.AttachDelegate("DCSExtra.MainRotorRpm", () => receiver.Current.MainRotorRpm);
            this.AttachDelegate("DCSExtra.MainRotorHz", () => receiver.Current.MainRotorHz);
            this.AttachDelegate("DCSExtra.BladePassHz", () => receiver.Current.BladePassHz);
            this.AttachDelegate("DCSExtra.RotorSource", () => receiver.Current.RotorSource);
            this.AttachDelegate("DCSExtra.CollectivePosition", () => receiver.Current.CollectivePosition);
            this.AttachDelegate("DCSExtra.CollectiveMovementRate", () => receiver.Current.CollectiveMovementRate);
            this.AttachDelegate("DCSExtra.EngineTorqueLeftPct", () => receiver.Current.EngineTorqueLeftPct);
            this.AttachDelegate("DCSExtra.EngineTorqueRightPct", () => receiver.Current.EngineTorqueRightPct);
            this.AttachDelegate("DCSExtra.TorqueGaugeNormalized", () => receiver.Current.TorqueGaugeNormalized);
            this.AttachDelegate("DCSExtra.RotorLoadIntensity", () => receiver.Current.RotorLoadIntensity);
            this.AttachDelegate("DCSExtra.TransmissionLoadIntensity", () => receiver.Current.TransmissionLoadIntensity);
            this.AttachDelegate("DCSExtra.EngineGovernorResponseIntensity", () => receiver.Current.EngineGovernorResponseIntensity);
            this.AttachDelegate("DCSExtra.RotorDroopIntensity", () => receiver.Current.RotorDroopIntensity);
            this.AttachDelegate("DCSExtra.RotorSpoolIntensity", () => receiver.Current.RotorSpoolIntensity);
            this.AttachDelegate("DCSExtra.TurbulenceIntensity", () => receiver.Current.TurbulenceIntensity);
            this.AttachDelegate("DCSExtra.ControlResponseIntensity", () => receiver.Current.ControlResponseIntensity);
            this.AttachDelegate("DCSExtra.AirframeBuffetIntensity", () => receiver.Current.AirframeBuffetIntensity);
            this.AttachDelegate("DCSExtra.ManeuverLoadIntensity", () => receiver.Current.ManeuverLoadIntensity);
            this.AttachDelegate("DCSExtra.NormalG", () => receiver.Current.NormalG);
            this.AttachDelegate("DCSExtra.NormalGDeviation", () => receiver.Current.NormalGDeviation);
            this.AttachDelegate("DCSExtra.PositiveGIntensity", () => receiver.Current.PositiveGIntensity);
            this.AttachDelegate("DCSExtra.NegativeGIntensity", () => receiver.Current.NegativeGIntensity);
            this.AttachDelegate("DCSExtra.ManeuverResponseIntensity", () => receiver.Current.ManeuverResponseIntensity);
            this.AttachDelegate("DCSExtra.LateralManeuverIntensity", () => receiver.Current.LateralManeuverIntensity);
            this.AttachDelegate("DCSExtra.SettlingWithPowerCue", () => receiver.Current.SettlingWithPowerCue);
            this.AttachDelegate("DCSExtra.VrsRiskIntensity", () => receiver.Current.VrsRiskIntensity);
            this.AttachDelegate("DCSExtra.TranslationalLiftTransition", () => receiver.Current.TranslationalLiftTransition);
            this.AttachDelegate("DCSExtra.BladeSlapIntensity", () => receiver.Current.BladeSlapIntensity);
            this.AttachDelegate("DCSExtra.HardManeuverPulse", () => receiver.Current.HardManeuverPulse);
            this.AttachDelegate("DCSExtra.AirframeImpactPulse", () => receiver.Current.AirframeImpactPulse);
            this.AttachDelegate("DCSExtra.RotorRunning", () => receiver.Current.RotorRunning);
            this.AttachDelegate("DCSExtra.ApuRunning", () => receiver.Current.ApuRunning);
            this.AttachDelegate("DCSExtra.Engine1StarterActive", () => receiver.Current.Engine1StarterActive);
            this.AttachDelegate("DCSExtra.Engine2StarterActive", () => receiver.Current.Engine2StarterActive);
            this.AttachDelegate("DCSExtra.LowRotorRpmWarning", () => receiver.Current.LowRotorRpmWarning);
            this.AttachDelegate("DCSExtra.Has.Acceleration", () => receiver.Current.HasAcceleration);
            this.AttachDelegate("DCSExtra.Has.AngularVelocity", () => receiver.Current.HasAngularVelocity);
            this.AttachDelegate("DCSExtra.Has.AirData", () => receiver.Current.HasAirData);
            this.AttachDelegate("DCSExtra.Has.EngineRpm", () => receiver.Current.HasEngineRpm);
            this.AttachDelegate("DCSExtra.Has.Gear", () => receiver.Current.HasGear);
            this.AttachDelegate("DCSExtra.Has.Flaps", () => receiver.Current.HasFlaps);
            this.AttachDelegate("DCSExtra.Has.Speedbrake", () => receiver.Current.HasSpeedbrake);
            this.AttachDelegate("DCSExtra.Has.ShakeAmplitude", () => receiver.Current.HasShakeAmplitude);
            this.AttachDelegate("DCSExtra.Has.WeaponPayload", () => receiver.Current.HasWeaponPayload);
            this.AttachDelegate("DCSExtra.Has.WeaponEvents", () => receiver.Current.HasWeaponEvents);
            this.AttachDelegate("DCSExtra.Has.StationGuns", () => receiver.Current.HasStationGuns);
            this.AttachDelegate("DCSExtra.Has.CrewGuns", () => receiver.Current.HasCrewGuns);
            this.AttachDelegate("DCSExtra.Has.GearStruts", () => receiver.Current.HasGearStruts);
            this.AttachDelegate("DCSExtra.Has.GroundMotion", () => receiver.Current.HasGroundMotion);
            this.AttachDelegate("DCSExtra.Has.WheelRotation", () => receiver.Current.HasWheelRotation);
            this.AttachDelegate("DCSExtra.Has.Skids", () => receiver.Current.HasSkids);
            this.AttachDelegate("DCSExtra.Has.Countermeasures", () => receiver.Current.HasCountermeasures);
            this.AttachDelegate("DCSExtra.Has.CockpitStates", () => receiver.Current.HasCockpitStates);
            this.AttachDelegate("DCSExtra.Has.RotorDynamics", () => receiver.Current.HasRotorDynamics);
            this.AttachDelegate("DCSExtra.Has.ControlPositions", () => receiver.Current.HasControlPositions);
            this.AttachDelegate("DCSExtra.Has.EngineTorque", () => receiver.Current.HasEngineTorque);
            this.AttachDelegate("DCSExtra.Has.TorqueGauge", () => receiver.Current.HasTorqueGauge);
            this.AttachDelegate("DCSExtra.Has.RotorWarning", () => receiver.Current.HasRotorWarning);
            this.AttachDelegate("DCSExtra.Has.PowertrainEffects", () => receiver.Current.HasPowertrainEffects);
            this.AttachDelegate("DCSExtra.Has.AerodynamicEffects", () => receiver.Current.HasAerodynamicEffects);
            this.AttachDelegate("DCSExtra.Has.BladeSlap", () => receiver.Current.HasBladeSlap);
            this.AttachDelegate("DCSExtra.Has.DetailedGroundEffects", () => receiver.Current.HasDetailedGroundEffects);
            this.AttachDelegate("DCSExtra.Has.WeaponEffects", () => receiver.Current.HasWeaponEffects);
            this.AttachDelegate("DCSExtra.Has.EngineLifecycle", () => receiver.Current.HasEngineLifecycle);
            this.AttachDelegate("DCSExtra.Has.Brakes", () => receiver.Current.HasBrakes);
            this.AttachDelegate("DCSExtra.Has.Doors", () => receiver.Current.HasDoors);
            RegisterHapticProperties();

            try
            {
                receiver.Start();
                SimHub.Logging.Current.Info("[DCS Extra Telemetry] Listening on 127.0.0.1:" + portSettings.SimHubPort);
            }
            catch (System.Exception ex)
            {
                SimHub.Logging.Current.Error("[DCS Extra Telemetry] Could not start UDP receiver: " + ex.Message);
            }
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Intentionally empty. Network I/O runs only on the background receiver thread.
        }

        private void RegisterHapticProperties()
        {
            this.AttachDelegate("DCSExtra.Haptic.EngineVersion", () => receiver.Current.Haptics.EngineVersion);
            this.AttachDelegate("DCSExtra.Haptic.ModelId", () => receiver.Current.Haptics.ModelId);
            this.AttachDelegate("DCSExtra.Haptic.ModelVersion", () => receiver.Current.Haptics.ModelVersion);
            this.AttachDelegate("DCSExtra.Haptic.ModelStatus", () => receiver.Current.Haptics.ModelStatus);
            this.AttachDelegate("DCSExtra.Haptic.LastResetReason", () => receiver.Current.Haptics.LastResetReason.ToString());

            RegisterHapticSignal("Touchdown", HapticSignalName.Touchdown, true);
            this.AttachDelegate("DCSExtra.Haptic.Touchdown.Pulse", () => ReadHapticValue(HapticSignalName.Touchdown, true, true) > 0.0001 ? 1.0 : 0.0);
            this.AttachDelegate("DCSExtra.Haptic.Touchdown.Strength", () => ReadHapticValue(HapticSignalName.Touchdown, false, true));
            RegisterHapticSignal("Liftoff", HapticSignalName.Liftoff, true);
            RegisterHapticSignal("Contact.Tail", HapticSignalName.TailContact, true);
            RegisterHapticSignal("Contact.Left", HapticSignalName.LeftContact, true);
            RegisterHapticSignal("Contact.Right", HapticSignalName.RightContact, true);
            RegisterHapticSignal("Skid.FrontLeft", HapticSignalName.SkidFrontLeft, true);
            RegisterHapticSignal("Skid.FrontRight", HapticSignalName.SkidFrontRight, true);
            RegisterHapticSignal("Skid.RearLeft", HapticSignalName.SkidRearLeft, true);
            RegisterHapticSignal("Skid.RearRight", HapticSignalName.SkidRearRight, true);
            RegisterHapticSignal("Ground.Roll", HapticSignalName.GroundRoll, false);
            RegisterHapticSignal("Ground.Bump", HapticSignalName.GroundBump, true);
            RegisterHapticSignal("Ground.Settling", HapticSignalName.GroundSettling, true);
            RegisterHapticSignal("Ground.Resonance", HapticSignalName.GroundResonance, false);
            RegisterHapticSignal("Ground.Suspension", HapticSignalName.SuspensionMovement, false);
            RegisterHapticSignal("Ground.PitchRock", HapticSignalName.GroundPitchRock, false);
            RegisterHapticSignal("Ground.RollRock", HapticSignalName.GroundRollRock, false);
            RegisterHapticSignal("Ground.Weight", HapticSignalName.WeightOnGround, false);
            RegisterHapticSignal("Ground.SkidScrub", HapticSignalName.SkidScrub, false);
            RegisterHapticSignal("Ground.WheelShimmy", HapticSignalName.WheelShimmy, false);
            RegisterHapticSignal("Ground.TailwheelCaster", HapticSignalName.TailwheelCaster, false);
            RegisterHapticSignal("Braking", HapticSignalName.Braking, false);
            RegisterHapticSignal("Braking.Application", HapticSignalName.BrakeApplication, false);
            RegisterHapticSignal("Braking.Deceleration", HapticSignalName.BrakeDeceleration, false);
            RegisterHapticSignal("Ground.WheelSkid", HapticSignalName.WheelSkid, false);
            RegisterHapticSignal("Rotor.Presence", HapticSignalName.RotorPresence, false);
            RegisterHapticSignal("Rotor.Load", HapticSignalName.RotorLoad, false);
            RegisterHapticSignal("Rotor.TransmissionLoad", HapticSignalName.TransmissionLoad, false);
            RegisterHapticSignal("Rotor.Spool", HapticSignalName.RotorSpool, false);
            RegisterHapticSignal("Rotor.Droop", HapticSignalName.RotorDroop, false);
            RegisterHapticSignal("Rotor.GovernorResponse", HapticSignalName.GovernorResponse, false);
            RegisterHapticSignal("Maneuver.Vertical", HapticSignalName.VerticalManeuver, false);
            RegisterHapticSignal("Maneuver.PositiveLoad", HapticSignalName.PositiveLoad, false);
            RegisterHapticSignal("Maneuver.NegativeLoad", HapticSignalName.NegativeLoad, false);
            RegisterHapticSignal("Maneuver.Lateral", HapticSignalName.LateralManeuver, false);
            RegisterHapticSignal("Maneuver.ControlResponse", HapticSignalName.ControlResponse, false);
            RegisterHapticSignal("Maneuver.Hard", HapticSignalName.HardManeuver, true);
            RegisterHapticSignal("Aerodynamic.Turbulence", HapticSignalName.Turbulence, false);
            RegisterHapticSignal("Aerodynamic.Buffet", HapticSignalName.Buffet, false);
            RegisterHapticSignal("Aerodynamic.TranslationalLift", HapticSignalName.TranslationalLift, false);
            RegisterHapticSignal("Aerodynamic.SettlingWithPower", HapticSignalName.SettlingWithPower, false);
            RegisterHapticSignal("Aerodynamic.VrsRisk", HapticSignalName.SettlingWithPower, false);
            RegisterHapticSignal("Aerodynamic.BladeSlap", HapticSignalName.BladeSlap, false);
            RegisterHapticSignal("Airframe.Shock", HapticSignalName.AirframeShock, true);
            RegisterHapticSignal("Mechanical.Doors", HapticSignalName.DoorMovement, false);
            RegisterHapticSignal("Mechanical.DoorsEndpoint", HapticSignalName.DoorEndpoint, true);
            RegisterHapticSignal("Mechanical.Gear", HapticSignalName.GearMovement, false);
            RegisterHapticSignal("Mechanical.GearEndpoint", HapticSignalName.GearEndpoint, true);
            RegisterHapticSignal("Mechanical.Flaps", HapticSignalName.FlapsMovement, false);
            RegisterHapticSignal("Mechanical.FlapsEndpoint", HapticSignalName.FlapsEndpoint, true);
            RegisterHapticSignal("Mechanical.Speedbrake", HapticSignalName.SpeedbrakeMovement, false);
            RegisterHapticSignal("Mechanical.SpeedbrakeEndpoint", HapticSignalName.SpeedbrakeEndpoint, true);
            RegisterHapticSignal("Weapon.LightGun", HapticSignalName.LightGun, false);
            RegisterHapticSignal("Weapon.HeavyGun", HapticSignalName.HeavyGun, false);
            RegisterHapticSignal("Weapon.Rocket", HapticSignalName.RocketLaunch, true);
            RegisterHapticSignal("Weapon.Missile", HapticSignalName.MissileLaunch, true);
            RegisterHapticSignal("Weapon.Recoil", HapticSignalName.WeaponRecoil, false);
            RegisterHapticSignal("Weapon.Burst", HapticSignalName.WeaponBurst, false);
            RegisterHapticSignal("Countermeasure.Chaff", HapticSignalName.ChaffRelease, true);
            RegisterHapticSignal("Countermeasure.Flare", HapticSignalName.FlareRelease, true);
            RegisterHapticSignal("Lifecycle.RotorRunning", HapticSignalName.RotorRunning, false);
            RegisterHapticSignal("Lifecycle.Apu", HapticSignalName.ApuRunning, false);
            RegisterHapticSignal("Lifecycle.Starter1", HapticSignalName.Starter1, false);
            RegisterHapticSignal("Lifecycle.Starter2", HapticSignalName.Starter2, false);
            RegisterHapticSignal("Lifecycle.LowRotorWarning", HapticSignalName.LowRotorWarning, false);

            RegisterHapticSignal("UH60.TailwheelSettle", HapticSignalName.Uh60TailwheelSettle, true);
            RegisterHapticSignal("UH60.MainGearLoading", HapticSignalName.Uh60MainGearLoading, false);
            RegisterHapticSignal("UH60.WheelShimmy", HapticSignalName.Uh60WheelShimmy, false);
            RegisterHapticSignal("UH60.TailwheelCaster", HapticSignalName.Uh60TailwheelCaster, false);
            RegisterHapticSignal("UH60.BrakeTailRecovery", HapticSignalName.Uh60BrakeTailRecovery, true);
            RegisterHapticSignal("UH60.LeftDoorGun", HapticSignalName.Uh60LeftDoorGun, true);
            RegisterHapticSignal("UH60.RightDoorGun", HapticSignalName.Uh60RightDoorGun, true);
            RegisterHapticSignal("UH60.DapCannon", HapticSignalName.Uh60DapCannon, false);
            RegisterHapticSignal("AH6.SkidForeAftLoad", HapticSignalName.Ah6SkidForeAftLoad, false);
            RegisterHapticSignal("AH6.SkidLateralLoad", HapticSignalName.Ah6SkidLateralLoad, false);
            RegisterHapticSignal("AH6.LightLanding", HapticSignalName.Ah6LightLanding, true);
            RegisterHapticSignal("AH64.TailMainSequence", HapticSignalName.Ah64TailMainSequence, true);
            RegisterHapticSignal("AH64.HeavyLanding", HapticSignalName.Ah64HeavyLanding, true);
            RegisterHapticSignal("AH64.M230Recoil", HapticSignalName.Ah64M230Recoil, false);
            RegisterHapticSignal("AH64.HydraLaunch", HapticSignalName.Ah64HydraLaunch, true);
            RegisterHapticSignal("AH64.HellfireLaunch", HapticSignalName.Ah64HellfireLaunch, true);
            RegisterHapticSignal("UH1.WeightOnSkidsLanding", HapticSignalName.Uh1WeightOnSkidsLanding, true);
            RegisterHapticSignal("UH1.TwoBladeResponse", HapticSignalName.Uh1TwoBladeResponse, false);
            RegisterHapticSignal("UH1.TorqueLoad", HapticSignalName.Uh1TorqueLoad, false);
            RegisterHapticSignal("OH58.SkidForeAftLoad", HapticSignalName.Oh58SkidForeAftLoad, false);
            RegisterHapticSignal("OH58.SkidLateralLoad", HapticSignalName.Oh58SkidLateralLoad, false);
            RegisterHapticSignal("Timing.DeltaSeconds", HapticSignalName.TimingDeltaSeconds, false);
            RegisterHapticSignal("Mix.ContinuousGain", HapticSignalName.MixContinuousGain, false);
            RegisterHapticSignal("Mix.PriorityDemand", HapticSignalName.MixPriorityDemand, false);
        }

        private void RegisterHapticSignal(string publicName, HapticSignalName signalName, bool eventStyle)
        {
            string prefix = "DCSExtra.Haptic." + publicName;
            string capabilityName = "DCSExtra.Has.Haptic" + publicName.Replace(".", string.Empty);
            this.AttachDelegate(capabilityName, () => receiver.Current.Haptics.Get(signalName).Supported);
            this.AttachDelegate(prefix + ".Active", () => ReadHapticValue(signalName, true, eventStyle) > 0.0001);
            this.AttachDelegate(prefix + ".Intensity", () => ReadHapticValue(signalName, false, false));
            this.AttachDelegate(prefix + ".Envelope", () => ReadHapticValue(signalName, true, eventStyle));
            this.AttachDelegate(prefix + ".Phase", () => ReadHapticValue(signalName, true, eventStyle) > 0.0001
                ? receiver.Current.Haptics.Get(signalName).Phase.ToString()
                : (receiver.Current.Haptics.Get(signalName).Supported ? "Idle" : "Unsupported"));
            this.AttachDelegate(prefix + ".EventId", () => receiver.Current.Haptics.Get(signalName).EventId);
            this.AttachDelegate(prefix + ".Confidence", () => receiver.Current.Haptics.Get(signalName).Confidence.ToString());
            this.AttachDelegate(prefix + ".Source", () => receiver.Current.Haptics.Get(signalName).Source);
        }

        private double ReadHapticValue(HapticSignalName signalName, bool envelope, bool eventStyle)
        {
            HapticStateSnapshot snapshot = receiver.Current.Haptics;
            HapticSignalState signal = snapshot.Get(signalName);
            double value = envelope ? signal.Envelope : signal.Intensity;
            return eventStyle
                ? receiver.HapticEventValue(value, snapshot.ReceivedAtUtc)
                : receiver.HapticContinuousValue(value, snapshot.ReceivedAtUtc);
        }

        public void End(PluginManager pluginManager)
        {
            if (receiver != null)
            {
                receiver.Dispose();
            }

            SimHub.Logging.Current.Info("[DCS Extra Telemetry] Stopped");
        }
    }
}

using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal enum HapticSignalName
    {
        Touchdown,
        Liftoff,
        TailContact,
        LeftContact,
        RightContact,
        SkidFrontLeft,
        SkidFrontRight,
        SkidRearLeft,
        SkidRearRight,
        GroundRoll,
        GroundResonance,
        SuspensionMovement,
        GroundPitchRock,
        GroundRollRock,
        WeightOnGround,
        SkidScrub,
        WheelShimmy,
        TailwheelCaster,
        Braking,
        BrakeApplication,
        BrakeDeceleration,
        WheelSkid,
        RotorPresence,
        RotorLoad,
        TransmissionLoad,
        RotorSpool,
        RotorDroop,
        GovernorResponse,
        VerticalManeuver,
        PositiveLoad,
        NegativeLoad,
        LateralManeuver,
        ControlResponse,
        HardManeuver,
        Turbulence,
        Buffet,
        TranslationalLift,
        SettlingWithPower,
        BladeSlap,
        AirframeShock,
        DoorMovement,
        DoorEndpoint,
        GearMovement,
        GearEndpoint,
        FlapsMovement,
        FlapsEndpoint,
        SpeedbrakeMovement,
        SpeedbrakeEndpoint,
        LightGun,
        HeavyGun,
        RocketLaunch,
        MissileLaunch,
        WeaponRecoil,
        WeaponBurst,
        ChaffRelease,
        FlareRelease,
        RotorRunning,
        ApuRunning,
        Starter1,
        Starter2,
        LowRotorWarning,
        Uh60TailwheelSettle,
        Uh60MainGearLoading,
        Uh60WheelShimmy,
        Uh60TailwheelCaster,
        Uh60BrakeTailRecovery,
        Uh60LeftDoorGun,
        Uh60RightDoorGun,
        Uh60DapCannon,
        Ah6SkidForeAftLoad,
        Ah6SkidLateralLoad,
        Ah6LightLanding,
        Ah64TailMainSequence,
        Ah64HeavyLanding,
        Ah64M230Recoil,
        Ah64HydraLaunch,
        Ah64HellfireLaunch,
        Uh1WeightOnSkidsLanding,
        Uh1TwoBladeResponse,
        Uh1TorqueLoad,
        Oh58SkidForeAftLoad,
        Oh58SkidLateralLoad,
        GroundBump,
        GroundSettling,
        TimingDeltaSeconds,
        MixContinuousGain,
        MixPriorityDemand,
        Count
    }

    internal sealed class HapticStateSnapshot
    {
        public const string CurrentEngineVersion = "1.7.1";
        private readonly HapticSignalState[] signals;

        public static readonly HapticStateSnapshot Empty = new HapticStateSnapshot(
            string.Empty, string.Empty, string.Empty, "Not initialized", HapticResetReason.Initial,
            DateTime.MinValue, new HapticSignalState[(int)HapticSignalName.Count]);

        public HapticStateSnapshot(
            string aircraftType,
            string modelId,
            string modelVersion,
            string modelStatus,
            HapticResetReason lastResetReason,
            DateTime receivedAtUtc,
            HapticSignalState[] signals)
        {
            AircraftType = aircraftType ?? string.Empty;
            ModelId = modelId ?? string.Empty;
            ModelVersion = modelVersion ?? string.Empty;
            ModelStatus = modelStatus ?? string.Empty;
            LastResetReason = lastResetReason;
            ReceivedAtUtc = receivedAtUtc;
            this.signals = new HapticSignalState[(int)HapticSignalName.Count];
            for (int index = 0; index < this.signals.Length; index++)
            {
                HapticSignalState signal = signals != null && index < signals.Length ? signals[index] : null;
                this.signals[index] = signal ?? HapticSignalState.Unsupported;
            }
        }

        public string EngineVersion { get { return CurrentEngineVersion; } }
        public string AircraftType { get; private set; }
        public string ModelId { get; private set; }
        public string ModelVersion { get; private set; }
        public string ModelStatus { get; private set; }
        public HapticResetReason LastResetReason { get; private set; }
        public DateTime ReceivedAtUtc { get; private set; }

        public HapticSignalState Get(HapticSignalName name)
        {
            int index = (int)name;
            return index >= 0 && index < signals.Length ? signals[index] : HapticSignalState.Unsupported;
        }
    }

    internal sealed class HapticStateBuilder
    {
        private readonly HapticSignalState[] signals = new HapticSignalState[(int)HapticSignalName.Count];

        public void Set(HapticSignalName name, HapticSignalState value)
        {
            signals[(int)name] = value ?? HapticSignalState.Unsupported;
        }

        public HapticSignalState Get(HapticSignalName name)
        {
            return signals[(int)name] ?? HapticSignalState.Unsupported;
        }

        public HapticStateSnapshot Build(
            string aircraftType,
            string modelId,
            string modelVersion,
            string modelStatus,
            HapticResetReason resetReason,
            DateTime receivedAtUtc)
        {
            return new HapticStateSnapshot(aircraftType, modelId, modelVersion, modelStatus, resetReason, receivedAtUtc, signals);
        }
    }
}

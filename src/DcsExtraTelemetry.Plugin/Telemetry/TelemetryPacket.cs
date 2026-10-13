namespace DcsExtraTelemetry.Plugin.Telemetry
{
    internal sealed class TelemetryPacket
    {
        public int protocol { get; set; }

        public string sessionId { get; set; }

        public long sequence { get; set; }

        public double sentAtMs { get; set; }

        public double modelTime { get; set; }

        public AircraftIdentity aircraft { get; set; }

        public TelemetryCapabilities capabilities { get; set; }

        public TelemetryValues values { get; set; }
    }

    internal sealed class AircraftIdentity
    {
        public string dcsType { get; set; }
        public string family { get; set; }
        public string groundType { get; set; }
    }

    internal sealed class TelemetryValues
    {
        public Vector3 acceleration { get; set; }

        public Vector3 angularVelocity { get; set; }

        public double? indicatedAirSpeedMps { get; set; }

        public double? trueAirSpeedMps { get; set; }

        public double? verticalVelocityMps { get; set; }

        public double? angleOfAttackRad { get; set; }

        public double? mach { get; set; }

        public double? shakeAmplitude { get; set; }

        public ScalarPair engineRpmPct { get; set; }

        public WeaponValues weapons { get; set; }

        public GroundValues ground { get; set; }

        public CountermeasureValues countermeasures { get; set; }

        public CockpitValues cockpit { get; set; }

        public DynamicsValues dynamics { get; set; }

        public MechanicalValues mechanical { get; set; }
        public EffectEnvelopeValues effects { get; set; }
    }

    internal sealed class EffectEnvelopeValues
    {
        public double? touchdownEnvelope { get; set; }
        public double? combatDamageEnvelope { get; set; }
        public double? doorMotionEnvelope { get; set; }
        public double? brakeEnvelope { get; set; }
        public double? weaponRecoilEnvelope { get; set; }
    }

    internal sealed class GroundValues
    {
        public double? groundSpeedMps { get; set; }
        public double? altitudeAglM { get; set; }
        public double? wheelBrakesPosition { get; set; }
        public string brakeSource { get; set; }
        public bool parkingBrakeOn { get; set; }
        public double? tailStrutCompression { get; set; }
        public double? leftStrutCompression { get; set; }
        public double? rightStrutCompression { get; set; }
        public double? strutCompressionRate { get; set; }
        public double? tailWheelRotation { get; set; }
        public double? leftWheelRotation { get; set; }
        public double? rightWheelRotation { get; set; }
        public double? skidFrontLeftCompression { get; set; }
        public double? skidFrontRightCompression { get; set; }
        public double? skidRearLeftCompression { get; set; }
        public double? skidRearRightCompression { get; set; }
        public double? leftSkidCompression { get; set; }
        public double? rightSkidCompression { get; set; }
        public double? skidFrontLeftContact { get; set; }
        public double? skidFrontRightContact { get; set; }
        public double? skidRearLeftContact { get; set; }
        public double? skidRearRightContact { get; set; }
        public double? skidFrontLeftContactPulse { get; set; }
        public double? skidFrontRightContactPulse { get; set; }
        public double? skidRearLeftContactPulse { get; set; }
        public double? skidRearRightContactPulse { get; set; }
        public string strutSource { get; set; }
        public double? onGround { get; set; }
        public double? tailWheelContact { get; set; }
        public double? leftWheelContact { get; set; }
        public double? rightWheelContact { get; set; }
        public double? tailWheelContactPulse { get; set; }
        public double? leftWheelContactPulse { get; set; }
        public double? rightWheelContactPulse { get; set; }
        public double? touchdownPulse { get; set; }
        public double? touchdownVerticalSpeedMps { get; set; }
        public double? touchdownStrength { get; set; }
        public double? suspensionMovementIntensity { get; set; }
        public double? groundRollIntensity { get; set; }
        public double? groundResonanceIntensity { get; set; }
        public double? brakingIntensity { get; set; }
        public double? brakeDecelerationIntensity { get; set; }
        public double? wheelSkidIntensity { get; set; }
        public double? groundPitchRockIntensity { get; set; }
        public double? groundRollRockIntensity { get; set; }
        public double? longitudinalGroundSpeedMps { get; set; }
        public double? lateralGroundSlipMps { get; set; }
        public double? skidScrubIntensity { get; set; }
        public double? wheelShimmyIntensity { get; set; }
        public double? tailWheelCasterIntensity { get; set; }
        public double? liftoffPulse { get; set; }
        public double? weightOnGroundIntensity { get; set; }
    }

    internal sealed class CountermeasureValues
    {
        public double? chaffRemaining { get; set; }
        public double? flareRemaining { get; set; }
        public double? chaffReleasePulse { get; set; }
        public double? flareReleasePulse { get; set; }
        public double? chaffReleasedCount { get; set; }
        public double? flareReleasedCount { get; set; }
    }

    internal sealed class CockpitValues
    {
        public bool masterArmOn { get; set; }
        public double? weaponSelectorPosition { get; set; }
        public string weaponSelectorName { get; set; }
        public bool rocketPairMode { get; set; }
        public bool rocketRippleMode { get; set; }
        public bool countermeasureArmed { get; set; }
        public double? countermeasureMode { get; set; }
        public DoorValues doors { get; set; }
    }

    internal sealed class DoorValues
    {
        public double? pilotDoorPosition { get; set; }
        public double? copilotDoorPosition { get; set; }
        public double? leftCargoDoorPosition { get; set; }
        public double? rightCargoDoorPosition { get; set; }
        public double? leftGunnerDoorPosition { get; set; }
        public double? rightGunnerDoorPosition { get; set; }
        public double? doorMovementIntensity { get; set; }
    }

    internal sealed class DynamicsValues
    {
        public double? mainRotorRpmPct { get; set; }
        public double? mainRotorRpm { get; set; }
        public double? mainRotorHz { get; set; }
        public double? bladePassHz { get; set; }
        public string rotorSource { get; set; }
        public double? collectivePosition { get; set; }
        public double? collectiveMovementRate { get; set; }
        public double? engineTorqueLeftPct { get; set; }
        public double? engineTorqueRightPct { get; set; }
        public double? torqueGaugeNormalized { get; set; }
        public double? rotorLoadIntensity { get; set; }
        public double? transmissionLoadIntensity { get; set; }
        public double? engineGovernorResponseIntensity { get; set; }
        public double? rotorDroopIntensity { get; set; }
        public double? rotorSpoolIntensity { get; set; }
        public double? turbulenceIntensity { get; set; }
        public double? controlResponseIntensity { get; set; }
        public double? airframeBuffetIntensity { get; set; }
        public double? maneuverLoadIntensity { get; set; }
        public double? normalG { get; set; }
        public double? normalGDeviation { get; set; }
        public double? positiveGIntensity { get; set; }
        public double? negativeGIntensity { get; set; }
        public double? maneuverResponseIntensity { get; set; }
        public double? lateralManeuverIntensity { get; set; }
        public double? settlingWithPowerCue { get; set; }
        public double? vrsRiskIntensity { get; set; }
        public double? translationalLiftTransition { get; set; }
        public double? bladeSlapIntensity { get; set; }
        public double? hardManeuverPulse { get; set; }
        public double? airframeImpactPulse { get; set; }
        public bool rotorRunning { get; set; }
        public bool apuRunning { get; set; }
        public bool engine1StarterActive { get; set; }
        public bool engine2StarterActive { get; set; }
        public bool lowRotorRpmWarning { get; set; }
    }

    internal sealed class Vector3
    {
        public double? x { get; set; }

        public double? y { get; set; }

        public double? z { get; set; }
    }

    internal sealed class ScalarPair
    {
        public double? left { get; set; }

        public double? right { get; set; }
    }

    internal sealed class MechanicalValues
    {
        public double? gearPosition { get; set; }

        public double? flapsPosition { get; set; }

        public double? speedbrakePosition { get; set; }
    }

    internal sealed class WeaponValues
    {
        public double? selectedStation { get; set; }

        public string selectedWeaponName { get; set; }

        public double? totalPayloadCount { get; set; }

        public double? cannonShells { get; set; }

        public double? weaponFirePulse { get; set; }

        public double? gunFirePulse { get; set; }

        public double? stationGunFirePulse { get; set; }

        public double? crewGunFirePulse { get; set; }

        public double? leftCrewGunFirePulse { get; set; }

        public double? rightCrewGunFirePulse { get; set; }

        public double? payloadReleasePulse { get; set; }

        public double? gunRoundsFired { get; set; }

        public double? stationGunRoundsFired { get; set; }

        public double? crewGunRoundsFired { get; set; }

        public double? leftCrewGunRoundsFired { get; set; }

        public double? rightCrewGunRoundsFired { get; set; }

        public double? stationGunAmmoRemaining { get; set; }

        public double? leftCrewGunAmmoRemaining { get; set; }

        public double? rightCrewGunAmmoRemaining { get; set; }

        public double? payloadReleasedCount { get; set; }

        public double? lastFiredWeaponStation { get; set; }

        public string lastFiredWeaponName { get; set; }

        public long weaponEventCounter { get; set; }

        public string weaponClass { get; set; }

        public double? weaponBurstActive { get; set; }

        public double? weaponBurstDurationMs { get; set; }

        public double? weaponRateHz { get; set; }

        public double? nominalWeaponRateHz { get; set; }

        public double? weaponImpulseStrength { get; set; }

        public double? lightGunIntensity { get; set; }

        public double? heavyGunIntensity { get; set; }

        public double? rocketLaunchIntensity { get; set; }

        public double? missileLaunchIntensity { get; set; }
        public string loadoutSummary { get; set; }
    }

    internal sealed class TelemetryCapabilities
    {
        public bool acceleration { get; set; }

        public bool angularVelocity { get; set; }

        public bool airData { get; set; }

        public bool engineRpm { get; set; }

        public bool gear { get; set; }

        public bool flaps { get; set; }

        public bool speedbrake { get; set; }

        public bool shakeAmplitude { get; set; }

        public bool weaponPayload { get; set; }

        public bool weaponEvents { get; set; }

        public bool stationGuns { get; set; }

        public bool crewGuns { get; set; }

        public bool gearStruts { get; set; }

        public bool groundMotion { get; set; }

        public bool wheelRotation { get; set; }

        public bool skids { get; set; }

        public bool countermeasures { get; set; }

        public bool cockpitStates { get; set; }

        public bool rotorDynamics { get; set; }

        public bool controlPositions { get; set; }

        public bool engineTorque { get; set; }

        public bool torqueGauge { get; set; }

        public bool rotorWarning { get; set; }

        public bool powertrainEffects { get; set; }

        public bool aerodynamicEffects { get; set; }

        public bool bladeSlap { get; set; }

        public bool detailedGroundEffects { get; set; }

        public bool weaponEffects { get; set; }

        public bool engineLifecycle { get; set; }
        public bool brakes { get; set; }
        public bool doors { get; set; }
    }
}

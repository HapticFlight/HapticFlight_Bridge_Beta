using DcsExtraTelemetry.Plugin.Telemetry;
using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal sealed class HapticStateEngine
    {
        private const double ResetGapSeconds = 0.50;
        private readonly AircraftHapticModelCatalog catalog = new AircraftHapticModelCatalog();
        private readonly LiftoffEventDetector liftoff = new LiftoffEventDetector();
        private readonly GroundInteractionDetector groundInteraction = new GroundInteractionDetector();
        private readonly RealismMix realismMix = new RealismMix();
        private readonly ExponentialSmoother groundRoll = new ExponentialSmoother(0.08, 0.22);
        private readonly ExponentialSmoother braking = new ExponentialSmoother(0.08, 0.22);
        private readonly ExponentialSmoother verticalManeuver = new ExponentialSmoother(0.08, 0.22);
        private readonly ExponentialSmoother lateralManeuver = new ExponentialSmoother(0.09, 0.24);
        private readonly ExponentialSmoother turbulence = new ExponentialSmoother(0.08, 0.22);
        private readonly RateOfChangeFilter gearRate = new RateOfChangeFilter();
        private readonly RateOfChangeFilter flapRate = new RateOfChangeFilter();
        private readonly RateOfChangeFilter speedbrakeRate = new RateOfChangeFilter();
        private readonly MechanicalEndpointDetector doorEndpoint = new MechanicalEndpointDetector();
        private readonly MechanicalEndpointDetector gearEndpoint = new MechanicalEndpointDetector();
        private readonly MechanicalEndpointDetector flapEndpoint = new MechanicalEndpointDetector();
        private readonly MechanicalEndpointDetector speedbrakeEndpoint = new MechanicalEndpointDetector();
        private TouchdownEventDetector touchdown;
        private IAircraftHapticModel currentModel;
        private HapticStateSnapshot current = HapticStateSnapshot.Empty;
        private bool initialized;
        private string lastSession = string.Empty;
        private string lastAircraft = string.Empty;
        private long lastSequence;
        private double lastModelTime;
        private DateTime lastReceivedUtc;
        private DateTime frozenSinceUtc;
        private string lastCapabilityFingerprint = string.Empty;
        private string lastSourceFingerprint = string.Empty;
        private HapticResetReason lastResetReason = HapticResetReason.Initial;

        public HapticStateSnapshot Current { get { return current; } }

        public HapticStateSnapshot Update(TelemetryPacket packet, DateTime receivedAtUtc)
        {
            HapticFrameContext frame = HapticFrameContext.FromPacket(packet, receivedAtUtc);
            IAircraftHapticModel selected = catalog.Select(frame.AircraftType, frame.AircraftFamily);

            if (initialized && string.Equals(lastSession, frame.SessionId, StringComparison.Ordinal)
                && frame.Sequence <= lastSequence)
            {
                return current;
            }

            HapticResetReason reset = DetermineReset(frame, selected);
            if (reset != HapticResetReason.None)
            {
                ResetState(reset, selected);
                frozenSinceUtc = DateTime.MinValue;
            }

            if (reset == HapticResetReason.None && initialized && frame.ModelTime == lastModelTime)
            {
                if (frozenSinceUtc == DateTime.MinValue) frozenSinceUtc = receivedAtUtc;
                if ((receivedAtUtc - frozenSinceUtc).TotalMilliseconds > 250.0)
                {
                    ResetState(HapticResetReason.ModelTimeFrozen, selected);
                    current = new HapticStateBuilder().Build(frame.AircraftType, selected.Id,
                        selected.Version, catalog.Status, HapticResetReason.ModelTimeFrozen, receivedAtUtc);
                    RememberFrame(frame);
                    return current;
                }
            }
            else
            {
                frozenSinceUtc = DateTime.MinValue;
            }

            if (touchdown == null)
            {
                currentModel = selected;
                touchdown = new TouchdownEventDetector(selected.Touchdown);
            }

            double delta = initialized && reset == HapticResetReason.None ? frame.ModelTime - lastModelTime : 0.0;
            if (delta < 0.0 || delta > 0.25) delta = 0.0;
            HapticSignalState touchdownState = touchdown.Update(frame, currentModel);
            HapticSignalState liftoffState = liftoff.Update(frame, currentModel);
            HapticStateBuilder builder = new HapticStateBuilder();
            builder.Set(HapticSignalName.Touchdown, touchdownState);
            builder.Set(HapticSignalName.Liftoff, liftoffState);
            PopulateGroundSignals(builder, frame, delta);
            groundInteraction.Update(builder, frame, currentModel, delta, touchdownState);
            PopulateRotorSignals(builder, frame);
            PopulateManeuverSignals(builder, frame, delta);
            PopulateAerodynamicSignals(builder, frame, delta, touchdownState);
            PopulateMechanicalSignals(builder, frame, delta);
            PopulateWeaponSignals(builder, frame);
            PopulateLifecycleSignals(builder, frame);
            PopulateAircraftSpecificSignals(builder, frame, touchdownState);
            realismMix.Update(builder, delta);

            current = builder.Build(frame.AircraftType, currentModel.Id, currentModel.Version,
                catalog.Status, lastResetReason, receivedAtUtc);
            RememberFrame(frame);
            return current;
        }

        public void Reset(HapticResetReason reason)
        {
            ResetState(reason, currentModel);
            initialized = false;
            current = HapticStateSnapshot.Empty;
        }

        private HapticResetReason DetermineReset(HapticFrameContext frame, IAircraftHapticModel selected)
        {
            if (!initialized) return HapticResetReason.Initial;
            if (!string.Equals(lastSession, frame.SessionId, StringComparison.Ordinal)) return HapticResetReason.NewSession;
            if (!string.Equals(lastAircraft, frame.AircraftType, StringComparison.Ordinal)) return HapticResetReason.AircraftChanged;
            if (currentModel == null || !string.Equals(currentModel.Id, selected.Id, StringComparison.Ordinal))
                return HapticResetReason.ModelChanged;
            if (!string.Equals(lastCapabilityFingerprint, CapabilityFingerprint(frame), StringComparison.Ordinal))
                return HapticResetReason.CapabilityChanged;
            if (!string.Equals(lastSourceFingerprint, SourceFingerprint(frame), StringComparison.Ordinal))
                return HapticResetReason.SourceChanged;
            if (frame.ModelTime < lastModelTime) return HapticResetReason.ModelTimeReversed;
            if (frame.ModelTime - lastModelTime > ResetGapSeconds) return HapticResetReason.TimingGap;
            if (lastReceivedUtc != DateTime.MinValue
                && (frame.ReceivedAtUtc - lastReceivedUtc).TotalSeconds > 1.0)
                return HapticResetReason.StaleResume;
            return HapticResetReason.None;
        }

        private void ResetState(HapticResetReason reason, IAircraftHapticModel selected)
        {
            lastResetReason = reason;
            currentModel = selected;
            touchdown = selected == null ? null : new TouchdownEventDetector(selected.Touchdown);
            liftoff.Reset();
            groundInteraction.Reset();
            realismMix.Reset();
            groundRoll.Reset();
            braking.Reset();
            verticalManeuver.Reset();
            lateralManeuver.Reset();
            turbulence.Reset();
            gearRate.Reset();
            flapRate.Reset();
            speedbrakeRate.Reset();
            doorEndpoint.Reset();
            gearEndpoint.Reset();
            flapEndpoint.Reset();
            speedbrakeEndpoint.Reset();
        }

        private void RememberFrame(HapticFrameContext frame)
        {
            initialized = true;
            lastSession = frame.SessionId;
            lastAircraft = frame.AircraftType;
            lastSequence = frame.Sequence;
            lastModelTime = frame.ModelTime;
            lastReceivedUtc = frame.ReceivedAtUtc;
            lastCapabilityFingerprint = CapabilityFingerprint(frame);
            lastSourceFingerprint = SourceFingerprint(frame);
        }

        private static string CapabilityFingerprint(HapticFrameContext frame)
        {
            return string.Concat(frame.HasAcceleration ? "1" : "0", frame.HasAngularVelocity ? "1" : "0",
                frame.HasAirData ? "1" : "0", frame.HasEngineRpm ? "1" : "0", frame.HasGearStruts ? "1" : "0",
                frame.HasGroundMotion ? "1" : "0", frame.HasDetailedGroundEffects ? "1" : "0",
                frame.HasBrakes ? "1" : "0", frame.HasRotorDynamics ? "1" : "0",
                frame.HasControlPositions ? "1" : "0", frame.HasEngineTorque ? "1" : "0",
                frame.HasTorqueGauge ? "1" : "0", frame.HasRotorWarning ? "1" : "0",
                frame.HasPowertrainEffects ? "1" : "0", frame.HasAerodynamicEffects ? "1" : "0",
                frame.HasBladeSlap ? "1" : "0", frame.HasEngineLifecycle ? "1" : "0",
                frame.HasDoors ? "1" : "0", frame.HasWeaponEffects ? "1" : "0",
                frame.HasCountermeasures ? "1" : "0", frame.HasGear ? "1" : "0",
                frame.HasFlaps ? "1" : "0", frame.HasSpeedbrake ? "1" : "0");
        }

        private static string SourceFingerprint(HapticFrameContext frame)
        {
            return string.Concat(frame.StrutSource, "|", frame.RotorSource, "|", frame.BrakeSource);
        }

        private void PopulateContactSignals(HapticStateBuilder builder, HapticFrameContext frame)
        {
            bool supported = frame.HasGearStruts;
            HapticSourceConfidence confidence = currentModel.Confidence;
            string source = currentModel.Id + ":" + frame.StrutSource;
            builder.Set(HapticSignalName.TailContact, Continuous(supported, Pulse(frame.Ground == null ? null : frame.Ground.tailWheelContactPulse), confidence, source));
            builder.Set(HapticSignalName.LeftContact, Continuous(supported, Pulse(frame.Ground == null ? null : frame.Ground.leftWheelContactPulse), confidence, source));
            builder.Set(HapticSignalName.RightContact, Continuous(supported, Pulse(frame.Ground == null ? null : frame.Ground.rightWheelContactPulse), confidence, source));
            bool skids = currentModel.GroundContactKind == GroundContactKind.FourPointSkids;
            builder.Set(HapticSignalName.SkidFrontLeft, Continuous(skids, Pulse(frame.Ground == null ? null : frame.Ground.skidFrontLeftContactPulse), confidence, source));
            builder.Set(HapticSignalName.SkidFrontRight, Continuous(skids, Pulse(frame.Ground == null ? null : frame.Ground.skidFrontRightContactPulse), confidence, source));
            builder.Set(HapticSignalName.SkidRearLeft, Continuous(skids, Pulse(frame.Ground == null ? null : frame.Ground.skidRearLeftContactPulse), confidence, source));
            builder.Set(HapticSignalName.SkidRearRight, Continuous(skids, Pulse(frame.Ground == null ? null : frame.Ground.skidRearRightContactPulse), confidence, source));
        }

        private void PopulateGroundSignals(HapticStateBuilder builder, HapticFrameContext frame, double delta)
        {
            GroundValues ground = frame.Ground;
            bool detailed = frame.HasDetailedGroundEffects && ground != null
                && currentModel.GroundContactKind != GroundContactKind.Unsupported;
            HapticSourceConfidence confidence = currentModel.Confidence;
            string source = currentModel.Id + ":ground";
            bool wheels = currentModel.GroundContactKind == GroundContactKind.ThreePointWheels;
            bool recordedRoll = detailed && ground.groundRollIntensity.HasValue;
            double rollTarget = frame.OnGround && wheels
                ? (recordedRoll ? P(ground.groundRollIntensity)
                    : 0.18 * HapticMath.SmoothStep(0.15, 20.0, frame.GroundSpeed))
                : 0.0;
            double rolling = groundRoll.Update(rollTarget, delta);
            builder.Set(HapticSignalName.GroundRoll, Continuous(frame.HasGroundMotion && wheels,
                frame.OnGround ? rolling : 0,
                recordedRoll ? confidence : HapticSourceConfidence.Proxy,
                source + (recordedRoll ? ":derived-roll-texture" : ":speed-texture")));
            builder.Set(HapticSignalName.GroundResonance, Continuous(detailed, P(ground == null ? null : ground.groundResonanceIntensity), confidence, source));
            builder.Set(HapticSignalName.WeightOnGround, Continuous(detailed, P(ground == null ? null : ground.weightOnGroundIntensity), confidence, source));
            bool skidModel = currentModel.GroundContactKind == GroundContactKind.FourPointSkids
                || currentModel.GroundContactKind == GroundContactKind.WeightOnSkids;
            bool slipAvailable = ground != null && GroundInteractionDetector.Finite(ground.longitudinalGroundSpeedMps)
                && GroundInteractionDetector.Finite(ground.lateralGroundSlipMps);
            double slip = slipAvailable ? Math.Sqrt(Math.Pow(ground.longitudinalGroundSpeedMps.Value, 2)
                + Math.Pow(ground.lateralGroundSlipMps.Value, 2)) : 0;
            builder.Set(HapticSignalName.SkidScrub, Continuous(detailed && skidModel && slipAvailable,
                frame.OnGround ? HapticMath.SmoothStep(0.08, 4.0, slip) : 0, HapticSourceConfidence.Proxy, source + ":body-axis-slide"));
            builder.Set(HapticSignalName.WheelShimmy, Continuous(detailed && currentModel.GroundContactKind == GroundContactKind.ThreePointWheels,
                P(ground == null ? null : ground.wheelShimmyIntensity), confidence, source));
            builder.Set(HapticSignalName.TailwheelCaster, Continuous(detailed && currentModel.GroundContactKind == GroundContactKind.ThreePointWheels,
                P(ground == null ? null : ground.tailWheelCasterIntensity), confidence, source));
            double brakingValue = braking.Update(P(ground == null ? null : ground.brakingIntensity), delta);
            builder.Set(HapticSignalName.Braking, Continuous(frame.HasBrakes, brakingValue, confidence, currentModel.Id + ":" + frame.BrakeSource));
            builder.Set(HapticSignalName.BrakeApplication, Continuous(frame.HasBrakes,
                P(ground == null ? null : ground.wheelBrakesPosition), HapticSourceConfidence.Direct,
                currentModel.Id + ":" + frame.BrakeSource + ":pedal"));
            builder.Set(HapticSignalName.BrakeDeceleration, Continuous(frame.HasBrakes,
                P(ground == null ? null : ground.brakeDecelerationIntensity), confidence, source));
            builder.Set(HapticSignalName.WheelSkid, Continuous(detailed && wheels && frame.HasBrakes,
                P(ground == null ? null : ground.wheelSkidIntensity), HapticSourceConfidence.DerivedVerified,
                source + ":derived-lateral-wheel-skid"));
        }

        private void PopulateRotorSignals(HapticStateBuilder builder, HapticFrameContext frame)
        {
            DynamicsValues dynamics = frame.Dynamics;
            bool rotor = frame.HasRotorDynamics && dynamics != null;
            bool power = frame.HasPowertrainEffects && dynamics != null;
            HapticSourceConfidence rotorConfidence = RotorConfidence(frame.RotorSource, currentModel.Confidence);
            string source = currentModel.Id + ":" + frame.RotorSource;
            builder.Set(HapticSignalName.RotorPresence, Continuous(rotor, frame.RotorRpmPct / 100.0, rotorConfidence, source));
            builder.Set(HapticSignalName.RotorLoad, Continuous(power, P(dynamics == null ? null : dynamics.rotorLoadIntensity), rotorConfidence, source));
            builder.Set(HapticSignalName.TransmissionLoad, Continuous(power, P(dynamics == null ? null : dynamics.transmissionLoadIntensity), rotorConfidence, source));
            builder.Set(HapticSignalName.RotorSpool, Continuous(power, P(dynamics == null ? null : dynamics.rotorSpoolIntensity), rotorConfidence, source));
            builder.Set(HapticSignalName.RotorDroop, Continuous(power, P(dynamics == null ? null : dynamics.rotorDroopIntensity), rotorConfidence, source));
            builder.Set(HapticSignalName.GovernorResponse, Continuous(power, P(dynamics == null ? null : dynamics.engineGovernorResponseIntensity), rotorConfidence, source));
        }

        private void PopulateManeuverSignals(HapticStateBuilder builder, HapticFrameContext frame, double delta)
        {
            DynamicsValues dynamics = frame.Dynamics;
            bool supported = frame.HasAerodynamicEffects && dynamics != null && !frame.OnGround;
            bool suppressed = frame.WeaponActive;
            string source = currentModel.Id + ":flight-motion";
            HapticSourceConfidence confidence = HapticSourceConfidence.DerivedVerified;
            builder.Set(HapticSignalName.VerticalManeuver, Continuous(supported,
                verticalManeuver.Update(suppressed ? 0.0 : P(dynamics == null ? null : dynamics.maneuverResponseIntensity), delta),
                confidence, source));
            builder.Set(HapticSignalName.PositiveLoad, Continuous(supported,
                suppressed ? 0.0 : P(dynamics == null ? null : dynamics.positiveGIntensity), confidence, source));
            builder.Set(HapticSignalName.NegativeLoad, Continuous(supported,
                suppressed ? 0.0 : P(dynamics == null ? null : dynamics.negativeGIntensity), confidence, source));
            builder.Set(HapticSignalName.LateralManeuver, Continuous(supported,
                lateralManeuver.Update(suppressed ? 0.0 : P(dynamics == null ? null : dynamics.lateralManeuverIntensity), delta),
                confidence, source));
            builder.Set(HapticSignalName.ControlResponse, Continuous(supported,
                suppressed ? 0.0 : P(dynamics == null ? null : dynamics.controlResponseIntensity), confidence, source));
            builder.Set(HapticSignalName.HardManeuver, Continuous(supported,
                suppressed ? 0.0 : P(dynamics == null ? null : dynamics.hardManeuverPulse), confidence, source));
        }

        private void PopulateAerodynamicSignals(HapticStateBuilder builder, HapticFrameContext frame, double delta, HapticSignalState touchdownState)
        {
            DynamicsValues dynamics = frame.Dynamics;
            bool supported = frame.HasAerodynamicEffects && dynamics != null && !frame.OnGround;
            bool recoil = frame.WeaponActive || frame.LightGun > 0.01 || frame.HeavyGun > 0.01;
            string source = currentModel.Id + ":aerodynamic";
            HapticSourceConfidence confidence = HapticSourceConfidence.DerivedVerified;
            builder.Set(HapticSignalName.Turbulence, Continuous(supported,
                turbulence.Update(recoil ? 0.0 : P(dynamics == null ? null : dynamics.turbulenceIntensity), delta), confidence, source));
            builder.Set(HapticSignalName.Buffet, Continuous(supported,
                recoil ? 0.0 : P(dynamics == null ? null : dynamics.airframeBuffetIntensity), confidence, source));
            builder.Set(HapticSignalName.TranslationalLift, Continuous(supported,
                P(dynamics == null ? null : dynamics.translationalLiftTransition), confidence, source));
            builder.Set(HapticSignalName.SettlingWithPower, Continuous(supported,
                P(dynamics == null ? null : dynamics.vrsRiskIntensity), confidence, source + ":derived-vrs-risk"));
            builder.Set(HapticSignalName.BladeSlap, Continuous(supported && frame.HasBladeSlap,
                P(dynamics == null ? null : dynamics.bladeSlapIntensity), RotorConfidence(frame.RotorSource, confidence), source));
            double shock = recoil || touchdownState.Active || frame.OnGround ? 0.0
                : P(dynamics == null ? null : dynamics.airframeImpactPulse);
            builder.Set(HapticSignalName.AirframeShock, Continuous(supported, shock, confidence, source));
        }

        private void PopulateMechanicalSignals(HapticStateBuilder builder, HapticFrameContext frame, double delta)
        {
            builder.Set(HapticSignalName.DoorMovement, Continuous(frame.HasDoors, frame.DoorMovement,
                HapticSourceConfidence.DerivedVerified, currentModel.Id + ":doors"));
            builder.Set(HapticSignalName.DoorEndpoint, doorEndpoint.Update(frame.HasDoors,
                frame.DoorPosition, frame.ModelTime, HapticSourceConfidence.Direct, currentModel.Id + ":door-endpoint"));
            builder.Set(HapticSignalName.GearMovement, Continuous(frame.HasGear,
                HapticMath.Clamp01(Math.Abs(gearRate.Update(frame.GearPosition, delta)) / 0.5), HapticSourceConfidence.Direct, currentModel.Id + ":gear"));
            builder.Set(HapticSignalName.GearEndpoint, gearEndpoint.Update(frame.HasGear,
                frame.GearPosition, frame.ModelTime, HapticSourceConfidence.Direct, currentModel.Id + ":gear-endpoint"));
            builder.Set(HapticSignalName.FlapsMovement, Continuous(frame.HasFlaps,
                HapticMath.Clamp01(Math.Abs(flapRate.Update(frame.FlapsPosition, delta)) / 0.5), HapticSourceConfidence.Direct, currentModel.Id + ":flaps"));
            builder.Set(HapticSignalName.FlapsEndpoint, flapEndpoint.Update(frame.HasFlaps,
                frame.FlapsPosition, frame.ModelTime, HapticSourceConfidence.Direct, currentModel.Id + ":flaps-endpoint"));
            builder.Set(HapticSignalName.SpeedbrakeMovement, Continuous(frame.HasSpeedbrake,
                HapticMath.Clamp01(Math.Abs(speedbrakeRate.Update(frame.SpeedbrakePosition, delta)) / 0.5), HapticSourceConfidence.Direct, currentModel.Id + ":speedbrake"));
            builder.Set(HapticSignalName.SpeedbrakeEndpoint, speedbrakeEndpoint.Update(frame.HasSpeedbrake,
                frame.SpeedbrakePosition, frame.ModelTime, HapticSourceConfidence.Direct, currentModel.Id + ":speedbrake-endpoint"));
        }

        private void PopulateWeaponSignals(HapticStateBuilder builder, HapticFrameContext frame)
        {
            bool supported = frame.HasWeaponEffects;
            string source = currentModel.Id + ":payload-events";
            HapticSourceConfidence confidence = HapticSourceConfidence.DerivedVerified;
            builder.Set(HapticSignalName.LightGun, Continuous(supported, frame.LightGun, confidence, source));
            builder.Set(HapticSignalName.HeavyGun, Continuous(supported, frame.HeavyGun, confidence, source));
            builder.Set(HapticSignalName.RocketLaunch, Continuous(supported, frame.Rocket, confidence, source));
            builder.Set(HapticSignalName.MissileLaunch, Continuous(supported, frame.Missile, confidence, source));
            builder.Set(HapticSignalName.WeaponRecoil, Continuous(supported,
                Math.Max(frame.LightGun, frame.HeavyGun), confidence, source));
            builder.Set(HapticSignalName.WeaponBurst, Continuous(supported,
                frame.WeaponActive ? Math.Max(frame.WeaponImpulse, Math.Max(frame.LightGun, frame.HeavyGun)) : 0.0,
                confidence, source));
            builder.Set(HapticSignalName.ChaffRelease, Continuous(frame.HasCountermeasures, frame.ChaffPulse, HapticSourceConfidence.Direct, currentModel.Id + ":countermeasures"));
            builder.Set(HapticSignalName.FlareRelease, Continuous(frame.HasCountermeasures, frame.FlarePulse, HapticSourceConfidence.Direct, currentModel.Id + ":countermeasures"));
        }

        private void PopulateLifecycleSignals(HapticStateBuilder builder, HapticFrameContext frame)
        {
            builder.Set(HapticSignalName.RotorRunning, Continuous(frame.HasRotorDynamics, frame.RotorRunning ? 1.0 : 0.0, RotorConfidence(frame.RotorSource, HapticSourceConfidence.DerivedVerified), frame.RotorSource));
            bool uh60 = string.Equals(currentModel.Family, "UH60", StringComparison.Ordinal);
            bool uh1 = string.Equals(currentModel.Family, "UH1", StringComparison.Ordinal);
            builder.Set(HapticSignalName.ApuRunning, Continuous(frame.HasEngineLifecycle && uh60, frame.ApuRunning ? 1.0 : 0.0, HapticSourceConfidence.Direct, currentModel.Id + ":apu"));
            builder.Set(HapticSignalName.Starter1, Continuous(frame.HasEngineLifecycle && uh60, frame.Starter1 ? 1.0 : 0.0, HapticSourceConfidence.Direct, currentModel.Id + ":starter1"));
            builder.Set(HapticSignalName.Starter2, Continuous(frame.HasEngineLifecycle && uh60, frame.Starter2 ? 1.0 : 0.0, HapticSourceConfidence.Direct, currentModel.Id + ":starter2"));
            builder.Set(HapticSignalName.LowRotorWarning, Continuous(frame.HasRotorWarning && (uh60 || uh1), frame.LowRotorWarning ? 1.0 : 0.0, HapticSourceConfidence.Direct, currentModel.Id + ":rotor-warning"));
        }

        private void PopulateAircraftSpecificSignals(HapticStateBuilder builder, HapticFrameContext frame, HapticSignalState touchdownState)
        {
            GroundValues ground = frame.Ground;
            DynamicsValues dynamics = frame.Dynamics;
            string family = currentModel.Family;
            if (family == "UH60")
            {
                double tailPulse = Pulse(ground == null ? null : ground.tailWheelContactPulse);
                bool mains = frame.LeftContact || frame.RightContact;
                double mainLoading = mains ? HapticMath.Clamp01(Math.Max(frame.LeftCompression, frame.RightCompression)) : 0.0;
                builder.Set(HapticSignalName.Uh60TailwheelSettle, Continuous(true, touchdownState.Active ? 0.0 : tailPulse, HapticSourceConfidence.DerivedVerified, "UH60-v1:tailwheel"));
                builder.Set(HapticSignalName.Uh60MainGearLoading, Continuous(true, mainLoading, HapticSourceConfidence.DerivedVerified, "UH60-v1:main-gear"));
                builder.Set(HapticSignalName.Uh60WheelShimmy, Continuous(frame.HasDetailedGroundEffects, P(ground == null ? null : ground.wheelShimmyIntensity), HapticSourceConfidence.DerivedVerified, "UH60-v1:wheel-shimmy"));
                builder.Set(HapticSignalName.Uh60TailwheelCaster, Continuous(frame.HasDetailedGroundEffects, P(ground == null ? null : ground.tailWheelCasterIntensity), HapticSourceConfidence.DerivedVerified, "UH60-v1:tail-caster"));
                builder.Set(HapticSignalName.Uh60BrakeTailRecovery, Continuous(frame.HasBrakes, mains && frame.BrakePosition <= 0.25 ? tailPulse : 0.0, HapticSourceConfidence.DerivedVerified, "UH60-v1:brake-tail"));
                builder.Set(HapticSignalName.Uh60LeftDoorGun, Continuous(frame.HasWeaponEffects, frame.LeftCrewGunPulse, HapticSourceConfidence.Direct, "UH60-v1:left-door-gun"));
                builder.Set(HapticSignalName.Uh60RightDoorGun, Continuous(frame.HasWeaponEffects, frame.RightCrewGunPulse, HapticSourceConfidence.Direct, "UH60-v1:right-door-gun"));
                builder.Set(HapticSignalName.Uh60DapCannon, Continuous(frame.HasWeaponEffects && frame.AircraftType == "UH-60L_DAP", frame.HeavyGun, HapticSourceConfidence.DerivedVerified, "UH60-v1:dap-cannon"));
            }
            else if (family == "AH6" || family == "OH58")
            {
                double front = P(ground == null ? null : ground.skidFrontLeftCompression) + P(ground == null ? null : ground.skidFrontRightCompression);
                double rear = P(ground == null ? null : ground.skidRearLeftCompression) + P(ground == null ? null : ground.skidRearRightCompression);
                double left = P(ground == null ? null : ground.skidFrontLeftCompression) + P(ground == null ? null : ground.skidRearLeftCompression);
                double right = P(ground == null ? null : ground.skidFrontRightCompression) + P(ground == null ? null : ground.skidRearRightCompression);
                HapticSignalName foreAft = family == "AH6" ? HapticSignalName.Ah6SkidForeAftLoad : HapticSignalName.Oh58SkidForeAftLoad;
                HapticSignalName lateral = family == "AH6" ? HapticSignalName.Ah6SkidLateralLoad : HapticSignalName.Oh58SkidLateralLoad;
                builder.Set(foreAft, Continuous(frame.HasGearStruts, Math.Abs(front - rear) / 2.0, currentModel.Confidence, currentModel.Id + ":skid-fore-aft"));
                builder.Set(lateral, Continuous(frame.HasGearStruts, Math.Abs(left - right) / 2.0, currentModel.Confidence, currentModel.Id + ":skid-lateral"));
                if (family == "AH6") builder.Set(HapticSignalName.Ah6LightLanding, Scale(touchdownState, 0.72, "AH6-v1:light-landing"));
            }
            else if (family == "AH64")
            {
                double sequence = frame.TailContact && (frame.LeftContact || frame.RightContact) ? touchdownState.Envelope : 0.0;
                builder.Set(HapticSignalName.Ah64TailMainSequence, Continuous(true, sequence, currentModel.Confidence, "AH64-v1:tail-main"));
                builder.Set(HapticSignalName.Ah64HeavyLanding, Scale(touchdownState, 1.0, "AH64-v1:heavy-landing"));
                bool m230 = Contains(frame.LastFiredWeaponName, "m230");
                builder.Set(HapticSignalName.Ah64M230Recoil, Continuous(frame.HasWeaponEffects, m230 ? frame.HeavyGun : 0.0, HapticSourceConfidence.DerivedVerified, "AH64-v1:m230"));
                builder.Set(HapticSignalName.Ah64HydraLaunch, Continuous(frame.HasWeaponEffects, Contains(frame.LastFiredWeaponName, "hydra") ? frame.Rocket : 0.0, HapticSourceConfidence.DerivedVerified, "AH64-v1:hydra"));
                builder.Set(HapticSignalName.Ah64HellfireLaunch, Continuous(frame.HasWeaponEffects, Contains(frame.LastFiredWeaponName, "hellfire") ? frame.Missile : 0.0, HapticSourceConfidence.DerivedVerified, "AH64-v1:hellfire"));
            }
            else if (family == "UH1")
            {
                builder.Set(HapticSignalName.Uh1WeightOnSkidsLanding, Scale(touchdownState, 0.86, "UH1-v1:weight-on-skids"));
                builder.Set(HapticSignalName.Uh1TwoBladeResponse, Continuous(frame.HasRotorDynamics, P(dynamics == null ? null : dynamics.rotorLoadIntensity), currentModel.Confidence, "UH1-v1:two-blade"));
                builder.Set(HapticSignalName.Uh1TorqueLoad, Continuous(frame.HasTorqueGauge && dynamics != null && dynamics.torqueGaugeNormalized.HasValue, P(dynamics == null ? null : dynamics.torqueGaugeNormalized), HapticSourceConfidence.Direct, "UH1-v1:torque-gauge"));
            }
        }

        private static HapticSignalState Scale(HapticSignalState source, double scale, string name)
        {
            return source.Supported
                ? new HapticSignalState(true, source.Active, source.Intensity * scale, source.Envelope * scale,
                    source.Phase, source.EventId, source.Confidence, name)
                : HapticSignalState.Unsupported;
        }

        private static HapticSignalState Continuous(bool supported, double value, HapticSourceConfidence confidence, string source)
        {
            return HapticSignalState.Continuous(supported, value, confidence, source);
        }

        private static double P(double? value) { return value.HasValue ? HapticMath.Clamp01(value.Value) : 0.0; }
        private static double Pulse(double? value) { return value.HasValue && value.Value > 0.5 ? 1.0 : 0.0; }
        private static bool Contains(string value, string token) { return (value ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static HapticSourceConfidence RotorConfidence(string source, HapticSourceConfidence fallback)
        {
            return Contains(source, "Proxy") ? HapticSourceConfidence.Proxy : fallback;
        }

    }
}

using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal enum TouchdownDetectorState
    {
        Uninitialized,
        Grounded,
        AirborneUnarmed,
        AirborneArmed,
        ContactCandidate,
        TouchdownActive,
        Cooldown
    }

    internal interface IHapticDetector
    {
        void Reset();
        HapticSignalState Update(HapticFrameContext frame, IAircraftHapticModel model);
    }

    internal sealed class TouchdownEventDetector : IHapticDetector
    {
        private readonly RollingPeakFilter sinkHistory = new RollingPeakFilter(64);
        private AttackHoldReleaseEnvelope envelope;
        private TouchdownParameters parameters;
        private TouchdownDetectorState state;
        private bool initialized;
        private bool previousOnGround;
        private double airborneSince;
        private double candidateSince;
        private double cooldownUntil;
        private double eventStrength;
        private double candidateSink;
        private double candidateLoading;
        private long eventId;

        public TouchdownEventDetector(TouchdownParameters parameters)
        {
            Configure(parameters);
            Reset();
        }

        public TouchdownDetectorState State { get { return state; } }

        public void Configure(TouchdownParameters value)
        {
            if (value == null) throw new ArgumentNullException("value");
            value.Validate();
            parameters = value;
            envelope = new AttackHoldReleaseEnvelope(value.AttackSeconds, value.HoldSeconds, value.ReleaseSeconds);
        }

        public HapticSignalState Update(HapticFrameContext frame, IAircraftHapticModel model)
        {
            bool supported = frame != null && model != null && frame.HasGearStruts
                && frame.HasAirData && frame.Ground != null
                && model.GroundContactKind != GroundContactKind.Unsupported;
            if (!supported)
            {
                Reset();
                return HapticSignalState.Unsupported;
            }

            double now = frame.ModelTime;
            // Only airborne velocity belongs to the pre-impact history. Preserve
            // it at first contact so a delayed one-wheel confirmation cannot
            // expire the landing evidence or include post-impact motion.
            if (!frame.OnGround) sinkHistory.Add(now, Math.Max(0.0, -frame.VerticalVelocity));

            if (!initialized)
            {
                initialized = true;
                previousOnGround = frame.OnGround;
                if (frame.OnGround)
                {
                    state = TouchdownDetectorState.Grounded;
                }
                else
                {
                    airborneSince = now;
                    state = TouchdownDetectorState.AirborneUnarmed;
                }
                return Signal(frame, model, now);
            }

            if (!frame.OnGround)
            {
                if (previousOnGround)
                {
                    airborneSince = now;
                    state = TouchdownDetectorState.AirborneUnarmed;
                    sinkHistory.Reset();
                    sinkHistory.Add(now, Math.Max(0.0, -frame.VerticalVelocity));
                }

                double requiredAirborne = eventId > 0L
                    ? parameters.BounceRearmSeconds
                    : parameters.AirborneArmSeconds;
                if (now - airborneSince >= requiredAirborne)
                    state = TouchdownDetectorState.AirborneArmed;
            }
            else if (!previousOnGround)
            {
                if (state == TouchdownDetectorState.AirborneArmed)
                {
                    candidateSince = now;
                    candidateSink = sinkHistory.PeakSince(now - parameters.PreImpactHistorySeconds);
                    candidateLoading = 0.0;
                    state = TouchdownDetectorState.ContactCandidate;
                }
                else
                {
                    state = TouchdownDetectorState.Grounded;
                }
            }

            if (frame.OnGround && state == TouchdownDetectorState.ContactCandidate)
            {
                if (GroundInteractionDetector.Compression(frame, model) != null)
                    candidateLoading = Math.Max(candidateLoading, Math.Max(0, frame.StrutCompressionRate));
                bool confirmed = HasConfirmedContact(frame, model.GroundContactKind);
                bool persistentFallback = AllowsPersistentSingleContact(frame, model.GroundContactKind)
                    && now - candidateSince >= parameters.PersistentSingleContactSeconds;
                // The Hook's touchdown pulse is direct evidence of an
                // airborne-to-ground transition.  Use it only to confirm a
                // tail-first three-wheel landing when a pre-impact sink was
                // captured.  This preserves the normal two-main rule and
                // prevents stationary tail contact from becoming a body hit.
                bool hookConfirmedTailLanding = HasHookConfirmedTailLanding(frame, model, candidateSink);
                if (((confirmed || hookConfirmedTailLanding) && now - candidateSince >= parameters.ContactConfirmationSeconds)
                    || persistentFallback)
                {
                    Trigger(now);
                }
            }

            double envelopeValue = envelope.Value(now);
            if (state == TouchdownDetectorState.TouchdownActive
                && envelopeValue <= 0.0
                && envelope.Phase(now) == HapticSignalPhase.Idle)
            {
                eventStrength = 0.0;
                state = now < cooldownUntil ? TouchdownDetectorState.Cooldown : TouchdownDetectorState.Grounded;
            }
            else if (state == TouchdownDetectorState.Cooldown && now >= cooldownUntil)
            {
                state = frame.OnGround ? TouchdownDetectorState.Grounded : TouchdownDetectorState.AirborneUnarmed;
                if (!frame.OnGround) airborneSince = now;
            }

            previousOnGround = frame.OnGround;
            return Signal(frame, model, now);
        }

        public void Reset()
        {
            initialized = false;
            previousOnGround = false;
            airborneSince = 0.0;
            candidateSince = 0.0;
            cooldownUntil = 0.0;
            eventStrength = 0.0;
            candidateSink = 0.0;
            candidateLoading = 0.0;
            eventId = 0L;
            sinkHistory.Reset();
            state = TouchdownDetectorState.Uninitialized;
            if (envelope != null) envelope.Reset();
        }

        private void Trigger(double now)
        {
            double peakSink = candidateSink;
            eventStrength = HapticMath.SmoothStep(
                parameters.MinimumSinkRateMps,
                parameters.FullScaleSinkRateMps,
                peakSink);
            // Compression can reinforce a confirmed impact, but cannot turn
            // stationary loading into a full-strength landing by itself.
            eventStrength = HapticMath.Clamp01(eventStrength + (1 - eventStrength)
                * 0.20 * HapticMath.SmoothStep(0.15, 2.0, candidateLoading));
            eventId++;
            envelope.Trigger(now);
            cooldownUntil = now + parameters.RetriggerCooldownSeconds;
            state = TouchdownDetectorState.TouchdownActive;
        }

        private HapticSignalState Signal(HapticFrameContext frame, IAircraftHapticModel model, double now)
        {
            double envelopeValue = envelope.Value(now);
            HapticSignalPhase phase = envelope.Phase(now);
            bool active = envelopeValue > 0.0;
            return new HapticSignalState(
                true,
                active,
                eventStrength,
                envelopeValue * eventStrength,
                phase,
                eventId,
                model.Confidence,
                model.Id + ":" + frame.StrutSource);
        }

        private static bool HasConfirmedContact(HapticFrameContext frame, GroundContactKind kind)
        {
            if (kind == GroundContactKind.FourPointSkids)
            {
                int count = (frame.SkidFrontLeft ? 1 : 0) + (frame.SkidFrontRight ? 1 : 0)
                    + (frame.SkidRearLeft ? 1 : 0) + (frame.SkidRearRight ? 1 : 0);
                bool bilateral = (frame.SkidFrontLeft || frame.SkidRearLeft)
                    && (frame.SkidFrontRight || frame.SkidRearRight);
                return count >= 3 || (count >= 2 && bilateral);
            }
            // A centered body touchdown is confirmed promptly when both main
            // gear carry the aircraft.  One main wheel is deliberately left
            // to the persistent-contact path so a brief roof/obstacle strike
            // remains an individual wheel cue rather than a full landing.
            if (kind == GroundContactKind.ThreePointWheels) return frame.LeftContact && frame.RightContact;
            if (kind == GroundContactKind.WeightOnSkids) return frame.OnGround;
            return frame.OnGround;
        }

        private static bool AllowsPersistentSingleContact(HapticFrameContext frame, GroundContactKind kind)
        {
            // Tailwheel-only contact is a settle/sequence cue, not sufficient
            // support for a whole-aircraft touchdown.  A persistent main-wheel
            // contact can still confirm an intentional one-wheel landing.
            return (kind == GroundContactKind.ThreePointWheels
                    && (frame.LeftContact || frame.RightContact))
                || kind == GroundContactKind.WeightOnSkids
                || kind == GroundContactKind.VerifiedGeneric;
        }

        private bool HasHookConfirmedTailLanding(HapticFrameContext frame, IAircraftHapticModel model, double preImpactSink)
        {
            if (model.GroundContactKind != GroundContactKind.ThreePointWheels
                || !frame.TailContact
                || frame.RawTouchdownPulse < 0.5
                || frame.RawTouchdownStrength <= 0.0)
                return false;

            // Require independently captured descent evidence.  The Hook's
            // pulse alone can arise from a contact animation edge; it must not
            // promote that edge to a whole-airframe impact.
            return preImpactSink >= parameters.MinimumSinkRateMps;
        }

    }

    internal sealed class LiftoffEventDetector : IHapticDetector
    {
        private readonly AttackHoldReleaseEnvelope envelope = new AttackHoldReleaseEnvelope(0.015, 0.035, 0.180);
        private bool initialized;
        private bool priorGrounded;
        private double candidateSince;
        private bool candidate;
        private long eventId;

        public HapticSignalState Update(HapticFrameContext frame, IAircraftHapticModel model)
        {
            bool supported = frame != null && model != null && frame.HasGearStruts;
            if (!supported)
            {
                Reset();
                return HapticSignalState.Unsupported;
            }
            if (!initialized)
            {
                initialized = true;
                priorGrounded = frame.OnGround;
                return State(frame.ModelTime, model, frame.StrutSource);
            }
            if (priorGrounded && !frame.OnGround)
            {
                candidate = true;
                candidateSince = frame.ModelTime;
            }
            if (candidate && !frame.OnGround && frame.ModelTime - candidateSince >= 0.05)
            {
                eventId++;
                envelope.Trigger(frame.ModelTime);
                candidate = false;
            }
            if (frame.OnGround) candidate = false;
            priorGrounded = frame.OnGround;
            return State(frame.ModelTime, model, frame.StrutSource);
        }

        public void Reset()
        {
            initialized = false;
            priorGrounded = false;
            candidate = false;
            candidateSince = 0.0;
            eventId = 0L;
            envelope.Reset();
        }

        private HapticSignalState State(double now, IAircraftHapticModel model, string source)
        {
            double value = envelope.Value(now);
            double intensity = value > 0.0 ? 0.35 : 0.0;
            return new HapticSignalState(true, value > 0.0, intensity, value * 0.35,
                envelope.Phase(now), eventId, model.Confidence, model.Id + ":" + source);
        }
    }
}

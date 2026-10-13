using System;
using DcsExtraTelemetry.Plugin.Telemetry;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    // Rates are normalized animation units/second, not physical force. The
    // thresholds below are provisional tuning, isolated from the raw contract.
    internal sealed class GroundInteractionDetector
    {
        private readonly ExponentialSmoother roughness = new ExponentialSmoother(0.045, 0.16);
        private readonly ExponentialSmoother pitch = new ExponentialSmoother(0.06, 0.18);
        private readonly ExponentialSmoother roll = new ExponentialSmoother(0.06, 0.18);
        private readonly AttackHoldReleaseEnvelope bump = new AttackHoldReleaseEnvelope(0.008, 0.025, 0.17);
        private readonly AttackHoldReleaseEnvelope settle = new AttackHoldReleaseEnvelope(0.025, 0.025, 0.32);
        private readonly AttackHoldReleaseEnvelope[] contacts = new AttackHoldReleaseEnvelope[7];
        private readonly double[] contactStrength = new double[7];
        private readonly long[] contactIds = new long[7];
        private readonly bool[] previousContact = new bool[7];
        private readonly bool[] previousDirectContactPulse = new bool[7];
        private readonly double[] contactUntil = new double[7];
        private double[] previousCompression;
        private bool initialized, wasGrounded, compressionInitialized, bumpArmed = true, previousTailSettlePulse;
        private double groundSince, bumpUntil, bumpStrength, settleStrength, noise;
        private long bumpId, touchdownId, settleId;

        public GroundInteractionDetector()
        {
            for (int i = 0; i < contacts.Length; i++)
                contacts[i] = new AttackHoldReleaseEnvelope(0.008, 0.015, 0.11);
        }

        public static bool Finite(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        }

        public static double[] Compression(HapticFrameContext f, IAircraftHapticModel model)
        {
            GroundValues g = f.Ground;
            if (!f.HasGearStruts || g == null) return null;
            double?[] values;
            if (model.GroundContactKind == GroundContactKind.FourPointSkids)
                values = new[] { g.skidFrontLeftCompression, g.skidFrontRightCompression, g.skidRearLeftCompression, g.skidRearRightCompression };
            else if (model.GroundContactKind == GroundContactKind.ThreePointWheels)
                values = new[] { g.tailStrutCompression, g.leftStrutCompression, g.rightStrutCompression };
            else return null; // Weight-on-skids is not suspension travel.
            double[] result = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (!Finite(values[i])) return null;
                result[i] = values[i].Value;
            }
            return result;
        }

        public void Update(HapticStateBuilder builder, HapticFrameContext f, IAircraftHapticModel model,
            double dt, HapticSignalState touchdown)
        {
            bool supported = f.HasGroundMotion && f.HasGearStruts && f.Ground != null
                && model.GroundContactKind != GroundContactKind.Unsupported;
            if (!supported) { Reset(); return; }
            double now = f.ModelTime;
            if (!initialized || (f.OnGround && !wasGrounded)) groundSince = now;
            double[] compression = Compression(f, model);
            double movement = 0, loading = 0, pitchRate = 0, rollRate = 0;
            bool rates = compression != null && compressionInitialized && initialized && dt > 0 && dt <= 0.25;
            double[] rate = compression == null ? null : new double[compression.Length];
            if (rates)
            {
                for (int i = 0; i < compression.Length; i++)
                {
                    rate[i] = (compression[i] - previousCompression[i]) / dt;
                    if (Math.Abs(rate[i]) < 0.015) rate[i] = 0;
                    movement += Math.Abs(rate[i]) / compression.Length;
                    loading = Math.Max(loading, rate[i]);
                }
                pitchRate = rate.Length == 3 ? rate[0] - (rate[1] + rate[2]) / 2
                    : (rate[0] + rate[1] - rate[2] - rate[3]) / 2;
                rollRate = rate.Length == 3 ? rate[1] - rate[2]
                    : (rate[0] + rate[2] - rate[1] - rate[3]) / 2;
            }
            if (UpdateContacts(builder, f, model, rate, dt)) groundSince = now;
            if (touchdown.EventId != touchdownId)
            {
                touchdownId = touchdown.EventId;
                settleStrength = touchdown.Intensity * 0.35;
                settle.Trigger(now); settleId++;
            }
            // Contact changes and landing compression belong to landing/contact
            // cues. Bumps require established support, never just an edge in WoW.
            double threshold = Math.Max(model.GroundContactKind == GroundContactKind.FourPointSkids ? 0.45 : 0.30, noise * 4);
            if (loading < threshold * 0.45) bumpArmed = true;
            if (rates && f.OnGround && wasGrounded && now - groundSince >= 0.25
                && !touchdown.Active && !f.WeaponActive && bumpArmed && now >= bumpUntil && loading > threshold)
            {
                bumpStrength = HapticMath.SmoothStep(threshold, threshold + 2.0, loading);
                bump.Trigger(now); bumpId++; bumpUntil = now + 0.18; bumpArmed = false;
                settleStrength = bumpStrength * 0.3; settle.Trigger(now); settleId++;
            }
            if (dt > 0 && loading < threshold && f.OnGround)
                noise += (loading - noise) * (1 - Math.Exp(-dt / 0.8));
            if (!f.OnGround) { bump.Reset(); settle.Reset(); roughness.Reset(); pitch.Reset(); roll.Reset(); }
            double impact = Math.Max(touchdown.Envelope, bumpStrength * bump.Value(now));
            double duck = 1 - 0.75 * impact;
            string source = model.Id + ":compression-rate-provisional";
            var confidence = HapticSourceConfidence.Provisional;
            bool travel = compression != null;
            builder.Set(HapticSignalName.GroundBump, new HapticSignalState(travel, bump.Value(now) > 0,
                bump.Value(now) > 0 ? bumpStrength : 0, bumpStrength * bump.Value(now), bump.Phase(now), bumpId, confidence, source));
            double settling = f.OnGround ? Math.Max(settleStrength * settle.Value(now),
                settle.Value(now) * HapticMath.Clamp01(movement) * 0.3) : 0;
            builder.Set(HapticSignalName.GroundSettling, new HapticSignalState(true, settling > 0,
                settling, settling, settle.Phase(now), settleId, confidence,
                model.Id + (travel ? ":impact-and-travel-settling" : ":sink-based-settling-proxy")));
            double texture = roughness.Update(f.OnGround && f.GroundSpeed > 0.15 && rates ? movement / 1.5 : 0, dt);
            builder.Set(HapticSignalName.SuspensionMovement, HapticSignalState.Continuous(travel, texture * duck, confidence, source));
            builder.Set(HapticSignalName.GroundPitchRock, HapticSignalState.Continuous(travel,
                pitch.Update(f.OnGround && rates ? Math.Abs(pitchRate) / 2 : 0, dt) * duck, confidence, source));
            builder.Set(HapticSignalName.GroundRollRock, HapticSignalState.Continuous(travel,
                roll.Update(f.OnGround && rates ? Math.Abs(rollRate) / 2 : 0, dt) * duck, confidence, source));
            previousCompression = compression; compressionInitialized = compression != null;
            initialized = true; wasGrounded = f.OnGround;
        }

        private bool UpdateContacts(HapticStateBuilder builder, HapticFrameContext f, IAircraftHapticModel model, double[] rates, double dt)
        {
            bool changed = false;
            bool skids = model.GroundContactKind == GroundContactKind.FourPointSkids;
            bool wheels = model.GroundContactKind == GroundContactKind.ThreePointWheels;
            // UH-60 brake unloading can leave the binary tail contact asserted.
            // Preserve the Hook's separately detected tail-support recovery edge.
            bool tailSettlePulse = model.Family == "UH60" && f.Ground.tailWheelContactPulse == 1;
            bool[] current = { f.TailContact, f.LeftContact, f.RightContact, f.SkidFrontLeft, f.SkidFrontRight, f.SkidRearLeft, f.SkidRearRight };
            bool[] directPulse = { f.Ground.tailWheelContactPulse == 1, f.Ground.leftWheelContactPulse == 1,
                f.Ground.rightWheelContactPulse == 1, f.Ground.skidFrontLeftContactPulse == 1,
                f.Ground.skidFrontRightContactPulse == 1, f.Ground.skidRearLeftContactPulse == 1,
                f.Ground.skidRearRightContactPulse == 1 };
            HapticSignalName[] names = { HapticSignalName.TailContact, HapticSignalName.LeftContact, HapticSignalName.RightContact,
                HapticSignalName.SkidFrontLeft, HapticSignalName.SkidFrontRight, HapticSignalName.SkidRearLeft, HapticSignalName.SkidRearRight };
            for (int i = 0; i < current.Length; i++)
            {
                bool available = i < 3 ? wheels : skids;
                if (available && initialized && current[i] != previousContact[i]) changed = true;
                bool recovery = i == 0 && tailSettlePulse && !previousTailSettlePulse && f.OnGround;
                bool directEdge = directPulse[i] && !previousDirectContactPulse[i];
                if (available && initialized && dt > 0 && ((current[i] && !previousContact[i]) || recovery || directEdge)
                    && f.ModelTime >= contactUntil[i])
                {
                    changed = true;
                    int index = skids ? i - 3 : i;
                    double load = rates == null ? 0 : Math.Max(0, rates[index]);
                    contactStrength[i] = rates == null ? 0.2 : 0.1 + 0.7 * HapticMath.SmoothStep(0.05, 2.0, load);
                    // The Hook retains a short pulse for a real per-wheel
                    // contact edge.  It covers fast or lightly loaded wheel
                    // strikes that can be missed by a compression sample.
                    // A UH-60 tail-first arrival has much more initial tail
                    // compression than its following main-wheel contact. Do
                    // not let that sample-rate-dependent travel difference
                    // make the tail cue dominate a soft main-wheel arrival.
                    if (directEdge)
                    {
                        bool mainWheel = wheels && (i == 1 || i == 2);
                        double floor = mainWheel ? 0.50 : 0.32;
                        double range = mainWheel ? 0.40 : 0.25;
                        contactStrength[i] = Math.Max(contactStrength[i], floor
                            + range * HapticMath.SmoothStep(0.05, 2.0, load));
                    }
                    contacts[i].Trigger(f.ModelTime); contactIds[i]++; contactUntil[i] = f.ModelTime + 0.15;
                }
                double value = contactStrength[i] * contacts[i].Value(f.ModelTime);
                builder.Set(names[i], new HapticSignalState(available, value > 0, value, value,
                    contacts[i].Phase(f.ModelTime), contactIds[i], HapticSourceConfidence.Provisional, model.Id + ":contact-loading"));
                previousContact[i] = current[i];
                previousDirectContactPulse[i] = directPulse[i];
            }
            previousTailSettlePulse = tailSettlePulse;
            return changed;
        }

        public void Reset()
        {
            initialized = wasGrounded = compressionInitialized = previousTailSettlePulse = false; bumpArmed = true;
            previousCompression = null; groundSince = bumpUntil = bumpStrength = settleStrength = noise = 0;
            bumpId = touchdownId = settleId = 0; bump.Reset(); settle.Reset(); roughness.Reset(); pitch.Reset(); roll.Reset();
            for (int i = 0; i < contacts.Length; i++)
            { contacts[i].Reset(); previousContact[i] = previousDirectContactPulse[i] = false; contactStrength[i] = contactUntil[i] = 0; contactIds[i] = 0; }
        }
    }
}

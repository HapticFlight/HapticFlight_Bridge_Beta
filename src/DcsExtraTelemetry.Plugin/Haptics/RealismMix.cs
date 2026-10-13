using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    // Gain guidance only. Actual audio summation, routing and amplifier limits are downstream.
    internal sealed class RealismMix
    {
        private double continuousGain = 1.0;
        public void Reset() { continuousGain = 1.0; }

        public void Update(HapticStateBuilder builder, double delta)
        {
            builder.Set(HapticSignalName.TimingDeltaSeconds, HapticSignalState.Continuous(true,
                delta, HapticSourceConfidence.DerivedVerified, "engine-timing:model-time-delta"));
            double impact = Math.Max(builder.Get(HapticSignalName.Touchdown).Envelope,
                Math.Max(builder.Get(HapticSignalName.GroundBump).Envelope,
                    builder.Get(HapticSignalName.AirframeShock).Envelope));
            double weapon = Math.Max(builder.Get(HapticSignalName.LightGun).Envelope,
                Math.Max(builder.Get(HapticSignalName.HeavyGun).Envelope,
                    Math.Max(builder.Get(HapticSignalName.RocketLaunch).Envelope,
                        builder.Get(HapticSignalName.MissileLaunch).Envelope)));
            double priority = Math.Max(impact, weapon);
            double target = 1.0 - .55 * priority;
            if (delta > 0 && delta <= .25)
            {
                double tau = target < continuousGain ? .025 : .3;
                continuousGain += (target - continuousGain) * (1.0 - Math.Exp(-delta / tau));
            }
            builder.Set(HapticSignalName.MixContinuousGain, HapticSignalState.Continuous(true,
                continuousGain, HapticSourceConfidence.Provisional, "realism-mix-v1:gain-guidance"));
            builder.Set(HapticSignalName.MixPriorityDemand, HapticSignalState.Continuous(true,
                priority, HapticSourceConfidence.DerivedVerified, "realism-mix-v1:max-event-demand"));
        }
    }
}

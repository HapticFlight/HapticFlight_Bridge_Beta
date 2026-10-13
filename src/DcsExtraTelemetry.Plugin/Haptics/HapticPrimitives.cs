using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal enum HapticSignalPhase
    {
        Unsupported,
        Idle,
        Attack,
        Hold,
        Release
    }

    internal enum HapticSourceConfidence
    {
        Unsupported,
        Direct,
        DerivedVerified,
        Proxy,
        Provisional
    }

    internal enum HapticResetReason
    {
        None,
        Initial,
        NewSession,
        AircraftChanged,
        ModelChanged,
        ModelTimeReversed,
        ModelTimeFrozen,
        TimingGap,
        StaleResume,
        CapabilityChanged,
        SourceChanged,
        PolicyChanged,
        Shutdown
    }

    internal sealed class HapticSignalState
    {
        public static readonly HapticSignalState Unsupported = new HapticSignalState(
            false, false, 0.0, 0.0, HapticSignalPhase.Unsupported, 0L,
            HapticSourceConfidence.Unsupported, string.Empty);

        public HapticSignalState(
            bool supported,
            bool active,
            double intensity,
            double envelope,
            HapticSignalPhase phase,
            long eventId,
            HapticSourceConfidence confidence,
            string source)
        {
            Supported = supported;
            Active = supported && active;
            Intensity = supported ? HapticMath.Clamp01(intensity) : 0.0;
            Envelope = supported ? HapticMath.Clamp01(envelope) : 0.0;
            Phase = supported ? phase : HapticSignalPhase.Unsupported;
            EventId = eventId < 0L ? 0L : eventId;
            Confidence = supported ? confidence : HapticSourceConfidence.Unsupported;
            Source = supported ? source ?? string.Empty : string.Empty;
        }

        public bool Supported { get; private set; }
        public bool Active { get; private set; }
        public double Intensity { get; private set; }
        public double Envelope { get; private set; }
        public HapticSignalPhase Phase { get; private set; }
        public long EventId { get; private set; }
        public HapticSourceConfidence Confidence { get; private set; }
        public string Source { get; private set; }

        public static HapticSignalState Continuous(
            bool supported,
            double intensity,
            HapticSourceConfidence confidence,
            string source)
        {
            double value = HapticMath.Clamp01(intensity);
            return supported
                ? new HapticSignalState(true, value > 0.0001, value, value,
                    value > 0.0001 ? HapticSignalPhase.Hold : HapticSignalPhase.Idle,
                    0L, confidence, source)
                : Unsupported;
        }
    }

    internal static class HapticMath
    {
        public static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0) return 0.0;
            return value >= 1.0 ? 1.0 : value;
        }

        public static double SmoothStep(double minimum, double maximum, double value)
        {
            if (maximum <= minimum) return value >= maximum ? 1.0 : 0.0;
            double normalized = Clamp01((value - minimum) / (maximum - minimum));
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }

        public static double Max(params double[] values)
        {
            double maximum = 0.0;
            if (values == null) return maximum;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] > maximum) maximum = values[index];
            }
            return maximum;
        }
    }
}

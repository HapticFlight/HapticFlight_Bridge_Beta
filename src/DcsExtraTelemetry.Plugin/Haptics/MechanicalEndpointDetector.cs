using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal sealed class MechanicalEndpointDetector
    {
        private readonly AttackHoldReleaseEnvelope envelope = new AttackHoldReleaseEnvelope(0.010, 0.035, 0.120);
        private bool initialized;
        private bool moving;
        private double priorPosition;
        private long eventId;

        public HapticSignalState Update(bool supported, double position, double modelTime,
            HapticSourceConfidence confidence, string source)
        {
            if (!supported)
            {
                Reset();
                return HapticSignalState.Unsupported;
            }
            position = HapticMath.Clamp01(position);
            if (!initialized)
            {
                initialized = true;
                priorPosition = position;
                return State(modelTime, confidence, source);
            }

            double movement = Math.Abs(position - priorPosition);
            if (movement > 0.0005) moving = true;
            bool endpoint = position <= 0.01 || position >= 0.99;
            if (moving && endpoint && movement <= 0.0005)
            {
                eventId++;
                envelope.Trigger(modelTime);
                moving = false;
            }
            priorPosition = position;
            return State(modelTime, confidence, source);
        }

        public void Reset()
        {
            initialized = false;
            moving = false;
            priorPosition = 0.0;
            eventId = 0L;
            envelope.Reset();
        }

        private HapticSignalState State(double modelTime, HapticSourceConfidence confidence, string source)
        {
            double value = envelope.Value(modelTime);
            return new HapticSignalState(true, value > 0.0, value, value,
                envelope.Phase(modelTime), eventId, confidence, source);
        }
    }
}

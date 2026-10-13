using System;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal sealed class ExponentialSmoother
    {
        private readonly double attackSeconds;
        private readonly double releaseSeconds;
        private bool initialized;
        private double value;

        public ExponentialSmoother(double attackSeconds, double releaseSeconds)
        {
            if (attackSeconds <= 0.0) throw new ArgumentOutOfRangeException("attackSeconds");
            if (releaseSeconds <= 0.0) throw new ArgumentOutOfRangeException("releaseSeconds");
            this.attackSeconds = attackSeconds;
            this.releaseSeconds = releaseSeconds;
        }

        public double Value { get { return value; } }

        public double Update(double target, double deltaSeconds)
        {
            target = HapticMath.Clamp01(target);
            if (!initialized)
            {
                initialized = true;
                value = target;
                return value;
            }

            if (deltaSeconds <= 0.0) return value;
            double boundedDelta = Math.Min(0.25, deltaSeconds);
            double timeConstant = target > value ? attackSeconds : releaseSeconds;
            double coefficient = 1.0 - Math.Exp(-boundedDelta / timeConstant);
            value += (target - value) * coefficient;
            value = HapticMath.Clamp01(value);
            return value;
        }

        public void Reset()
        {
            initialized = false;
            value = 0.0;
        }
    }

    internal sealed class AttackHoldReleaseEnvelope
    {
        private readonly double attackSeconds;
        private readonly double holdSeconds;
        private readonly double releaseSeconds;
        private double startedAt;
        private bool active;

        public AttackHoldReleaseEnvelope(double attackSeconds, double holdSeconds, double releaseSeconds)
        {
            if (attackSeconds <= 0.0) throw new ArgumentOutOfRangeException("attackSeconds");
            if (holdSeconds < 0.0) throw new ArgumentOutOfRangeException("holdSeconds");
            if (releaseSeconds <= 0.0) throw new ArgumentOutOfRangeException("releaseSeconds");
            this.attackSeconds = attackSeconds;
            this.holdSeconds = holdSeconds;
            this.releaseSeconds = releaseSeconds;
        }

        public void Trigger(double modelTime)
        {
            startedAt = modelTime;
            active = true;
        }

        public double Value(double modelTime)
        {
            if (!active) return 0.0;
            double elapsed = modelTime - startedAt;
            if (elapsed < 0.0)
            {
                Reset();
                return 0.0;
            }
            if (elapsed < attackSeconds) return HapticMath.Clamp01(elapsed / attackSeconds);
            if (elapsed < attackSeconds + holdSeconds) return 1.0;
            double releaseElapsed = elapsed - attackSeconds - holdSeconds;
            if (releaseElapsed < releaseSeconds) return HapticMath.Clamp01(1.0 - releaseElapsed / releaseSeconds);
            active = false;
            return 0.0;
        }

        public HapticSignalPhase Phase(double modelTime)
        {
            if (!active) return HapticSignalPhase.Idle;
            double elapsed = modelTime - startedAt;
            if (elapsed < 0.0) return HapticSignalPhase.Idle;
            if (elapsed < attackSeconds) return HapticSignalPhase.Attack;
            if (elapsed < attackSeconds + holdSeconds) return HapticSignalPhase.Hold;
            if (elapsed < attackSeconds + holdSeconds + releaseSeconds) return HapticSignalPhase.Release;
            return HapticSignalPhase.Idle;
        }

        public bool IsPulseActive(double modelTime)
        {
            return active && modelTime - startedAt >= 0.0
                && modelTime - startedAt < attackSeconds + holdSeconds;
        }

        public void Reset()
        {
            active = false;
            startedAt = 0.0;
        }
    }

    internal sealed class HysteresisLatch
    {
        private readonly double onThreshold;
        private readonly double offThreshold;
        private bool value;

        public HysteresisLatch(double onThreshold, double offThreshold)
        {
            if (offThreshold > onThreshold) throw new ArgumentException("Off threshold must not exceed on threshold.");
            this.onThreshold = onThreshold;
            this.offThreshold = offThreshold;
        }

        public bool Update(double input)
        {
            value = input > (value ? offThreshold : onThreshold);
            return value;
        }

        public void Reset() { value = false; }
    }

    internal sealed class RateOfChangeFilter
    {
        private bool initialized;
        private double prior;

        public double Update(double value, double deltaSeconds)
        {
            if (!initialized)
            {
                initialized = true;
                prior = value;
                return 0.0;
            }
            if (deltaSeconds <= 0.001 || deltaSeconds > 0.25)
            {
                prior = value;
                return 0.0;
            }
            double rate = (value - prior) / deltaSeconds;
            prior = value;
            return rate;
        }

        public void Reset()
        {
            initialized = false;
            prior = 0.0;
        }
    }

    internal sealed class RollingPeakFilter
    {
        private readonly double[] times;
        private readonly double[] values;
        private int start;
        private int count;

        public RollingPeakFilter(int capacity)
        {
            if (capacity < 2 || capacity > 4096) throw new ArgumentOutOfRangeException("capacity");
            times = new double[capacity];
            values = new double[capacity];
        }

        public void Add(double modelTime, double value)
        {
            int index;
            if (count < times.Length)
            {
                index = (start + count) % times.Length;
                count++;
            }
            else
            {
                index = start;
                start = (start + 1) % times.Length;
            }
            times[index] = modelTime;
            values[index] = double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
        }

        public double PeakSince(double minimumModelTime)
        {
            double peak = 0.0;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (start + offset) % times.Length;
                if (times[index] >= minimumModelTime && values[index] > peak) peak = values[index];
            }
            return peak;
        }

        public void Reset()
        {
            start = 0;
            count = 0;
        }
    }
}

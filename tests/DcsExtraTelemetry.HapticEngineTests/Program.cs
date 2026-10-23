using DcsExtraTelemetry.Plugin.Haptics;
using DcsExtraTelemetry.Plugin.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.HapticEngineTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && string.Equals(args[0], "--analyze-recording", StringComparison.OrdinalIgnoreCase))
                    return AnalyzeRecording(args);
                TestModelSelectionAndValidation(); TestFiltersAndEnvelope(); TestParkedStartDoesNotTrigger();
                TestTouchdownStrengthAndBounce(); TestWheelContactSemantics(); TestFourSkidConfirmation(); TestLiftoffConfirmation(); TestSnapshotImmutability();
                TestEngineTimingResets(); TestSessionChangeWhilePaused(); TestCapabilityGating(); TestRealismMix(); TestEngineLifecycleAndPerformance();
                TestGroundInteraction(); TestMainWheelContactPriority(); TestGroundRateIndependence(); TestQuickBounceAndCapturedSink();
                Console.WriteLine("PASS: aircraft-aware haptic engine models, filters, touchdown, lifecycle, performance, and snapshots.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); return 1; }
        }

        private sealed class SignalStatistics
        {
            public readonly List<double> Values = new List<double>();
            public long Supported;
            public long Active;
            public long MaximumEventId;
            public readonly HashSet<string> Confidence = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Sources = new HashSet<string>(StringComparer.Ordinal);
        }

        private static int AnalyzeRecording(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("Usage: --analyze-recording <recording.jsonl> <output.json>");
            string inputPath = Path.GetFullPath(args[1]);
            string outputPath = Path.GetFullPath(args[2]);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var engine = new HapticStateEngine();
            var signals = Enum.GetValues(typeof(HapticSignalName)).Cast<HapticSignalName>()
                .Where(name => name != HapticSignalName.Count)
                .ToDictionary(name => name, name => new SignalStatistics());
            var resetReasonTransitions = new Dictionary<string, long>(StringComparer.Ordinal);
            string previousResetReason = null;
            long samples = 0, malformed = 0;
            double firstSentAt = double.NaN, lastSentAt = double.NaN;
            DateTime receivedOrigin = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            using (var reader = File.OpenText(inputPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    TelemetryPacket packet;
                    try { packet = serializer.Deserialize<TelemetryPacket>(line); }
                    catch { malformed++; continue; }
                    if (packet == null || packet.values == null || packet.aircraft == null) { malformed++; continue; }
                    if (double.IsNaN(firstSentAt)) firstSentAt = packet.sentAtMs;
                    lastSentAt = packet.sentAtMs;
                    DateTime received = receivedOrigin.AddMilliseconds(Math.Max(0, packet.sentAtMs - firstSentAt));
                    HapticStateSnapshot snapshot = engine.Update(packet, received);
                    string reset = snapshot.LastResetReason.ToString();
                    if (!string.Equals(previousResetReason, reset, StringComparison.Ordinal))
                    {
                        resetReasonTransitions[reset] = resetReasonTransitions.ContainsKey(reset)
                            ? resetReasonTransitions[reset] + 1 : 1;
                        previousResetReason = reset;
                    }
                    samples++;
                    foreach (HapticSignalName name in signals.Keys)
                    {
                        HapticSignalState signal = snapshot.Get(name);
                        SignalStatistics stats = signals[name];
                        if (signal.Supported) stats.Supported++;
                        if (signal.Active) stats.Active++;
                        stats.MaximumEventId = Math.Max(stats.MaximumEventId, signal.EventId);
                        if (signal.Supported) stats.Values.Add(signal.Envelope);
                        stats.Confidence.Add(signal.Confidence.ToString());
                        if (!string.IsNullOrWhiteSpace(signal.Source)) stats.Sources.Add(signal.Source);
                    }
                }
            }

            var signalReport = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<HapticSignalName, SignalStatistics> entry in signals)
            {
                SignalStatistics stats = entry.Value;
                stats.Values.Sort();
                signalReport[entry.Key.ToString()] = new
                {
                    supportedSamples = stats.Supported,
                    activeSamples = stats.Active,
                    activePercent = samples == 0 ? 0 : Math.Round(100.0 * stats.Active / samples, 4),
                    mean = stats.Values.Count == 0 ? 0 : Math.Round(stats.Values.Average(), 6),
                    p95 = Percentile(stats.Values, .95),
                    p99 = Percentile(stats.Values, .99),
                    max = stats.Values.Count == 0 ? 0 : Math.Round(stats.Values[stats.Values.Count - 1], 6),
                    maximumEventId = stats.MaximumEventId,
                    confidence = stats.Confidence.OrderBy(value => value).ToArray(),
                    sources = stats.Sources.OrderBy(value => value).ToArray()
                };
            }
            var report = new
            {
                schemaVersion = 1,
                recording = inputPath,
                engineVersion = HapticStateSnapshot.CurrentEngineVersion,
                samples,
                malformedLines = malformed,
                durationSeconds = double.IsNaN(firstSentAt) ? 0 : Math.Round((lastSentAt - firstSentAt) / 1000.0, 3),
                resetReasonTransitions,
                signals = signalReport,
                caveat = "Engine replay validates deterministic signal behavior; final tactile gain requires hardware seat testing."
            };
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, serializer.Serialize(report), new UTF8Encoding(false));
            Console.WriteLine("PASS: analyzed {0} packets with Haptic Engine {1}.", samples, HapticStateSnapshot.CurrentEngineVersion);
            return malformed == 0 && samples > 0 ? 0 : 2;
        }

        private static double Percentile(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0;
            int index = Math.Min(sorted.Count - 1, Math.Max(0, (int)Math.Ceiling(sorted.Count * fraction) - 1));
            return Math.Round(sorted[index], 6);
        }

        private static void TestModelSelectionAndValidation()
        {
            AircraftHapticModelCatalog catalog = new AircraftHapticModelCatalog();
            Assert(catalog.Status.StartsWith("Loaded 6", StringComparison.Ordinal), "embedded aircraft-model definitions did not load.");
            Assert(catalog.Select("UH-60L", "UH60").Id == "UH60-v1", "UH-60 model selection failed.");
            Assert(catalog.Select("MH-6J", "AH6").GroundContactKind == GroundContactKind.FourPointSkids, "AH-6 skid policy was not selected.");
            Assert(catalog.Select("OH58D", "OH58").Provisional, "OH-58 model lost its provisional status.");
            Assert(catalog.Select("UNKNOWN", "OTHER").Id == "Generic-v1", "Unknown aircraft did not fail closed to generic.");
            AircraftHapticModelCatalog invalidCatalog = new AircraftHapticModelCatalog("[{\"Id\":\"broken\",\"Version\":\"1\",\"GroundContactKind\":\"not-a-kind\"}]");
            Assert(invalidCatalog.Select("UH-60L", "UH60").Id == "Generic-v1"
                && invalidCatalog.Status.StartsWith("Definitions invalid", StringComparison.Ordinal),
                "invalid definitions did not fail closed to verified generic.");
            bool rejected = false;
            try { new TouchdownParameters { FullScaleSinkRateMps = 0.1 }.Validate(); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Invalid model parameters were accepted.");
        }

        private static void TestFiltersAndEnvelope()
        {
            ExponentialSmoother smoother = new ExponentialSmoother(0.08, 0.22);
            AssertNear(smoother.Update(0.0, 0.016), 0.0, "smoother initial value");
            double rise = smoother.Update(1.0, 0.016);
            Assert(rise > 0.0 && rise < 1.0, "smoother did not produce a bounded rise.");
            AssertNear(smoother.Update(0.0, 0.0), rise, "zero-time smoother update");
            AttackHoldReleaseEnvelope envelope = new AttackHoldReleaseEnvelope(0.02, 0.05, 0.20);
            envelope.Trigger(1.0);
            AssertNear(envelope.Value(1.0), 0.0, "envelope start");
            Assert(envelope.Value(1.02) > 0.99, "envelope did not reach hold.");
            Assert(envelope.Value(1.15) > 0.0, "envelope released too early.");
            AssertNear(envelope.Value(1.30), 0.0, "envelope did not clear.");
            RollingPeakFilter peak = new RollingPeakFilter(4);
            peak.Add(1.0, 0.2); peak.Add(1.1, 0.8); peak.Add(1.2, 0.4);
            AssertNear(peak.PeakSince(1.05), 0.8, "rolling peak window");
            AssertNear(peak.PeakSince(1.15), 0.4, "rolling peak expiry");
            peak.Reset();
            AssertNear(peak.PeakSince(0.0), 0.0, "rolling peak reset");
            MechanicalEndpointDetector endpoint = new MechanicalEndpointDetector();
            Assert(endpoint.Update(true, 0.0, 0.0, HapticSourceConfidence.Direct, "test").EventId == 0L,
                "mechanical endpoint produced an initialization event.");
            endpoint.Update(true, 0.5, 0.1, HapticSourceConfidence.Direct, "test");
            endpoint.Update(true, 1.0, 0.2, HapticSourceConfidence.Direct, "test");
            HapticSignalState endpointTriggered = endpoint.Update(true, 1.0, 0.21, HapticSourceConfidence.Direct, "test");
            Assert(endpointTriggered.EventId == 1L, "mechanical endpoint did not trigger after movement settled.");
            Assert(endpoint.Update(true, 1.0, 0.23, HapticSourceConfidence.Direct, "test").Active,
                "mechanical endpoint envelope did not become active.");
        }

        private static void TestParkedStartDoesNotTrigger()
        {
            IAircraftHapticModel model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");
            TouchdownEventDetector detector = new TouchdownEventDetector(model.Touchdown);
            for (int index = 0; index < 30; index++)
            {
                HapticSignalState state = detector.Update(Frame(index / 60.0, true, -2.0, true, true, true), model);
                Assert(state.EventId == 0L && state.Envelope == 0.0, "parked start produced touchdown.");
            }
        }

        private static void TestTouchdownStrengthAndBounce()
        {
            IAircraftHapticModel model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");
            HapticSignalState gentle = RunWheelTouchdown(model, -1.0), firm = RunWheelTouchdown(model, -3.0);
            Assert(gentle.EventId == 1L && gentle.Envelope > 0.0, "gentle touchdown was not detected.");
            Assert(firm.Intensity > gentle.Intensity, "firm touchdown was not stronger than gentle touchdown.");
            TouchdownEventDetector detector = new TouchdownEventDetector(model.Touchdown);
            detector.Update(Frame(0.00, true, 0.0, true, true, true), model);
            detector.Update(Frame(0.10, false, -1.5, false, false, false), model);
            detector.Update(Frame(0.40, false, -1.5, false, false, false), model);
            detector.Update(Frame(0.45, true, -1.5, false, true, true), model);
            HapticSignalState first = detector.Update(Frame(0.51, true, 0.0, false, true, true), model);
            Assert(first.EventId == 1L, "first bounce touchdown was not detected.");
            detector.Update(Frame(0.90, false, -1.0, false, false, false), model);
            detector.Update(Frame(1.02, false, -1.0, false, false, false), model);
            detector.Update(Frame(1.04, true, -1.0, false, true, true), model);
            HapticSignalState second = detector.Update(Frame(1.10, true, 0.0, false, true, true), model);
            Assert(second.EventId == 2L, "confirmed bounce did not retrigger touchdown.");
            HapticSignalState cleared = detector.Update(Frame(1.50, true, 0.0, false, true, false), model);
            Assert(cleared.EventId == 2L && !cleared.Active && cleared.Intensity == 0.0 && cleared.Envelope == 0.0,
                "touchdown output did not clear after release.");
        }

        private static void TestFourSkidConfirmation()
        {
            IAircraftHapticModel model = new AircraftHapticModelCatalog().Select("AH-6J", "AH6");
            TouchdownEventDetector detector = new TouchdownEventDetector(model.Touchdown);
            detector.Update(SkidFrame(0.00, true, false, false, false, false, 0.0), model);
            detector.Update(SkidFrame(0.10, false, false, false, false, false, -1.0), model);
            detector.Update(SkidFrame(0.40, false, false, false, false, false, -1.0), model);
            detector.Update(SkidFrame(0.45, true, true, false, false, false, -1.0), model);
            HapticSignalState single = detector.Update(SkidFrame(0.70, true, true, false, false, false, 0.0), model);
            Assert(single.EventId == 0L, "single-skid contact produced body touchdown.");
            detector.Update(SkidFrame(0.71, true, true, true, false, false, 0.0), model);
            HapticSignalState bilateral = detector.Update(SkidFrame(0.77, true, true, true, false, false, 0.0), model);
            Assert(bilateral.EventId == 1L, "bilateral skid contact did not confirm touchdown.");
        }

        private static void TestWheelContactSemantics()
        {
            IAircraftHapticModel model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");

            TouchdownEventDetector briefSingle = ArmedWheelDetector(model);
            briefSingle.Update(Frame(0.45, true, -1.0, false, false, true), model);
            HapticSignalState brief = briefSingle.Update(Frame(0.51, true, 0.0, false, false, true), model);
            Assert(brief.EventId == 0L, "brief single-main contact produced body touchdown.");

            TouchdownEventDetector persistentSingle = ArmedWheelDetector(model);
            persistentSingle.Update(Frame(0.45, true, -1.0, false, false, true), model);
            HapticSignalState persistent = persistentSingle.Update(Frame(0.58, true, 0.0, false, false, true), model);
            Assert(persistent.EventId == 1L, "persistent single-main landing did not confirm touchdown.");

            TouchdownEventDetector bothMains = ArmedWheelDetector(model);
            bothMains.Update(Frame(0.45, true, -1.0, false, true, true), model);
            HapticSignalState centered = bothMains.Update(Frame(0.51, true, 0.0, false, true, true), model);
            Assert(centered.EventId == 1L, "two-main landing did not confirm centered touchdown.");

            TouchdownEventDetector tailOnly = ArmedWheelDetector(model);
            tailOnly.Update(Frame(0.45, true, -1.0, true, false, false), model);
            HapticSignalState tail = tailOnly.Update(Frame(0.70, true, 0.0, true, false, false), model);
            Assert(tail.EventId == 0L, "tailwheel-only contact produced body touchdown.");

            TouchdownEventDetector hookConfirmedTail = ArmedWheelDetector(model);
            HapticFrameContext tailImpact = Frame(0.45, true, -2.0, true, false, false);
            tailImpact.RawTouchdownPulse = 1.0; tailImpact.RawTouchdownStrength = 0.5;
            hookConfirmedTail.Update(tailImpact, model);
            HapticFrameContext tailConfirm = Frame(0.51, true, 0.0, true, false, false);
            tailConfirm.RawTouchdownPulse = 1.0; tailConfirm.RawTouchdownStrength = 0.5;
            hookConfirmedTail.Update(tailConfirm, model);
            HapticFrameContext tailOutput = Frame(0.53, true, 0.0, true, false, false);
            tailOutput.RawTouchdownPulse = 1.0; tailOutput.RawTouchdownStrength = 0.5;
            HapticSignalState confirmedTail = hookConfirmedTail.Update(tailOutput, model);
            Assert(confirmedTail.EventId == 1L && confirmedTail.Envelope > 0.0,
                "Hook-confirmed tail-first rolling landing did not produce touchdown.");
        }

        private static TouchdownEventDetector ArmedWheelDetector(IAircraftHapticModel model)
        {
            TouchdownEventDetector detector = new TouchdownEventDetector(model.Touchdown);
            detector.Update(Frame(0.00, true, 0.0, true, true, true), model);
            detector.Update(Frame(0.10, false, -1.0, false, false, false), model);
            detector.Update(Frame(0.40, false, -1.0, false, false, false), model);
            return detector;
        }

        private static void TestLiftoffConfirmation()
        {
            IAircraftHapticModel model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");
            LiftoffEventDetector detector = new LiftoffEventDetector();
            detector.Update(Frame(0.0, true, 0.0, true, true, true), model);
            detector.Update(Frame(0.1, false, 0.5, false, false, false), model);
            HapticSignalState state = detector.Update(Frame(0.16, false, 0.5, false, false, false), model);
            Assert(state.EventId == 1L, "liftoff was not confirmed.");
            HapticSignalState cleared = detector.Update(Frame(0.50, false, 0.5, false, false, false), model);
            Assert(cleared.EventId == 1L && !cleared.Active && cleared.Intensity == 0.0 && cleared.Envelope == 0.0,
                "liftoff output did not clear after release.");
        }

        private static void TestSnapshotImmutability()
        {
            HapticSignalState[] source = new HapticSignalState[(int)HapticSignalName.Count];
            source[(int)HapticSignalName.Touchdown] = HapticSignalState.Continuous(true, 0.5, HapticSourceConfidence.DerivedVerified, "test");
            HapticStateSnapshot snapshot = new HapticStateSnapshot("TEST", "TEST-v1", "1", "test catalog", HapticResetReason.None, DateTime.UtcNow, source);
            source[(int)HapticSignalName.Touchdown] = HapticSignalState.Unsupported;
            AssertNear(snapshot.Get(HapticSignalName.Touchdown).Intensity, 0.5, "snapshot copied source state");
        }

        private static void TestEngineLifecycleAndPerformance()
        {
            HapticStateEngine engine = new HapticStateEngine();
            TelemetryPacket packet = EnginePacket();
            DateTime received = DateTime.UtcNow;
            HapticStateSnapshot first = engine.Update(packet, received);
            Assert(first.ModelId == "UH60-v1" && first.ModelStatus.StartsWith("Loaded 6", StringComparison.Ordinal),
                "engine did not publish the validated UH-60 model catalog.");
            HapticStateSnapshot duplicate = engine.Update(packet, received.AddMilliseconds(1));
            Assert(object.ReferenceEquals(first, duplicate), "duplicate packet advanced haptic state.");
            packet.sequence = 0;
            packet.modelTime = 99.0;
            HapticStateSnapshot outOfOrder = engine.Update(packet, received.AddMilliseconds(2));
            Assert(object.ReferenceEquals(first, outOfOrder), "out-of-order packet advanced haptic state.");

            packet.sequence = 2;
            packet.modelTime = 1.0 + 1.0 / 60.0;
            packet.capabilities.brakes = false;
            HapticStateSnapshot capabilityReset = engine.Update(packet, received.AddMilliseconds(17));
            Assert(capabilityReset.LastResetReason == HapticResetReason.CapabilityChanged,
                "capability transition did not reset the engine.");

            packet.capabilities.brakes = true;
            packet.sequence++;
            packet.modelTime += 1.0 / 60.0;
            packet.values.ground.strutSource = "LoGetMechInfo";
            HapticStateSnapshot sourceReset = engine.Update(packet, received.AddMilliseconds(34));
            Assert(sourceReset.LastResetReason == HapticResetReason.CapabilityChanged,
                "capability restoration did not reset before source classification.");
            packet.sequence++;
            packet.modelTime += 1.0 / 60.0;
            packet.values.ground.strutSource = "UH60DrawArguments";
            HapticStateSnapshot sourceChanged = engine.Update(packet, received.AddMilliseconds(51));
            Assert(sourceChanged.LastResetReason == HapticResetReason.SourceChanged,
                "source transition did not reset the engine.");
            const int iterations = 25000;
            Stopwatch watch = Stopwatch.StartNew();
            for (int index = 0; index < iterations; index++)
            {
                packet.sequence++;
                packet.modelTime += 1.0 / 60.0;
                engine.Update(packet, received.AddSeconds(packet.modelTime));
            }
            watch.Stop();
            double millisecondsPerPacket = watch.Elapsed.TotalMilliseconds / iterations;
            Assert(millisecondsPerPacket < 0.25,
                "haptic update exceeded 0.25 ms/packet: " + millisecondsPerPacket.ToString("0.0000") + " ms.");
            Console.WriteLine("Haptic update benchmark: " + millisecondsPerPacket.ToString("0.0000") + " ms/packet.");
        }

        private static void TestEngineTimingResets()
        {
            HapticStateEngine engine = new HapticStateEngine();
            TelemetryPacket packet = EnginePacket();
            DateTime received = DateTime.UtcNow;
            engine.Update(packet, received);

            packet.sequence++; packet.modelTime = 0.5;
            Assert(engine.Update(packet, received.AddMilliseconds(17)).LastResetReason == HapticResetReason.ModelTimeReversed,
                "backward model time did not reset.");
            packet.sequence++; packet.modelTime = 1.1;
            Assert(engine.Update(packet, received.AddMilliseconds(34)).LastResetReason == HapticResetReason.TimingGap,
                "excessive model-time gap did not reset.");
            packet.sequence++; packet.modelTime += 1.0 / 60.0;
            Assert(engine.Update(packet, received.AddSeconds(2)).LastResetReason == HapticResetReason.StaleResume,
                "stale resume did not reset.");
            packet.sequence++;
            engine.Update(packet, received.AddSeconds(2.02));
            packet.sequence++;
            HapticStateSnapshot frozen = engine.Update(packet, received.AddSeconds(2.30));
            Assert(frozen.LastResetReason == HapticResetReason.ModelTimeFrozen
                && !frozen.Get(HapticSignalName.Touchdown).Supported,
                "frozen model time did not fail closed.");
        }

        private static void TestRealismMix()
        {
            double reference = 0;
            foreach (int hz in new[] { 30, 60, 120 })
            {
                var mix = new RealismMix(); var builder = new HapticStateBuilder();
                builder.Set(HapticSignalName.HeavyGun, HapticSignalState.Continuous(true, 1,
                    HapticSourceConfidence.Direct, "test"));
                for (int i = 0; i < hz; i++) mix.Update(builder, 1.0 / hz);
                AssertNear(builder.Get(HapticSignalName.TimingDeltaSeconds).Intensity, 1.0 / hz, "timing delta");
                double gain = builder.Get(HapticSignalName.MixContinuousGain).Intensity;
                Assert(Math.Abs(gain - .45) < .000001, "mix attack outside bound");
                if (reference > 0) Assert(Math.Abs(gain - reference) < .000001, "mix depends on frame rate");
                reference = gain;
                AssertNear(builder.Get(HapticSignalName.HeavyGun).Intensity, 1, "mix mutated legacy output");
                builder.Set(HapticSignalName.HeavyGun, HapticSignalState.Unsupported);
                mix.Update(builder, 1.0 / hz);
                Assert(builder.Get(HapticSignalName.MixContinuousGain).Intensity < .6, "mix recovery snapped");
                for (int i = 0; i < hz * 3; i++) mix.Update(builder, 1.0 / hz);
                Assert(builder.Get(HapticSignalName.MixContinuousGain).Intensity > .999, "mix failed to recover");
                mix.Reset(); mix.Update(builder, 0);
                AssertNear(builder.Get(HapticSignalName.MixContinuousGain).Intensity, 1, "mix reset");
            }
        }

        private static void TestSessionChangeWhilePaused()
        {
            var engine = new HapticStateEngine();
            var packet = EnginePacket();
            DateTime now = DateTime.UtcNow;
            engine.Update(packet, now);
            packet.sequence++;
            engine.Update(packet, now.AddMilliseconds(10));
            packet.sequence++;
            engine.Update(packet, now.AddMilliseconds(300));
            packet.sessionId = "new-session-at-same-model-time";
            packet.sequence = 1;
            Assert(engine.Update(packet, now.AddMilliseconds(310)).LastResetReason == HapticResetReason.NewSession,
                "new session at frozen model time was misclassified as pause.");
            packet.sequence++; packet.modelTime += .016;
            packet.capabilities.flaps = false;
            Assert(engine.Update(packet, now.AddMilliseconds(326)).LastResetReason == HapticResetReason.CapabilityChanged,
                "mechanical capability loss did not reset endpoint history.");
        }

        private static void TestCapabilityGating()
        {
            var engine = new HapticStateEngine();
            var packet = EnginePacket();
            packet.values.ground.onGround = 0;
            packet.values.dynamics.bladeSlapIntensity = .8;
            packet.values.dynamics.apuRunning = true;
            packet.values.dynamics.lowRotorRpmWarning = true;
            HapticStateSnapshot absent = engine.Update(packet, DateTime.UtcNow);
            Assert(!absent.Get(HapticSignalName.BladeSlap).Supported, "blade-slap value bypassed capability gate");
            Assert(!absent.Get(HapticSignalName.ApuRunning).Supported, "lifecycle value bypassed capability gate");
            Assert(!absent.Get(HapticSignalName.LowRotorWarning).Supported, "rotor-warning value bypassed capability gate");

            packet.sequence++; packet.modelTime += .02;
            packet.capabilities.bladeSlap = true;
            packet.capabilities.engineLifecycle = true;
            packet.capabilities.rotorWarning = true;
            HapticStateSnapshot present = engine.Update(packet, DateTime.UtcNow.AddMilliseconds(20));
            Assert(present.LastResetReason == HapticResetReason.CapabilityChanged, "new semantic capabilities did not reset state");
            Assert(present.Get(HapticSignalName.BladeSlap).Supported && present.Get(HapticSignalName.BladeSlap).Intensity > .79,
                "blade-slap capability was not honored");
            Assert(present.Get(HapticSignalName.ApuRunning).Supported && present.Get(HapticSignalName.ApuRunning).Intensity == 1,
                "lifecycle capability was not honored");
            Assert(present.Get(HapticSignalName.LowRotorWarning).Supported && present.Get(HapticSignalName.LowRotorWarning).Intensity == 1,
                "rotor-warning capability was not honored");

            var hueyEngine = new HapticStateEngine();
            var huey = EnginePacket();
            huey.aircraft = new AircraftIdentity { dcsType = "UH-1H", family = "UH1", groundType = "SKIDS" };
            huey.values.dynamics.torqueGaugeNormalized = .7;
            Assert(!hueyEngine.Update(huey, DateTime.UtcNow).Get(HapticSignalName.Uh1TorqueLoad).Supported,
                "torque gauge value bypassed capability gate");
            huey.sequence++; huey.modelTime += .02; huey.capabilities.torqueGauge = true;
            Assert(hueyEngine.Update(huey, DateTime.UtcNow.AddMilliseconds(20)).Get(HapticSignalName.Uh1TorqueLoad).Supported,
                "torque gauge capability was not honored");
        }

        private static TelemetryPacket EnginePacket()
        {
            return new TelemetryPacket
            {
                protocol = 1,
                sessionId = "haptic-test",
                sequence = 1,
                modelTime = 1.0,
                aircraft = new AircraftIdentity { dcsType = "UH-60L", family = "UH60", groundType = "WHEELS" },
                capabilities = new TelemetryCapabilities
                {
                    acceleration = true, angularVelocity = true, airData = true, gear = true, flaps = true,
                    speedbrake = true, gearStruts = true, groundMotion = true, detailedGroundEffects = true,
                    brakes = true, rotorDynamics = true, powertrainEffects = true, aerodynamicEffects = true,
                    doors = true, weaponEffects = true, countermeasures = true
                },
                values = new TelemetryValues
                {
                    acceleration = new Vector3 { x = 0.0, y = 1.0, z = 0.0 },
                    angularVelocity = new Vector3 { x = 0.0, y = 0.0, z = 0.0 },
                    trueAirSpeedMps = 20.0,
                    verticalVelocityMps = 0.0,
                    ground = new GroundValues { strutSource = "UH60DrawArguments", brakeSource = "UH60BrakeArgument" },
                    dynamics = new DynamicsValues { rotorSource = "UH60RotorPhase", mainRotorRpmPct = 100.0, rotorRunning = true },
                    weapons = new WeaponValues(),
                    countermeasures = new CountermeasureValues(),
                    cockpit = new CockpitValues { doors = new DoorValues() },
                    mechanical = new MechanicalValues()
                }
            };
        }

        private static HapticSignalState RunWheelTouchdown(IAircraftHapticModel model, double verticalVelocity)
        {
            TouchdownEventDetector detector = new TouchdownEventDetector(model.Touchdown);
            detector.Update(Frame(0.00, true, 0.0, true, true, true), model);
            detector.Update(Frame(0.10, false, verticalVelocity, false, false, false), model);
            detector.Update(Frame(0.40, false, verticalVelocity, false, false, false), model);
            detector.Update(Frame(0.45, true, verticalVelocity, false, true, true), model);
            detector.Update(Frame(0.51, true, 0.0, false, true, true), model);
            return detector.Update(Frame(0.53, true, 0.0, false, true, true), model);
        }

        private static HapticFrameContext Frame(double time, bool onGround, double verticalVelocity, bool tail, bool left, bool right)
        {
            return new HapticFrameContext { ModelTime = time, HasGearStruts = true, HasAirData = true, OnGround = onGround,
                TailContact = tail, LeftContact = left, RightContact = right, VerticalVelocity = verticalVelocity,
                StrutSource = "UH60DrawArguments", Ground = new GroundValues() };
        }

        private static HapticFrameContext SkidFrame(double time, bool onGround, bool fl, bool fr, bool rl, bool rr, double verticalVelocity)
        {
            HapticFrameContext frame = Frame(time, onGround, verticalVelocity, false, onGround, onGround);
            frame.SkidFrontLeft = fl; frame.SkidFrontRight = fr; frame.SkidRearLeft = rl; frame.SkidRearRight = rr;
            frame.StrutSource = "AH6SkidDrawArguments"; return frame;
        }

        private static HapticFrameContext GroundFrame(double time, double compression, bool grounded = true)
        {
            var frame = Frame(time, grounded, 0, grounded, grounded, grounded);
            frame.HasGroundMotion = frame.HasDetailedGroundEffects = true;
            frame.GroundSpeed = 5;
            frame.Ground.tailStrutCompression = compression;
            frame.Ground.leftStrutCompression = compression;
            frame.Ground.rightStrutCompression = compression;
            return frame;
        }

        private static HapticStateSnapshot GroundUpdate(GroundInteractionDetector detector, HapticFrameContext frame, double dt)
        {
            var model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");
            var builder = new HapticStateBuilder();
            detector.Update(builder, frame, model, dt, HapticSignalState.Unsupported);
            return builder.Build("UH-60L", model.Id, model.Version, "test", HapticResetReason.None, DateTime.UtcNow);
        }

        private static void TestGroundInteraction()
        {
            var detector = new GroundInteractionDetector();
            var spawn = GroundUpdate(detector, GroundFrame(0, 0.1), 0);
            Assert(spawn.Get(HapticSignalName.GroundBump).EventId == 0, "ground spawn bump");
            Assert(spawn.Get(HapticSignalName.LeftContact).EventId == 0, "ground spawn contact");
            for (int i = 1; i <= 30; i++) GroundUpdate(detector, GroundFrame(i / 60.0, 0.1), 1.0 / 60);
            var impact = GroundUpdate(detector, GroundFrame(0.52, 0.14), 0.02);
            Assert(impact.Get(HapticSignalName.GroundBump).EventId == 1, "supported bump missed");
            var active = GroundUpdate(detector, GroundFrame(0.54, 0.14), 0.02);
            Assert(active.Get(HapticSignalName.GroundBump).Envelope > 0, "bump envelope missing");
            var rebound = GroundUpdate(detector, GroundFrame(0.56, 0.1), 0.02);
            Assert(rebound.Get(HapticSignalName.GroundBump).EventId == 1, "extension became another impact");
            for (int i = 35; i <= 70; i++) GroundUpdate(detector, GroundFrame(i / 60.0, 0.1), 1.0 / 60);
            var quiet = GroundUpdate(detector, GroundFrame(1.2, 0.1), 0.02);
            AssertNear(quiet.Get(HapticSignalName.GroundBump).Envelope, 0, "bump clear");
            var tailRecovery = GroundFrame(1.21, 0.1); tailRecovery.Ground.tailWheelContactPulse = 1;
            var recovery = GroundUpdate(detector, tailRecovery, 0.01);
            Assert(recovery.Get(HapticSignalName.TailContact).EventId == 1, "brake tail recovery lost while binary contact stayed asserted");
            tailRecovery.ModelTime = 1.215;
            Assert(GroundUpdate(detector, tailRecovery, 0.005).Get(HapticSignalName.TailContact).EventId == 1,
                "held tail recovery pulse retriggered");
            var leftWheelStrike = GroundFrame(1.40, 0.1); leftWheelStrike.Ground.leftWheelContactPulse = 1;
            GroundUpdate(detector, leftWheelStrike, 0.02);
            var leftWheelOutput = GroundFrame(1.42, 0.1); leftWheelOutput.Ground.leftWheelContactPulse = 1;
            var leftContact = GroundUpdate(detector, leftWheelOutput, 0.02).Get(HapticSignalName.LeftContact);
            Assert(leftContact.EventId == 1 && leftContact.Envelope > 0 && leftContact.Intensity >= 0.30,
                "direct left-wheel contact pulse did not produce a usable contact cue");
            var air = GroundUpdate(detector, GroundFrame(1.44, 0.16, false), 0.02);
            AssertNear(air.Get(HapticSignalName.SuspensionMovement).Intensity, 0, "airborne roughness");
            AssertNear(air.Get(HapticSignalName.GroundBump).Envelope, 0, "airborne bump");
            detector.Reset();
            GroundUpdate(detector, GroundFrame(0, 0.1), 0);
            var missing = GroundFrame(0.5, 0.2); missing.Ground.leftStrutCompression = null;
            Assert(!GroundUpdate(detector, missing, 0.02).Get(HapticSignalName.GroundBump).Supported, "missing travel did not fail closed");
            missing.Ground.leftStrutCompression = double.NaN;
            Assert(!GroundUpdate(detector, missing, 0.02).Get(HapticSignalName.GroundBump).Supported, "NaN travel accepted");
            var engine = new HapticStateEngine(); var packet = EnginePacket();
            packet.values.ground.onGround = 1;
            packet.values.ground.groundSpeedMps = 6;
            packet.values.ground.groundRollIntensity = .2;
            HapticSignalState wheelRoll = engine.Update(packet, DateTime.UtcNow).Get(HapticSignalName.GroundRoll);
            AssertNear(wheelRoll.Intensity, .2, "derived wheel-roll texture");
            Assert(wheelRoll.Confidence == HapticSourceConfidence.DerivedVerified,
                "derived wheel-roll texture was not distinguished from speed proxy");
            engine = new HapticStateEngine(); packet = EnginePacket();
            packet.aircraft = new AircraftIdentity { dcsType = "UH-1H", family = "UH1" };
            packet.values.ground.onGround = 1;
            packet.values.ground.longitudinalGroundSpeedMps = 2; packet.values.ground.lateralGroundSlipMps = 0;
            var slide = engine.Update(packet, DateTime.UtcNow);
            Assert(slide.Get(HapticSignalName.SkidScrub).Intensity > 0, "forward skid slide missed");
            Assert(!slide.Get(HapticSignalName.GroundBump).Supported, "binary skid signal invented suspension");
            Assert(!slide.Get(HapticSignalName.GroundRoll).Supported, "skid aircraft got wheel roll");
        }

        private static void TestMainWheelContactPriority()
        {
            var tailDetector = new GroundInteractionDetector();
            GroundUpdate(tailDetector, GroundFrame(0, 0.1), 0);
            GroundUpdate(tailDetector, GroundFrame(0.1, 0.1), 0.1);
            var tailStrike = GroundFrame(0.2, 0.1); tailStrike.Ground.tailWheelContactPulse = 1;
            GroundUpdate(tailDetector, tailStrike, 0.1);
            tailStrike.ModelTime = 0.22;
            double tail = GroundUpdate(tailDetector, tailStrike, 0.02).Get(HapticSignalName.TailContact).Intensity;

            var mainDetector = new GroundInteractionDetector();
            GroundUpdate(mainDetector, GroundFrame(0, 0.1), 0);
            GroundUpdate(mainDetector, GroundFrame(0.1, 0.1), 0.1);
            var mainStrike = GroundFrame(0.2, 0.1); mainStrike.Ground.leftWheelContactPulse = 1;
            GroundUpdate(mainDetector, mainStrike, 0.1);
            mainStrike.ModelTime = 0.22;
            double main = GroundUpdate(mainDetector, mainStrike, 0.02).Get(HapticSignalName.LeftContact).Intensity;

            Assert(main >= 0.50, "direct main-wheel floor is too weak for a soft contact");
            Assert(tail <= 0.57, "tail direct-contact range can dominate main-wheel contact");
            Assert(main > tail, "soft direct main-wheel contact is not stronger than tail contact");
        }

        private static void TestGroundRateIndependence()
        {
            double minimum = 1, maximum = 0;
            foreach (int hz in new[] { 30, 60, 120 })
            {
                var detector = new GroundInteractionDetector(); double peak = 0; long events = 0;
                for (int i = 0; i <= hz * 2; i++)
                {
                    double t = (double)i / hz;
                    double travel = 0.1 + (t >= 0.6 && t < 0.7 ? (t - 0.6) : t >= 0.7 && t < 0.9 ? 0.1 - (t - 0.7) * 0.5 : 0);
                    var s = GroundUpdate(detector, GroundFrame(t, travel), i == 0 ? 0 : 1.0 / hz);
                    peak = Math.Max(peak, s.Get(HapticSignalName.GroundBump).Intensity);
                    events = s.Get(HapticSignalName.GroundBump).EventId;
                }
                Assert(events == 1, "sample rate changed bump count at " + hz);
                minimum = Math.Min(minimum, peak); maximum = Math.Max(maximum, peak);
            }
            Assert(maximum - minimum < 0.03, "sample rate changed impact severity");
        }

        private static void TestQuickBounceAndCapturedSink()
        {
            var model = new AircraftHapticModelCatalog().Select("UH-60L", "UH60");
            var detector = new TouchdownEventDetector(model.Touchdown);
            detector.Update(Frame(0, false, -3, false, false, false), model);
            detector.Update(Frame(0.3, false, -3, false, false, false), model);
            detector.Update(Frame(0.32, true, 0, false, true, true), model);
            detector.Update(Frame(0.38, true, 0, false, true, true), model);
            detector.Update(Frame(0.40, false, -1, false, false, false), model);
            detector.Update(Frame(0.51, false, -1, false, false, false), model);
            detector.Update(Frame(0.52, true, 0, false, true, true), model);
            var bounce = detector.Update(Frame(0.58, true, 0, false, true, true), model);
            Assert(bounce.EventId == 2, "short genuine bounce lost to first-impact cooldown");
            detector.Reset();
            detector.Update(Frame(0, false, -3, false, false, false), model);
            detector.Update(Frame(0.3, false, -3, false, false, false), model);
            detector.Update(Frame(0.32, true, 0, false, true, false), model);
            var delayed = detector.Update(Frame(0.6, true, 0, false, true, false), model);
            Assert(delayed.Intensity > 0.5, "delayed confirmation expired pre-contact sink");
        }

        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void AssertNear(double actual, double expected, string name)
        {
            if (Math.Abs(actual - expected) > 0.0001) throw new InvalidOperationException(name + " expected " + expected + " but got " + actual + ".");
        }
    }
}

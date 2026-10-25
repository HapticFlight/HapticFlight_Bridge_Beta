using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.Launcher
{
    internal static class TelemetryAnalysisCategories
    {
        public static readonly string[] All = { "Rotor and Powertrain", "Aerodynamics", "Ground Interaction", "Weapons and Countermeasures" };
    }

    internal sealed class TelemetryAnalysisResult
    {
        public string AircraftType { get; set; }
        public string AircraftFamily { get; set; }
        public long ValidSamples { get; set; }
        public bool Rotor { get; set; }
        public bool Ground { get; set; }
        public bool Weapons { get; set; }
        public bool Cockpit { get; set; }
        public bool Brakes { get; set; }
        public bool Doors { get; set; }
        public bool Touchdown { get; set; }
        public bool Liftoff { get; set; }
        public bool BrakeApplied { get; set; }
        public bool DoorMoved { get; set; }
        public bool LightGun { get; set; }
        public bool HeavyGun { get; set; }
        public bool Rocket { get; set; }
        public bool Missile { get; set; }
        public bool Countermeasure { get; set; }
        public bool Etl { get; set; }
        public double MaxTouchdown { get; set; }
        public double MaxGroundRoll { get; set; }
        public double MaxBraking { get; set; }
        public double MaxRotorDroop { get; set; }
        public double MaxImpact { get; set; }
        public double MaxBladeSlap { get; set; }
        public List<EffectMapTrack> EffectMap { get; } = new List<EffectMapTrack>();
        public List<FlightPhaseSpan> FlightPhases { get; } = new List<FlightPhaseSpan>();
        // These are compact bucketed views of the source recording. They are intentionally
        // separate from EffectMap, whose values are profile-modelled rather than measured
        // SimHub or shaker output.
        public List<TelemetrySignalTrack> TelemetrySignals { get; } = new List<TelemetrySignalTrack>();
        public List<FlightAnalysisEvent> Events { get; } = new List<FlightAnalysisEvent>();
        public List<FlightDebriefFinding> DebriefFindings { get; } = new List<FlightDebriefFinding>();
        public double EffectMapDurationSeconds { get; set; }
        public double AnalysisBucketSeconds { get; set; }
        public double[][] CategoryLoads { get; set; }
        public int[] ActiveEffectCounts { get; set; }
        public int PeakConcurrency { get; set; }
        public double PeakConcurrencyTimeSeconds { get; set; }
        public long BoundsMilliseconds { get; set; }
        public long ParseMilliseconds { get; set; }
        public long BucketBuildMilliseconds { get; set; }
        public long EpisodePhaseMilliseconds { get; set; }
        public long InitialRenderMilliseconds { get; set; }

        public string SignalQualityText()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Aircraft: {0} ({1})\nFlight/rotor: {2}\nGround/contact: {3}\nWeapons: {4}\nCockpit states: {5}\nBrakes: {6}\nDoors: {7}",
                string.IsNullOrEmpty(AircraftType) ? "Not identified" : AircraftType, AircraftFamily,
                Quality(Rotor, AircraftFamily == "AH6" ? "proxy rotor source" : "native/derived source"),
                Quality(Ground, "derived"), Quality(Weapons, "event-derived"), Quality(Cockpit, "module adapter"),
                Quality(Brakes, "dedicated source"), Quality(Doors, "module draw arguments"));
        }

        public string ChecklistText()
        {
            return string.Join("\n", new[] { Check(Liftoff, "Takeoff/liftoff"), Check(Touchdown, "Landing/touchdown"),
                Check(BrakeApplied, "Rolling brake application"), Check(Etl, "ETL transition"),
                Check(LightGun, "Light gun / M134"), Check(HeavyGun, "Heavy gun / M230"), Check(Rocket, "Rocket launch"),
                Check(Missile, "Missile launch"), Check(Countermeasure, "Chaff or flare"), Check(DoorMoved, "Door movement") });
        }

        public string ReportText(string path)
        {
            return "Recording: " + Path.GetFileName(path) + "\nValid samples: " + ValidSamples.ToString(CultureInfo.InvariantCulture) + "\n\n" +
                SignalQualityText() + "\n\nObserved test coverage\n" + ChecklistText() + "\n\nPeak effect-ready values\n" +
                string.Format(CultureInfo.InvariantCulture,
                    "Touchdown {0:0.000} | Ground roll {1:0.000} | Braking {2:0.000} | Rotor droop {3:0.000} | Impact {4:0.000} | Blade slap cue {5:0.000}",
                    MaxTouchdown, MaxGroundRoll, MaxBraking, MaxRotorDroop, MaxImpact, MaxBladeSlap) +
                string.Format(CultureInfo.InvariantCulture,
                    "\n\nLoad diagnostics\nBounds {0} ms | Parse {1} ms | Buckets {2} ms | Episodes/phases {3} ms | Initial render {4} ms | {5} buckets ({6:0.###} s)",
                    BoundsMilliseconds, ParseMilliseconds, BucketBuildMilliseconds, EpisodePhaseMilliseconds, InitialRenderMilliseconds,
                    ActiveEffectCounts == null ? 0 : ActiveEffectCounts.Length, AnalysisBucketSeconds);
        }

        private static string Quality(bool available, string source) { return available ? "Available - " + source : "Unavailable in recording"; }
        private static string Check(bool value, string label) { return (value ? "PASS  " : "NOT OBSERVED  ") + label; }
    }

    internal sealed class EffectMapTrack
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Frequency { get; set; }
        public double FrequencyHz { get; set; }
        public double[] Buckets { get; set; }
        public List<EffectEpisode> Episodes { get; } = new List<EffectEpisode>();
        public double PeakIntensity { get; set; }
        public double AverageActiveIntensity { get; set; }
        public double ActiveSeconds { get; set; }
    }

    internal sealed class EffectEpisode
    {
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public double Peak { get; set; }
        public double Average { get; set; }
    }

    internal sealed class FlightPhaseSpan
    {
        public string Name { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
    }

    internal sealed class TelemetrySignalTrack
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
        public double[] Values { get; set; }
        internal int[] Counts { get; set; }
    }

    internal sealed class FlightAnalysisEvent
    {
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string Phase { get; set; }
        public string Effect { get; set; }
        public string Category { get; set; }
        public string Frequency { get; set; }
        public double Peak { get; set; }
        public double Average { get; set; }
        public string TimeText { get { return Format(StartSeconds); } }
        public string DurationText { get { return (EndSeconds - StartSeconds).ToString("0.00", CultureInfo.InvariantCulture) + " s"; } }
        public string PeakText { get { return (Peak * 100).ToString("0", CultureInfo.InvariantCulture) + "%"; } }
        public string AverageText { get { return (Average * 100).ToString("0", CultureInfo.InvariantCulture) + "%"; } }

        private static string Format(double seconds)
        {
            return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class FlightDebriefFinding
    {
        public string Severity { get; set; }
        public string Text { get; set; }
    }

    internal sealed class PhaseBucketObservation
    {
        public bool Seen;
        public bool Airborne;
        public double GroundMotion;
        public double WeaponActivity;
        public double RotorRpmPercent;
        public bool MachineryRunning;
    }

    internal static class TelemetryRecordingAnalyzer
    {
        private const int MaximumBuckets = 20000;
        private const double PreferredBucketSeconds = .25;

        public static TelemetryAnalysisResult Analyze(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Telemetry recording was not found.", path);
            var result = new TelemetryAnalysisResult { AircraftFamily = "UNKNOWN" };
            var boundsWatch = Stopwatch.StartNew();
            double firstMs, lastMs;
            FindBounds(path, out firstMs, out lastMs);
            boundsWatch.Stop();
            result.BoundsMilliseconds = boundsWatch.ElapsedMilliseconds;
            if (lastMs < firstMs) return result;

            double spanMs = Math.Max(1.0, lastMs - firstMs);
            double durationSeconds = spanMs / 1000.0;
            int bucketCount = Math.Min(MaximumBuckets, Math.Max(1, (int)Math.Ceiling(durationSeconds / PreferredBucketSeconds)));
            double bucketSeconds = durationSeconds / bucketCount;
            var tracks = CreateTracks(bucketCount);
            var tracksByName = new Dictionary<string, EffectMapTrack>(StringComparer.Ordinal);
            foreach (EffectMapTrack track in tracks) tracksByName[track.Name] = track;
            var phases = new PhaseBucketObservation[bucketCount];
            for (int i = 0; i < phases.Length; i++) phases[i] = new PhaseBucketObservation();
            var signals = CreateTelemetrySignals(bucketCount);
            int firstAir = -1, lastAir = -1;
            var values = new double[ScalarNames.Length];
            long parseTicks = 0, bucketTicks = 0;

            using (var reader = File.OpenText(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Array.Clear(values, 0, values.Length);
                    long started = Stopwatch.GetTimestamp();
                    string aircraft;
                    ScanScalars(line, values, out aircraft);
                    parseTicks += Stopwatch.GetTimestamp() - started;
                    if (string.IsNullOrEmpty(aircraft)) continue;
                    result.ValidSamples++;
                    if (string.IsNullOrEmpty(result.AircraftType)) { result.AircraftType = aircraft; result.AircraftFamily = Family(aircraft); }
                    started = Stopwatch.GetTimestamp();
                    int bucket = Math.Min(bucketCount - 1, Math.Max(0, (int)(((Value(values, "sentAtMs") - firstMs) / spanMs) * bucketCount)));
                    AccumulateSummary(result, values);
                    AccumulateEffects(tracks, tracksByName, bucket, values);
                    AccumulateTelemetrySignals(signals, bucket, values);
                    PhaseBucketObservation phase = phases[bucket];
                    phase.Seen = true;
                    phase.Airborne = Value(values, "onGround") < .5;
                    phase.GroundMotion = Math.Max(Value(values, "groundRollIntensity"), Value(values, "wheelBrakesPosition"));
                    phase.WeaponActivity = Math.Max(Math.Max(Value(values, "lightGunIntensity"), Value(values, "heavyGunIntensity")), Math.Max(Value(values, "rocketLaunchIntensity"), Value(values, "missileLaunchIntensity")));
                    phase.RotorRpmPercent = Value(values, "mainRotorRpmPct");
                    phase.MachineryRunning = phase.RotorRpmPercent > 12 || Value(values, "apuRunning") > 0 || Value(values, "engine1StarterActive") > 0 || Value(values, "engine2StarterActive") > 0;
                    if (phase.Airborne) { if (firstAir < 0) firstAir = bucket; lastAir = bucket; }
                    bucketTicks += Stopwatch.GetTimestamp() - started;
                }
            }

            result.ParseMilliseconds = TicksToMilliseconds(parseTicks);
            result.BucketBuildMilliseconds = TicksToMilliseconds(bucketTicks);
            ApplyProfileFrequencyMetadata(result, tracks);
            var finishWatch = Stopwatch.StartNew();
            foreach (EffectMapTrack track in tracks) BuildEpisodes(track, bucketSeconds);
            BuildFlightPhases(result, phases, firstAir, lastAir, durationSeconds);
            BuildLoadMetrics(result, tracks, bucketCount, bucketSeconds);
            AddCombinedLoadSignal(signals, result, bucketCount);
            finishWatch.Stop();
            result.EpisodePhaseMilliseconds = finishWatch.ElapsedMilliseconds;
            result.EffectMap.AddRange(tracks);
            result.EffectMapDurationSeconds = durationSeconds;
            result.AnalysisBucketSeconds = bucketSeconds;
            result.TelemetrySignals.AddRange(signals);
            BuildEvents(result, tracks);
            BuildDebriefFindings(result);
            return result;
        }

        private static List<TelemetrySignalTrack> CreateTelemetrySignals(int bucketCount)
        {
            return new List<TelemetrySignalTrack>
            {
                Signal("Indicated airspeed", "m/s", "recorded telemetry", bucketCount),
                Signal("Vertical speed", "m/s", "recorded telemetry", bucketCount),
                Signal("Rotor RPM", "%", "recorded telemetry; aircraft-dependent direct/derived/proxy", bucketCount),
                Signal("Wheel brakes", "%", "recorded telemetry", bucketCount),
                Signal("Touchdown envelope", "%", "recorded telemetry", bucketCount),
                Signal("Braking intensity", "%", "recorded telemetry", bucketCount),
                Signal("Rotor droop", "%", "recorded telemetry", bucketCount),
                Signal("Modelled combined effect load", "relative", "replayed profile model; not SimHub/shaker output", bucketCount)
            };
        }

        private static TelemetrySignalTrack Signal(string name, string unit, string source, int bucketCount)
        {
            return new TelemetrySignalTrack { Name = name, Unit = unit, Source = source, Values = new double[bucketCount], Counts = new int[bucketCount] };
        }

        private static void AccumulateTelemetrySignals(List<TelemetrySignalTrack> signals, int bucket, double[] values)
        {
            AddSignal(signals[0], bucket, Value(values, "indicatedAirSpeedMps"));
            AddSignal(signals[1], bucket, Value(values, "verticalVelocityMps"));
            AddSignal(signals[2], bucket, Value(values, "mainRotorRpmPct"));
            AddSignal(signals[3], bucket, Value(values, "wheelBrakesPosition") * 100.0);
            AddSignal(signals[4], bucket, Value(values, "touchdownEnvelope") * 100.0);
            AddSignal(signals[5], bucket, Value(values, "brakingIntensity") * 100.0);
            AddSignal(signals[6], bucket, Value(values, "rotorDroopIntensity") * 100.0);
        }

        private static void AddSignal(TelemetrySignalTrack signal, int bucket, double value)
        {
            signal.Values[bucket] += value;
            signal.Counts[bucket]++;
        }

        private static void AddCombinedLoadSignal(List<TelemetrySignalTrack> signals, TelemetryAnalysisResult result, int bucketCount)
        {
            TelemetrySignalTrack combined = signals[signals.Count - 1];
            for (int bucket = 0; bucket < bucketCount; bucket++)
            {
                for (int category = 0; category < result.CategoryLoads.Length; category++)
                    combined.Values[bucket] += result.CategoryLoads[category][bucket];
                combined.Counts[bucket] = 1;
            }
            foreach (TelemetrySignalTrack signal in signals)
                for (int bucket = 0; bucket < signal.Values.Length; bucket++)
                    if (signal.Counts[bucket] > 0) signal.Values[bucket] /= signal.Counts[bucket];
        }

        private static void BuildEvents(TelemetryAnalysisResult result, List<EffectMapTrack> tracks)
        {
            foreach (EffectMapTrack track in tracks)
            {
                foreach (EffectEpisode episode in track.Episodes)
                {
                    double midpoint = episode.StartSeconds + (episode.EndSeconds - episode.StartSeconds) / 2.0;
                    result.Events.Add(new FlightAnalysisEvent
                    {
                        StartSeconds = episode.StartSeconds,
                        EndSeconds = episode.EndSeconds,
                        Phase = PhaseAt(result.FlightPhases, midpoint),
                        Effect = track.Name,
                        Category = track.Category,
                        Frequency = track.Frequency,
                        Peak = episode.Peak,
                        Average = episode.Average
                    });
                }
            }
            result.Events.Sort((left, right) => left.StartSeconds.CompareTo(right.StartSeconds));
        }

        private static string PhaseAt(List<FlightPhaseSpan> phases, double seconds)
        {
            FlightPhaseSpan phase = phases.FirstOrDefault(item => seconds >= item.StartSeconds && seconds <= item.EndSeconds);
            return phase == null ? "Elapsed" : phase.Name;
        }

        private static void BuildDebriefFindings(TelemetryAnalysisResult result)
        {
            if (result.PeakConcurrency >= 3)
                result.DebriefFindings.Add(new FlightDebriefFinding { Severity = "Review", Text = result.PeakConcurrency + " effects overlap at " + TimeSpan.FromSeconds(result.PeakConcurrencyTimeSeconds).ToString(@"m\:ss") + "; review gain and priority headroom." });
            if (result.MaxTouchdown >= .70)
                result.DebriefFindings.Add(new FlightDebriefFinding { Severity = "Landing", Text = "High touchdown envelope recorded (" + (result.MaxTouchdown * 100).ToString("0") + "%). Review touchdown and ground-effect overlap." });
            if (result.MaxRotorDroop >= .45)
                result.DebriefFindings.Add(new FlightDebriefFinding { Severity = "Rotor", Text = "Rotor droop reached " + (result.MaxRotorDroop * 100).ToString("0") + "%. Confirm the rotor source before retuning." });
            if (result.EffectMap.All(track => track.Category != "Rotor and Powertrain" || track.Episodes.Count == 0))
                result.DebriefFindings.Add(new FlightDebriefFinding { Severity = "Coverage", Text = "No rotor/powertrain effect episode was modelled in this recording." });
            if (result.DebriefFindings.Count == 0)
                result.DebriefFindings.Add(new FlightDebriefFinding { Severity = "Info", Text = "No high-priority tuning review flag was found. Compare this run with a controlled baseline before changing gains." });
        }

        private static void FindBounds(string path, out double first, out double last)
        {
            first = double.MaxValue; last = double.MinValue;
            using (var reader = File.OpenText(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (ReadString(line, "\"dcsType\":\"").Length == 0) continue;
                    double time = ReadNumber(line, "sentAtMs");
                    if (first == double.MaxValue) first = time;
                    last = time;
                }
            }
            if (first == double.MaxValue) { first = 0; last = -1; }
        }

        private static List<EffectMapTrack> CreateTracks(int bucketCount)
        {
            var tracks = new List<EffectMapTrack>();
            foreach (object[] definition in EffectDefinitions)
                tracks.Add(new EffectMapTrack { Name = (string)definition[0], Category = CategoryFor((string)definition[0]), FrequencyHz = (double)definition[1], Frequency = ((double)definition[1]).ToString("0.#", CultureInfo.InvariantCulture) + " Hz", Buckets = new double[bucketCount] });
            return tracks;
        }

        private static void ApplyProfileFrequencyMetadata(TelemetryAnalysisResult result, List<EffectMapTrack> tracks)
        {
            Dictionary<string, ProfileFrequency> frequencies = LoadProfileFrequencies(result.AircraftFamily);
            foreach (EffectMapTrack track in tracks)
            {
                ProfileFrequency frequency;
                if (frequencies != null && frequencies.TryGetValue(track.Name, out frequency))
                {
                    track.FrequencyHz = frequency.IsDynamic ? 0 : frequency.Hz;
                    track.Frequency = frequency.IsDynamic ? "dynamic" : frequency.Hz.ToString("0.#", CultureInfo.InvariantCulture) + " Hz";
                }
                else if (frequencies != null)
                {
                    track.FrequencyHz = 0;
                    track.Frequency = "profile unavailable";
                }
            }
        }

        private static Dictionary<string, ProfileFrequency> LoadProfileFrequencies(string family)
        {
            string profilePath = FindProfilePath(family);
            if (profilePath == null) return null;

            try
            {
                var serializer = new JavaScriptSerializer();
                object root = serializer.DeserializeObject(File.ReadAllText(profilePath));
                var frequencies = new Dictionary<string, ProfileFrequency>(StringComparer.OrdinalIgnoreCase);
                ReadProfileContainers(root, frequencies);
                return frequencies;
            }
            catch
            {
                return null;
            }
        }

        private static void ReadProfileContainers(object value, Dictionary<string, ProfileFrequency> frequencies)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                object descriptionValue;
                object outputValue;
                if (dictionary.TryGetValue("Description", out descriptionValue)
                    && dictionary.TryGetValue("Output", out outputValue)
                    && descriptionValue is string)
                {
                    var output = outputValue as Dictionary<string, object>;
                    if (output != null)
                    {
                        object forceValue;
                        object formulaValue;
                        object frequencyValue;
                        bool dynamic = dictionary.TryGetValue("ForceFrequencies", out forceValue)
                            && forceValue is bool && (bool)forceValue
                            && dictionary.TryGetValue("FrontLeftFrequencyFormula", out formulaValue)
                            && formulaValue is Dictionary<string, object>
                            && !string.IsNullOrWhiteSpace(ReadStringValue((Dictionary<string, object>)formulaValue, "Expression"));
                        if (dynamic)
                        {
                            frequencies[(string)descriptionValue] = new ProfileFrequency { IsDynamic = true };
                        }
                        else if (output.TryGetValue("Frequency", out frequencyValue) && frequencyValue is IConvertible)
                        {
                            frequencies[(string)descriptionValue] = new ProfileFrequency { Hz = Convert.ToDouble(frequencyValue, CultureInfo.InvariantCulture) };
                        }
                    }
                }

                foreach (object child in dictionary.Values) ReadProfileContainers(child, frequencies);
                return;
            }

            var list = value as object[];
            if (list != null) foreach (object child in list) ReadProfileContainers(child, frequencies);
        }

        private static string ReadStringValue(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary.TryGetValue(key, out value) ? value as string : null;
        }

        private static string FindProfilePath(string family)
        {
            string folderName;
            string filePrefix;
            if (family == "UH60") { folderName = "uh60"; filePrefix = "DCS World - DCS UH60L"; }
            else if (family == "AH6") { folderName = "ah6"; filePrefix = "DCS World - AH-6J MH-6J"; }
            else if (family == "AH64") { folderName = "ah64"; filePrefix = "DCS World - DCS AH-64D"; }
            else if (family == "UH1") { folderName = "uh1"; filePrefix = "DCS World - DCS UH-1H"; }
            else return null;

            var roots = new List<string>();
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int depth = 0; current != null && depth < 8; depth++, current = current.Parent)
            {
                roots.Add(Path.Combine(current.FullName, "Profiles"));
                roots.Add(Path.Combine(current.FullName, "profiles"));
            }

            return roots.Select(root => Path.Combine(root, folderName))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, filePrefix + "*.siprofile", SearchOption.TopDirectoryOnly))
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private sealed class ProfileFrequency
        {
            public double Hz { get; set; }
            public bool IsDynamic { get; set; }
        }

        private static void AccumulateSummary(TelemetryAnalysisResult result, double[] v)
        {
            result.Rotor |= Value(v, "rotorDynamics") > 0; result.Ground |= Value(v, "gearStruts") > 0 || Value(v, "skids") > 0;
            result.Weapons |= Value(v, "weaponEffects") > 0; result.Cockpit |= Value(v, "cockpitStates") > 0;
            result.Brakes |= Value(v, "brakes") > 0; result.Doors |= Value(v, "doors") > 0;
            result.Touchdown |= Value(v, "touchdownPulse") > 0; result.Liftoff |= Value(v, "liftoffPulse") > 0;
            result.BrakeApplied |= Value(v, "wheelBrakesPosition") > .05; result.DoorMoved |= Value(v, "doorMovementIntensity") > .02;
            result.LightGun |= Value(v, "lightGunIntensity") > 0; result.HeavyGun |= Value(v, "heavyGunIntensity") > 0;
            result.Rocket |= Value(v, "rocketLaunchIntensity") > 0; result.Missile |= Value(v, "missileLaunchIntensity") > 0;
            result.Countermeasure |= Value(v, "chaffReleasePulse") > 0 || Value(v, "flareReleasePulse") > 0;
            result.Etl |= Value(v, "translationalLiftTransition") > .25;
            result.MaxTouchdown = Math.Max(result.MaxTouchdown, Value(v, "touchdownStrength"));
            result.MaxGroundRoll = Math.Max(result.MaxGroundRoll, Value(v, "groundRollIntensity"));
            result.MaxBraking = Math.Max(result.MaxBraking, Value(v, "brakingIntensity"));
            result.MaxRotorDroop = Math.Max(result.MaxRotorDroop, Value(v, "rotorDroopIntensity"));
            result.MaxImpact = Math.Max(result.MaxImpact, Value(v, "airframeImpactPulse"));
            result.MaxBladeSlap = Math.Max(result.MaxBladeSlap, Value(v, "bladeSlapIntensity"));
        }

        private static void AccumulateEffects(List<EffectMapTrack> tracks, Dictionary<string, EffectMapTrack> byName, int bucket, double[] v)
        {
            for (int i = 0; i < tracks.Count; i++) if (!UsesProfileCalculation(tracks[i].Name)) SetPeak(tracks[i], bucket, Clamp(Value(v, (string)EffectDefinitions[i][2])));
            bool airborne = Value(v, "onGround") < .5;
            double maneuver = Value(v, "maneuverResponseIntensity"), lateral = Value(v, "lateralManeuverIntensity"), control = Value(v, "controlResponseIntensity"), governor = Value(v, "engineGovernorResponseIntensity");
            double vertical = Clamp((Math.Abs(Value(v, "verticalVelocityMps")) - 1.5) / 8.5);
            double combined = 1 - (1 - Clamp(maneuver)) * (1 - Clamp(lateral) * .68) * (1 - Clamp(control) * .85) * (1 - Clamp(governor) * .45) * (1 - vertical * .40);
            SetNamedPeak(byName, "Maneuver load and vertical energy", bucket, airborne ? Math.Pow(Clamp(combined), .90) : 0);
            SetNamedPeak(byName, "Turbulence body thump", bucket, airborne ? Math.Pow(Clamp((Value(v, "turbulenceIntensity") - .12) / .45), .90) : 0);
            SetNamedPeak(byName, "Airframe buffet texture", bucket, airborne ? Math.Pow(Clamp((Value(v, "airframeBuffetIntensity") - .22) / .38), 1.20) : 0);
            SetNamedPeak(byName, "Engine governor load response", bucket, Clamp((governor - .045) / .955));
            SetNamedPeak(byName, "Rotor spool transition", bucket, Math.Pow(Clamp((Value(v, "rotorSpoolIntensity") - .10) / .35), .85));
            double groundMotion = 26 * Math.Pow(Clamp((Value(v, "groundResonanceIntensity") - .08) / .28), .90);
            double governedGroundRotor = Clamp((Clamp(Value(v, "mainRotorRpmPct") / 100.0) - .70) / .25);
            governedGroundRotor = governedGroundRotor * governedGroundRotor * (3 - 2 * governedGroundRotor);
            double supportedWeight = Clamp(Value(v, "weightOnGroundIntensity") / .20);
            double rotorTransfer = Value(v, "rotorRunning") >= .5
                ? governedGroundRotor * (6 + 10 * Math.Sqrt(supportedWeight)) : 0;
            SetNamedPeak(byName, "Gear-borne rotor vibration", bucket,
                airborne ? 0 : Clamp(Math.Max(groundMotion, rotorTransfer) / 30.0));
            SetNamedPeak(byName, "Ground support rocking", bucket, Math.Pow(Math.Max(Clamp(Value(v, "groundPitchRockIntensity")), Clamp(Value(v, "groundRollRockIntensity"))), .80));
            SetNamedPeak(byName, "Wheel braking load", bucket, Math.Pow(Math.Max(Clamp(Value(v, "brakingIntensity")), Clamp(Value(v, "brakeDecelerationIntensity")) * .75), .72));
            double nr = Clamp((Clamp(Value(v, "mainRotorRpmPct") / 100.0) - .15) / .80); nr = nr * nr * (3 - 2 * nr);
            SetNamedPeak(byName, "Main rotor blade-pass 4-rev", bucket, Clamp((3 + 11 * nr + 22 * Clamp(Value(v, "rotorLoadIntensity")) + 7 * Clamp(maneuver) + 4 * Clamp(lateral) + 4 * Clamp(control) + 2 * vertical) / 54.0));
            SetNamedPeak(byName, "Blade slap (descent BVI cue)", bucket, airborne ? Math.Pow(Clamp(Value(v, "bladeSlapIntensity")), .92) : 0);
        }

        private static void BuildEpisodes(EffectMapTrack track, double bucketSeconds)
        {
            const double threshold = .02;
            int start = -1, lastActive = -1, count = 0, totalActive = 0;
            double peak = 0, allActiveTotal = 0;
            for (int i = 0; i <= track.Buckets.Length; i++)
            {
                bool active = i < track.Buckets.Length && track.Buckets[i] >= threshold;
                if (active) { if (start < 0) start = i; lastActive = i; count++; allActiveTotal += track.Buckets[i]; totalActive++; peak = Math.Max(peak, track.Buckets[i]); }
                else if (start >= 0 && (i > lastActive + 1 || i == track.Buckets.Length))
                {
                    double episodeTotal = 0; for (int b = start; b <= lastActive; b++) if (track.Buckets[b] >= threshold) episodeTotal += track.Buckets[b];
                    track.Episodes.Add(new EffectEpisode { StartSeconds = start * bucketSeconds, EndSeconds = Math.Min(track.Buckets.Length * bucketSeconds, (lastActive + 1) * bucketSeconds), Peak = peak, Average = count == 0 ? 0 : episodeTotal / count });
                    start = -1; lastActive = -1; count = 0; peak = 0;
                }
            }
            track.ActiveSeconds = totalActive * bucketSeconds;
            track.AverageActiveIntensity = totalActive == 0 ? 0 : allActiveTotal / totalActive;
        }

        private static void BuildFlightPhases(TelemetryAnalysisResult result, PhaseBucketObservation[] observations, int firstAir, int lastAir, double duration)
        {
            string[] labels = new string[observations.Length];
            double safeDuration = Math.Max(.001, duration);
            int takeoff = Math.Max(1, (int)(20.0 / safeDuration * observations.Length)), approach = Math.Max(1, (int)(30.0 / safeDuration * observations.Length));
            PhaseBucketObservation previous = null;
            for (int b = 0; b < observations.Length; b++)
            {
                PhaseBucketObservation s = observations[b]; if (!s.Seen && previous != null) s = previous; if (s.Seen) previous = s;
                if (!s.Seen) { labels[b] = "Elapsed"; continue; }
                if (s.WeaponActivity > .02) labels[b] = "Combat";
                else if (s.Airborne) labels[b] = firstAir >= 0 && b - firstAir <= takeoff ? "Takeoff" : lastAir >= 0 && lastAir - b <= approach ? "Approach" : "Flight";
                else if (lastAir >= 0 && b > lastAir && b - lastAir <= takeoff) labels[b] = "Landing";
                else if (s.GroundMotion > .03) labels[b] = "Taxi";
                else if (s.MachineryRunning) labels[b] = lastAir >= 0 && b > lastAir ? "Shutdown" : "Startup";
                else labels[b] = "Ground";
            }
            int combatGap = Math.Max(1, (int)(60.0 / safeDuration * observations.Length)), priorCombat = -1;
            for (int i = 0; i < labels.Length; i++) if (labels[i] == "Combat") { if (priorCombat >= 0 && i - priorCombat <= combatGap) for (int fill = priorCombat; fill <= i; fill++) if (labels[fill] != "Takeoff" && labels[fill] != "Approach") labels[fill] = "Combat"; priorCombat = i; }
            string current = labels[0]; int start = 0;
            for (int i = 1; i <= labels.Length; i++) if (i == labels.Length || labels[i] != current) { result.FlightPhases.Add(new FlightPhaseSpan { Name = current, StartSeconds = start * duration / labels.Length, EndSeconds = i * duration / labels.Length }); if (i < labels.Length) { start = i; current = labels[i]; } }
            MergeShortPhases(result.FlightPhases, 8.0);
        }

        private static void BuildLoadMetrics(TelemetryAnalysisResult result, List<EffectMapTrack> tracks, int buckets, double bucketSeconds)
        {
            result.CategoryLoads = new double[TelemetryAnalysisCategories.All.Length][];
            for (int c = 0; c < result.CategoryLoads.Length; c++) result.CategoryLoads[c] = new double[buckets];
            result.ActiveEffectCounts = new int[buckets];
            for (int b = 0; b < buckets; b++)
            {
                int active = 0;
                foreach (EffectMapTrack track in tracks)
                {
                    double value = track.Buckets[b]; if (value >= .02) active++;
                    int category = Array.IndexOf(TelemetryAnalysisCategories.All, track.Category); if (category >= 0) result.CategoryLoads[category][b] += value;
                }
                result.ActiveEffectCounts[b] = active;
                if (active > result.PeakConcurrency) { result.PeakConcurrency = active; result.PeakConcurrencyTimeSeconds = (b + .5) * bucketSeconds; }
            }
        }

        private static void MergeShortPhases(List<FlightPhaseSpan> phases, double minimumSeconds)
        {
            for (int i = phases.Count - 1; i >= 0; i--) { FlightPhaseSpan phase = phases[i]; if (phase.EndSeconds - phase.StartSeconds >= minimumSeconds || phases.Count == 1) continue; if (i > 0) { phases[i - 1].EndSeconds = phase.EndSeconds; phases.RemoveAt(i); } else { phases[1].StartSeconds = phase.StartSeconds; phases.RemoveAt(0); } }
        }

        private static string CategoryFor(string name)
        {
            if (Contains(name, "gun") || Contains(name, "rocket") || Contains(name, "missile") || Contains(name, "flare") || Contains(name, "chaff")) return "Weapons and Countermeasures";
            if (Contains(name, "touchdown") || Contains(name, "wheel") || Contains(name, "ground") || Contains(name, "gear-borne") || Contains(name, "suspension") || Contains(name, "tailwheel") || Contains(name, "liftoff") || Contains(name, "braking")) return "Ground Interaction";
            if (Contains(name, "rotor") || Contains(name, "engine") || Contains(name, "transmission") || Contains(name, "collective") || Contains(name, "APU")) return "Rotor and Powertrain";
            return "Aerodynamics";
        }
        private static bool Contains(string value, string part) { return value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static readonly string[] EffectValueNames = {
            "apuRunning", "engine1StarterActive", "engine2StarterActive", "rotorSpoolIntensity", "rotorLoadIntensity", "bladeSlapIntensity", "transmissionLoadIntensity", "engineGovernorResponseIntensity", "collectiveMovementRate", "rotorDroopIntensity", "maneuverResponseIntensity", "translationalLiftTransition", "turbulenceIntensity", "airframeBuffetIntensity", "indicatedAirSpeedMps", "settlingWithPowerCue", "hardManeuverPulse", "airframeImpactPulse", "touchdownEnvelope", "leftWheelContactPulse", "rightWheelContactPulse", "tailWheelContactPulse", "liftoffPulse", "groundRollIntensity", "groundResonanceIntensity", "suspensionMovementIntensity", "groundPitchRockIntensity", "groundRollRockIntensity", "wheelShimmyIntensity", "tailWheelCasterIntensity", "brakingIntensity", "brakeDecelerationIntensity", "lightGunIntensity", "heavyGunIntensity", "rocketLaunchIntensity", "missileLaunchIntensity", "flareReleasePulse", "chaffReleasePulse"
        };
        private static readonly object[][] EffectDefinitions = {
            D("APU machinery",55,"apuRunning"), D("Engine starters",48,"engine1StarterActive"), D("Rotor spool transition",18,"rotorSpoolIntensity"),
            D("Rotor 1-rev airframe pulse",4.3,"rotorLoadIntensity"), D("Rotor 2-rev structural harmonic",8.6,"rotorLoadIntensity"), D("Main rotor blade-pass 4-rev",17.2,"rotorLoadIntensity"),
            D("Blade slap (descent BVI cue)",17.2,"bladeSlapIntensity"), D("Transmission load",22,"transmissionLoadIntensity"), D("Engine governor load response",22,"engineGovernorResponseIntensity"),
            D("Collective movement thump",22,"collectiveMovementRate"), D("Low rotor droop roughness",15,"rotorDroopIntensity"), D("Maneuver load and vertical energy",18,"maneuverResponseIntensity"),
            D("Translational lift transition",13,"translationalLiftTransition"), D("Turbulence body thump",16,"turbulenceIntensity"), D("Airframe buffet texture",22,"airframeBuffetIntensity"),
            D("High-speed airframe texture",30,"indicatedAirSpeedMps"), D("Powered low-speed descent cue",12,"settlingWithPowerCue"), D("Hard maneuver pulse",18,"hardManeuverPulse"), D("Airframe impact pulse",18,"airframeImpactPulse"),
            D("Touchdown body impact",22,"touchdownEnvelope"), D("Main wheel contact detail",30,"leftWheelContactPulse"), D("Tailwheel settle tick",22,"tailWheelContactPulse"), D("Liftoff unloading",14,"liftoffPulse"),
            D("Ground roll base",24,"groundRollIntensity"), D("Gear-borne rotor vibration",13,"groundResonanceIntensity"), D("Ground support rocking",16,"groundPitchRockIntensity"), D("Suspension surface roughness",23,"suspensionMovementIntensity"), D("Wheel shimmy",38,"wheelShimmyIntensity"), D("Tailwheel caster",17,"tailWheelCasterIntensity"), D("Wheel braking load",22,"brakingIntensity"),
            D("M134 and light-gun sustained fire",50,"lightGunIntensity"), D("M134 low body harmonic",25,"lightGunIntensity"), D("Weapon-specific heavy gun rhythm",21,"heavyGunIntensity"), D("M230 individual-shot body thump",10.4,"heavyGunIntensity"), D("GAU-19 recoil harmonic",43.4,"heavyGunIntensity"),
            D("Rocket launch kick",32,"rocketLaunchIntensity"), D("Rocket launch body reaction",18,"rocketLaunchIntensity"), D("Weapon-specific missile body",20,"missileLaunchIntensity"), D("Missile motor ignition",28,"missileLaunchIntensity"), D("Flare dispenser pulse",45,"flareReleasePulse"), D("Chaff dispenser pulse",35,"chaffReleasePulse")
        };
        private static object[] D(string name, double frequency, string key) { return new object[] { name, frequency, key }; }
        private static bool UsesProfileCalculation(string name) { return name == "Rotor spool transition" || name == "Main rotor blade-pass 4-rev" || name == "Blade slap (descent BVI cue)" || name == "Maneuver load and vertical energy" || name == "Turbulence body thump" || name == "Airframe buffet texture" || name == "Engine governor load response" || name == "Gear-borne rotor vibration" || name == "Ground support rocking" || name == "Wheel braking load"; }

        private static readonly string[] ScalarNames = BuildScalarNames();
        private static readonly Dictionary<int, int> ScalarHashes = BuildScalarHashes();
        private static readonly Dictionary<string, int> ScalarIndexes = BuildScalarIndexes();
        private static string[] BuildScalarNames()
        {
            var names = new List<string>(EffectValueNames);
            names.AddRange(new[] { "sentAtMs", "onGround", "verticalVelocityMps", "lateralManeuverIntensity", "controlResponseIntensity", "mainRotorRpmPct", "rotorRunning", "weightOnGroundIntensity", "wheelBrakesPosition", "doorMovementIntensity", "touchdownPulse", "touchdownStrength", "rotorDynamics", "gearStruts", "skids", "weaponEffects", "cockpitStates", "brakes", "doors" });
            var unique = new List<string>(); foreach (string name in names) if (!unique.Contains(name)) unique.Add(name); return unique.ToArray();
        }
        private static Dictionary<string, int> BuildScalarIndexes() { var map = new Dictionary<string, int>(StringComparer.Ordinal); for (int i = 0; i < ScalarNames.Length; i++) map[ScalarNames[i]] = i; return map; }
        private static Dictionary<int, int> BuildScalarHashes() { var map = new Dictionary<int, int>(); for (int i = 0; i < ScalarNames.Length; i++) { int hash = Hash(ScalarNames[i], 0, ScalarNames[i].Length); if (map.ContainsKey(hash)) throw new InvalidOperationException("Telemetry scalar hash collision."); map[hash] = i; } return map; }
        private static double Value(double[] values, string key) { int index; return ScalarIndexes.TryGetValue(key, out index) ? values[index] : 0; }
        private static void SetNamedPeak(Dictionary<string, EffectMapTrack> tracks, string name, int bucket, double value) { EffectMapTrack track; if (tracks.TryGetValue(name, out track)) SetPeak(track, bucket, value); }
        private static void SetPeak(EffectMapTrack track, int bucket, double value) { double clamped = Clamp(value); track.Buckets[bucket] = Math.Max(track.Buckets[bucket], clamped); track.PeakIntensity = Math.Max(track.PeakIntensity, clamped); }
        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static long TicksToMilliseconds(long ticks) { return (long)Math.Round(ticks * 1000.0 / Stopwatch.Frequency); }

        private static void ScanScalars(string json, double[] values, out string aircraft)
        {
            aircraft = ""; int i = 0;
            while (i < json.Length)
            {
                if (json[i] != '"') { i++; continue; }
                int keyStart = ++i; while (i < json.Length && json[i] != '"') i++; if (i >= json.Length) return;
                int keyLength = i - keyStart; i++; while (i < json.Length && char.IsWhiteSpace(json[i])) i++; if (i >= json.Length || json[i] != ':') continue;
                i++; while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (keyLength == 7 && Matches(json, keyStart, keyLength, "dcsType")) { if (i < json.Length && json[i] == '"') { int start = ++i; while (i < json.Length && json[i] != '"') i++; aircraft = i > start ? json.Substring(start, i - start) : ""; if (i < json.Length) i++; } continue; }
                int scalarIndex; int hash = Hash(json, keyStart, keyLength); if (!ScalarHashes.TryGetValue(hash, out scalarIndex) || !Matches(json, keyStart, keyLength, ScalarNames[scalarIndex])) { SkipScalarValue(json, ref i); continue; }
                if (i + 4 <= json.Length && string.CompareOrdinal(json, i, "true", 0, 4) == 0) { values[scalarIndex] = 1; i += 4; }
                else if (i < json.Length && (json[i] == '-' || json[i] == '+' || (json[i] >= '0' && json[i] <= '9'))) values[scalarIndex] = ParseNumber(json, ref i);
            }
        }
        private static void SkipScalarValue(string text, ref int index)
        {
            if (index >= text.Length || text[index] == '{' || text[index] == '[') return;
            if (text[index] == '"') { index++; while (index < text.Length) { if (text[index] == '\\') { index += 2; continue; } if (text[index++] == '"') break; } return; }
            while (index < text.Length && text[index] != ',' && text[index] != '}' && text[index] != ']') index++;
        }
        private static double ParseNumber(string text, ref int index)
        {
            int i = index; bool negative = false; if (text[i] == '-' || text[i] == '+') { negative = text[i] == '-'; i++; }
            double value = 0; while (i < text.Length && text[i] >= '0' && text[i] <= '9') { value = value * 10 + text[i++] - '0'; }
            if (i < text.Length && text[i] == '.') { i++; double scale = .1; while (i < text.Length && text[i] >= '0' && text[i] <= '9') { value += (text[i++] - '0') * scale; scale *= .1; } }
            if (i < text.Length && (text[i] == 'e' || text[i] == 'E')) { i++; bool expNegative = false; if (i < text.Length && (text[i] == '-' || text[i] == '+')) { expNegative = text[i] == '-'; i++; } int exponent = 0; while (i < text.Length && text[i] >= '0' && text[i] <= '9') exponent = exponent * 10 + text[i++] - '0'; value *= Math.Pow(10, expNegative ? -exponent : exponent); }
            index = i; return negative ? -value : value;
        }
        private static int Hash(string value, int start, int length) { unchecked { int hash = (int)2166136261; for (int i = 0; i < length; i++) hash = (hash ^ value[start + i]) * 16777619; return hash; } }
        private static bool Matches(string value, int start, int length, string expected) { if (length != expected.Length) return false; for (int i = 0; i < length; i++) if (value[start + i] != expected[i]) return false; return true; }
        private static string Family(string type) { if (type.StartsWith("UH-60", StringComparison.OrdinalIgnoreCase)) return "UH60"; if (type.StartsWith("AH-6", StringComparison.OrdinalIgnoreCase) || type.StartsWith("MH-6", StringComparison.OrdinalIgnoreCase)) return "AH6"; if (type.StartsWith("AH-64", StringComparison.OrdinalIgnoreCase)) return "AH64"; if (type.StartsWith("UH-1", StringComparison.OrdinalIgnoreCase)) return "UH1"; return "OTHER"; }
        private static string ReadString(string line, string marker) { int start = line.IndexOf(marker, StringComparison.Ordinal); if (start < 0) return ""; start += marker.Length; int end = line.IndexOf('"', start); return end > start ? line.Substring(start, end - start) : ""; }
        private static double ReadNumber(string line, string name) { string marker = "\"" + name + "\":"; int start = line.IndexOf(marker, StringComparison.Ordinal); if (start < 0) return 0; start += marker.Length; return ParseNumber(line, ref start); }
    }
}

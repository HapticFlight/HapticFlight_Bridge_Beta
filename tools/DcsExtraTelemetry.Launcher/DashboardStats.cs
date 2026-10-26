using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DcsExtraTelemetry.Launcher
{
    internal enum DashboardPeriod
    {
        Week,
        Month,
        AllTime
    }

    // What the dashboard needs from one recording. Built once per file and cached, so
    // switching periods or returning to the dashboard never re-parses a recording.
    internal sealed class FlightSummary
    {
        public string Path { get; set; }
        public string Aircraft { get; set; }
        public DateTime RecordedAt { get; set; }
        public double DurationSeconds { get; set; }
        public int EffectsFired { get; set; }
        public int PeakConcurrency { get; set; }
        public Dictionary<string, int> EpisodesByCategory { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        // Shaker intensity per interval; IntervalSeconds adapts to the flight length.
        public double IntervalSeconds { get; set; } = 60;
        public double[] IntensitySeries { get; set; } = new double[0];
        public string[] StrongestEffects { get; set; } = new string[0];
    }

    internal sealed class DashboardSnapshot
    {
        public DashboardPeriod Period { get; set; }
        public int FlightCount { get; set; }
        public int PreviousFlightCount { get; set; }
        public double FlightSeconds { get; set; }
        public double PreviousFlightSeconds { get; set; }
        public int EffectsFired { get; set; }
        public int PreviousEffectsFired { get; set; }
        public string FlightTimeChartTitle { get; set; }
        public double[] FlightMinutesSeries { get; set; } = new double[0];
        public string[] FlightMinutesLabels { get; set; } = new string[0];
        public List<DonutSlice> CategoryShares { get; } = new List<DonutSlice>();
        public FlightSummary LastFlight { get; set; }
        public List<FlightSummary> RecentFlights { get; } = new List<FlightSummary>();
        public List<AircraftSummary> Aircraft { get; } = new List<AircraftSummary>();
    }

    internal sealed class AircraftSummary
    {
        public string Aircraft { get; set; }
        public int FlightCount { get; set; }
        public DateTime LastFlown { get; set; }
    }

    internal static class DashboardStats
    {
        // Category order and palette keys shared by the donut and its legend.
        public static readonly string[][] Categories =
        {
            new[] { "Rotor and Powertrain", "Rotor", "ChartCategoryRotor" },
            new[] { "Weapons and Countermeasures", "Weapons", "ChartCategoryWeapons" },
            new[] { "Ground Interaction", "Ground", "ChartCategoryGround" },
            new[] { "Aerodynamics", "Airframe", "ChartCategoryAirframe" }
        };

        private const int MaximumRecordings = 60;
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, FlightSummary> Cache = new Dictionary<string, FlightSummary>(StringComparer.OrdinalIgnoreCase);
        // Serialises builds so two refreshes never analyse the same large recording at once.
        private static readonly object BuildLock = new object();
        private static bool diskCacheLoaded;
        private static bool diskCacheDirty;

        public static DashboardSnapshot Build(IEnumerable<FileInfo> recordings, DashboardPeriod period, DateTime now)
        {
            lock (BuildLock) return BuildLocked(recordings, period, now);
        }

        private static DashboardSnapshot BuildLocked(IEnumerable<FileInfo> recordings, DashboardPeriod period, DateTime now)
        {
            LoadDiskCache();
            List<FlightSummary> flights = recordings
                .Where(file => file != null && file.Exists && file.Length > 0)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(MaximumRecordings)
                .Select(Summarize)
                .Where(summary => summary != null)
                .ToList();
            SaveDiskCache();

            var snapshot = new DashboardSnapshot { Period = period, LastFlight = flights.FirstOrDefault() };
            snapshot.RecentFlights.AddRange(flights.Take(5));
            DateTime today = now.Date;
            DateTime periodStart;
            DateTime previousStart;
            if (period == DashboardPeriod.Week)
            {
                periodStart = today.AddDays(-6);
                previousStart = periodStart.AddDays(-7);
            }
            else if (period == DashboardPeriod.Month)
            {
                periodStart = today.AddDays(-29);
                previousStart = periodStart.AddDays(-30);
            }
            else
            {
                periodStart = DateTime.MinValue;
                previousStart = DateTime.MinValue;
            }

            List<FlightSummary> current = flights.Where(flight => flight.RecordedAt >= periodStart).ToList();
            List<FlightSummary> previous = period == DashboardPeriod.AllTime
                ? new List<FlightSummary>()
                : flights.Where(flight => flight.RecordedAt >= previousStart && flight.RecordedAt < periodStart).ToList();

            snapshot.FlightCount = current.Count;
            snapshot.PreviousFlightCount = previous.Count;
            snapshot.FlightSeconds = current.Sum(flight => flight.DurationSeconds);
            snapshot.PreviousFlightSeconds = previous.Sum(flight => flight.DurationSeconds);
            snapshot.EffectsFired = current.Sum(flight => flight.EffectsFired);
            snapshot.PreviousEffectsFired = previous.Sum(flight => flight.EffectsFired);

            BuildFlightTimeSeries(snapshot, current, period, today);

            foreach (string[] category in Categories)
            {
                snapshot.CategoryShares.Add(new DonutSlice
                {
                    Label = category[1],
                    BrushKey = category[2],
                    Value = current.Sum(flight =>
                    {
                        int episodes;
                        return flight.EpisodesByCategory.TryGetValue(category[0], out episodes) ? episodes : 0;
                    })
                });
            }

            snapshot.Aircraft.AddRange(flights
                .GroupBy(flight => flight.Aircraft, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AircraftSummary
                {
                    Aircraft = group.First().Aircraft,
                    FlightCount = group.Count(),
                    LastFlown = group.Max(flight => flight.RecordedAt)
                })
                .OrderByDescending(aircraft => aircraft.LastFlown));
            return snapshot;
        }

        private static void BuildFlightTimeSeries(DashboardSnapshot snapshot, List<FlightSummary> flights, DashboardPeriod period, DateTime today)
        {
            if (period == DashboardPeriod.AllTime)
            {
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var values = new double[12];
                var labels = new string[12];
                for (int index = 0; index < 12; index++)
                {
                    DateTime month = monthStart.AddMonths(index - 11);
                    labels[index] = month.ToString("MMM", CultureInfo.CurrentCulture);
                    values[index] = flights
                        .Where(flight => flight.RecordedAt.Year == month.Year && flight.RecordedAt.Month == month.Month)
                        .Sum(flight => flight.DurationSeconds) / 60.0;
                }
                snapshot.FlightTimeChartTitle = "Flight time per month";
                snapshot.FlightMinutesSeries = values;
                snapshot.FlightMinutesLabels = labels;
                return;
            }

            int days = period == DashboardPeriod.Week ? 7 : 30;
            var dayValues = new double[days];
            var dayLabels = new string[days];
            for (int index = 0; index < days; index++)
            {
                DateTime day = today.AddDays(index - (days - 1));
                dayValues[index] = flights.Where(flight => flight.RecordedAt.Date == day).Sum(flight => flight.DurationSeconds) / 60.0;
                if (period == DashboardPeriod.Week)
                    dayLabels[index] = day == today ? "Today" : day.ToString("ddd", CultureInfo.CurrentCulture);
                else
                    dayLabels[index] = (days - 1 - index) % 5 == 0 ? day.ToString("d MMM", CultureInfo.CurrentCulture) : string.Empty;
            }
            snapshot.FlightTimeChartTitle = "Flight time per day";
            snapshot.FlightMinutesSeries = dayValues;
            snapshot.FlightMinutesLabels = dayLabels;
        }

        private static string CacheKey(FileInfo file)
        {
            return file.FullName + "|" + file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                + "|" + file.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static FlightSummary Summarize(FileInfo file)
        {
            string key = CacheKey(file);
            lock (CacheLock)
            {
                FlightSummary cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }

            FlightSummary summary;
            try
            {
                summary = FromAnalysis(file, TelemetryRecordingAnalyzer.Analyze(file.FullName));
            }
            catch
            {
                // A recording that cannot be parsed simply does not count toward the stats.
                summary = null;
            }

            lock (CacheLock)
            {
                Cache[key] = summary;
                diskCacheDirty = true;
            }
            return summary;
        }

        // Also used when a recording has just been analysed for the Flights page, so the
        // dashboard picks it up without parsing the file a second time.
        public static void Remember(string path, TelemetryAnalysisResult result)
        {
            if (string.IsNullOrWhiteSpace(path) || result == null || !File.Exists(path)) return;
            var file = new FileInfo(path);
            FlightSummary summary = FromAnalysis(file, result);
            lock (CacheLock)
            {
                Cache[CacheKey(file)] = summary;
                diskCacheDirty = true;
            }
        }

        // Recordings can be hundreds of megabytes, so summaries are kept on disk and each
        // file is analysed once. The cache is disposable: a missing or unreadable file only
        // means the next dashboard refresh re-analyses the recordings.
        private sealed class CachedSummary
        {
            public string Key { get; set; }
            public bool Unreadable { get; set; }
            public string Path { get; set; }
            public string Aircraft { get; set; }
            public DateTime RecordedAt { get; set; }
            public double DurationSeconds { get; set; }
            public int EffectsFired { get; set; }
            public int PeakConcurrency { get; set; }
            public Dictionary<string, int> EpisodesByCategory { get; set; }
            public double IntervalSeconds { get; set; }
            public double[] IntensitySeries { get; set; }
            public string[] StrongestEffects { get; set; }
        }

        private static string DiskCachePath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DcsExtraTelemetry", "dashboard-summaries.json");
        }

        private static void LoadDiskCache()
        {
            lock (CacheLock)
            {
                if (diskCacheLoaded) return;
                diskCacheLoaded = true;
                try
                {
                    string cachePath = DiskCachePath();
                    if (!File.Exists(cachePath)) return;
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    List<CachedSummary> entries = serializer.Deserialize<List<CachedSummary>>(File.ReadAllText(cachePath));
                    foreach (CachedSummary entry in entries ?? new List<CachedSummary>())
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key) || Cache.ContainsKey(entry.Key)) continue;
                        if (entry.Unreadable)
                        {
                            Cache[entry.Key] = null;
                            continue;
                        }
                        var summary = new FlightSummary
                        {
                            Path = entry.Path,
                            Aircraft = DisplayAircraft(entry.Aircraft),
                            RecordedAt = entry.RecordedAt.ToLocalTime(),
                            DurationSeconds = entry.DurationSeconds,
                            EffectsFired = entry.EffectsFired,
                            PeakConcurrency = entry.PeakConcurrency,
                            IntervalSeconds = entry.IntervalSeconds > 0 ? entry.IntervalSeconds : 60,
                            IntensitySeries = entry.IntensitySeries ?? new double[0],
                            StrongestEffects = entry.StrongestEffects ?? new string[0]
                        };
                        if (entry.EpisodesByCategory != null)
                            foreach (KeyValuePair<string, int> category in entry.EpisodesByCategory) summary.EpisodesByCategory[category.Key] = category.Value;
                        Cache[entry.Key] = summary;
                    }
                }
                catch
                {
                    // Start from an empty cache; summaries are rebuilt from the recordings.
                }
            }
        }

        private static void SaveDiskCache()
        {
            List<CachedSummary> entries;
            lock (CacheLock)
            {
                if (!diskCacheDirty) return;
                diskCacheDirty = false;
                entries = Cache.Select(pair => pair.Value == null
                    ? new CachedSummary { Key = pair.Key, Unreadable = true }
                    : new CachedSummary
                    {
                        Key = pair.Key,
                        Path = pair.Value.Path,
                        Aircraft = pair.Value.Aircraft,
                        RecordedAt = pair.Value.RecordedAt,
                        DurationSeconds = pair.Value.DurationSeconds,
                        EffectsFired = pair.Value.EffectsFired,
                        PeakConcurrency = pair.Value.PeakConcurrency,
                        EpisodesByCategory = new Dictionary<string, int>(pair.Value.EpisodesByCategory),
                        IntervalSeconds = pair.Value.IntervalSeconds,
                        IntensitySeries = pair.Value.IntensitySeries,
                        StrongestEffects = pair.Value.StrongestEffects
                    }).ToList();
            }
            try
            {
                string cachePath = DiskCachePath();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cachePath));
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string temporary = cachePath + ".tmp";
                File.WriteAllText(temporary, serializer.Serialize(entries));
                if (File.Exists(cachePath)) File.Delete(cachePath);
                File.Move(temporary, cachePath);
            }
            catch
            {
                // Caching is an optimisation only.
            }
        }

        private static FlightSummary FromAnalysis(FileInfo file, TelemetryAnalysisResult result)
        {
            if (result == null || result.ValidSamples == 0 || result.EffectMapDurationSeconds <= 0) return null;
            var summary = new FlightSummary
            {
                Path = file.FullName,
                Aircraft = DisplayAircraft(result.AircraftType),
                RecordedAt = file.LastWriteTime,
                DurationSeconds = result.EffectMapDurationSeconds,
                EffectsFired = result.EffectMap.Sum(track => track.Episodes.Count),
                PeakConcurrency = result.PeakConcurrency
            };
            foreach (EffectMapTrack track in result.EffectMap)
            {
                if (track.Episodes.Count == 0 || string.IsNullOrWhiteSpace(track.Category)) continue;
                int existing;
                summary.EpisodesByCategory.TryGetValue(track.Category, out existing);
                summary.EpisodesByCategory[track.Category] = existing + track.Episodes.Count;
            }

            double bucketSeconds = result.AnalysisBucketSeconds > 0
                ? result.AnalysisBucketSeconds
                : result.EffectMapDurationSeconds / Math.Max(1, result.ActiveEffectCounts == null ? 1 : result.ActiveEffectCounts.Length);
            // About 40 bars whatever the length: 5 s bars for a short hop, minutes for a long sortie.
            double interval = new[] { 5.0, 10, 15, 30, 60, 120, 300, 600 }.FirstOrDefault(step => step >= result.EffectMapDurationSeconds / 40.0);
            if (interval <= 0) interval = 600;
            summary.IntervalSeconds = interval;
            int minutes = Math.Max(1, (int)Math.Ceiling(result.EffectMapDurationSeconds / interval));
            var intensity = new double[minutes];
            var samples = new int[minutes];
            if (result.CategoryLoads != null && result.CategoryLoads.Length > 0)
            {
                int bucketCount = result.CategoryLoads.Max(loads => loads == null ? 0 : loads.Length);
                for (int bucket = 0; bucket < bucketCount; bucket++)
                {
                    double total = 0;
                    foreach (double[] loads in result.CategoryLoads)
                        if (loads != null && bucket < loads.Length) total += loads[bucket];
                    int minute = Math.Min(minutes - 1, (int)(bucket * bucketSeconds / interval));
                    intensity[minute] += total;
                    samples[minute]++;
                }
                for (int minute = 0; minute < minutes; minute++)
                    if (samples[minute] > 0) intensity[minute] /= samples[minute];
            }
            summary.IntensitySeries = intensity;

            var strongest = new string[minutes];
            var strongestValue = new double[minutes];
            foreach (EffectMapTrack track in result.EffectMap)
            {
                if (track.Buckets == null) continue;
                for (int bucket = 0; bucket < track.Buckets.Length; bucket++)
                {
                    int minute = Math.Min(minutes - 1, (int)(bucket * bucketSeconds / interval));
                    if (track.Buckets[bucket] > strongestValue[minute])
                    {
                        strongestValue[minute] = track.Buckets[bucket];
                        strongest[minute] = track.Name;
                    }
                }
            }
            summary.StrongestEffects = strongest;
            return summary;
        }

        // DCS type names use underscores for variants ("UH-60L_DAP"); show them as words.
        public static string DisplayAircraft(string aircraftType)
        {
            return string.IsNullOrWhiteSpace(aircraftType) ? "Unknown aircraft" : aircraftType.Replace('_', ' ').Trim();
        }

        // The aircraft a recording was analysed as, when it is already in the summary cache.
        public static string CachedAircraft(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            LoadDiskCache();
            lock (CacheLock)
            {
                string prefix = path + "|";
                FlightSummary summary = Cache.Where(pair => pair.Value != null && pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Value).FirstOrDefault();
                return summary == null || summary.Aircraft == "Unknown aircraft" ? null : summary.Aircraft;
            }
        }

        // "+3", "+12%", "-48m" style deltas. Returns null when there is nothing to compare.
        public static string FormatCountDelta(int current, int previous)
        {
            int delta = current - previous;
            if (delta == 0) return current == 0 ? null : "same";
            return (delta > 0 ? "+" : "") + delta.ToString(CultureInfo.CurrentCulture);
        }

        public static string FormatPercentDelta(double current, double previous)
        {
            if (previous <= 0) return current > 0 ? "new" : null;
            double percent = (current - previous) / previous * 100;
            if (Math.Abs(percent) < .5) return "same";
            return (percent > 0 ? "+" : "") + percent.ToString("0", CultureInfo.CurrentCulture) + "%";
        }

        public static string FormatDurationDelta(double currentSeconds, double previousSeconds)
        {
            double delta = currentSeconds - previousSeconds;
            if (Math.Abs(delta) < 60) return currentSeconds <= 0 ? null : "same";
            return (delta > 0 ? "+" : "-") + FormatDuration(Math.Abs(delta));
        }

        public static string FormatDuration(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (span.TotalHours >= 1) return (int)span.TotalHours + "h " + span.Minutes.ToString("00", CultureInfo.InvariantCulture) + "m";
            return span.Minutes + "m";
        }

        public static string FormatClock(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + span.ToString("mm\\:ss", CultureInfo.InvariantCulture)
                : ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + ":" + span.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        // Short code for an aircraft tile, e.g. "UH-60L" -> "60", "AH-64D_BLK_II" -> "64", "UH-1H" -> "1H".
        public static string TileCode(string aircraft)
        {
            if (string.IsNullOrWhiteSpace(aircraft) || aircraft.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0) return "?";
            string name = aircraft.Split('_', ' ')[0];
            int dash = name.IndexOf('-');
            string tail = dash >= 0 && dash + 1 < name.Length ? name.Substring(dash + 1) : name;
            string digits = new string(tail.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length >= 2) return digits.Substring(0, 2);
            string code = new string(tail.Where(char.IsLetterOrDigit).ToArray());
            return code.Length == 0 ? "?" : code.Substring(0, Math.Min(2, code.Length)).ToUpperInvariant();
        }
    }
}

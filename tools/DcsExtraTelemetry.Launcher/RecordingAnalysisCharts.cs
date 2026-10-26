using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DcsExtraTelemetry.Launcher
{
    internal static class RecordingChartPalette
    {
        public static readonly string[] Categories = TelemetryAnalysisCategories.All;
        public static Color ColorFor(string category)
        {
            if (category == "Ground Interaction") return ResourceColor("ChartCategoryGround");
            if (category == "Weapons and Countermeasures") return ResourceColor("ChartCategoryWeapons");
            if (category == "Aerodynamics") return ResourceColor("ChartCategoryAirframe");
            return ResourceColor("ChartCategoryRotor");
        }

        public static int DisplayOrder(string category)
        {
            if (category == "Aerodynamics") return 0;
            if (category == "Rotor and Powertrain") return 1;
            if (category == "Ground Interaction") return 2;
            return 3;
        }

        // GlassThemePalette.Active is the single source of truth for these
        // hex values; the live theme resource is preferred, falling back to it only when no
        // Application (e.g. design-time or a unit test) has applied a theme yet.
        public static Color ResourceColor(string key)
        {
            // ChartBackground is still resolved through ResourceColor("ChartBackground")
            // by the presentation contract; its transparent value lets the glass host show.
            SolidColorBrush brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as SolidColorBrush;
            if (brush != null) return brush.Color;
            string hex;
            IReadOnlyDictionary<string, string> palette = GlassThemePalette.Active ?? GlassThemePalette.DarkGlass;
            return palette.TryGetValue(key, out hex)
                ? (Color)ColorConverter.ConvertFromString(hex)
                : Colors.Gray;
        }
    }

    internal static class RecordingPhasePresentation
    {
        private static readonly string[] CanonicalOrder = { "Startup", "Taxi", "Takeoff", "Flight", "Combat", "Approach", "Landing", "Shutdown", "Ground", "Elapsed" };

        public static List<FlightPhaseSpan> Build(TelemetryAnalysisResult result)
        {
            double duration = result == null ? 0 : Math.Max(.001, result.EffectMapDurationSeconds);
            var phases = result == null
                ? new List<FlightPhaseSpan>()
                : result.FlightPhases
                    .Where(phase => phase != null && !string.IsNullOrWhiteSpace(phase.Name) && phase.EndSeconds > phase.StartSeconds)
                    .OrderBy(phase => phase.StartSeconds)
                    .Select(phase => new FlightPhaseSpan
                    {
                        Name = phase.Name,
                        StartSeconds = Math.Max(0, Math.Min(duration, phase.StartSeconds)),
                        EndSeconds = Math.Max(0, Math.Min(duration, phase.EndSeconds))
                    })
                    .Where(phase => phase.EndSeconds > phase.StartSeconds)
                    .ToList();

            MergeAdjacent(phases);
            int furthestCanonicalPhase = -1;
            for (int i = 0; i < phases.Count; i++)
            {
                int order = CanonicalIndex(phases[i].Name);
                if (order >= furthestCanonicalPhase) { furthestCanonicalPhase = order; continue; }

                // Brief detector regressions (for example Startup while already airborne,
                // or Taxi between Approach and Landing) are visual noise rather than a
                // meaningful new phase. Fold only short regressions into the surrounding
                // chronological presentation; the analyzer's source spans stay untouched.
                double seconds = phases[i].EndSeconds - phases[i].StartSeconds;
                double regressionLimit = Math.Max(12, Math.Min(30, duration * .015));
                if (seconds > regressionLimit) continue;
                if (i + 1 < phases.Count && CanonicalIndex(phases[i + 1].Name) >= furthestCanonicalPhase)
                    phases[i + 1].StartSeconds = phases[i].StartSeconds;
                else if (i > 0)
                    phases[i - 1].EndSeconds = phases[i].EndSeconds;
                phases.RemoveAt(i--);
            }
            MergeAdjacent(phases);
            return phases;
        }

        public static string PhaseAt(TelemetryAnalysisResult result, double seconds)
        {
            return Build(result).Where(phase => seconds >= phase.StartSeconds && seconds <= phase.EndSeconds).Select(phase => phase.Name).FirstOrDefault() ?? "Elapsed";
        }

        private static void MergeAdjacent(List<FlightPhaseSpan> phases)
        {
            for (int i = 1; i < phases.Count; i++)
            {
                FlightPhaseSpan previous = phases[i - 1];
                FlightPhaseSpan current = phases[i];
                if (!string.Equals(previous.Name, current.Name, StringComparison.OrdinalIgnoreCase) || current.StartSeconds > previous.EndSeconds + .01) continue;
                previous.EndSeconds = Math.Max(previous.EndSeconds, current.EndSeconds);
                phases.RemoveAt(i--);
            }
        }

        private static int CanonicalIndex(string name)
        {
            for (int i = 0; i < CanonicalOrder.Length; i++) if (string.Equals(CanonicalOrder[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return CanonicalOrder.Length;
        }
    }

    internal sealed class TimelineOverviewControl : FrameworkElement
    {
        private double durationSeconds = 1;
        private double viewStartSeconds;
        private double visibleSeconds = 1;
        private double cursorSeconds = -1;
        private bool dragging;
        private double dragOffsetSeconds;

        public event Action<double> ViewStartRequested;

        public void SetViewport(double duration, double viewStart, double visible, double cursor)
        {
            durationSeconds = Math.Max(.001, duration);
            visibleSeconds = Math.Max(.001, Math.Min(durationSeconds, visible));
            viewStartSeconds = Math.Max(0, Math.Min(durationSeconds - visibleSeconds, viewStart));
            cursorSeconds = cursor;
            InvalidateVisual();
        }

        public void RefreshTheme() { InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            Color trackColor = RecordingChartPalette.ResourceColor("ScrollTrack");
            Color thumbColor = RecordingChartPalette.ResourceColor("ScrollThumb");
            Color cursorColor = RecordingChartPalette.ResourceColor("ChartCursor");
            double railHeight = 5;
            double railTop = Math.Max(0, (ActualHeight - railHeight) / 2);
            var rail = new Rect(0, railTop, ActualWidth, railHeight);
            dc.DrawRoundedRectangle(new SolidColorBrush(trackColor), null, rail, railHeight / 2, railHeight / 2);

            double thumbLeft = viewStartSeconds / durationSeconds * ActualWidth;
            double thumbWidth = Math.Max(14, visibleSeconds / durationSeconds * ActualWidth);
            thumbWidth = Math.Min(ActualWidth, thumbWidth);
            thumbLeft = Math.Min(Math.Max(0, ActualWidth - thumbWidth), thumbLeft);
            var thumb = new Rect(thumbLeft, railTop, thumbWidth, railHeight);
            dc.DrawRoundedRectangle(new SolidColorBrush(thumbColor), null, thumb, railHeight / 2, railHeight / 2);

            if (cursorSeconds >= 0 && cursorSeconds <= durationSeconds)
            {
                double x = Math.Max(1, Math.Min(ActualWidth - 1, cursorSeconds / durationSeconds * ActualWidth));
                Color glow = Color.FromArgb(42, cursorColor.R, cursorColor.G, cursorColor.B);
                dc.DrawRectangle(new SolidColorBrush(glow), null, new Rect(x - 2, railTop - 3, 4, railHeight + 6));
                dc.DrawLine(new Pen(new SolidColorBrush(cursorColor), 1), new Point(x, railTop - 3), new Point(x, railTop + railHeight + 3));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (visibleSeconds >= durationSeconds - .001 || ActualWidth <= 0) return;
            double seconds = Math.Max(0, Math.Min(durationSeconds, e.GetPosition(this).X / ActualWidth * durationSeconds));
            if (seconds >= viewStartSeconds && seconds <= viewStartSeconds + visibleSeconds)
                dragOffsetSeconds = seconds - viewStartSeconds;
            else
                dragOffsetSeconds = visibleSeconds / 2;
            dragging = true;
            CaptureMouse();
            RequestViewStart(seconds - dragOffsetSeconds);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || !IsMouseCaptured || ActualWidth <= 0) return;
            double seconds = Math.Max(0, Math.Min(durationSeconds, e.GetPosition(this).X / ActualWidth * durationSeconds));
            RequestViewStart(seconds - dragOffsetSeconds);
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private void RequestViewStart(double seconds)
        {
            double start = Math.Max(0, Math.Min(durationSeconds - visibleSeconds, seconds));
            if (ViewStartRequested != null) ViewStartRequested(start);
        }
    }

    internal sealed class EffectTimelineControl : FrameworkElement
    {
        private const double LabelWidth = 257, SummaryWidth = 98, PhaseHeight = 36, GroupHeight = 28, RowHeight = 27, AxisHeight = 8;
        private static readonly FontFamily UiFace = new FontFamily("Segoe UI Variable Text, Segoe UI");
        private static readonly FontFamily MonoFace = new FontFamily("Cascadia Mono, Consolas");
        private readonly HashSet<string> expandedInactiveGroups = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Tuple<Rect, string, bool>> hitTargets = new List<Tuple<Rect, string, bool>>();
        private TelemetryAnalysisResult result;
        private double zoom = 1, viewStart;
        private double cursorSeconds;
        private bool hideUnusedEffects;
        private HashSet<string> visibleEffectNames;
        private DrawingGroup cachedChart;
        private double cachedPlotLeft, cachedPlotWidth, cachedCursorBottom;
        public event Action<string> EffectSelected;
        public event Action<double> TimeSelected;
        public event Action<string> HoverDetailChanged;
        public event EventHandler ViewChanged;
        public string SelectedEffectName { get; set; }
        public double CursorSeconds { get { return cursorSeconds; } set { if (Math.Abs(cursorSeconds - value) < .0001) return; cursorSeconds = value; InvalidateVisual(); } }
        public bool HideUnusedEffects
        {
            get { return hideUnusedEffects; }
            set { if (hideUnusedEffects == value) return; hideUnusedEffects = value; UpdateHeight(); InvalidateChart(); }
        }
        public void SetVisibleEffects(IEnumerable<string> names)
        {
            visibleEffectNames = names == null ? null : new HashSet<string>(names, StringComparer.Ordinal);
            UpdateHeight();
            InvalidateChart();
        }

        public TelemetryAnalysisResult Result
        {
            get { return result; }
            set { if (ReferenceEquals(result, value)) return; result = value; viewStart = 0; UpdateHeight(); InvalidateChart(); NotifyViewChanged(); }
        }
        public double Zoom { get { return zoom; } }
        public double ViewStartSeconds { get { return ViewStart; } }
        public double VisibleSecondsInView { get { return VisibleSeconds; } }
        public double DurationSeconds { get { return Duration; } }
        public double ScrollableSeconds { get { return Math.Max(0, Duration - VisibleSeconds); } }
        public void SetZoom(double value) { SetZoom(value, ViewStart + VisibleSeconds / 2); }
        public void ResetView() { zoom = 1; viewStart = 0; InvalidateChart(); NotifyViewChanged(); }
        public void SetViewStart(double seconds)
        {
            double next = Math.Max(0, Math.Min(ScrollableSeconds, seconds));
            if (Math.Abs(ViewStart - next) < .0001) return;
            viewStart = next;
            InvalidateChart();
            NotifyViewChanged();
        }
        public void RefreshTheme() { InvalidateChart(); }
        public void SelectEffect(string name) { if (string.Equals(SelectedEffectName, name, StringComparison.Ordinal)) return; SelectedEffectName = name; InvalidateChart(); }
        public double GetEffectOffset(string name)
        {
            double y = PhaseHeight;
            foreach (IGrouping<string, EffectMapTrack> group in OrderedGroups())
            {
                bool expanded = group.Any(t => t.Episodes.Count > 0) || expandedInactiveGroups.Contains(group.Key);
                y += GroupHeight;
                if (!expanded) continue;
                foreach (EffectMapTrack track in group) { if (string.Equals(track.Name, name, StringComparison.Ordinal)) return Math.Max(0, y - GroupHeight); y += RowHeight; }
            }
            return 0;
        }

        private double Duration { get { return result == null ? 0 : Math.Max(.001, result.EffectMapDurationSeconds); } }
        private double VisibleSeconds { get { return Duration / zoom; } }
        private double ViewStart { get { return Math.Max(0, Math.Min(viewStart, Duration - VisibleSeconds)); } }

        private void SetZoom(double value, double anchorSeconds)
        {
            double oldVisible = VisibleSeconds;
            zoom = Math.Max(1, Math.Min(16, value));
            double fraction = oldVisible <= 0 ? .5 : (anchorSeconds - ViewStart) / oldVisible;
            viewStart = Math.Max(0, Math.Min(Duration - VisibleSeconds, anchorSeconds - fraction * VisibleSeconds));
            InvalidateChart();
            NotifyViewChanged();
        }

        private void NotifyViewChanged()
        {
            if (ViewChanged != null) ViewChanged(this, EventArgs.Empty);
        }

        private void UpdateHeight()
        {
            if (result == null) { Height = 180; return; }
            double height = PhaseHeight + AxisHeight;
            foreach (IGrouping<string, EffectMapTrack> group in OrderedGroups())
            {
                bool active = group.Any(t => t.Episodes.Count > 0);
                height += GroupHeight + (active || expandedInactiveGroups.Contains(group.Key) ? group.Count() * RowHeight : 0);
            }
            Height = Math.Max(180, height + 8);
        }

        private IEnumerable<IGrouping<string, EffectMapTrack>> OrderedGroups()
        {
            if (result == null) return Enumerable.Empty<IGrouping<string, EffectMapTrack>>();
            IEnumerable<EffectMapTrack> tracks = result.EffectMap;
            if (visibleEffectNames != null) tracks = tracks.Where(t => visibleEffectNames.Contains(t.Name));
            if (hideUnusedEffects) tracks = tracks.Where(t => t.Episodes.Count > 0);
            return tracks.GroupBy(t => t.Category).OrderBy(g => RecordingChartPalette.DisplayOrder(g.Key));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            // WPF can render this control once with a transient zero/small arrange size
            // while the Flights layout is switching tabs or fitting the window.
            if (ActualWidth < 10 || ActualHeight < 10) return;
            if (cachedChart == null)
            {
                cachedChart = new DrawingGroup();
                using (DrawingContext cache = cachedChart.Open()) RenderStatic(cache);
                cachedChart.Freeze();
            }
            dc.DrawDrawing(cachedChart);
            if (result != null) DrawCursor(dc, cachedPlotLeft, cachedPlotWidth, PhaseHeight, cachedCursorBottom, RecordingChartPalette.ResourceColor("ChartCursor"));
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            cachedChart = null;
            base.OnRenderSizeChanged(sizeInfo);
        }

        private void InvalidateChart() { cachedChart = null; InvalidateVisual(); }

        private void RenderStatic(DrawingContext dc)
        {
            hitTargets.Clear();
            Color lane = RecordingChartPalette.ResourceColor("ChartLane");
            Color grid = RecordingChartPalette.ResourceColor("ChartGridMajor");
            Color text = RecordingChartPalette.ResourceColor("TextPrimary");
            Color secondary = RecordingChartPalette.ResourceColor("TextSecondary");
            if (result == null || result.EffectMap.Count == 0) return;
            double plotLeft = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28)), plotWidth = Math.Max(50, ActualWidth - plotLeft - SummaryWidth);
            DrawPhases(dc, plotLeft, plotWidth, secondary, grid);
            DrawGrid(dc, plotLeft, plotWidth, PhaseHeight, ActualHeight - AxisHeight, grid);
            double y = PhaseHeight;
            foreach (IGrouping<string, EffectMapTrack> group in OrderedGroups())
            {
                bool active = group.Any(t => t.Episodes.Count > 0), expanded = active || expandedInactiveGroups.Contains(group.Key);
                Color groupColor = RecordingChartPalette.ColorFor(group.Key);
                dc.DrawRectangle(new SolidColorBrush(groupColor), null, new Rect(2, y + 10, 8, 8));
                DrawText(dc, DisplayCategory(group.Key).ToUpperInvariant(), 22, y + 3, 13, text, FontWeights.SemiBold, plotLeft - 30);
                DrawText(dc, active ? group.Count(t => t.Episodes.Count > 0) + " of " + group.Count() + " active" : (expanded ? "collapse inactive" : "show inactive effects"), Math.Max(plotLeft, plotLeft + plotWidth - 90), y + 5, 10.5, secondary, FontWeights.Normal, 90, true);
                if (!active) hitTargets.Add(Tuple.Create(new Rect(0, y, plotLeft, GroupHeight), group.Key, true));
                y += GroupHeight;
                if (!expanded) continue;
                foreach (EffectMapTrack track in group)
                {
                    bool selected = !string.IsNullOrEmpty(SelectedEffectName) && string.Equals(SelectedEffectName, track.Name, StringComparison.Ordinal);
                    if (selected)
                    {
                        Color selection = RecordingChartPalette.ResourceColor("ChartCursor");
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(18, selection.R, selection.G, selection.B)), null, new Rect(4, y + 1, ActualWidth - 8, RowHeight - 2), 4, 4);
                    }
                    DrawTrackLabel(dc, track, 16, y + 3, plotLeft, track.Episodes.Count == 0 ? secondary : text, secondary);
                    dc.DrawRoundedRectangle(new SolidColorBrush(lane), null, new Rect(plotLeft, y + 6, plotWidth, 15), 3, 3);
                    if (track.Episodes.Count == 0)
                    {
                        Pen dashed = new Pen(new SolidColorBrush(Color.FromArgb((byte)(secondary.A * .45), secondary.R, secondary.G, secondary.B)), 1) { DashStyle = DashStyles.Dash };
                        dc.DrawLine(dashed, new Point(plotLeft, y + RowHeight / 2), new Point(plotLeft + plotWidth, y + RowHeight / 2));
                        DrawText(dc, "never fired", plotLeft + plotWidth + 8, y + 3, 11, secondary, FontWeights.Normal);
                    }
                    else
                    {
                        Color c = groupColor;
                        foreach (EffectEpisode episode in track.Episodes)
                        {
                            double start = Math.Max(episode.StartSeconds, ViewStart), end = Math.Min(episode.EndSeconds, ViewStart + VisibleSeconds);
                            if (end <= start) continue;
                            double x = plotLeft + (start - ViewStart) / VisibleSeconds * plotWidth;
                            double w = Math.Max(2, (end - start) / VisibleSeconds * plotWidth);
                            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(82 + episode.Peak * 173), c.R, c.G, c.B)), null, new Rect(x, y + 7, w, 13), 2, 2);
                        }
                        double activity = track.ActiveSeconds / Duration;
                        string summary = track.Episodes.Count <= 12 ? track.Episodes.Count + (track.Episodes.Count == 1 ? " event" : " events") : (activity * 100).ToString("0", CultureInfo.InvariantCulture) + "% of flight";
                        DrawText(dc, summary, plotLeft + plotWidth + 8, y + 3, 11, secondary, FontWeights.Normal, 215, true);
                    }
                    hitTargets.Add(Tuple.Create(new Rect(0, y, ActualWidth, RowHeight), track.Name, false));
                    y += RowHeight;
                }
            }
            cachedPlotLeft = plotLeft; cachedPlotWidth = plotWidth; cachedCursorBottom = y;
        }

        private void DrawPhases(DrawingContext dc, double left, double width, Color secondary, Color grid)
        {
            Color phaseBand = RecordingChartPalette.ResourceColor("ChartPhaseBand");
            dc.DrawRoundedRectangle(new SolidColorBrush(phaseBand), null, new Rect(left, 0, width, PhaseHeight - 6), 5, 5);
            dc.DrawLine(new Pen(new SolidColorBrush(grid), 1), new Point(left, PhaseHeight - 6), new Point(left + width, PhaseHeight - 6));
            List<FlightPhaseSpan> phases = RecordingPhasePresentation.Build(result);
            for (int phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
            {
                FlightPhaseSpan phase = phases[phaseIndex];
                double start = Math.Max(phase.StartSeconds, ViewStart), end = Math.Min(phase.EndSeconds, ViewStart + VisibleSeconds);
                if (end <= start) continue;
                double x = left + (start - ViewStart) / VisibleSeconds * width, w = (end - start) / VisibleSeconds * width;
                if (x > left + .5 && w >= 6) dc.DrawLine(new Pen(new SolidColorBrush(grid), 1), new Point(x, 4), new Point(x, PhaseHeight - 5));
                if (w > 42 && (string.Equals(phase.Name, "Flight", StringComparison.OrdinalIgnoreCase) || string.Equals(phase.Name, "Combat", StringComparison.OrdinalIgnoreCase) || string.Equals(phase.Name, "Approach", StringComparison.OrdinalIgnoreCase)))
                    DrawText(dc, phase.Name.ToUpperInvariant(), x + 6, 7, 9, secondary, FontWeights.Normal);
            }
        }

        private void DrawGrid(DrawingContext dc, double left, double width, double top, double bottom, Color grid)
        {
            for (int i = 0; i <= 4; i++)
            {
                double x = left + width * i / 4.0;
                dc.DrawLine(new Pen(new SolidColorBrush(grid), 1), new Point(x, top), new Point(x, bottom));
            }
        }

        private void DrawCursor(DrawingContext dc, double left, double width, double top, double bottom, Color accent)
        {
            if (CursorSeconds < ViewStart || CursorSeconds > ViewStart + VisibleSeconds) return;
            double x = left + (CursorSeconds - ViewStart) / VisibleSeconds * width;
            dc.DrawLine(new Pen(new SolidColorBrush(accent), 1.25), new Point(x, top), new Point(x, bottom));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (HandlePointerSelection(e.GetPosition(this))) e.Handled = true;
        }

        private bool HandlePointerSelection(Point p)
        {
            double left = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28)), width = Math.Max(50, ActualWidth - left - SummaryWidth);
            if (p.X >= left && p.X <= left + width)
            {
                string row = hitTargets.Where(hit => !hit.Item3 && hit.Item1.Contains(p)).Select(hit => hit.Item2).FirstOrDefault();
                if (!string.IsNullOrEmpty(row) && !string.Equals(SelectedEffectName, row, StringComparison.Ordinal))
                {
                    SelectedEffectName = row;
                    if (EffectSelected != null) EffectSelected(row);
                    InvalidateChart();
                }
                if (TimeSelected != null) TimeSelected(ViewStart + (p.X - left) / width * VisibleSeconds);
                return true;
            }
            foreach (Tuple<Rect, string, bool> hit in hitTargets)
            {
                if (!hit.Item1.Contains(p)) continue;
                if (hit.Item3) { if (!expandedInactiveGroups.Add(hit.Item2)) expandedInactiveGroups.Remove(hit.Item2); UpdateHeight(); InvalidateChart(); return true; }
                SelectedEffectName = string.Equals(SelectedEffectName, hit.Item2, StringComparison.Ordinal) ? null : hit.Item2;
                if (EffectSelected != null) EffectSelected(SelectedEffectName); InvalidateChart(); return true;
            }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.GetPosition(this); string row = hitTargets.Where(h => !h.Item3 && h.Item1.Contains(p)).Select(h => h.Item2).FirstOrDefault();
            double left = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28)), width = Math.Max(50, ActualWidth - left - SummaryWidth);
            if (row == null || p.X < left || p.X > left + width) { if (HoverDetailChanged != null) HoverDetailChanged(null); return; }
            double seconds = ViewStart + (p.X - left) / width * VisibleSeconds;
            EffectMapTrack track = result.EffectMap.First(t => t.Name == row);
            int bucket = Math.Min(track.Buckets.Length - 1, Math.Max(0, (int)(seconds / Duration * track.Buckets.Length)));
            EffectEpisode episode = track.Episodes.FirstOrDefault(x => seconds >= x.StartSeconds && seconds <= x.EndSeconds);
            string phase = RecordingPhasePresentation.PhaseAt(result, seconds);
            string detail = row + "  |  " + FormatTime(seconds) + "  |  " + phase + "  |  " + track.Frequency + "  |  " + (track.Buckets[bucket] * 100).ToString("0") + "%" + (episode == null ? "" : "  |  episode " + FormatTime(episode.StartSeconds) + " for " + FormatTime(episode.EndSeconds - episode.StartSeconds) + "  |  peak " + (episode.Peak * 100).ToString("0") + "%  |  avg " + (episode.Average * 100).ToString("0") + "%");
            if (HoverDetailChanged != null) HoverDetailChanged(detail);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (HoverDetailChanged != null) HoverDetailChanged(null);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                double left = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28)), width = Math.Max(50, ActualWidth - left - SummaryWidth);
                double anchor = ViewStart + Math.Max(0, Math.Min(1, (e.GetPosition(this).X - left) / width)) * VisibleSeconds;
                SetZoom(zoom * (e.Delta > 0 ? 1.35 : 1 / 1.35), anchor);
                e.Handled = true;
            }
            else if (zoom > 1 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SetViewStart(ViewStart - Math.Sign(e.Delta) * VisibleSeconds * .12);
                e.Handled = true;
            }
        }

        private static void DrawText(DrawingContext dc, string value, double x, double y, double size, Color color, FontWeight weight, double maxWidth = 215, bool mono = false)
        {
            var ft = new FormattedText(value ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(mono ? MonoFace : UiFace, FontStyles.Normal, weight, FontStretches.Normal), size, new SolidColorBrush(color), 1.0) { MaxTextWidth = Math.Max(1, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(ft, new Point(x, y));
        }
        private static void DrawTrackLabel(DrawingContext dc, EffectMapTrack track, double x, double y, double plotLeft, Color text, Color secondary)
        {
            const double gap = 8;
            double frequencyWidth = MeasureText(track.Frequency, 11.5, FontWeights.Normal, true);
            double nameWidth = Math.Max(24, plotLeft - x - frequencyWidth - gap - 8);
            double naturalNameWidth = MeasureText(track.Name, 13, FontWeights.Normal, false);
            double drawnNameWidth = Math.Min(nameWidth, naturalNameWidth);
            DrawText(dc, track.Name, x, y, 13, text, FontWeights.Normal, nameWidth);
            DrawText(dc, track.Frequency, x + drawnNameWidth + gap, y, 11.5, secondary, FontWeights.Normal, frequencyWidth + 3, true);
        }
        private static double MeasureText(string value, double size, FontWeight weight, bool mono)
        {
            var ft = new FormattedText(value ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(mono ? MonoFace : UiFace, FontStyles.Normal, weight, FontStretches.Normal), size, Brushes.White, 1.0);
            return ft.WidthIncludingTrailingWhitespace;
        }
        private static string DisplayCategory(string category) { return category == "Rotor and Powertrain" ? "Rotor" : category; }
        private static string FormatTime(double seconds) { return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss"); }
    }

    internal sealed class PhaseMatrixControl : FrameworkElement
    {
        private const double LabelWidth = 254, GroupHeight = 28, RowHeight = 26, HeaderHeight = 30, PeakWidth = 54;
        private static readonly FontFamily UiFace = new FontFamily("Segoe UI Variable Text, Segoe UI");
        private static readonly FontFamily MonoFace = new FontFamily("Cascadia Mono, Consolas");
        private readonly List<Tuple<Rect, string>> hitTargets = new List<Tuple<Rect, string>>();
        private TelemetryAnalysisResult result;
        private bool hideUnusedEffects;
        private HashSet<string> visibleEffectNames;
        public event Action<string> EffectSelected;
        public event Action<string> HoverDetailChanged;
        public string SelectedEffectName { get; set; }

        public TelemetryAnalysisResult Result
        {
            get { return result; }
            set { if (ReferenceEquals(result, value)) return; result = value; UpdateHeight(); InvalidateVisual(); }
        }

        public bool HideUnusedEffects
        {
            get { return hideUnusedEffects; }
            set { if (hideUnusedEffects == value) return; hideUnusedEffects = value; UpdateHeight(); InvalidateVisual(); }
        }

        public void SetVisibleEffects(IEnumerable<string> names)
        {
            visibleEffectNames = names == null ? null : new HashSet<string>(names, StringComparer.Ordinal);
            UpdateHeight();
            InvalidateVisual();
        }

        public void SelectEffect(string name) { if (string.Equals(SelectedEffectName, name, StringComparison.Ordinal)) return; SelectedEffectName = name; InvalidateVisual(); }
        public void RefreshTheme() { InvalidateVisual(); }

        private void UpdateHeight()
        {
            if (result == null) { Height = 180; return; }
            IEnumerable<EffectMapTrack> visible = result.EffectMap.Where(IsTrackVisible);
            int trackCount = visible.Count();
            int groupCount = RecordingChartPalette.Categories.Count(category => visible.Any(t => t.Category == category));
            Height = Math.Max(180, HeaderHeight + groupCount * GroupHeight + trackCount * RowHeight + 8);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc); hitTargets.Clear();
            if (ActualWidth < 10 || ActualHeight < 10) return;
            Color cell = RecordingChartPalette.ResourceColor("ChartLane");
            Color text = RecordingChartPalette.ResourceColor("TextPrimary");
            Color secondary = RecordingChartPalette.ResourceColor("TextSecondary");
            if (result == null || result.EffectMap.Count == 0) return;
            List<string> phases = OrderedPhaseNames();
            double left = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28));
            double matrixWidth = Math.Max(120, ActualWidth - left - PeakWidth - 12);
            double columnWidth = matrixWidth / Math.Max(1, phases.Count);
            for (int p = 0; p < phases.Count; p++)
                DrawText(dc, phases[p].ToUpperInvariant(), left + p * columnWidth + 6, 10, 9, secondary, FontWeights.Normal, Math.Max(20, columnWidth - 10), true);
            DrawText(dc, "PEAK", left + matrixWidth + 13, 10, 9, secondary, FontWeights.Normal, PeakWidth - 10, true);

            double y = HeaderHeight;
            foreach (string category in RecordingChartPalette.Categories.OrderBy(RecordingChartPalette.DisplayOrder))
            {
                List<EffectMapTrack> tracks = result.EffectMap.Where(t => t.Category == category && IsTrackVisible(t)).ToList();
                if (tracks.Count == 0) continue;
                Color categoryColor = RecordingChartPalette.ColorFor(category);
                dc.DrawRectangle(new SolidColorBrush(categoryColor), null, new Rect(8, y + 10, 8, 8));
                DrawText(dc, DisplayCategory(category).ToUpperInvariant(), 22, y + 5, 10.5, text, FontWeights.SemiBold, left - 30);
                int activeCount = tracks.Count(track => track.Episodes.Count > 0);
                DrawText(dc, activeCount + " of " + tracks.Count + " active", Math.Max(left, ActualWidth - PeakWidth - 112), y + 5, 9.5, secondary, FontWeights.Normal, 106, true);
                y += GroupHeight;
                foreach (EffectMapTrack track in tracks)
                {
                    bool inactive = track.Episodes.Count == 0;
                    bool selected = !string.IsNullOrEmpty(SelectedEffectName) && string.Equals(SelectedEffectName, track.Name, StringComparison.Ordinal);
                    if (selected)
                    {
                        Color selection = RecordingChartPalette.ResourceColor("ChartCursor");
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(18, selection.R, selection.G, selection.B)), null, new Rect(4, y + 1, ActualWidth - 8, RowHeight - 2), 4, 4);
                    }
                    DrawTrackLabel(dc, track, 16, y + 3, left, inactive ? secondary : text, secondary);
                    for (int p = 0; p < phases.Count; p++)
                    {
                        double value = PeakForPhase(track, phases[p]);
                        Rect rect = new Rect(left + p * columnWidth + 1.5, y + 3, Math.Max(1, columnWidth - 3), RowHeight - 6);
                        dc.DrawRoundedRectangle(new SolidColorBrush(cell), null, rect, 3, 3);
                        if (value >= .02)
                        {
                            byte alpha = (byte)Math.Max(56, Math.Min(255, 56 + value * 199));
                            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(alpha, categoryColor.R, categoryColor.G, categoryColor.B)), null, rect, 3, 3);
                        }
                    }
                    double peak = track.PeakIntensity;
                    DrawText(dc, peak < .02 ? "—" : (peak * 100).ToString("0", CultureInfo.InvariantCulture) + "%", left + matrixWidth + 13, y + 3, 10, secondary, FontWeights.Normal, PeakWidth - 10, true);
                    hitTargets.Add(Tuple.Create(new Rect(0, y, ActualWidth, RowHeight), track.Name));
                    y += RowHeight;
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            string name = hitTargets.Where(h => h.Item1.Contains(e.GetPosition(this))).Select(h => h.Item2).FirstOrDefault();
            if (name == null) return;
            SelectedEffectName = string.Equals(SelectedEffectName, name, StringComparison.Ordinal) ? null : name;
            if (EffectSelected != null) EffectSelected(SelectedEffectName);
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point point = e.GetPosition(this);
            string name = hitTargets.Where(h => h.Item1.Contains(point)).Select(h => h.Item2).FirstOrDefault();
            if (name == null) { if (HoverDetailChanged != null) HoverDetailChanged(null); return; }
            EffectMapTrack track = result.EffectMap.First(t => t.Name == name);
            List<string> phases = OrderedPhaseNames();
            double left = Math.Min(LabelWidth, Math.Max(150, ActualWidth * .28));
            double matrixWidth = Math.Max(120, ActualWidth - left - PeakWidth - 12);
            int phaseIndex = point.X < left || point.X >= left + matrixWidth ? -1 : Math.Min(phases.Count - 1, Math.Max(0, (int)((point.X - left) / matrixWidth * phases.Count)));
            string detail = phaseIndex < 0 ? "Recorded peak " + (track.PeakIntensity * 100).ToString("0") + "%" : phases[phaseIndex] + " peak " + (PeakForPhase(track, phases[phaseIndex]) * 100).ToString("0") + "%";
            string hover = track.Name + "  |  " + track.Category + "  |  " + track.Frequency + "  |  " + detail;
            if (HoverDetailChanged != null) HoverDetailChanged(hover);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (HoverDetailChanged != null) HoverDetailChanged(null);
        }

        private bool IsTrackVisible(EffectMapTrack track)
        {
            return (visibleEffectNames == null || visibleEffectNames.Contains(track.Name)) && (!hideUnusedEffects || track.Episodes.Count > 0);
        }

        private List<string> OrderedPhaseNames()
        {
            string[] canonical = { "Startup", "Taxi", "Takeoff", "Flight", "Combat", "Approach", "Landing", "Shutdown", "Ground", "Elapsed" };
            List<FlightPhaseSpan> displayPhases = RecordingPhasePresentation.Build(result);
            var present = new HashSet<string>(displayPhases.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var ordered = canonical.Where(present.Contains).ToList();
            ordered.AddRange(displayPhases.Select(p => p.Name).Where(p => !ordered.Contains(p, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase));
            return ordered.Count == 0 ? new List<string> { "Elapsed" } : ordered;
        }

        private double PeakForPhase(EffectMapTrack track, string phaseName)
        {
            double peak = 0, duration = Math.Max(.001, result.EffectMapDurationSeconds);
            foreach (FlightPhaseSpan phase in RecordingPhasePresentation.Build(result).Where(p => string.Equals(p.Name, phaseName, StringComparison.OrdinalIgnoreCase)))
            {
                int first = Math.Max(0, Math.Min(track.Buckets.Length - 1, (int)Math.Floor(phase.StartSeconds / duration * track.Buckets.Length)));
                int last = Math.Max(first, Math.Min(track.Buckets.Length - 1, (int)Math.Ceiling(phase.EndSeconds / duration * track.Buckets.Length) - 1));
                for (int i = first; i <= last; i++) peak = Math.Max(peak, track.Buckets[i]);
            }
            return peak;
        }

        private static void DrawText(DrawingContext dc, string value, double x, double y, double size, Color color, FontWeight weight, double maxWidth, bool mono = false)
        {
            var ft = new FormattedText(value ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(mono ? MonoFace : UiFace, FontStyles.Normal, weight, FontStretches.Normal), size, new SolidColorBrush(color), 1.0) { MaxTextWidth = Math.Max(1, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(ft, new Point(x, y));
        }
        private static void DrawTrackLabel(DrawingContext dc, EffectMapTrack track, double x, double y, double matrixLeft, Color text, Color secondary)
        {
            const double frequencyWidth = 58;
            DrawText(dc, track.Name, x, y, 11, text, FontWeights.Normal, Math.Max(24, matrixLeft - x - frequencyWidth - 10));
            DrawText(dc, track.Frequency, matrixLeft - frequencyWidth, y, 10.5, secondary, FontWeights.Normal, frequencyWidth - 4, true);
        }
        private static string DisplayCategory(string category) { return category == "Rotor and Powertrain" ? "Rotor" : category; }
    }

    internal sealed class CombinedLoadControl : FrameworkElement
    {
        private TelemetryAnalysisResult result;
        private double cursorSeconds;
        private DrawingGroup cachedChart;
        private HashSet<string> visibleEffectNames;
        public TelemetryAnalysisResult Result { get { return result; } set { if (ReferenceEquals(result, value)) return; result = value; cachedChart = null; InvalidateVisual(); } }
        public double CursorSeconds { get { return cursorSeconds; } set { if (Math.Abs(cursorSeconds - value) < .0001) return; cursorSeconds = value; InvalidateVisual(); } }
        public event Action<double> TimeSelected;
        public event Action<string> HoverDetailChanged;
        public void SetVisibleEffects(IEnumerable<string> names) { visibleEffectNames = names == null ? null : new HashSet<string>(names, StringComparer.Ordinal); cachedChart = null; InvalidateVisual(); }
        public void RefreshTheme() { cachedChart = null; InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth < 20 || ActualHeight < 30) return;
            if (cachedChart == null)
            {
                cachedChart = new DrawingGroup();
                using (DrawingContext cache = cachedChart.Open()) RenderStatic(cache);
                cachedChart.Freeze();
            }
            dc.DrawDrawing(cachedChart);
            if (Result == null || Result.EffectMapDurationSeconds <= 0) return;
            double left = 8, right = ActualWidth - 8, top = 8, bottom = ActualHeight - 22;
            double cursorX = left + CursorSeconds / Result.EffectMapDurationSeconds * (right - left);
            Color accent = RecordingChartPalette.ResourceColor("ChartCursor");
            dc.DrawLine(new Pen(new SolidColorBrush(accent), 1.25), new Point(cursorX, top), new Point(cursorX, bottom));
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { cachedChart = null; base.OnRenderSizeChanged(sizeInfo); }

        private void RenderStatic(DrawingContext dc)
        {
            if (Result == null || Result.EffectMap.Count == 0 || Result.CategoryLoads == null || Result.EffectMapDurationSeconds <= 0) return;
            List<EffectMapTrack> visibleTracks = Result.EffectMap.Where(t => visibleEffectNames == null || visibleEffectNames.Contains(t.Name)).ToList();
            if (visibleTracks.Count == 0) return;
            double left = 8, right = ActualWidth - 8, top = 8, bottom = ActualHeight - 24, width = right - left, height = bottom - top;
            Color lane = RecordingChartPalette.ResourceColor("ChartLane");
            Color phaseBand = RecordingChartPalette.ResourceColor("ChartPhaseBand");
            Color majorGrid = RecordingChartPalette.ResourceColor("ChartGridMajor");
            Color minorGrid = RecordingChartPalette.ResourceColor("ChartGridMinor");
            Color secondary = RecordingChartPalette.ResourceColor("TextSecondary");
            dc.DrawRoundedRectangle(new SolidColorBrush(lane), null, new Rect(left, top, width, height), 8, 8);
            foreach (FlightPhaseSpan phase in RecordingPhasePresentation.Build(Result))
            {
                double phaseLeft = left + Math.Max(0, Math.Min(Result.EffectMapDurationSeconds, phase.StartSeconds)) / Result.EffectMapDurationSeconds * width;
                double phaseRight = left + Math.Max(0, Math.Min(Result.EffectMapDurationSeconds, phase.EndSeconds)) / Result.EffectMapDurationSeconds * width;
                if (phaseRight <= phaseLeft) continue;
                dc.DrawRectangle(new SolidColorBrush(phaseBand), null, new Rect(phaseLeft, top, phaseRight - phaseLeft, height));
            }
            for (int i = 0; i <= 8; i++)
            {
                double x = left + width * i / 8.0;
                dc.DrawLine(new Pen(new SolidColorBrush(i % 2 == 0 ? majorGrid : minorGrid), 1), new Point(x, top), new Point(x, bottom));
            }
            int sourceBuckets = Result.ActiveEffectCounts.Length, columns = Math.Max(1, Math.Min(sourceBuckets, (int)Math.Max(1, width / 14)));
            double maxLoad = 1;
            double[,] loads = new double[columns, RecordingChartPalette.Categories.Length];
            for (int c = 0; c < columns; c++)
            {
                int from = c * sourceBuckets / columns, to = Math.Max(from + 1, (c + 1) * sourceBuckets / columns);
                for (int b = from; b < Math.Min(to, sourceBuckets); b++)
                {
                    for (int g = 0; g < RecordingChartPalette.Categories.Length; g++)
                    {
                        string category = RecordingChartPalette.Categories[g];
                        double categoryLoad = visibleTracks.Count == Result.EffectMap.Count
                            ? Result.CategoryLoads[g][b]
                            : visibleTracks.Where(t => t.Category == category && b < t.Buckets.Length).Sum(t => t.Buckets[b]);
                        loads[c, g] = Math.Max(loads[c, g], categoryLoad);
                    }
                }
                double total = 0; for (int g = 0; g < RecordingChartPalette.Categories.Length; g++) total += loads[c, g]; maxLoad = Math.Max(maxLoad, total);
            }
            for (int c = 0; c < columns; c++)
            {
                double y = bottom;
                int topSegment = -1;
                for (int g = RecordingChartPalette.Categories.Length - 1; g >= 0; g--) if (loads[c, g] > 0) { topSegment = g; break; }
                for (int g = 0; g < RecordingChartPalette.Categories.Length; g++)
                {
                    double h = loads[c, g] / maxLoad * height; y -= h;
                    Rect segment = new Rect(left + c * width / columns + 1, y, Math.Max(1, width / columns - 2), h);
                    Brush brush = new SolidColorBrush(RecordingChartPalette.ColorFor(RecordingChartPalette.Categories[g]));
                    if (g == topSegment) dc.DrawRoundedRectangle(brush, null, segment, 2, 2);
                    else dc.DrawRectangle(brush, null, segment);
                }
            }
            DrawLabel(dc, "0:00", left, bottom + 6, secondary);
            DrawLabel(dc, FormatTime(Result.EffectMapDurationSeconds), right - 36, bottom + 6, secondary);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (Result == null || TimeSelected == null) return; double fraction = Math.Max(0, Math.Min(1, (e.GetPosition(this).X - 8) / Math.Max(1, ActualWidth - 16))); TimeSelected(fraction * Result.EffectMapDurationSeconds);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Result == null || Result.EffectMap.Count == 0) { if (HoverDetailChanged != null) HoverDetailChanged(null); return; }
            double fraction = Math.Max(0, Math.Min(1, (e.GetPosition(this).X - 8) / Math.Max(1, ActualWidth - 16)));
            double seconds = fraction * Result.EffectMapDurationSeconds;
            int bucket = Math.Min(Result.EffectMap[0].Buckets.Length - 1, Math.Max(0, (int)(fraction * Result.EffectMap[0].Buckets.Length)));
            var active = Result.EffectMap.Where(t => (visibleEffectNames == null || visibleEffectNames.Contains(t.Name)) && t.Buckets[bucket] >= .02).OrderByDescending(t => t.Buckets[bucket]).ToList();
            var categoryText = RecordingChartPalette.Categories.Select(category => category + " " + active.Where(t => t.Category == category).Sum(t => t.Buckets[bucket]).ToString("0.00", CultureInfo.InvariantCulture));
            string hover = FormatTime(seconds) + "  |  " + active.Count + " effects active  |  " + string.Join("  |  ", categoryText) + (active.Count == 0 ? "" : "  |  strongest: " + string.Join(", ", active.Take(3).Select(t => t.Name + " " + (t.Buckets[bucket] * 100).ToString("0") + "%")));
            if (HoverDetailChanged != null) HoverDetailChanged(hover);
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (HoverDetailChanged != null) HoverDetailChanged(null);
        }
        private static void DrawLabel(DrawingContext dc, string value, double x, double y, Color color)
        {
            var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 9.5, new SolidColorBrush(color), 1.0);
            dc.DrawText(text, new Point(x, y));
        }
        private static string FormatTime(double seconds) { return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss"); }
    }
}

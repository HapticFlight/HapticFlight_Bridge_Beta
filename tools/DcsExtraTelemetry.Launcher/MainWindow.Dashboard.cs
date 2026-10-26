using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DcsExtraTelemetry.Launcher
{
    // Dashboard, aircraft list and selected-effect navigation for the dark glass layout.
    public partial class MainWindow
    {
        // Tile colours cycle through these palette keys; each aircraft always gets the same one.
        private static readonly string[] AircraftTileBrushKeys =
            { "ChartCategoryRotor", "Primary", "ChartBarPeak", "ChartCategoryAirframe", "TextMuted" };

        private int dashboardRefreshVersion;
        private bool suppressDashboardRefresh;
        private DashboardSnapshot lastDashboardSnapshot;

        private async void RefreshDashboard()
        {
            if (OpenLastFlightButton == null) return;
            int version = ++dashboardRefreshVersion;
            List<FileInfo> recordings = new List<FileInfo>();
            try
            {
                foreach (string folderPath in GetRecordingFoldersForBrowsing())
                {
                    var folder = new DirectoryInfo(folderPath);
                    if (folder.Exists) recordings.AddRange(RecordingFileFormat.EnumerateFiles(folder));
                }
            }
            catch (Exception ex)
            {
                AppendLog("DASHBOARD: " + ex.Message);
            }

            DashboardSnapshot snapshot;
            try
            {
                List<FileInfo> distinct = recordings
                    .GroupBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                snapshot = await Task.Run(() => DashboardStats.Build(distinct, DashboardPeriod.AllTime, DateTime.Now));
            }
            catch (Exception ex)
            {
                AppendLog("DASHBOARD: " + ex.Message);
                return;
            }

            // A newer refresh (another period, a new recording) has already started.
            if (version != dashboardRefreshVersion) return;
            lastDashboardSnapshot = snapshot;
            RenderDashboard(snapshot);
        }

        private void RenderDashboard(DashboardSnapshot snapshot)
        {
            RenderLastFlight(snapshot.LastFlight);
            RenderAircraftLists(snapshot.Aircraft);
            FitDashboardWindowToContent(AdvancedInstallOptionsExpander.IsExpanded);
        }

        private void RenderLastFlight(FlightSummary flight)
        {
            bool hasFlight = flight != null;
            OpenLastFlightButton.IsEnabled = hasFlight;
            if (!hasFlight)
            {
                LastFlightChipText.Text = "No flights yet";
                LastFlightEndText.Text = "Record a flight on the Flights tab to see your recent activity here.";
                return;
            }

            LastFlightChipText.Text = flight.Aircraft + ", " + FormatRelativeDate(flight.RecordedAt);
            LastFlightEndText.Text = "Flight duration  ·  " + DashboardStats.FormatClock(flight.DurationSeconds);
        }

        private void RenderAircraftLists(IList<AircraftSummary> aircraft)
        {
            MyAircraftPanel.Children.Clear();
            AircraftTilesPanel.Children.Clear();
            AircraftEmptyText.Visibility = aircraft.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (aircraft.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "Your aircraft appear here after your first recorded flight.",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                MyAircraftPanel.Children.Add(empty);
                return;
            }

            foreach (AircraftSummary item in aircraft.Take(6))
                MyAircraftPanel.Children.Add(CreateAircraftRow(item));

            foreach (AircraftSummary item in aircraft)
            {
                var tile = new Border { Width = 200, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(12) };
                tile.SetResourceReference(Border.BackgroundProperty, "GlassSurfaceSubtle");
                var stack = new StackPanel();
                stack.Children.Add(CreateAircraftTile(item.Aircraft, 44));
                stack.Children.Add(new TextBlock { Text = item.Aircraft, FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 10, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
                var detail = new TextBlock
                {
                    Text = item.FlightCount + (item.FlightCount == 1 ? " flight" : " flights") + ", last " + FormatRelativeDate(item.LastFlown),
                    FontSize = 11.5,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                detail.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                stack.Children.Add(detail);
                tile.Child = stack;
                AircraftTilesPanel.Children.Add(tile);
            }
        }

        private FrameworkElement CreateAircraftRow(AircraftSummary item)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 11) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(CreateAircraftTile(item.Aircraft, 36));

            var text = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = item.Aircraft, FontWeight = FontWeights.Bold, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis });
            var last = new TextBlock { Text = "Last flown " + FormatRelativeDate(item.LastFlown), FontSize = 11 };
            last.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            text.Children.Add(last);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var count = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            count.Children.Add(new TextBlock { Text = item.FlightCount.ToString(CultureInfo.CurrentCulture), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right });
            var unit = new TextBlock { Text = item.FlightCount == 1 ? "flight" : "flights", FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Right };
            unit.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            count.Children.Add(unit);
            Grid.SetColumn(count, 2);
            row.Children.Add(count);
            return row;
        }

        private Border CreateAircraftTile(string aircraft, double size)
        {
            int hash = 0;
            foreach (char character in aircraft ?? string.Empty) hash = (hash * 31 + char.ToUpperInvariant(character)) & 0x7FFFFFFF;
            var tile = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * .22),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            tile.SetResourceReference(Border.BackgroundProperty, AircraftTileBrushKeys[hash % AircraftTileBrushKeys.Length]);
            tile.Child = new TextBlock
            {
                Text = DashboardStats.TileCode(aircraft),
                Style = (Style)FindResource("AircraftTileText"),
                FontSize = size * .34,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E))
            };
            return tile;
        }

        private static string FormatRelativeDate(DateTime when)
        {
            DateTime today = DateTime.Today;
            App app = Application.Current as App;
            if (when.Date == today) return "today " + (app == null ? when.ToString("HH:mm", CultureInfo.CurrentCulture) : app.FormatFlightTime(when));
            if (when.Date == today.AddDays(-1)) return "yesterday";
            return app == null ? when.ToString(when.Year == today.Year ? "d MMM" : "d MMM yyyy", CultureInfo.CurrentCulture) : app.FormatFlightDate(when);
        }

        private void UpdateSetupSummary(bool fullyInstalled, int itemsNeedingAttention, int updatesAvailable)
        {
            if (SetupSummaryText == null) return;
            SetupSummaryText.Text = fullyInstalled
                ? updatesAvailable == 0 ? "Everything is installed" : updatesAvailable == 1 ? "Installed · 1 component update available" : "Installed · " + updatesAvailable + " component updates available"
                : itemsNeedingAttention == 1 ? "1 item needs attention" : itemsNeedingAttention + " items need attention";
            SetupSummaryText.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled && updatesAvailable == 0 ? "Success" : "Warning");
            SetupBadge.SetResourceReference(Border.BackgroundProperty, fullyInstalled && updatesAvailable == 0 ? "GoodBadgeBackground" : fullyInstalled ? "Warning" : "Danger");
            SetupBadge.SetResourceReference(Border.BorderBrushProperty, fullyInstalled && updatesAvailable == 0 ? "Success" : fullyInstalled ? "Warning" : "DangerHover");
            SetupBadge.BorderThickness = fullyInstalled && updatesAvailable == 0 ? new Thickness(1) : new Thickness(2);
            SetupBadgeGlyph.Text = fullyInstalled && updatesAvailable == 0 ? "✓" : "!";
            SetupBadgeGlyph.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled && updatesAvailable == 0 ? "Success" : "DangerText");
            TitleStatusDetailText.Text = fullyInstalled
                ? updatesAvailable == 0 ? "Start DCS to fly" : updatesAvailable == 1 ? "1 component update available" : updatesAvailable + " component updates available"
                : itemsNeedingAttention == 1 ? "1 setup item left" : itemsNeedingAttention + " setup items left";
        }

        private async void OpenLastFlightButton_Click(object sender, RoutedEventArgs e)
        {
            FlightSummary flight = lastDashboardSnapshot == null ? null : lastDashboardSnapshot.LastFlight;
            if (flight == null) return;
            if (string.Equals(flight.Path, lastRecordingPath, StringComparison.OrdinalIgnoreCase) && currentEffectMapResult != null)
            {
                OpenRecordingAnalyzer();
                return;
            }
            try
            {
                await AnalyzeRecordingAsync(flight.Path);
                OpenRecordingAnalyzer();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        // ============ Selected effect navigation and export (formerly the analyser window) ============

        private List<EffectMapTrack> SelectableEffectTracks()
        {
            if (currentEffectMapResult == null) return new List<EffectMapTrack>();
            HashSet<string> visible = VisibleEffectNames();
            return currentEffectMapResult.EffectMap
                .Where(track => visible.Contains(track.Name))
                .OrderBy(track => RecordingChartPalette.DisplayOrder(track.Category))
                .ThenBy(track => track.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateSelectedEffectNavigation()
        {
            if (SelectedEffectPreviousButton == null) return;
            List<EffectMapTrack> tracks = SelectableEffectTracks();
            int index = tracks.FindIndex(track => string.Equals(track.Name, selectedEffectMapTrack, StringComparison.Ordinal));
            bool canNavigate = tracks.Count > 1;
            SelectedEffectPreviousButton.IsEnabled = canNavigate;
            SelectedEffectNextButton.IsEnabled = canNavigate;
            SelectedEffectNameText.ToolTip = index < 0 ? null
                : "Effect " + (index + 1).ToString(CultureInfo.CurrentCulture) + " of " + tracks.Count.ToString(CultureInfo.CurrentCulture);
        }

        private void SelectedEffectPreviousButton_Click(object sender, RoutedEventArgs e) { SelectAdjacentEffect(-1); }
        private void SelectedEffectNextButton_Click(object sender, RoutedEventArgs e) { SelectAdjacentEffect(1); }

        private void SelectAdjacentEffect(int direction)
        {
            List<EffectMapTrack> tracks = SelectableEffectTracks();
            if (tracks.Count == 0) return;
            int index = tracks.FindIndex(track => string.Equals(track.Name, selectedEffectMapTrack, StringComparison.Ordinal));
            if (index < 0) index = 0;
            index = (index + direction + tracks.Count) % tracks.Count;
            selectedEffectMapTrack = tracks[index].Name;
            EffectTimelineScrollViewer.ScrollToVerticalOffset(EffectTimelineChart.GetEffectOffset(selectedEffectMapTrack));
            if (!string.IsNullOrEmpty(soloEffectMapTrack))
            {
                // An isolated view follows the selection so the charts keep showing one effect.
                soloEffectMapTrack = selectedEffectMapTrack;
                RenderFlightsDashboard();
                return;
            }
            UpdateSelectedEffectPanel();
            RenderDashboardCanvases();
        }

        private void ExportEffectReportButton_Click(object sender, RoutedEventArgs e)
        {
            TelemetryAnalysisResult result = currentEffectMapResult;
            if (result == null) return;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export effect report",
                Filter = "CSV report (*.csv)|*.csv|Text report (*.txt)|*.txt",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = SafeReportFileName((string.IsNullOrWhiteSpace(result.AircraftType) ? "flight" : result.AircraftType) + "-effect-report.csv")
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                HashSet<string> visible = VisibleEffectNames();
                var report = new StringBuilder();
                report.AppendLine("HapticFlight Bridge effect report");
                report.AppendLine("Recording," + Csv(string.IsNullOrWhiteSpace(lastRecordingPath) ? "Imported recording" : Path.GetFileName(lastRecordingPath)));
                report.AppendLine("Aircraft," + Csv(result.AircraftType));
                report.AppendLine("Duration," + Csv(DashboardStats.FormatClock(result.EffectMapDurationSeconds)));
                report.AppendLine("Valid samples," + result.ValidSamples.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("Peak concurrency," + result.PeakConcurrency.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("Peak concurrency time," + Csv(DashboardStats.FormatClock(result.PeakConcurrencyTimeSeconds)));
                report.AppendLine();
                report.AppendLine("Debrief findings");
                report.AppendLine("Severity,Finding");
                foreach (FlightDebriefFinding finding in result.DebriefFindings)
                    report.AppendLine(Csv(finding.Severity) + "," + Csv(finding.Text));
                report.AppendLine();
                report.AppendLine("Effect events");
                report.AppendLine("Time,Phase,Effect,Category,Frequency,Duration,Peak intensity,Average intensity");
                foreach (FlightAnalysisEvent item in result.Events)
                    report.AppendLine(Csv(item.TimeText) + "," + Csv(item.Phase) + "," + Csv(item.Effect) + "," + Csv(DisplayCategory(item.Category)) + "," + Csv(item.Frequency) + "," + Csv(item.DurationText) + "," + Csv(item.PeakText) + "," + Csv(item.AverageText));
                report.AppendLine();
                report.AppendLine("Telemetry signal sources");
                report.AppendLine("Signal,Unit,Source");
                foreach (TelemetrySignalTrack signal in result.TelemetrySignals)
                    report.AppendLine(Csv(signal.Name) + "," + Csv(signal.Unit) + "," + Csv(signal.Source));
                report.AppendLine();
                report.AppendLine("Effect,Category,Frequency,Peak intensity,Events,Shown in current view");
                foreach (EffectMapTrack track in result.EffectMap
                    .OrderBy(item => RecordingChartPalette.DisplayOrder(item.Category))
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    report.AppendLine(Csv(track.Name) + "," + Csv(DisplayCategory(track.Category)) + "," + Csv(track.Frequency) + ","
                        + (track.PeakIntensity * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%,"
                        + track.Episodes.Count.ToString(CultureInfo.InvariantCulture) + "," + (visible.Contains(track.Name) ? "Yes" : "No"));
                }
                File.WriteAllText(dialog.FileName, report.ToString(), new UTF8Encoding(false));
                AnalysisHoverInspectorText.Text = "Exported effect report to " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The effect report could not be written.\n\n" + ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string DisplayCategory(string category)
        {
            string[] match = DashboardStats.Categories.FirstOrDefault(entry => entry[0] == category);
            return match == null ? category : match[1];
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string SafeReportFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "effect-report.csv" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars()) text = text.Replace(invalid, '-');
            return text;
        }
    }
}

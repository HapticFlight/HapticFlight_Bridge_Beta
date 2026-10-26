using Microsoft.Win32;
using DcsExtraTelemetry.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DcsExtraTelemetry.Launcher
{
    public partial class MainWindow : Window
    {
        private const double ReferenceWindowWidth = 740.0;
        private const double ReferenceWindowHeight = 440.0;
        private const double DefaultWorkAreaCoverage = 0.94;
        private const double MinimumWindowWidth = 720.0;
        // The analysis charts need room for effect labels and a readable timeline.
        // Keep the rest of the app at its compact reference size.
        private const double FlightAnalysisWindowWidth = 1120.0;
        private const double MinimumFlightAnalysisWindowWidth = 920.0;
        // Keep a compact but usable desktop window on short pages; taller pages
        // grow only when their visible content needs the additional room.
        private const double MinimumWindowHeight = 410.0;
        private const double ReferenceFrameWidth = 1120.0;
        private const double ReferenceFrameHeight = 780.0;
        private const double MaximizedFrameInset = 8.0;
        private readonly HashSet<string> customDcsProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly UpdateService updateService = new UpdateService();
        private string packageRoot;
        private string simHubPath;
        private string confirmedDcsProfile;
        private PackageInfo packageInfo;
        private UpdateInfo availableUpdate;
        private bool isBusy;
        private bool sidebarCollapsed;
        private Process recorderProcess;
        private EventWaitHandle recorderStopSignal;
        private string lastRecordingPath;
        private int simHubTelemetryPort = TelemetryPortSettings.DefaultSimHubPort;
        private int recorderPort = TelemetryPortSettings.DefaultRecorderPort;
        private TelemetryAnalysisResult currentEffectMapResult;
        private readonly HashSet<string> hiddenEffectMapTracks = new HashSet<string>(StringComparer.Ordinal);
        // These controls affect the dashboard only. ShakeIt output remains
        // untouched so a user can inspect a quiet lane without changing a live
        // haptic profile.
        private readonly HashSet<string> mutedEffectMapTracks = new HashSet<string>(StringComparer.Ordinal);
        private string soloEffectMapTrack;
        private string selectedEffectMapTrack;
        private bool updatingEffectMapFilters;
        private bool updatingRecordingSelector;
        private bool hasRecentRecordings;
        private string activeAnalysisTab = "Timeline";
        private bool homePathsReady;
        private bool homeFullyInstalled;
        private readonly DispatcherTimer replayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private double replayPositionSeconds;
        private bool replayPlaying;
        private bool updatingReplaySlider;
        private string debriefExperimentPlan = string.Empty;
        private readonly HashSet<string> focusedEffectMapTracks = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, TextBlock> replayLegendValues = new Dictionary<string, TextBlock>(StringComparer.Ordinal);
        private readonly List<System.Windows.Shapes.Rectangle> replayBars = new List<System.Windows.Shapes.Rectangle>();
        private readonly Dictionary<string, SolidColorBrush> replayBarBrushes = new Dictionary<string, SolidColorBrush>(StringComparer.Ordinal);
        private StackPanel loadCategoryActivityPanel;
        private readonly EffectTimelineControl EffectTimelineChart = new EffectTimelineControl();
        private readonly PhaseMatrixControl PhaseMatrixChart = new PhaseMatrixControl();
        private readonly CombinedLoadControl CombinedLoadChart = new CombinedLoadControl();
        private TelemetrySignalTrack selectedTelemetrySignal;
        private TelemetryAnalysisResult comparisonAnalysisResult;
        private bool updatingTelemetrySignalSelection;
        private bool updatingComparisonSelector;
        public MainWindow()
        {
            InitializeComponent();
            EffectTimelineHost.Children.Add(EffectTimelineChart);
            PhaseMatrixHost.Children.Add(PhaseMatrixChart);
            CombinedLoadHost.Children.Add(CombinedLoadChart);
            InitializeDisplayPreferences();
            Grid combinedLoadLayout = CombinedLoadHost.Parent as Grid;
            if (combinedLoadLayout != null && combinedLoadLayout.RowDefinitions.Count > 2)
                combinedLoadLayout.RowDefinitions[2].Height = new GridLength(285);
            ConfigureCombinedLoadLayout();
            EffectTimelineChart.EffectSelected += EffectTimelineChart_EffectSelected;
            EffectTimelineChart.TimeSelected += RecordingChart_TimeSelected;
            EffectTimelineChart.ViewChanged += EffectTimelineChart_ViewChanged;
            PhaseMatrixChart.EffectSelected += EffectTimelineChart_EffectSelected;
            CombinedLoadChart.TimeSelected += RecordingChart_TimeSelected;
            EffectTimelineChart.HoverDetailChanged += RecordingChart_HoverDetailChanged;
            PhaseMatrixChart.HoverDetailChanged += RecordingChart_HoverDetailChanged;
            CombinedLoadChart.HoverDetailChanged += RecordingChart_HoverDetailChanged;
            App app = Application.Current as App;
            if (app != null) app.ThemeChanged += App_ThemeChanged;
            LoadPortSettings();
            UpdateAppearanceButtons();
            replayTimer.Tick += ReplayTimer_Tick;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;
            AdvancedInstallOptionsExpander.Expanded += AdvancedInstallOptionsExpander_Expanded;
            AdvancedInstallOptionsExpander.Collapsed += AdvancedInstallOptionsExpander_Collapsed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyReferenceWindowSize();
            UpdateScaleFrameLayout();
            WindowBackdrop.Apply(this, ((App)Application.Current).IsDarkTheme);
        }

        private void ApplyReferenceWindowSize()
        {
            Rect workArea = SystemParameters.WorkArea;
            double availableWidth = workArea.Width * DefaultWorkAreaCoverage;
            double availableHeight = workArea.Height * DefaultWorkAreaCoverage;
            double referenceAspect = ReferenceWindowWidth / ReferenceWindowHeight;
            double targetWidth = Math.Min(ReferenceWindowWidth, availableWidth);
            double targetHeight = targetWidth / referenceAspect;
            if (targetHeight > availableHeight)
            {
                targetHeight = availableHeight;
                targetWidth = targetHeight * referenceAspect;
            }

            MinWidth = Math.Min(MinimumWindowWidth, availableWidth);
            MinHeight = Math.Min(MinimumWindowHeight, availableHeight);
            Width = Math.Max(MinWidth, targetWidth);
            Height = Math.Max(MinHeight, targetHeight);
            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2.0);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2.0);
            double compactDashboardHeight = Math.Max(MinHeight, Math.Min(Height, 490.0));
            Height = compactDashboardHeight;
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2.0);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string launchedPackageRoot = FindPackageRoot();
            if (TryRedirectToActivePackage(launchedPackageRoot))
            {
                Close();
                return;
            }
            packageRoot = launchedPackageRoot;
            UninstallApplicationButton.Visibility = IsSetupInstallation() ? Visibility.Visible : Visibility.Collapsed;
            AppendLog(IsAdministrator()
                ? "Launcher started with administrator privileges."
                : "Launcher started. Administrator approval will be requested only when SimHub files are changed.");
            if (packageRoot == null)
            {
                OverallStatusText.Text = "Package files not found";
                AppendLog("ERROR: This launcher must remain inside the bundle's Tools folder.");
                HomeUpdateStatusText.Text = "Run the launcher from the release bundle to install or update files.";
                HomeUpdateStatusText.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
                FitWindowToPage(SettingsPage);
                return;
            }

            try
            {
                packageInfo = updateService.ReadPackageInfo(packageRoot);
                updateService.ConfigureRepository(packageInfo.repository);
                InstallState installState = updateService.ReadInstallState();
                RestoreInstallState(installState);
                UpdateVersionLabels();
            }
            catch (Exception ex)
            {
                CurrentVersionText.Text = "unavailable";
                CurrentEngineVersionText.Text = "unavailable";
                UpdateStatusText.Text = "Updater unavailable: " + ex.Message;
                ApplyUpdateButton.IsEnabled = false;
                AppendLog("WARNING: " + ex.Message);
            }

            AppendLog("Package: " + packageRoot);
            RefreshBundledProfiles();
            RefreshEnvironment();
            FitWindowToPage(SettingsPage);
            if (packageInfo != null) await CheckForUpdatesAsync(showErrors: false);
        }

        private void RefreshEnvironment()
        {
            if (isBusy || packageRoot == null)
            {
                return;
            }

            List<string> profiles = DetectDcsProfiles();
            foreach (string customProfile in customDcsProfiles)
            {
                if (Directory.Exists(customProfile) && !profiles.Contains(customProfile, StringComparer.OrdinalIgnoreCase))
                {
                    profiles.Add(customProfile);
                }
            }

            string previouslySelected = GetSelectedDcsProfiles().FirstOrDefault();
            List<string> orderedProfiles = profiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            if (!string.IsNullOrWhiteSpace(confirmedDcsProfile)
                && !orderedProfiles.Contains(confirmedDcsProfile, StringComparer.OrdinalIgnoreCase))
            {
                confirmedDcsProfile = null;
            }

            DcsProfilesPanel.Children.Clear();
            foreach (string profile in orderedProfiles)
            {
                bool shouldSelect = string.Equals(previouslySelected, profile, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(confirmedDcsProfile, profile, StringComparison.OrdinalIgnoreCase)
                    || (string.IsNullOrWhiteSpace(previouslySelected) && orderedProfiles.Count == 1);
                RadioButton radioButton = new RadioButton
                {
                    Content = new TextBlock { Text = profile, TextTrimming = TextTrimming.CharacterEllipsis },
                    ToolTip = profile,
                    Tag = profile,
                    GroupName = "DcsSavedGamesProfile",
                    IsChecked = shouldSelect,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                radioButton.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
                radioButton.Checked += SelectionChanged;
                DcsProfilesPanel.Children.Add(radioButton);
            }

            string overviewDcsPath = confirmedDcsProfile ?? orderedProfiles.FirstOrDefault();
            OverviewDcsPathText.Text = overviewDcsPath ?? "Not detected";
            DcsSummaryText.Foreground = (Brush)FindResource("TextSecondary");
            SetStatus(DcsPathStatusText, profiles.Count == 0 ? "Profile not detected" : "Profile detected", profiles.Count > 0);

            if (string.IsNullOrWhiteSpace(simHubPath) || !File.Exists(Path.Combine(simHubPath, "SimHubWPF.exe")))
            {
                simHubPath = DetectSimHubPath();
            }

            SimHubPathText.Text = simHubPath ?? "No SimHub installation found.";
            OverviewSimHubPathText.Text = simHubPath ?? "Not detected";
            DcsSummaryText.Text = profiles.Count > 0 && !string.IsNullOrWhiteSpace(simHubPath)
                ? "DCS World and SimHub found"
                : profiles.Count == 0 ? "DCS World not detected" : "SimHub not detected";
            SimHubSummaryText.Text = packageInfo == null ? "Version unavailable" : "Version " + packageInfo.version;
            SimHubSummaryText.Foreground = (Brush)FindResource("TextSecondary");
            SetStatus(SimHubPathStatusText,
                string.IsNullOrWhiteSpace(simHubPath) ? "Destination not detected" : "Destination detected",
                string.IsNullOrWhiteSpace(simHubPath) ? (bool?)false : true);
            RefreshInstallationStatus();
            RefreshRecentFlights();
        }

        private void RefreshInstallationStatus()
        {
            string baselinePath = Path.Combine(packageRoot, "baseline.json");
            SetStatus(BaselineStatusText,
                File.Exists(baselinePath) ? "Baseline captured and ready for comparison" : "Baseline not captured",
                File.Exists(baselinePath));

            List<string> selectedProfiles = GetSelectedDcsProfiles();
            bool dcsProfileConfirmed = IsDcsProfileConfirmed(selectedProfiles);
            if (selectedProfiles.Count != 1)
            {
                SetStatus(HookStatusText, "Select and confirm one DCS profile", null);
                SetStatus(BridgePathStatusText, "Waiting for profile", null);
            }
            else if (!dcsProfileConfirmed)
            {
                SetStatus(HookStatusText, "Confirm the selected DCS profile", null);
                SetStatus(BridgePathStatusText, "Profile confirmation required", null);
            }
            else
            {
                string sourceHook = Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua");
                int installed = 0;
                int different = 0;
                foreach (string profile in selectedProfiles)
                {
                    string target = Path.Combine(profile, "Scripts", "Hooks", "DCSExtraTelemetry.lua");
                    if (!File.Exists(target))
                    {
                        continue;
                    }

                    if (File.Exists(sourceHook) && HashesMatch(sourceHook, target))
                    {
                        installed++;
                    }
                    else
                    {
                        different++;
                    }
                }

                if (different > 0)
                {
                    SetStatus(HookStatusText, "A different DCS Extra Telemetry Hook was detected", null);
                    SetStatus(BridgePathStatusText, "Different Hook detected", null);
                }
                else if (installed == selectedProfiles.Count)
                {
                    SetStatus(HookStatusText, "Installed in the confirmed DCS profile", true);
                    SetStatus(BridgePathStatusText, "Hook installed", true);
                }
                else if (installed > 0)
                {
                    SetStatus(HookStatusText, "Installed in " + installed + " of " + selectedProfiles.Count + " selected profiles", null);
                    SetStatus(BridgePathStatusText, "Partially installed", null);
                }
                else
                {
                    SetStatus(HookStatusText, "Not installed", false);
                    SetStatus(BridgePathStatusText, "Hook not installed", false);
                }
            }

            if (string.IsNullOrWhiteSpace(simHubPath))
            {
                SetStatus(PluginStatusText, "SimHub not found", null);
            }
            else
            {
                string sourcePlugin = Path.Combine(packageRoot, "SimHub", "DcsExtraTelemetry.Plugin.dll");
                string targetPlugin = Path.Combine(simHubPath, "DcsExtraTelemetry.Plugin.dll");
                if (!File.Exists(targetPlugin))
                {
                    SetStatus(PluginStatusText, "Not installed", false);
                }
                else if (File.Exists(sourcePlugin) && HashesMatch(sourcePlugin, targetPlugin))
                {
                    SetStatus(PluginStatusText, "Installed and matches this package", true);
                }
                else
                {
                    SetStatus(PluginStatusText, "A different plugin build is installed", null);
                }
            }

            int profileCount = GetSelectedDcsProfiles().Count;
            bool pathsReady = profileCount == 1 && dcsProfileConfirmed && !string.IsNullOrWhiteSpace(simHubPath);
            bool hookInstalled = pathsReady && AreSelectedDcsHooksInstalled();
            // The SimHub DLL is independently installable. Do not make its
            // status depend on selecting a DCS Saved Games profile: users may
            // legitimately install or refresh the plugin on a SimHub-only PC.
            bool pluginInstalled = IsSimHubPluginInstalled();
            bool fullyInstalled = hookInstalled && pluginInstalled;
            homePathsReady = pathsReady;
            homeFullyInstalled = fullyInstalled;

            HomeRedesignTitle.Text = fullyInstalled ? "Ready to fly" : dcsProfileConfirmed ? "Two steps left" : "Three steps left";
            HomeRedesignSubtitle.Text = fullyInstalled
                ? "Start DCS and fly for two minutes to verify telemetry."
                : dcsProfileConfirmed
                    ? "Install the Hook and plugin, then fly two minutes to verify."
                    : "Choose a DCS profile, install both components, then verify in flight.";
            RedesignStep1StatusText.Text = dcsProfileConfirmed ? "1 · DONE" : "1 · NOW";
            RedesignStep1StatusText.SetResourceReference(TextBlock.ForegroundProperty, dcsProfileConfirmed ? "Success" : "Warning");
            RedesignStep1DetailText.Text = dcsProfileConfirmed ? "Saved Games found" : "Saved Games profile";
            RedesignStep2StatusText.Text = fullyInstalled ? "2 · DONE" : dcsProfileConfirmed ? "2 · NOW" : "2 · NEXT";
            RedesignStep2StatusText.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled ? "Success" : dcsProfileConfirmed ? "Warning" : "TextSecondary");
            RedesignStep3StatusText.Text = "3 · NEXT";
            RedesignStep3StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            RedesignStep4StatusText.Text = "4 · READY";
            RedesignStep4StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            HomeStep1Label.Text = dcsProfileConfirmed ? "1 · DONE" : "1 · NOW";
            HomeStep1Label.SetResourceReference(TextBlock.ForegroundProperty, dcsProfileConfirmed ? "Success" : "Warning");
            HomeStep2Label.Text = fullyInstalled ? "2 · DONE" : dcsProfileConfirmed ? "2 · NOW" : "2 · NEXT";
            HomeStep2Label.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled ? "Success" : dcsProfileConfirmed ? "Warning" : "TextSecondary");
            HomeStep3Label.Text = fullyInstalled ? "3 · VERIFIED" : "3 · NEXT";
            HomeStep3Label.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled ? "Success" : "TextSecondary");
            HomeStep3Detail.Text = fullyInstalled ? "Ready for flight" : "Verify in flight";
            SetStepDisc(HomeStep1Disc, HomeStep1Glyph, 1, dcsProfileConfirmed, !dcsProfileConfirmed);
            SetStepDisc(HomeStep2Disc, HomeStep2Glyph, 2, fullyInstalled, dcsProfileConfirmed && !fullyInstalled);
            SetStepDisc(HomeStep3Disc, HomeStep3Glyph, 3, false, fullyInstalled);
            SetStepDisc(HomeStep4Disc, HomeStep4Glyph, 4, false, false);
            HomeStep1Connector.SetResourceReference(Border.BackgroundProperty, dcsProfileConfirmed ? "Success" : "GlassStroke");
            HomeStep2Connector.SetResourceReference(Border.BackgroundProperty, fullyInstalled ? "Success" : "GlassStroke");
            HomeStep3Connector.SetResourceReference(Border.BackgroundProperty, fullyInstalled ? "Success" : "GlassStroke");
            HomeProfileNameText.Text = dcsProfileConfirmed ? new DirectoryInfo(confirmedDcsProfile).Name : "No DCS profile selected";
            HomeProfilePathText.Text = dcsProfileConfirmed ? confirmedDcsProfile : "Choose a Saved Games profile to continue";
            HomeProfileStateText.Text = dcsProfileConfirmed ? "Confirmed" : "Required";
            HomeProfileStateText.SetResourceReference(TextBlock.ForegroundProperty, dcsProfileConfirmed ? "Success" : "Danger");
            SetStatusLight(HomeProfileIndicator, dcsProfileConfirmed ? "Success" : "Danger", true);
            List<string> selectedProfilesForCard = GetSelectedDcsProfiles();
            bool hasDetectedDcsProfiles = DcsProfilesPanel.Children.OfType<RadioButton>().Any();
            RedesignProfileNameText.Text = dcsProfileConfirmed
                ? new DirectoryInfo(confirmedDcsProfile).Name
                : selectedProfilesForCard.Count == 1
                    ? new DirectoryInfo(selectedProfilesForCard[0]).Name
                    : hasDetectedDcsProfiles ? "Select a profile" : "No profile found";
            RedesignProfilePathText.Text = dcsProfileConfirmed ? new DirectoryInfo(confirmedDcsProfile).Name : "none selected";
            RedesignProfileStateText.Text = HomeProfileStateText.Text;
            RedesignProfileStateText.SetResourceReference(TextBlock.ForegroundProperty, dcsProfileConfirmed ? "Success" : "Danger");
            SetStatusLight(RedesignProfileIndicator, dcsProfileConfirmed ? "Success" : "Danger", true);
            DcsProfileConfirmationText.Visibility = dcsProfileConfirmed ? Visibility.Collapsed : Visibility.Visible;
            ConfirmDcsProfileButton.Visibility = profileCount == 1 && !dcsProfileConfirmed ? Visibility.Visible : Visibility.Collapsed;
            ConfirmDcsProfileButton.IsEnabled = profileCount == 1 && !dcsProfileConfirmed;
            BrowseDcsProfileButton.Content = profileCount == 0 && !hasDetectedDcsProfiles ? "Browse for DCS folder" : "Browse...";
            BrowseDcsProfileButton.Style = (Style)FindResource(profileCount == 0 && !hasDetectedDcsProfiles
                ? "CompactPrimaryButton" : "CompactButton");
            if (profileCount == 0)
                SetStatus(DcsProfileConfirmationText,
                    hasDetectedDcsProfiles ? "Select a profile above to continue." : "Browse to your DCS Saved Games folder to add it.",
                    null);
            else if (!dcsProfileConfirmed)
                SetStatus(DcsProfileConfirmationText, "Confirm this profile to install the DCS Hook.", null);
            HomeSimHubPathText.Text = string.IsNullOrWhiteSpace(simHubPath) ? "Not detected" : simHubPath + (pluginInstalled ? " · plugin installed" : string.Empty);
            HomeSimHubStateText.Text = pluginInstalled ? "Installed" : string.IsNullOrWhiteSpace(simHubPath) ? "Choose..." : "Detected";
            HomeSimHubStateText.SetResourceReference(TextBlock.ForegroundProperty, pluginInstalled ? "Success" : "Warning");
            SetStatusLight(DashboardSimHubIndicator, pluginInstalled ? "Success" : string.IsNullOrWhiteSpace(simHubPath) ? "Danger" : "Warning", true);
            SimHubPathText.Text = string.IsNullOrWhiteSpace(simHubPath) ? "Not detected" : simHubPath;
            RedesignSimHubStateText.Text = string.IsNullOrWhiteSpace(simHubPath) ? "Required" : "Selected";
            RedesignSimHubStateText.SetResourceReference(TextBlock.ForegroundProperty, string.IsNullOrWhiteSpace(simHubPath) ? "Warning" : "Success");
            SetStatusLight(HomeSimHubIndicator, string.IsNullOrWhiteSpace(simHubPath) ? "Warning" : "Success", true);
            SetStatusLight(HomeSavedGamesIndicator, profileCount > 0 ? "Success" : "NeutralStatus", profileCount > 0);
            RedesignSavedGamesPathText.Text = profileCount > 0
                ? new DirectoryInfo(GetSelectedDcsProfiles().First()).Name
                : "DCS profile location";
            RedesignSavedGamesActionButton.Content = profileCount > 0 ? "Found" : "Browse";
            RedesignSavedGamesActionButton.SetResourceReference(Control.ForegroundProperty, profileCount > 0 ? "Success" : "FocusRing");
            HomeHookStateText.Text = hookInstalled ? "Installed · v" + (packageInfo == null ? "current" : packageInfo.version) : dcsProfileConfirmed ? "Install DCS telemetry" : "Choose DCS folder first";
            HomePluginStateText.Text = pluginInstalled ? "Installed · v" + (packageInfo == null ? "current" : packageInfo.version) : string.IsNullOrWhiteSpace(simHubPath) ? "Choose SimHub folder" : "Install SimHub plugin";
            HomeHookStateText.Text = hookInstalled ? "Installed" : "Not installed";
            HomePluginStateText.Text = pluginInstalled ? "Installed" : "Not installed";
            HomeHookStateText.SetResourceReference(TextBlock.ForegroundProperty, hookInstalled ? "Success" : "Danger");
            HomePluginStateText.SetResourceReference(TextBlock.ForegroundProperty, pluginInstalled ? "Success" : "Danger");
            HomeHookInstallButton.Visibility = hookInstalled ? Visibility.Collapsed : Visibility.Visible;
            HomeHookInstalledBadge.Visibility = hookInstalled ? Visibility.Visible : Visibility.Collapsed;
            HomePluginInstallButton.Visibility = pluginInstalled ? Visibility.Collapsed : Visibility.Visible;
            HomePluginInstalledBadge.Visibility = pluginInstalled ? Visibility.Visible : Visibility.Collapsed;
            RedesignHookStateText.Text = hookInstalled ? "Installed" : "Not installed";
            RedesignPluginStateText.Text = pluginInstalled ? "Installed" : "Not installed";
            RedesignHookStateText.SetResourceReference(TextBlock.ForegroundProperty, hookInstalled ? "Success" : "TextPrimary");
            RedesignPluginStateText.SetResourceReference(TextBlock.ForegroundProperty, pluginInstalled ? "Success" : "TextPrimary");
            RedesignHookInstallButton.Content = hookInstalled ? "Installed" : "Install";
            RedesignHookInstallButton.IsEnabled = dcsProfileConfirmed && !hookInstalled;
            RedesignHookUninstallButton.IsEnabled = dcsProfileConfirmed
                && (hookInstalled || HasDifferentDcsHook());
            if (hookInstalled)
                SetStatus(RedesignHookPrerequisiteText, "Installed in the confirmed DCS profile", true);
            else if (!dcsProfileConfirmed)
                SetStatus(RedesignHookPrerequisiteText, "Next: choose and confirm a DCS profile above", null);
            else
                SetStatus(RedesignHookPrerequisiteText, "Ready · target: " + new DirectoryInfo(confirmedDcsProfile).Name, true);
            RedesignPluginInstallButton.Content = pluginInstalled ? "Installed" : "Install plugin";
            RedesignPluginInstallButton.IsEnabled = !string.IsNullOrWhiteSpace(simHubPath) && !pluginInstalled;
            RedesignPluginUninstallButton.IsEnabled = !string.IsNullOrWhiteSpace(simHubPath)
                && (pluginInstalled || HasDifferentSimHubPlugin());
            if (pluginInstalled)
                SetStatus(RedesignPluginPrerequisiteText, "Installed in the selected SimHub folder", true);
            else if (string.IsNullOrWhiteSpace(simHubPath))
                SetStatus(RedesignPluginPrerequisiteText, "Next: choose the SimHub folder above", null);
            else
                SetStatus(RedesignPluginPrerequisiteText, "Ready · target: " + new DirectoryInfo(simHubPath).Name, true);
            HomePackageVersionText.Text = packageInfo == null ? "App release unavailable" : "v" + packageInfo.version;
            HomeGoToFlightsButton.Style = (Style)FindResource(fullyInstalled ? "PrimaryButton" : "SecondaryButton");
            TitleStatusText.Text = fullyInstalled ? "Ready" : pathsReady ? "Waiting for DCS" : "Setup required";
            TitleStatusText.SetResourceReference(TextBlock.ForegroundProperty, fullyInstalled ? "Success" : pathsReady ? "Warning" : "Danger");
            SetStatusLight(TitleStatusIndicator, fullyInstalled ? "Success" : pathsReady ? "Warning" : "Danger", true);
            UpdateSidebarFooter();

            int itemsNeedingAttention = (dcsProfileConfirmed ? 0 : 1)
                + (string.IsNullOrWhiteSpace(simHubPath) ? 1 : 0)
                + (dcsProfileConfirmed && !hookInstalled ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(simHubPath) && !pluginInstalled ? 1 : 0);
            UpdateSetupSummary(fullyInstalled, itemsNeedingAttention);
            if (!fullyInstalled)
            {
                if (!dcsProfileConfirmed)
                    SetStatus(OneClickStatusText, "Start here: choose and confirm one DCS Saved Games profile. Advanced install options are below.", false);
                else if (string.IsNullOrWhiteSpace(simHubPath))
                    SetStatus(OneClickStatusText, "Start here: choose the SimHub installation folder. Advanced install options are below.", false);
                else
                    SetStatus(OneClickStatusText, "Ready to install the DCS Hook and SimHub plugin. Individual controls are in Advanced install options.", null);
            }
            HomeRequiredActionCard.Visibility = fullyInstalled ? Visibility.Collapsed : Visibility.Visible;
            if (string.IsNullOrWhiteSpace(simHubPath))
            {
                HomeRequiredActionTitle.Text = "Choose your SimHub folder";
                HomeRequiredActionDetail.Text = "Choose SimHub to install or refresh the telemetry plugin. A DCS folder is not required for this step.";
                HomeRequiredActionButton.Content = "Choose SimHub folder";
            }
            else if (!pluginInstalled)
            {
                HomeRequiredActionTitle.Text = "Install the SimHub plugin";
                HomeRequiredActionDetail.Text = "This installs only DcsExtraTelemetry.Plugin.dll into SimHub. You can configure DCS later.";
                HomeRequiredActionButton.Content = "Install SimHub plugin";
            }
            else if (!dcsProfileConfirmed)
            {
                HomeRequiredActionTitle.Text = "Choose your DCS Saved Games folder";
                HomeRequiredActionDetail.Text = "The SimHub plugin is installed. Choose DCS only when you are ready to install the separate telemetry Hook.";
                HomeRequiredActionButton.Content = "Choose DCS folder";
            }
            else
            {
                HomeRequiredActionTitle.Text = "Install required telemetry components";
                HomeRequiredActionDetail.Text = "DCS and SimHub locations are ready. Install both components to finish setup.";
                HomeRequiredActionButton.Content = "Finish setup";
            }

            OneClickInstallButton.IsEnabled = true;
            ChooseDcsFolderButton.Visibility = dcsProfileConfirmed ? Visibility.Collapsed : Visibility.Visible;
            ChooseSimHubFolderButton.Visibility = dcsProfileConfirmed && string.IsNullOrWhiteSpace(simHubPath)
                ? Visibility.Visible
                : Visibility.Collapsed;
            OneClickInstallButton.Visibility = Visibility.Visible;
            bool hasInstalledComponents = HasAnyInstalledComponents();
            bool uninstallEnabled = pathsReady && hasInstalledComponents;
            if (fullyInstalled)
            {
                OverviewTitleText.Text = "You're ready to fly";
                OverviewInstallStatusIndicator.Fill = (Brush)FindResource("Success");
                OverviewInstallStatusIndicator.Effect = null;
                SidebarStatusIndicator.Fill = (Brush)FindResource("Success");
                SidebarStatusIndicator.Effect = null;
                SidebarStatusText.Text = "System idle";
                OneClickInstallButton.Content = "Repair setup";
                SetStatus(OneClickStatusText, "DCS Extra Telemetry is installed and ready. Reinstall to repair or refresh the integration.", true);
            }
            else if (pathsReady)
            {
                OverviewTitleText.Text = "Ready to finish setup";
                OverviewInstallStatusIndicator.Fill = (Brush)FindResource("Warning");
                OverviewInstallStatusIndicator.Effect = null;
                SidebarStatusIndicator.Fill = (Brush)FindResource("Success");
                SidebarStatusIndicator.Effect = null;
                SidebarStatusText.Text = "System idle";
                OneClickInstallButton.Content = "Install everything";
                SetStatus(OneClickStatusText, "This will install or update DCS SimHub telemetry components and configure the required settings.", true);
            }
            else
            {
                OverviewTitleText.Text = dcsProfileConfirmed ? "Let's connect SimHub" : "Let's find your DCS folder";
                OverviewInstallStatusIndicator.Fill = (Brush)FindResource("Danger");
                OverviewInstallStatusIndicator.Effect = null;
                SidebarStatusIndicator.Fill = (Brush)FindResource("Danger");
                SidebarStatusIndicator.Effect = null;
                SidebarStatusText.Text = "Setup required";
                OneClickInstallButton.Content = "Install everything";
                SetStatus(OneClickStatusText,
                    dcsProfileConfirmed ? "Waiting for the SimHub installation path" : "Confirm one DCS profile to continue",
                    false);
            }

            if (pathsReady)
            {
                OverallStatusText.Text = "Connected";
                OverallStatusText.Foreground = (Brush)FindResource("TextPrimary");
                OverallStatusIndicator.Fill = (Brush)FindResource("Success");
                OverallStatusIndicator.Effect = null;
            }
            else
            {
                OverallStatusText.Text = dcsProfileConfirmed ? "Choose the SimHub installation path" : "Confirm the DCS install location";
                OverallStatusText.Foreground = (Brush)FindResource("TextPrimary");
                OverallStatusIndicator.Fill = (Brush)FindResource("Warning");
                OverallStatusIndicator.Effect = null;
            }
        }

        private async void OneClickInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: true, requireSimHub: true)) return;

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Repairing HapticFlight Bridge integration", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    string dcsProfile = GetSelectedDcsProfiles().Single();
                    await RunSetupCommandAsync("--setup-install", dcsProfile, simHubPath);
                    AppendLog("DCS and SimHub integration repaired and verified.");
                    RefreshEnvironment();
                });
                return;
            }

            bool replaceDcsHook = HasDifferentDcsHook();
            bool replaceSimHubPlugin = HasDifferentSimHubPlugin();
            if ((replaceDcsHook || replaceSimHubPlugin) && MessageBox.Show(
                    "A different version of one or more DCS Extra Telemetry files is already installed. "
                    + "The installer will back those files up before replacing them. Continue?",
                    "Existing installation detected", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunUiActionAsync("Installing DCS Extra Telemetry", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);

                AppendLog("Step 1 of 5: Verifying package integrity.");
                await Task.Run(() => InstallService.VerifyPackage(packageRoot, AppendLogThreadSafe));

                AppendLog("Step 2 of 5: Capturing safety baseline.");
                string baselinePath = Path.Combine(packageRoot, "baseline.json");
                string dcsProfile = GetSelectedDcsProfiles().Single();
                await Task.Run(() => InstallService.CaptureBaseline(packageRoot, dcsProfile, simHubPath, baselinePath, AppendLogThreadSafe));

                AppendLog("Step 3 of 5: Installing isolated DCS Hook.");
                await Task.Run(() => InstallService.InstallDcsHook(packageRoot, dcsProfile, replaceDcsHook, AppendLogThreadSafe));

                AppendLog("Step 4 of 5: Installing SimHub plugin.");
                await RunNativeSimHubActionAsync(packageRoot, install: true, option: replaceSimHubPlugin);

                AppendLog("Step 5 of 5: Verifying unrelated files are unchanged.");
                await Task.Run(() => InstallService.VerifyBaseline(baselinePath, AppendLogThreadSafe));

                SaveInstallState(packageInfo == null ? null : packageInfo.version, packageRoot);

                MessageBox.Show(
                    "Installation completed successfully. Start SimHub, then start DCS. The new properties will appear under DCSExtra.",
                    "DCS Extra Telemetry installed", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(showErrors: true);
        }

        private async void SignInToGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            SetStatus(UpdateStatusText, "Public beta updates do not require GitHub sign-in.", true);
            await CheckForUpdatesAsync(showErrors: true);
        }

        private async void ApplyUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (availableUpdate == null || packageInfo == null) return;

            if (IsSetupInstallation())
            {
                if (MessageBox.Show("Download and run HapticFlight Bridge Setup " + availableUpdate.Version
                    + "? Close DCS and SimHub before upgrading.", "Install update",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
                await RunUiActionAsync("Downloading Setup update", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    string installer = await updateService.DownloadInstallerAsync(availableUpdate);
                    Process.Start(new ProcessStartInfo { FileName = installer, UseShellExecute = true });
                    Close();
                });
                return;
            }

            if (MessageBox.Show(
                    "Download and install HapticFlight Bridge " + availableUpdate.Version + "? "
                    + "The downloaded ZIP and every packaged file will be verified before the updated launcher opens. "
                    + "If DCS and SimHub are already configured, their installed components will also be updated.",
                    "Install update", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            {
                return;
            }

            UpdateInfo selectedUpdate = availableUpdate;
            await RunUiActionAsync("Updating DCS Extra Telemetry", async () =>
            {
                bool canUpdateInstalledComponents = EnsureSelectionsAvailable(requireDcs: true, requireSimHub: true);
                if (canUpdateInstalledComponents)
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);

                string updatedPackageRoot = await updateService.DownloadAndVerifyAsync(selectedUpdate, AppendLog);

                AppendLog("Step 1: Verifying downloaded package integrity.");
                await Task.Run(() => InstallService.VerifyPackage(updatedPackageRoot, AppendLogThreadSafe));

                if (canUpdateInstalledComponents)
                {
                    AppendLog("Step 2: Capturing a fresh safety baseline.");
                    string baselinePath = Path.Combine(updatedPackageRoot, "baseline.json");
                    string dcsProfile = GetSelectedDcsProfiles().Single();
                    await Task.Run(() => InstallService.CaptureBaseline(updatedPackageRoot, dcsProfile, simHubPath, baselinePath, AppendLogThreadSafe));

                    AppendLog("Step 3: Updating the isolated DCS Hook.");
                    await Task.Run(() => InstallService.InstallDcsHook(updatedPackageRoot, dcsProfile, true, AppendLogThreadSafe));

                    AppendLog("Step 4: Updating the SimHub plugin.");
                    await RunNativeSimHubActionAsync(updatedPackageRoot, install: true, option: true);

                    AppendLog("Step 5: Verifying unrelated files are unchanged.");
                    await Task.Run(() => InstallService.VerifyBaseline(baselinePath, AppendLogThreadSafe));
                }
                else
                {
                    AppendLog("DCS or SimHub setup is incomplete. The application package was updated; installed telemetry components were left unchanged.");
                }

                packageRoot = updatedPackageRoot;
                packageInfo = updateService.ReadPackageInfo(packageRoot);
                updateService.ConfigureRepository(packageInfo.repository);
                availableUpdate = null;
                UpdateVersionLabels();
                ApplyUpdateButton.IsEnabled = false;
                SetStatus(UpdateStatusText, "Updated to " + packageInfo.version, true);
                SaveInstallState(packageInfo.version, packageRoot);

                MessageBox.Show(
                    "Update " + packageInfo.version + " installed successfully. The updated launcher will now open."
                    + (canUpdateInstalledComponents ? " Start SimHub, then DCS." : " Use Repair setup after selecting DCS and SimHub to synchronize installed telemetry components."),
                    "DCS Extra Telemetry updated", MessageBoxButton.OK, MessageBoxImage.Information);
                LaunchPackageLauncher(updatedPackageRoot);
                Close();
            });
        }

        private async void OneClickUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSetupInstallation() && !EnsureSelections(requireDcs: true, requireSimHub: true)) return;

            MessageBoxResult confirmation = MessageBox.Show(
                "Remove DCS Extra Telemetry from DCS and SimHub?\n\n"
                + "This removes only the telemetry Hook and SimHub plugin. Your other DCS mods and files are left in place.",
                "Uninstall DCS Extra Telemetry",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes) return;

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Removing HapticFlight Bridge integration", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    await RunSetupCommandAsync("--setup-uninstall");
                    AppendLog("DCS and SimHub integration removed. Windows Installed apps can remove the application.");
                    RefreshEnvironment();
                });
                return;
            }

            await RunUiActionAsync("Uninstalling DCS Extra Telemetry", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                string dcsProfile = GetSelectedDcsProfiles().Single();

                AppendLog("Removing the SimHub plugin.");
                await RunNativeSimHubActionAsync(packageRoot, install: false, option: false);

                AppendLog("Removing the isolated DCS Hook.");
                await Task.Run(() => InstallService.RemoveDcsHook(packageRoot, dcsProfile, false, AppendLogThreadSafe));

                string baselinePath = Path.Combine(packageRoot, "baseline.json");
                if (File.Exists(baselinePath))
                {
                    // Baseline comparison is informational during uninstall.
                    // Other DCS integrations may legitimately update their own
                    // files after this package was installed; that must never
                    // turn a successful removal of our Hook/DLL into an error.
                    try
                    {
                        AppendLog("Checking whether unrelated files changed since the baseline.");
                        await Task.Run(() => InstallService.VerifyBaseline(baselinePath, AppendLogThreadSafe));
                    }
                    catch (Exception ex)
                    {
                        AppendLog("UNINSTALL NOTICE: Package-owned telemetry files were removed. Unrelated files changed after the baseline and were left untouched: " + ex.Message);
                    }
                }
                else
                {
                    AppendLog("No baseline was present; only package-owned files were removed.");
                }

                MessageBox.Show(
                    "Uninstallation completed. Only the DCS Extra Telemetry Hook and SimHub plugin files were removed.",
                    "DCS Extra Telemetry uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void UninstallApplicationButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSetupInstallation()) return;
            if (MessageBox.Show(
                "Uninstall HapticFlight Bridge from this PC?\n\n"
                + "Windows Setup will also remove the DCS hook and SimHub plugin installed by HapticFlight Bridge. "
                + "Your flight recordings and unrelated DCS or SimHub files will be kept. Close DCS and SimHub first.",
                "Uninstall HapticFlight Bridge", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

            try
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                string uninstaller = SetupIntegration.FindUninstaller(packageRoot);
                Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = uninstaller,
                    WorkingDirectory = packageRoot,
                    UseShellExecute = true
                });
                if (process == null) throw new InvalidOperationException("Windows uninstaller could not start.");
                Close();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async void VerifyPackageButton_Click(object sender, RoutedEventArgs e)
        {
            await RunUiActionAsync("Verifying package", async () =>
            {
                await Task.Run(() => InstallService.VerifyPackage(packageRoot, AppendLogThreadSafe));
            });
        }

        private async void CaptureBaselineButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: true, requireSimHub: true)) return;
            await RunUiActionAsync("Capturing baseline", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                string dcsProfile = GetSelectedDcsProfiles().Single();
                string baselinePath = Path.Combine(packageRoot, "baseline.json");
                await Task.Run(() => InstallService.CaptureBaseline(packageRoot, dcsProfile, simHubPath, baselinePath, AppendLogThreadSafe));
            });
        }

        private async void VerifyBaselineButton_Click(object sender, RoutedEventArgs e)
        {
            await RunUiActionAsync("Verifying baseline", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                string baselinePath = Path.Combine(packageRoot, "baseline.json");
                await Task.Run(() => InstallService.VerifyBaseline(baselinePath, AppendLogThreadSafe));
            });
        }

        private async void InstallDcsHookButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: true, requireSimHub: false)) return;
            bool replaceExisting = HasDifferentDcsHook();
            if (replaceExisting && MessageBox.Show(
                    "A different DCSExtraTelemetry.lua exists. The installer can back it up before replacement. Continue?",
                    "Different project file detected", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Installing DCS Hook", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    await RunSetupCommandAsync("--setup-install-hook", GetSelectedDcsProfiles().Single());
                });
                return;
            }

            await RunUiActionAsync("Installing isolated DCS Hook", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: false);
                string dcsProfile = GetSelectedDcsProfiles().Single();
                await Task.Run(() => InstallService.InstallDcsHook(packageRoot, dcsProfile, replaceExisting, AppendLogThreadSafe));
            });
        }

        private async void RemoveDcsHookButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: true, requireSimHub: false)) return;
            if (MessageBox.Show(
                    "Remove only DCSExtraTelemetry.lua from the selected DCS profile(s)? No directory or third-party file will be removed.",
                    "Remove DCS Hook", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Removing DCS Hook", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    await RunSetupCommandAsync("--setup-remove-hook");
                });
                return;
            }

            await RunUiActionAsync("Removing DCS Hook", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: false);
                string dcsProfile = GetSelectedDcsProfiles().Single();
                await Task.Run(() => InstallService.RemoveDcsHook(packageRoot, dcsProfile, false, AppendLogThreadSafe));
            });
        }

        private void LaunchMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string monitorPath = Path.Combine(packageRoot, "Tools", "DcsExtraTelemetry.Monitor.exe");
                if (!File.Exists(monitorPath)) throw new FileNotFoundException("Monitor is missing.", monitorPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = monitorPath,
                    Arguments = "--port " + recorderPort + " --seconds 300",
                    WorkingDirectory = Path.GetDirectoryName(monitorPath),
                    UseShellExecute = true
                });
                AppendLog("Monitor launched on the recorder mirror. SimHub can remain open.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (recorderProcess != null && !recorderProcess.HasExited) return;
                string monitorPath = Path.Combine(packageRoot, "Tools", "DcsExtraTelemetry.Monitor.exe");
                if (!File.Exists(monitorPath)) throw new FileNotFoundException("Monitor is missing.", monitorPath);

                string recordingsFolder = GetRecordingsFolder();
                Directory.CreateDirectory(recordingsFolder);
                lastRecordingPath = Path.Combine(recordingsFolder,
                    "DcsExtraTelemetry-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl.gz");
                DisposeRecorderStopSignal();
                string stopEventName = "Local\\DcsExtraTelemetry.RecorderStop." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid().ToString("N");
                recorderStopSignal = new EventWaitHandle(false, EventResetMode.ManualReset, stopEventName);
                recorderProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = monitorPath,
                        Arguments = "--port " + recorderPort
                            + " --seconds 86400 --quiet --stop-event " + QuoteArgument(stopEventName)
                            + " --record " + QuoteArgument(lastRecordingPath),
                        WorkingDirectory = Path.GetDirectoryName(monitorPath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    },
                    EnableRaisingEvents = true
                };
                recorderProcess.Exited += RecorderProcess_Exited;
                if (!recorderProcess.Start()) throw new InvalidOperationException("The telemetry recorder could not be started.");

                SetRecorderStatus("Recording to " + Path.GetFileName(lastRecordingPath), "Success");
                StartRecordingButton.IsEnabled = false;
                StopRecordingButton.IsEnabled = true;
                AppendLog("Telemetry recording started alongside SimHub: " + lastRecordingPath);
            }
            catch (Exception ex)
            {
                StopRecorder();
                ShowError(ex);
            }
        }

        private async void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            string completedRecording = lastRecordingPath;
            StopRecorder();
            AppendLog("Telemetry recording stopped" + (string.IsNullOrEmpty(completedRecording) ? "." : ": " + completedRecording));
            if (!string.IsNullOrWhiteSpace(completedRecording) && File.Exists(completedRecording)
                && new FileInfo(completedRecording).Length > 0)
            {
                SetRecorderStatus("Recording stopped · analyzing " + Path.GetFileName(completedRecording), "Warning");
                try
                {
                    await AnalyzeRecordingAsync(completedRecording);
                    OpenRecordingAnalyzer();
                    SetRecorderStatus("Recording analyzed · " + Path.GetFileName(completedRecording), "Success");
                }
                catch (Exception ex)
                {
                    SetRecorderStatus("Recording stopped · analysis failed", "Danger");
                    ShowError(ex);
                }
            }
            else
            {
                SetRecorderStatus("Recording stopped · no DCS packets were captured", "Warning");
            }
        }

        private void OpenRecordingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = GetRecordingsFolder();
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void HomeRequiredActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (homeFullyInstalled) { TelemetryNavButton_Click(sender, e); return; }
            if (string.IsNullOrWhiteSpace(simHubPath)) { ChooseSimHubButton_Click(sender, e); return; }
            if (!IsSimHubPluginInstalled()) { InstallSimHubPluginButton_Click(sender, e); return; }
            if (string.IsNullOrWhiteSpace(confirmedDcsProfile)) { AddDcsProfileButton_Click(sender, e); return; }
            if (homePathsReady) OneClickInstallButton_Click(sender, e);
        }

        private async void ImportRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import DCS telemetry recording",
                Filter = "DCS telemetry recordings (*.jsonl;*.jsonl.gz)|*.jsonl;*.jsonl.gz|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                lastRecordingPath = dialog.FileName;
                await AnalyzeRecordingAsync(dialog.FileName);
                OpenRecordingAnalyzer();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async Task AnalyzeRecordingAsync(string path)
        {
            FlightEmptyState.Visibility = Visibility.Collapsed;
            FlightDashboard.Visibility = Visibility.Visible;
            SetFlightsAnalysisChrome(true);
            FitWindowToPage(TelemetryPage);
            try
            {
                TelemetryAnalysisResult result = await Task.Run(() => TelemetryRecordingAnalyzer.Analyze(path));
                DashboardStats.Remember(path, result);
                lastRecordingPath = path;
                replayTimer.Stop();
                replayPlaying = false;
                replayPositionSeconds = 0;
                UpdateEffectMap(result);
                PopulateRecordingSelector(path);
                PopulateComparisonRecordingSelector();
                RefreshRecentFlights();
                AppendLog("Telemetry recording analyzed: " + path);
            }
            catch
            {
                FlightDashboard.Visibility = Visibility.Collapsed;
                FlightEmptyState.Visibility = Visibility.Visible;
                SetFlightsAnalysisChrome(false);
                FitWindowToPage(TelemetryPage);
                throw;
            }
        }

        private void PopulateRecordingSelector(string selectedPath)
        {
            if (RecordingSelectorComboBox == null) return;
            var paths = new List<string>();
            if (IsUsableRecording(selectedPath)) paths.Add(selectedPath);
            foreach (string folderPath in GetRecordingFoldersForBrowsing())
            {
                try
                {
                    DirectoryInfo folder = new DirectoryInfo(folderPath);
                    if (folder.Exists) paths.AddRange(RecordingFileFormat.EnumerateFiles(folder).Where(file => ReadRecentRecordingInfo(file) != null).OrderByDescending(file => file.LastWriteTimeUtc).Select(file => file.FullName));
                }
                catch (Exception ex)
                {
                    AppendLog("RECORDING SELECTOR: " + ex.Message);
                }
            }
            updatingRecordingSelector = true;
            RecordingSelectorComboBox.Items.Clear();
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var item = new ComboBoxItem { Content = Path.GetFileName(path), Tag = path, ToolTip = path };
                RecordingSelectorComboBox.Items.Add(item);
                if (string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase)) RecordingSelectorComboBox.SelectedItem = item;
            }
            RecordingSelectorComboBox.Visibility = RecordingSelectorComboBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            updatingRecordingSelector = false;
        }

        private async void RecordingSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingRecordingSelector) return;
            var item = RecordingSelectorComboBox.SelectedItem as ComboBoxItem;
            string path = item == null ? null : item.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;
            if (string.Equals(path, lastRecordingPath, StringComparison.OrdinalIgnoreCase) && currentEffectMapResult != null) { OpenRecordingAnalyzer(); return; }
            try { await AnalyzeRecordingAsync(path); OpenRecordingAnalyzer(); }
            catch (Exception ex) { ShowError(ex); }
        }

        private sealed class RecentRecordingInfo
        {
            public string Path { get; set; }
            public string Aircraft { get; set; }
            public string Map { get; set; }
            public DateTime RecordedAt { get; set; }
        }

        private void RefreshRecentFlights()
        {
            if (RecentFlightsPanel == null || RecentFlightsSidebarPanel == null) return;
            RecentFlightsPanel.Children.Clear();
            var recordings = new List<RecentRecordingInfo>();
            try
            {
                var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (string folderPath in GetRecordingFoldersForBrowsing())
                {
                    DirectoryInfo folder = new DirectoryInfo(folderPath);
                    if (!folder.Exists) continue;
                    foreach (FileInfo file in RecordingFileFormat.EnumerateFiles(folder)) files[file.FullName] = file;
                }
                foreach (FileInfo file in files.Values.OrderByDescending(item => item.LastWriteTimeUtc))
                {
                    RecentRecordingInfo info = ReadRecentRecordingInfo(file);
                    if (info != null) recordings.Add(info);
                }
            }
            catch (Exception ex)
            {
                AppendLog("RECENT RECORDINGS: " + ex.Message);
            }

            hasRecentRecordings = recordings.Count > 0;
            PopulateRecordingSelector(lastRecordingPath);
            FlightEmptyStateTitleText.Text = hasRecentRecordings
                ? "Choose a saved flight · " + recordings.Count + " available"
                : "No flights recorded yet";
            FlightEmptyStateDescriptionText.Text = hasRecentRecordings
                ? "All compatible recordings in the recordings folder are listed here. Select one to review it, or start a new recording in DCS."
                : "Start the recorder, fly for a couple of minutes in DCS, then land. The launcher will summarise which bass-shaker effects fired and how hard.";
            if (!suppressDashboardRefresh) RefreshDashboard();
            if (!hasRecentRecordings)
            {
                RecentFlightsHintText.Text = "No saved recordings yet";
                UpdateRecentFlightsVisibility();
                return;
            }

            string search = FlightSearchTextBox == null ? null : FlightSearchTextBox.Text.Trim();
            List<RecentRecordingInfo> shown = string.IsNullOrEmpty(search)
                ? recordings
                : recordings.Where(recording =>
                    recording.Aircraft.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || recording.Map.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            RecentFlightsHintText.Text = string.IsNullOrEmpty(search)
                ? shown.Count + " available " + (shown.Count == 1 ? "flight" : "flights")
                : shown.Count == 0 ? "No flights match \"" + search + "\"" : shown.Count + " matching " + (shown.Count == 1 ? "flight" : "flights");
            foreach (RecentRecordingInfo recording in shown)
            {
                bool selected = string.Equals(recording.Path, lastRecordingPath, StringComparison.OrdinalIgnoreCase) && currentEffectMapResult != null;
                var button = new Button
                {
                    Style = (Style)FindResource(selected ? "ListRowButtonSelected" : "ListRowButton"),
                    Tag = recording.Path,
                    ToolTip = recording.Path,
                    Width = 220,
                    Height = 48,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(7, 4, 7, 4)
                };
                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                // Older recordings lack the aircraft in their first packets; the dashboard's
                // analysis of the full file usually knows it.
                string aircraftName = recording.Aircraft == "Aircraft unknown"
                    ? DashboardStats.CachedAircraft(recording.Path) ?? "Unknown aircraft"
                    : DashboardStats.DisplayAircraft(recording.Aircraft);
                content.Children.Add(CreateAircraftTile(aircraftName, 32));
                var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock
                {
                    Text = aircraftName,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                var detail = new TextBlock
                {
                    Text = (recording.Map == "Map not recorded" ? string.Empty : recording.Map + ", ") + ((App)Application.Current).FormatFlightDateTime(recording.RecordedAt),
                    FontSize = 11,
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                detail.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                text.Children.Add(detail);
                Grid.SetColumn(text, 1);
                content.Children.Add(text);
                button.Content = content;
                button.Click += RecentRecordingButton_Click;
                RecentFlightsPanel.Children.Add(button);
            }
            UpdateRecentFlightsVisibility();
        }

        private void FlightSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Filtering only re-lists recordings; the dashboard numbers do not depend on it.
            suppressDashboardRefresh = true;
            try { RefreshRecentFlights(); }
            finally { suppressDashboardRefresh = false; }
        }

        private void FlightSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ShowPage(TelemetryNavButton, TelemetryPage);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                FlightSearchTextBox.Clear();
                e.Handled = true;
            }
        }

        private static RecentRecordingInfo ReadRecentRecordingInfo(FileInfo file)
        {
            if (file == null || !file.Exists || file.Length == 0) return null;
            string firstPacket = null;
            try
            {
                using (var reader = RecordingFileFormat.OpenText(file.FullName))
                {
                    for (int index = 0; index < 20 && !reader.EndOfStream; index++)
                    {
                        string line = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line) && line.IndexOf("\"dcsType\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            firstPacket = line;
                            break;
                        }
                    }
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(firstPacket)) return null;
            string aircraft = ReadRecentRecordingValue(firstPacket, "dcsType");
            string map = ReadRecentRecordingValue(firstPacket, "map");
            if (string.IsNullOrWhiteSpace(map)) map = ReadRecentRecordingValue(firstPacket, "theater");
            if (string.IsNullOrWhiteSpace(map)) map = ReadRecentRecordingValue(firstPacket, "theatre");
            if (string.IsNullOrWhiteSpace(map)) map = ReadRecentRecordingValue(firstPacket, "terrain");
            return new RecentRecordingInfo
            {
                Path = file.FullName,
                Aircraft = string.IsNullOrWhiteSpace(aircraft) ? "Aircraft unknown" : aircraft,
                Map = string.IsNullOrWhiteSpace(map) ? "Map not recorded" : map,
                RecordedAt = file.LastWriteTime
            };
        }

        private static string ReadRecentRecordingValue(string json, string name)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(name)) return null;
            string token = "\"" + name + "\"";
            int key = json.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (key < 0) return null;
            int colon = json.IndexOf(':', key + token.Length);
            if (colon < 0) return null;
            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;
            if (valueStart >= json.Length || json[valueStart] != '\"') return null;
            valueStart++;
            int valueEnd = json.IndexOf('\"', valueStart);
            return valueEnd > valueStart ? json.Substring(valueStart, valueEnd - valueStart) : null;
        }

        private async void RecentRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string path = button == null ? null : button.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!IsUsableRecording(path))
            {
                RefreshRecentFlights();
                ShowError(new InvalidDataException("This recording is incomplete and cannot be analyzed."));
                return;
            }
            if (string.Equals(path, lastRecordingPath, StringComparison.OrdinalIgnoreCase) && currentEffectMapResult != null) { OpenRecordingAnalyzer(); return; }
            try { await AnalyzeRecordingAsync(path); OpenRecordingAnalyzer(); }
            catch (Exception ex) { ShowError(ex); }
        }

        private static bool IsUsableRecording(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            return ReadRecentRecordingInfo(new FileInfo(path)) != null;
        }

        // The analysis lives on the Flights page; opening a recording just brings that page
        // forward with the analysed flight in view.
        private void OpenRecordingAnalyzer()
        {
            if (currentEffectMapResult == null) return;
            ShowPage(TelemetryNavButton, TelemetryPage);
            RenderFlightsDashboard();
            FitWindowToPage(TelemetryPage);
        }

        private void NewRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnToRecorder();
        }

        internal void ReturnToRecorder()
        {
            replayTimer.Stop();
            replayPlaying = false;
            currentEffectMapResult = null;
            lastRecordingPath = null;
            soloEffectMapTrack = null;
            selectedEffectMapTrack = null;
            FlightDashboard.Visibility = Visibility.Collapsed;
            FlightEmptyState.Visibility = Visibility.Visible;
            SetFlightsAnalysisChrome(false);
            RefreshRecentFlights();
            FitWindowToPage(TelemetryPage);
            MainScrollViewer.ScrollToTop();
            Dispatcher.BeginInvoke(new Action(() => StartRecordingButton.Focus()));
        }

        private void RefreshBundledProfiles()
        {
            BundledProfilesPanel.Children.Clear();
            string profilesPath = new[]
            {
                packageRoot == null ? null : Path.Combine(packageRoot, "Profiles"),
                packageRoot == null ? null : Path.Combine(packageRoot, "profiles"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles")
            }.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

            if (profilesPath == null)
            {
                BundledProfilesPanel.Children.Add(new TextBlock
                {
                    Text = "No bundled SimHub profiles found in this package.",
                    Foreground = (Brush)FindResource("Warning"),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            string[] profileFiles = Directory.GetFiles(profilesPath, "*.siprofile", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (profileFiles.Length == 0)
            {
                BundledProfilesPanel.Children.Add(new TextBlock
                {
                    Text = "No bundled SimHub profiles found in this package.",
                    Foreground = (Brush)FindResource("Warning"),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            string profileVersionToken = packageInfo == null ? null : packageInfo.groundProfileVersion;
            int currentProfileCount = profileFiles.Count(path =>
                string.Equals(Path.GetDirectoryName(path), profilesPath, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(profileVersionToken)
                    ? Path.GetFileName(path).IndexOf("-Realism", StringComparison.OrdinalIgnoreCase) >= 0
                    : Path.GetFileName(path).IndexOf(profileVersionToken, StringComparison.OrdinalIgnoreCase) >= 0));
            int historyProfileCount = profileFiles.Length - currentProfileCount;
            string profileVersion = packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.groundProfileVersion)
                ? "Current profile set"
                : "Haptic Engine v" + packageInfo.groundProfileVersion + " Realism";

            BundledProfilesPanel.Children.Add(new TextBlock
            {
                Text = profileVersion,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            BundledProfilesPanel.Children.Add(new TextBlock
            {
                Text = currentProfileCount + " current aircraft profiles",
                Foreground = (Brush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            if (historyProfileCount > 0)
            {
                BundledProfilesPanel.Children.Add(new TextBlock
                {
                    Text = historyProfileCount + " rollback profiles included",
                    Foreground = (Brush)FindResource("TextSecondary"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
        }

        private void RefreshRecentFlightsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRecentFlights();
        }

        private void UpdateRecentFlightsVisibility()
        {
            if (RecentFlightsSidebarPanel != null)
            {
                bool flightsVisible = TelemetryPage != null && TelemetryPage.Visibility == Visibility.Visible;
            RecentFlightsSidebarPanel.Visibility = flightsVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateEffectMap(TelemetryAnalysisResult result)
        {
            currentEffectMapResult = result;
            replayPositionSeconds = Math.Max(0, Math.Min(replayPositionSeconds, result.EffectMapDurationSeconds));
            hiddenEffectMapTracks.Clear();
            foreach (EffectMapTrack inactive in result.EffectMap.Where(track => track.Episodes.Count == 0)) hiddenEffectMapTracks.Add(inactive.Name);
            soloEffectMapTrack = null;
            HideUnusedEffectsCheckBox.IsChecked = true;
            focusedEffectMapTracks.Clear();
            foreach (EffectMapTrack focusedTrack in result.EffectMap
                .OrderByDescending(track => track.Buckets.Length == 0 ? 0 : track.Buckets.Max() * 0.7 + track.Buckets.Average() * 0.3)
                .Take(8))
                focusedEffectMapTracks.Add(focusedTrack.Name);
            mutedEffectMapTracks.RemoveWhere(name => !result.EffectMap.Any(track => string.Equals(track.Name, name, StringComparison.Ordinal)));
            if (!result.EffectMap.Any(track => string.Equals(track.Name, selectedEffectMapTrack, StringComparison.Ordinal))) selectedEffectMapTrack = null;
            if (selectedEffectMapTrack == null && result.EffectMap.Count > 0)
                selectedEffectMapTrack = result.EffectMap.OrderByDescending(TrackPeak).First().Name;
            UpdatePlaybackUi();
            PopulateAnalysisEffectFilter();
            PopulateTelemetrySignals(result);
            PopulateDebrief(result);
            PopulateComparisonRecordingSelector();
            RenderFlightsDashboard();
        }

        private void RenderFlightsDashboard()
        {
            TelemetryAnalysisResult result = currentEffectMapResult;
            if (result == null) return;
            var renderWatch = Stopwatch.StartNew();
            FlightDashboard.Visibility = Visibility.Visible;
            FlightEmptyState.Visibility = Visibility.Collapsed;
            SetFlightsAnalysisChrome(true);
            string aircraft = string.IsNullOrWhiteSpace(result.AircraftType) ? "Unknown aircraft" : result.AircraftType;
            FlightDashboardTitle.Text = aircraft + " · " + FormatDashboardDuration(result.EffectMapDurationSeconds);
            FlightDashboardMeta.Text = (string.IsNullOrWhiteSpace(lastRecordingPath) ? "Imported recording" : Path.GetFileName(lastRecordingPath)) +
                " · " + result.ValidSamples.ToString("N0") + " valid samples · " + result.EffectMap.Count + " effects";
            UpdatePlaybackUi();
            PopulateEffectIsolationOptions(result);
            HashSet<string> visibleEffectNames = VisibleEffectNames();
            EffectTimelineChart.Result = result;
            EffectTimelineChart.HideUnusedEffects = false;
            EffectTimelineChart.SetVisibleEffects(visibleEffectNames);
            EffectTimelineChart.SelectEffect(soloEffectMapTrack);
            UpdateTimelinePanScrollBar();
            PhaseMatrixChart.Result = result;
            PhaseMatrixChart.HideUnusedEffects = false;
            PhaseMatrixChart.SetVisibleEffects(visibleEffectNames);
            PhaseMatrixChart.SelectEffect(soloEffectMapTrack);
            CombinedLoadChart.Result = result;
            CombinedLoadChart.SetVisibleEffects(visibleEffectNames);
            int activeEffects = result.EffectMap.Count(t => t.Episodes.Count > 0);
            TimelineSummaryText.Text = visibleEffectNames.Count + " of " + result.EffectMap.Count + " effects shown · " + result.FlightPhases.Count + " detected phases";
            PhaseMatrixSummaryText.Text = result.FlightPhases.Select(phase => phase.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() + " phase types detected · shade = peak strength";
            StrongestEffectsPanel.Children.Clear();
            LiveEffectsLegendPanel.Children.Clear();
            replayLegendValues.Clear();
            IList<EffectMapTrack> ranked = string.IsNullOrEmpty(soloEffectMapTrack)
                ? result.EffectMap.Where(track => visibleEffectNames.Contains(track.Name)).OrderByDescending(TrackPeak).Take(8).ToList()
                : result.EffectMap.Where(track => string.Equals(track.Name, soloEffectMapTrack, StringComparison.Ordinal)).ToList();
            for (int rank = 0; rank < ranked.Count; rank++)
            {
                EffectMapTrack track = ranked[rank];
                if (rank < 4) AddLiveLegendRow(track, GetTrackColor(track), IsEffectMapTrackSilent(track.Name));
            }
            PopulateCombinedLoadPanels(result, ranked);
            UpdateAnalysisFilterChips();
            AnalysisEmptyStateText.Visibility = visibleEffectNames.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderDashboardCanvases();
            UpdateSelectedEffectPanel();
            UpdateRecordingCharts();
            RenderTelemetryChart();
            SetAnalysisTab(activeAnalysisTab);
            renderWatch.Stop();
            result.InitialRenderMilliseconds = renderWatch.ElapsedMilliseconds;
        }

        private void SetFlightsAnalysisChrome(bool analysisVisible)
        {
            if (!analysisVisible) ReplayDashboardCard.Visibility = Visibility.Collapsed;
        }

        private void ConfigureCombinedLoadLayout()
        {
            if (LoadTabPanel == null || LoadTabPanel.Children.Count < 2) return;
            LoadTabPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LoadTabPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Border chartCard = LoadTabPanel.Children[0] as Border;
            Border summaryCard = LoadTabPanel.Children[1] as Border;
            if (chartCard == null || summaryCard == null) return;
            Grid.SetRow(chartCard, 0); Grid.SetColumn(chartCard, 0); Grid.SetColumnSpan(chartCard, 3);
            Grid.SetRow(summaryCard, 1); Grid.SetColumn(summaryCard, 2); Grid.SetColumnSpan(summaryCard, 1);
            summaryCard.Margin = new Thickness(0, 12, 0, 0);
            Grid summaryGrid = summaryCard.Child as Grid;
            if (summaryGrid != null)
            {
                TextBlock title = summaryGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (title != null) title.Text = "Flight summary";
                TextBlock subtitle = summaryGrid.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
                if (subtitle != null) subtitle.Text = "Recording totals and peaks";
            }

            loadCategoryActivityPanel = new StackPanel();
            var activityContent = new StackPanel();
            activityContent.Children.Add(new TextBlock { Text = "Category activity", FontSize = 13.5, FontWeight = FontWeights.SemiBold });
            var activitySubtitle = new TextBlock { Text = "Peak per category", FontSize = 10.5, Margin = new Thickness(0, 3, 0, 8) };
            activitySubtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            activityContent.Children.Add(activitySubtitle);
            activityContent.Children.Add(loadCategoryActivityPanel);
            var activityCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 12, 0, 0), Child = activityContent };
            Grid.SetRow(activityCard, 1); Grid.SetColumn(activityCard, 0); Grid.SetColumnSpan(activityCard, 1);
            LoadTabPanel.Children.Add(activityCard);
        }

        private void PopulateCombinedLoadPanels(TelemetryAnalysisResult result, IList<EffectMapTrack> ranked)
        {
            if (loadCategoryActivityPanel == null) return;
            loadCategoryActivityPanel.Children.Clear();
            foreach (string category in RecordingChartPalette.Categories.OrderBy(RecordingChartPalette.DisplayOrder))
            {
                List<EffectMapTrack> tracks = result.EffectMap.Where(track => track.Category == category).ToList();
                if (tracks.Count == 0) continue;
                double peak = tracks.Max(TrackPeak);
                string resourceKey = CategoryResourceKey(category);
                var row = new Grid { Height = 34, Margin = new Thickness(0, 0, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var swatch = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 9, 0) };
                swatch.SetResourceReference(Border.BackgroundProperty, resourceKey);
                labelPanel.Children.Add(swatch);
                labelPanel.Children.Add(new TextBlock { Text = category, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 145 });
                var strip = new UniformGrid { Rows = 1, Columns = 28, Margin = new Thickness(0, 7, 8, 7) };
                for (int cell = 0; cell < 28; cell++)
                {
                    int from = cell * result.ActiveEffectCounts.Length / 28;
                    int to = Math.Max(from + 1, (cell + 1) * result.ActiveEffectCounts.Length / 28);
                    double value = 0;
                    foreach (EffectMapTrack track in tracks)
                        for (int bucket = from; bucket < Math.Min(to, track.Buckets.Length); bucket++) value = Math.Max(value, track.Buckets[bucket]);
                    var bar = new Border { Margin = new Thickness(1, 0, 0, 0), CornerRadius = new CornerRadius(1), Opacity = value < .02 ? .08 : .35 + value * .65 };
                    bar.SetResourceReference(Border.BackgroundProperty, value < .02 ? "ChartLane" : resourceKey);
                    strip.Children.Add(bar);
                }
                var peakText = new TextBlock { Text = (peak * 100).ToString("0") + "%", FontSize = 10.5, FontFamily = new FontFamily("Cascadia Mono, Consolas"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(labelPanel, 0); Grid.SetColumn(strip, 1); Grid.SetColumn(peakText, 2);
                row.Children.Add(labelPanel); row.Children.Add(strip); row.Children.Add(peakText);
                loadCategoryActivityPanel.Children.Add(row);
            }

            AddLoadSummaryRow("Peak concurrency", result.PeakConcurrency + " at " + FormatMapTime(result.PeakConcurrencyTimeSeconds));
            AddLoadSummaryRow("Valid samples", result.ValidSamples.ToString("N0"));
            AddLoadSummaryRow("Effects never fired", result.EffectMap.Count(track => track.Episodes.Count == 0).ToString());
            if (ranked.Count > 0) AddLoadSummaryRow("Strongest effect", ranked[0].Name + "  " + (TrackPeak(ranked[0]) * 100).ToString("0") + "%");
        }

        private void AddLoadSummaryRow(string label, string value)
        {
            var row = new Grid { Height = 30 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labelText = new TextBlock { Text = label, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            var valueText = new TextBlock { Text = value, FontSize = 10.5, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(valueText, 1); row.Children.Add(labelText); row.Children.Add(valueText); StrongestEffectsPanel.Children.Add(row);
        }

        private static string CategoryResourceKey(string category)
        {
            if (category == "Aerodynamics") return "ChartCategoryAirframe";
            if (category == "Ground Interaction") return "ChartCategoryGround";
            if (category == "Weapons and Countermeasures") return "ChartCategoryWeapons";
            return "ChartCategoryRotor";
        }

        private void AddStrongestEffectRow(EffectMapTrack track)
        {
            Color color = RecordingChartPalette.ColorFor(track.Category);
            double activeSeconds = track.ActiveSeconds;
            var grid = new Grid { Height = 34, Tag = track.Name, Cursor = Cursors.Hand };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            var swatch = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center };
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock { Text = track.Name, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis });
            labels.Children.Add(new TextBlock { Text = track.Episodes.Count + " events · " + FormatDashboardDuration(activeSeconds) + " active", FontSize = 9.5, Foreground = (Brush)FindResource("TextSecondary") });
            var peak = new TextBlock { Text = (TrackPeak(track) * 100).ToString("0") + "%", FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(swatch, 0); Grid.SetColumn(labels, 1); Grid.SetColumn(peak, 2); grid.Children.Add(swatch); grid.Children.Add(labels); grid.Children.Add(peak);
            grid.MouseLeftButtonUp += delegate { selectedEffectMapTrack = track.Name; soloEffectMapTrack = string.Equals(soloEffectMapTrack, track.Name, StringComparison.Ordinal) ? null : track.Name; EffectTimelineScrollViewer.ScrollToVerticalOffset(EffectTimelineChart.GetEffectOffset(track.Name)); PopulateEffectIsolationOptions(currentEffectMapResult); RenderFlightsDashboard(); };
            StrongestEffectsPanel.Children.Add(grid);
        }

        private void EffectTimelineChart_EffectSelected(string name)
        {
            soloEffectMapTrack = name;
            if (!string.IsNullOrEmpty(name)) selectedEffectMapTrack = name;
            if (!string.IsNullOrEmpty(name)) EffectTimelineScrollViewer.ScrollToVerticalOffset(EffectTimelineChart.GetEffectOffset(name));
            PopulateEffectIsolationOptions(currentEffectMapResult);
            RenderFlightsDashboard();
        }

        private void RecordingChart_TimeSelected(double seconds)
        {
            replayPositionSeconds = seconds;
            RefreshPlaybackFrame();
        }

        private void RecordingChart_HoverDetailChanged(string detail)
        {
            AnalysisHoverInspectorText.Text = string.IsNullOrWhiteSpace(detail)
                ? "Hover over a chart item to inspect its time, phase, frequency, and intensity."
                : detail;
            AnalysisHoverInspectorText.SetResourceReference(TextBlock.ForegroundProperty, string.IsNullOrWhiteSpace(detail) ? "TextSecondary" : "TextPrimary");
        }

        private void TimelineTabButton_Click(object sender, RoutedEventArgs e) { SetAnalysisTab("Timeline"); }
        private void PhaseTabButton_Click(object sender, RoutedEventArgs e) { SetAnalysisTab("Phases"); }
        private void LoadTabButton_Click(object sender, RoutedEventArgs e) { SetAnalysisTab("Load"); }
        private void TelemetryTabButton_Click(object sender, RoutedEventArgs e) { SetAnalysisTab("Telemetry"); }
        private void DebriefTabButton_Click(object sender, RoutedEventArgs e) { SetAnalysisTab("Debrief"); }
        private void ReplayDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ReplayDashboardCard.Visibility = ReplayDashboardCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetAnalysisTab(string tab)
        {
            activeAnalysisTab = tab;
            TimelineTabPanel.Visibility = tab == "Timeline" ? Visibility.Visible : Visibility.Collapsed;
            PhaseTabPanel.Visibility = tab == "Phases" ? Visibility.Visible : Visibility.Collapsed;
            LoadTabPanel.Visibility = tab == "Load" ? Visibility.Visible : Visibility.Collapsed;
            TelemetryTabPanel.Visibility = tab == "Telemetry" ? Visibility.Visible : Visibility.Collapsed;
            DebriefTabPanel.Visibility = tab == "Debrief" ? Visibility.Visible : Visibility.Collapsed;
            // Debrief is a complete run-level review. The selected-effect card belongs to
            // timeline exploration and made it look as though the debrief contained only one effect.
            AnalysisHoverInspectorCard.Visibility = tab == "Debrief" ? Visibility.Collapsed : Visibility.Visible;
            SelectedEffectReviewCard.Visibility = tab == "Debrief" ? Visibility.Collapsed : Visibility.Visible;
            TimelineTabButton.Style = (Style)FindResource(tab == "Timeline" ? "AnalysisTabButtonActive" : "AnalysisTabButton");
            PhaseTabButton.Style = (Style)FindResource(tab == "Phases" ? "AnalysisTabButtonActive" : "AnalysisTabButton");
            LoadTabButton.Style = (Style)FindResource(tab == "Load" ? "AnalysisTabButtonActive" : "AnalysisTabButton");
            TelemetryTabButton.Style = (Style)FindResource(tab == "Telemetry" ? "AnalysisTabButtonActive" : "AnalysisTabButton");
            DebriefTabButton.Style = (Style)FindResource(tab == "Debrief" ? "AnalysisTabButtonActive" : "AnalysisTabButton");
            if (tab == "Telemetry") RenderTelemetryChart();
            RecordingChart_HoverDetailChanged(null);
        }

        private void PopulateTelemetrySignals(TelemetryAnalysisResult result)
        {
            if (TelemetrySignalComboBox == null) return;
            string selectedName = selectedTelemetrySignal == null ? null : selectedTelemetrySignal.Name;
            updatingTelemetrySignalSelection = true;
            TelemetrySignalComboBox.Items.Clear();
            foreach (TelemetrySignalTrack signal in result.TelemetrySignals)
            {
                var item = new ComboBoxItem { Content = signal.Name + " (" + signal.Unit + ")", Tag = signal, ToolTip = signal.Source };
                TelemetrySignalComboBox.Items.Add(item);
                if (string.Equals(signal.Name, selectedName, StringComparison.Ordinal)) TelemetrySignalComboBox.SelectedItem = item;
            }
            if (TelemetrySignalComboBox.SelectedItem == null && TelemetrySignalComboBox.Items.Count > 0)
                TelemetrySignalComboBox.SelectedIndex = 0;
            selectedTelemetrySignal = (TelemetrySignalComboBox.SelectedItem as ComboBoxItem)?.Tag as TelemetrySignalTrack;
            updatingTelemetrySignalSelection = false;
        }

        private void TelemetrySignalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingTelemetrySignalSelection) return;
            selectedTelemetrySignal = (TelemetrySignalComboBox.SelectedItem as ComboBoxItem)?.Tag as TelemetrySignalTrack;
            RenderTelemetryChart();
        }

        private void NormalizeTelemetryCheckBox_Changed(object sender, RoutedEventArgs e) { RenderTelemetryChart(); }
        private void TelemetryCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { RenderTelemetryChart(); }

        private void TelemetryCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (currentEffectMapResult == null || TelemetryCanvas.ActualWidth <= 1) return;
            double fraction = Math.Max(0, Math.Min(1, e.GetPosition(TelemetryCanvas).X / TelemetryCanvas.ActualWidth));
            replayPositionSeconds = fraction * currentEffectMapResult.EffectMapDurationSeconds;
            RefreshPlaybackFrame();
        }

        private void RenderTelemetryChart()
        {
            if (TelemetryCanvas == null || selectedTelemetrySignal == null || currentEffectMapResult == null || !TelemetryTabPanel.IsVisible) return;
            TelemetryCanvas.Children.Clear();
            double width = TelemetryCanvas.ActualWidth, height = TelemetryCanvas.ActualHeight;
            if (width < 10 || height < 10 || selectedTelemetrySignal.Values == null || selectedTelemetrySignal.Values.Length == 0) return;
            double minimum = selectedTelemetrySignal.Values.Min(), maximum = selectedTelemetrySignal.Values.Max();
            double range = Math.Max(.0001, maximum - minimum);
            bool normalize = NormalizeTelemetryCheckBox.IsChecked == true;
            double rawMinimum = Math.Min(0, minimum), rawRange = Math.Max(.0001, Math.Max(0, maximum) - rawMinimum);
            int points = Math.Min(180, selectedTelemetrySignal.Values.Length);
            var line = new System.Windows.Shapes.Polyline { Stroke = new SolidColorBrush(RecordingChartPalette.ResourceColor("ChartCategoryRotor")), StrokeThickness = 1.7, SnapsToDevicePixels = true };
            for (int point = 0; point < points; point++)
            {
                int start = point * selectedTelemetrySignal.Values.Length / points;
                int end = Math.Max(start + 1, (point + 1) * selectedTelemetrySignal.Values.Length / points);
                double value = 0; int count = 0;
                for (int index = start; index < Math.Min(end, selectedTelemetrySignal.Values.Length); index++) { value += selectedTelemetrySignal.Values[index]; count++; }
                value = count == 0 ? 0 : value / count;
                double y = normalize ? (value - minimum) / range : (value - rawMinimum) / rawRange;
                line.Points.Add(new Point(point * width / Math.Max(1, points - 1), height - 4 - y * Math.Max(1, height - 8)));
            }
            TelemetryCanvas.Children.Add(line);
            double duration = Math.Max(.001, currentEffectMapResult.EffectMapDurationSeconds);
            double cursorX = Math.Max(0, Math.Min(width, replayPositionSeconds / duration * width));
            TelemetryCanvas.Children.Add(new System.Windows.Shapes.Line { X1 = cursorX, X2 = cursorX, Y1 = 0, Y2 = height, Stroke = (Brush)FindResource("ChartCursor"), StrokeThickness = 1.25, IsHitTestVisible = false });
            int cursorBucket = Math.Min(selectedTelemetrySignal.Values.Length - 1, Math.Max(0, (int)Math.Floor(replayPositionSeconds / duration * selectedTelemetrySignal.Values.Length)));
            TelemetrySignalSourceText.Text = selectedTelemetrySignal.Source;
            TelemetryCursorText.Text = FormatMapTime(replayPositionSeconds) + " · " + selectedTelemetrySignal.Name + " " + selectedTelemetrySignal.Values[cursorBucket].ToString("0.##") + " " + selectedTelemetrySignal.Unit + (normalize ? " · normalized view" : "");
        }

        private void PopulateDebrief(TelemetryAnalysisResult result)
        {
            if (DebriefEventsGrid == null) return;
            int effectCount = result.Events.Select(item => item.Effect).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            DebriefSummaryText.Text = result.ValidSamples.ToString("N0") + " samples · " + effectCount + " effects with activity · " + result.Events.Count + " modelled episodes · " + result.FlightPhases.Count + " phase spans.";
            DebriefCoverageText.Text = effectCount + " effects had recorded activity in this flight; the " + result.Events.Count + " rows below are their individual episodes. Start and flight phase locate an episode; peak, active time, and frequency describe the modelled cue, not the SimHub or shaker result.";
            DebriefDurationValueText.Text = FormatDashboardDuration(result.EffectMapDurationSeconds);
            DebriefEffectCoverageValueText.Text = effectCount.ToString();
            DebriefOverlapValueText.Text = result.PeakConcurrency.ToString();
            DebriefOverlapDetailText.Text = "at " + FormatMapTime(result.PeakConcurrencyTimeSeconds) + " · simultaneous effects";
            DebriefStoryText.Text = "This " + FormatDashboardDuration(result.EffectMapDurationSeconds) + " recording moved through " + result.FlightPhases.Count + " estimated flight phases. " + effectCount + " effects showed modelled activity; the greatest stacking was " + result.PeakConcurrency + " effects at " + FormatMapTime(result.PeakConcurrencyTimeSeconds) + ".";
            int peakBucket = result.ActiveEffectCounts == null || result.ActiveEffectCounts.Length == 0
                ? 0
                : Math.Min(result.ActiveEffectCounts.Length - 1, Math.Max(0, (int)Math.Floor(result.PeakConcurrencyTimeSeconds / Math.Max(.001, result.AnalysisBucketSeconds))));
            List<EffectMapTrack> peakContributors = result.EffectMap
                .Where(track => track.Buckets != null && peakBucket < track.Buckets.Length && track.Buckets[peakBucket] >= .02)
                .OrderByDescending(track => track.Buckets[peakBucket])
                .ToList();
            List<EffectMapTrack> candidateCues = peakContributors.OrderBy(track => track.Buckets[peakBucket]).Take(3).ToList();
            string moment = FormatMapTime(result.PeakConcurrencyTimeSeconds);
            if (result.PeakConcurrency >= 3 && candidateCues.Count > 0)
            {
                string candidates = string.Join(", ", candidateCues.Select(track => track.Name));
                string leaders = string.Join(", ", peakContributors.Take(4).Select(track => track.Name + " " + (track.Buckets[peakBucket] * 100).ToString("0") + "%"));
                DebriefRecommendationText.Text = "At " + moment + ", " + result.PeakConcurrency + " cues overlap. Keep the dominant cue intact first; make an A/B copy of the SimHub profile and reduce only the lowest-priority competing cue(s) by 10%, then repeat this maneuver.";
                DebriefContributorsText.Text = "Start with: " + candidates + ". Strongest modelled contributors at this moment: " + leaders + ".";
                debriefExperimentPlan = "HapticFlight Bridge — overlap experiment\r\n\r\n"
                    + "Recording: " + (string.IsNullOrWhiteSpace(lastRecordingPath) ? "Imported recording" : Path.GetFileName(lastRecordingPath)) + "\r\n"
                    + "Moment to inspect: " + moment + "\r\n"
                    + "Modelled overlap: " + result.PeakConcurrency + " active cues\r\n"
                    + "Candidate competing cues: " + candidates + "\r\n"
                    + "Dominant modelled contributors: " + leaders + "\r\n\r\n"
                    + "Experiment: In SimHub, duplicate the current profile. Keep the dominant cue intact and reduce only one candidate competing cue by 10%. Fly the same maneuver, record it, then compare the new flight here. This is a modelled timing/mixing lead, not a shaker-output measurement.";
            }
            else
            {
                DebriefRecommendationText.Text = "No major overlap was detected. Choose one effect you want to improve, make one small SimHub profile change, then repeat the same maneuver and compare the two recordings.";
                DebriefContributorsText.Text = "Use Timeline to pick an effect and Telemetry to confirm the underlying signal before tuning.";
                debriefExperimentPlan = "HapticFlight Bridge — controlled tuning experiment\r\n\r\nChoose one effect, change one SimHub setting, repeat the same maneuver, record it, and compare the result. The analyzer validates telemetry and modelled timing, not physical shaker output.";
            }
            InspectPeakOverlapButton.Content = "Inspect " + moment + " in Timeline";
            InspectPeakOverlapButton.IsEnabled = result.PeakConcurrency > 0;
            DebriefPhaseStoryPanel.Children.Clear();
            foreach (FlightPhaseSpan phase in result.FlightPhases)
            {
                var phaseChip = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(0, 0, 6, 5), ToolTip = FormatMapTime(phase.StartSeconds) + "–" + FormatMapTime(phase.EndSeconds) };
                phaseChip.SetResourceReference(Border.BackgroundProperty, "SecondaryButtonBackground");
                var phaseText = new TextBlock { Text = phase.Name + " · " + FormatDashboardDuration(phase.EndSeconds - phase.StartSeconds), FontSize = 10 };
                phaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                phaseChip.Child = phaseText;
                DebriefPhaseStoryPanel.Children.Add(phaseChip);
            }
            DebriefEventsGrid.ItemsSource = result.Events;
            DebriefFindingsList.Items.Clear();
            foreach (FlightDebriefFinding finding in result.DebriefFindings)
            {
                var row = new TextBlock { Text = finding.Severity + " · " + finding.Text, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
                row.SetResourceReference(TextBlock.ForegroundProperty, finding.Severity == "Info" ? "TextSecondary" : "Warning");
                DebriefFindingsList.Items.Add(row);
            }
            ComparisonSummaryText.Text = "Select a recent recording to compare key metrics.";
        }

        private void DebriefEventsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FlightAnalysisEvent item = DebriefEventsGrid.SelectedItem as FlightAnalysisEvent;
            if (item == null) return;
            replayPositionSeconds = item.StartSeconds;
            selectedEffectMapTrack = item.Effect;
            RefreshPlaybackFrame();
        }

        private void InspectPeakOverlapButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEffectMapResult == null) return;
            replayPositionSeconds = currentEffectMapResult.PeakConcurrencyTimeSeconds;
            SetAnalysisTab("Timeline");
            RefreshPlaybackFrame();
        }

        private void CopyDebriefExperimentButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(debriefExperimentPlan)) return;
            Clipboard.SetText(debriefExperimentPlan);
            AppendLog("Debrief experiment plan copied.");
        }

        private void PopulateComparisonRecordingSelector()
        {
            if (CompareRecordingComboBox == null || RecordingSelectorComboBox == null) return;
            updatingComparisonSelector = true;
            CompareRecordingComboBox.Items.Clear();
            foreach (ComboBoxItem source in RecordingSelectorComboBox.Items.OfType<ComboBoxItem>())
            {
                string path = source.Tag as string;
                if (string.Equals(path, lastRecordingPath, StringComparison.OrdinalIgnoreCase)) continue;
                CompareRecordingComboBox.Items.Add(new ComboBoxItem { Content = source.Content, Tag = path, ToolTip = path });
            }
            CompareRecordingComboBox.Visibility = CompareRecordingComboBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            updatingComparisonSelector = false;
        }

        private async void CompareRecordingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingComparisonSelector || currentEffectMapResult == null) return;
            string path = (CompareRecordingComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;
            ComparisonSummaryText.Text = "Analyzing comparison recording…";
            try
            {
                comparisonAnalysisResult = await Task.Run(() => TelemetryRecordingAnalyzer.Analyze(path));
                ComparisonSummaryText.Text = BuildComparisonSummary(currentEffectMapResult, comparisonAnalysisResult, Path.GetFileName(path));
            }
            catch (Exception ex) { ComparisonSummaryText.Text = "Comparison could not be loaded: " + ex.Message; }
        }

        private static string BuildComparisonSummary(TelemetryAnalysisResult current, TelemetryAnalysisResult baseline, string baselineName)
        {
            return "Compared with " + baselineName + " · duration " + SignedDelta(current.EffectMapDurationSeconds - baseline.EffectMapDurationSeconds, "s")
                + " · touchdown " + SignedDelta((current.MaxTouchdown - baseline.MaxTouchdown) * 100, "%")
                + " · rotor droop " + SignedDelta((current.MaxRotorDroop - baseline.MaxRotorDroop) * 100, "%")
                + " · peak concurrent effects " + SignedDelta(current.PeakConcurrency - baseline.PeakConcurrency, string.Empty) + ".";
        }

        private static string SignedDelta(double value, string suffix) { return (value >= 0 ? "+" : string.Empty) + value.ToString("0.##", CultureInfo.InvariantCulture) + suffix; }

        private void TimelineZoomOutButton_Click(object sender, RoutedEventArgs e) { EffectTimelineChart.SetZoom(EffectTimelineChart.Zoom / 1.5); }
        private void TimelineZoomInButton_Click(object sender, RoutedEventArgs e) { EffectTimelineChart.SetZoom(EffectTimelineChart.Zoom * 1.5); }
        private void TimelineResetButton_Click(object sender, RoutedEventArgs e) { EffectTimelineChart.ResetView(); }

        private void EffectTimelineChart_ViewChanged(object sender, EventArgs e)
        {
            UpdateTimelinePanScrollBar();
        }

        private void TimelinePanScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (EffectTimelineChart == null || TimelinePanScrollBar == null || TimelinePanScrollBar.IsMouseCaptureWithin == false && Math.Abs(EffectTimelineChart.ViewStartSeconds - e.NewValue) < .0001) return;
            EffectTimelineChart.SetViewStart(e.NewValue);
        }

        private void UpdateTimelinePanScrollBar()
        {
            if (EffectTimelineChart == null || TimelinePanScrollBar == null || TimelineWindowText == null) return;
            double duration = EffectTimelineChart.DurationSeconds;
            double visible = EffectTimelineChart.VisibleSecondsInView;
            double maximum = EffectTimelineChart.ScrollableSeconds;
            TimelinePanScrollBar.Minimum = 0;
            TimelinePanScrollBar.Maximum = Math.Max(0, maximum);
            TimelinePanScrollBar.ViewportSize = Math.Max(.001, visible);
            TimelinePanScrollBar.SmallChange = Math.Max(.25, visible * .05);
            TimelinePanScrollBar.LargeChange = Math.Max(1, visible * .8);
            TimelinePanScrollBar.IsEnabled = maximum > .01;
            TimelinePanScrollBar.Value = Math.Min(maximum, EffectTimelineChart.ViewStartSeconds);
            TimelineWindowText.Text = maximum <= .01 ? "Fit" : FormatDashboardDuration(visible) + " view";
        }

        private void UpdateRecordingCharts()
        {
            if (currentEffectMapResult == null) return;
            EffectTimelineChart.CursorSeconds = replayPositionSeconds;
            EffectTimelineChart.SelectEffect(soloEffectMapTrack);
            PhaseMatrixChart.SelectEffect(soloEffectMapTrack);
            CombinedLoadChart.CursorSeconds = replayPositionSeconds;
            int peakConcurrency;
            double peakTime;
            CalculateVisiblePeakConcurrency(out peakConcurrency, out peakTime);
            ConcurrencySummaryText.Text = "Peak concurrency " + peakConcurrency + " visible effects at " + FormatMapTime(peakTime) + " · stacked height shows combined normalized intensity";
        }

        private void CalculateVisiblePeakConcurrency(out int peak, out double peakTime)
        {
            peak = 0;
            peakTime = 0;
            if (currentEffectMapResult == null) return;
            HashSet<string> visible = VisibleEffectNames();
            List<EffectMapTrack> tracks = currentEffectMapResult.EffectMap.Where(track => visible.Contains(track.Name)).ToList();
            int buckets = tracks.Count == 0 ? 0 : tracks.Min(track => track.Buckets.Length);
            for (int bucket = 0; bucket < buckets; bucket++)
            {
                int count = tracks.Count(track => track.Buckets[bucket] >= .02);
                if (count <= peak) continue;
                peak = count;
                peakTime = currentEffectMapResult.EffectMapDurationSeconds * (bucket + .5) / buckets;
            }
        }

        private HashSet<string> VisibleEffectNames()
        {
            var visible = new HashSet<string>(StringComparer.Ordinal);
            if (currentEffectMapResult == null) return visible;
            foreach (EffectMapTrack track in currentEffectMapResult.EffectMap)
                if (!hiddenEffectMapTracks.Contains(track.Name) && (string.IsNullOrEmpty(soloEffectMapTrack) || string.Equals(soloEffectMapTrack, track.Name, StringComparison.Ordinal))) visible.Add(track.Name);
            return visible;
        }

        private void PopulateAnalysisEffectFilter()
        {
            if (AnalysisEffectChecklistPanel == null || currentEffectMapResult == null) return;
            updatingEffectMapFilters = true;
            AnalysisEffectCategoryPanel.Children.Clear();
            AnalysisEffectChecklistPanel.Children.Clear();
            foreach (string category in RecordingChartPalette.Categories)
            {
                List<EffectMapTrack> categoryTracks = currentEffectMapResult.EffectMap.Where(track => track.Category == category).ToList();
                if (categoryTracks.Count == 0) continue;
                int selected = categoryTracks.Count(track => !hiddenEffectMapTracks.Contains(track.Name));
                var categoryCheck = new CheckBox { Content = category + "  ·  " + selected + " / " + categoryTracks.Count, Tag = category, IsThreeState = false, Margin = new Thickness(0, 3, 0, 3), FontWeight = FontWeights.SemiBold };
                categoryCheck.IsChecked = selected == categoryTracks.Count;
                categoryCheck.Click += AnalysisEffectCategoryCheckBox_Click;
                AnalysisEffectCategoryPanel.Children.Add(categoryCheck);
            }
            string search = AnalysisEffectSearchTextBox == null ? string.Empty : (AnalysisEffectSearchTextBox.Text ?? string.Empty).Trim();
            foreach (EffectMapTrack track in currentEffectMapResult.EffectMap.OrderBy(track => track.Name, StringComparer.OrdinalIgnoreCase))
            {
                bool unused = track.Episodes.Count == 0;
                var check = new CheckBox
                {
                    Content = track.Name + (unused ? "  ·  unused in this recording" : ""),
                    Tag = track.Name,
                    IsChecked = !hiddenEffectMapTracks.Contains(track.Name),
                    Margin = new Thickness(0, 3, 0, 3),
                    Opacity = unused ? .62 : 1,
                    Visibility = string.IsNullOrEmpty(search) || track.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ? Visibility.Visible : Visibility.Collapsed,
                    ToolTip = track.Category + " · " + track.Frequency
                };
                check.Checked += AnalysisEffectCheckBox_Changed;
                check.Unchecked += AnalysisEffectCheckBox_Changed;
                AnalysisEffectChecklistPanel.Children.Add(check);
            }
            updatingEffectMapFilters = false;
            UpdateAnalysisFilterUi();
        }

        private void ApplyAnalysisEffectFilter()
        {
            if (!string.IsNullOrEmpty(soloEffectMapTrack) && hiddenEffectMapTracks.Contains(soloEffectMapTrack)) soloEffectMapTrack = null;
            PopulateAnalysisEffectFilter();
            RenderFlightsDashboard();
        }

        private void AnalysisEffectsFilterButton_Click(object sender, RoutedEventArgs e) { AnalysisEffectsFilterPopup.IsOpen = !AnalysisEffectsFilterPopup.IsOpen; }
        private void AnalysisEffectsDoneButton_Click(object sender, RoutedEventArgs e) { AnalysisEffectsFilterPopup.IsOpen = false; }
        private void AnalysisEffectsResetButton_Click(object sender, RoutedEventArgs e) { ShowOnlyEffectGroup(active: true, unused: false); }
        private void AnalysisEffectsActiveButton_Click(object sender, RoutedEventArgs e) { ShowOnlyEffectGroup(active: true, unused: false); }
        private void AnalysisEffectsUnusedButton_Click(object sender, RoutedEventArgs e) { ShowOnlyEffectGroup(active: false, unused: true); }
        private void AnalysisEffectsAllButton_Click(object sender, RoutedEventArgs e) { ShowOnlyEffectGroup(active: true, unused: true); }
        private void AnalysisEffectsClearButton_Click(object sender, RoutedEventArgs e) { ShowOnlyEffectGroup(active: false, unused: false); }

        private void ShowOnlyEffectGroup(bool active, bool unused)
        {
            if (currentEffectMapResult == null) return;
            hiddenEffectMapTracks.Clear();
            foreach (EffectMapTrack track in currentEffectMapResult.EffectMap)
                if ((track.Episodes.Count > 0 && !active) || (track.Episodes.Count == 0 && !unused)) hiddenEffectMapTracks.Add(track.Name);
            HideUnusedEffectsCheckBox.IsChecked = active && !unused;
            soloEffectMapTrack = null;
            ApplyAnalysisEffectFilter();
        }

        private void AnalysisEffectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (updatingEffectMapFilters) return;
            var check = sender as CheckBox;
            string name = check == null ? null : check.Tag as string;
            if (string.IsNullOrEmpty(name)) return;
            if (check.IsChecked == true) hiddenEffectMapTracks.Remove(name); else hiddenEffectMapTracks.Add(name);
            HideUnusedEffectsCheckBox.IsChecked = currentEffectMapResult != null && currentEffectMapResult.EffectMap.Where(track => track.Episodes.Count == 0).All(track => hiddenEffectMapTracks.Contains(track.Name));
            ApplyAnalysisEffectFilter();
        }

        private void AnalysisEffectCategoryCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (updatingEffectMapFilters || currentEffectMapResult == null) return;
            var check = sender as CheckBox;
            string category = check == null ? null : check.Tag as string;
            if (string.IsNullOrEmpty(category)) return;
            bool show = check.IsChecked != false;
            foreach (EffectMapTrack track in currentEffectMapResult.EffectMap.Where(track => track.Category == category))
                if (show) hiddenEffectMapTracks.Remove(track.Name); else hiddenEffectMapTracks.Add(track.Name);
            ApplyAnalysisEffectFilter();
        }

        private void AnalysisEffectSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingEffectMapFilters || currentEffectMapResult == null) return;
            PopulateAnalysisEffectFilter();
        }

        private void UpdateAnalysisFilterUi()
        {
            if (currentEffectMapResult == null) return;
            int total = currentEffectMapResult.EffectMap.Count;
            int active = currentEffectMapResult.EffectMap.Count(track => track.Episodes.Count > 0);
            int visible = VisibleEffectNames().Count;
            AnalysisEffectsFilterButton.Content = string.IsNullOrEmpty(soloEffectMapTrack) ? "Effects: " + visible + " / " + total : "Isolated: " + soloEffectMapTrack;
            AnalysisEffectFilterCountText.Text = visible + " shown · " + active + " active · " + (total - active) + " unused";
        }

        private void UpdateAnalysisFilterChips()
        {
            if (AnalysisFilterChipsPanel == null || currentEffectMapResult == null) return;
            AnalysisFilterChipsPanel.Children.Clear();
            if (!string.IsNullOrEmpty(soloEffectMapTrack)) AddAnalysisFilterChip("Isolated: " + soloEffectMapTrack);
            int hiddenUnused = currentEffectMapResult.EffectMap.Count(track => track.Episodes.Count == 0 && hiddenEffectMapTracks.Contains(track.Name));
            if (hiddenUnused > 0) AddAnalysisFilterChip(hiddenUnused + " unused hidden");
            int hiddenActive = currentEffectMapResult.EffectMap.Count(track => track.Episodes.Count > 0 && hiddenEffectMapTracks.Contains(track.Name));
            if (hiddenActive > 0) AddAnalysisFilterChip(hiddenActive + " active hidden");
            if (hiddenEffectMapTracks.Count > 0 || !string.IsNullOrEmpty(soloEffectMapTrack))
            {
                var clear = new Button { Content = "Clear filters", Style = (Style)FindResource("CompactButton"), Margin = new Thickness(0, 0, 0, 4) };
                clear.Click += AnalysisEffectsResetButton_Click;
                AnalysisFilterChipsPanel.Children.Add(clear);
            }
            UpdateAnalysisFilterUi();
        }

        private void AddAnalysisFilterChip(string text)
        {
            var border = new Border { Style = (Style)FindResource("FilterChip") };
            border.Child = new TextBlock { Text = text, FontSize = 10.5 };
            AnalysisFilterChipsPanel.Children.Add(border);
        }

        private void PopulateEffectIsolationOptions(TelemetryAnalysisResult result)
        {
            if (EffectIsolationComboBox == null || result == null) return;
            updatingEffectMapFilters = true;
            EffectIsolationComboBox.Items.Clear();
            EffectIsolationComboBox.Items.Add("All effects");
            bool hideUnused = HideUnusedEffectsCheckBox != null && HideUnusedEffectsCheckBox.IsChecked == true;
            foreach (EffectMapTrack track in result.EffectMap.Where(track => !hideUnused || track.Episodes.Count > 0).OrderBy(track => track.Name, StringComparer.OrdinalIgnoreCase))
                EffectIsolationComboBox.Items.Add(track.Name);
            EffectIsolationComboBox.SelectedItem = string.IsNullOrEmpty(soloEffectMapTrack) ? "All effects" : soloEffectMapTrack;
            updatingEffectMapFilters = false;
        }

        private void EffectIsolationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingEffectMapFilters || currentEffectMapResult == null) return;
            string selected = EffectIsolationComboBox.SelectedItem as string;
            soloEffectMapTrack = string.IsNullOrWhiteSpace(selected) || string.Equals(selected, "All effects", StringComparison.Ordinal)
                ? null
                : selected;
            if (!string.IsNullOrEmpty(soloEffectMapTrack)) selectedEffectMapTrack = soloEffectMapTrack;
            if (!string.IsNullOrEmpty(soloEffectMapTrack)) EffectTimelineScrollViewer.ScrollToVerticalOffset(EffectTimelineChart.GetEffectOffset(soloEffectMapTrack));
            RenderFlightsDashboard();
        }

        private void HideUnusedEffectsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (EffectTimelineChart == null || PhaseMatrixChart == null) return;
            bool hideUnused = HideUnusedEffectsCheckBox.IsChecked == true;
            if (hideUnused && currentEffectMapResult != null && !string.IsNullOrEmpty(soloEffectMapTrack))
            {
                EffectMapTrack selected = currentEffectMapResult.EffectMap.FirstOrDefault(track => string.Equals(track.Name, soloEffectMapTrack, StringComparison.Ordinal));
                if (selected != null && selected.Episodes.Count == 0) soloEffectMapTrack = null;
            }
            EffectTimelineChart.HideUnusedEffects = hideUnused;
            PhaseMatrixChart.HideUnusedEffects = hideUnused;
            PopulateEffectIsolationOptions(currentEffectMapResult);
            if (currentEffectMapResult != null)
            {
                int visible = hideUnused ? currentEffectMapResult.EffectMap.Count(track => track.Episodes.Count > 0) : currentEffectMapResult.EffectMap.Count;
                TimelineSummaryText.Text = visible + " effects shown · " + currentEffectMapResult.FlightPhases.Count + " detected phases";
            }
            UpdateRecordingCharts();
        }

        private static double TrackPeak(EffectMapTrack track) { return track == null ? 0 : track.PeakIntensity; }
        private static string FormatDashboardDuration(double seconds) { return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss"); }

        private void AddLiveLegendRow(EffectMapTrack track, Color color, bool silent)
        {
            var grid = new Grid { Height = 15, Opacity = silent ? .4 : 1 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            var swatch = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = track.Name, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var value = new TextBlock { Text = (ValueAtPlayback(track) * 100).ToString("0") + "%", FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(swatch, 0); Grid.SetColumn(name, 1); Grid.SetColumn(value, 2); grid.Children.Add(swatch); grid.Children.Add(name); grid.Children.Add(value);
            LiveEffectsLegendPanel.Children.Add(grid);
            replayLegendValues[track.Name] = value;
        }

        private void DashboardCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { RenderDashboardCanvases(); }
        private Color GetTrackColor(EffectMapTrack track)
        {
            return RecordingChartPalette.ColorFor(track.Category);
        }

        private void RenderDashboardCanvases()
        {
            if (currentEffectMapResult == null || !FlightDashboard.IsVisible) return;
            RenderLiveEffects();
            EffectMapTrack selected = currentEffectMapResult.EffectMap.FirstOrDefault(t => string.Equals(t.Name, selectedEffectMapTrack, StringComparison.Ordinal));
            if (selected != null) RenderHeatStrip(SelectedEffectCanvas, selected, GetTrackColor(selected), 32);
        }

        private void RenderLiveEffects()
        {
            double width = LiveEffectsCanvas.ActualWidth;
            if (width < 10 || currentEffectMapResult.EffectMap.Count == 0) return;
            const int columns = 92;
            while (replayBars.Count < columns)
            {
                var rectangle = new System.Windows.Shapes.Rectangle { RadiusX = 1, RadiusY = 1 };
                replayBars.Add(rectangle);
                LiveEffectsCanvas.Children.Add(rectangle);
            }
            double cellWidth = Math.Max(1, width / columns - 1);
            double duration = Math.Max(0.001, currentEffectMapResult.EffectMapDurationSeconds);
            double windowStart, windowEnd;
            GetPlaybackWindow(out windowStart, out windowEnd);
            double windowSpan = Math.Max(0.001, windowEnd - windowStart);
            for (int column = 0; column < columns; column++)
            {
                EffectMapTrack dominant = null;
                double value = 0;
                double sampleTime = windowStart + ((column + .5) / columns) * windowSpan;
                foreach (EffectMapTrack track in currentEffectMapResult.EffectMap)
                {
                    if (track.Buckets == null || track.Buckets.Length == 0 || IsEffectMapTrackSilent(track.Name)) continue;
                    int bucket = Math.Min(track.Buckets.Length - 1, Math.Max(0, (int)Math.Floor(sampleTime / duration * track.Buckets.Length)));
                    if (track.Buckets[bucket] > value) { value = track.Buckets[bucket]; dominant = track; }
                }
                System.Windows.Shapes.Rectangle bar = replayBars[column];
                if (dominant == null) { bar.Visibility = Visibility.Collapsed; continue; }
                bar.Visibility = Visibility.Visible;
                double barHeight = Math.Max(2, value * LiveEffectsCanvas.Height);
                bar.Width = cellWidth;
                bar.Height = barHeight;
                if (!string.Equals(bar.Tag as string, dominant.Name, StringComparison.Ordinal))
                {
                    SolidColorBrush brush;
                    if (!replayBarBrushes.TryGetValue(dominant.Name, out brush))
                    {
                        brush = new SolidColorBrush(GetTrackColor(dominant));
                        brush.Freeze();
                        replayBarBrushes[dominant.Name] = brush;
                    }
                    bar.Fill = brush;
                    bar.Tag = dominant.Name;
                }
                bar.Opacity = .35 + value * .6;
                bar.ToolTip = dominant.Name + " · " + FormatMapTime(sampleTime) + " · " + (value * 100).ToString("0") + "%";
                Canvas.SetLeft(bar, column * width / columns); Canvas.SetTop(bar, LiveEffectsCanvas.Height - barHeight);
            }
        }

        private void RenderHeatStrip(Canvas canvas, EffectMapTrack track, Color color, double height)
        {
            canvas.Children.Clear(); double width = canvas.ActualWidth;
            if (width < 5 || track.Buckets == null || track.Buckets.Length == 0) return;
            const int cells = 62;
            for (int i = 0; i < cells; i++)
            {
                int bucket = Math.Min(track.Buckets.Length - 1, (int)Math.Floor((double)i / cells * track.Buckets.Length));
                double value = track.Buckets[bucket];
                var rect = new System.Windows.Shapes.Rectangle { Width = Math.Max(1, width / cells - 1), Height = height, Fill = new SolidColorBrush(color), Opacity = value < .03 ? 0 : .18 + value * .82, ToolTip = track.Name + " · " + FormatMapTime(currentEffectMapResult.EffectMapDurationSeconds * i / cells) + " · " + (value * 100).ToString("0") + "%" };
                Canvas.SetLeft(rect, i * width / cells); canvas.Children.Add(rect);
            }
            if (currentEffectMapResult.EffectMapDurationSeconds > 0)
            {
                var cursor = new System.Windows.Shapes.Line { X1 = 0, X2 = 0, Y1 = 0, Y2 = height, Stroke = (Brush)FindResource("Primary"), StrokeThickness = 1.5, IsHitTestVisible = false };
                Canvas.SetLeft(cursor, width * replayPositionSeconds / currentEffectMapResult.EffectMapDurationSeconds);
                canvas.Children.Add(cursor);
            }
        }

        private void UpdateSelectedEffectPanel()
        {
            if (currentEffectMapResult == null) return;
            EffectMapTrack track = currentEffectMapResult.EffectMap.FirstOrDefault(t => string.Equals(t.Name, selectedEffectMapTrack, StringComparison.Ordinal));
            if (track == null) return;
            double peak = TrackPeak(track);
            int bursts = track.Buckets == null ? 0 : track.Buckets.Count(v => v >= Math.Max(.1, peak * .55));
            SelectedEffectSwatch.Background = new SolidColorBrush(GetTrackColor(track));
            SelectedEffectNameText.Text = track.Name;
            SelectedEffectStatsText.Text = track.Frequency + " · peak " + (peak * 100).ToString("0") + "% · " + bursts + " active spans";
            SelectedEffectPropertyText.Text = "Modelled episode — inspect the matching effect in your selected SimHub profile.";
            TuningEvidenceText.Text = track.Episodes.Count + " modelled event" + (track.Episodes.Count == 1 ? "" : "s") +
                " · peak " + (peak * 100).ToString("0") + "% · " + FormatDashboardDuration(track.ActiveSeconds) + " active";
            TuningBoundaryText.Text = "Use this as timing and overlap evidence. SimHub renders the output; validate any profile change on the physical device.";
            UpdateSelectedEffectNavigation();
        }

        private double ValueAtPlayback(EffectMapTrack track)
        {
            if (track == null || track.Buckets == null || track.Buckets.Length == 0 || currentEffectMapResult == null) return 0;
            double duration = Math.Max(.001, currentEffectMapResult.EffectMapDurationSeconds);
            double position = Math.Max(0, Math.Min(replayPositionSeconds, duration));
            int index = Math.Min(track.Buckets.Length - 1, Math.Max(0, (int)Math.Floor(position / duration * track.Buckets.Length)));
            return track.Buckets[index];
        }

        private void GetPlaybackWindow(out double start, out double end)
        {
            double duration = currentEffectMapResult == null ? 0 : Math.Max(0, currentEffectMapResult.EffectMapDurationSeconds);
            if (duration <= 30)
            {
                start = 0;
                end = duration;
                return;
            }
            start = Math.Max(0, Math.Min(replayPositionSeconds - 15, duration - 30));
            end = Math.Min(duration, start + 30);
        }

        private void UpdatePlaybackUi()
        {
            double duration = currentEffectMapResult == null ? 0 : Math.Max(0, currentEffectMapResult.EffectMapDurationSeconds);
            replayPositionSeconds = Math.Max(0, Math.Min(replayPositionSeconds, duration));

            DashboardModeTitleText.Text = "Recording replay";
            DashboardModeStatusText.Text = replayPlaying ? "PLAYING" : "PAUSED";
            string statusBrush = replayPlaying ? "Success" : "Warning";
            DashboardModeStatusText.SetResourceReference(TextBlock.ForegroundProperty, statusBrush);
            DashboardModeDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, statusBrush);
            ReplayControlsPanel.Visibility = Visibility.Visible;
            ReplayPlayPauseButton.Content = replayPlaying ? "Pause" : "Play";

            updatingReplaySlider = true;
            ReplayPositionSlider.Maximum = Math.Max(1, duration);
            ReplayPositionSlider.Value = replayPositionSeconds;
            updatingReplaySlider = false;
            ReplayPositionText.Text = FormatMapTime(replayPositionSeconds) + " / " + FormatMapTime(duration);

            double start, end;
            GetPlaybackWindow(out start, out end);
            ReplayWindowStartText.Text = FormatMapTime(start);
            ReplayWindowMiddleText.Text = FormatMapTime(start + (end - start) / 2);
            ReplayWindowEndText.Text = FormatMapTime(end);
        }

        private void RefreshPlaybackFrame()
        {
            if (currentEffectMapResult == null) { UpdatePlaybackUi(); return; }
            UpdatePlaybackUi();
            foreach (KeyValuePair<string, TextBlock> item in replayLegendValues)
            {
                EffectMapTrack track = currentEffectMapResult.EffectMap.FirstOrDefault(candidate => candidate.Name == item.Key);
                if (track != null) item.Value.Text = (ValueAtPlayback(track) * 100).ToString("0") + "%";
            }
            RenderLiveEffects();
            UpdateRecordingCharts();
        }

        private void ReplayTimer_Tick(object sender, EventArgs e)
        {
            if (!replayPlaying || currentEffectMapResult == null) return;
            replayPositionSeconds = Math.Min(currentEffectMapResult.EffectMapDurationSeconds, replayPositionSeconds + replayTimer.Interval.TotalSeconds);
            if (replayPositionSeconds >= currentEffectMapResult.EffectMapDurationSeconds)
            {
                replayPlaying = false;
                replayTimer.Stop();
            }
            RefreshPlaybackFrame();
        }

        private void ReplayPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEffectMapResult == null || currentEffectMapResult.EffectMapDurationSeconds <= 0) return;
            if (replayPositionSeconds >= currentEffectMapResult.EffectMapDurationSeconds) replayPositionSeconds = 0;
            replayPlaying = !replayPlaying;
            if (replayPlaying) replayTimer.Start(); else replayTimer.Stop();
            RefreshPlaybackFrame();
        }

        private void ReplayBackButton_Click(object sender, RoutedEventArgs e)
        {
            replayPositionSeconds = Math.Max(0, replayPositionSeconds - 10);
            RefreshPlaybackFrame();
        }

        private void ReplayForwardButton_Click(object sender, RoutedEventArgs e)
        {
            double duration = currentEffectMapResult == null ? 0 : currentEffectMapResult.EffectMapDurationSeconds;
            replayPositionSeconds = Math.Min(duration, replayPositionSeconds + 10);
            RefreshPlaybackFrame();
        }

        private void ReplayPositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingReplaySlider || currentEffectMapResult == null) return;
            replayPositionSeconds = e.NewValue;
            RefreshPlaybackFrame();
        }

        private void SetRecorderStatus(string text, string resourceName)
        {
            RecordingStatusText.Text = text;
            RecordingStatusText.SetResourceReference(TextBlock.ForegroundProperty, resourceName);
        }

        private EffectMapTrack SelectedEffectTrack()
        {
            return currentEffectMapResult == null ? null : currentEffectMapResult.EffectMap.FirstOrDefault(track => string.Equals(track.Name, selectedEffectMapTrack, StringComparison.Ordinal));
        }

        private void CopySimHubHandoffButton_Click(object sender, RoutedEventArgs e)
        {
            EffectMapTrack track = SelectedEffectTrack();
            if (track == null) return;
            Clipboard.SetText(
                "HapticFlight Bridge — SimHub test handoff\r\n" +
                "Effect: " + track.Name + "\r\n" +
                "Modelled peak: " + (TrackPeak(track) * 100).ToString("0") + "%\r\n" +
                "Modelled activity: " + FormatDashboardDuration(track.ActiveSeconds) + " across " + track.Episodes.Count + " event(s)\r\n\r\n" +
                "Open the matching effect in your active SimHub ShakeIt profile. Use SimHub's own preview/test control if it is available, then fly the same controlled scenario. HapticFlight Bridge does not trigger the effect, write profile values, or measure shaker output.");
            AppendLog("SimHub test handoff copied for " + track.Name + ".");
        }

        private void CopyTuningWorksheetButton_Click(object sender, RoutedEventArgs e)
        {
            EffectMapTrack track = SelectedEffectTrack();
            if (track == null || currentEffectMapResult == null) return;
            string recording = string.IsNullOrWhiteSpace(lastRecordingPath) ? "Imported recording" : Path.GetFileName(lastRecordingPath);
            Clipboard.SetText(
                "# HapticFlight profile tuning worksheet\r\n\r\n" +
                "## Baseline\r\n" +
                "- Recording: " + recording + "\r\n" +
                "- Aircraft: " + currentEffectMapResult.AircraftType + "\r\n" +
                "- Effect under review: " + track.Name + "\r\n" +
                "- Modelled evidence: " + track.Episodes.Count + " event(s), " + (TrackPeak(track) * 100).ToString("0") + "% peak, " + FormatDashboardDuration(track.ActiveSeconds) + " active\r\n" +
                "- Flight phase / time: inspect the selected event in Debrief\r\n\r\n" +
                "## One SimHub change\r\n" +
                "- Profile and effect label: ______________________________\r\n" +
                "- Single parameter changed: ______________________________\r\n" +
                "- Old value → new value: _________________________________\r\n" +
                "- Expected result: ________________________________________\r\n\r\n" +
                "## Physical validation\r\n" +
                "- Same DCS scenario repeated: yes / no\r\n" +
                "- SimHub preview or mission observation: _________________\r\n" +
                "- Seat/shaker result (strength, timing, harshness, overlap):\r\n" +
                "  _________________________________________________________\r\n" +
                "- Retest recording: ______________________________________\r\n\r\n" +
                "Compare the baseline and retest in Debrief. The analyzer validates telemetry and model timing only; it does not measure SimHub mixing or physical output.");
            AppendLog("A/B tuning worksheet copied for " + track.Name + ".");
        }

        private void TelemetryLogToggle_Click(object sender, RoutedEventArgs e)
        {
            TelemetryLogDrawer.IsOpen = !TelemetryLogDrawer.IsOpen;
            TelemetryLogToggle.Content = TelemetryLogDrawer.IsOpen ? "Telemetry log ▴" : "Telemetry log";
        }

        private void CloseTelemetryLogDrawerButton_Click(object sender, RoutedEventArgs e)
        {
            TelemetryLogDrawer.IsOpen = false;
            TelemetryLogToggle.Content = "Telemetry log";
        }

        private bool IsEffectMapTrackSilent(string name)
        {
            return mutedEffectMapTracks.Contains(name) ||
                (!string.IsNullOrEmpty(soloEffectMapTrack) && !string.Equals(soloEffectMapTrack, name, StringComparison.Ordinal));
        }

        private static string FormatMapTime(double seconds) { return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss"); }

        private void RecorderProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                recorderProcess = null;
                DisposeRecorderStopSignal();
                SetRecorderStatus("Recorder stopped", "TextSecondary");
                StartRecordingButton.IsEnabled = true;
                StopRecordingButton.IsEnabled = false;
            }));
        }

        private void StopRecorder()
        {
            Process process = recorderProcess;
            recorderProcess = null;
            if (process != null)
            {
                process.Exited -= RecorderProcess_Exited;
                if (!process.HasExited)
                {
                    if (recorderStopSignal != null) recorderStopSignal.Set();
                    process.WaitForExit(5000);
                    if (!process.HasExited) process.Kill();
                }
                process.Dispose();
            }
            DisposeRecorderStopSignal();
            if (RecordingStatusText != null)
            {
                SetRecorderStatus("Recorder stopped", "TextSecondary");
                StartRecordingButton.IsEnabled = true;
                StopRecordingButton.IsEnabled = false;
            }
        }

        private void DisposeRecorderStopSignal()
        {
            EventWaitHandle signal = recorderStopSignal;
            recorderStopSignal = null;
            if (signal != null) signal.Dispose();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            App app = Application.Current as App;
            if (app != null) app.ThemeChanged -= App_ThemeChanged;
            replayTimer.Stop();
            StopRecorder();
        }

        private static string GetRecordingsFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DCS Extra Telemetry", "Recordings");
        }

        private static IEnumerable<string> GetRecordingFoldersForBrowsing()
        {
            var folders = new List<string> { GetRecordingsFolder() };
            string[] oneDriveRoots =
            {
                Environment.GetEnvironmentVariable("OneDrive"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive")
            };
            foreach (string root in oneDriveRoots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                folders.Add(Path.Combine(root, "Documents", "DCS Extra Telemetry", "Recordings"));
            }
            return folders.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private async void InstallSimHubPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: false, requireSimHub: true)) return;
            bool replaceExisting = HasDifferentSimHubPlugin();
            if (replaceExisting && MessageBox.Show(
                    "A different DcsExtraTelemetry.Plugin.dll is installed. The installer can back it up before replacement. Continue?",
                    "Different plugin build detected", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Installing SimHub plugin", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    await RunSetupCommandAsync("--setup-install-plugin", simHubPath);
                });
                return;
            }

            await RunUiActionAsync("Installing SimHub plugin", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: false, requireSimHubClosed: true);
                await RunNativeSimHubActionAsync(packageRoot, install: true, option: replaceExisting);
            });
        }

        private async void RemoveSimHubPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelections(requireDcs: false, requireSimHub: true)) return;
            if (MessageBox.Show(
                    "Remove only DcsExtraTelemetry.Plugin.dll and its PDB from SimHub?",
                    "Remove SimHub plugin", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (IsSetupInstallation())
            {
                await RunUiActionAsync("Removing SimHub plugin", async () =>
                {
                    EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                    await RunSetupCommandAsync("--setup-remove-plugin");
                });
                return;
            }

            await RunUiActionAsync("Removing SimHub plugin", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: false, requireSimHubClosed: true);
                await RunNativeSimHubActionAsync(packageRoot, install: false, option: false);
            });
        }

        private async void CompleteRollbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsSetupInstallation()) { OneClickUninstallButton_Click(sender, e); return; }
            if (!EnsureSelections(requireDcs: true, requireSimHub: true)) return;
            if (!File.Exists(Path.Combine(packageRoot, "baseline.json")))
            {
                MessageBox.Show("The baseline is missing. Capture it before using verified rollback.", "Baseline required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(
                    "Remove this package's SimHub plugin and DCS Hook, then verify every unrelated file against the baseline?",
                    "Complete rollback", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunUiActionAsync("Completing rollback", async () =>
            {
                EnsureProgramsClosed(requireDcsClosed: true, requireSimHubClosed: true);
                string dcsProfile = GetSelectedDcsProfiles().Single();
                await RunNativeSimHubActionAsync(packageRoot, install: false, option: false);
                await Task.Run(() => InstallService.RemoveDcsHook(packageRoot, dcsProfile, false, AppendLogThreadSafe));
                await Task.Run(() => InstallService.VerifyBaseline(Path.Combine(packageRoot, "baseline.json"), AppendLogThreadSafe));
            });
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshEnvironment();
            AppendLog("Installation detection refreshed.");
        }

        private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            if (MaximizeWindowButton != null)
            {
                MaximizeWindowButton.Content = maximized ? "\uE923" : "\uE922";
                MaximizeWindowButton.ToolTip = maximized ? "Restore down" : "Maximize";
            }
            if (WindowFrame != null) WindowFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
            UpdateScaleFrameLayout();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScaleFrameLayout();
        }

        private void AdvancedInstallOptionsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            FitWindowToPage(SettingsPage);
        }

        private void AdvancedInstallOptionsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            FitWindowToPage(SettingsPage);
        }

        private void FitDashboardWindowToContent(bool expanded)
        {
            FitWindowToPage(SettingsPage);
        }

        private void FitWindowToPage(FrameworkElement page)
        {
            if (WindowState != WindowState.Normal || page == null || !IsLoaded)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (WindowState != WindowState.Normal || page.Visibility != Visibility.Visible) return;

                bool isFlightAnalysis = page == TelemetryPage && FlightDashboard.Visibility == Visibility.Visible;
                Rect workArea = SystemParameters.WorkArea;
                double availableWidth = Math.Max(MinimumWindowWidth, workArea.Width * DefaultWorkAreaCoverage);
                double targetWidth = Math.Min(isFlightAnalysis ? FlightAnalysisWindowWidth : ReferenceWindowWidth, availableWidth);
                MinWidth = Math.Min(isFlightAnalysis ? MinimumFlightAnalysisWindowWidth : MinimumWindowWidth, availableWidth);
                Width = Math.Max(MinWidth, targetWidth);
                double maxHeight = Math.Max(MinimumWindowHeight, workArea.Height * DefaultWorkAreaCoverage);
                // WPF will measure the page during the next layout pass. Forcing an
                // infinite-height measure here while changing the window width can
                // re-enter layout with invalid child sizes (notably the flight charts).
                // The analysis page scrolls, so give it the available height directly.
                double contentHeight = Math.Max(ActionArea.DesiredSize.Height, page.DesiredSize.Height);
                if (double.IsNaN(contentHeight) || double.IsInfinity(contentHeight) || contentHeight < 0)
                    contentHeight = MinimumWindowHeight;
                double desiredHeight = isFlightAnalysis
                    ? maxHeight
                    : Math.Max(MinimumWindowHeight, contentHeight + 64 + 14 + 12);
                double targetHeight = Math.Min(desiredHeight, maxHeight);
                Height = targetHeight;
                Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2.0);
                Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2.0);
            }), DispatcherPriority.Loaded);
        }

        private void UpdateScaleFrameLayout()
        {
            if (WindowFrame == null || WindowScaleViewbox == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            double inset = WindowState == WindowState.Maximized ? MaximizedFrameInset : 0.0;
            WindowScaleViewbox.Margin = new Thickness(inset);

        }

        private void AnalysisScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            ScrollViewer viewer = sender as ScrollViewer;
            if (viewer == null) return;
            bool atTop = viewer.VerticalOffset <= 0;
            bool atBottom = viewer.VerticalOffset >= viewer.ScrollableHeight - .5;
            e.Handled = true;
            double step = e.Delta > 0 ? -20 : 20;
            if ((e.Delta > 0 && !atTop) || (e.Delta < 0 && !atBottom))
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset + step);
            else
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + step);
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                ScrollViewer nestedViewer = source as ScrollViewer;
                if (nestedViewer != null && !ReferenceEquals(nestedViewer, MainScrollViewer)) return;
                try { source = VisualTreeHelper.GetParent(source); }
                catch (InvalidOperationException) { source = LogicalTreeHelper.GetParent(source); }
            }
            e.Handled = true;
            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + (e.Delta > 0 ? -20 : 20));
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OverviewNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(OverviewNavButton, SettingsPage);
        }

        private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            sidebarCollapsed = !sidebarCollapsed;
            SidebarColumn.Width = new GridLength(sidebarCollapsed ? 58 : 186);
            SidebarSectionTitle.Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarFooterText.Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarToggleGlyph.Text = sidebarCollapsed ? "\uE76C" : "\uE76B";
            SidebarToggleButton.ToolTip = sidebarCollapsed ? "Expand navigation" : "Collapse navigation";
            UpdateRecentFlightsVisibility();

            TextBlock[] labels =
            {
                OverviewNavLabel, InstallationNavLabel, TelemetryNavLabel,
                DiagnosticsNavLabel, SettingsNavLabel
            };
            TextBlock[] icons =
            {
                OverviewNavIcon, InstallationNavIcon, TelemetryNavIcon,
                DiagnosticsNavIcon, SettingsNavIcon
            };
            Button[] buttons =
            {
                OverviewNavButton, InstallationNavButton, TelemetryNavButton,
                DiagnosticsNavButton, SettingsNavButton
            };

            for (int index = 0; index < labels.Length; index++)
            {
                labels[index].Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
                icons[index].Margin = sidebarCollapsed ? new Thickness(0) : new Thickness(0, 0, 14, 0);
                buttons[index].HorizontalContentAlignment = sidebarCollapsed
                    ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            }
        }

        private void InstallationNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(OverviewNavButton, SettingsPage);
        }

        private void AircraftNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AircraftNavButton, AircraftPage);
        }

        private void TelemetryNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(TelemetryNavButton, TelemetryPage);
        }

        private void DiagnosticsNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(OverviewNavButton, SettingsPage);
            AdvancedExpander.Visibility = Visibility.Visible;
            AdvancedExpander.IsExpanded = true;
        }

        private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(SettingsNavButton, OptionsPage);
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(OverviewNavButton, SettingsPage);
            AdvancedExpander.Visibility = Visibility.Visible;
            AdvancedExpander.IsExpanded = true;
        }

        private void ShowPage(Button selectedButton, FrameworkElement selectedPage)
        {
            SelectNavigationButton(selectedButton);
            TopStatusPill.Visibility = selectedPage == TelemetryPage ? Visibility.Collapsed : Visibility.Visible;
            foreach (FrameworkElement page in new FrameworkElement[] { OverviewPage, TelemetryPage, AircraftPage, InstallationPage, DiagnosticsPage, SettingsPage, OptionsPage })
            {
                page.Visibility = page == selectedPage ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateRecentFlightsVisibility();
            UpdateSidebarFooter();
            MainScrollViewer.ScrollToTop();
            FitWindowToPage(selectedPage);
        }

        private void SelectNavigationButton(Button selectedButton)
        {
            foreach (Button button in new[] { OverviewNavButton, TelemetryNavButton, AircraftNavButton, InstallationNavButton, DiagnosticsNavButton, SettingsNavButton })
            {
                button.Style = (Style)FindResource(button == selectedButton ? "ActiveNavButton" : "NavButton");
            }
        }

        private void SystemThemeButton_Click(object sender, RoutedEventArgs e)
        {
            SetThemePreference(ThemePreference.System);
        }

        private bool updatingDisplayPreferences;

        private void InitializeDisplayPreferences()
        {
            App app = (App)Application.Current;
            updatingDisplayPreferences = true;
            foreach (ComboBoxItem item in TimeFormatComboBox.Items)
                if (string.Equals(item.Tag as string, app.Use24HourTime ? "24Hour" : "12Hour", StringComparison.Ordinal))
                    TimeFormatComboBox.SelectedItem = item;
            foreach (ComboBoxItem item in DateFormatComboBox.Items)
                if (string.Equals(item.Tag as string, app.DateFormat, StringComparison.Ordinal))
                    DateFormatComboBox.SelectedItem = item;
            TimeFormatComboBox.SelectionChanged += DisplayFormatComboBox_SelectionChanged;
            DateFormatComboBox.SelectionChanged += DisplayFormatComboBox_SelectionChanged;
            updatingDisplayPreferences = false;
            UpdateDisplayFormatPreview();
        }

        private void DisplayFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingDisplayPreferences || TimeFormatComboBox.SelectedItem == null || DateFormatComboBox.SelectedItem == null) return;
            var time = TimeFormatComboBox.SelectedItem as ComboBoxItem;
            var date = DateFormatComboBox.SelectedItem as ComboBoxItem;
            if (time == null || date == null) return;
            ((App)Application.Current).SetDisplayPreferences((string)time.Tag == "24Hour", (string)date.Tag);
            UpdateDisplayFormatPreview();
            RefreshRecentFlights();
            RefreshDashboard();
        }

        private void UpdateDisplayFormatPreview()
        {
            App app = Application.Current as App;
            if (app != null && DisplayFormatPreviewText != null)
                DisplayFormatPreviewText.Text = "Preview · " + app.FormatFlightDateTime(DateTime.Now);
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e)
        {
            SetThemePreference(ThemePreference.Light);
        }

        private void DarkThemeButton_Click(object sender, RoutedEventArgs e)
        {
            SetThemePreference(ThemePreference.Dark);
        }

        private void SetThemePreference(ThemePreference preference)
        {
            ((App)Application.Current).SetThemePreference(preference);
            UpdateAppearanceButtons();
        }

        private void UpdateAppearanceButtons()
        {
            App app = Application.Current as App;
            if (app == null) return;

            SetAppearanceButton(SystemThemeButton, app.ThemePreference == ThemePreference.System);
            SetAppearanceButton(LightThemeButton, app.ThemePreference == ThemePreference.Light);
            SetAppearanceButton(DarkThemeButton, app.ThemePreference == ThemePreference.Dark);
        }

        private void SetAppearanceButton(Button button, bool selected)
        {
            if (button == null) return;
            button.Style = (Style)FindResource(selected ? "PillButtonActive" : "PillButton");
        }

        private void App_ThemeChanged(object sender, EventArgs e)
        {
            UpdateAppearanceButtons();
            WindowBackdrop.Apply(this, ((App)Application.Current).IsDarkTheme);
            if (EffectTimelineChart != null) EffectTimelineChart.RefreshTheme();
            if (PhaseMatrixChart != null) PhaseMatrixChart.RefreshTheme();
            if (CombinedLoadChart != null) CombinedLoadChart.RefreshTheme();
            if (currentEffectMapResult != null) RenderFlightsDashboard();
        }

        private void LoadPortSettings()
        {
            TelemetryPortSettings settings = TelemetryPortSettings.Load();
            simHubTelemetryPort = settings.SimHubPort;
            recorderPort = settings.RecorderPort;
            SimHubPortTextBox.Text = simHubTelemetryPort.ToString();
            RecorderPortTextBox.Text = recorderPort.ToString();
            PortSettingsStatusText.Text = "SimHub uses " + simHubTelemetryPort
                + ". Recorder port " + recorderPort + " is managed automatically.";
        }

        private void FindAvailablePortsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int first = FindAvailableUdpPort(TelemetryPortSettings.DefaultSimHubPort, null);
                int second = FindAvailableRecorderPort(first);
                SimHubPortTextBox.Text = first.ToString();
                RecorderPortTextBox.Text = second.ToString();
                PortSettingsStatusText.Text = "SimHub port " + first
                    + " is available. Recorder port " + second + " will be reserved automatically.";
                PortSettingsStatusText.Foreground = (Brush)FindResource("Warning");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SavePortSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int requestedSimHubPort;
                if (!int.TryParse(SimHubPortTextBox.Text.Trim(), out requestedSimHubPort))
                {
                    throw new ArgumentException("Enter a whole-number UDP port between 1024 and 65535.");
                }

                if (requestedSimHubPort != simHubTelemetryPort && !IsUdpPortAvailable(requestedSimHubPort))
                {
                    throw new InvalidOperationException("UDP port " + requestedSimHubPort
                        + " is already in use. Choose another SimHub telemetry port.");
                }

                int requestedRecorderPort;
                if (!string.IsNullOrWhiteSpace(RecorderPortTextBox.Text))
                {
                    if (!int.TryParse(RecorderPortTextBox.Text.Trim(), out requestedRecorderPort))
                    {
                        throw new ArgumentException("Enter a whole-number recorder port between 1024 and 65535.");
                    }
                }
                else if (requestedSimHubPort == simHubTelemetryPort
                    && recorderPort != requestedSimHubPort
                    && ((recorderProcess != null && !recorderProcess.HasExited)
                        || IsUdpPortAvailable(recorderPort)))
                {
                    requestedRecorderPort = recorderPort;
                }
                else
                {
                    requestedRecorderPort = FindAvailableRecorderPort(requestedSimHubPort);
                    RecorderPortTextBox.Text = requestedRecorderPort.ToString();
                }

                TelemetryPortSettings settings = TelemetryPortSettings.Create(
                    requestedSimHubPort, requestedRecorderPort);

                if (requestedRecorderPort != recorderPort && !IsUdpPortAvailable(requestedRecorderPort))
                {
                    throw new InvalidOperationException("UDP port " + requestedRecorderPort
                        + " is already in use. Choose another recorder port.");
                }

                settings.Save();
                simHubTelemetryPort = settings.SimHubPort;
                recorderPort = settings.RecorderPort;
                PortSettingsStatusText.Text = "Saved SimHub port " + simHubTelemetryPort
                    + ". Recorder port " + recorderPort + " was "
                    + (AdvancedRecorderPortExpander.IsExpanded ? "set manually." : "selected automatically.")
                    + " Restart DCS and SimHub to apply.";
                PortSettingsStatusText.Foreground = (Brush)FindResource("Success");
                AppendLog("Telemetry ports saved: SimHub " + simHubTelemetryPort
                    + ", recorder " + recorderPort + ". Restart DCS and SimHub to apply.");
            }
            catch (Exception ex)
            {
                PortSettingsStatusText.Text = ex.Message;
                PortSettingsStatusText.Foreground = (Brush)FindResource("Danger");
                ShowError(ex);
            }
        }

        private static int FindAvailableUdpPort(int startingPort, int? excludedPort)
        {
            int firstPort = Math.Max(startingPort, TelemetryPortSettings.MinimumPort);
            for (int port = firstPort; port <= TelemetryPortSettings.MaximumPort; port++)
            {
                if (excludedPort != port && IsUdpPortAvailable(port)) return port;
            }
            throw new InvalidOperationException("No available local UDP port was found.");
        }

        private static int FindAvailableRecorderPort(int simHubPort)
        {
            int preferredStart = simHubPort < TelemetryPortSettings.MaximumPort
                ? simHubPort + 1
                : TelemetryPortSettings.MinimumPort;

            try
            {
                return FindAvailableUdpPort(preferredStart, simHubPort);
            }
            catch (InvalidOperationException)
            {
                if (preferredStart > TelemetryPortSettings.MinimumPort)
                {
                    return FindAvailableUdpPort(TelemetryPortSettings.MinimumPort, simHubPort);
                }
                throw;
            }
        }

        private static bool IsUdpPortAvailable(int port)
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.ExclusiveAddressUse = true;
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private void SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.IsChecked != true) return;
            confirmedDcsProfile = null;
            RefreshInstallationStatus();
        }

        private void ConfirmDcsProfileButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> selectedProfiles = GetSelectedDcsProfiles();
            if (selectedProfiles.Count != 1)
            {
                MessageBox.Show("Select exactly one DCS Saved Games profile before confirming.",
                    "DCS profile selection required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            confirmedDcsProfile = selectedProfiles[0];
            AppendLog("Confirmed DCS install location: " + confirmedDcsProfile);
            RefreshInstallationStatus();
        }

        private void AddDcsProfileButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Choose a DCS Saved Games profile folder (for example DCS or DCS.openbeta).";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = Path.GetFullPath(dialog.SelectedPath);
                    customDcsProfiles.Add(selectedPath);
                    confirmedDcsProfile = null;
                    RefreshEnvironment();
                    RadioButton selectedButton = DcsProfilesPanel.Children.OfType<RadioButton>()
                        .FirstOrDefault(button => string.Equals((string)button.Tag, selectedPath, StringComparison.OrdinalIgnoreCase));
                    if (selectedButton != null) selectedButton.IsChecked = true;
                }
            }
        }

        private void OpenProfilesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string[] candidates =
            {
                packageRoot == null ? null : Path.Combine(packageRoot, "profiles"),
                packageRoot == null ? null : Path.Combine(packageRoot, "Profiles"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles")
            };
            string profilesPath = candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));
            if (profilesPath == null)
            {
                MessageBox.Show("The bundled profiles folder was not found in this package.",
                    "Profiles folder unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = profilesPath,
                UseShellExecute = true
            });
        }

        private void ChooseSimHubButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Choose the SimHub folder containing SimHubWPF.exe.";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = Path.GetFullPath(dialog.SelectedPath);
                    if (!File.Exists(Path.Combine(selectedPath, "SimHubWPF.exe")))
                    {
                        MessageBox.Show("SimHubWPF.exe was not found in that folder.", "Invalid SimHub folder",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    simHubPath = selectedPath;
                    RefreshEnvironment();
                }
            }
        }

        private void OpenGuideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(packageRoot, "README-FIRST.md"),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private async Task RunUiActionAsync(string label, Func<Task> action)
        {
            if (isBusy) return;
            isBusy = true;
            ActionArea.IsEnabled = false;
            BusyText.Text = label + "...";
            AppendLog("--- " + label + " ---");

            try
            {
                await action();
                AppendLog("PASS: " + label + " completed.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                isBusy = false;
                ActionArea.IsEnabled = true;
                BusyText.Text = string.Empty;
                RefreshEnvironment();
            }
        }

        private async Task RunNativeSimHubActionAsync(string actionPackageRoot, bool install, bool option)
        {
            if (IsAdministrator())
            {
                await Task.Run(() =>
                {
                    if (install) InstallService.InstallSimHubPlugin(actionPackageRoot, simHubPath, option, AppendLogThreadSafe);
                    else InstallService.RemoveSimHubPlugin(actionPackageRoot, simHubPath, option, AppendLogThreadSafe);
                });
                return;
            }

            AppendLog("Windows administrator approval is required to change the SimHub plugin files.");
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            string operation = install ? "--native-install-simhub" : "--native-remove-simhub";
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = operation + " " + QuoteArgument(actionPackageRoot) + " " + QuoteArgument(simHubPath)
                    + " " + (option ? "true" : "false"),
                WorkingDirectory = actionPackageRoot,
                UseShellExecute = true,
                Verb = "runas"
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Administrator approval could not be started.");
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("The administrator-approved SimHub operation failed with exit code "
                        + process.ExitCode + ".");
            }
            AppendLog(install ? "SimHub plugin files installed." : "SimHub plugin files removed.");
        }

        private async Task CheckForUpdatesAsync(bool showErrors)
        {
            if (isBusy || packageInfo == null) return;
            availableUpdate = null;
            ApplyUpdateButton.IsEnabled = false;
            SetStatus(UpdateStatusText, "Checking public beta releases...", null);
            try
            {
                UpdateInfo update = IsSetupInstallation()
                    ? await updateService.FindInstallerUpdateAsync(packageInfo.version)
                    : await updateService.FindUpdateAsync(packageInfo.version);
                availableUpdate = update;
                if (update == null)
                {
                    SetStatus(UpdateStatusText, "You have the newest available version.", true);
                }
                else
                {
                    SetStatus(UpdateStatusText, "Version " + update.Version + " is available.", null);
                    ApplyUpdateButton.IsEnabled = true;
                    AppendLog("Update available: " + update.TagName);
                }
            }
            catch (Exception ex)
            {
                SetStatus(UpdateStatusText, "Update check failed: " + ex.Message, null);
                AppendLog("UPDATE CHECK: " + ex.Message);
                if (showErrors) ShowError(ex);
            }
        }

        private bool TryRedirectToActivePackage(string launchedRoot)
        {
            if (string.IsNullOrWhiteSpace(launchedRoot)) return false;
            if (File.Exists(Path.Combine(launchedRoot, "HapticFlight Bridge.exe"))) return false;
            try
            {
                InstallState state = updateService.ReadInstallState();
                string activeRoot = state == null ? null : state.activePackageRoot;
                if (string.IsNullOrWhiteSpace(activeRoot) || !Directory.Exists(activeRoot)
                    || string.Equals(Path.GetFullPath(activeRoot), Path.GetFullPath(launchedRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                UpdateService.VerifyPackageManifest(activeRoot, allowRuntimeFiles: true);
                PackageInfo launchedInfo = updateService.ReadPackageInfo(launchedRoot);
                PackageInfo activeInfo = updateService.ReadPackageInfo(activeRoot);
                if (!string.Equals(launchedInfo.repository, activeInfo.repository, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                ReleaseVersion launchedVersion;
                ReleaseVersion activeVersion;
                if (!ReleaseVersion.TryParse(launchedInfo.version, out launchedVersion)
                    || !ReleaseVersion.TryParse(activeInfo.version, out activeVersion)
                    || activeVersion.CompareTo(launchedVersion) <= 0)
                {
                    return false;
                }

                LaunchPackageLauncher(activeRoot);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("UPDATER HANDOFF: Cached package was not used: " + ex.Message);
                return false;
            }
        }

        private bool IsSetupInstallation()
        {
            return packageRoot != null && File.Exists(Path.Combine(packageRoot, "HapticFlight Bridge.exe"));
        }

        private static async Task RunSetupCommandAsync(string operation, params string[] values)
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HapticFlight Bridge.exe");
            string arguments = operation + (values.Length == 0 ? string.Empty : " " + string.Join(" ",
                values.Select(QuoteArgument)));
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = true
            }))
            {
                if (process == null) throw new InvalidOperationException("Setup operation could not start.");
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0) throw new InvalidOperationException("Setup operation did not complete.");
            }
        }

        private static void LaunchPackageLauncher(string root)
        {
            string launcher = Path.Combine(root, "HapticFlight Bridge.exe");
            if (!File.Exists(launcher)) launcher = Path.Combine(root, "Tools", "DcsExtraTelemetry.Launcher.exe");
            if (!File.Exists(launcher)) throw new FileNotFoundException("The updated launcher is missing.", launcher);
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                WorkingDirectory = Path.GetDirectoryName(launcher),
                UseShellExecute = true
            });
            if (process == null) throw new InvalidOperationException("The updated launcher could not be started.");
        }

        private void RestoreInstallState(InstallState state)
        {
            if (state == null) return;
            if (!string.IsNullOrWhiteSpace(state.dcsProfile) && Directory.Exists(state.dcsProfile))
            {
                confirmedDcsProfile = Path.GetFullPath(state.dcsProfile);
                customDcsProfiles.Add(confirmedDcsProfile);
            }
            if (!string.IsNullOrWhiteSpace(state.simHubPath)
                && File.Exists(Path.Combine(state.simHubPath, "SimHubWPF.exe")))
            {
                simHubPath = Path.GetFullPath(state.simHubPath);
            }
        }

        private void SaveInstallState(string version, string activeRoot)
        {
            List<string> profiles = GetSelectedDcsProfiles();
            updateService.WriteInstallState(new InstallState
            {
                installedVersion = version,
                activePackageRoot = activeRoot,
                dcsProfile = profiles.Count == 1 ? profiles[0] : confirmedDcsProfile,
                simHubPath = simHubPath
            });
        }

        private bool EnsureSelections(bool requireDcs, bool requireSimHub)
        {
            List<string> selectedProfiles = GetSelectedDcsProfiles();
            if (requireDcs && selectedProfiles.Count != 1)
            {
                MessageBox.Show("Select exactly one DCS Saved Games profile.", "DCS profile required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (requireDcs && !IsDcsProfileConfirmed(selectedProfiles))
            {
                MessageBox.Show("Click Confirm DCS profile before continuing.", "DCS profile confirmation required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (requireSimHub && (string.IsNullOrWhiteSpace(simHubPath) || !File.Exists(Path.Combine(simHubPath, "SimHubWPF.exe"))))
            {
                MessageBox.Show("Choose the SimHub installation folder first.", "SimHub required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        private bool EnsureSelectionsAvailable(bool requireDcs, bool requireSimHub)
        {
            List<string> selectedProfiles = GetSelectedDcsProfiles();
            if (requireDcs && (selectedProfiles.Count != 1 || !IsDcsProfileConfirmed(selectedProfiles))) return false;
            if (requireSimHub && (string.IsNullOrWhiteSpace(simHubPath) || !File.Exists(Path.Combine(simHubPath, "SimHubWPF.exe")))) return false;
            return true;
        }

        private static void EnsureProgramsClosed(bool requireDcsClosed, bool requireSimHubClosed)
        {
            if (requireDcsClosed && Process.GetProcessesByName("DCS").Length > 0)
                throw new InvalidOperationException("Close DCS before continuing.");
            if (requireSimHubClosed && Process.GetProcessesByName("SimHubWPF").Length > 0)
                throw new InvalidOperationException("Close SimHub before continuing.");
        }

        private List<string> GetSelectedDcsProfiles()
        {
            return DcsProfilesPanel.Children.OfType<RadioButton>()
                .Where(radioButton => radioButton.IsChecked == true)
                .Select(radioButton => (string)radioButton.Tag)
                .Where(Directory.Exists)
                .ToList();
        }

        private bool IsDcsProfileConfirmed(List<string> selectedProfiles)
        {
            return selectedProfiles.Count == 1
                && !string.IsNullOrWhiteSpace(confirmedDcsProfile)
                && string.Equals(selectedProfiles[0], confirmedDcsProfile, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasDifferentDcsHook()
        {
            string source = Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua");
            return GetSelectedDcsProfiles().Any(profile =>
            {
                string target = Path.Combine(profile, "Scripts", "Hooks", "DCSExtraTelemetry.lua");
                return File.Exists(target) && (!File.Exists(source) || !HashesMatch(source, target));
            });
        }

        private bool HasDifferentSimHubPlugin()
        {
            return new[] { "DcsExtraTelemetry.Plugin.dll", "DcsExtraTelemetry.Plugin.pdb" }.Any(file =>
            {
                string source = Path.Combine(packageRoot, "SimHub", file);
                string target = Path.Combine(simHubPath, file);
                return File.Exists(target) && (!File.Exists(source) || !HashesMatch(source, target));
            });
        }

        private bool AreSelectedDcsHooksInstalled()
        {
            List<string> profiles = GetSelectedDcsProfiles();
            if (profiles.Count == 0) return false;

            string source = Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua");
            return File.Exists(source) && profiles.All(profile =>
            {
                string target = Path.Combine(profile, "Scripts", "Hooks", "DCSExtraTelemetry.lua");
                return File.Exists(target) && HashesMatch(source, target);
            });
        }

        private bool IsSimHubPluginInstalled()
        {
            if (string.IsNullOrWhiteSpace(simHubPath)) return false;

            string source = Path.Combine(packageRoot, "SimHub", "DcsExtraTelemetry.Plugin.dll");
            string target = Path.Combine(simHubPath, "DcsExtraTelemetry.Plugin.dll");
            return File.Exists(source) && File.Exists(target) && HashesMatch(source, target);
        }

        private bool HasAnyInstalledComponents()
        {
            bool hookPresent = GetSelectedDcsProfiles().Any(profile =>
                File.Exists(Path.Combine(profile, "Scripts", "Hooks", "DCSExtraTelemetry.lua")));
            bool pluginPresent = !string.IsNullOrWhiteSpace(simHubPath)
                && File.Exists(Path.Combine(simHubPath, "DcsExtraTelemetry.Plugin.dll"));
            return hookPresent || pluginPresent;
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            LogTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }

        private void AppendLogThreadSafe(string message)
        {
            if (Dispatcher.CheckAccess()) AppendLog(message);
            else Dispatcher.Invoke(() => AppendLog(message));
        }

        private void ShowError(Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "HapticFlight Bridge", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void SetStatus(TextBlock target, string text, bool? success)
        {
            if (target == null) return;
            target.Text = text;
            target.SetResourceReference(TextBlock.ForegroundProperty,
                success == true ? "Success" : success == false ? "Danger" : "TextSecondary");
            if (target == UpdateStatusText && HomeUpdateStatusText != null)
            {
                HomeUpdateStatusText.Text = text;
                HomeUpdateStatusText.SetResourceReference(TextBlock.ForegroundProperty,
                    success == true ? "Success" : success == false ? "StatusError" : "Warning");
            }
        }

        private void SetStatusLight(System.Windows.Shapes.Ellipse light, string resourceName, bool glow)
        {
            if (light == null) return;
            // Status lights are flat in the dark glass design; the glow parameter is ignored.
            light.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, resourceName);
            light.Effect = null;
        }

        private void SetStepDisc(Border disc, TextBlock glyph, int number, bool complete, bool active)
        {
            if (disc == null || glyph == null) return;
            disc.SetResourceReference(Border.BorderBrushProperty, complete ? "Success" : active ? "Warning" : "GlassStroke");
            disc.SetResourceReference(Border.BackgroundProperty, complete ? "Success" : "GlassSurfaceStrong");
            disc.Effect = null;
            glyph.Text = complete ? "\uE73E" : number.ToString();
            glyph.FontFamily = new FontFamily(complete ? "Segoe MDL2 Assets" : "Segoe UI Variable Text, Segoe UI");
            glyph.SetResourceReference(TextBlock.ForegroundProperty,
                complete ? "PrimaryText" : active ? "Warning" : "TextSecondary");
        }

        private void UpdateVersionLabels()
        {
            string packageVersion = packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.version)
                ? "unavailable"
                : "v" + packageInfo.version;
            string engineVersion = packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.hapticEngineVersion)
                ? "unavailable"
                : packageInfo.hapticEngineVersion;

            CurrentVersionText.Text = packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.version)
                ? "?"
                : packageInfo.version;
            CurrentEngineVersionText.Text = engineVersion;
            if (HomePackageVersionText != null)
                HomePackageVersionText.Text = packageVersion;
            if (HomeEngineVersionText != null)
                HomeEngineVersionText.Text = "Haptic Engine " + engineVersion;
            string rollback = packageInfo == null ? null : packageInfo.lastKnownWorkingVersion;
            if (SidebarRollbackVersionText != null)
                SidebarRollbackVersionText.Text = string.IsNullOrWhiteSpace(rollback)
                    ? "NO PUBLIC ROLLBACK RELEASE"
                    : "ROLLBACK: v" + rollback.TrimStart('v', 'V');
            if (HomeRollbackVersionText != null)
                HomeRollbackVersionText.Text = string.IsNullOrWhiteSpace(rollback)
                    ? "No public rollback release"
                    : "Rollback release v" + rollback.TrimStart('v', 'V');
        }

        private void UpdateSidebarFooter()
        {
            if (SidebarFooterIndicator == null || SidebarFooterText == null) return;
            bool supportPageVisible = SettingsPage != null && SettingsPage.Visibility == Visibility.Visible;
            SidebarFooterIndicator.Visibility = supportPageVisible ? Visibility.Visible : Visibility.Collapsed;
            SidebarSimHubPortText.Visibility = supportPageVisible ? Visibility.Collapsed : Visibility.Visible;
            SidebarRecorderPortText.Visibility = supportPageVisible ? Visibility.Collapsed : Visibility.Visible;
            if (SidebarInstalledVersionText != null)
            {
                string installedPlugin = string.IsNullOrWhiteSpace(simHubPath)
                    ? null
                    : Path.Combine(simHubPath, "DcsExtraTelemetry.Plugin.dll");
                if (string.IsNullOrWhiteSpace(installedPlugin) || !File.Exists(installedPlugin))
                {
                    SidebarInstalledVersionText.Text = "Installed plugin: not installed";
                    SidebarInstalledVersionText.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
                }
                else
                {
                    try
                    {
                        FileVersionInfo info = FileVersionInfo.GetVersionInfo(installedPlugin);
                        string version = string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
                        SidebarInstalledVersionText.Text = "Installed plugin: " + (string.IsNullOrWhiteSpace(version) ? "version unavailable" : "v" + version.TrimStart('v', 'V'));
                        SidebarInstalledVersionText.SetResourceReference(TextBlock.ForegroundProperty, "Success");
                    }
                    catch
                    {
                        SidebarInstalledVersionText.Text = "Installed plugin: version unavailable";
                        SidebarInstalledVersionText.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
                    }
                }
            }

            if (!supportPageVisible)
            {
                SidebarFooterText.Text = "LOCAL CONNECTION";
                SidebarSimHubPortText.Text = "SIMHUB " + simHubTelemetryPort;
                SidebarRecorderPortText.Text = "RECORDER " + recorderPort;
                return;
            }

            SidebarFooterText.Text = homeFullyInstalled ? "All systems ready" : homePathsReady ? "Setup incomplete" : "Setup required";
            SetStatusLight(SidebarFooterIndicator,
                homeFullyInstalled ? "Success" : homePathsReady ? "Warning" : "Danger", true);
        }

        private static string QuoteArgument(string value)
        {
            return App.QuoteWindowsArgument(value);
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool HashesMatch(string first, string second)
        {
            return string.Equals(ComputeSha256(first), ComputeSha256(second), StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private string FindPackageRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int depth = 0; current != null && depth < 8; depth++, current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "manifest.sha256"))
                    && File.Exists(Path.Combine(current.FullName, "package-info.json"))
                    && File.Exists(Path.Combine(current.FullName, "DCS", "DCSExtraTelemetry.lua")))
                {
                    return current.FullName;
                }

                string distRoot = Path.Combine(current.FullName, "dist");
                if (Directory.Exists(distRoot) && Directory.Exists(Path.Combine(current.FullName, "tools", "DcsExtraTelemetry.Launcher")))
                {
                    var candidates = new List<Tuple<ReleaseVersion, string>>();
                    foreach (string candidate in Directory.GetDirectories(distRoot, "DcsExtraTelemetry-*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            PackageInfo info = updateService.ReadPackageInfo(candidate);
                            ReleaseVersion version;
                            if (ReleaseVersion.TryParse(info.version, out version)) candidates.Add(Tuple.Create(version, candidate));
                        }
                        catch { }
                    }

                    foreach (Tuple<ReleaseVersion, string> candidate in candidates.OrderByDescending(item => item.Item1))
                    {
                        try
                        {
                            UpdateService.VerifyPackageManifest(candidate.Item2, allowRuntimeFiles: true);
                            return candidate.Item2;
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

        private static List<string> DetectDcsProfiles()
        {
            var results = new List<string>();
            string savedGamesRoot = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    object value = key?.GetValue("{4C5C32FF-BB9D-43B0-BF7B-1DDE8B8C7D1E}", null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value is string path) savedGamesRoot = Environment.ExpandEnvironmentVariables(path);
                }
            }
            catch
            {
                // Use the default below.
            }

            if (string.IsNullOrWhiteSpace(savedGamesRoot))
                savedGamesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");

            if (!Directory.Exists(savedGamesRoot)) return results;
            foreach (string directory in Directory.GetDirectories(savedGamesRoot, "DCS*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(directory);
                if (string.Equals(name, "DCS", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("DCS.", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(Path.GetFullPath(directory));
                }
            }
            return results;
        }

        private static string DetectSimHubPath()
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("SIMHUB_INSTALL_PATH"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SimHub"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SimHub")
            };

            return candidates.Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .FirstOrDefault(path => File.Exists(Path.Combine(path, "SimHubWPF.exe")));
        }
    }
}

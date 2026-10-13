using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace DcsExtraTelemetry.Launcher
{
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }

    public partial class App : Application
    {
        private const string SettingsFolderName = "DcsExtraTelemetry";
        private const string SettingsFileName = "launcher-theme.txt";
        private const string DisplaySettingsFileName = "launcher-display.txt";

        public ThemePreference ThemePreference { get; private set; }
        public bool IsDarkTheme { get; private set; }
        public bool Use24HourTime { get; private set; }
        public string DateFormat { get; private set; }
        public event EventHandler ThemeChanged;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            ThemePreference = ReadThemePreference();
            ReadDisplayPreferences();
            PromoteGlassStyles();
            ApplyTheme();
            base.OnStartup(e);

            if (TryRunElevatedHelper(e.Args)) return;
            new MainWindow().Show();
        }

        private bool TryRunElevatedHelper(string[] args)
        {
            if (args == null || args.Length != 4) return false;
            string operation = args[0];
            if (!string.Equals(operation, "--native-install-simhub", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(operation, "--native-remove-simhub", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                bool option = string.Equals(args[3], "true", StringComparison.OrdinalIgnoreCase);
                if (string.Equals(operation, "--native-install-simhub", StringComparison.OrdinalIgnoreCase))
                    InstallService.InstallSimHubPlugin(args[1], args[2], option, null);
                else
                    InstallService.RemoveSimHubPlugin(args[1], args[2], option, null);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                MessageBox.Show(ex.Message, "HapticFlight Bridge", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
            return true;
        }

        public void SetThemePreference(ThemePreference preference)
        {
            ThemePreference = preference;
            SaveThemePreference(preference);
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            IsDarkTheme = ThemePreference == ThemePreference.Dark
                || (ThemePreference == ThemePreference.System && !SystemUsesLightTheme());
            IReadOnlyDictionary<string, string> palette = GlassThemePalette.SetActive(IsDarkTheme);
            foreach (KeyValuePair<string, string> token in palette)
            {
                if (token.Key.EndsWith("Color", StringComparison.Ordinal))
                    SetThemeColor(token.Key, token.Value);
                else
                    SetColor(token.Key, token.Value);
            }
            SetGlassMaterialBrushes(palette);
            Resources["HeaderLogo"] = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(IsDarkTheme
                    ? "pack://application:,,,/Assets/HapticFlightLogoDark.png"
                    : "pack://application:,,,/Assets/HapticFlightLogoLight.png", UriKind.Absolute));
            EventHandler handler = ThemeChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void PromoteGlassStyles()
        {
            // App.xaml retains a few legacy controls that are not part of the redesign.
            // Promote the glass dictionary's entries into the primary dictionary so WPF
            // resolves redesigned keys before the legacy local style declarations.
            var glass = new ResourceDictionary
            {
                Source = new Uri("GlassStyles.xaml", UriKind.Relative)
            };
            foreach (DictionaryEntry entry in glass) Resources[entry.Key] = entry.Value;
        }

        private void SetColor(string key, string value)
        {
            // WPF may freeze shared Freezable resources on some systems. Replacing the
            // resource is safe; mutating a frozen SolidColorBrush throws during startup.
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        private void SetThemeColor(string key, string value)
        {
            Resources[key] = (Color)ColorConverter.ConvertFromString(value);
        }

        private void SetGlassMaterialBrushes(IReadOnlyDictionary<string, string> palette)
        {
            var surface = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            surface.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(palette["GlassSurfaceTopColor"]), 0));
            surface.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(palette["GlassSurfaceBottomColor"]), 1));
            Resources["GlassCardSurfaceBrush"] = surface;

            var edge = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            edge.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(palette["GlassStrokeTopColor"]), 0));
            edge.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(palette["GlassStrokeBottomColor"]), 1));
            Resources["GlassEdgeStrokeBrush"] = edge;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string logPath = WriteCrashLog(e.Exception);
            MessageBox.Show(
                "DCS Extra Telemetry could not continue. A diagnostic log was written to:\n\n" + logPath
                + "\n\n" + e.Exception.Message,
                "HapticFlight Bridge", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(-1);
        }

        private static string WriteCrashLog(Exception exception)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsFolderName);
            string path = Path.Combine(folder, "launcher-crash.log");
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(path, DateTime.Now.ToString("O") + Environment.NewLine
                    + exception + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // Preserve the original exception even if diagnostic logging is unavailable.
            }
            return path;
        }

        private static ThemePreference ReadThemePreference()
        {
            try
            {
                string path = GetSettingsPath();
                ThemePreference value;
                return File.Exists(path) && Enum.TryParse(File.ReadAllText(path), out value)
                    ? value
                    : ThemePreference.System;
            }
            catch
            {
                return ThemePreference.System;
            }
        }

        private static void SaveThemePreference(ThemePreference preference)
        {
            try
            {
                string path = GetSettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, preference.ToString());
            }
            catch
            {
                // Appearance preferences are optional; launcher operation must not depend on them.
            }
        }

        public string FormatFlightDateTime(DateTime value)
        {
            return FormatFlightDate(value) + ", " + FormatFlightTime(value);
        }

        public string FormatFlightDate(DateTime value)
        {
            string pattern = DateFormat == "DayFirst" ? "dd/MM/yyyy"
                : DateFormat == "Iso" ? "yyyy-MM-dd" : "M/d/yyyy";
            return value.ToString(pattern, CultureInfo.CurrentCulture);
        }

        public string FormatFlightTime(DateTime value)
        {
            return value.ToString(Use24HourTime ? "HH:mm" : "h:mm tt", CultureInfo.CurrentCulture);
        }

        public void SetDisplayPreferences(bool use24HourTime, string dateFormat)
        {
            Use24HourTime = use24HourTime;
            DateFormat = dateFormat == "DayFirst" || dateFormat == "Iso" ? dateFormat : "US";
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SettingsFolderName, DisplaySettingsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, new[] { Use24HourTime ? "24Hour" : "12Hour", DateFormat });
            }
            catch
            {
                // Display preferences remain usable for this session if the profile is read-only.
            }
        }

        private void ReadDisplayPreferences()
        {
            Use24HourTime = false;
            DateFormat = "US";
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SettingsFolderName, DisplaySettingsFileName);
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length > 0) Use24HourTime = string.Equals(lines[0], "24Hour", StringComparison.OrdinalIgnoreCase);
                if (lines.Length > 1 && (lines[1] == "DayFirst" || lines[1] == "Iso" || lines[1] == "US"))
                    DateFormat = lines[1];
            }
            catch
            {
                // Fall back to familiar US formatting.
            }
        }

        private static bool SystemUsesLightTheme()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 0);
                return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsFolderName, SettingsFileName);
        }
    }
}

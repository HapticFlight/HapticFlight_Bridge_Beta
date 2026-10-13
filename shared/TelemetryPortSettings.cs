using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DcsExtraTelemetry.Configuration
{
    internal sealed class TelemetryPortSettings
    {
        public const int DefaultSimHubPort = 29381;
        public const int DefaultRecorderPort = 29382;
        public const int MinimumPort = 1024;
        public const int MaximumPort = 65535;

        public int SimHubPort { get; private set; }
        public int RecorderPort { get; private set; }

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DcsExtraTelemetry",
                    "ports.ini");
            }
        }

        private TelemetryPortSettings(int simHubPort, int recorderPort)
        {
            SimHubPort = simHubPort;
            RecorderPort = recorderPort;
        }

        public static TelemetryPortSettings Create(int simHubPort, int recorderPort)
        {
            Validate(simHubPort, recorderPort);
            return new TelemetryPortSettings(simHubPort, recorderPort);
        }

        public static TelemetryPortSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return Create(DefaultSimHubPort, DefaultRecorderPort);
                }

                Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string rawLine in File.ReadAllLines(SettingsPath))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int separator = line.IndexOf('=');
                    if (separator <= 0 || separator == line.Length - 1) continue;

                    int value;
                    if (int.TryParse(line.Substring(separator + 1).Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out value))
                    {
                        values[line.Substring(0, separator).Trim()] = value;
                    }
                }

                int simHubPort;
                int recorderPort;
                if (!values.TryGetValue("simhubPort", out simHubPort)
                    || !values.TryGetValue("recorderPort", out recorderPort))
                {
                    return Create(DefaultSimHubPort, DefaultRecorderPort);
                }

                return Create(simHubPort, recorderPort);
            }
            catch
            {
                // Telemetry must remain available even if a user-edited settings file is invalid.
                return Create(DefaultSimHubPort, DefaultRecorderPort);
            }
        }

        public void Save()
        {
            Validate(SimHubPort, RecorderPort);
            string directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath,
                "# DCS Extra Telemetry local UDP ports\r\n"
                + "simhubPort=" + SimHubPort.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "recorderPort=" + RecorderPort.ToString(CultureInfo.InvariantCulture) + "\r\n");

            if (File.Exists(SettingsPath))
            {
                File.Replace(temporaryPath, SettingsPath, null);
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
        }

        public static void Validate(int simHubPort, int recorderPort)
        {
            ValidatePort(simHubPort, "SimHub telemetry port");
            ValidatePort(recorderPort, "Recorder port");
            if (simHubPort == recorderPort)
            {
                throw new ArgumentException("The SimHub and recorder ports must be different.");
            }
        }

        private static void ValidatePort(int port, string label)
        {
            if (port < MinimumPort || port > MaximumPort)
            {
                throw new ArgumentOutOfRangeException(label,
                    label + " must be between " + MinimumPort + " and " + MaximumPort + ".");
            }
        }
    }
}

using DcsExtraTelemetry.Plugin.Telemetry;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace DcsExtraTelemetry.Monitor
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                int port = ReadIntegerArgument(args, "--port", UdpTelemetryReceiver.DefaultPort, 1, 65535);
                int seconds = ReadIntegerArgument(args, "--seconds", 120, 1, 86400);
                string recordPath = ReadStringArgument(args, "--record");
                string stopEventName = ReadStringArgument(args, "--stop-event");
                bool quiet = HasFlag(args, "--quiet");
                return Run(port, seconds, recordPath, stopEventName, quiet);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }

        private static int Run(int port, int seconds, string recordPath, string stopEventName, bool quiet)
        {
            bool receivedPacket = false;
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            StreamWriter recording = null;
            if (!string.IsNullOrWhiteSpace(recordPath))
            {
                string fullPath = Path.GetFullPath(recordPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                Stream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                if (fullPath.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
                {
                    stream = new GZipStream(stream, CompressionMode.Compress);
                }
                recording = new StreamWriter(stream, new UTF8Encoding(false), 65536) { AutoFlush = false };
            }

            using (recording)
            using (EventWaitHandle stopEvent = OpenStopEvent(stopEventName))
            using (UdpTelemetryReceiver receiver = new UdpTelemetryReceiver(port, message => Console.Error.WriteLine(message)))
            {
                if (recording != null)
                {
                    receiver.PacketReceived += (json, snapshot) => recording.WriteLine(json);
                }
                receiver.Start();
                if (!quiet)
                {
                    Console.WriteLine("DCS Extra Telemetry Monitor");
                    Console.WriteLine("Listening on 127.0.0.1:{0} for {1} seconds.", port, seconds);
                    Console.WriteLine(port == UdpTelemetryReceiver.DefaultPort
                        ? "Close SimHub while using the primary telemetry port."
                        : "Recorder mirror selected; SimHub can remain open.");
                    Console.WriteLine("Start a DCS mission now. Press Ctrl+C to stop early.");
                    if (recording != null) Console.WriteLine("Recording {0} validated packets to {1}",
                        recordPath.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase) ? "compressed raw" : "raw",
                        Path.GetFullPath(recordPath));
                    Console.WriteLine();
                }

                while (DateTime.UtcNow < deadline)
                {
                    if (stopEvent != null && stopEvent.WaitOne(0)) break;
                    TelemetrySnapshot snapshot = receiver.Current;
                    if (quiet)
                    {
                        if (snapshot.ReceivedAtUtc != DateTime.MinValue) receivedPacket = true;
                    }
                    else if (snapshot.ReceivedAtUtc == DateTime.MinValue)
                    {
                        Console.WriteLine("WAITING  no valid packet yet");
                    }
                    else
                    {
                        receivedPacket = true;
                        Console.WriteLine(
                            "{0,-7} aircraft={1,-22} age={2,6:0}ms rate={3,5:0.0}Hz " +
                            "accel=({4,6:0.00},{5,6:0.00},{6,6:0.00}) rpm=({7,6:0.0},{8,6:0.0}) " +
                            "gear={9:0.00} weapon={10:0} gunPulse={11:0} crewPulse={12:0} " +
                            "crewRounds={13:0} crewAmmo=({14:0},{15:0}) releasePulse={16:0} invalid={17} dropped={18}",
                            receiver.IsConnected ? "LIVE" : "STALE",
                            string.IsNullOrEmpty(snapshot.AircraftType) ? "(unknown)" : snapshot.AircraftType,
                            receiver.DataAgeMilliseconds,
                            snapshot.PacketRateHz,
                            snapshot.AccelerationXG,
                            snapshot.AccelerationYG,
                            snapshot.AccelerationZG,
                            snapshot.EngineRpmLeftPct,
                            snapshot.EngineRpmRightPct,
                            snapshot.GearPosition,
                            snapshot.WeaponPayloadCount,
                            snapshot.GunFirePulse,
                            snapshot.CrewGunFirePulse,
                            snapshot.CrewGunRoundsFired,
                            snapshot.LeftCrewGunAmmoRemaining,
                            snapshot.RightCrewGunAmmoRemaining,
                            snapshot.PayloadReleasePulse,
                            receiver.InvalidPackets,
                            receiver.DroppedPackets);
                    }

                    if (stopEvent != null) stopEvent.WaitOne(250);
                    else Thread.Sleep(1000);
                }
            }

            if (!receivedPacket)
            {
                Console.Error.WriteLine("FAIL: No valid telemetry packet was received.");
                return 2;
            }

            if (!quiet) Console.WriteLine("PASS: Valid DCS telemetry was received without installing the SimHub plugin.");
            return 0;
        }

        private static EventWaitHandle OpenStopEvent(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try { return EventWaitHandle.OpenExisting(name); }
            catch (WaitHandleCannotBeOpenedException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static string ReadStringArgument(string[] args, string name)
        {
            for (int index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    throw new ArgumentException(name + " requires a value.");
                return args[index + 1];
            }
            return null;
        }

        private static bool HasFlag(string[] args, string name)
        {
            foreach (string argument in args)
                if (string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static int ReadIntegerArgument(string[] args, string name, int defaultValue, int minimum, int maximum)
        {
            for (int index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value))
                {
                    throw new ArgumentException(name + " requires an integer value.");
                }

                if (value < minimum || value > maximum)
                {
                    throw new ArgumentOutOfRangeException(name, name + " must be between " + minimum + " and " + maximum + ".");
                }

                return value;
            }

            return defaultValue;
        }
    }
}

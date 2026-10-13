using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

[assembly: AssemblyTitle("HapticFlight Bridge")]
[assembly: AssemblyDescription("DCS and SimHub telemetry setup launcher")]
[assembly: AssemblyCompany("HapticFlight Bridge")]
[assembly: AssemblyProduct("HapticFlight Bridge")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("__PACKAGE_FILE_VERSION__")]
[assembly: AssemblyInformationalVersion("__PACKAGE_INFORMATIONAL_VERSION__")]

namespace DcsExtraTelemetry.Standalone
{
    internal static class Program
    {
        private const string PayloadResource = "DcsExtraTelemetry.Payload.zip";

        [STAThread]
        private static void Main()
        {
            try
            {
                string bundleRoot = PrepareBundle();
                string launcherPath = Path.Combine(bundleRoot, "Tools", "DcsExtraTelemetry.Launcher.exe");
                if (!File.Exists(launcherPath))
                    throw new FileNotFoundException("The bundled launcher was not found.", launcherPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = launcherPath,
                    WorkingDirectory = Path.GetDirectoryName(launcherPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "HapticFlight Bridge could not start.\n\n" + ex.Message,
                    "HapticFlight Bridge",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string PrepareBundle()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream payload = assembly.GetManifestResourceStream(PayloadResource))
            {
                if (payload == null) throw new InvalidDataException("The application payload is missing.");

                string digest = ComputeDigest(payload).Substring(0, 12).ToLowerInvariant();
                string cacheRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DcsExtraTelemetry", "Standalone", digest);
                // Keep runtime bookkeeping beside the signed package, never inside it.
                // The launcher's manifest verifier rejects every unlisted package file.
                string readyMarker = cacheRoot + ".ready";
                if (!File.Exists(readyMarker))
                {
                    string staging = cacheRoot + ".staging-" + Guid.NewGuid().ToString("N");
                    Directory.CreateDirectory(staging);
                    try
                    {
                        payload.Position = 0;
                        ExtractSafely(payload, staging);
                        string[] roots = Directory.GetDirectories(staging);
                        if (roots.Length != 1)
                            throw new InvalidDataException("The application bundle has an unexpected layout.");

                        Directory.CreateDirectory(Path.GetDirectoryName(cacheRoot));
                        if (!Directory.Exists(cacheRoot)) Directory.Move(roots[0], cacheRoot);
                        File.WriteAllText(readyMarker, digest);
                    }
                    finally
                    {
                        if (Directory.Exists(staging)) Directory.Delete(staging, true);
                    }
                }

                return cacheRoot;
            }
        }

        private static string ComputeDigest(Stream stream)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("X2")));
            }
        }

        private static void ExtractSafely(Stream payload, string destinationRoot)
        {
            string resolvedRoot = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                    if (!destination.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The bundle contains an unsafe path.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }
    }
}

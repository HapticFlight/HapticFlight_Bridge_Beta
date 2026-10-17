using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.Launcher
{
    internal sealed class InstalledFileReceipt
    {
        public string Destination { get; set; }
        public string InstalledSha256 { get; set; }
        public string OriginalBackup { get; set; }
    }

    internal sealed class IntegrationReceipt
    {
        public int FormatVersion { get; set; }
        public string Version { get; set; }
        public string DcsProfile { get; set; }
        public string SimHubPath { get; set; }
        public List<InstalledFileReceipt> Files { get; set; }
    }

    // Invoked by the installed Setup executable's elevated helper. The receipt is
    // stored outside the application folder so upgrades and Windows uninstall work
    // without the originally downloaded installer.
    internal static class SetupIntegration
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        internal static string FindUninstaller(string applicationRoot)
        {
            if (string.IsNullOrWhiteSpace(applicationRoot) || !Directory.Exists(applicationRoot))
                throw new DirectoryNotFoundException("The application installation folder is missing.");
            string[] candidates = Directory.GetFiles(applicationRoot, "unins*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => Regex.IsMatch(Path.GetFileName(path), @"^unins\d+\.exe$", RegexOptions.IgnoreCase)
                    && File.Exists(Path.ChangeExtension(path, ".dat")))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("The Windows uninstaller could not be identified. Use Windows Installed apps to remove HapticFlight Bridge.");
            return candidates[0];
        }

        internal static void Install(string packageRoot, string dcsProfile, string simHubPath, string receiptPath)
        {
            UpdateService.VerifyPackageManifest(packageRoot, allowRuntimeFiles: true);
            PackageInfo package = new UpdateService().ReadPackageInfo(packageRoot);
            dcsProfile = Path.GetFullPath(dcsProfile);
            simHubPath = Path.GetFullPath(simHubPath);
            if (!Directory.Exists(dcsProfile)) throw new DirectoryNotFoundException("DCS profile not found: " + dcsProfile);
            if (!File.Exists(Path.Combine(simHubPath, "SimHubWPF.exe")))
                throw new DirectoryNotFoundException("SimHub not found: " + simHubPath);
            EnsureApplicationsClosed();

            string[] sources = {
                Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua"),
                Path.Combine(packageRoot, "SimHub", "DcsExtraTelemetry.Plugin.dll")
            };
            string[] destinations = {
                Path.Combine(dcsProfile, "Scripts", "Hooks", "DCSExtraTelemetry.lua"),
                Path.Combine(simHubPath, "DcsExtraTelemetry.Plugin.dll")
            };
            foreach (string source in sources)
                if (!File.Exists(source)) throw new FileNotFoundException("Setup payload is incomplete.", source);

            IntegrationReceipt previous = ReadReceipt(receiptPath);
            if (previous != null && ((!string.IsNullOrWhiteSpace(previous.DcsProfile)
                && !SamePath(previous.DcsProfile, dcsProfile))
                || (!string.IsNullOrWhiteSpace(previous.SimHubPath)
                && !SamePath(previous.SimHubPath, simHubPath))))
                throw new InvalidOperationException("The existing integration uses different DCS or SimHub folders. Remove it before changing paths.");

            string receiptDirectory = Path.GetDirectoryName(Path.GetFullPath(receiptPath));
            Directory.CreateDirectory(receiptDirectory);
            string transactionRoot = Path.Combine(receiptDirectory, "transaction-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(transactionRoot);
            string[] rollbackFiles = new string[destinations.Length];
            bool[] existed = new bool[destinations.Length];
            bool[] touched = new bool[destinations.Length];
            var newFiles = new List<InstalledFileReceipt>();
            string baseline = Path.Combine(transactionRoot, "baseline.json");
            try
            {
                // Capture the original state before the first external write.
                InstallService.CaptureBaseline(packageRoot, dcsProfile, simHubPath, baseline, null);
                for (int i = 0; i < destinations.Length; i++)
                {
                    existed[i] = File.Exists(destinations[i]);
                    if (existed[i])
                    {
                        rollbackFiles[i] = Path.Combine(transactionRoot, "rollback-" + i);
                        File.Copy(destinations[i], rollbackFiles[i]);
                        if (previous != null && previous.Files != null)
                        {
                            InstalledFileReceipt old = previous.Files.FirstOrDefault(item => SamePath(item.Destination, destinations[i]));
                            if (old != null && !SameHash(destinations[i], old.InstalledSha256))
                                throw new IOException("An installed integration file was modified: " + destinations[i]);
                        }
                    }
                }

                for (int i = 0; i < destinations.Length; i++)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinations[i]));
                    touched[i] = true;
                    AtomicCopy(sources[i], destinations[i]);
                    if (!SameHash(destinations[i], Hash(sources[i])))
                        throw new IOException("Installed file verification failed: " + destinations[i]);

                    InstalledFileReceipt old = previous == null || previous.Files == null ? null
                        : previous.Files.FirstOrDefault(item => SamePath(item.Destination, destinations[i]));
                    newFiles.Add(new InstalledFileReceipt
                    {
                        Destination = destinations[i],
                        InstalledSha256 = Hash(sources[i]),
                        OriginalBackup = old == null ? null : old.OriginalBackup
                    });
                }
                InstallService.VerifyBaseline(baseline, null);

                for (int i = 0; i < destinations.Length; i++)
                {
                    if (newFiles[i].OriginalBackup == null && existed[i]
                        && (previous == null || !previous.Files.Any(item => SamePath(item.Destination, destinations[i]))))
                    {
                        string backup = Path.Combine(receiptDirectory, "original-" + Guid.NewGuid().ToString("N") + ".bak");
                        File.Copy(rollbackFiles[i], backup);
                        newFiles[i].OriginalBackup = backup;
                    }
                }
                WriteReceipt(receiptPath, new IntegrationReceipt
                {
                    FormatVersion = 2,
                    Version = package.version,
                    DcsProfile = dcsProfile,
                    SimHubPath = simHubPath,
                    Files = newFiles
                });
            }
            catch
            {
                for (int i = destinations.Length - 1; i >= 0; i--)
                {
                    if (!touched[i]) continue;
                    if (rollbackFiles[i] != null) AtomicCopy(rollbackFiles[i], destinations[i]);
                    else if (!existed[i] && File.Exists(destinations[i])) File.Delete(destinations[i]);
                }
                throw;
            }
            finally
            {
                try { Directory.Delete(transactionRoot, true); }
                catch (IOException) { /* Retain a diagnostic snapshot if cleanup is blocked. */ }
            }
        }

        internal static void InstallDcsHook(string packageRoot, string dcsProfile, string receiptPath)
        {
            InstallOne(packageRoot, dcsProfile, receiptPath, true);
        }

        internal static void InstallSimHubPlugin(string packageRoot, string simHubPath, string receiptPath)
        {
            InstallOne(packageRoot, simHubPath, receiptPath, false);
        }

        private static void InstallOne(string packageRoot, string targetRoot, string receiptPath, bool hook)
        {
            UpdateService.VerifyPackageManifest(packageRoot, allowRuntimeFiles: true);
            PackageInfo package = new UpdateService().ReadPackageInfo(packageRoot);
            targetRoot = Path.GetFullPath(targetRoot);
            if (hook && !Directory.Exists(targetRoot))
                throw new DirectoryNotFoundException("DCS profile not found: " + targetRoot);
            if (!hook && !File.Exists(Path.Combine(targetRoot, "SimHubWPF.exe")))
                throw new DirectoryNotFoundException("SimHub not found: " + targetRoot);
            EnsureApplicationsClosed();

            string source = hook
                ? Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua")
                : Path.Combine(packageRoot, "SimHub", "DcsExtraTelemetry.Plugin.dll");
            string destination = hook
                ? Path.Combine(targetRoot, "Scripts", "Hooks", "DCSExtraTelemetry.lua")
                : Path.Combine(targetRoot, "DcsExtraTelemetry.Plugin.dll");
            if (!File.Exists(source)) throw new FileNotFoundException("Setup payload is incomplete.", source);
            IntegrationReceipt previous = ReadReceipt(receiptPath);
            string previousPath = previous == null ? null : hook ? previous.DcsProfile : previous.SimHubPath;
            if (!string.IsNullOrWhiteSpace(previousPath) && !SamePath(previousPath, targetRoot))
                throw new InvalidOperationException("Remove the existing component before changing its installation folder.");
            InstalledFileReceipt old = previous == null ? null
                : previous.Files.FirstOrDefault(item => SamePath(item.Destination, destination));
            bool existed = File.Exists(destination);
            if (old != null && existed && !SameHash(destination, old.InstalledSha256))
                throw new IOException("An installed integration file was modified: " + destination);

            string receiptDirectory = Path.GetDirectoryName(Path.GetFullPath(receiptPath));
            Directory.CreateDirectory(receiptDirectory);
            string rollback = existed ? Path.Combine(receiptDirectory,
                "rollback-" + Guid.NewGuid().ToString("N") + ".tmp") : null;
            string original = old == null && existed ? Path.Combine(receiptDirectory,
                "original-" + Guid.NewGuid().ToString("N") + ".bak") : null;
            if (rollback != null) File.Copy(destination, rollback);
            try
            {
                AtomicCopy(source, destination);
                string digest = Hash(source);
                if (!SameHash(destination, digest)) throw new IOException("Installed file verification failed: " + destination);
                if (original != null) File.Copy(rollback, original);
                var files = previous == null ? new List<InstalledFileReceipt>()
                    : previous.Files.Where(item => !SamePath(item.Destination, destination)).ToList();
                files.Add(new InstalledFileReceipt
                {
                    Destination = destination,
                    InstalledSha256 = digest,
                    OriginalBackup = old == null ? original : old.OriginalBackup
                });
                WriteReceipt(receiptPath, new IntegrationReceipt
                {
                    FormatVersion = 2,
                    Version = package.version,
                    DcsProfile = hook ? targetRoot : previous == null ? null : previous.DcsProfile,
                    SimHubPath = hook ? previous == null ? null : previous.SimHubPath : targetRoot,
                    Files = files
                });
            }
            catch
            {
                if (rollback != null) AtomicCopy(rollback, destination);
                else if (File.Exists(destination)) File.Delete(destination);
                if (original != null && File.Exists(original)) File.Delete(original);
                throw;
            }
            finally { if (rollback != null && File.Exists(rollback)) File.Delete(rollback); }
        }

        internal static void UninstallDcsHook(string receiptPath)
        {
            UninstallOne(receiptPath, true);
        }

        internal static void UninstallSimHubPlugin(string receiptPath)
        {
            UninstallOne(receiptPath, false);
        }

        private static void UninstallOne(string receiptPath, bool hook)
        {
            IntegrationReceipt receipt = ReadReceipt(receiptPath);
            if (receipt == null)
                throw new InvalidOperationException("This component was not installed by HapticFlight Bridge.");
            string destination = hook && !string.IsNullOrWhiteSpace(receipt.DcsProfile)
                ? Path.Combine(receipt.DcsProfile, "Scripts", "Hooks", "DCSExtraTelemetry.lua")
                : !hook && !string.IsNullOrWhiteSpace(receipt.SimHubPath)
                    ? Path.Combine(receipt.SimHubPath, "DcsExtraTelemetry.Plugin.dll") : null;
            InstalledFileReceipt item = destination == null ? null
                : receipt.Files.FirstOrDefault(file => SamePath(file.Destination, destination));
            if (item == null)
                throw new InvalidOperationException("This component was not installed by HapticFlight Bridge.");
            EnsureApplicationsClosed();
            if (File.Exists(destination) && !SameHash(destination, item.InstalledSha256))
                throw new IOException("An integration file changed since installation: " + destination);
            if (item.OriginalBackup != null && !File.Exists(item.OriginalBackup))
                throw new FileNotFoundException("The original integration backup is missing.", item.OriginalBackup);
            string snapshot = File.Exists(destination) ? Path.Combine(Path.GetDirectoryName(receiptPath),
                "rollback-" + Guid.NewGuid().ToString("N") + ".tmp") : null;
            if (snapshot != null) File.Copy(destination, snapshot);
            try
            {
                if (item.OriginalBackup != null) AtomicCopy(item.OriginalBackup, destination);
                else if (File.Exists(destination)) File.Delete(destination);
                var remaining = receipt.Files.Where(file => !SamePath(file.Destination, destination)).ToList();
                if (remaining.Count == 0) File.Delete(receiptPath);
                else WriteReceipt(receiptPath, new IntegrationReceipt
                {
                    FormatVersion = 2,
                    Version = receipt.Version,
                    DcsProfile = hook ? null : receipt.DcsProfile,
                    SimHubPath = hook ? receipt.SimHubPath : null,
                    Files = remaining
                });
            }
            catch
            {
                if (snapshot != null) AtomicCopy(snapshot, destination);
                throw;
            }
            finally { if (snapshot != null && File.Exists(snapshot)) File.Delete(snapshot); }
            if (item.OriginalBackup != null)
            {
                try { File.Delete(item.OriginalBackup); }
                catch (IOException) { /* Component removal completed; retain the stale backup. */ }
            }
        }

        internal static void Uninstall(string receiptPath)
        {
            IntegrationReceipt receipt = ReadReceipt(receiptPath);
            if (receipt == null) return;
            EnsureApplicationsClosed();
            if (receipt.Files == null || receipt.Files.Count < 1 || receipt.Files.Count > 2)
                throw new InvalidDataException("The installation receipt is incomplete.");
            foreach (InstalledFileReceipt item in receipt.Files)
            {
                if (File.Exists(item.Destination) && !SameHash(item.Destination, item.InstalledSha256))
                    throw new IOException("An integration file changed since installation: " + item.Destination);
                if (item.OriginalBackup != null && !File.Exists(item.OriginalBackup))
                    throw new FileNotFoundException("The original integration backup is missing.", item.OriginalBackup);
            }
            string rollbackRoot = Path.Combine(Path.GetDirectoryName(receiptPath),
                "uninstall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rollbackRoot);
            var snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (InstalledFileReceipt item in receipt.Files.Where(item => File.Exists(item.Destination)))
                {
                    string snapshot = Path.Combine(rollbackRoot, snapshots.Count.ToString() + ".bak");
                    File.Copy(item.Destination, snapshot);
                    snapshots.Add(item.Destination, snapshot);
                }
                try
                {
                    foreach (InstalledFileReceipt item in receipt.Files)
                    {
                        if (item.OriginalBackup != null) AtomicCopy(item.OriginalBackup, item.Destination);
                        else if (File.Exists(item.Destination)) File.Delete(item.Destination);
                    }
                    File.Delete(receiptPath);
                }
                catch
                {
                    foreach (KeyValuePair<string, string> snapshot in snapshots)
                        AtomicCopy(snapshot.Value, snapshot.Key);
                    throw;
                }
                foreach (InstalledFileReceipt item in receipt.Files)
                    if (item.OriginalBackup != null)
                    {
                        try { File.Delete(item.OriginalBackup); }
                        catch (IOException) { /* The integration is already removed; retain the backup. */ }
                    }
            }
            finally
            {
                try { Directory.Delete(rollbackRoot, true); }
                catch (IOException) { /* Preserve the completed operation if cleanup is blocked. */ }
            }
        }

        private static void EnsureApplicationsClosed()
        {
            if (Process.GetProcessesByName("DCS").Length > 0
                || Process.GetProcessesByName("SimHubWPF").Length > 0)
                throw new InvalidOperationException("Close DCS and SimHub before changing their integration files.");
        }

        internal static IntegrationReceipt ReadReceipt(string path)
        {
            if (!File.Exists(path)) return null;
            IntegrationReceipt receipt = Serializer.Deserialize<IntegrationReceipt>(File.ReadAllText(path, Encoding.UTF8));
            if (receipt == null || (receipt.FormatVersion != 1 && receipt.FormatVersion != 2))
                throw new InvalidDataException("Unsupported installation receipt.");
            if (receipt.Files == null || receipt.Files.Count < 1 || receipt.Files.Count > 2
                || (receipt.FormatVersion == 1 && receipt.Files.Count != 2))
                throw new InvalidDataException("The installation receipt is incomplete.");
            var expected = new List<string>();
            if (!string.IsNullOrWhiteSpace(receipt.DcsProfile))
                expected.Add(Path.Combine(receipt.DcsProfile, "Scripts", "Hooks", "DCSExtraTelemetry.lua"));
            if (!string.IsNullOrWhiteSpace(receipt.SimHubPath))
                expected.Add(Path.Combine(receipt.SimHubPath, "DcsExtraTelemetry.Plugin.dll"));
            if (expected.Count != receipt.Files.Count)
                throw new InvalidDataException("The installation receipt has mismatched component paths.");
            string backupFolder = Path.GetDirectoryName(Path.GetFullPath(path));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (InstalledFileReceipt file in receipt.Files)
            {
                if (file == null || !expected.Any(item => SamePath(file.Destination, item))
                    || !seen.Add(Path.GetFullPath(file.Destination))
                    || !Regex.IsMatch(file.InstalledSha256 ?? string.Empty, "^[0-9A-Fa-f]{64}$"))
                    throw new InvalidDataException("The installation receipt has an invalid file record.");
                if (file.OriginalBackup != null
                    && (!SamePath(Path.GetDirectoryName(file.OriginalBackup), backupFolder)
                        || !Regex.IsMatch(Path.GetFileName(file.OriginalBackup),
                            "^original-[0-9a-f]{32}\\.bak$", RegexOptions.IgnoreCase)))
                    throw new InvalidDataException("The installation receipt has an unsafe backup path.");
            }
            return receipt;
        }

        private static void WriteReceipt(string path, IntegrationReceipt receipt)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, Serializer.Serialize(receipt), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static void AtomicCopy(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static bool SamePath(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second)
                && string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameHash(string path, string expected)
        {
            return string.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
    }
}

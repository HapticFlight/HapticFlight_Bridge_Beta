using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.Launcher
{
    internal sealed class HashRecord
    {
        public string Path { get; set; }
        public string Sha256 { get; set; }
    }

    internal sealed class HashScope
    {
        public string Root { get; set; }
        public List<HashRecord> Files { get; set; }
    }

    internal sealed class SafetyBaseline
    {
        public int FormatVersion { get; set; }
        public string CreatedUtc { get; set; }
        public List<HashScope> DcsScopes { get; set; }
        public HashScope SimHubScope { get; set; }
    }

    internal static class InstallService
    {
        private static readonly string[] PluginFiles =
        {
            "DcsExtraTelemetry.Plugin.dll",
            "DcsExtraTelemetry.Plugin.pdb"
        };

        public static void VerifyPackage(string packageRoot, Action<string> log)
        {
            UpdateService.VerifyPackageManifest(packageRoot, allowRuntimeFiles: true);
            Log(log, "PASS: Every packaged file matches manifest.sha256.");
        }

        public static void CaptureBaseline(string packageRoot, string dcsProfile, string simHubPath,
            string outputPath, Action<string> log)
        {
            ValidateProfile(dcsProfile);
            ValidateSimHub(simHubPath);

            string dcsScripts = Path.Combine(dcsProfile, "Scripts");
            string ownedHook = Path.Combine(dcsScripts, "Hooks", "DCSExtraTelemetry.lua");
            string[] ownedPluginPaths = PluginFiles.Select(file => Path.Combine(simHubPath, file)).ToArray();

            var baseline = new SafetyBaseline
            {
                FormatVersion = 1,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                DcsScopes = new List<HashScope>
                {
                    new HashScope
                    {
                        Root = dcsScripts,
                        Files = GetHashRecords(dcsScripts, new[] { ownedHook }, SearchOption.AllDirectories)
                    }
                },
                SimHubScope = new HashScope
                {
                    Root = simHubPath,
                    Files = GetHashRecords(simHubPath, ownedPluginPaths, SearchOption.TopDirectoryOnly)
                }
            };

            string json = new JavaScriptSerializer().Serialize(baseline);
            File.WriteAllText(Path.GetFullPath(outputPath), json, new UTF8Encoding(true));
            int count = baseline.DcsScopes.Sum(scope => scope.Files.Count) + baseline.SimHubScope.Files.Count;
            Log(log, "Baseline captured: " + Path.GetFullPath(outputPath));
            Log(log, "DCS profiles: 1; unrelated files hashed: " + count);
        }

        public static void VerifyBaseline(string baselinePath, Action<string> log)
        {
            if (!File.Exists(baselinePath)) throw new FileNotFoundException("Baseline file was not found.", baselinePath);
            SafetyBaseline baseline = new JavaScriptSerializer().Deserialize<SafetyBaseline>(File.ReadAllText(baselinePath));
            if (baseline == null || baseline.FormatVersion != 1) throw new InvalidDataException("Unsupported baseline format.");

            var differences = new List<string>();
            foreach (HashScope scope in baseline.DcsScopes ?? new List<HashScope>())
            {
                string ownedHook = Path.Combine(scope.Root, "Hooks", "DCSExtraTelemetry.lua");
                differences.AddRange(Compare(scope.Files,
                    GetHashRecords(scope.Root, new[] { ownedHook }, SearchOption.AllDirectories)));
            }

            string[] ownedPluginPaths = PluginFiles.Select(file => Path.Combine(baseline.SimHubScope.Root, file)).ToArray();
            differences.AddRange(Compare(baseline.SimHubScope.Files,
                GetHashRecords(baseline.SimHubScope.Root, ownedPluginPaths, SearchOption.TopDirectoryOnly)));

            if (differences.Count > 0)
                throw new InvalidOperationException(differences.Count + " unrelated file difference(s) detected: "
                    + string.Join("; ", differences.Take(8)));

            Log(log, "PASS: Every unrelated DCS Scripts file and SimHub root file matches the baseline byte-for-byte.");
        }

        public static void InstallDcsHook(string packageRoot, string dcsProfile, bool replaceExisting, Action<string> log)
        {
            ValidateProfile(dcsProfile);
            string source = Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua");
            if (!File.Exists(source)) throw new FileNotFoundException("Packaged DCS Hook is missing.", source);

            string scriptsPath = Path.Combine(dcsProfile, "Scripts");
            string hooksPath = Path.Combine(scriptsPath, "Hooks");
            string destination = Path.Combine(hooksPath, "DCSExtraTelemetry.lua");
            Directory.CreateDirectory(scriptsPath);
            List<HashRecord> before = GetHashRecords(scriptsPath, new[] { destination }, SearchOption.AllDirectories);

            if (File.Exists(destination))
            {
                if (HashesMatch(source, destination))
                {
                    Log(log, "Already installed: " + destination);
                    return;
                }
                if (!replaceExisting) throw new IOException("A different DCSExtraTelemetry.lua already exists: " + destination);
                BackupFile(packageRoot, "DCS", destination, Path.GetFileName(dcsProfile) + "-DCSExtraTelemetry.lua");
            }

            Directory.CreateDirectory(hooksPath);
            SafeCopy(source, destination);
            List<string> differences = Compare(before,
                GetHashRecords(scriptsPath, new[] { destination }, SearchOption.AllDirectories));
            if (differences.Count > 0) throw new InvalidOperationException("Safety check failed: an unrelated DCS Scripts file changed.");
            Log(log, "Installed: " + destination);
            Log(log, "Export.lua was not read, written, reordered, or used.");
        }

        public static void RemoveDcsHook(string packageRoot, string dcsProfile, bool force, Action<string> log)
        {
            ValidateProfile(dcsProfile);
            string source = Path.Combine(packageRoot, "DCS", "DCSExtraTelemetry.lua");
            string destination = Path.Combine(dcsProfile, "Scripts", "Hooks", "DCSExtraTelemetry.lua");
            if (!File.Exists(destination))
            {
                Log(log, "Not installed: " + destination);
                return;
            }
            if (!force && (!File.Exists(source) || !HashesMatch(source, destination)))
                throw new IOException("The installed Hook differs from this package. Refusing to remove it: " + destination);
            File.Delete(destination);
            Log(log, "Removed: " + destination);
            Log(log, "No directory, Export.lua, or third-party Hook was removed.");
        }

        public static void InstallSimHubPlugin(string packageRoot, string simHubPath, bool replaceExisting, Action<string> log)
        {
            ValidateSimHub(simHubPath);
            foreach (string file in PluginFiles)
            {
                string source = Path.Combine(packageRoot, "SimHub", file);
                if (!File.Exists(source))
                {
                    if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                    throw new FileNotFoundException("Package file is missing.", source);
                }
                string destination = Path.Combine(simHubPath, file);
                if (File.Exists(destination))
                {
                    if (HashesMatch(source, destination))
                    {
                        Log(log, "Already installed: " + destination);
                        continue;
                    }
                    if (!replaceExisting) throw new IOException("A different " + file + " already exists in SimHub.");
                    BackupFile(packageRoot, "SimHub", destination, file);
                }
                SafeCopy(source, destination);
                Log(log, "Installed: " + destination);
            }
            Log(log, "Only DcsExtraTelemetry.Plugin.dll and its optional PDB were written.");
        }

        public static void RemoveSimHubPlugin(string packageRoot, string simHubPath, bool force, Action<string> log)
        {
            ValidateSimHub(simHubPath);
            foreach (string file in PluginFiles)
            {
                string source = Path.Combine(packageRoot, "SimHub", file);
                string destination = Path.Combine(simHubPath, file);
                if (!File.Exists(destination)) continue;
                if (!force && File.Exists(source) && !HashesMatch(source, destination))
                    throw new IOException("The installed " + file + " differs from this package. Refusing to remove it.");
                File.Delete(destination);
                Log(log, "Removed: " + destination);
            }
            Log(log, "No other SimHub plugin, configuration, profile, or file was removed.");
        }

        private static List<HashRecord> GetHashRecords(string root, IEnumerable<string> excludedPaths, SearchOption option)
        {
            if (!Directory.Exists(root)) return new List<HashRecord>();
            var excluded = new HashSet<string>((excludedPaths ?? Enumerable.Empty<string>()).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            return Directory.EnumerateFiles(root, "*", option)
                .Select(Path.GetFullPath)
                .Where(path => !excluded.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new HashRecord { Path = path, Sha256 = ComputeSha256(path) })
                .ToList();
        }

        private static List<string> Compare(IEnumerable<HashRecord> before, IEnumerable<HashRecord> after)
        {
            var beforeMap = (before ?? Enumerable.Empty<HashRecord>()).ToDictionary(record => record.Path,
                StringComparer.OrdinalIgnoreCase);
            var afterMap = (after ?? Enumerable.Empty<HashRecord>()).ToDictionary(record => record.Path,
                StringComparer.OrdinalIgnoreCase);
            var differences = new List<string>();
            foreach (KeyValuePair<string, HashRecord> item in beforeMap)
            {
                HashRecord current;
                if (!afterMap.TryGetValue(item.Key, out current)) differences.Add("Missing: " + item.Value.Path);
                else if (!string.Equals(item.Value.Sha256, current.Sha256, StringComparison.OrdinalIgnoreCase))
                    differences.Add("Changed: " + item.Value.Path);
            }
            differences.AddRange(afterMap.Where(item => !beforeMap.ContainsKey(item.Key)).Select(item => "Added: " + item.Value.Path));
            return differences.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void SafeCopy(string source, string destination)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.Copy(source, temporary, true);
            try
            {
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void BackupFile(string packageRoot, string category, string source, string name)
        {
            string folder = Path.Combine(packageRoot, "Backups", category + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            File.Copy(source, Path.Combine(folder, name), true);
        }

        private static void ValidateProfile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                throw new DirectoryNotFoundException("DCS profile folder does not exist: " + path);
        }

        private static void ValidateSimHub(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.Combine(path, "SimHubWPF.exe")))
                throw new DirectoryNotFoundException("SimHub was not found at: " + path);
        }

        private static bool HashesMatch(string first, string second)
        {
            return string.Equals(ComputeSha256(first), ComputeSha256(second), StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void Log(Action<string> log, string message)
        {
            if (log != null) log(message);
        }
    }
}

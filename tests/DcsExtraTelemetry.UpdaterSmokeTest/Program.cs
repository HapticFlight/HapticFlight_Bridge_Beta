using DcsExtraTelemetry.Launcher;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DcsExtraTelemetry.UpdaterSmokeTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "DcsExtraTelemetry-UpdaterTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
            try
            {
                TestVersionOrdering();
                TestChecksumAssetParsing();
                TestPublishedDigestAgreement();
                TestPackageRepositoryConfiguration(testRoot);
                TestManifestVerification(testRoot);
                TestSafeExtraction(testRoot);
                TestNativeInstallWorkflow(testRoot);
                TestSetupLifecycle(testRoot);
                TestUninstallerDiscovery(testRoot);
                TestInstallerDownload(testRoot);
                TestTelemetryRecordingAnalysis(testRoot);
                if (Array.IndexOf(args, "--live") >= 0) TestLivePublicReleaseCheck();
                if (Array.IndexOf(args, "--live-download") >= 0) TestLivePublicReleaseDownload();
                int packageIndex = Array.IndexOf(args, "--package");
                if (packageIndex >= 0)
                {
                    if (packageIndex + 1 >= args.Length) throw new ArgumentException("--package requires a path");
                    TestPackagedRelease(args[packageIndex + 1]);
                }
                Console.WriteLine("PASS: updater, package integrity, ZIP defenses, native install workflow, and telemetry analysis.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
            finally
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }
        }

        private static void TestVersionOrdering()
        {
            ReleaseVersion test10 = Parse("0.1.0-test.10");
            ReleaseVersion test11 = Parse("v0.1.0-test.11");
            ReleaseVersion stable = Parse("0.1.0");
            ReleaseVersion nextTest = Parse("0.1.1-test.1");
            Assert(test11.CompareTo(test10) > 0, "test.11 was not newer than test.10");
            Assert(stable.CompareTo(test11) > 0, "stable release was not newer than its prerelease");
            Assert(nextTest.CompareTo(stable) > 0, "next patch prerelease was not newer than current stable");
            ReleaseVersion invalid;
            Assert(!ReleaseVersion.TryParse("latest", out invalid), "invalid release tag was accepted");
        }

        private static void TestManifestVerification(string testRoot)
        {
            string package = Path.Combine(testRoot, "package");
            Directory.CreateDirectory(package);
            string payload = Path.Combine(package, "payload.txt");
            File.WriteAllText(payload, "trusted", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(package, "manifest.sha256"),
                UpdateService.ComputeSha256(payload) + "  payload.txt" + Environment.NewLine,
                Encoding.ASCII);

            UpdateService.VerifyPackageManifest(package);
            File.WriteAllText(payload, "tampered", new UTF8Encoding(false));
            AssertThrows<InvalidDataException>(() => UpdateService.VerifyPackageManifest(package),
                "tampered package file was accepted");

            File.WriteAllText(payload, "trusted", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(package, "manifest.sha256"),
                UpdateService.ComputeSha256(payload) + "  payload.txt" + Environment.NewLine,
                Encoding.ASCII);
            File.WriteAllText(Path.Combine(package, "unsigned.txt"), "unsigned", new UTF8Encoding(false));
            AssertThrows<InvalidDataException>(() => UpdateService.VerifyPackageManifest(package),
                "unsigned package file was accepted");
        }

        private static void TestChecksumAssetParsing()
        {
            string expectedName = "DcsExtraTelemetry-0.1.0-test.132.zip";
            string expectedHash = new string('A', 64);
            string checksum = expectedHash.ToLowerInvariant() + "  " + expectedName + Environment.NewLine;
            Assert(UpdateService.ReadChecksumAsset(checksum, expectedName) == expectedHash,
                "checksum asset did not return the expected SHA-256 value");
            Assert(UpdateService.ReadChecksumAsset(expectedHash + "  other.zip", expectedName) == null,
                "checksum asset accepted the wrong package name");
        }

        private static void TestPublishedDigestAgreement()
        {
            string name = "HapticFlight-Bridge-Setup-0.1.0-test.133.exe";
            string hash = new string('A', 64);
            Assert(UpdateService.ValidatePublishedDigest("sha256:" + hash, hash + "  " + name, name) == hash,
                "matching Setup checksums were rejected");
            Assert(UpdateService.ValidatePublishedDigest(null, hash + "  " + name, name) == hash,
                "a valid checksum without GitHub's optional digest was rejected");
            Assert(UpdateService.ValidatePublishedDigest("sha256:" + hash, hash + "  other.exe", name) == null,
                "a checksum for another installer was accepted");
            AssertThrows<InvalidDataException>(() => UpdateService.ValidatePublishedDigest(
                "sha256:" + new string('B', 64), hash + "  " + name, name),
                "conflicting GitHub and release checksums were accepted");
        }

        private static void TestUninstallerDiscovery(string testRoot)
        {
            string folder = Path.Combine(testRoot, "uninstaller");
            Directory.CreateDirectory(folder);
            AssertThrows<InvalidOperationException>(() => SetupIntegration.FindUninstaller(folder),
                "missing uninstaller was accepted");
            string executable = Path.Combine(folder, "unins000.exe");
            File.WriteAllText(executable, "fixture");
            AssertThrows<InvalidOperationException>(() => SetupIntegration.FindUninstaller(folder),
                "uninstaller without its data file was accepted");
            File.WriteAllText(Path.ChangeExtension(executable, ".dat"), "fixture");
            Assert(SetupIntegration.FindUninstaller(folder) == executable,
                "complete uninstaller was not found");
        }

        private static void TestInstallerDownload(string testRoot)
        {
            string source = Path.Combine(testRoot, "source-setup.exe");
            string cache = Path.Combine(testRoot, "installer-cache");
            File.WriteAllText(source, "verified installer fixture");
            var update = new UpdateInfo
            {
                AssetName = "HapticFlight-Bridge-Setup-0.1.0-test.133.exe",
                Sha256 = UpdateService.ComputeSha256(source),
                DownloadUrl = new Uri(source).AbsoluteUri
            };
            var service = new UpdateService();
            string installed = service.DownloadInstallerAsync(update, cache).GetAwaiter().GetResult();
            Assert(File.ReadAllText(installed) == "verified installer fixture", "Setup download failed");
            File.WriteAllText(installed, "corrupt cached installer");
            installed = service.DownloadInstallerAsync(update, cache).GetAwaiter().GetResult();
            Assert(File.ReadAllText(installed) == "verified installer fixture", "corrupt Setup cache was reused");
            update.Sha256 = new string('B', 64);
            AssertThrows<InvalidDataException>(() => service.DownloadInstallerAsync(update, cache).GetAwaiter().GetResult(),
                "incorrect Setup digest was accepted");
        }

        private static void TestPackageRepositoryConfiguration(string testRoot)
        {
            string package = Path.Combine(testRoot, "repository-package");
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, "package-info.json"),
                "{\"version\":\"0.1.0-test.132\",\"repository\":\"coledesigns/hapticflight-bridge-beta\"}",
                new UTF8Encoding(false));
            var service = new UpdateService();
            PackageInfo info = service.ReadPackageInfo(package);
            service.ConfigureRepository(info.repository);
            Assert(string.Equals(service.Repository, "coledesigns/hapticflight-bridge-beta", StringComparison.Ordinal),
                "package-configured public update repository was not applied");
        }

        private static void TestSafeExtraction(string testRoot)
        {
            string safeZip = Path.Combine(testRoot, "safe.zip");
            using (ZipArchive archive = ZipFile.Open(safeZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("bundle/file.txt");
                using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write("safe");
            }
            string safeDestination = Path.Combine(testRoot, "safe-output");
            UpdateService.ExtractZipSafely(safeZip, safeDestination);
            Assert(File.Exists(Path.Combine(safeDestination, "bundle", "file.txt")), "safe ZIP was not extracted");

            string unsafeZip = Path.Combine(testRoot, "unsafe.zip");
            using (ZipArchive archive = ZipFile.Open(unsafeZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../escape.txt");
                using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write("unsafe");
            }
            string unsafeDestination = Path.Combine(testRoot, "unsafe-output");
            AssertThrows<InvalidDataException>(() => UpdateService.ExtractZipSafely(unsafeZip, unsafeDestination),
                "ZIP traversal entry was accepted");
            Assert(!File.Exists(Path.Combine(testRoot, "escape.txt")), "ZIP traversal wrote outside the destination");
        }

        private static void TestNativeInstallWorkflow(string testRoot)
        {
            string package = Path.Combine(testRoot, "native-package");
            string dcsProfile = Path.Combine(testRoot, "Saved Games", "DCS");
            string simHub = Path.Combine(testRoot, "SimHub");
            Directory.CreateDirectory(Path.Combine(package, "DCS"));
            Directory.CreateDirectory(Path.Combine(package, "SimHub"));
            Directory.CreateDirectory(Path.Combine(dcsProfile, "Scripts"));
            Directory.CreateDirectory(simHub);

            File.WriteAllText(Path.Combine(package, "DCS", "DCSExtraTelemetry.lua"), "native hook");
            File.WriteAllText(Path.Combine(package, "SimHub", "DcsExtraTelemetry.Plugin.dll"), "native plugin");
            File.WriteAllText(Path.Combine(package, "SimHub", "DcsExtraTelemetry.Plugin.pdb"), "native symbols");
            File.WriteAllText(Path.Combine(dcsProfile, "Scripts", "third-party.lua"), "unchanged");
            File.WriteAllText(Path.Combine(simHub, "SimHubWPF.exe"), "fixture");
            File.WriteAllText(Path.Combine(simHub, "third-party.plugin"), "unchanged");

            string baseline = Path.Combine(package, "baseline.json");
            InstallService.CaptureBaseline(package, dcsProfile, simHub, baseline, null);
            InstallService.InstallDcsHook(package, dcsProfile, false, null);
            InstallService.InstallSimHubPlugin(package, simHub, false, null);
            Assert(File.Exists(Path.Combine(dcsProfile, "Scripts", "Hooks", "DCSExtraTelemetry.lua")),
                "native Hook install did not create its owned file");
            Assert(File.Exists(Path.Combine(simHub, "DcsExtraTelemetry.Plugin.dll")),
                "native SimHub install did not create its owned file");
            InstallService.VerifyBaseline(baseline, null);

            InstallService.RemoveSimHubPlugin(package, simHub, false, null);
            InstallService.RemoveDcsHook(package, dcsProfile, false, null);
            InstallService.VerifyBaseline(baseline, null);
            Assert(File.ReadAllText(Path.Combine(dcsProfile, "Scripts", "third-party.lua")) == "unchanged",
                "native workflow changed an unrelated DCS file");
            Assert(File.ReadAllText(Path.Combine(simHub, "third-party.plugin")) == "unchanged",
                "native workflow changed an unrelated SimHub file");
        }

        private static void TestSetupLifecycle(string testRoot)
        {
            string package = Path.Combine(testRoot, "setup-package");
            string dcs = Path.Combine(testRoot, "setup-dcs");
            string simHub = Path.Combine(testRoot, "setup-simhub");
            string receipt = Path.Combine(testRoot, "setup-state", "receipt.json");
            string hook = Path.Combine(dcs, "Scripts", "Hooks", "DCSExtraTelemetry.lua");
            string plugin = Path.Combine(simHub, "DcsExtraTelemetry.Plugin.dll");
            Directory.CreateDirectory(Path.Combine(package, "DCS"));
            Directory.CreateDirectory(Path.Combine(package, "SimHub"));
            Directory.CreateDirectory(Path.GetDirectoryName(hook));
            Directory.CreateDirectory(simHub);
            File.WriteAllText(Path.Combine(simHub, "SimHubWPF.exe"), "fixture");
            File.WriteAllText(Path.Combine(dcs, "Scripts", "unrelated.lua"), "preserve");
            File.WriteAllText(Path.Combine(simHub, "unrelated.dll"), "preserve");
            File.WriteAllText(hook, "prior hook");
            File.WriteAllText(plugin, "prior plugin");
            File.WriteAllText(Path.Combine(package, "DCS", "DCSExtraTelemetry.lua"), "new hook");
            File.WriteAllText(Path.Combine(package, "SimHub", "DcsExtraTelemetry.Plugin.dll"), "new plugin");
            File.WriteAllText(Path.Combine(package, "package-info.json"),
                "{\"version\":\"0.1.0-test.132\",\"repository\":\"example/bridge\"}");
            WriteSetupManifest(package);

            SetupIntegration.Install(package, dcs, simHub, receipt);
            Assert(File.ReadAllText(hook) == "new hook" && File.ReadAllText(plugin) == "new plugin",
                "Setup did not install both integration points");
            Assert(SetupIntegration.ReadReceipt(receipt) != null, "Setup did not save an installation receipt");
            SetupIntegration.Install(package, dcs, simHub, receipt);
            File.WriteAllText(Path.Combine(package, "DCS", "DCSExtraTelemetry.lua"), "upgraded hook");
            File.WriteAllText(Path.Combine(package, "SimHub", "DcsExtraTelemetry.Plugin.dll"), "upgraded plugin");
            File.WriteAllText(Path.Combine(package, "package-info.json"),
                "{\"version\":\"0.1.0-test.133\",\"repository\":\"example/bridge\"}");
            WriteSetupManifest(package);
            SetupIntegration.Install(package, dcs, simHub, receipt);
            Assert(File.ReadAllText(hook) == "upgraded hook" && File.ReadAllText(plugin) == "upgraded plugin",
                "Setup upgrade did not replace both integration points");
            File.WriteAllText(plugin, "modified plugin");
            AssertThrows<IOException>(() => SetupIntegration.Uninstall(receipt),
                "Setup removed a user-modified integration file");
            File.WriteAllText(plugin, "upgraded plugin");
            SetupIntegration.Uninstall(receipt);
            Assert(File.ReadAllText(hook) == "prior hook" && File.ReadAllText(plugin) == "prior plugin",
                "Setup uninstall did not restore previous integration files");
            Assert(File.ReadAllText(Path.Combine(dcs, "Scripts", "unrelated.lua")) == "preserve"
                && File.ReadAllText(Path.Combine(simHub, "unrelated.dll")) == "preserve",
                "Setup changed unrelated files");

            SetupIntegration.InstallDcsHook(package, dcs, receipt);
            Assert(File.ReadAllText(hook) == "upgraded hook" && File.ReadAllText(plugin) == "prior plugin",
                "Installing only the DCS Hook changed the SimHub plugin");
            Assert(SetupIntegration.ReadReceipt(receipt).Files.Count == 1,
                "A single installed component did not produce a partial receipt");
            SetupIntegration.InstallSimHubPlugin(package, simHub, receipt);
            Assert(File.ReadAllText(plugin) == "upgraded plugin"
                && SetupIntegration.ReadReceipt(receipt).Files.Count == 2,
                "Installing the SimHub plugin changed or lost the DCS Hook");
            SetupIntegration.UninstallDcsHook(receipt);
            Assert(File.ReadAllText(hook) == "prior hook" && File.ReadAllText(plugin) == "upgraded plugin",
                "Removing only the DCS Hook changed the SimHub plugin");
            SetupIntegration.UninstallSimHubPlugin(receipt);
            Assert(File.ReadAllText(plugin) == "prior plugin" && !File.Exists(receipt),
                "Removing the final component did not restore SimHub and clear the receipt");

            SetupIntegration.InstallDcsHook(package, dcs, receipt);
            SetupIntegration.Uninstall(receipt);
            Assert(File.ReadAllText(hook) == "prior hook" && !File.Exists(receipt),
                "Application uninstall did not clean up a Hook-only installation");
        }

        private static void WriteSetupManifest(string package)
        {
            string manifest = Path.Combine(package, "manifest.sha256");
            var lines = Directory.GetFiles(package, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, manifest, StringComparison.OrdinalIgnoreCase))
                .Select(path => UpdateService.ComputeSha256(path) + "  " + path.Substring(package.Length + 1));
            File.WriteAllLines(manifest, lines, Encoding.ASCII);
        }

        private static void TestLivePublicReleaseCheck()
        {
            UpdateInfo update = new UpdateService().FindUpdateAsync("0.1.0-test.9").GetAwaiter().GetResult();
            Assert(update != null, "public GitHub release check did not find a version newer than test.9");
            Assert(update.Version.CompareTo(Parse("0.1.0-test.9")) > 0, "public GitHub release check returned an older version");
            Assert(!string.IsNullOrWhiteSpace(update.Sha256) && update.Sha256.Length == 64,
                "public GitHub release did not provide a SHA-256 digest");
            Assert(!string.IsNullOrWhiteSpace(update.DownloadUrl), "public GitHub release did not provide a download URL");
        }

        private static void TestLivePublicReleaseDownload()
        {
            var service = new UpdateService();
            UpdateInfo update = service.FindUpdateAsync("0.1.0-test.92").GetAwaiter().GetResult();
            Assert(update != null, "public GitHub release check did not find an update newer than test.92");
            string packageRoot = service.DownloadAndVerifyAsync(update, null).GetAwaiter().GetResult();
            PackageInfo info = service.ReadPackageInfo(packageRoot);
            Assert(string.Equals(info.version, update.Version.ToString(), StringComparison.OrdinalIgnoreCase),
                "downloaded package version did not match the selected update");
            UpdateService.VerifyPackageManifest(packageRoot, allowRuntimeFiles: true);
        }

        private static void TestTelemetryRecordingAnalysis(string testRoot)
        {
            string recording = Path.Combine(testRoot, "recording.jsonl");
            string sample = "{\"sentAtMs\":1000,\"aircraft\":{\"dcsType\":\"UH-60L_DAP\"},\"capabilities\":{" +
                "\"rotorDynamics\":true,\"gearStruts\":true,\"weaponEffects\":true," +
                "\"cockpitStates\":true,\"brakes\":true,\"doors\":true},\"values\":{" +
                "\"touchdownPulse\":1,\"liftoffPulse\":1,\"touchdownStrength\":0.72," +
                "\"wheelBrakesPosition\":0.8,\"brakingIntensity\":0.55,\"doorMovementIntensity\":0.4," +
                "\"lightGunIntensity\":0.6,\"heavyGunIntensity\":0.7,\"rocketLaunchIntensity\":0.8," +
                "\"missileLaunchIntensity\":0.9,\"flareReleasePulse\":1,\"translationalLiftTransition\":0.5," +
                "\"groundRollIntensity\":0.45,\"rotorDroopIntensity\":0.35,\"airframeImpactPulse\":1}}";
            File.WriteAllText(recording, sample + Environment.NewLine + "{malformed" + Environment.NewLine, new UTF8Encoding(false));

            TelemetryAnalysisResult result = TelemetryRecordingAnalyzer.Analyze(recording);
            Assert(result.ValidSamples == 1, "telemetry analyzer did not count the sample");
            Assert(result.AircraftFamily == "UH60", "telemetry analyzer did not normalize the aircraft family");
            Assert(result.Rotor && result.Ground && result.Weapons && result.Cockpit && result.Brakes && result.Doors,
                "telemetry analyzer did not report available signal families");
            Assert(result.Touchdown && result.Liftoff && result.BrakeApplied && result.DoorMoved,
                "telemetry analyzer did not observe ground/cockpit test events");
            Assert(result.LightGun && result.HeavyGun && result.Rocket && result.Missile && result.Countermeasure && result.Etl,
                "telemetry analyzer did not observe weapon, countermeasure, or ETL events");
            Assert(Math.Abs(result.MaxTouchdown - 0.72) < 0.0001 && Math.Abs(result.MaxBraking - 0.55) < 0.0001,
                "telemetry analyzer peak values were incorrect");
            Assert(result.EffectMap.Count == 41 && result.ActiveEffectCounts != null && result.ActiveEffectCounts.Length == 1,
                "telemetry analyzer did not build the bounded effect buckets");
            Assert(result.CategoryLoads != null && result.CategoryLoads.Length == 4 && result.PeakConcurrency > 0,
                "telemetry analyzer did not precompute load and concurrency metrics");
            EffectMapTrack impact = result.EffectMap.Find(track => track.Name == "Airframe impact pulse");
            Assert(impact != null && impact.Episodes.Count == 1 && impact.PeakIntensity == 1 && impact.ActiveSeconds > 0,
                "telemetry analyzer did not preserve the single-sample impulse episode");
        }

        private static void TestPackagedRelease(string packageRoot)
        {
            UpdateService.VerifyPackageManifest(packageRoot);
            var service = new UpdateService();
            PackageInfo info = service.ReadPackageInfo(packageRoot);
            service.ConfigureRepository(info.repository);
            Assert(!string.IsNullOrWhiteSpace(service.Repository),
                "packaged release repository metadata was not usable");
        }

        private static ReleaseVersion Parse(string text)
        {
            ReleaseVersion result;
            if (!ReleaseVersion.TryParse(text, out result)) throw new InvalidOperationException("Could not parse " + text);
            return result;
        }

        private static void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

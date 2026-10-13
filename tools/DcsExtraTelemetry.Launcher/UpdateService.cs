using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.Launcher
{
    internal sealed class PackageInfo
    {
        public string version { get; set; }

        public string repository { get; set; }

        public string hapticEngineVersion { get; set; }

        public string groundProfileVersion { get; set; }

        public string lastKnownWorkingVersion { get; set; }
    }

    internal sealed class InstallState
    {
        public string installedVersion { get; set; }

        public string activePackageRoot { get; set; }

        public string dcsProfile { get; set; }

        public string simHubPath { get; set; }
    }

    internal sealed class UpdateInfo
    {
        public ReleaseVersion Version { get; set; }

        public string TagName { get; set; }

        public string AssetName { get; set; }

        public string Sha256 { get; set; }

        public string ReleaseUrl { get; set; }

        public string DownloadUrl { get; set; }
    }

    internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        private static readonly Regex Pattern = new Regex(
            @"^v?(\d+)\.(\d+)\.(\d+)(?:-test\.(\d+))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private ReleaseVersion(int major, int minor, int patch, int? test, string text)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Test = test;
            Text = text;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        public int? Test { get; }

        public string Text { get; }

        public static bool TryParse(string text, out ReleaseVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            Match match = Pattern.Match(text.Trim());
            if (!match.Success) return false;

            int major;
            int minor;
            int patch;
            int test;
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out major)
                || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out minor)
                || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out patch))
            {
                return false;
            }

            int? testVersion = null;
            if (match.Groups[4].Success)
            {
                if (!int.TryParse(match.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture, out test))
                    return false;
                testVersion = test;
            }

            version = new ReleaseVersion(major, minor, patch, testVersion,
                major + "." + minor + "." + patch + (testVersion.HasValue ? "-test." + testVersion.Value : string.Empty));
            return true;
        }

        public int CompareTo(ReleaseVersion other)
        {
            if (other == null) return 1;
            int result = Major.CompareTo(other.Major);
            if (result != 0) return result;
            result = Minor.CompareTo(other.Minor);
            if (result != 0) return result;
            result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;

            if (!Test.HasValue && !other.Test.HasValue) return 0;
            if (!Test.HasValue) return 1;
            if (!other.Test.HasValue) return -1;
            return Test.Value.CompareTo(other.Test.Value);
        }

        public override string ToString()
        {
            return Text;
        }
    }

    internal sealed class UpdateService
    {
        internal const string DefaultRepository = "coledesigns/dcs-simhub-telemetry-export";
        private const int MaximumArchiveEntries = 512;
        private const long MaximumExpandedBytes = 50L * 1024L * 1024L;
        private const long MaximumSingleFileBytes = 25L * 1024L * 1024L;
        private static readonly Regex ManifestLine = new Regex(
            @"^([0-9A-Fa-f]{64})  (.+)$", RegexOptions.CultureInvariant);
        private static readonly Regex RepositoryPattern = new Regex(
            @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);
        private static readonly Regex ChecksumPattern = new Regex(
            @"^([0-9A-Fa-f]{64})(?:\s{2,}|\s+\*?)(.+)$", RegexOptions.CultureInvariant);

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };

        public UpdateService()
        {
            Repository = DefaultRepository;
        }

        public string Repository { get; private set; }

        public void ConfigureRepository(string repository)
        {
            string normalized;
            if (!TryNormalizeRepository(repository, out normalized))
                throw new InvalidDataException("The package update repository is invalid.");
            Repository = normalized;
        }

        public PackageInfo ReadPackageInfo(string root)
        {
            string path = Path.Combine(root, "package-info.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Package version metadata is missing.", path);
            PackageInfo info = serializer.Deserialize<PackageInfo>(File.ReadAllText(path, Encoding.UTF8));
            ReleaseVersion parsed;
            string normalizedRepository;
            if (info == null || !ReleaseVersion.TryParse(info.version, out parsed)
                || !TryNormalizeRepository(info.repository, out normalizedRepository))
            {
                throw new InvalidDataException("Package version metadata is invalid.");
            }
            info.repository = normalizedRepository;
            return info;
        }

        public InstallState ReadInstallState()
        {
            string path = GetInstallStatePath();
            if (!File.Exists(path)) return null;
            try
            {
                return serializer.Deserialize<InstallState>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        public void WriteInstallState(InstallState state)
        {
            string path = GetInstallStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(state), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public async Task<UpdateInfo> FindUpdateAsync(string currentVersion)
        {
            ReleaseVersion current;
            if (!ReleaseVersion.TryParse(currentVersion, out current))
                throw new InvalidDataException("The current package version is invalid: " + currentVersion);

            string releasesJson;
            try
            {
                using (WebClient client = CreateGitHubClient())
                {
                    releasesJson = await client.DownloadStringTaskAsync(
                        "https://api.github.com/repos/" + Repository + "/releases?per_page=30");
                }
            }
            catch (WebException ex)
            {
                if (IsPrivateRepositoryResponse(ex))
                    throw new InvalidOperationException(
                        "Updates for this beta must be published from the public GitHub repository "
                        + Repository + ".");
                throw new InvalidOperationException("GitHub update check failed. " + DescribeWebException(ex), ex);
            }

            List<GitHubRelease> releases = serializer.Deserialize<List<GitHubRelease>>(releasesJson)
                ?? new List<GitHubRelease>();
            var candidates = new List<Tuple<ReleaseVersion, GitHubRelease, GitHubAsset>>();
            foreach (GitHubRelease release in releases.Where(item => item != null && !item.draft))
            {
                ReleaseVersion parsed;
                if (!ReleaseVersion.TryParse(release.tag_name, out parsed) || parsed.CompareTo(current) <= 0) continue;
                string expectedName = "DcsExtraTelemetry-" + parsed + ".zip";
                GitHubAsset asset = (release.assets ?? new List<GitHubAsset>())
                    .SingleOrDefault(item => string.Equals(item.name, expectedName, StringComparison.Ordinal));
                if (asset == null) continue;
                string digest = await ResolveAssetDigestAsync(release, asset);
                if (string.IsNullOrWhiteSpace(digest)) continue;
                asset.digest = "sha256:" + digest;
                candidates.Add(Tuple.Create(parsed, release, asset));
            }

            Tuple<ReleaseVersion, GitHubRelease, GitHubAsset> newest = candidates
                .OrderByDescending(item => item.Item1)
                .FirstOrDefault();
            if (newest == null) return null;

            return new UpdateInfo
            {
                Version = newest.Item1,
                TagName = newest.Item2.tag_name,
                AssetName = newest.Item3.name,
                Sha256 = newest.Item3.digest.Substring("sha256:".Length).ToUpperInvariant(),
                ReleaseUrl = newest.Item2.html_url,
                DownloadUrl = newest.Item3.browser_download_url
            };
        }

        public async Task<string> DownloadAndVerifyAsync(UpdateInfo update, Action<string> log)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            string updatesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DcsExtraTelemetry", "Packages");
            Directory.CreateDirectory(updatesRoot);

            string digestSuffix = update.Sha256.Substring(0, 12).ToLowerInvariant();
            string finalRoot = Path.Combine(updatesRoot, update.Version + "-" + digestSuffix);
            if (Directory.Exists(finalRoot))
            {
                try
                {
                    VerifyPackageManifest(finalRoot, allowRuntimeFiles: true);
                    PackageInfo existingInfo = ReadPackageInfo(finalRoot);
                    if (!string.Equals(existingInfo.version, update.Version.ToString(), StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(existingInfo.repository, Repository, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The cached update has unexpected version metadata.");
                    log?.Invoke("Using the previously verified update package: " + finalRoot);
                    return finalRoot;
                }
                catch (Exception ex)
                {
                    string rejectedRoot = finalRoot + ".rejected-" + Guid.NewGuid().ToString("N");
                    Directory.Move(finalRoot, rejectedRoot);
                    log?.Invoke("Rejected an invalid cached update and preserved it for diagnosis: " + ex.Message);
                }
            }

            string stagingRoot = Path.Combine(updatesRoot, ".staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingRoot);
            try
            {
                string zipPath = Path.Combine(stagingRoot, update.AssetName);
                if (string.IsNullOrWhiteSpace(update.DownloadUrl))
                    throw new InvalidDataException("The GitHub release does not provide a download URL.");
                log?.Invoke("Downloading " + update.AssetName + " from the configured GitHub release.");
                try
                {
                    using (WebClient client = CreateGitHubClient())
                    {
                        await client.DownloadFileTaskAsync(update.DownloadUrl, zipPath);
                    }
                }
                catch (WebException ex)
                {
                    if (IsPrivateRepositoryResponse(ex))
                        throw new InvalidOperationException("The public beta update asset is not publicly downloadable from "
                            + Repository + ".", ex);
                    throw new InvalidOperationException("GitHub download failed. " + DescribeWebException(ex), ex);
                }
                if (!File.Exists(zipPath)) throw new FileNotFoundException("GitHub did not download the expected update asset.", zipPath);
                string actualDigest = ComputeSha256(zipPath);
                if (!string.Equals(actualDigest, update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded ZIP does not match GitHub's SHA-256 digest.");
                log?.Invoke("PASS: GitHub SHA-256 digest verified.");

                string extractedRoot = Path.Combine(stagingRoot, "extracted");
                ExtractZipSafely(zipPath, extractedRoot);
                string package = FindExtractedPackageRoot(extractedRoot);
                VerifyPackageManifest(package);
                PackageInfo info = ReadPackageInfo(package);
                if (!string.Equals(info.version, update.Version.ToString(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(info.repository, Repository, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded package version does not match the selected release.");
                log?.Invoke("PASS: Every extracted package file matches manifest.sha256.");

                Directory.Move(package, finalRoot);
                return finalRoot;
            }
            finally
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            }
        }

        internal static void ExtractZipSafely(string zipPath, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            string resolvedRoot = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long expandedBytes = 0;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > MaximumArchiveEntries)
                    throw new InvalidDataException("The update ZIP contains too many entries.");

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string target = Path.GetFullPath(Path.Combine(destinationRoot, relative));
                    if (!target.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The update ZIP contains an unsafe path: " + entry.FullName);
                    if (!destinations.Add(target))
                        throw new InvalidDataException("The update ZIP contains a duplicate path: " + entry.FullName);

                    bool isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal)
                        || entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                    if (isDirectory)
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    if (entry.Length < 0 || entry.Length > MaximumSingleFileBytes)
                        throw new InvalidDataException("An update file is too large: " + entry.FullName);
                    expandedBytes += entry.Length;
                    if (expandedBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The expanded update exceeds the safety limit.");

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        internal static void VerifyPackageManifest(string packageRoot, bool allowRuntimeFiles = false)
        {
            string resolvedRoot = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string manifestPath = Path.Combine(packageRoot, "manifest.sha256");
            if (!File.Exists(manifestPath)) throw new InvalidDataException("manifest.sha256 is missing from the update.");

            var signedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(manifestPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Match match = ManifestLine.Match(line);
                if (!match.Success) throw new InvalidDataException("The update manifest contains a malformed line.");

                string relative = match.Groups[2].Value.Replace('/', Path.DirectorySeparatorChar);
                string path = Path.GetFullPath(Path.Combine(packageRoot, relative));
                if (!path.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase) || !signedFiles.Add(path))
                    throw new InvalidDataException("The update manifest contains an unsafe or duplicate path.");
                if (!File.Exists(path)) throw new InvalidDataException("A signed update file is missing: " + relative);
                if (!string.Equals(ComputeSha256(path), match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Update package hash mismatch: " + relative);
            }

            foreach (string file in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(file, manifestPath, StringComparison.OrdinalIgnoreCase)) continue;
                string relative = Path.GetFullPath(file).Substring(resolvedRoot.Length);
                if (allowRuntimeFiles && (string.Equals(relative, "baseline.json", StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith("Backups" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (!signedFiles.Contains(Path.GetFullPath(file)))
                    throw new InvalidDataException("The update contains an unsigned file: " + relative);
            }
        }

        internal static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string FindExtractedPackageRoot(string extractedRoot)
        {
            List<string> candidates = Directory.GetDirectories(extractedRoot, "*", SearchOption.AllDirectories)
                .Concat(new[] { extractedRoot })
                .Where(path => File.Exists(Path.Combine(path, "manifest.sha256"))
                    && File.Exists(Path.Combine(path, "package-info.json"))
                    && File.Exists(Path.Combine(path, "Tools", "DcsExtraTelemetry.Launcher.exe")))
                .ToList();
            if (candidates.Count != 1)
                throw new InvalidDataException("The update ZIP must contain exactly one DCS Extra Telemetry package.");
            return candidates[0];
        }

        private static WebClient CreateGitHubClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "HapticFlight-Bridge-Updater";
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private static bool IsPrivateRepositoryResponse(WebException exception)
        {
            var response = exception.Response as HttpWebResponse;
            return response != null && (response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden);
        }

        private async Task<string> ResolveAssetDigestAsync(GitHubRelease release, GitHubAsset packageAsset)
        {
            string digest = ExtractGithubDigest(packageAsset.digest);
            if (!string.IsNullOrWhiteSpace(digest)) return digest;

            string checksumName = packageAsset.name + ".sha256";
            GitHubAsset checksumAsset = (release.assets ?? new List<GitHubAsset>())
                .SingleOrDefault(item => string.Equals(item.name, checksumName, StringComparison.Ordinal));
            if (checksumAsset == null || string.IsNullOrWhiteSpace(checksumAsset.browser_download_url)) return null;

            try
            {
                using (WebClient client = CreateGitHubClient())
                {
                    string checksum = await client.DownloadStringTaskAsync(checksumAsset.browser_download_url);
                    return ReadChecksumAsset(checksum, packageAsset.name);
                }
            }
            catch (WebException)
            {
                return null;
            }
        }

        internal static string ReadChecksumAsset(string content, string expectedAssetName)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(expectedAssetName)) return null;
            foreach (string line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match match = ChecksumPattern.Match(line.Trim());
                if (match.Success && string.Equals(match.Groups[2].Value.Trim().TrimStart('*'), expectedAssetName,
                    StringComparison.Ordinal))
                {
                    return match.Groups[1].Value.ToUpperInvariant();
                }
            }
            return null;
        }

        private static string ExtractGithubDigest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;
            string value = digest.Substring("sha256:".Length).Trim();
            return Regex.IsMatch(value, "^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant)
                ? value.ToUpperInvariant() : null;
        }

        private static bool TryNormalizeRepository(string repository, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(repository)) return false;
            string candidate = repository.Trim();
            if (!RepositoryPattern.IsMatch(candidate)) return false;
            normalized = candidate;
            return true;
        }

        private static string DescribeWebException(WebException exception)
        {
            var response = exception.Response as HttpWebResponse;
            if (response != null)
            {
                return "GitHub returned " + (int)response.StatusCode + " " + response.StatusDescription + ".";
            }
            return string.IsNullOrWhiteSpace(exception.Message)
                ? "Check the internet connection and try again." : exception.Message;
        }

        private static string GetInstallStatePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DcsExtraTelemetry", "install-state.json");
        }

        private sealed class GitHubRelease
        {
            public string tag_name { get; set; }

            public bool draft { get; set; }

            public string html_url { get; set; }

            public List<GitHubAsset> assets { get; set; }
        }

        private sealed class GitHubAsset
        {
            public string name { get; set; }

            public string digest { get; set; }

            public string browser_download_url { get; set; }
        }

    }
}

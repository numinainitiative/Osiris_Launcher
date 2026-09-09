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
using System.Threading;
using System.Web.Script.Serialization;

namespace Osiris.Updater
{
    internal static class Program
    {
        private const int UpdateStartedExitCode = 10;
        private const string UserAgent = "Osiris-Updater/0.1";

        [STAThread]
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try
            {
                var options = Arguments.Parse(args);
                if (options.Mode == "apply")
                {
                    return ApplyUpdate(options);
                }

                if (options.Mode == "check")
                {
                    return CheckForUpdate(options);
                }

                return 2;
            }
            catch (Exception exception)
            {
                TryLog(args, "Updater failure: " + exception);
                return 1;
            }
        }

        private static int CheckForUpdate(Arguments options)
        {
            var root = ValidateRoot(options.Root);
            var configuration = ReadJson<VersionConfiguration>(Path.Combine(root, "version.json"));
            if (configuration.checkForUpdates == false)
            {
                return 0;
            }

            var currentVersion = OsirisVersion.Parse(configuration.version);
            var useConfiguredApi = string.IsNullOrWhiteSpace(options.ApiUrl);
            if (useConfiguredApi && WasCheckedRecently(root))
            {
                return 0;
            }
            var apiUrl = string.IsNullOrWhiteSpace(options.ApiUrl)
                ? configuration.releaseApiUrl
                : options.ApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                throw new InvalidOperationException("The Osiris release API URL is missing.");
            }

            ReleaseInfo latestRelease;
            try
            {
                var releases = DownloadJson<ReleaseInfo[]>(apiUrl);
                latestRelease = SelectLatestRelease(releases, currentVersion, configuration.channel);
            }
            catch (Exception exception)
            {
                if (useConfiguredApi) WriteCheckTime(root);
                WriteLog(root, "Update check unavailable: " + exception.Message);
                return 0;
            }
            if (useConfiguredApi) WriteCheckTime(root);

            if (latestRelease == null)
            {
                WriteLog(root, "No newer Osiris release is available.");
                return 0;
            }

            var releaseVersion = ParseReleaseVersion(latestRelease.tag_name);
            var expectedManifestName = "Osiris-" + releaseVersion + "-win-x64.json";
            var manifestAsset = latestRelease.assets == null
                ? null
                : latestRelease.assets.FirstOrDefault(asset =>
                    string.Equals(asset.name, expectedManifestName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset == null)
            {
                throw new InvalidOperationException("The release checksum manifest is missing: " + expectedManifestName);
            }

            var manifest = DownloadJson<ReleaseManifest>(manifestAsset.browser_download_url);
            ValidateManifest(manifest, releaseVersion);
            var packageAsset = latestRelease.assets.FirstOrDefault(asset =>
                string.Equals(asset.name, manifest.asset, StringComparison.OrdinalIgnoreCase));
            if (packageAsset == null)
            {
                throw new InvalidOperationException("The release package is missing: " + manifest.asset);
            }

            if (!options.Accept)
            {
                WriteLog(root, "Osiris " + releaseVersion + " is available; waiting for in-app acceptance.");
                return 0;
            }

            var workRoot = Path.Combine(root, "Data", "Runtime", "Updates", releaseVersion.ToString());
            EnsureChildPath(Path.Combine(root, "Data"), workRoot);
            Directory.CreateDirectory(workRoot);
            var packagePath = Path.Combine(workRoot, manifest.asset);
            var stagingPath = Path.Combine(workRoot, "staging");

            DownloadFile(packageAsset.browser_download_url, packagePath);
            ValidateDownloadedPackage(packagePath, manifest);
            ExtractPackage(packagePath, stagingPath);
            ValidateStagedPackage(stagingPath, manifest.version);

            var runnerPath = Path.Combine(workRoot, "Osiris.Updater.Apply.exe");
            File.Copy(Process.GetCurrentProcess().MainModule.FileName, runnerPath, true);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = runnerPath,
                WorkingDirectory = workRoot,
                UseShellExecute = false,
                Arguments = "--apply --root " + Quote(root) +
                    " --staging " + Quote(stagingPath) +
                    " --current " + Quote(currentVersion.ToString()) +
                    " --target " + Quote(releaseVersion.ToString()) +
                    " --parent " + options.ParentProcessId.ToString(CultureInfo.InvariantCulture) +
                    (options.NoRestart ? " --no-restart" : string.Empty)
            });
            if (process == null)
            {
                throw new InvalidOperationException("The update installer could not be started.");
            }

            WriteLog(root, "Prepared Osiris " + releaseVersion + " and started the installer.");
            return UpdateStartedExitCode;
        }

        private static int ApplyUpdate(Arguments options)
        {
            var root = ValidateRoot(options.Root);
            WaitForParent(options.ParentProcessId);
            WaitForLauncher(root);
            var staging = Path.GetFullPath(options.Staging).TrimEnd(Path.DirectorySeparatorChar);
            EnsureChildPath(Path.Combine(root, "Data", "Runtime", "Updates"), staging);
            ValidateStagedPackage(staging, options.TargetVersion);

            var backup = Path.Combine(
                root,
                "Data",
                "Recovery",
                "Updates",
                options.CurrentVersion + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            EnsureChildPath(Path.Combine(root, "Data", "Recovery", "Updates"), backup);
            Directory.CreateDirectory(backup);

            var currentApp = Path.Combine(root, "App");
            var stagedApp = Path.Combine(staging, "App");
            var backupApp = Path.Combine(backup, "App");
            var backupRootFiles = Path.Combine(backup, "Root");
            Directory.CreateDirectory(backupRootFiles);
            var appMoved = false;
            var newlyCreatedRootFiles = new List<string>();

            try
            {
                foreach (var stagedFile in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
                {
                    var currentFile = Path.Combine(root, Path.GetFileName(stagedFile));
                    if (File.Exists(currentFile))
                    {
                        File.Copy(currentFile, Path.Combine(backupRootFiles, Path.GetFileName(currentFile)), true);
                    }
                }

                Directory.Move(currentApp, backupApp);
                appMoved = true;
                Directory.Move(stagedApp, currentApp);

                foreach (var stagedFile in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
                {
                    var destinationFile = Path.Combine(root, Path.GetFileName(stagedFile));
                    if (!File.Exists(destinationFile))
                    {
                        newlyCreatedRootFiles.Add(destinationFile);
                    }
                    File.Copy(stagedFile, destinationFile, true);
                }

                WriteLog(root, "Installed Osiris " + options.TargetVersion + ". Backup: " + backup);
            }
            catch (Exception exception)
            {
                WriteLog(root, "Install failed; starting rollback: " + exception);
                if (Directory.Exists(currentApp))
                {
                    var failedApp = Path.Combine(backup, "Failed-App");
                    if (Directory.Exists(failedApp))
                    {
                        Directory.Delete(failedApp, true);
                    }
                    Directory.Move(currentApp, failedApp);
                }
                if (appMoved && Directory.Exists(backupApp))
                {
                    Directory.Move(backupApp, currentApp);
                }
                foreach (var backupFile in Directory.GetFiles(backupRootFiles))
                {
                    File.Copy(backupFile, Path.Combine(root, Path.GetFileName(backupFile)), true);
                }
                foreach (var newFile in newlyCreatedRootFiles)
                {
                    if (File.Exists(newFile))
                    {
                        File.Delete(newFile);
                    }
                }
                WriteLog(root, "Rollback completed.");
                return 1;
            }

            if (!options.NoRestart)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(root, "Osiris.exe"),
                    WorkingDirectory = root,
                    Arguments = "--skipupdatecheck",
                    UseShellExecute = false
                });
            }

            return 0;
        }

        private static void WaitForLauncher(string root)
        {
            var expectedPath = Path.GetFullPath(Path.Combine(root, "Osiris.exe"));
            var currentProcessId = Process.GetCurrentProcess().Id;
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                Process matchingProcess = null;
                foreach (var process in Process.GetProcessesByName("Osiris"))
                {
                    try
                    {
                        if (process.Id != currentProcessId &&
                            string.Equals(
                                Path.GetFullPath(process.MainModule.FileName),
                                expectedPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            matchingProcess = process;
                            break;
                        }
                    }
                    catch
                    {
                    }

                    process.Dispose();
                }

                if (matchingProcess == null)
                {
                    return;
                }

                using (matchingProcess)
                {
                    if (matchingProcess.WaitForExit(1000))
                    {
                        return;
                    }
                }
            }

            throw new TimeoutException("The Osiris launcher did not close in time for the update.");
        }

        private static ReleaseInfo SelectLatestRelease(
            IEnumerable<ReleaseInfo> releases,
            OsirisVersion currentVersion,
            string channel)
        {
            var allowPrereleases = !string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase);
            return releases
                .Where(release => release != null && !release.draft && (allowPrereleases || !release.prerelease))
                .Select(release => new
                {
                    Release = release,
                    Version = TryParseReleaseVersion(release.tag_name)
                })
                .Where(item => item.Version != null &&
                    IsStageAllowed(item.Version.Stage, channel) &&
                    item.Version.CompareTo(currentVersion) > 0)
                .OrderByDescending(item => item.Version)
                .Select(item => item.Release)
                .FirstOrDefault();
        }

        private static bool IsStageAllowed(string stage, string channel)
        {
            if (string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(stage, "Stable", StringComparison.OrdinalIgnoreCase);
            }
            if (string.Equals(channel, "beta", StringComparison.OrdinalIgnoreCase))
            {
                return !string.Equals(stage, "Alpha", StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        private static OsirisVersion ParseReleaseVersion(string tag)
        {
            var value = (tag ?? string.Empty).Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }
            return OsirisVersion.Parse(value);
        }

        private static OsirisVersion TryParseReleaseVersion(string tag)
        {
            OsirisVersion version;
            var value = (tag ?? string.Empty).Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }
            return OsirisVersion.TryParse(value, out version)
                ? version
                : null;
        }

        private static void ValidateManifest(ReleaseManifest manifest, OsirisVersion releaseVersion)
        {
            if (manifest == null || manifest.schemaVersion != 1 ||
                !string.Equals(manifest.version, releaseVersion.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(manifest.asset) ||
                !manifest.asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                manifest.size <= 0 ||
                string.IsNullOrWhiteSpace(manifest.sha256) || manifest.sha256.Length != 64)
            {
                throw new InvalidOperationException("The Osiris release manifest is invalid.");
            }
        }

        private static void ValidateDownloadedPackage(string path, ReleaseManifest manifest)
        {
            var info = new FileInfo(path);
            if (info.Length != manifest.size)
            {
                throw new InvalidOperationException("The downloaded package size does not match the release manifest.");
            }
            var hash = ComputeSha256(path);
            if (!string.Equals(hash, manifest.sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The downloaded package checksum is invalid.");
            }
        }

        private static void ExtractPackage(string packagePath, string stagingPath)
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, true);
            }
            Directory.CreateDirectory(stagingPath);
            var stagingPrefix = Path.GetFullPath(stagingPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                long expandedSize = 0;
                foreach (var entry in archive.Entries)
                {
                    expandedSize += entry.Length;
                    if (expandedSize > 4L * 1024 * 1024 * 1024)
                    {
                        throw new InvalidOperationException("The update archive expands beyond the safety limit.");
                    }
                    var destination = Path.GetFullPath(Path.Combine(stagingPath, entry.FullName));
                    if (!destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("The update archive contains an unsafe path.");
                    }
                }
            }
            ZipFile.ExtractToDirectory(packagePath, stagingPath);
        }

        private static void ValidateStagedPackage(string stagingPath, string expectedVersion)
        {
            if (!Directory.Exists(Path.Combine(stagingPath, "App")) ||
                !File.Exists(Path.Combine(stagingPath, "Osiris.exe")) ||
                !File.Exists(Path.Combine(stagingPath, "version.json")))
            {
                throw new InvalidOperationException("The staged package is not a complete Osiris installation.");
            }
            var version = ReadJson<VersionConfiguration>(Path.Combine(stagingPath, "version.json"));
            if (!string.Equals(version.version, expectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The staged package version does not match the release manifest.");
            }
            var stagedData = Path.Combine(stagingPath, "Data");
            if (Directory.Exists(stagedData) && Directory.EnumerateFileSystemEntries(stagedData).Any())
            {
                throw new InvalidOperationException("The update package contains writable Data.");
            }
        }

        private static string ValidateRoot(string root)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!File.Exists(Path.Combine(fullRoot, "Osiris.exe")) ||
                !Directory.Exists(Path.Combine(fullRoot, "App")) ||
                !Directory.Exists(Path.Combine(fullRoot, "Data")))
            {
                throw new InvalidOperationException("The Osiris installation root is invalid: " + fullRoot);
            }
            return fullRoot;
        }

        private static void EnsureChildPath(string parent, string child)
        {
            var parentPath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var childPath = Path.GetFullPath(child);
            if (!childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unsafe updater path: " + childPath);
            }
        }

        private static void WaitForParent(int processId)
        {
            if (processId <= 0)
            {
                return;
            }
            try
            {
                Process.GetProcessById(processId).WaitForExit(30000);
            }
            catch (ArgumentException)
            {
            }
        }

        private static T DownloadJson<T>(string url)
        {
            using (var client = CreateWebClient())
            {
                return Deserialize<T>(client.DownloadString(url));
            }
        }

        private static void DownloadFile(string url, string path)
        {
            using (var client = CreateWebClient())
            {
                client.DownloadFile(url, path);
            }
        }

        private static WebClient CreateWebClient()
        {
            var client = new TimeoutWebClient();
            client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2026-03-10";
            return client;
        }

        private static bool WasCheckedRecently(string root)
        {
            try
            {
                var path = GetCheckTimePath(root);
                DateTime checkedAt;
                return File.Exists(path) &&
                    DateTime.TryParse(
                        File.ReadAllText(path),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out checkedAt) &&
                    DateTime.UtcNow - checkedAt.ToUniversalTime() < TimeSpan.FromMinutes(30);
            }
            catch
            {
                return false;
            }
        }

        private static void WriteCheckTime(string root)
        {
            try
            {
                var path = GetCheckTimePath(root);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            }
            catch
            {
            }
        }

        private static string GetCheckTimePath(string root)
        {
            return Path.Combine(root, "Data", "Runtime", "Updates", "last-check.utc");
        }

        private static T ReadJson<T>(string path)
        {
            return Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
        }

        private static T Deserialize<T>(string json)
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<T>(json);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void WriteLog(string root, string message)
        {
            try
            {
                var logDirectory = Path.Combine(root, "Data", "logs");
                Directory.CreateDirectory(logDirectory);
                File.AppendAllText(
                    Path.Combine(logDirectory, "osiris-updater.log"),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static void TryLog(string[] args, string message)
        {
            try
            {
                var rootIndex = Array.FindIndex(args, value => string.Equals(value, "--root", StringComparison.OrdinalIgnoreCase));
                if (rootIndex >= 0 && rootIndex + 1 < args.Length)
                {
                    WriteLog(args[rootIndex + 1], message);
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class Arguments
    {
        public string Mode;
        public string Root;
        public string Staging;
        public string ApiUrl;
        public string CurrentVersion;
        public string TargetVersion;
        public int ParentProcessId;
        public bool Accept;
        public bool NoRestart;

        public static Arguments Parse(string[] args)
        {
            var result = new Arguments();
            for (var index = 0; index < args.Length; index++)
            {
                var value = args[index];
                if (value == "--check" || value == "--apply") result.Mode = value.Substring(2);
                else if (value == "--accept") result.Accept = true;
                else if (value == "--no-restart") result.NoRestart = true;
                else if (value == "--root") result.Root = Next(args, ref index, value);
                else if (value == "--staging") result.Staging = Next(args, ref index, value);
                else if (value == "--api") result.ApiUrl = Next(args, ref index, value);
                else if (value == "--current") result.CurrentVersion = Next(args, ref index, value);
                else if (value == "--target") result.TargetVersion = Next(args, ref index, value);
                else if (value == "--parent") result.ParentProcessId = int.Parse(Next(args, ref index, value), CultureInfo.InvariantCulture);
            }
            if (string.IsNullOrWhiteSpace(result.Mode) || string.IsNullOrWhiteSpace(result.Root))
            {
                throw new ArgumentException("Updater mode and root are required.");
            }
            return result;
        }

        private static string Next(string[] args, ref int index, string option)
        {
            if (++index >= args.Length) throw new ArgumentException("Missing value for " + option);
            return args[index];
        }
    }

    internal sealed class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            request.Timeout = 5000;
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
            {
                httpRequest.ReadWriteTimeout = 5000;
            }
            return request;
        }
    }

    internal sealed class OsirisVersion : IComparable<OsirisVersion>
    {
        public string Stage;
        public int Major;
        public int Minor;
        public int Patch;

        public static OsirisVersion Parse(string value)
        {
            OsirisVersion version;
            if (!TryParse(value, out version)) throw new FormatException("Invalid Osiris version: " + value);
            return version;
        }

        public static bool TryParse(string value, out OsirisVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var match = Regex.Match(
                value,
                @"^(Alpha|Beta|Stable)_(\d+)\.(\d+)\.(\d+)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success) return false;
            int major, minor, patch;
            if (!int.TryParse(match.Groups[2].Value, out major) ||
                !int.TryParse(match.Groups[3].Value, out minor) ||
                !int.TryParse(match.Groups[4].Value, out patch)) return false;
            version = new OsirisVersion
            {
                Stage = CanonicalStage(match.Groups[1].Value),
                Major = major,
                Minor = minor,
                Patch = patch
            };
            return true;
        }

        public int CompareTo(OsirisVersion other)
        {
            if (other == null) return 1;

            // Beta_2026.x.x was Osiris's short-lived calendar-style scheme.
            // Any conventional version is newer than a legacy calendar version,
            // allowing installations bootstrapped with this updater to migrate to
            // Beta_1.x.x and then resume normal semantic version comparison.
            var legacy = IsLegacyCalendarVersion();
            var otherLegacy = other.IsLegacyCalendarVersion();
            if (legacy != otherLegacy) return legacy ? -1 : 1;

            var result = Major.CompareTo(other.Major);
            if (result == 0) result = Minor.CompareTo(other.Minor);
            if (result == 0) result = Patch.CompareTo(other.Patch);
            if (result == 0) result = StageRank(Stage).CompareTo(StageRank(other.Stage));
            return result;
        }

        private bool IsLegacyCalendarVersion()
        {
            return Major >= 2000 && Major <= 2999;
        }

        private static string CanonicalStage(string stage)
        {
            if (string.Equals(stage, "alpha", StringComparison.OrdinalIgnoreCase)) return "Alpha";
            if (string.Equals(stage, "beta", StringComparison.OrdinalIgnoreCase)) return "Beta";
            return "Stable";
        }

        private static int StageRank(string stage)
        {
            if (string.Equals(stage, "Alpha", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(stage, "Beta", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        public override string ToString()
        {
            return Stage + "_" + Major + "." + Minor + "." + Patch;
        }
    }

    internal sealed class VersionConfiguration
    {
        public string version { get; set; }
        public string channel { get; set; }
        public string releaseApiUrl { get; set; }
        public bool? checkForUpdates { get; set; }
    }

    internal sealed class ReleaseInfo
    {
        public string tag_name { get; set; }
        public string body { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public ReleaseAsset[] assets { get; set; }
    }

    internal sealed class ReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal sealed class ReleaseManifest
    {
        public int schemaVersion { get; set; }
        public string version { get; set; }
        public string channel { get; set; }
        public string asset { get; set; }
        public long size { get; set; }
        public string sha256 { get; set; }
    }
}

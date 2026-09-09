using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

[assembly: AssemblyTitle("Osiris")]
[assembly: AssemblyDescription("Numina Initiative Osiris launcher")]
[assembly: AssemblyCompany("Numina Initiative")]
[assembly: AssemblyProduct("Osiris")]
[assembly: AssemblyVersion("0.0.36.0")]
[assembly: AssemblyFileVersion("0.0.36.0")]

internal static class OsirisLauncher
{
    private const uint FrNotEnum = 0x20;
    private const string ExtensionDataRemovalQueueFileName = "osiris-extension-data-removals.txt";
    private const string ExtensionInstallQueueDirectoryName = "osiris-extension-install-queue";
    private const long MaximumExtensionPackageBytes = 256L * 1024L * 1024L;
    private const int MaximumExtensionPackageFiles = 2048;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);

    private static List<string> RegisterBundledFonts(string appDirectory)
    {
        var registeredFonts = new List<string>();
        var fontDirectory = Path.Combine(
            appDirectory,
            "Themes",
            "Desktop",
            "Default",
            "Fonts");
        foreach (var fileName in new[]
        {
            "Rajdhani-Light.ttf",
            "Rajdhani-Regular.ttf",
            "Rajdhani-Medium.ttf",
            "Rajdhani-SemiBold.ttf",
            "Rajdhani-Bold.ttf"
        })
        {
            var fontPath = Path.Combine(fontDirectory, fileName);
            if (!File.Exists(fontPath))
            {
                continue;
            }

            try
            {
                // Register Osiris's own faces for this Windows session without
                // adding a permanent user font or exposing duplicate entries in
                // font pickers. The wrapper remains alive for the desktop
                // process's lifetime and removes each registration on exit.
                if (AddFontResourceEx(fontPath, FrNotEnum, IntPtr.Zero) > 0)
                {
                    registeredFonts.Add(fontPath);
                }
            }
            catch
            {
                // A font problem must not prevent the desktop engine from
                // opening; release packaging separately requires every face.
            }
        }

        return registeredFonts;
    }

    private static void UnregisterBundledFonts(IEnumerable<string> fontPaths)
    {
        foreach (var fontPath in fontPaths.Reverse())
        {
            try
            {
                RemoveFontResourceEx(fontPath, FrNotEnum, IntPtr.Zero);
            }
            catch
            {
            }
        }
    }

    private static Process FindDesktopEngine(string appDirectory)
    {
        var expectedPath = Path.GetFullPath(Path.Combine(appDirectory, "Osiris.DesktopEngine.exe"));
        foreach (var process in Process.GetProcessesByName("Osiris.DesktopEngine"))
        {
            try
            {
                var actualPath = Path.GetFullPath(process.MainModule.FileName);
                if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        return null;
    }

    private static void WaitForDesktopEngine(string appDirectory, Process desktopApp)
    {
        desktopApp.WaitForExit();

        // DesktopApp is Playnite's short-lived bootstrap process. Keep this
        // wrapper (and therefore its temporary font registrations) alive for
        // the real desktop engine that it creates.
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var engine = FindDesktopEngine(appDirectory);
            if (engine != null)
            {
                using (engine)
                {
                    engine.WaitForExit();
                }

                return;
            }

            Thread.Sleep(100);
        }
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static IEnumerable<string> RemoveUserDataArguments(string[] args)
    {
        for (var index = 0; index < (args == null ? 0 : args.Length); index++)
        {
            if (string.Equals(args[index], "--waitforpid", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (args[index].StartsWith("--waitforpid=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(args[index], "--userdatadir", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (args[index].StartsWith("--userdatadir=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(args[index], "--skipupdatecheck", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Quote(args[index]);
        }
    }

    private static bool IsInheritedPlayniteInstallRequest(string[] args)
    {
        for (var index = 0; index < (args == null ? 0 : args.Length); index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--uridata", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length &&
                    IsPlayniteAddonUri(args[index + 1]))
                {
                    return true;
                }

                index++;
                continue;
            }

            const string uriPrefix = "--uridata=";
            if (argument.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase) &&
                IsPlayniteAddonUri(argument.Substring(uriPrefix.Length)))
            {
                return true;
            }

            if (string.Equals(argument, "--installext", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length &&
                    IsPlayniteExtensionFile(args[index + 1]))
                {
                    return true;
                }

                index++;
                continue;
            }

            const string installExtensionPrefix = "--installext=";
            if (argument.StartsWith(installExtensionPrefix, StringComparison.OrdinalIgnoreCase) &&
                IsPlayniteExtensionFile(argument.Substring(installExtensionPrefix.Length)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlayniteAddonUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.StartsWith("playnite://playnite/installaddon/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlayniteExtensionFile(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var extension = Path.GetExtension(value.Trim('"'));
        return string.Equals(extension, ".pext", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".pthm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasArgument(string[] args, string argumentName)
    {
        for (var index = 0; index < (args == null ? 0 : args.Length); index++)
        {
            if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WaitForRequestedProcess(string[] args)
    {
        var processId = 0;
        for (var index = 0; index < (args == null ? 0 : args.Length); index++)
        {
            if (string.Equals(args[index], "--waitforpid", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length)
                {
                    int.TryParse(args[index + 1], out processId);
                }
                break;
            }

            const string prefix = "--waitforpid=";
            if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[index].Substring(prefix.Length), out processId);
                break;
            }
        }

        if (processId <= 0 || processId == Process.GetCurrentProcess().Id)
        {
            return true;
        }

        try
        {
            using (var process = Process.GetProcessById(processId))
            {
                return process.WaitForExit(60000);
            }
        }
        catch (ArgumentException)
        {
            // The requested process already exited before the launcher opened it.
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MoveFileIfNeeded(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void RemoveDirectoryIfEmpty(string path)
    {
        if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
        {
            Directory.Delete(path, false);
        }
    }

    private static void MoveExtensionDirectory(
        string extensionsDirectory,
        string sourceRelativePath,
        string categoryName)
    {
        var sourceDirectory = Path.Combine(extensionsDirectory, sourceRelativePath);
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var destinationDirectory = Path.Combine(
            extensionsDirectory,
            categoryName,
            Path.GetFileName(sourceDirectory));
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.Move(sourceDirectory, destinationDirectory);
        }
    }

    private static void MoveLegacyGroup(
        string extensionsDirectory,
        string legacyGroupName,
        string categoryName)
    {
        var groupDirectory = Path.Combine(extensionsDirectory, legacyGroupName);
        if (!Directory.Exists(groupDirectory))
        {
            return;
        }

        foreach (var sourceDirectory in Directory.GetDirectories(groupDirectory))
        {
            MoveExtensionDirectory(
                extensionsDirectory,
                Path.Combine(legacyGroupName, Path.GetFileName(sourceDirectory)),
                categoryName);
        }

        RemoveDirectoryIfEmpty(groupDirectory);
    }

    private static void OrganizeExtensions(string dataDir)
    {
        var extensionsDirectory = Path.Combine(dataDir, "Extensions");
        foreach (var categoryName in new[]
        {
            "Libraries",
            "Metadata",
            "Extras",
            "Utilities"
        })
        {
            Directory.CreateDirectory(Path.Combine(extensionsDirectory, categoryName));
        }

        foreach (var folderName in new[]
        {
            "SteamLibrary_Builtin",
            "XboxLibrary_Builtin"
        })
        {
            MoveExtensionDirectory(extensionsDirectory, folderName, "Libraries");
        }

        foreach (var folderName in new[]
        {
            "IGDBMetadata_Builtin",
            "IgnMetadata_6024e3a9-de7e-4848-9101-7a2f818e7e47",
            "SteamGridDB_Playnite_Metadata",
            "Universal_Steam_Metadata"
        })
        {
            MoveExtensionDirectory(extensionsDirectory, folderName, "Metadata");
        }

        foreach (var folderName in new[]
        {
            "OsirisGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b",
            "playnite-howlongtobeat-plugin"
        })
        {
            MoveExtensionDirectory(extensionsDirectory, folderName, "Extras");
        }

        MoveLegacyGroup(extensionsDirectory, "Metadata Sources", "Metadata");
        MoveLegacyGroup(extensionsDirectory, "Enhancements", "Extras");

        var legacyPlugins = Path.Combine(extensionsDirectory, "Plugins");
        if (Directory.Exists(legacyPlugins))
        {
            foreach (var sourceDirectory in Directory.GetDirectories(legacyPlugins))
            {
                var folderName = Path.GetFileName(sourceDirectory);
                var categoryName =
                    folderName.IndexOf("Gallery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    folderName.IndexOf("HowLongToBeat", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Extras"
                        : "Utilities";
                MoveExtensionDirectory(
                    extensionsDirectory,
                    Path.Combine("Plugins", folderName),
                    categoryName);
            }

            RemoveDirectoryIfEmpty(legacyPlugins);
        }
    }

    private static void PrepareDataLayout(string dataDir)
    {
        var coreSettings = Path.Combine(dataDir, "Settings", "Core");
        var osirisSettings = Path.Combine(dataDir, "Settings", "Osiris");
        var recovery = Path.Combine(dataDir, "Recovery");
        var runtime = Path.Combine(dataDir, "Runtime");

        var extensionCategories = new[]
        {
            "Libraries",
            "Metadata",
            "Extras",
            "Utilities"
        };

        foreach (var directory in new[]
        {
            coreSettings,
            osirisSettings,
            recovery,
            runtime,
            Path.Combine(dataDir, "Backups"),
            Path.Combine(dataDir, "Extensions"),
            Path.Combine(dataDir, "ExtensionsData"),
            Path.Combine(dataDir, "logs")
        })
        {
            Directory.CreateDirectory(directory);
        }

        foreach (var category in extensionCategories)
        {
            Directory.CreateDirectory(Path.Combine(dataDir, "Extensions", category));
            Directory.CreateDirectory(Path.Combine(dataDir, "ExtensionsData", category));
        }

        foreach (var fileName in new[]
        {
            "config.json",
            "fullscreenConfig.json",
            "windowPositions.json",
            "licenseagreements.json"
        })
        {
            MoveFileIfNeeded(
                Path.Combine(dataDir, fileName),
                Path.Combine(coreSettings, fileName));
        }

        foreach (var fileName in new[]
        {
            "libraryState.ini",
            "osiris-font-size.json",
            "osiris-game-details-settings.json",
            "osiris-grid-view-settings.json",
            "osiris-ui-settings.json",
            "osiris-ui-theme.json"
        })
        {
            MoveFileIfNeeded(
                Path.Combine(dataDir, fileName),
                Path.Combine(osirisSettings, fileName));
        }

        MoveFileIfNeeded(
            Path.Combine(dataDir, "config", "osiris-primary-colour.txt"),
            Path.Combine(osirisSettings, "osiris-primary-colour.txt"));

        foreach (var fileName in new[]
        {
            "config.json",
            "fullscreenConfig.json",
            "windowPositions.json"
        })
        {
            MoveFileIfNeeded(
                Path.Combine(dataDir, "Backup", fileName),
                Path.Combine(recovery, fileName));
        }

        foreach (var fileName in new[]
        {
            "backup.json",
            "restoreBackup.json",
            "safestart.flag",
            "extinstalls.json"
        })
        {
            MoveFileIfNeeded(
                Path.Combine(dataDir, fileName),
                Path.Combine(runtime, fileName));
        }

        OrganizeExtensions(dataDir);
        RemoveDirectoryIfEmpty(Path.Combine(dataDir, "Backup"));
        RemoveDirectoryIfEmpty(Path.Combine(dataDir, "config"));
    }

    private static bool IsExtensionCategory(string value)
    {
        return string.Equals(value, "Libraries", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Metadata", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Extras", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Utilities", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveQueuedExtensionDataDirectory(
        string extensionsDataRoot,
        string queuedRelativePath,
        out string targetDirectory)
    {
        targetDirectory = null;
        if (string.IsNullOrWhiteSpace(queuedRelativePath) || Path.IsPathRooted(queuedRelativePath))
        {
            return false;
        }

        var segments = queuedRelativePath.Trim().Split(new[]
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !IsExtensionCategory(segments[0]) ||
            segments.Any(segment => segment == "." || segment == ".."))
        {
            return false;
        }

        var identifierSeparator = segments[1].LastIndexOf('_');
        Guid extensionId;
        if (identifierSeparator < 1 ||
            !Guid.TryParse(segments[1].Substring(identifierSeparator + 1), out extensionId))
        {
            return false;
        }

        try
        {
            var root = Path.GetFullPath(extensionsDataRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, segments[0], segments[1])).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            targetDirectory = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ProcessExtensionDataRemovalQueue(string dataDir)
    {
        var queuePath = Path.Combine(dataDir, "Runtime", ExtensionDataRemovalQueueFileName);
        if (!File.Exists(queuePath))
        {
            return;
        }

        try
        {
            var extensionsDataRoot = Path.Combine(dataDir, "ExtensionsData");
            var failed = new List<string>();
            foreach (var queuedPath in File.ReadAllLines(queuePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string targetDirectory;
                if (!TryResolveQueuedExtensionDataDirectory(
                        extensionsDataRoot,
                        queuedPath,
                        out targetDirectory))
                {
                    continue;
                }

                try
                {
                    var categoryDirectory = Path.GetDirectoryName(targetDirectory);
                    if (string.IsNullOrEmpty(categoryDirectory) ||
                        !Directory.Exists(categoryDirectory) ||
                        (File.GetAttributes(categoryDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        failed.Add(queuedPath.Trim());
                        continue;
                    }

                    if (Directory.Exists(targetDirectory))
                    {
                        var attributes = File.GetAttributes(targetDirectory);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            failed.Add(queuedPath.Trim());
                            continue;
                        }

                        Directory.Delete(targetDirectory, true);
                    }
                }
                catch
                {
                    failed.Add(queuedPath.Trim());
                }
            }

            if (failed.Count == 0)
            {
                File.Delete(queuePath);
            }
            else
            {
                File.WriteAllLines(queuePath, failed.ToArray());
            }
        }
        catch
        {
            // Extension maintenance must never prevent Osiris from launching.
        }
    }

    private static void ProcessExtensionInstallQueue(string dataDir)
    {
        var runtimeRoot = Path.Combine(dataDir, "Runtime");
        var queueRoot = Path.Combine(runtimeRoot, ExtensionInstallQueueDirectoryName);
        if (!Directory.Exists(queueRoot))
        {
            return;
        }

        try
        {
            if ((File.GetAttributes(runtimeRoot) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(queueRoot) & FileAttributes.ReparsePoint) != 0)
            {
                LogExtensionInstall(dataDir, "Refused an extension install queue beneath a reparse point.");
                return;
            }

            foreach (var jobDirectory in Directory.GetDirectories(queueRoot))
            {
                var jobName = Path.GetFileName(jobDirectory);
                Guid jobId;
                if (!Guid.TryParseExact(jobName, "N", out jobId))
                {
                    continue;
                }

                try
                {
                    InstallQueuedExtension(dataDir, jobDirectory, jobId);
                    Directory.Delete(jobDirectory, true);
                }
                catch (Exception exception)
                {
                    LogExtensionInstall(
                        dataDir,
                        "Failed queued extension job " + jobName + ": " + exception);
                    PreserveFailedExtensionJob(queueRoot, jobDirectory, jobName, exception);
                }
            }
        }
        catch (Exception exception)
        {
            LogExtensionInstall(dataDir, "Extension install queue processing failed: " + exception);
        }
    }

    private static void InstallQueuedExtension(string dataDir, string jobDirectory, Guid jobId)
    {
        if ((File.GetAttributes(jobDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The extension install job is a reparse point.");
        }

        var metadataPath = Path.Combine(jobDirectory, "install.ini");
        var packagePath = Path.Combine(jobDirectory, "package.pext");
        var metadata = ReadInstallMetadata(metadataPath);
        string schemaVersion;
        string extensionId;
        string version;
        string relativeFolder;
        string expectedHash;
        string expectedSizeText;
        if (!metadata.TryGetValue("schemaVersion", out schemaVersion) || schemaVersion != "1" ||
            !metadata.TryGetValue("id", out extensionId) ||
            !metadata.TryGetValue("version", out version) ||
            !metadata.TryGetValue("relativeFolder", out relativeFolder) ||
            !metadata.TryGetValue("sha256", out expectedHash) ||
            !metadata.TryGetValue("size", out expectedSizeText))
        {
            throw new InvalidDataException("The extension install metadata is incomplete.");
        }

        string category;
        string folderName;
        if (!TryValidateExtensionIdentity(relativeFolder, extensionId, out category, out folderName))
        {
            throw new InvalidDataException("The extension install identity or target folder is invalid.");
        }

        Version parsedVersion;
        if (!Version.TryParse(version, out parsedVersion))
        {
            throw new InvalidDataException("The extension install version is invalid.");
        }

        long expectedSize;
        if (!long.TryParse(expectedSizeText, out expectedSize) ||
            expectedSize <= 0 || expectedSize > MaximumExtensionPackageBytes ||
            !Regex.IsMatch(expectedHash ?? string.Empty, "^[0-9a-fA-F]{64}$") ||
            !File.Exists(packagePath))
        {
            throw new InvalidDataException("The extension package size or checksum is invalid.");
        }

        var packageInfo = new FileInfo(packagePath);
        if (packageInfo.Length != expectedSize)
        {
            throw new InvalidDataException("The queued extension package size changed.");
        }

        var actualHash = ComputeSha256(packagePath);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The queued extension package checksum changed.");
        }

        var stagingRoot = Path.Combine(
            dataDir,
            "Runtime",
            "osiris-extension-install-staging");
        Directory.CreateDirectory(stagingRoot);
        EnsurePlainDirectory(stagingRoot);
        var stageDirectory = ResolveChildPath(stagingRoot, jobId.ToString("N"));
        if (Directory.Exists(stageDirectory))
        {
            EnsurePlainDirectory(stageDirectory);
            Directory.Delete(stageDirectory, true);
        }
        Directory.CreateDirectory(stageDirectory);

        try
        {
            ExtractAndValidateExtensionPackage(
                packagePath,
                stageDirectory,
                extensionId,
                version,
                category);

            var extensionsRoot = Path.Combine(dataDir, "Extensions");
            Directory.CreateDirectory(extensionsRoot);
            EnsurePlainDirectory(extensionsRoot);
            var categoryRoot = ResolveChildPath(extensionsRoot, category);
            Directory.CreateDirectory(categoryRoot);
            EnsurePlainDirectory(categoryRoot);
            var targetDirectory = ResolveChildPath(categoryRoot, folderName);
            var backupDirectory = default(string);

            if (Directory.Exists(targetDirectory))
            {
                EnsurePlainDirectory(targetDirectory);
                var installedVersion = ReadManifestValue(
                    Path.Combine(targetDirectory, "extension.yaml"),
                    "Version") ?? "unknown";
                Version parsedInstalledVersion;
                if (Version.TryParse(installedVersion, out parsedInstalledVersion) &&
                    parsedInstalledVersion >= parsedVersion)
                {
                    LogExtensionInstall(
                        dataDir,
                        "Skipped " + extensionId + " " + version +
                        "; installed version " + installedVersion + " is current or newer.");
                    return;
                }
                var backupRoot = Path.Combine(
                    dataDir,
                    "Recovery",
                    "ExtensionUpdates",
                    folderName);
                var recoveryRoot = Path.Combine(dataDir, "Recovery");
                var updateRecoveryRoot = Path.Combine(recoveryRoot, "ExtensionUpdates");
                Directory.CreateDirectory(recoveryRoot);
                EnsurePlainDirectory(recoveryRoot);
                Directory.CreateDirectory(updateRecoveryRoot);
                EnsurePlainDirectory(updateRecoveryRoot);
                Directory.CreateDirectory(backupRoot);
                EnsurePlainDirectory(backupRoot);
                var backupName = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
                    "-" + SanitizePathSegment(installedVersion);
                backupDirectory = ResolveChildPath(backupRoot, backupName);
                Directory.Move(targetDirectory, backupDirectory);
            }

            try
            {
                Directory.Move(stageDirectory, targetDirectory);
                ValidateInstalledExtension(targetDirectory, extensionId, version, category);
            }
            catch
            {
                if (Directory.Exists(targetDirectory))
                {
                    EnsurePlainDirectory(targetDirectory);
                    Directory.Delete(targetDirectory, true);
                }
                if (!string.IsNullOrEmpty(backupDirectory) && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, targetDirectory);
                }
                throw;
            }

            LogExtensionInstall(
                dataDir,
                "Installed " + extensionId + " " + version +
                (string.IsNullOrEmpty(backupDirectory) ? "." : "; previous version preserved at " + backupDirectory + "."));
        }
        finally
        {
            if (Directory.Exists(stageDirectory))
            {
                EnsurePlainDirectory(stageDirectory);
                Directory.Delete(stageDirectory, true);
            }
        }
    }

    private static Dictionary<string, string> ReadInstallMetadata(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024)
        {
            throw new InvalidDataException("The extension install metadata is missing or too large.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }
            var key = line.Substring(0, separator).Trim();
            var value = line.Substring(separator + 1).Trim();
            if (!values.ContainsKey(key))
            {
                values.Add(key, value);
            }
        }
        return values;
    }

    private static bool TryValidateExtensionIdentity(
        string relativeFolder,
        string extensionId,
        out string category,
        out string folderName)
    {
        category = null;
        folderName = null;
        if (string.IsNullOrWhiteSpace(relativeFolder) ||
            string.IsNullOrWhiteSpace(extensionId) ||
            Path.IsPathRooted(relativeFolder))
        {
            return false;
        }

        var segments = relativeFolder.Split(new[]
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || !IsExtensionCategory(segments[0]) ||
            segments.Any(segment => segment == "." || segment == "..") ||
            !string.Equals(segments[1], extensionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = extensionId.LastIndexOf('_');
        Guid id;
        if (separator < 1 || !Guid.TryParse(extensionId.Substring(separator + 1), out id))
        {
            return false;
        }

        category = segments[0];
        folderName = segments[1];
        return true;
    }

    private static void ExtractAndValidateExtensionPackage(
        string packagePath,
        string stageDirectory,
        string expectedId,
        string expectedVersion,
        string category)
    {
        var stageRoot = Path.GetFullPath(stageDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long totalLength = 0;
        var fileCount = 0;
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = ZipFile.OpenRead(packagePath))
        {
            foreach (var entry in archive.Entries)
            {
                var relative = (entry.FullName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
                    relative.IndexOf(':') >= 0)
                {
                    throw new InvalidDataException("The extension package contains an invalid path.");
                }

                var segments = relative.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Any(segment => segment == "." || segment == "..") ||
                    segments.Any(segment => string.Equals(segment, "Data", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(segment, "ExtensionsData", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException("The extension package attempts to escape its installation folder.");
                }

                var destination = Path.GetFullPath(Path.Combine(stageRoot, relative));
                if (!destination.StartsWith(stageRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The extension package contains a path traversal.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                fileCount++;
                totalLength += entry.Length;
                if (fileCount > MaximumExtensionPackageFiles ||
                    entry.Length > MaximumExtensionPackageBytes ||
                    totalLength > MaximumExtensionPackageBytes ||
                    !destinations.Add(destination))
                {
                    throw new InvalidDataException("The extension package exceeds its safe extraction limits.");
                }

                var extension = Path.GetExtension(destination);
                if (string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".user", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".suo", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The extension package contains development or private files.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (var input = entry.Open())
                using (var output = File.Open(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
            }
        }

        ValidateInstalledExtension(stageDirectory, expectedId, expectedVersion, category);
    }

    private static void ValidateInstalledExtension(
        string directory,
        string expectedId,
        string expectedVersion,
        string category)
    {
        var manifestPath = Path.Combine(directory, "extension.yaml");
        var id = ReadManifestValue(manifestPath, "Id");
        var version = ReadManifestValue(manifestPath, "Version");
        var module = ReadManifestValue(manifestPath, "Module");
        var type = ReadManifestValue(manifestPath, "Type");
        if (!string.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(version, expectedVersion, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(module) ||
            string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidDataException("The extension manifest does not match the catalog.");
        }

        if ((string.Equals(type, "GameLibrary", StringComparison.OrdinalIgnoreCase) && category != "Libraries") ||
            (string.Equals(type, "MetadataProvider", StringComparison.OrdinalIgnoreCase) && category != "Metadata") ||
            (string.Equals(type, "GenericPlugin", StringComparison.OrdinalIgnoreCase) &&
             category != "Extras" && category != "Utilities") ||
            (!string.Equals(type, "GameLibrary", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(type, "MetadataProvider", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(type, "GenericPlugin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The extension type does not match its catalog category.");
        }

        string modulePath;
        if (!TryResolveFileWithinDirectory(directory, module, out modulePath) ||
            !File.Exists(modulePath) ||
            !string.Equals(Path.GetExtension(modulePath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The extension module is missing or invalid.");
        }

        var icon = ReadManifestValue(manifestPath, "Icon");
        string iconPath;
        if (!string.IsNullOrWhiteSpace(icon) &&
            (!TryResolveFileWithinDirectory(directory, icon, out iconPath) || !File.Exists(iconPath)))
        {
            throw new InvalidDataException("The extension icon is missing or invalid.");
        }
    }

    private static bool TryResolveFileWithinDirectory(string directory, string relativePath, out string resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        try
        {
            var root = Path.GetFullPath(directory).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            resolved = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadManifestValue(string path, string key)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > 128 * 1024)
        {
            return null;
        }
        var prefix = key + ":";
        var line = File.ReadLines(path).FirstOrDefault(value =>
            value.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line == null ? null : line.Substring(line.IndexOf(':') + 1).Trim().Trim('"', '\'');
    }

    private static string ResolveChildPath(string rootDirectory, string childName)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var child = Path.GetFullPath(Path.Combine(root, childName)).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A managed extension path escaped its root.");
        }
        return child;
    }

    private static void EnsurePlainDirectory(string path)
    {
        if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("A managed extension directory is missing or unsafe.");
        }
    }

    private static string ComputeSha256(string path)
    {
        using (var algorithm = SHA256.Create())
        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            return BitConverter.ToString(algorithm.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var sanitized = new string((value ?? "unknown")
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static void PreserveFailedExtensionJob(
        string queueRoot,
        string jobDirectory,
        string jobName,
        Exception exception)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(jobDirectory, "error.txt"),
                DateTime.Now.ToString("O") + Environment.NewLine + exception.Message,
                new UTF8Encoding(false));
            var failedPath = ResolveChildPath(queueRoot, ".failed-" + jobName);
            if (!Directory.Exists(failedPath))
            {
                Directory.Move(jobDirectory, failedPath);
            }
        }
        catch
        {
        }
    }

    private static void LogExtensionInstall(string dataDir, string message)
    {
        try
        {
            var logPath = Path.Combine(dataDir, "logs", "extension-installer.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(
                logPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static void RepairRelocatedLibraryPath(string dataDir)
    {
        var configPath = Path.Combine(dataDir, "Settings", "Core", "config.json");
        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var expression = new Regex(
                "(\\\"DatabasePath\\\"\\s*:\\s*\\\")(?<value>(?:\\\\.|[^\\\"])*)(\\\")",
                RegexOptions.CultureInvariant);
            var match = expression.Match(json);
            if (!match.Success)
            {
                return;
            }

            var configuredPath = match.Groups["value"].Value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"");
            var normalized = configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var looksLikePortableLibrary =
                normalized.EndsWith("\\Data\\library", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("\\App\\library", StringComparison.OrdinalIgnoreCase);
            if (!looksLikePortableLibrary || Directory.Exists(configuredPath))
            {
                return;
            }

            var repairedPath = Path.Combine(dataDir, "library");
            var escapedPath = repairedPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            json = json.Substring(0, match.Groups["value"].Index) +
                escapedPath +
                json.Substring(match.Groups["value"].Index + match.Groups["value"].Length);
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // A malformed user configuration should be handled by Osiris itself.
        }
    }

    private static void RepairLegacyBackupArchivePath(string dataDir)
    {
        var configPath = Path.Combine(dataDir, "Settings", "Core", "config.json");
        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var expression = new Regex(
                "(\\\"AutoBackupDir\\\"\\s*:\\s*\\\")(?<value>(?:\\\\.|[^\\\"])*)(\\\")",
                RegexOptions.CultureInvariant);
            var match = expression.Match(json);
            if (!match.Success)
            {
                return;
            }

            var configuredPath = match.Groups["value"].Value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"");
            var normalized = configuredPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!normalized.EndsWith("\\Data\\Backup", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var repairedPath = Path.Combine(dataDir, "Backups");
            var escapedPath = repairedPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            json = json.Substring(0, match.Groups["value"].Index) +
                escapedPath +
                json.Substring(match.Groups["value"].Index + match.Groups["value"].Length);
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // A malformed user configuration should be handled by Osiris itself.
        }
    }

    private static void OrganizeLibraryDatabaseFiles(string dataDir)
    {
        var libraryDirectory = Path.Combine(dataDir, "library");
        var databaseDirectory = Path.Combine(libraryDirectory, "DataBase");
        var filesDirectory = Path.Combine(libraryDirectory, "files");

        Directory.CreateDirectory(databaseDirectory);
        Directory.CreateDirectory(filesDirectory);

        foreach (var databaseFile in Directory.GetFiles(
            libraryDirectory,
            "*.db",
            SearchOption.TopDirectoryOnly))
        {
            MoveFileIfNeeded(
                databaseFile,
                Path.Combine(databaseDirectory, Path.GetFileName(databaseFile)));
        }

        MoveFileIfNeeded(
            Path.Combine(libraryDirectory, "database.json"),
            Path.Combine(databaseDirectory, "database.json"));
    }

    private static void NormalizeAnimatedArtwork(string appDir, string dataDir)
    {
        var normalizer = Path.Combine(appDir, "Osiris.ArtworkNormalizer.exe");
        if (!File.Exists(normalizer))
        {
            return;
        }

        try
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = normalizer,
                WorkingDirectory = appDir,
                Arguments = Quote(dataDir),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                if (process == null)
                {
                    return;
                }

                // Normalization normally only scans headers. Animated files are
                // handled in isolated workers so a hostile or unusually large
                // image cannot exhaust the launcher's address space.
                if (!process.WaitForExit(120000))
                {
                    try { process.Kill(); }
                    catch { }
                }
            }
        }
        catch
        {
            // Media repair must never prevent Osiris from launching. The helper
            // records per-file failures in Data/logs/artwork-normalizer.log.
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (IsInheritedPlayniteInstallRequest(args))
        {
            return 0;
        }

        var root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appDir = Path.Combine(root, "App");
        var dataDir = Path.Combine(root, "Data");
        var exe = Path.Combine(appDir, "Osiris.DesktopApp.exe");

        if (!WaitForRequestedProcess(args))
        {
            return 1;
        }

        Directory.CreateDirectory(dataDir);
        PrepareDataLayout(dataDir);
        ProcessExtensionDataRemovalQueue(dataDir);
        ProcessExtensionInstallQueue(dataDir);
        OrganizeLibraryDatabaseFiles(dataDir);
        RepairRelocatedLibraryPath(dataDir);
        RepairLegacyBackupArchivePath(dataDir);
        NormalizeAnimatedArtwork(appDir, dataDir);
        var registeredFonts = RegisterBundledFonts(appDir);
        try
        {
            var forwardedArguments = new List<string>(RemoveUserDataArguments(args));
            if (!HasArgument(args, "--hidesplashscreen"))
            {
                forwardedArguments.Add(Quote("--hidesplashscreen"));
            }

            var forwarded = forwardedArguments.Count == 0
                ? string.Empty
                : " " + string.Join(" ", forwardedArguments.ToArray());
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = appDir,
                Arguments = "--userdatadir " + Quote(dataDir) + forwarded,
                UseShellExecute = false
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return 1;
                }

                WaitForDesktopEngine(appDir, process);
            }

            return 0;
        }
        finally
        {
            UnregisterBundledFonts(registeredFonts);
        }
    }
}

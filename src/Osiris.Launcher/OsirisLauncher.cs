using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("Osiris")]
[assembly: AssemblyDescription("Numina Initiative Osiris launcher")]
[assembly: AssemblyCompany("Numina Initiative")]
[assembly: AssemblyProduct("Osiris")]
[assembly: AssemblyVersion("1.0.31.0")]
[assembly: AssemblyFileVersion("1.0.31.0")]

internal static class OsirisLauncher
{
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
            "Enhancements",
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
            MoveExtensionDirectory(extensionsDirectory, folderName, "Enhancements");
        }

        MoveLegacyGroup(extensionsDirectory, "Metadata Sources", "Metadata");

        var legacyPlugins = Path.Combine(extensionsDirectory, "Plugins");
        if (Directory.Exists(legacyPlugins))
        {
            foreach (var sourceDirectory in Directory.GetDirectories(legacyPlugins))
            {
                var folderName = Path.GetFileName(sourceDirectory);
                var categoryName =
                    folderName.IndexOf("Gallery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    folderName.IndexOf("HowLongToBeat", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Enhancements"
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
            "Enhancements",
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

    private static bool RunUpdateCheck(string root, string[] args)
    {
        if (args != null && args.Any(argument =>
            string.Equals(argument, "--skipupdatecheck", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var updater = Path.Combine(root, "App", "Osiris.Updater.exe");
        var version = Path.Combine(root, "version.json");
        if (!File.Exists(updater) || !File.Exists(version))
        {
            return false;
        }

        try
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = updater,
                WorkingDirectory = Path.GetDirectoryName(updater),
                Arguments = "--check --root " + Quote(root) +
                    " --parent " + Process.GetCurrentProcess().Id,
                UseShellExecute = false
            }))
            {
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit();
                return process.ExitCode == 10;
            }
        }
        catch
        {
            // Update availability must never prevent Osiris from launching.
            return false;
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appDir = Path.Combine(root, "App");
        var dataDir = Path.Combine(root, "Data");
        var exe = Path.Combine(appDir, "Osiris.DesktopApp.exe");

        Directory.CreateDirectory(dataDir);
        PrepareDataLayout(dataDir);
        OrganizeLibraryDatabaseFiles(dataDir);
        RepairRelocatedLibraryPath(dataDir);
        RepairLegacyBackupArchivePath(dataDir);
        if (RunUpdateCheck(root, args))
        {
            return 0;
        }
        var forwardedArguments = new List<string>(RemoveUserDataArguments(args));
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

        Process.Start(startInfo);
        return 0;
    }
}

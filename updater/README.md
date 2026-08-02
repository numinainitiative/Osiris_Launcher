# Osiris updater

Osiris now uses Numina Initiative's GitHub Releases instead of Playnite's
program-update system.

At launch, `Osiris.exe` runs `App/Osiris.Updater.exe`. The updater reads the
installed `version.json`, queries the configured public GitHub Releases API,
and compares semantic versions. Alpha and beta installations can receive
prereleases; stable installations ignore them.

When a newer release is available, the user sees an Osiris update prompt with
the release notes. If accepted, the updater:

1. Downloads the versioned checksum manifest and ZIP from GitHub Releases.
2. Validates the package size and SHA-256 checksum.
3. Rejects unsafe archive paths or any package containing writable `Data`.
4. Stages the package under the installation's `Data/Runtime/Updates` folder.
5. Waits for the launcher to exit, backs up the current application under
   `Data/Recovery/Updates`, and installs the new application files.
6. Restores the previous application automatically if installation fails.
7. Restarts Osiris with the existing `Data` directory.

Public installations do not contain a GitHub token. The configured repository
and its release assets must therefore be public. GitHub documents that public
release assets can be downloaded without authentication.

Run the automated tests with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\updater\tests\Test-Updater.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\updater\tests\Test-FullPackageUpgrade.ps1
```

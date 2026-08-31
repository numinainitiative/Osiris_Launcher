# Installation

Osiris prereleases are distributed as portable Windows ZIP packages.

## Install

1. Open the official GitHub Releases page.
2. Download `Osiris-<version>-win-x64.zip`.
3. Download the matching `Osiris-<version>-win-x64.json` manifest.
4. Extract the ZIP into a folder where your Windows user account can write.
5. Run `Osiris.exe`.

Do not use GitHub's automatically generated source-code archives. They contain
repository source, not the runnable application.

## Verify the Download

The manifest contains the expected SHA-256 checksum for the ZIP:

```powershell
$manifest = Get-Content .\Osiris-<version>-win-x64.json -Raw | ConvertFrom-Json
(Get-FileHash .\Osiris-<version>-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant() -eq $manifest.sha256
```

The result should be `True`.

## User Data

Osiris creates a writable `Data` folder inside the installation directory. This
folder contains the user's library, settings, extension data, cache, logs, and
backups. Updates must preserve it.

## Windows SmartScreen

Current prerelease packages are not code-signed. Windows may show a SmartScreen
warning until production code signing is available.

# Osiris Launcher

Osiris is a Windows game-library launcher based on Playnite and developed by
Numina Initiative.

This private repository is currently being prepared as the source of truth for
Osiris development and releases. It intentionally does not contain a copied
live installation or any user's `Data` directory.

## Repository and release model

- Git tracks source code, build scripts, documentation, installer definitions,
  updater code, and version metadata.
- GitHub Releases will host the versioned downloadable packages containing the
  application runtime and its large binary dependencies.
- Each installation keeps its own writable `Data` directory. An update must
  replace application files without replacing or uploading user data.

## Build a local release candidate

The initial release builder reads the isolated programming installation located
at `..\..\Programming\Development\Osiris` and creates a sanitized package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\New-OsirisRelease.ps1
```

The package is written to `artifacts/`, which is excluded from Git. See
[`docs/RELEASE-PROCESS.md`](docs/RELEASE-PROCESS.md) before publishing anything.

The personal installation under `G:\Gaming\Apps` is never a build source. See
[`docs/ENVIRONMENTS.md`](docs/ENVIRONMENTS.md) for the environment boundaries.

## Current status

The repository and packaging boundary are being established. Automatic update
checks, signed packages, unattended publishing, and the production installer
are not implemented yet.

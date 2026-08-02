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
The current development inputs and their deployed outputs are recorded in
[`docs/SOURCE-BUILD-MAP.md`](docs/SOURCE-BUILD-MAP.md).

## Current status

The inherited Playnite program update check is disabled and enforced by the
release builder. Osiris now contains its own GitHub Releases updater with
checksum validation, `Data` preservation, and rollback. Public repository
publication, code signing, unattended publishing, and the production installer
still require completion.

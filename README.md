# Osiris Launcher

Osiris Launcher is a Windows game-library launcher developed by Numina
Initiative. It provides a unified home for PC games, extensions, metadata, and
library management.

## Download

Download the latest prerelease from the
[GitHub Releases](https://github.com/numinainitiative/Osiris_Launcher/releases)
page.

Use the ZIP package named:

```text
Osiris-<version>-win-x64.zip
```

Do not use GitHub's automatically generated source-code archives; they are not
runnable Osiris packages.

See [Installation](docs/INSTALL.md) for checksum verification and setup notes.

## Repository Contents

- `src/Osiris.Launcher`: launcher wrapper source.
- `updater`: Osiris updater source.
- `installer`: installer-related files.
- `build`: release packaging scripts.
- `media`: repository-owned branding assets.
- `docs`: concise project documentation.

Large runtime binaries are distributed through GitHub Releases instead of normal
Git history.

## User Data

Each Osiris installation keeps a local `Data` folder for the user's library,
settings, extension data, cache, logs, and backups. Release packages and source
commits must not include a populated user `Data` folder.

## Maintainers

- [Release checklist](docs/RELEASE.md)
- [Branding assets](docs/BRANDING.md)

## Status

Osiris is currently in Beta. Prerelease packages are portable and unsigned while
the production installer and code signing are being completed.

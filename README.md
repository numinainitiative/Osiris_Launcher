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
- `build`: release packaging scripts.
- `media`: product branding assets.
- `docs`: installation notes.

Large runtime binaries are distributed through GitHub Releases instead of normal
Git history.

## User Data

Each Osiris installation keeps a local `Data` folder for the user's library,
settings, extension data, cache, logs, and backups. Updating Osiris preserves
this folder.

## Status

Osiris is currently in Beta. Prerelease packages are portable and unsigned while
code signing is being completed.

## License

Osiris is released under the [MIT License](LICENSE). Third-party runtime notices
are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

# Osiris Beta_1.0.32

Beta release focused on the independent extension ecosystem and media
reliability.

## Highlights

- Adds the GitHub-backed Extensions Browse catalog, verified package downloads,
  restart-safe installation and updates, recovery backups, and clear Install,
  Installing, Restart Needed, Installed, and Settings states.
- Introduces the redesigned Browse and Installed extension cards and the shared
  Osiris settings host used by Steam Library, Xbox Library, Steam Metadata, and
  SteamGridDB Metadata.
- Adds the original SteamGridDB Metadata provider contract. SteamGridDB appears
  as a media source only while that extension is installed and enabled; Osiris
  no longer discovers legacy credentials or calls SteamGridDB independently.
- Adds bounded animated-artwork handling. Original animations are preserved for
  playback while lightweight static posters protect the 32-bit desktop process
  from large WebP memory spikes.
- Improves Game Edit media preview lifetime, caching, and cleanup to reduce
  intermittent out-of-memory crashes.
- Adds restart-safe extension disable, data removal, uninstall, and transactional
  rollback behavior.

## Install

Download `Osiris-Beta_1.0.32-win-x64.zip` from the release assets, extract it to
a new folder, and run `Osiris.exe`. The ZIP is portable and creates a private
`Data` profile on first launch. It contains no development library, extensions,
credentials, browser state, settings, or logs.

Existing `Beta_1.0.31` installations can update normally through the Osiris
updater. The package remains unsigned while installer and code-signing work is
in progress. The accompanying JSON manifest contains its SHA-256 checksum.

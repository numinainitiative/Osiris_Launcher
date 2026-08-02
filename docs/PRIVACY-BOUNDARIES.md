# Privacy and data boundaries

## Never publish or commit

The working installation's top-level `Data` directory is a personal profile. It
can contain a game library, paths, extension state, browser state, logs, caches,
backups, and settings. It must never be copied into a release or Git history.

The working `App` directory can also accumulate writable state. The release
builder excludes these root entries when present:

- `Backup`, `Backups`, `browsercache`, `cache`, `config`
- `Data`, `Extensions`, `ExtensionsData`, `library`, `logs`
- `Recovery`, `Runtime`, `Settings`, `User Data`
- known user configuration and window-state files

## Release invariant

A public package contains application files plus an empty top-level `Data`
directory. First launch creates the user's writable profile. Updating an existing
installation must preserve that profile.

No package may be published until it passes the builder's validation and is
installed and launched successfully in a clean test location.


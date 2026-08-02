# Clean installation test

## 0.1.0-alpha.1 — 2026-08-02

Package: `Osiris-0.1.0-alpha.1-win-x64.zip`

SHA-256:
`e76da3a699cb17d27e2e160f74dfa8b4a66c04cc74b9767f986474d0d9c93d4c`

Test location:
`C:\Development\Osiris Launcher\Programming\Temp\Clean Install Tests\Osiris-0.1.0-alpha.1`

### Passed

- The archive checksum matched its release manifest.
- Required executables and runtime license notices were present.
- The packaged `Data` directory was empty before first launch.
- Osiris launched from the isolated test directory.
- First launch created a new profile under the test installation's `Data` directory.
- The running process explicitly used the test profile through `--userdatadir`.
- No reference to the developer's normal Osiris installation was found in readable
  test-profile configuration or log files.
- No fatal or unhandled startup error was recorded.

### Observations

- Chromium logged a non-fatal GPU overlay warning and continued through software
  rendering.
- Missing settings and database warnings were expected during creation of a new
  user profile.

### Release blocker discovered

The startup log shows the application requesting Playnite's official stable
update metadata:

`https://www.playnite.link/update/stable/10.56/update.json`

Osiris must not be publicly released while it can update from the upstream
Playnite channel. The built-in update check must be disabled or redirected to a
Numina Initiative-controlled Osiris release endpoint and tested before the first
public package is published.

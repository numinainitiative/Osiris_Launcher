# Osiris release process

This is the bootstrap process while publishing and updating are still being
implemented.

1. Update and test the working Osiris installation.
2. Change `version.json` to a new semantic version.
3. Run `build/New-OsirisRelease.ps1`.
4. Inspect the generated manifest and SHA-256 checksum.
5. Install the package in a clean test directory and test first launch.
6. Test an update from the previous released version while preserving its Data.
7. Commit the source and release metadata.
8. Create a matching Git tag and GitHub Release.
9. Attach the generated ZIP and manifest to that GitHub Release.

The first public release will not be published until the updater can reliably
detect a newer release, verify its package, replace only application files, and
recover from a failed update.

The inherited Playnite update check is a release blocker. It must be disabled or
redirected to the Numina Initiative Osiris channel before publication.

## Versioning

Osiris uses semantic versions. During private testing the channel is `alpha`,
starting with `0.1.0-alpha.1`. Stable releases will use versions such as `1.0.0`.

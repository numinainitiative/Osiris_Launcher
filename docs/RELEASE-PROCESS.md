# Osiris release process

This is the bootstrap release process while automated publishing and the
production installer are still being implemented.

1. Update and test the working Osiris installation.
2. Change `version.json` to the new Osiris version.
3. Run `build/New-OsirisRelease.ps1`.
4. Inspect the generated manifest and SHA-256 checksum.
5. Install the package in a clean test directory and test first launch.
6. Run both updater tests under `updater/tests` and confirm that `Data` survives.
7. Commit and push the exact source and version metadata used for the package.
8. Create a draft release with `build/Publish-OsirisRelease.ps1` and inspect the
   ZIP, manifest, release notes, tag, privacy boundary, and third-party notices.
9. Publish the draft. Installed copies on the matching channel will discover it
   through the public GitHub Releases API.

The updater now detects releases, verifies packages, preserves `Data`, and rolls
back a failed installation. The repository must be public before publication;
otherwise unauthenticated Osiris installations receive a GitHub 404 response.

The release builder verifies that inherited Playnite program updates have been
disabled with `tools/DisablePlayniteProgramUpdates`. This guard must remain in
place permanently unless the inherited Playnite updater code is removed from a
future fully rebuilt Osiris core. Osiris's own updater is independent of it.

## GitHub release contract

- Tag: the exact version, for example `Beta_1.0.31`.
- ZIP asset: `Osiris-<version>-win-x64.zip`.
- Manifest asset: `Osiris-<version>-win-x64.json`.
- Alpha and beta releases are GitHub prereleases. Stable releases are not.
- Published assets are immutable inputs to installed clients; never replace an
  asset under an existing version. Publish a new version for every correction.

`Publish-OsirisRelease.ps1` creates a draft by default. To publish immediately,
set `OSIRIS_GITHUB_TOKEN` to a fine-grained token with repository Contents write
permission and add `-Publish`. Draft-first publishing is recommended.

## Versioning

Osiris versions use `<Stage>_<Major>.<Minor>.<Patch>`. The accepted stages are
`Alpha`, `Beta`, and `Stable`. The current version is `Beta_1.0.31`; the next
routine beta update should be `Beta_1.0.32`. GitHub tags and release asset names
must preserve the exact capitalization and punctuation.

The retired `Beta_2026.x.x` calendar-style versions remain immutable historical
releases. Because their shipped updater compares the first numeric component
literally, those installations require one manual installation of
`Beta_1.0.31`; the conventional updater then handles all future updates and
treats `Beta_1.x.x` as newer than the legacy scheme.

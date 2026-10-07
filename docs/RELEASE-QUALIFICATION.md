# Release qualification

Build candidates with `build/New-OsirisRelease.ps1` from the prepared Development
installation. Its persistent Data profile must never be cleared or included in
the release archive. The builder verifies disabled inherited program updates,
branding, privacy exclusions, release metadata and checksums.

`build/Test-ReleaseCandidate.ps1 -Version Beta_0.0.51 -PreviousVersion Beta_0.0.50`
creates fresh qualification directories under `Programming/Sandbox/CleanInstall`
and `Programming/Sandbox/UpgradeTest`. It checks the candidate archive, loads the
coordinated Stats/Game Gallery/Screenshots Gallery/Trophies/Exophase/Steam Library/
Xbox Library packages, verifies every expected plugin version, tests root-wrapper
startup, and applies the real candidate through the updater while verifying
preservation of existing Data and extension files. Extension fixture versions
in the script must be updated when qualifying later coordinated releases.

Close the in-scope Development installation gracefully before startup tests.
The native IPC channel is shared: tests refuse to run when another engine is
active. Only root Osiris.exe wrappers launch the application. Startup logs are
under Data/logs. Reopen Development after testing; do not modify the personal
acceptance installation as part of public release qualification.

`updater/tests/Test-Updater.ps1` additionally exercises forced rollback. Its
isolated fixtures now default to a unique UpgradeTest directory and refuse to
overwrite existing directories. Neither test deletes an existing profile.

Upload an application draft first using `build/Publish-OsirisRelease.ps1` without
`-Publish`. Verify both uploaded assets' sizes and GitHub SHA-256 digests before
publishing the completed release as latest. Publish coordinated extensions
through their repository's verified draft-first publisher, then promote the
matching catalog entries. Confirm anonymous downloads against checksums.
GitHub's raw main catalog endpoint can cache the previous revision for up to
five minutes; an immutable commit URL verifies the published catalog immediately.

Beta 0.0.50 qualification passed: 231 theme checks, 92 Stats insight/settings
checks, 17 synthetic screenshot collector checks, 36 viewer presentation checks,
clean startup, real 0.0.49 upgrade with Data preservation, and forced rollback.

Beta 0.0.51 qualification passed: 231 theme/update checks, 33 edition/grid-options/
starting-page checks, 51 collection checks, 24 screenshot-folder/editor checks,
27 responsive Details checks, Details lifecycle/artwork policy, 156 Trophies
checks and 64 Exophase checks. Clean startup loaded all seven coordinated
extension versions. The real 0.0.50 -> 0.0.51 updater preserved the Data sentinel
and old Stats installation; forced apply failure restored the previous payload
without changing Data. Qualification directories use the suffix
`Beta_0.0.51-qualification-20261007-172219`. Xbox's read-only bridge builds cleanly;
its live-account trophy access remains acceptance-test work, not a qualification claim.

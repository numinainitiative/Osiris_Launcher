# Release qualification

Build candidates with `build/New-OsirisRelease.ps1` from the prepared Development
installation. Its persistent Data profile must never be cleared or included in
the release archive. The builder verifies disabled inherited program updates,
branding, privacy exclusions, release metadata and checksums.

`build/Test-ReleaseCandidate.ps1 -Version Beta_0.0.52 -PreviousVersion Beta_0.0.51`
creates fresh qualification directories under `Programming/Sandbox/CleanInstall`
and `Programming/Sandbox/UpgradeTest`. It checks the candidate archive, loads the
coordinated packages for all 11 catalogued extensions, verifies every expected
plugin version, tests root-wrapper
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

Beta 0.0.52 qualification passed on 2026-10-19: 583 theme/settings/update checks,
69 Exophase, 76 HowLongToBeat, 110 Stats insights, 160 Trophies,
23 synthetic screenshot collector, 39 screenshot presentation, 30 extension
navigation, 5 IGN fixtures, 5 Steam metadata matching (including read-only live
search), 11 time-only action tracking and 6 disposable uninstall checks.
Clean and real Beta 0.0.51 upgrade startup loaded all 11 catalogued extensions.
Existing Data and the old Stats extension survived the update; forced updater
failure restored the previous payload and retained Data. Qualification directories:
`Beta_0.0.52-qualification-20261019-152724` under CleanInstall and UpgradeTest.
Package SHA-256: `0f56dd6a70abdce57c122c95574d61d2c2c764a43d2e61b3ce59a17efdc0f9dc`.
The builder used the prepared Development runtime while theme source consolidation
remains incomplete; settings/font/navigation source is still in the workspace's
legacy ThemeSource tree. No private profile is part of the release.
The inherited IGN AngleSharp 0.9.9 NU1902 advisory remains unchanged.

Beta 0.0.52 and all eight coordinated extension releases are published. Both
application asset sizes and GitHub SHA-256 digests were checked before draft
promotion. Anonymous full application and extension package downloads match
their manifests. The public latest application endpoint and both immutable and
raw-main extension catalogues match all 11 expected versions/checksums. The
live host suite passes 586 checks. Development was reopened through its root
wrapper with the original persistent Data path; startup fatal/unhandled/plugin
error checks passed. The personal installation was not modified.

Beta 0.0.51 qualification passed: 231 theme/update checks, 33 edition/grid-options/
starting-page checks, 51 collection checks, 24 screenshot-folder/editor checks,
27 responsive Details checks, Details lifecycle/artwork policy, 156 Trophies
checks and 64 Exophase checks. Clean startup loaded all seven coordinated
extension versions. The real 0.0.50 -> 0.0.51 updater preserved the Data sentinel
and old Stats installation; forced apply failure restored the previous payload
without changing Data. Qualification directories use the suffix
`Beta_0.0.51-qualification-20261007-172219`. Xbox's read-only bridge builds cleanly;
its live-account trophy access remains acceptance-test work, not a qualification claim.

The four coordinated releases are published. GitHub asset sizes and digests,
anonymous extension downloads, the public latest app manifest, and both the
immutable and raw-main extension catalogues were verified. Live host update
validation passes with 234 checks and 11 public catalogue entries. Application
package SHA-256: `1fe454c047cce058c822791a52e5ed8962760d2c15c404c129a1f544599bfaa9`.

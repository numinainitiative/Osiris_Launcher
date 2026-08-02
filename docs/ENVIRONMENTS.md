# Osiris environments

Osiris development uses separate environments so build activity and disposable
test data cannot affect the developer's personal game library.

## Personal acceptance installation

Path: `G:\Gaming\Apps\Osiris Launcher App`

- Used for normal gaming and multi-day prerelease acceptance testing.
- Contains the real personal `Data` profile.
- Receives release candidates only through the same updater intended for users.
- Is never used as a build input and is never modified by development scripts.
- Its database and backup paths must remain under its own G-drive `Data` folder.

## Local Git repository

Path: `C:\Development\Osiris Launcher Project\Osiris_Launcher`

- Authoritative source, build scripts, installer, updater, version metadata, and
  release documentation.
- Local `artifacts` are ignored by Git; downloadable binaries belong in GitHub
  Releases rather than normal Git history.

## Development sandbox

Path: `C:\Development\Osiris Launcher Project\Sandboxes\Development\Osiris`

- Receives rapid local development builds.
- Uses a persistent but disposable demo `Data` profile.
- Is the transitional input to the release builder while the full source build
  is consolidated into the repository.
- Must never reference the personal G-drive `Data` profile.

## Clean-install sandbox

Path: `C:\Development\Osiris Launcher Project\Sandboxes\CleanInstall`

- Recreated from a release package for each release candidate.
- Begins with an empty `Data` directory.
- Simulates installation by a completely new user.

## Upgrade-test sandbox

Path: `C:\Development\Osiris Launcher Project\Sandboxes\UpgradeTest`

- Starts from the previous published version with disposable demo data.
- Receives the candidate through the real updater.
- Verifies application files change while the existing `Data` profile survives.
- Tests failure recovery and rollback before personal or public deployment.

## Demo profile seed

Path: `C:\Development\Osiris Launcher Project\Sandboxes\Profiles\DemoSeed`

- Holds a deliberately non-personal baseline used to reset sandbox profiles.
- Must contain no copied personal library, credentials, browser state, or logs.

## Promotion order

1. Build and hot-test in the development sandbox.
2. Test a fresh install in the clean-install sandbox.
3. Test an in-place update and rollback in the upgrade-test sandbox.
4. Publish a GitHub prerelease for the personal acceptance installation.
5. Use the candidate for several days.
6. Publish the tested commit and reproducible build to the stable channel.


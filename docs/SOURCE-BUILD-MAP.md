# Osiris source and build-input map

Audit date: 2026-08-02

Osiris does not yet have one reproducible source build. The current release
builder sanitizes and packages the disposable installation at
`Programming/Development/Osiris`. The following development inputs were traced
to that installation; paths in this document are relative to
`Development/Development (AI)/Development` unless otherwise stated.

## Active inputs and deployed outputs

| Development input | Produced or patched artifact | Deployed destination | Status |
| --- | --- | --- | --- |
| `DecompiledDesktopEngine/Osiris.DesktopEngine.csproj` | `bin/Release/net462/Playnite.DesktopApp.exe` | `App/Osiris.DesktopEngine.exe` | Active decompiled desktop source. Builds successfully after redirecting its references to the programming installation. Not yet in Git. |
| `ThemeSource/Code/OsirisTheme.csproj` | `media-build/OsirisTheme.dll` | `App/OsirisTheme.dll` and `App/Themes/Desktop/Default/OsirisTheme.dll` | Active. Both deployed copies must remain synchronized; the App-root copy is the assembly loaded by the desktop engine. |
| `Launchers/OsirisHardBoundLauncher.cs` | `OsirisHardBoundLauncher.exe` | `App/Osiris.DesktopApp.exe` and `App/Osiris.FullscreenApp.exe` | Active. Both deployed launchers matched the development executable by SHA-256. They force each installation to use its root `Data` directory. |
| `OsirisLauncher.cs` | root launcher | `Osiris.exe` | Migrated to `src/Osiris.Launcher` in the authoritative repository. The release builder now compiles it and embeds the repository-owned Osiris icon. |
| `Tools/OsirisPortableUninstaller.cs` | portable uninstaller | `Uninstall Osiris.exe` | Active source, but compilation and resource-patching steps are not orchestrated. |
| `Tools/OsirisArtworkNormalizer.csproj` | `Osiris.ArtworkNormalizer.exe` | `App/Osiris.ArtworkNormalizer.exe` | Active; build output matched the deployed tool by SHA-256. |
| Core and SDK patchers under `Tools/` | patched `Playnite.dll`, `Playnite.SDK.dll`, launcher, and selected extension assemblies | corresponding files under `App/` and `Data/Extensions/` | Active transformations, but their input versions and execution order are not captured in one build script. |
| `GitHub/Extensions/extensions/GameGallery/source` | standalone Game Gallery extension | `Data/Extensions/Enhancements/GameGallery_...` | Migrated to the private `numinainitiative/Osiris_Extensions` repository as version 2.0.1. It builds into an independently versioned `.pext` package; extension-private caches live under the matching `ExtensionsData/Enhancements` folder. Version 2.0.1 restores the theme presentation contract used for the large media stage and right-side thumbnail/trailer rail. The legacy nested repository remains preserved as upstream history/reference. |
| Loose XAML files at `Development/Development (AI)` | directly copied theme/control overrides | `App/Themes/Desktop/Default/...` and other theme paths | Partially traceable. `ComboBox.xaml`, `ListBox.xaml`, `ListView.xaml`, `Menu.xaml`, and `ScrollViewer.xaml` matched deployed files; other loose copies are stale or ambiguous. The release builder synchronizes the bottom-right footer label in `MainWindow.xaml` with `version.json`. |
| `Build-Release.ps1` | legacy sanitized directory copy | development-only `Release/` output | Superseded by the authoritative `build/New-OsirisRelease.ps1`. |

The inherited `App/Osiris.FullscreenEngine.exe` and several runtime binaries
have no confirmed editable source in the active workspace. Decompiled and
diagnostic folders that do not currently produce a deployed file are reference
material, not authoritative build inputs.

## Extension repository boundary

Osiris extension source, catalog metadata, license records, packaging, and
extension-specific releases belong in the separate private repository at
`GitHub/Extensions` (`numinainitiative/Osiris_Extensions`). Installed packages
under either installation's `Data/Extensions` are deployed test/runtime copies,
not source inputs. Private extension settings under `ExtensionsData` must never
enter either repository or a release package.

## Fresh-install presentation defaults

The active desktop-engine source applies Osiris presentation defaults only when
the profile has no configured database path, before `DesktopAppViewModel` is
created. This keeps first launch safe while leaving all later user changes
editable and persistent. The current baseline is:

- 20-pixel details-list icons;
- 214-pixel grid width, 2:3 cover ratio, zero card margin, and 20-pixel spacing;
- no grid-card background and no window background image;
- the sidebar menu button remains in its normal position;
- Vertical grid mode with Eight units on a new library; Horizontal mode starts
  with Three units the first time it is selected.

The grid-style and grid-size controls remain visible. Their selections are
stored independently in `Data/Settings/Osiris/libraryState.ini`; `Data` is never
used as a release build source, and no files from it are included in the public
release payload.

## Inherited Playnite program-update path

`App/Common.config` configures Playnite's primary and secondary update roots and
the `stable` branch. At startup, both desktop and fullscreen applications call
`PlayniteApplication.StartUpdateCheckerAsync`. Automatic program checks and the
desktop **Check for Updates** command both evaluate
`Playnite.Updater.IsUpdateAvailable`, whose original getter downloads
`update.json`. Historical logs confirmed a request to:

`https://www.playnite.link/update/stable/10.56/update.json`

Add-on updates do not use `Updater`; they call `Addons.CheckAddonUpdates`
separately. Disabling all startup update work would therefore remove useful
add-on and blacklist checks unnecessarily.

The interim source-controlled tool at
`tools/DisablePlayniteProgramUpdates` changes only
`Updater.IsUpdateAvailable` to return `false`. This blocks automatic and manual
Playnite **program** updates while preserving add-on behavior. The release
builder verifies the patch before it copies any runtime files.

## Reproducibility gaps

1. Migrate the active desktop engine, hard-bound mode launchers, theme source,
   and remaining patchers into the authoritative repository.
2. Replace absolute references to the programming installation with explicit,
   versioned build inputs or a generated local dependency path.
3. Define the exact original Playnite 10.56 inputs, patch order, expected
   intermediate hashes, and final hashes in one build/deploy script.
4. Identify or reconstruct the active fullscreen-engine source.
5. Consolidate direct XAML/theme overlays so each deployed file has one
   authoritative source.
6. Migrate the remaining extension sources with the same provenance, license,
   packaging, and clean-install verification used for Game Gallery.

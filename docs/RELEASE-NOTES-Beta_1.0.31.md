# Osiris Beta_1.0.31

Private GitHub prerelease using Osiris's conventional version sequence.

## Highlights

- Changes the official Osiris version from the retired calendar-style
  `Beta_2026.0.31` format to conventional `Beta_1.0.31` versioning.
- Adds transition-aware version comparison for future updates from the legacy
  calendar-style line to the conventional line.
- Polishes the desktop title bar with balanced Lucide window controls.
- Enlarges and refines the bottom information panel, with quieter links and a
  neutral beta badge.
- Adds the Osiris extension-settings host used by Steam Library, including its
  dynamic navigation, modal editing flow, and consistent Osiris settings styles.
- Aligns extension typography and controls with the user's selected Osiris font
  family and font-size settings.
- Adds canonical managed-list/table styling used by Exclusion List and extension
  settings pages.
- Preserves the corrected first-launch library grid defaults and cover layout.

## Install

Download `Osiris-Beta_1.0.31-win-x64.zip` from the release assets, extract it
into a new folder, and run `Osiris.exe`. Do not download GitHub's automatically
generated source-code archives; they are not runnable Osiris packages.

The ZIP is portable and creates a new private `Data` profile on first launch.
It does not contain the development library, installed extension packages,
credentials, browser state, settings, or logs. A conventional installer and code
signing are not included yet.

Existing `Beta_2026.0.31` installations need this one release installed manually
because their immutable old updater cannot rank the new conventional number.
After `Beta_1.0.31` is installed, future conventional updates are compared and
installed normally.

While this repository is private, GitHub requires a signed-in authorized account
to download the release and Osiris cannot detect it automatically. Automatic
GitHub update notifications will be enabled for normal installations when the
repository becomes public.

The SHA-256 checksum is published in the accompanying JSON manifest.

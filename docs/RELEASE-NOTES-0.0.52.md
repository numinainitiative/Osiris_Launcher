# Osiris Beta 0.0.52

## Settings and presentation

- Polish main and extension settings with consistent spacing, typography, margins, controls and Update-menu-style dropdown menus.
- Restore application-wide font selection and add a font style selector with live preview and Save/Cancel support. Icon fonts remain unchanged.
- Refine General startup/tray controls, Storage, Backup, Performance, Contacts & Links and extension account/danger-zone pages.
- Redesign Notifications with All/Unread/Archived tabs, compact outlined cards, clearer update messages and read/unread states, persistent dismiss controls and a wider details panel.
- Refresh Game Gallery and Trophies icons.

## Jobs and game tracking

- Open the Update menu with a left click. Run all Jobs executes the currently enabled jobs in menu order.
- Add Update Extensions Database for configured Exophase, HowLongToBeat and Trophies databases, with cancellation and per-provider results.
- Add Disabled (time only) to File/URL action tracking. Game-running detection, screenshots, play count and lifecycle events remain active; Osiris does not add elapsed time for that action. It does not import Steam hours or alter Steam achievements.
- Improve local-game uninstall handling for read-only files while retaining confirmation and path-safety checks.

## Coordinated extensions

Game Gallery 2.0.5, Exophase 0.2.8, HowLongToBeat 1.2.5, Stats 0.1.4,
Screenshots Gallery 0.1.1, Trophies 0.1.1, IGN Metadata 1.0.2 and
Steam Metadata 1.0.1 are published separately through Extensions.
Update Osiris first, then update installed extensions.

These include extension-to-game navigation fixes, clearer platform/time legends,
shared earned-trophy statistics, verified screenshot transfers and metadata matching/API fixes.
Steam Library, Xbox Library and SteamGridDB settings polish is part of the app update;
their already-current packages do not need a new version.

## Safety and qualification

Existing Data, settings, screenshots and extension state are preserved by the normal updater.
Release archives contain no user profile, browser state or credentials.
The inherited Playnite program-update channel remains disabled.
Clean installation, real Beta 0.0.51 upgrade, data preservation, forced rollback,
all 11 extension startups and targeted regression checks passed.
Live Steam/Xbox/Exophase account behavior still depends on account permissions and service availability.
IGN retains its inherited AngleSharp 0.9.9 dependency advisory; this release does not upgrade that parser.

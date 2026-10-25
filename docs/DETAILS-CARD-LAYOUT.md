# Game Details card layout editor

Implemented 21 October 2026; included in Osiris Beta 0.0.53.

The existing header edit button opens the same card-editing treatment as Home:
square dashed outlines (3px stroke, 10/6 pattern), four thick directional arrows,
and a centred 144px dimmed square. Details cards have no delete buttons.
The hero artwork/logo, play panel and hero statistics are not editable here.

Development correction (22 October, not in the existing 0.0.53 assets): up/down
reorder visible cards within their original column. Left/right controls are
removed, including in narrow layout. The secondary cards are discovered inside
their nested shared panel so each receives its own edit box. Old cross-column
saved layouts normalize to original columns without losing cards. Hidden extension
cards remain hidden and retain their saved position until available again.

Click the header button again to Save. Leaving the page or switching games
cancels unsaved moves. This is one layout shared across games, not a per-game
database change. Saving uses an atomic replacement of
`Data/Settings/Osiris/osiris-details-card-layout.json` (schema version 1, stable
card-ID lists `Left` and `Right`). Unknown/duplicate IDs are ignored, missing
cards are restored to their default column, and unavailable cards are retained.
Save errors keep editing active. No screenshot, tracking, game, extension or
personal settings data is written into the layout document.

## Implementation and verification

Editable code remains in the legacy ThemeSource directory pending consolidation:
`Development/Contents and Stuff/Development (AI)/Development/ThemeSource`.
`Code/DetailsCardLayout.cs` manages staging, persistence and responsive order;
`Code/CardLayoutEditChrome.cs` supplies identical Home/Details chrome.
`Views/DetailsViewGameOverview.xaml` attaches the editor; the direction-button
style in `Views/MainWindow.xaml` is shared at dictionary scope. HeaderNavigation
routes the existing header button by page context. Responsive layout refreshes
the saved/staged order after reparenting the secondary column.

The editor temporarily wraps existing cards rather than rebuilding their
content. It restores original margins/bindings and removes overlays on exit.
Provider controls and plugin tabs remain outside the editable card list.

Build `Code/OsirisTheme.csproj` in Release with `EmbedRajdhaniFonts=true`.
Run `DetailsCardLayoutValidation` (52 checks), `UpdateChecksValidation` (679),
`ResponsiveGameDetailsValidation` (27), and `DetailsLifecycleValidation` against
the built theme and Development App. Isolated tests use temporary layout files,
not the live Development profile. They cover no-delete chrome, staging/cancel,
persistence, bindings, unavailable cards, compact mode, selection/unload cleanup,
normalization, repeated editing and failed saves.

Deploy the three matching theme DLL copies plus the two changed Views files
only after verified runtime backups. Restart Development through its root
`Osiris.exe` wrapper, verify the sole engine's Development Data path, and inspect
startup logs. Do not use or modify the personal installation for this work.

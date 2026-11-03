# Independent edition profiles (Beta 0.0.55)

An edition is now a distinct native game record. `GameEditions.json` schema 4
stores the grouping, edition labels, selected edition and physical game IDs.
One selected member appears in the Library grid/sidebar, Home cards and
Collections. Switching rebuilds Game Details and every Game Edit page around
that member's actual database identity; it does not copy fields onto the
integrated original.

Use Game Edit -> General -> Edition to add/select edition labels, then Save.
Newly added editions receive a local profile with the game's name and otherwise
blank metadata, installation and actions. The footer edition selector is
available on every editor page, including General and Advanced. Each profile
has its own extension data keys, media, screenshots, playtime, last activity and
play count. Its native `PluginId` determines installation permissions. The
original Steam/Xbox profile keeps its integration; a new DRM-free edition has
full local installation/archive controls.

Removing an edition label on Save removes that edition's database profile.
Removing an integrated member this way also adds an import exclusion so the
next library refresh does not recreate it outside the group. The native Remove
Game flow retains its existing Remove/Remove and exclude decision and removes
only the current member. Remaining members survive, including when the original
is removed. Old navigation IDs resolve to the surviving selected profile.
Removing a profile never invokes uninstall or traverses its install/archive
directory. Removing the final edition label retains the standalone game; the
ordinary Remove Game command can remove that final profile separately.

## Copy a page from another edition

The footer's **Copy page** button opens a list of other editions in this group.
Choose a source to fill the current editor page from that edition's saved data.
The destination remains a draft until Save; Cancel discards the copied changes.
Nothing is synchronized afterwards. General includes its metadata selectors and
description; Advanced copies its editable fields while protecting game IDs and
library ownership. Actions/Links are deep clones, and Scripts/Installation use
explicit field lists. The edition labels/group membership are never copied.

Media stages separate artwork copies through the normal editor save pipeline;
Gallery uses its existing reversible add/remove draft handling. HowLongToBeat
and Exophase load independent settings into the destination's controls.
Screenshots/Stats copy their editable enablement setting, keeping recorded
screenshots and session history distinct. Trophies fills the editable catalogue
input; Import remains explicit and earned achievement history is not cloned.
Danger Zone has no copy control.

`src/Osiris.EditionProfiles/EditionPageCopy.cs` owns the native field whitelist and
metadata picker adaptation. The isolated profile suite now passes 54 checks,
including identity protection, page boundaries, draft-only changes, independent
collection/action/link objects and actual native metadata selector behavior.

## Optional shared pages (Beta 0.0.56, 3 November 2026)

The canonical **Share page** action sits next to **Copy page** in the Game Edit
footer. It currently supports General, Advanced, Actions, Links, Scripts and
Trophies. Other pages retain their independent file/settings ownership. Sharing
is opt-in for each page and each group of editions; creating an edition never
links anything automatically. Choose an edition to fill the current draft and
stage a link, then Save. Cancel or switching editions discards pending links.
Linked pages show **Shared page**. **Stop sharing (keep a copy)** separates only
this edition; other linked editions stay linked.

Native pages synchronize only their explicit `EditionPageCopy` field whitelist,
using SDK game-update events from either edition. Changed fields are deep-copied
into sibling records. IDs, platform/library ownership, installation, running
state and unrelated page fields never follow these links. Advanced intentionally
shares its playtime, play count, last played, completion state, added date,
favorite/hidden flags and notes. These are mirrored totals, not summed totals.
Joining or leaving a shared group is blocked while a member is running.

Optional `SharedPages` sets live in the existing schema-4 grouping document.
Sharing graph writes use the existing recovery snapshots and atomic replacement;
a failed multi-page sharing change restores the previous graph. The native save
pipeline retains its existing per-store commit behavior.

Trophies 0.1.2 resolves a single canonical catalogue through the public read-only
`OsirisTheme.EditionPageSharing.ResolveOwner` bridge. Editor, card, earned-trophy
Stats bridge, refresh revision and sync all use that owner. Integrated-library
source filtering uses the canonical profile, so a local edition explicitly linked
to Steam displays the same actual earned trophies. Sharing previews disable Sync
until Save; catalogue import remains staged. Cancel does not establish a link or
write its preview catalogue. Unlinking snapshots catalogue and earned progress,
including provider provenance; deleting the canonical edition transfers the
catalogue before removing its native row. Explicit successful sync replaces
snapshot presentation provenance with the selected profile's own provider.

`EditionPageSharing.cs` is authoritative app source, linked into the theme build.
Validation: 77 isolated profile/copy/sharing/dispatcher checks, 165 Trophies checks,
65 Details layout checks and 72 Collection checks. The sharing suite exercises
real SDK update-event delivery, both directions, third-member joins, restart,
unlink, running and unrelated-game guards, failed batch rollback, catalogue
preservation and canonical-member removal. No real games/accounts are changed by
these tests.

Deployed to Development only after a checksum-verified 13-file, 11,985,696-byte
backup at `Data/Recovery/ManualFixes/shared-edition-pages-20261103-000533`.
Development's persistent Data is preserved; personal installation/public releases
are unchanged. All three theme DLLs, canonical MainWindow XAML and the matching
Trophies DLL/manifest are deployed together. Visual confirmation is left to the
user; no mouse movement or screenshots are used.

## Existing editions and recovery

Schema 3 profiles convert when opened in Game Edit, switched or saved. Opening
the app or reading Library filters alone does not convert them. Before conversion
or profile removal, a JSON recovery snapshot is written under
`Data/Settings/Osiris/EditionProfileRecovery`. The previous grouping document is
also retained as `GameEditions.json.previous` using atomic file replacement.
Legacy page snapshots and inert retired pairing records remain in the grouping
document as recovery data.

The original database ID/integration is retained for the original/default
edition; added editions receive their former edition IDs as native game IDs.
Existing per-edition installation/actions/media and known per-game settings are
restored. Historically shared metadata is copied as the starting value and then
becomes independent. Historically shared playtime/play count/last activity stay
on the original, since they cannot be reliably attributed retroactively.
New editions created after conversion start with empty data, except their name.
Switching/removing grouped profiles is blocked while a group member is running.

## Source and verification

- New source: `src/Osiris.EditionProfiles/IndependentEditionProfiles.cs`, compiled
  into the existing theme alongside the legacy `GameEditions` partial class.
- Legacy theme integration: `Code/OsirisGameEditions.cs`, navigation/Game Edit
  in `Code/OsirisSidebarBehavior.cs`, Home/Collection selectors and the native
  Installation checkbox binding in `Views/MainWindow.xaml`.
- `tools/EditionProfilesValidation` uses an isolated mock SDK database and
  explicit sandbox paths; it covers creation, full metadata separation,
  installation permissions, independent storage/stats, persistence/reload,
  running-session guards, deletion of either member without uninstalling files,
  old-navigation recovery, native member removal and schema-3 migration.
  It also injects a database write failure to verify conversion rolls back
  without orphan records. All 41 checks pass, alongside 33 legacy compatibility,
  687 host/settings, 72 Collection, 65 Details, 27 responsive and 46 native
  action/lifecycle checks.

Initially deployed to Development on 2 November, then qualified for Beta0.0.55
at the user's request. Copy the matched theme DLL into its three
runtime locations together with `Themes/Desktop/Default/Views/MainWindow.xaml`,
after backing up those files, the grouping/settings, database records and
existing edition media folders. Launch through the root `Osiris.exe` wrapper.
The initial Development tests used `--nolibupdate`; the background metadata
event's MainWindow access has since been moved to the UI dispatcher and normal
startup verified for Beta0.0.55. No personal installation was modified.

Development was deployed and reopened successfully on 2 November. The four
runtime files and relevant persistent settings/database/edition-media files were
checksum-backed up under `Data/Recovery/ManualFixes/independent-edition-profiles-20261102-201722`
(101 files, 42,840,700 bytes). Startup loaded all twelve extensions with the
intended persistent Development Data path and no new fatal/unhandled errors.
Visual acceptance is left to the user; no mouse interaction or screenshots were
used for verification.

Optional shared pages are qualified for Beta 0.0.56 with Trophies 0.1.2; clean startup and the real 0.0.55 upgrade passed.

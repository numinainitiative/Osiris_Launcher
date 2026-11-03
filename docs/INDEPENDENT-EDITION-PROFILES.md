# Independent edition profiles (Beta 0.0.55)

An edition is now a distinct native game record. `GameEditions.json` schema 4
stores the grouping, edition labels, selected edition and physical game IDs.
One selected member appears in the Library grid/sidebar, Home cards and
Collections. Switching rebuilds Game Details and every Game Edit page around
that member's actual database identity; it does not copy fields onto the
integrated original.

In released 0.0.55/0.0.56, use Game Edit -> General -> Edition to add/select edition labels, then Save. Development now uses the footer controls described below.
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

## Development footer and page mirrors (3 November 2026)

General no longer contains an Editions field. The bottom panel contains Add
Edition, the edition selector, Delete edition immediately after it, and Copy
page. All controls share one 86px-high footer row spanning the full width under
the navigation panel. The edition/mirror controls scroll horizontally only when
needed; an Auto-width actions column reserves the actual Save/Cancel/metadata
button width and prevents overlap. Add Edition
prompts for a unique name, creates an otherwise-empty local profile (base game
name retained) and selects it without losing the previous edition's draft.
Unsaved new editions are removed on Cancel or navigation away. Saving commits
visited drafts and retains the new profiles. Single-profile games show Standard
and can add another edition without configuring anything in General.

The old Share page menu is replaced by the canonical switch, label **Enable page
mirror with** and canonical edition dropdown. Choose a source for each supported
page (General, Advanced, Actions, Links, Scripts, Trophies). Mirrored content is
read-only; Copy page and an indirect Advanced statistics reset are disabled on
the dependent edition. Disable mirroring to keep an independently editable copy.
The supplied `media/icons/edition-link.svg` vector is rendered in green at the
far right edge of mirrored navigation items. Source pages are not marked as mirrors.
Pending mirror settings/icons follow each edition's unsaved draft.

Mirrors are directional. The selected edition supplies the page, including
Advanced totals; native target updates never overwrite its source. Source chains
resolve to their ultimate owner. Cycle-producing dropdown choices are excluded,
and cycles involving multiple unsaved edition drafts are checked before any
edition is saved. Optional schema-4 PageMirrors maps store target/page/source;
old SharedPages sets migrate in memory to their canonical source until a later
normal Save writes the document. Native field whitelists still protect identities,
library ownership, installation and unrelated pages. Trophies' existing read-only
host bridge follows the same source, with no extension package change.

Delete edition and Edit Danger Zone remove the explicit editor selection only.
Library grid/list context-menu removal resolves a grouped/stale navigation ID to
its active physical edition and deduplicates grouped selections. Legacy overlay
groups convert with recovery backups before this explicit removal operation,
so their single old database row cannot accidentally erase the entire group. Other editions
survive. Confirmation identifies the edition and explains preservation of
siblings/install/archive folders. Removing a mirror source detaches direct
dependents with saved native values and a trophy catalogue/progress snapshot;
remaining chains continue following their surviving source. Installed game files
are never traversed. Removing the final edition removes the final library entry.

Authoritative integration: EditionEditingControls.cs, EditionPageSharing.cs,
EditionEditorSession.cs and the legacy controller/MainWindow XAML/profile model.
119 isolated profile/draft/mirror/UI checks passed, including canonical controls,
read-only content, green vector badge, cycle guards, target-only removal routing,
empty independent creation and navigation cleanup. 65 Details/72 Collection
checks also passed. Development alone received the three matched theme DLLs and
MainWindow XAML after a checksum-verified 29-file, 6,629,155-byte backup at
`Data/Recovery/ManualFixes/edition-footer-mirrors-20261103-163725`.
Final DLL SHA-256: `7a0f1510210009c1b31e991682019b91f2cb0ba3cb01481d2d7a4291e35151b5`.
Personal runtime hash remains `7028d37633763c89430882d121176649585991a154d47057b22091dc93473b18`.
No public release or personal file changes; persistent Development Data preserved.
No mouse movement or screenshots. Development reopened through its root wrapper
with correct Data path and clean core startup; visual acceptance is left to user.

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

## Released 0.0.56 shared pages (historical; replaced in Development)

The canonical **Share page** action sits next to **Copy page** in the Game Edit
footer. It currently supports General, Advanced, Actions, Links, Scripts and
Trophies. Other pages retain their independent file/settings ownership. Sharing
is opt-in for each page and each group of editions; creating an edition never
links anything automatically. Choose an edition to fill the current draft and
stage a link, then Save. Cancel discards pending links; switching editions now retains them in each edition's draft.
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

## Edition editor draft hotfix (3 November 2026)

Switching editions in Game Edit keeps a separate in-memory draft for every
visited physical game profile. The native editor model is suspended rather than
closed, preserving its metadata selectors and derived values. Focused text
bindings are flushed before switching. Reopening an edition restores its current
text, settings, actions/scripts/links, artwork selection and temporary file
ownership, gallery add/remove lists, HLTB/Exophase selections, screenshots row
removals, Stats toggle, Trophies editor instance and pending sharing choices.
Group edition-label selections follow the entire editing session.

Save validates all drafts, then commits every visited edition through the
existing native and extension save pipelines. Invalid drafts are restored for
correction before any edition is committed. The existing per-store save behavior
is retained; this does not add an all-store disk transaction. Cancel/navigation
releases all suspended native models and only session-owned temporary media;
original screenshots/gallery files are not removed until Save. Switching does
not auto-save. Old size-calculation callbacks are invalidated across switches.

Authoritative additions: `EditorDraftSnapshot.cs`, `EditionEditorSession.cs`.
The latter extends the existing GameEditPageController partial class; the legacy
Screenshots editor exposes a memory-only draft capture/restore operation. Native
models, editor controls and staged file ownership are never serialized into live
Data. 101 isolated profile/draft/sharing checks pass (including the real switch
handler, two native-model fixtures, focused bindings, repeated round trips,
single Save for both editions, independent metadata selectors/General/Advanced,
Trophies target IDs, pending sharing, Cancel cleanup and invalid-draft preflight),
plus 65 Details and 72 Collection checks.

The user explicitly authorized this hotfix on Development and the personal
installation. Both were closed before replacing only the three matching theme
DLLs. Each installation has a checksum-verified backup under
`Data/Recovery/ManualFixes/edition-editor-drafts-20261103-151832`.
Installed SHA-256: `7028d37633763c89430882d121176649585991a154d47057b22091dc93473b18`.
Version remains Beta 0.0.56; no new public release is published by this hotfix.
Data, settings, libraries and extension packages are preserved. No mouse movement
or screenshots were used; visual acceptance is left to the user.

Both installations restarted through their root wrappers with correct original Data paths and no core startup errors. Development was then closed; personal was left running for acceptance testing. Personal Trophies 0.1.1 was preserved; this hotfix requires no extension update.

Development footer alignment correction: the previous two-row wrapping layout was replaced with a full-width single horizontal row. XAML parses, named controls/styles are preserved, and the previous runtime XAML is checksum-backed up at `Data/Recovery/ManualFixes/edition-footer-single-row-20261103-175724`. Only Development MainWindow XAML changed; personal/runtime DLLs/data are untouched.

Development mirror indicator alignment: the link badge now occupies a dedicated trailing Auto column, aligned right with a 12px label gap. Labels retain their original left inset. The semantic green badge is excluded from selected/unselected navigation text recoloring. Three runtime DLLs were hash-backed up under `Data/Recovery/ManualFixes/mirror-icon-right-20261103-180632` before deployment; personal installation and Data are unchanged.

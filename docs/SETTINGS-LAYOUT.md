# Settings page layout

General's font family row also includes Style: shared 190x38 dropdown with
18 px label gap and 28 px group separation. Default preserves existing role
weights; Light/Regular/Medium/SemiBold/Bold/ExtraBold preview across ordinary,
fixed, inline and HTML text. Native font matching handles absent exact weights.
Icons and text sizes remain unchanged. Save persists FontWeight beside family
and size; Cancel restores saved weight and selector. Default restores weights
captured before explicit preview. Existing fixed-family/size passes must not
overwrite explicit weight, and HTML refreshes when weight changes.

General → Application font restores a live-preview, Save/Cancel-staged font
choice using installed text families plus Rajdhani. Canonical 190x38 dropdown,
18 px label gap and 24 px option spacing; font size remains unchanged. Save
persists FontFamily in the existing osiris-font-size.json; missing/unavailable
or path-like values safely fall back to Rajdhani. Existing profiles preserve
their current look until an explicit choice. The chosen family covers ordinary
and locked core/extension pages, notification bodies, inline text, new controls
and Osiris overview HTML, without replacing icon glyph families. Local FontFamily
resources and post-extension-style passes follow the choice; fixed role sizes
and weights remain. Cancel restores prior saved family and old special body
faces when no global override was committed. External sign-in sites retain
their own web typography. No profile reset or app restart is required to preview.

Xbox Library General, Account and Danger Zone now all use the canonical
40 px margins/760 px forms, natural switch rows/56 px columns, 24 px option
spacing and actual Backup label/hint/toggle/action styles. Account preserves
native login/status with 2 px hints, 12 px before actions and 24 px after the
action row. Status stays 15 px without losing native async triggers. Danger
Zone uses polished original controls and existing confirmations/queued actions;
manifest-ID scoping also supports disabled management. Repeat font/layout passes
preserve controls, bindings and scrolling; plugin behavior/data remain unchanged.

Steam Metadata closed dropdown labels use SelectedItem.Value for language and
SelectedItem.Name for Steam Deck, respecting native DisplayMemberPath without
changing bindings, choices or values. Override only the closed presenter of the
canonical template; single-line ellipsis prevents raw object names/wrapping and
keeps the chevron clear. The metadata helper's ScrollViewer stretches across
the viewport around its 760 px form, avoiding right-outline clipping caused by
the theme scroll template's one-pixel inner border. Verification includes actual
native choice types and measured bounds under the real scroll template.

Xbox Library: Connect account moves from General to the first Account option,
above authentication. Open games with the Xbox app follows General's installed,
uninstalled and console imports. Advanced is removed; troubleshooting moves to
Account. Original controls/bindings/LoginCommand/status and Save/Cancel stay.
Import choices still depend on Settings.ConnectAccount across pages; the Xbox-app
launch choice remains independent. This is organization-only, not a layout or
authentication rebuild.

SteamGridDB Metadata General no longer exposes similar-media application;
saved behavior remains unchanged. Adult/humorous artwork are its only options.
General, Account and Danger Zone use shared 40 px margins/760 px forms and actual
Backup styles. Natural 56 px switch columns, 24 px option gaps and 17/15 px text
with 2 px hints match other polished extensions. Account's original API field
and LoginCommand action use Backup's 388x40 field, 8 px gap and 38 px button
height. Get API key uses 104 px minimum width, measuring naturally for its full
label plus canonical padding; fixed 104 px clipped the text at current font/DPI.
This caption-sized action is the exception to the fixed 500 px path row. Keep
18 px above controls, one scroll wrapper and 2 px final outline gutter.
Danger Zone keeps polished original management controls and red actions.
Repeated typography/layout is idempotent. No extension assembly or data change.

Steam Metadata exposes General and Danger Zone; Tags is not added during host
preparation. Saved tag lists, limits and prefixes remain unchanged. Both pages
use shared 40 px margins/760 px forms and actual Backup styles. General's four
options have natural heights, 24 px gaps and 17/15 px text roles with 2 px hints.
Three selectors retain native choices, display paths and bindings, using the
190x38 outlined field and one explicit 18 px spacer; vertical-cover switch uses
the 56 px column. One automatic scroll wrapper is retained across font refreshes.
Danger Zone uses polished original management controls/canonical red actions.
No Steam Metadata assembly rebuild, settings reset or provider behavior change.

Steam Library's Additional Accounts category is now Account. General exposes
native installed/uninstalled import switches with exact Backup styles and 24 px
spacing. Uninstalled import defaults off in the existing native setting; preserve
saved choices. Disable its switch while Connect account is off; keep the 15 px
hint "A connected Steam account is needed for this option" readable, 2 px beneath
its title. Native Steam account fetching/import filtering remain unchanged.
Account retains Connect account/status, login/authentication and identity/API
controls. Remove the former two import options from Account (uninstalled moves
to General; IgnoreOtherInstalled is hidden), web/API comparison, troubleshooting
and entire additional-accounts table/actions UI. Keep legacy accounts/settings
without clearing data. One 760 px scroll form retains namescope, commands,
password helpers and Save/Cancel. Reapply styles after host typography;
preparation remains idempotent. Theme-only, no Steam assembly/auth changes.

Steam General/Account also use shared 40 px outer margins/760 px left forms,
natural rows and 24 px option gaps. Connect uses Backup's switch and 56 px column;
original live status appears under its label with 2 px gap/15 px hint typography.
Its style derives from the native status style to retain async login-state
triggers and warning colours. Native web/API radios keep selection templates,
bindings and grouping, with standard label typography. Authenticate reuses the
exact Backup action style/38 px height and 12/24 px action spacing. Family/free
switches use canonical rows; existing visibility/enabled dependencies remain.
Account fields reuse Backup's 388x40/15 px helper, 18 px label gap and 24 px row
spacing. No legacy 25/40 px authentication indent. Typography refresh is
idempotent; password helpers/commands remain on original controls. Other Steam
categories have not been migrated by this scoped request.

Stats exposes Global and Per Game under Insights, followed by Danger Zone;
General is removed before navigation is built. Group by page header rather than
old tab indices, keeping Danger Zone outside the group. All three pages use
shared 40 px margins and 760 px left forms with actual Backup styles. Insights
switches have natural heights, 56 px switch columns, 24 px option gaps and
17/15 px text roles; master-switch hints align beneath their titles with 2 px
spacing. Retain original two-way bindings, native controls, preferences and
transactional management actions. Scrolling and repeated normalization are
idempotent. This is a theme-only change, with no Stats package rebuild.

Screenshots Gallery General and Danger Zone share the canonical Game Gallery
adapter: 40 px margins/760 px form, natural expanded-experience row, actual
Backup label style/18 px selector gap, outlined dropdown and Update-style popup.
Keep Cinematic/Full screen values and native two-way binding; capture/collection
behavior and saved media are unchanged. Danger Zone uses shared natural rows,
17/15 px roles, 2/24/12 px gaps, red actions and existing management safeguards.
No screenshot extension assembly rebuild or package upgrade is required.

IGN Metadata opens directly on Danger Zone, without the redundant General
placeholder/navigation item. Identification uses plugin or manifest ID so
disabled installations remain manageable. Shared 40 px margins/760 px form,
actual Backup 17/15 px text/toggle/action styles, natural row heights and
2/24/12 px gaps match polished extension settings. Native destructive controls,
warnings, queued Save/Cancel/restart semantics and metadata configuration stay
unchanged. Other providers' General pages are unaffected.

HowLongToBeat's Database action has a single description above the button,
combining the saved-match eligibility and unchanged-unfetched-game explanation.
The former below-button status line is collapsed with zero margin; its native
binding remains. Useful live status is appended to the upper paragraph, while
the redundant default idle note is omitted. Native action/footer behavior stays.

HowLongToBeat General/Danger Zone use shared 40 px margins and 760 px left
form. Retain all existing options/headings and native update behavior. General
has one automatic vertical ScrollViewer, natural profile row, exact 18 px
label/selector spacer and three Auto completion-switch rows with 24 px gaps.
Use actual Backup label/hint/toggle/action styles and outlined dropdown (shared
Update-style popup). Headings/hints use 17/15 px roles, 2 px internal gaps;
database hint/action/status use 12 px gaps and pixel-safe bottom clearance.
Danger Zone shares the polished Exophase management layout and safeguards.
Scoped theme adapter only; installed extension and saved data remain unchanged.

Update Button jobs adds Update Extensions Database after Update Metadata,
before Check for Updates (four visibility switches, canonical 56/2/24 px
toggle/hint/option layout). Its preference defaults visible for old settings
and follows staged Save/Cancel. Menu styling is unchanged; Run all Jobs awaits
this stage between metadata and release checks. See
[Extension database updates](EXTENSION-DATABASE-UPDATES.md) for provider scope,
cancellation, failures and compatibility.

All main/extension settings dropdown popup lists use the Update button menu's
appearance: #111111/#282828 one-pixel surface/outline, 8 px corners, 4 px
padding, 6 px offset and minimum 260 px width; 40 px rows with 14 px horizontal
padding, Rajdhani SemiBold 16 px/#D5D6DC and rounded #1A1A1A hover/selection.
Hand cursor and disabled states remain. SettingsDropdownMenus.Enabled inherits
from both settings-shell roots, covering dynamically created extension controls.
This popup-only adaptation leaves closed fields, original selection bindings,
choices, icons/templates, keyboard navigation and native scrolling intact.
Only template chrome above the scrolling presenter is replaced; opening again
does not duplicate wrappers/styles. Dropdowns outside settings are unaffected.

Game Gallery General and Danger Zone use 40 px outer margins and a 760 px
left form. Expanded experience uses natural row height, exact Backup label
style/18 px gap, and the actual main-settings outlined dropdown style (190 x 38,
6 px corners, shared navigation hover/focus/popup, hand cursor on field and
options). Preserve Cinematic/Full screen tags and preference binding. Danger
Zone shares Exophase's polished natural rows, 17/15 px roles and 2/24/12 px gaps,
retaining original live controls, red actions and staged management safeguards.
Host-side presentation only; installed Game Gallery package remains unchanged.

Exophase Account hides the entire Profile option without modifying its saved
reference/binding. Connection actions own the single 24 px gap before
Synchronisation. Sync section/button use layout rounding and device-pixel
snapping plus a 2 px bottom gutter, retaining the canonical template and full
one-pixel border to prevent final-edge clipping at scaled DPI.

Exophase Synchronisation is on Account; General retains its two
preferences only. Move the original heading/hint/Update Database controls, not
copies, preserving the native click handler and CanSynchronize binding. Keep
24 px above the section, 2 px title/hint and 12 px hint/action spacing; no
remaining General trailing section gap. Repeated host preparation is idempotent.

Exophase Danger Zone uses shared 40 px margins/760 px form, natural switch
height with a 56 px text column, 24 px gaps between blocks, 2 px title/hint
gaps and 12 px before action buttons. Reuse actual Backup label/hint/toggle/
action styles; retain destructive colours and original live controls and
transactional confirmation/Save/Cancel/restart handling. Scope is Exophase only.

Exophase → General and Account use 40 px outer margins, a 760 px left form, natural
option rows with 24 px gaps and 2 px label/hint spacing. It reuses the actual
Backup label/hint/toggle/action styles rather than approximations. General is
first in navigation, Account second; both introductory titles/descriptions are
removed. Subsection headings use standard 17 px labels, hints 15 px; the sync
action follows its description by 12 px. A scoped host adapter reapplies the
secondary role after the host's normal/deferred 17 px minimum-font pass. Account
keeps authentication/profile bindings, canonical actions with 10 px separation,
and the shared 388 x 40 profile field with 18 px preceding gap. Other categories
and extensions remain unchanged. Only theme runtime DLLs are
deployed; installed Exophase is not upgraded or rebuilt for this layout change.

Game Edit → Actions adds a per-action Disabled (time only) choice for File/URL play
actions, using the existing styled Tracking Mode dropdown and native Save/Cancel
binding. Running detection/screenshots/play count/lifecycle remain active; only
elapsed playtime is suppressed. No control geometry or other choices change. See
[Action tracking](ACTION-TRACKING.md) for the Development-only runtime patch.

Notifications content page uses a flush shell without inner navigation/header.
Its user-requested Hydra-inspired toolbar now replaces both selectors with
underlined All/Unread/Archived tabs, page-background Mark all as read and
Cancel-red Clear all. Toolbar/card shapes share a 900 px cap and symmetric
70 px side margins; full-width zero-margin scroll host keeps the scrollbar flush.
Cards use one outline, no dual box/dot, 20 px padding and natural 112 px minimum
height. Unread is vivid with #0A0A0A background, one neutral step above read #090909.
Read icons desaturate bitmap assets at original resolution/DPI with alpha intact,
not a tile-sized screenshot overlay; vector outlines/corners/monochrome logos and
brush geometry stay live. Unread/unload restores original asset references.
Read text is grey, card opacity 0.55.
Unread cards have a slightly lighter/thicker full outline (#3A3A3A, 1.25 px);
read retains #252525, 1 px. Hover outline #454545. Unread also has a restored
white left-edge dent (#F0F0F0, 3 px, follows 8 px corners), hidden on read cards
and non-interactive. No glow/gradient; card centering remains.
Relative date sits below the preview without a clock, aligned with title/preview.
It shares preview's default 17 px font and #7A7A7A neutral grey, with 0.7 opacity.
This matches dimmed game-list title brightness without the #777A82 blue tint;
game-list colours themselves remain unchanged.
Update titles show source name only; preview reads "New version available for
update <version>." Normalize existing messages at display time, retaining IDs,
original bodies and archive state. Exophase uses its full-size rounded canonical
tile rather than a separately framed inset. Exact date remains in its tooltip.
Notification list and opened titles/descriptions match settings typography:
scoped DynamicResource FontFamily (Rajdhani), SemiBold, default 17 px title/body/date.
List cross reproduces user-supplied Lucide x.svg (24-unit canvas scaled to
19.2 px, 20% smaller, round 2-unit stroke):
always visible, vertically centered, neutral #7A7A7A and hover-only #C52929.
No red background; original CloseCommand remains.
Opened description body alone uses Overview's bundled Inter Medium via
OsirisFooterTypography.FontFamily, retaining 17 px size. Detail title/date and
list fonts stay unchanged.
Opened body uses non-inherited PreserveFontFace=True so page-open runtime
typography locking cannot replace its Inter family/Medium weight; all surrounding
text retains normal locking. Regression checks exercise repeated runtime passes
and verify the resolved glyph face is real bundled Inter Medium.
Right-anchored detail panel expands left from 520 to 780 px, capped to viewport width; #090909
background matches the app. Relative dates refresh each minute
while visible, without changing stored dates.
Read/archive/clear/detail behavior stays intact. This is not the Notifications
settings category, and the requested design exception does not change other pages.

Contacts & Links uses shared 40 px margins and a 760 px left-anchored form.
Default headings and secondary descriptions use 2/24 px gaps.
Descriptions explicitly align Left so their 720 px maximum width does not
center them within the form or indent them relative to the titles.
All five links share outlined cards (#090909 surface, 1 px #252525 border, 7 px corners),
64 px icons, 16/24 px padding and 24 px inter-card/section gaps. Existing link
destinations and canonical Copy/Open/Donate actions are preserved.

Backup uses shared 40 px margins, a 760 px left form and content-sized options
with 24 px gaps. Added Automatic Backup heading and requested description;
What to include uses default heading/secondary description sizes and 2 px gaps.
Backup folder reuses Storage's stretching outlined dropdown and 10 px Browse
gap, replacing the differently styled editable path bar. Browse stages the
single destination using the existing Save/Cancel setting. Rotating backups
offers integer values 1–10 instead of a slider. The retention dropdown is
narrowed to 90 px; canonical height/states and 18 px label gap are unchanged.
Create Backup Now replaces Help as the final left-aligned page action and is
removed from the Save/Cancel footer.
Scheduling, inclusion choices and health/progress behavior are retained.
The redundant Backup folder title above the selector is removed along with its
title-only spacing; the path row retains the standard inter-option gap.
Backup Status is widened from 480 to 520 px and its leading margin grows from
34 to 50 px. The path/Browse row is shortened to 700 px with full-path tooltip.
All compact status labels, reason/history/empty/progress text use the same hint
style as descriptions; larger values and icons retain their sizes. History
rows measure naturally with an 84 px minimum to accommodate larger text.
Backup count and folder size share the Backup History header, aligned inline
to its right. Removed the separate metric band/extra divider and increased the
history list from 226 to 340 px, preserving live values and list actions.

General's System tray icon dropdown shows only White/Black text, without icon
previews. Saved Bright/Dark values and the hidden Default compatibility alias
remain unchanged, as do the actual tray icons and minimize/close behavior.

General now uses Taskbar/System tray choices for Minimize Osiris to and calls
fullscreen startup Console View. The Launch Osiris when you start your computer
switch retains enable/disable control, with Closed to tray/Minimized choices
beside it (disabled while startup is off). These replace the last two separate
switches. Native settings and normal Save/Cancel are retained; explicit startup
selections set mutually exclusive flags. Existing legacy values are not eagerly
rewritten. All dropdowns reuse canonical controls and 18 px label gaps; the eight
options keep shared outer margins and 24 px content-sized spacing.

Performance uses shared 40 px margins, the left 760 px form and five naturally
measured options with 24 px gaps. Removed fixed toggle row buckets and extra
first-row inset. All five options use consistent 2 px title/description gaps;
cache/diagnostic actions retain 12 px internal top spacing. Options, commands, bindings,
quality control dimensions and collapsed profile content are unchanged.
Current cache status uses settings description typography, retaining its muted
colour and live size updates rather than the previous compact 12 px override.

Updating is labelled Updating Button. Its 760 px left form uses shared 40 px
margins, natural described options and 24 px gaps. Default-sized Update Button
jobs heading and secondary description precede the three job switches by 24 px.
Three staged switches select visible jobs: Integrated Libraries, Metadata and
Check for Updates. Developer mode and Restart Osiris remain permanent menu-only
actions. Blue Run all Jobs follows Check for Updates and is disabled with no jobs.
Left click opens the menu only; right-click opening is disabled. Menu items use
committed preferences after Save (Cancel restores). Selecting an item runs that
action; Run all Jobs runs only enabled jobs, sequentially from top to bottom:
library refresh, metadata, update checks. Developer mode itself remains session-only.
Startup/import metadata behavior is unchanged. Legacy combined/developer/restart
visibility fields are retained but ignored; individual job preferences survive.

Storage capacity bar and legend form one group: default 24 px above the bar,
12 px bar-to-legend gap and 48 px below the legend before Installed games.
The installed-games table is 440 px tall, with navigation-style 24 px icons next
to names, Library/Size columns and default 17 px text for headers and rows.

Storage is now a selectable-installation-path manager. Existing saved paths
migrate without deletion; the ordered path list is no longer limited to three.
Add/Remove stage the list on the window and use existing Save/
Cancel. Saving atomically updates osiris-storage-settings.json; first path is
the default quick destination for local-game installs when no destination is
already set. Neither removing a path nor changing default moves/deletes files.

The 760 px form contains a stretching canonical dropdown and Refresh/Add path/
Remove buttons filling the table width; no Set as default/text row. Drive capacity
is shown at the right of the Games/Other/Free legend row beneath a full-width bar.
The read-only table matches installation paths directly, including saved edition
records, and uses their saved InstallSize (not recursive filesystem scans).
Missing sizes show Unknown; shared installation folders count once. GB labels
use Windows-style binary divisors as requested. Other includes remaining drive
usage. Capacity reads stay asynchronous/cancellable. Network/mounted linked
paths may show capacity unavailable. Steam/Xbox retain external install control.
No uninstallation/move operations are introduced.

Notifications uses shared 40 px margins, 760 px form and content-sized options
with 24 px gaps/2 px title-to-hint spacing. Developer notifications control is
removed; pop-up and unread-dot behavior/bindings are unchanged.

Scripts uses shared 40 px margins, a 760 px left form, default Game scripts
title and secondary explanation. Only three game editors are shown; application
scripts remain saved but have no editor here. Test script uses the exact Save
action style, headers have a 50 px minimum, editors are 180 px, cards have 24 px
gaps. No scripts run during layout validation. Exclusion entry count is removed.

Exclusion List polish: shared 40 px margins; default-sized Excluded games title,
secondary explanation below it, count above-right, Remove selected below-left.
Page-local table text uses 17 px and columns 2*/1*/3* (Name/Library/Game ID).
Wrapped rows grow naturally with a 46 px minimum. Other tables keep their shared
styles; exclusion selection/removal and Save/Cancel remain unchanged.

Metadata now has a saved Update Metadata for Integrated Libraries games switch
below the startup switch. It defaults on to preserve automatic top-up behavior;
off skips supplemental source/custom-media requests after integrated imports.
Steam/Xbox return identity/runtime-only records; Osiris's chosen metadata sources
exclusively own descriptive fields and artwork. Existing saved game metadata
is not cleared on refresh. New games only are processed through
missing-only downloads, with standard collection imports queued/debounced and
custom Osiris sync processing its own batch without duplicate jobs. It is
independent of the manual Updating metadata action and startup whole-library job.
Descriptions wrap at the source-box edge (684 px from form origin); switch text
gets 628 px after its 56 px column. Category hints use 684 px max width and shared
2/24 px spacing, including Osiris Integrations.

Metadata contains independent date format selectors for Release Date, Added
Date and Last Played; the former Library Format navigation/page is removed.
Release Date retains its import/source controls, followed by a canonical Format
dropdown (984 px full row within the 1000 px form), aligned to import-limit
switches with the same 28 px source-to-option gap and vertically centred label.
The Osiris Integrations
section below Media has Added Date and Last Played with format selectors only,
not import switches or sources. Its description uses shared secondary typography
and 2/24 px spacing. Labels start at the left switch edge; each dropdown follows
its natural label width with the canonical 18 px gap. Existing formats, including custom legacy ones,
are restored per field. Changes stage until Save, follow Cancel/reopen behavior,
and retain restart notification checks. Stored date values and other date-format
settings are unchanged. Shared canonical controls and 24 px gaps remain in use.

Metadata now uses shared 40 px outer margins and content-sized field rows with
24 px gaps. General/Media headings use standard label typography with normal
secondary hints. The 1000 px form width is an explicit exception needed
for ordered source selectors and collection limits. Bulk default source priority
and Apply/Enable/Disable all controls are removed; per-field sources remain.
The new Update Metadata when Osiris starts switch is opt-in/default-off and
uses the existing missing-only update job after database and startup UI readiness.
Save/Cancel applies to this preference independently of the manual Updating action.
The Update Metadata manually guidance block is removed; manual Updating behaviour is unchanged.

Home Page's Show fun statistics toggle/description is removed; this option is
handled by Stats. No stored values or runtime Stats behaviour are changed.

Manager follows the user's dropdown-and-list mock-up: Platforms, Genres,
Developers, Publishers, Features, Tags, Age ratings, Editions and Sources are
available through the canonical Fields dropdown (width unchanged), followed by a
760 px-wide, 560 px-tall rounded list using Manual Carousel's surface treatment.
Shared 40 px page margins and 24 px control/list/action gaps apply. Editing
actions remain below the list, including Remove unused where supported.
List text uses the same canonical settings-label style as the rest of the form.
Categories, Companies, Series, Regions, Completion statuses and Filter presets
are removed from this UI, not from storage. Developers/Publishers use the
existing shared company catalogue and commands, preserving game assignments.
Editions offers Add, Rename and Remove unused only: direct deletion is unavailable,
and used editions remain protected by the catalogue save-layer reference check.
All named lists, multiselection, native
editing commands and Edition hooks are preserved. Optional platform artwork,
region specification and default-status side panes are removed from the UI;
existing stored values are not changed. This is an explicit exception to the
standard managed-table action-above-table arrangement.

Development presentation update, 2026-10-08:

- Settings remains the existing in-page settings host, not a popup or clone.
- The global Settings toolbar, including search, remains visible. Content starts
  below its existing 76-pixel toolbar area.
- The settings container has no outer inset, border, or rounded card outline.
- Category title/description headers, their separator line and reserved header rows are removed.
  Functional subsection headings and field hints remain.
- Navigation, Backup-derived form styles, bindings, and the
  Save/Cancel transaction are unchanged. The footer stays within the page.
- Settings navigation is 260 px wide with left-aligned labels and group
  chevrons directly beside their titles (18 px chevron column). Child items
  retain their navigation indentation. It reserves the bottom 86 px for the footer. The footer divider
  extends across navigation and content; the restart notice sits immediately
  before Save, after any page-specific action such as Create Backup Now.
- Navigation content has an additional 12 px leading inset, leaving selection
  backgrounds unchanged; group chevrons use a -2 px optical vertical correction.

This explicit user-requested shell exception changes only the outer settings
geometry; Backup remains the canonical reference for settings controls and forms.

The same shell now applies to Game Edit (opened from Game Details) and all
installed-extension settings, including management-only disabled extensions.
Their category header bands are hidden, while functional headings, hints and
controls remain. Game Edit's edition selector is preserved on the left of the
footer, with page-specific actions and Save/Cancel on the right. Game Edit
navigation scrolls independently above the footer. Extension restart guidance
is placed beside Save. Browse and installed catalogue pages retain their prior
geometry; shared runtime chrome restores it when leaving extension settings.

Spacing rollout now covers Settings -> General, Home Page, Navigation Panel and Game Details: they use the shared
`OsirisSettingsContentMargin` Thickness (`40,40,40,40`) with a single outer
spacing owner. Control dimensions and padding remain unchanged. Other pages
retain their current margins pending explicit migration. DESIGN-AGENT.md records
this contract and the staged rollout scope.
General, Home, Navigation Panel and Game Details now size every option to its actual control/text layout, not
fixed 54/68/74 px row buckets. A shared OsirisSettingsOptionGap (24 px) separates
content-sized options regardless of control type or description length. Labels
and descriptions retain their 2 px internal gap; wrapping grows the option
instead of consuming the following option's spacing. General's final option
has no trailing gap; hidden conditional options collapse with their own spacing.
Navigation Panel's width slider and px value share a vertically centred Grid
row, with the canonical 420 px slider span and 18 px value gap. Its four switches
use natural content heights; global animation-disable conditions are preserved.
Game Details' two hero sliders use the same slider/value alignment. Its field
list retains three columns, now within a 704 px span after its 56 px leading
indent, fitting the 760 px form. All switches use the canonical 56 px column;
field rows size naturally with 24 px gaps rather than fixed 42 px buckets.
Home no longer adds an extra gap before Banner source or exposes
Birthday editing; existing birthday data and greetings are preserved for Profile.
Canonical outlined dropdowns use a hand cursor, white field/option text, and
the exact navigation hover brush as their hover background without hiding their
outline. This shared style also updates its existing consumers on other pages.

Home Page's Manual Carousel replaces Single Hero, migrating its existing image
as the first manual entry. The always-visible hero box reuses Game Edit's
Screenshots Gallery box and hover red-cross treatment, with 226 x 128 hero
tiles and a plus tile for multi-image import. Library Carousel makes this
gallery grayscale/disabled. Both modes use the existing 30-second rotation.
The Manual Carousel outer box has a 7 px corner radius.
Edits are staged until Save; Cancel restores the list. Save copies static
images into Settings/Osiris/Home/ManualHeroes with content-addressed names and
profile-relative paths; originals are never modified. Removing an entry removes
it from the carousel while retaining inactive profile copies for recovery.

Editable theme source currently lives in
`Development/Contents and Stuff/Development (AI)/Development/ThemeSource/Views/MainWindow.xaml`
within the workspace. Deployment is restricted to
`Programming/Development/Osiris/App/Themes/Desktop/Default/Views/MainWindow.xaml`.

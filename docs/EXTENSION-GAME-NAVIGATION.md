# Extension pages and Game Details navigation

## Extension subpages (Osiris Beta 0.0.53)

The search-panel Back action now lets the currently visible installed extension
handle a nested page before consuming host history. A global page can expose
`public bool OsirisTryNavigateBack()` on its UserControl. Return true only when
the page handled a local Back action; false allows the normal host history path.
The host probes only visible controls while the current entry has an extension
identity. Hidden/unloaded views are excluded. Missing callbacks, false results,
and exceptions fall through safely; no other navigation changes.

Fun Games0.2.1 uses this to discard an inline builder draft before returning from
Roulette to the Fun Games hub. At the hub it returns false. There is no duplicate
Back to games button. Removing the nested content triggers its existing animation
cancellation/image cleanup. Saved roulettes are not modified by navigation.

Host implementation remains in legacy ThemeSource/Code/OsirisSidebarBehavior.cs;
build and deploy all three theme DLLs to Development with verified backups.
Extension implementation is authoritative in Extensions/extensions/FunGames.
UpdateChecksValidation adds six contract checks (handled/local state, ordinary
history fallback, unsupported/null page, error fallback, and invisible-page
exclusion); existing navigation and all other regression checks remain active.
No public release or personal installation changes accompany this work.

Released in Osiris Beta 0.0.52 with Exophase 0.2.8 and HowLongToBeat 1.2.5.

Game-title links in Exophase and HowLongToBeat follow Home/Library's native
Library -> Details -> SelectGameBeforeRender sequence. Details activation can
clear its two-way selection binding; restoring selection immediately and on the
stable owning window's DataBind dispatcher pass prevents a correct heading with
empty game content. The bridge's current-game guard prevents that queued restore
from overriding another game or a page the user has already navigated to.

The host's Back history must also restore native extension content, not only its
header and fixed-page visibility. Installed global-page entries now retain the
native sidebar activation command that originally opened the extension. Their
history clones preserve it. Restoring an entry executes that native command
before applying fixed-page visibility and header state; it does not execute the
outer history-recording command or add a new history entry. Native Opened/Closed
lifecycle and selected-sidebar state therefore remain owned by the application.
Fallback-only extension pages do not have a native command and keep their
existing fixed-page behavior. No extension account, sync, or settings behavior
is changed by either fix.

Until theme-source consolidation, the editable host change is in
`Development/Contents and Stuff/Development (AI)/Development/ThemeSource/Code/OsirisSidebarBehavior.cs`.
Build `OsirisTheme.csproj` with `EmbedRajdhaniFonts=true` and deploy all three
theme DLL copies in Development (App root, Default theme root, CustomControls).
No XAML change is required. Preserve Development Data and restart only through
the root `Osiris.exe` wrapper after confirming it is the sole active installation.

The authoritative Extensions repository contains both title-link fixes and the
shared `validation/ExtensionGameNavigation` regression fixture. Its 30 checks
invoke real extension handlers, the actual global-page opening command, and the
theme bridge with an isolated in-memory WPF view model. Tests assert selected
Details content and restored extension content, immediate Back before queued
bindings, repeated round trips, Back history, rapid game changes and stale-restore
cancellation. Old DLL/theme baseline modes reproduce the two separate failures.
No fixture window is shown or live database/network used. The user supplies
visual acceptance in Development; personal installation and releases are unchanged.

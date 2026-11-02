# Per-action tracking opt-out

## Independent executable and playtime (Beta 0.0.55)

File/URL play actions now expose Launching path, Tracking path and Track playtime.
The launch path/arguments still start Steam, a launcher or the game as before.
Tracking path browses for the actual game `.exe`; blank retains automatic
detection. Track playtime controls only elapsed-time persistence. New selections
are stored per action/edition using existing GameAction fields, so native editor
Save/Cancel and JSON cloning remain transactional. No live game/action is migrated.

The adapter keeps legacy enum entries internally but hides the visible tracking
mode selector in favour of a canonical action-editor checkbox. Legacy modes0–5
retain their saved values until edited. Reserved6/7 mean exact executable with
time enabled/disabled; the runtime patch rejects SDK collisions. Normalization
validates the target exists before launching and edits only the expanded clone,
never the saved SourceAction. Both flags use the exact full executable path via
QueryFullProcessImageName (including x64 games from the x86 host), not merely a
filename or parent/launcher process tree. The game emits one Started event when
the target appears and one Stopped event after exit, with a two-second exit
debounce. Startup waits at most three minutes before the native failure path.
Native lifecycle continues to drive screenshots, play count and last played;
the existing stop-time suppression additionally recognizes7, but not6.

Original code: `src/Osiris.LaunchLifecycle`; guarded core patcher:
`tools/SteamLaunchHotfix`. The historical SteamSession type/tool names remain
internal identifiers only: this feature is explicitly selected by tracking path,
works for non-Steam launchers too, and does not auto-detect Steam actions.
Deploy matched Playnite.dll, Osiris.LaunchLifecycle.dll and all three theme DLL
copies together after verified backups; never apply the theme alone. This was
introduced in Development on 1 November and is included in Beta0.0.55. Earlier
experimental Screenshots Gallery changes remain unapplied and are not needed:
the existing collector works through the repaired native lifecycle.

Validation passed 46 action/lifecycle/UI checks, including a short-lived launcher
spawning the selected executable and screenshot collection with playtime both
enabled and disabled. Existing theme checks passed (687 host, 65 Details, 72
Collection, 27 responsive). Development was deployed with checksum backups on
1 November. Its first restart exposed an unrelated existing cross-thread error
in startup library refresh; the affected method's IL matches the prior backup.
Development was reopened through its root wrapper with `--nolibupdate` for that
launch only, with its existing Data path and no new startup errors. The separate
refresh error was fixed for Beta0.0.55 by moving MainWindow access onto the
application dispatcher. A background-event regression passes, and Development
now completes normal Steam/Xbox import and install-size scanning without the
workaround or startup errors. No saved refresh setting was changed.

## Earlier time-only implementation (Beta 0.0.52)

Released in Osiris Beta 0.0.52. The deployment procedure below also documents
the original Development implementation and its safety boundaries.

Game Edit → Actions → Tracking Mode now includes **Disabled (time only)** for File/URL play
actions. Set it on the Steam launch action while leaving the direct executable
action on Default/Process/Directory/Process name as appropriate. Save/Cancel and
edition action copying remain native; no existing action or game is rewritten.

The inherited runtime does not detect Steam command arguments as an automatic
tracking opt-out. Default File tracking follows the launched process; URL
tracking watches the configured installation directory. A launcher handoff can
leave these monitors watching the wrong process/path. That specific REDlauncher
handoff was not reproduced against the user's game, and is not repaired here.

Steam and Osiris counters are separate. Running both does not by itself add
Steam's hours to Osiris; duplication depends on later import/synchronization.
Disabled does not import Steam hours or change Steam achievements. It simply
prevents Osiris from adding elapsed playtime for the selected action. Running
game detection, screenshots, play count, last played and lifecycle events remain.

## Compatibility and implementation

- UI adapter `ThemeSource/Code/ActionTrackingOptions.cs` adds a native
  `SelectorBehaviors.EnumSelector` entry to GameTaskView's ComboTrackingMode.
  Existing canonical combo styling/bindings are retained. It restores saved
  selection and avoids duplicate entries; other action choices are untouched.
- The SDK enum is **not modified**: inherited values 0–4 (including ProcessName)
  remain unchanged. Value 5 is reserved by this Osiris runtime patch. JSON
  save/reload and native launch cloning preserve it. Emulator/script editors do
  not expose this unsupported choice.
- `tools/ActionTrackingHotfix` checks value 5 is unused, validates the inherited
  GenericPlayController File/URL launch-to-tracking boundary, then stages a
  patched Playnite.dll outside the installation. Both File and URL branches
  reach the guard after the normal launch operation succeeds.
- Disabled preserves the real Started/Stopped lifecycle and process monitoring.
  Only the transient launch clone's monitor mode changes: Directory when the
  configured installation directory exists, otherwise Default. This helps follow
  Steam/REDlauncher handoffs to the actual installation, but requires the correct
  game installation directory (not the Steam client folder). Native directory
  tracking still has its existing limits, such as lingering launcher processes.
- The original saved SourceAction retains value 5. At real stop handling,
  GamesEditor suppresses only that action's SessionLength before playtime is added
  and before extension stop callbacks run. Existing time is never reset. Start
  callbacks, game activity/play count, screenshots and other non-time lifecycle
  processes remain intact; post-game cleanup occurs on actual detected exit.
  Other modes and integrated-library controllers are not suppressed.
- The patch is fail-closed on an unknown SDK/controller layout, and idempotent
  on its own output. It does not change branding/updater/uninstall modifications.
  Do not overwrite the entire inherited runtime with an unpatched stock build.

## Build and verification

From the workspace root, stage and verify the core patch:

```powershell
dotnet run --project GitHub/Osiris_Launcher/tools/ActionTrackingHotfix -- --patch Programming/Development/Osiris/App Programming/Sandbox/ActionTrackingHotfix/Playnite.dll
dotnet run --project GitHub/Osiris_Launcher/tools/ActionTrackingHotfix -- --verify Programming/Development/Osiris/App Programming/Sandbox/ActionTrackingHotfix/Playnite.dll
```

Build OsirisTheme with the existing embedded-font build and run its validation
suite. Build `tools/ActionTrackingHotfix/Validation/Validation.csproj`; run the
validator with absolute arguments: Development App directory, staged Playnite.dll,
freshly built OsirisTheme.dll, and the built
`Validation/Probe/bin/Release/net462/Probe.exe`. Build the Probe project first.
The validator uses in-memory game/action objects and a harmless windowless
two-second probe process; it never opens a live library or launches a user's
game. It verifies real process/directory Started/Stopped lifecycles, positive
elapsed time suppressed only for mode 5, normal elapsed time retained, play count
and last-played handling intact, and dropdown/save/reload compatibility.

Deploy only to Development with recoverable, checksum-verified backups of
Playnite.dll and each of the three existing OsirisTheme.dll copies. Preserve Data.
If personal Osiris is active, do not send the shared shutdown IPC or launch the
Development wrapper: ask the user to close personal first. Launch Development
only through its root Osiris.exe wrapper, verify its Data command line, and scan
startup logs. No personal deployment or public release is authorized by this task.

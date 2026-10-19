# Per-action tracking opt-out

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

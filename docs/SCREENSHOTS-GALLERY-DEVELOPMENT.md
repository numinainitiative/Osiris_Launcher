# Screenshots Gallery editor folder access (Beta 0.0.51)

Game Edit -> Extensions -> Screenshots Gallery has an **Open folder** action
beside the screenshot count. It remains available with zero screenshots and
when the per-game gallery switch is off, so it does not depend on the Game
Details card being visible.

The action uses the current game's SDK file-storage path plus `Screenshots`
(`Data/library/files/<game GUID>/Screenshots`), matching Screenshots Gallery's
collector and card. If missing, this directory is created only when the user
presses the button; opening the editor alone does not create it. Windows opens
the directory through its shell. No screenshots, Windows Pictures originals,
pending deletions or gallery settings are changed by this action. Game Edit's
existing Save/Cancel transaction remains unchanged.

The action reuses the editor's exact Backup-derived
`OsirisSettingsActionButtonStyle`, including its normal, hover and disabled
states. It is disabled until a valid game has been loaded.

Editable theme source currently lives in
`Development/Contents and Stuff/Development (AI)/Development/ThemeSource`:

- `Code/OsirisScreenshotsGameEditor.cs`: current-game folder resolution and opening.
- `Views/MainWindow.xaml`: editor action, alongside its count.
- `Code/Validation/ScreenshotsFolderValidation.csproj`: synthetic folder, style,
  empty-state, switching and Save/Cancel checks without Explorer or live-profile writes.

Build `Code/OsirisTheme.csproj` with `EmbedRajdhaniFonts=true`. Deploy its assembly
to all three Development locations (`App/OsirisTheme.dll`, the Default theme
root and its `CustomControls` folder), plus the changed MainWindow view. No
Screenshots Gallery extension package rebuild is needed for this editor-host
change. Preserve Development Data and restart through the root `Osiris.exe`
wrapper. Beta 0.0.51 packages this host change; personal installations should
receive it through the normal Osiris updater, not by copying Development files.

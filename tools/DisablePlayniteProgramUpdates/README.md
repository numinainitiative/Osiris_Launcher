# Disable inherited Playnite program updates

This interim build tool patches `Playnite.Updater.IsUpdateAvailable` to return
`false` without downloading Playnite's update manifest. It disables both the
automatic and manual **program** update paths while preserving the separate
add-on update and add-on blacklist checks.

The tool deliberately validates the expected Playnite 10.56 method shape before
writing a new assembly. Input and output must be different files so the working
runtime cannot be truncated if a build fails.

```powershell
dotnet run --project .\tools\DisablePlayniteProgramUpdates -- `
  C:\path\to\original\Playnite.dll `
  C:\path\to\patched\Playnite.dll

dotnet run --project .\tools\DisablePlayniteProgramUpdates -- `
  --verify C:\path\to\patched\Playnite.dll
```

This patch is a release-safety measure until the editable Osiris core and the
Numina Initiative updater are consolidated in this repository. Do not redirect
the inherited updater to an Osiris package: its installer and manifest contract
are Playnite-specific.

# Beta 0.0.52 local font-stability hotfix

This is a local acceptance hotfix; it does not replace published GitHub assets
or advance the public version. The personal installation is explicitly in scope
for this user-requested repair. Saved font size/family/weight must not be reset.

## Cause and correction

- Font selection was applying Rajdhani to Overview HTML even when FontFamily
  was null (no explicit choice), replacing its established Inter face. Cache
  each HTML view's configured family; apply a global family only for an explicit
  choice and restore that configured family when the first preview is cancelled.
- The footer's Inter face was also overwritten. Its controls, version text and
  template descendants now preserve their original face until an explicit global
  font choice. Explicit family and weight choices still work throughout the app.
- The edition selector's nested ToggleButton could pick up an implicit button
  font instead of the ComboBox typography. Bind family/size/weight to the owner,
  do not scale those projections again, and let header title and selector use
  the same scalable 17 px base role.
- FontSize bindings and compiled TemplateBinding presenters already receive
  their owner's scaled size. Do not apply the offset a second time. Keep ordinary
  inherited sizes and existing fixed settings scopes intact.

The editable files remain in legacy ThemeSource pending consolidation:
`Code/OsirisSidebarBehavior.cs`, `Code/ApplicationFontFamily.cs`,
`Views/MainWindow.xaml`, `Views/TopPanel.xaml` and
`Code/Validation/UpdateChecksValidation.cs`.

## Verification and installation

The regression suite passes 628 checks, including personal/default base 15 and
Development base 17, 10/12/24 px previews, repeated passes, bindings/templates,
HTML, first-choice cancellation and explicit family overrides. The released DLL
reproduces the missing legacy-footer face in the same test. Details lifecycle,
32 library identity/layout checks and file-backed HTML checks also pass.

Use the normal release builder into a dedicated private artifact directory,
never overwrite the public 0.0.52 artifacts. `Test-ReleaseCandidate.ps1` accepts
`-CandidateDirectory` for this purpose while keeping previous public archives
as upgrade fixtures. Verify privacy boundaries, checksums and sandbox startups.

Apply the qualified same-version hotfix through the existing updater's --apply
transaction with a fresh staging directory beneath that installation's
Data/Runtime/Updates. This intentionally bypasses only the newer-version discovery
step; the normal package validation, App swap, recovery backup and rollback remain.
Launch only through the root wrapper, verify the intended Data path/sole engine,
scan startup errors and verify the saved typography settings hash is unchanged.

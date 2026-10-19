# Local-game read-only uninstall hotfix

The confirmed local-game uninstall previously used `Directory.Delete(path, true)`.
A read-only game file (for example Underground 2's `server.dll`) made this fail.

`src/Osiris.UninstallSafety/InstallFolderDeletion.cs` handles this operation. It
preflights the tree, refuses drive roots and reparse points, clears only the
ReadOnly attribute, and deletes files followed by deepest directories first.
ACLs and running-process locks are not bypassed. Existing folder validation,
confirmation, library record retention, and installed-state changes after success
remain in the theme. Failure can still leave a partially removed installation.
No game files are removed while deploying or testing the hotfix.

The legacy theme source includes the helper directly for future builds. For
different currently installed builds, `tools/UninstallHotfix` changes only the
Directory.Delete call in LocalLibraryActionButton.ExecuteLocalUninstall to call
the helper assembly, preserving each build's other methods and resources. These
patched builds require `App/OsirisUninstallSafety.dll`. Stage patches before
deployment, verify recovery hashes, and restart installations separately through
their root wrappers. Restore each original theme DLL to roll back; the unused
helper DLL can remain. Do not deploy a different installation's theme wholesale.

Validation: `dotnet run --project tools/UninstallHotfix/Validation/Validation.csproj
-c Release` uses only uniquely named temporary fixture directories, testing
read-only files, root rejection, non-recursive rejection, and locked files.

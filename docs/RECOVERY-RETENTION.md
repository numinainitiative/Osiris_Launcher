# Recovery retention

Implemented on 4 November 2026 and initially deployed to Development only.
Included in Beta 0.0.58; the already published Beta 0.0.57 assets are unchanged.

After an update is successfully installed, the updater retains its just-created
restore copy plus the newest other complete copy in `Data/Recovery/Updates`.
Ordering uses the UTC timestamp in the backup name, not the version number or
directory modification time. Complete copies have `App` and `Root/version.json`.
Older recognized update snapshots, including previous failed-update remnants,
are removed only after success. Failed installs roll back before retention runs,
preserving existing restore points. Cleanup errors are logged and never cause a
completed update to roll back.

Deletion is restricted to recognized updater directories under that exact
installation's `Data/Recovery/Updates`, with stored root version metadata.
Junctions/reparse points in the parent chain or snapshots are refused. Unknown
directories, ManualFixes, ExtensionUpdates, current library, screenshots,
extension data and settings are preserved. This does not prune user-created
backups or runtime downloads.

Updater integration tests cover successful upgrade/two-copy retention, timestamp
ordering, removal of old failed snapshots, failed-install rollback, preservation
of existing Data and unrelated recovery files, and refusal to follow a junction
into an external directory. Shell icon validation also passes.

The requested one-time cleanup used the same tested implementation on the
personal and Development installations. Personal update snapshots went from 28
to 2 (18,071,014,002 bytes reclaimed); Development went from 4 to 2
(796,110,601 bytes reclaimed). Personal program files were not patched.
Development's previous updater was checksum-backed up under
`Data/Recovery/ManualFixes/updater-retention-20261104-131426` and replaced with
SHA-256 `21a2a17eb28a1ecd9bd2e69bb76ecbec003886ab02e6f6ea77888472af761d95`.
Future release builds compile this updater from source.

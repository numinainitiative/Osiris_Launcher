# Update Extensions Database

The Update menu offers **Update Extensions Database**, directly below Update
Metadata. Settings → Updating Button controls its visibility (on by default).
Run all Jobs awaits it after metadata and before release checks when enabled.
It does not install extension packages, clear caches or redownload game metadata.

## Audited providers

Only loaded/enabled extension instances are used; DLLs are not instantiated or
loaded from disk by the runner. Providers run sequentially in this order:

1. Exophase: existing `SynchronizeAsync(settings, progress, cancellationToken)`
   updates stored cross-platform activity through the configured profile/session.
   It preserves the extension's existing matching and playtime semantics.
2. HowLongToBeat: existing `UpdateStoredDatabaseAsync(progress, token)` refreshes
   previously fetched/manual matches only, retains game IDs and records the
   last-update timestamp through the extension's normal settings persistence.
   Unfetched games are left untouched. Partial failures are reported.
3. Trophies: existing `SyncGame(gameId, token)` checks all library game IDs,
   skipping games with no saved catalogue. Both local Exophase-backed and
   integrated Steam/Xbox catalogues use their native updater. It does not create,
   export, rematch or replace a user's catalogue selection. Disabled Trophies
   preferences are respected. Each operation has the native library-sync's
   45-second timeout; failures do not prevent later catalogues from being checked.

Stats, Game Gallery, Screenshots Gallery and metadata providers do not expose
an equivalent whole-database refresh action in the audited source. Their local
session/media caches are not treated as remote databases, cleared or rebuilt by
this job. Steam/Xbox library imports and game metadata remain separate jobs.
Existing integrated-library trophy synchronization remains unchanged.

Future loaded extensions can explicitly opt in with an instance method returning
`Task`: `UpdateDatabaseForOsiris(IProgress<string>, CancellationToken)`. The runner
does not guess method names, invoke arbitrary refresh commands or click UI buttons.

## Execution and safety

The job shares the Update button's cancellable footer process. Its own gate
prevents duplicate batches; Exophase/HowLongToBeat busy flags prevent overlapping
native database actions and are restored in `finally`. Trophies retains its
native synchronization semaphore. Old queued progress reports are ignored after
a provider finishes. Cancellation is passed into actual provider operations;
the runner awaits their termination rather than abandoning background writes.

An individual provider failure does not stop the remaining providers. The footer
and update-action trace report updated, skipped and attention-needed providers;
credential-bearing exception messages are not exposed. Missing/unsupported
installed updater APIs are reported, not silently declared successful. Account
connections and saved matches/catalogues must already be configured.

## Development verification

Theme host implementation: `ExtensionDatabaseUpdates.cs` and `OsirisUpdateActions`
in the working ThemeSource tree. No installed extension assembly is replaced.
Validation covers actual installed backend signatures (Exophase 0.2.6,
HowLongToBeat 1.2.4, Trophies 0.1.0), fake updater dispatch, ordering,
deduplication, partial failures, busy guards, cancellation, future opt-in and all
16 menu-visibility combinations. It does not contact accounts or mutate the
Development game library during tests. Live provider responses require a manual
run with configured accounts. Deploy only to Development, retaining Data and
checksum-verified recovery copies; the personal installation remains out of scope.

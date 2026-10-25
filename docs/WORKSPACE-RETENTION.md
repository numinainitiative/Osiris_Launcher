# Local workspace retention

The user requested retaining the last three releases on 25 October 2026.
This is local disk housekeeping, not deletion of GitHub Releases or Git history.

Keep the three most recent application ZIPs and checksum manifests, plus one
recent matching clean-install and upgrade qualification pair for each release.
Expanded staging trees, older qualification installations, duplicate public
downloads and obsolete generated release-validation directories are disposable.
Build fresh staging when packaging a release; do not use an old staging tree as
editable source. Keep extension package fixtures required by qualification tools,
including the historical Stats package used to verify upgrade preservation.

Never include authoritative source repositories, registered Git worktrees,
legacy editable source, graphics, manual backup archives, extension recovery
archives, or the persistent Development Data profile in automatic cleanup.
The personal installation is outside this cleanup scope.

The initial cleanup removed 105 generated targets totaling 79,873,197,964 bytes
(74.388 GiB). It retained Beta 0.0.51, 0.0.52 and 0.0.53; verified each retained
ZIP against its checksum manifest; preserved the registered historical release
worktree; and confirmed Development Data retained 9,143 files / 8,190,943,560
bytes. Old disposable test copies were permanently removed, not recycled;
fresh fixtures can be generated again from the retained release packages.

The local guarded helper is `Programming/Temp/Cleanup-OsirisGenerated.ps1`
relative to the workspace root. It defaults to a read-only plan and requires
`-Delete` to remove validated targets. It retains the three newest root-level
release ZIPs by modification time, validates their manifests, rejects unknown
sandbox directories, Git markers, links and running target processes, and
restricts deletion to individual children of the three explicit output roots.
Inspect its plan before use, especially if archives were copied with new dates.
The October audit and result are under
`Programming/Temp/WorkspaceCleanup-20261025-115851/`; these reports are local,
not a public profile export. No recurring cleanup was scheduled.

# Release Checklist

Use this checklist before publishing an Osiris release.

1. Update `version.json`.
2. Build the release package:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\build\New-OsirisRelease.ps1
   ```

3. Confirm the ZIP and JSON manifest were created under `artifacts/`.
4. Verify the ZIP checksum matches the manifest.
5. Test a clean install from the ZIP.
6. Test updating from the previous release and confirm the `Data` folder survives.
7. Commit the exact source, scripts, and version metadata used for the build.
8. Create a GitHub draft release.
9. Attach both release assets:
   - `Osiris-<version>-win-x64.zip`
   - `Osiris-<version>-win-x64.json`
10. Publish the draft after final inspection.

## Versioning

Osiris versions use:

```text
<Stage>_<Major>.<Minor>.<Patch>
```

Examples:

- `Beta_1.0.32`
- `Beta_1.0.33`
- `Stable_1.0.0`

Alpha and beta releases should be marked as GitHub prereleases. Stable releases
should not.

## Release Rules

- Never replace files attached to an already published version.
- Publish a new version for every correction.
- Release packages must not contain a user's populated `Data` folder.
- Keep third-party license notices included with every package.

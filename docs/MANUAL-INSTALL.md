# Manual installation from GitHub

## Fresh installation

1. Sign in to GitHub with an account that can access the private repository.
2. Open `https://github.com/numinainitiative/Osiris_Launcher/releases`.
3. Open the required release and download both of these release assets:
   - `Osiris-<version>-win-x64.zip`
   - `Osiris-<version>-win-x64.json`
4. Do not use GitHub's automatically generated **Source code (zip)** or
   **Source code (tar.gz)** downloads. They contain repository source, not the
   compiled application.
5. Optionally verify the package in PowerShell, substituting the downloaded
   paths:

   ```powershell
   $manifest = Get-Content .\Osiris-<version>-win-x64.json -Raw | ConvertFrom-Json
   (Get-FileHash .\Osiris-<version>-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant() -eq $manifest.sha256
   ```

   The result must be `True`.
6. Create a new folder, such as `C:\Games\Osiris`, and extract the release ZIP
   into it. Do not extract it over a development or personal installation.
7. Run `Osiris.exe`. The first launch creates a new writable `Data` profile
   inside that installation folder.

## Private prerelease limitation

GitHub requires authentication to view or download assets from a private
repository. A normal Osiris installation does not contain a GitHub credential,
so it cannot automatically discover private releases. While the repository is
private, download prereleases manually through a signed-in browser.

After the repository is public, the same GitHub Releases channel can be checked
directly by installed Osiris clients for update notifications. Release packages
remain checksum-validated, and automatic updates preserve the installation's
`Data` directory and retain a rollback backup.

## Windows warning

The prerelease is not code-signed yet. Windows may display a SmartScreen warning
until production code signing is implemented. Only run packages downloaded from
the official Numina Initiative repository whose SHA-256 matches the accompanying
manifest.

# Branding Assets

Osiris keeps repository-owned branding sources in `media/branding`.

These files are build and release inputs, not marketing leftovers:

- `osiris.ico`: Windows icon embedded by the launcher and updater projects.
- `osiris-logo-source.jpg`: original square Osiris mark used to regenerate shared logo surfaces and icon frames.
- `osiris-logo.png`: 512 px PNG generated from the source logo for legacy theme image paths.
- `osiris-loading-logo.png`: separate transparent logo reserved for the startup/loading screen.
- `osiris-missing-cover.png`: default placeholder cover used when a game has no cover artwork.

Run `tools/Branding/New-BrandingAssets.ps1` after replacing the source logo. The script only performs format and size conversion: it regenerates `osiris-logo.png` and `osiris.ico` while leaving `osiris-loading-logo.png` unchanged.

Do not commit personal library artwork, screenshots, generated prompt notes, temporary design exports, or runtime `Data` files to this repository.

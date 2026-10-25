# Executable, taskbar and tray branding

Osiris Beta 0.0.54 uses the accepted `media/branding/osiris.ico` for all three
shell surfaces. Its SHA-256 is
`32264a091d2f781b8ad73453c22bf6c1952a4762b40b84e03cfc401e75c1ece4`.
The existing Branding tool verifies all nine icon frames against the launcher
and updater native icon resources. The release builder also updates and verifies
all native icon groups/languages on both desktop and fullscreen engines.

The inherited main window's Icon loads the theme `Images/applogo.ico`. The tray
loads the theme's `TrayIcon`, `TrayIconWhite` or `TrayIconBlack` BitmapImage path
and creates its native Icon from that file. All three aliases (`applogo.ico`,
`applogo_white.ico`, `applogo_black.ico`) now contain the exact executable icon
bytes, not separately drawn/rasterized approximations. Saved tray colour values
are preserved for compatibility but no longer select different artwork. Tray
activation, menus, notifications, startup and minimize/close behavior are unchanged.

Release staging copies the authoritative icon to the aliases and checks hashes;
qualification checks the extracted package aliases too. No browser state, profile
or settings file is bundled. Development's five original program/icon files are
kept in its local Recovery/ManualFixes before deployment. Visual acceptance is
provided by the user; validation does not move the pointer or take screenshots.

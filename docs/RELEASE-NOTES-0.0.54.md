# Osiris Beta 0.0.54

- Taskbar and system-tray icons now use the same multi-resolution artwork as
  Osiris.exe. Both saved tray colour aliases resolve to this shared icon.
- Release checks verify theme icon aliases and both engine executable resources
  against the accepted Osiris icon. Tray actions and minimize behaviour remain.
- Storage excludes stale uninstalled entries with no actual installation folder,
  including phantom zero-byte entries, while retaining real edition installs.
- Collection covers now use Library's hover zoom, respecting disabled animations.
- Game Details edit mode gives every card its own outline, including the right
  column, and only allows up/down movement within its original column.
- Game Edit now shows Version in General and Completion Status in Advanced.

Existing library, profiles and extension data are preserved. Extension packages
are unchanged; Fun Games 0.3.2 remains current. Inherited updates remain disabled.

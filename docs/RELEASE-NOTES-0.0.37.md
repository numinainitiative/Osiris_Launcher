# Osiris Beta 0.0.37

This release improves application updates and corrects window sizing across
different display scales and screen sizes.

- Shows Osiris update preparation and restart progress in the bottom activity
  panel, consistently with library and metadata operations.
- Makes the manual `Check for Updates` action check Osiris itself as well as
  installed extensions.
- Treats the minimum window width as 1700 physical pixels instead of 1700 WPF
  units, preventing 125% display scaling from turning the limit into 2125
  pixels.
- Caps the minimum width to the monitor's usable work area on narrower screens
  so the application is not forced beyond the right edge.
- Recalculates the limit after DPI, monitor, display, taskbar, and window-state
  changes.

Existing games, settings, extensions, and other files in `Data` are preserved
during the update.

# Osiris Beta 0.0.41

This release adds the Osiris host experience for cross-platform activity from
the new Exophase extension.

- Adds Exophase to Installed Extras, extension settings, Game Edit, and the
  Game Details extension-card stack.
- Supports a display-only Total Time Played value that combines native Osiris
  tracking with time from other platforms without overwriting the native game
  field or counting an integrated library twice.
- Reworks the Time Played editor into separate Hours and Minutes fields, with
  fully visible minute values and a 9999-hour maximum.
- Improves editable platform selectors so manually entered platform names stay
  visible and can be selected again.
- Gives extension activity cards the standard collapsed height and
  `view more` / `view less` behavior when their platform lists are long.
- Simplifies the HowLongToBeat and Exophase card headers while preserving their
  established content layouts.
- Preserves existing games, settings, extensions, and all other files in `Data`
  during the update.

# Osiris Beta 0.0.43

This patch adds the host interface required by Exophase 0.2.2 to combine
multiple releases of the same game.

- Separates Exophase search results from the editions already linked to a
  game.
- Adds a removable Selected editions list in Game Edit.
- Lets users add Game of the Year, Complete, remastered, regional, and other
  Exophase editions to one Osiris game.
- Rebuilds the combined platform-time preview whenever an edition is added or
  removed, without losing manual rows or time corrections.
- Returns to automatic title matching when the last explicit edition is
  removed.
- Preserves existing games, play time, settings, extensions, account sessions,
  and every other file in `Data` during the update.

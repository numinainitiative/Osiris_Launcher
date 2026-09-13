# Osiris Beta 0.0.42

This patch completes the host experience for Exophase 0.2.1 and makes
cross-platform activity easier to associate with differently named games.

- Adds an Exophase search and explicit match selector to Game Edit. It searches
  the last synchronized Exophase library, previews the selected platform times,
  and preserves manually entered activity.
- Handles common edition, remaster, and subtitle differences when searching
  for a corresponding Exophase title.
- Hides the Exophase Game Details card when no synchronized or manually added
  platform activity is available; native Osiris time alone no longer produces
  an empty card.
- Uses the canonical rounded Exophase artwork in the public extension browser.
- Preserves existing games, play time, settings, extensions, account sessions,
  and all other files in `Data` during the update.

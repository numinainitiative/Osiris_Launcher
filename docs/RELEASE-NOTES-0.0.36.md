# Osiris Beta 0.0.36

This release improves Osiris on smaller displays and narrower window sizes.

- Makes extension cards wrap onto additional rows whenever they no longer fit
  across the Extensions page.
- Makes Game Details responsive: statistics cards are progressively moved into
  Details as space becomes limited, while lower cards switch cleanly from two
  columns to one.
- Keeps Links left-aligned in the compact Game Details layout and removes the
  large empty gaps that could appear between stacked cards.
- Sets a 1700-DIP minimum application width to protect layouts from unsupported
  window sizes.
- Keeps the selected Library grid-unit count while resizing the window by
  shrinking covers proportionally instead of silently showing fewer columns.

Existing games, settings, extensions, and other files in `Data` are preserved
during the update.

Osiris Beta 0.0.47

- Adds edition-level integrated-library pairing, allowing an edition inside a contained game profile to use a Steam, Xbox, or other synchronized library identity for launching, installation metadata, actions, and native play-time tracking without creating a duplicate visible profile.
- Makes paired editions explicit in Edit Game, including paired-state and repair labels, duplicate-pairing protection, support for explicitly excluded library games, transactional Save/Cancel behavior, and automatic folder and installation-size refresh from the backing library record.
- Fixes edition-specific custom logos so each edition keeps and displays its own artwork without overwriting another edition.
- Adds the automatic Osiris status lifecycle: Not Played, Playing, Played, and Abandoned follow first play and last activity, while Completed and Beaten remain deliberate terminal states. The game context menu now includes Set Status and an Automatic reset.
- Adds the public Beaten-status bridge reserved for trophy extensions and the six accepted status icons on Game Details.
- Includes the accepted contained-edition selector, editor, library-exclusion, layout, and stability refinements completed in Development since Beta 0.0.46.

This portable beta preserves existing installation data in its own Data directory. Extension packages remain independently versioned, and the inherited Playnite program-update path remains disabled.

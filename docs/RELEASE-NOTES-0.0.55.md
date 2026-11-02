# Osiris Beta 0.0.55

- Editions now have independent game profiles grouped into one library entry.
  Each has its own metadata, installation, actions, extension data and statistics.
  New local editions of integrated-library games have full local installation
  management; the original retains its Steam/Xbox connection.
- The edition selector works on every Game Edit page. **Copy page** fills the
  current page from another edition as a one-time copy, with normal Save/Cancel.
  Profile identities and library connections remain separate.
- Removing an edition preserves the others and never uninstalls game files.
  Existing editions convert with recovery snapshots. Historically shared playtime
  stays on the original to avoid duplicating it.
- Actions separate **Launching path**, **Tracking path** and **Track playtime**.
  Select the actual game executable to track sessions through intermediary
  launchers while independently enabling/disabling playtime counting. Screenshots
  and other session events remain active when time counting is disabled.
- Hours and Minutes inputs align with adjacent fields and have more room for
  three- and four-digit hour values.
- Fixed a startup library-refresh crash caused by accessing the main window from
  a background metadata event.

Existing profiles and extension data are preserved. No extension packages change.
Inherited Playnite program-update checks remain disabled; Osiris uses its own
update channel.

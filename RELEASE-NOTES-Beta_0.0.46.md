Osiris Beta 0.0.46

- Adds contained game editions under one global game profile, with edition-specific artwork, installation, actions, gallery, and HowLongToBeat data while shared play time and general metadata remain unified.
- Adds reusable edition names to Edit Game and Settings -> Library -> Manager, including safe add, rename, selection, removal, and single- versus multi-edition presentation.
- Adds `Remove and Exclude` for integrated-library games so a removed profile can be placed on Playnite's native import-exclusion list and stay removed after library synchronization.
- Fixes an edition-switch crash caused by replacing artwork files still held open by the interface. Edition artwork now activates through immutable content-addressed files, with guarded editor switching if a media operation fails.
- Includes the accepted edition selector, header, typography, layout, and interaction refinements from Development.

This portable beta preserves existing installation data in its own Data directory. Extension packages remain independently versioned, and the inherited Playnite program-update path remains disabled.

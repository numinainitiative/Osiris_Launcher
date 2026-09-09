# Osiris Beta 0.0.35

This release moves application updates fully into Osiris.

- Replaces the external Windows update prompt with a native Osiris update
  window shown after the interface has loaded.
- Adds every available Osiris release to the Notifications page so it can be
  reviewed again later.
- Adds a **View update** action that reopens the release window and its complete,
  scrollable release notes.
- Announces each release only once while keeping its notification available.
- Starts the verified updater silently only after **Install Update** is chosen.
- Preserves the existing `Data` directory, including games and settings, during
  updates and restarts Osiris through its normal launcher.

Existing installations may display the previous Windows prompt once while
moving to 0.0.35. All updates after 0.0.35 use the new in-app experience.

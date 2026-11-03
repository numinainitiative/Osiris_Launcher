using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    // Editions are database identities, not overlays on an integrated-library game.
    // Legacy snapshots remain in the grouping document for recovery only.
    internal static partial class GameEditions
    {
        internal static bool IsIndependent(Guid gameId)
        {
            lock (SyncRoot) return GetProfile(gameId)?.IndependentProfiles == true;
        }

        internal static Guid PrepareProfileForEdit(Guid gameId)
        {
            var converted = false;
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                var game = API.Instance?.Database?.Games?.Get(gameId);
                if (profile != null && !profile.IndependentProfiles && game != null && !game.IsRunning)
                {
                    EnsureIndependent(profile, game);
                    converted = true;
                }
            }
            if (converted) RaiseChanged();
            return GetActiveProfileId(gameId);
        }

        internal static Guid GetActiveProfileId(Guid gameId)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                var selected = profile?.Editions.FirstOrDefault(e => e.EditionId == profile.ActiveEditionId);
                if (profile?.IndependentProfiles == true && API.Instance?.Database?.Games != null &&
                    (selected == null || API.Instance.Database.Games.Get(selected.GameProfileId) == null))
                {
                    selected = profile.Editions.FirstOrDefault(e => API.Instance.Database.Games.Get(e.GameProfileId) != null);
                    if (selected != null) profile.ActiveEditionId = selected.EditionId;
                }
                return profile?.IndependentProfiles == true && selected != null ? selected.GameProfileId : gameId;
            }
        }

        internal static bool IsGroupEntry(Guid gameId)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                return profile?.IndependentProfiles != true || GetActiveProfileId(gameId) == gameId;
            }
        }

        private static Game NewLocalProfile(string name, Guid id)
        {
            // Deliberately no GetCopy: metadata, platform identity, actions, stats and
            // extension keys must not leak from the integrated original.
            return new Game { Id = id, Name = name, Added = DateTime.Now,
                PluginId = Guid.Empty, GameId = null, IncludeLibraryPluginAction = false };
        }

        private static void BackupProfiles(GameEditionProfile profile)
        {
            var directory = Path.Combine(OsirisPaths.SettingsDirectory, "EditionProfileRecovery");
            Directory.CreateDirectory(directory);
            var games = profile.Editions.Select(e => API.Instance.Database.Games.Get(
                e.GameProfileId == Guid.Empty ? profile.GameId : e.GameProfileId))
                .Where(g => g != null).GroupBy(g => g.Id).Select(g => g.First()).ToList();
            File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") +
                "-" + profile.GameId + ".json"), JsonConvert.SerializeObject(new { Profile = profile, Games = games },
                Formatting.Indented), new UTF8Encoding(false));
        }

        private static void EnsureIndependent(GameEditionProfile profile, Game current)
        {
            if (profile.IndependentProfiles) return;
            if (current == null || current.IsRunning) throw new InvalidOperationException("Stop the game before changing its editions.");
            BackupProfiles(profile);
            var originalGame = current.GetCopy();
            var previousIds = profile.Editions.ToDictionary(e => e.EditionId, e => e.GameProfileId);
            var createdIds = new List<Guid>();
            // Capture the last overlay before recovering every edition. Shared historical
            // statistics stay on the original; they cannot be attributed retroactively.
            CaptureEditionCore(profile, profile.ActiveEditionId, current);
            CaptureRuntimeSettings(current.Id, profile.ActiveEditionId);
            var original = profile.Editions.FirstOrDefault(e => e.IsDefault) ?? profile.Editions.First();
            var copies = new List<Game>();
            foreach (var edition in profile.Editions)
            {
                var copy = current.GetCopy();
                copy.Id = edition == original ? profile.GameId : edition.EditionId;
                if (edition != original && API.Instance.Database.Games.Get(copy.Id) != null)
                    throw new InvalidOperationException("An edition identity is already in use. Its existing game profile was not replaced.");
                if (edition != original)
                {
                    copy.PluginId = Guid.Empty;
                    copy.GameId = null;
                    copy.IncludeLibraryPluginAction = false;
                    copy.Playtime = 0;
                    copy.PlayCount = 0;
                    copy.LastActivity = null;
                }
                ApplyEditionCore(profile, edition.EditionId, copy, false);
                // Edition media already lives under the snapshot's edition identity.
                // Keep valid paths there; only the original's logo needs materializing.
                if (edition == original)
                    CopyStoredFilesWithoutDeleting(edition.EditionId, copy.Id, IsMediaFile);
                edition.GameProfileId = copy.Id;
                copies.Add(copy);
            }
            try
            {
                foreach (var copy in copies)
                {
                    if (API.Instance.Database.Games.Get(copy.Id) == null)
                    {
                        createdIds.Add(copy.Id);
                        API.Instance.Database.Games.Add(copy);
                    }
                    else API.Instance.Database.Games.Update(copy);
                }
                foreach (var edition in profile.Editions)
                    if (edition.GameProfileId != edition.EditionId)
                        MaterializeRuntimeSettings(edition.EditionId, edition.GameProfileId);
                profile.IndependentProfiles = true;
                Save();
            }
            catch
            {
                profile.IndependentProfiles = false;
                foreach (var edition in profile.Editions) edition.GameProfileId = previousIds[edition.EditionId];
                foreach (var id in createdIds)
                {
                    var created = API.Instance.Database.Games.Get(id);
                    if (created != null) API.Instance.Database.Games.Remove(created);
                }
                API.Instance.Database.Games.Update(originalGame);
                throw;
            }
        }

        private static GameEditionProfile CreateIndependentGroup(Game game, string label)
        {
            var profile = new GameEditionProfile
            {
                IndependentProfiles = true, GameId = game.Id, ActiveEditionId = game.Id,
                Editions = new List<GameEditionRecord>
                {
                    new GameEditionRecord { EditionId = game.Id, GameProfileId = game.Id,
                        IsDefault = true, Label = label }
                }
            };
            document.Profiles.Add(profile);
            return profile;
        }

        private static GameEditionRecord AddLocalProfile(GameEditionProfile profile, string label, string name)
        {
            var id = Guid.NewGuid();
            var record = new GameEditionRecord { EditionId = id, GameProfileId = id, Label = label };
            // Register membership before Add fires collection callbacks.
            profile.Editions.Add(record);
            try { API.Instance.Database.Games.Add(NewLocalProfile(name, id)); }
            catch { profile.Editions.Remove(record); throw; }
            return record;
        }

        private static bool ApplyIndependentSelection(Guid gameId, IEnumerable<string> selectedLabels)
        {
            var database = API.Instance?.Database;
            var game = database?.Games?.Get(gameId);
            if (game == null) return false;
            var labels = (selectedLabels ?? Enumerable.Empty<string>()).Select(s => (s ?? "").Trim())
                .Where(s => s.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                if (profile == null && labels.Count == 0) return true;
                if (GroupIsRunning(profile, game)) return false;
                if (profile == null) profile = CreateIndependentGroup(game, labels[0]);
                else EnsureIndependent(profile, game);
                BackupProfiles(profile);
                if (labels.Count == 0)
                {
                    // Removing the last edition label keeps its actual game profile.
                    if (profile.Editions.Count != 1) return false;
                    document.Profiles.Remove(profile);
                }
                else
                {
                    var removed = profile.Editions.Where(e => !labels.Contains(e.Label,
                        StringComparer.CurrentCultureIgnoreCase)).ToList();
                    foreach (var label in labels)
                    {
                        AddCatalogLabelCore(label);
                        if (!profile.Editions.Any(e => string.Equals(e.Label, label, StringComparison.CurrentCultureIgnoreCase)))
                            AddLocalProfile(profile, label, game.Name);
                    }
                    foreach (var record in removed) RemoveProfileRecord(profile, record);
                    NormalizeGroup(profile);
                }
                Save();
            }
            RaiseChanged();
            return true;
        }

        private static bool GroupIsRunning(GameEditionProfile profile, Game fallback)
        {
            return fallback.IsRunning || (profile?.IndependentProfiles == true && profile.Editions.Any(e =>
                API.Instance.Database.Games.Get(e.GameProfileId)?.IsRunning == true));
        }

        private static GameEditionChoice AddIndependentEdition(Guid gameId, string currentLabel)
        {
            var game = API.Instance?.Database?.Games?.Get(gameId);
            if (game == null || string.IsNullOrWhiteSpace(currentLabel)) return null;
            GameEditionRecord added;
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                if (GroupIsRunning(profile, game)) return null;
                if (profile == null) profile = CreateIndependentGroup(game, currentLabel.Trim());
                else EnsureIndependent(profile, game);
                BackupProfiles(profile);
                AddCatalogLabelCore(currentLabel);
                added = AddLocalProfile(profile, string.Empty, game.Name);
                profile.ActiveEditionId = added.EditionId;
                Save();
            }
            RaiseChanged();
            return ToChoice(added);
        }

        private static bool SwitchIndependentEdition(Guid gameId, Guid editionId)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                if (profile == null || !profile.Editions.Any(e => e.EditionId == editionId)) return false;
                var game = API.Instance?.Database?.Games?.Get(gameId);
                if (game == null || GroupIsRunning(profile, game)) return false;
                EnsureIndependent(profile, game);
                var target = profile.Editions.First(e => e.EditionId == editionId);
                if (API.Instance.Database.Games.Get(target.GameProfileId) == null) return false;
                profile.ActiveEditionId = editionId;
                Save();
            }
            RaiseChanged();
            return true;
        }

        private static bool RemoveIndependentEdition(Guid gameId, Guid editionId, Guid fallback)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(gameId);
                var game = API.Instance?.Database?.Games?.Get(gameId);
                if (profile == null || game == null || GroupIsRunning(profile, game) || profile.Editions.Count < 2) return false;
                EnsureIndependent(profile, game);
                var record = profile.Editions.FirstOrDefault(e => e.EditionId == editionId);
                if (record == null) return false;
                BackupProfiles(profile);
                RemoveProfileRecord(profile, record);
                if (profile.Editions.Any(e => e.EditionId == fallback)) profile.ActiveEditionId = fallback;
                NormalizeGroup(profile);
                Save();
            }
            RaiseChanged();
            return true;
        }

        private static void RemoveProfileRecord(GameEditionProfile profile, GameEditionRecord record)
        {
            var database = API.Instance.Database;
            DetachSharedPagesForRemoval(profile, record.GameProfileId);
            var game = database.Games.Get(record.GameProfileId);
            // Removing a profile is never an uninstall and never traverses InstallDirectory.
            if (game != null)
            {
                if (game.PluginId != Guid.Empty)
                {
                    var exclusion = new Playnite.ImportExclusionItem(game.GameId, game.Name, game.PluginId, null);
                    if (database.ImportExclusions[exclusion.Id] == null) database.ImportExclusions.Add(exclusion);
                }
                database.Games.Remove(game);
            }
            profile.PreviousProfileIds.Add(record.GameProfileId);
            profile.Editions.Remove(record);
        }

        private static void NormalizeGroup(GameEditionProfile profile)
        {
            if (!profile.Editions.Any(e => e.EditionId == profile.ActiveEditionId))
                profile.ActiveEditionId = profile.Editions[0].EditionId;
            if (!profile.Editions.Any(e => e.IsDefault)) profile.Editions[0].IsDefault = true;
            // Keep the stable grouping key even if the original database row is
            // removed. Old navigation entries still resolve to the surviving profile.
        }
    }
}

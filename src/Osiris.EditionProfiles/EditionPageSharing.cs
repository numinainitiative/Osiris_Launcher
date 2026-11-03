using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    // Public, read-only bridge: extensions can resolve a shared page without a
    // build-time dependency on the host theme or parsing its private document.
    public static class EditionPageSharing
    {
        public static Guid ResolveOwner(Guid gameId, string page) => GameEditions.GetSharedPageOwner(gameId, page);
    }

    internal static partial class GameEditions
    {
        private static bool sharingEventsAttached;
        private static bool synchronisingSharedPages;
        private static readonly string[] ShareablePages = { "General", "Advanced", "Actions", "Links", "Scripts", "Trophies" };

        internal static bool CanSharePage(string page) => ShareablePages.Contains(page, StringComparer.Ordinal);

        internal static void InitializePageSharing()
        {
            lock (SyncRoot)
            {
                if (sharingEventsAttached || API.Instance?.Database?.Games == null) return;
                API.Instance.Database.Games.ItemUpdated += OnSharedGameUpdated;
                sharingEventsAttached = true;
            }
        }

        private static void OnSharedGameUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            if (args?.UpdatedItems == null) return;
            foreach (var change in args.UpdatedItems)
            {
                try { SynchronizeSharedPageFields(change.OldData, change.NewData); }
                catch (Exception error) { Trace.TraceError("Shared edition page update failed: " + error); }
            }
        }

        internal static Guid GetSharedPageOwner(Guid id, string page)
        {
            lock (SyncRoot)
            {
                var group = SharedGroup(GetProfile(id), id, page);
                var owner = group?.FirstOrDefault(g => API.Instance.Database.Games.Get(g) != null) ?? Guid.Empty;
                return owner == Guid.Empty ? id : owner;
            }
        }

        internal static bool IsPageShared(Guid id, string page)
        {
            lock (SyncRoot) return SharedGroup(GetProfile(id), id, page)?.Count > 1;
        }

        private static List<Guid> SharedGroup(GameEditionProfile profile, Guid id, string page)
        {
            if (profile?.IndependentProfiles != true || page == null || profile.SharedPages == null) return null;
            return profile.SharedPages.TryGetValue(page, out var groups) ? groups.FirstOrDefault(g => g.Contains(id)) : null;
        }

        internal static void CommitPageSharing(Guid id, IDictionary<string, Guid?> changes)
        {
            if (changes == null || changes.Count == 0) return;
            lock (SyncRoot)
            {
                var profile = GetProfile(id);
                if (profile == null) throw new InvalidOperationException("This edition is no longer available.");
                var previous = JsonConvert.SerializeObject(profile.SharedPages);
                try { foreach (var change in changes) SetPageSharing(id, change.Key, change.Value); }
                catch
                {
                    profile.SharedPages = JsonConvert.DeserializeObject<Dictionary<string, List<List<Guid>>>>(previous);
                    Save();
                    throw;
                }
            }
        }

        // Called only after the editor commits. Merely opening a menu or copying
        // into a draft never changes the sharing graph or another edition.
        internal static void SetPageSharing(Guid id, string page, Guid? sourceId)
        {
            if (!CanSharePage(page)) throw new InvalidOperationException("Sharing is unavailable for this page.");
            lock (SyncRoot)
            {
                var profile = GetProfile(id);
                var current = API.Instance?.Database?.Games?.Get(id);
                if (profile?.IndependentProfiles != true || current == null || GroupIsRunning(profile, current))
                    throw new InvalidOperationException("Stop the game before changing shared pages.");
                if (sourceId.HasValue && (sourceId == id || !profile.Editions.Any(e => e.GameProfileId == sourceId) ||
                    API.Instance.Database.Games.Get(sourceId.Value) == null))
                    throw new InvalidOperationException("Choose another edition of this game.");
                var before = JsonConvert.SerializeObject(profile.SharedPages);
                BackupProfiles(profile);
                try
                {
                    var old = SharedGroup(profile, id, page);
                    var oldOwner = GetSharedPageOwner(id, page);
                    // Keep a recoverable independent snapshot on both departure
                    // and deletion of the canonical member.
                    if (old != null)
                    {
                        CopySharedPageResources(oldOwner, id, page);
                        if (oldOwner == id && old.Count > 1) CopySharedPageResources(id, old.First(g => g != id), page);
                        old.Remove(id);
                    }
                    if (!profile.SharedPages.TryGetValue(page, out var groups))
                        profile.SharedPages[page] = groups = new List<List<Guid>>();
                    groups.RemoveAll(g => g.Count < 2);
                    if (sourceId.HasValue)
                    {
                        var destination = SharedGroup(profile, sourceId.Value, page);
                        if (destination == null) groups.Add(destination = new List<Guid> { sourceId.Value });
                        destination.Add(id);
                    }
                    Save();
                }
                catch
                {
                    profile.SharedPages = JsonConvert.DeserializeObject<Dictionary<string, List<List<Guid>>>>(before);
                    throw;
                }
            }
            RaiseChanged();
        }

        // A native playtime/import/editor update can originate from either member.
        // Mirror only changed whitelisted fields, never a whole stale Game clone.
        internal static void SynchronizeSharedPageFields(Game before, Game after)
        {
            if (after == null) return;
            lock (SyncRoot)
            {
                if (synchronisingSharedPages) return;
                var profile = GetProfile(after.Id);
                if (profile?.IndependentProfiles != true) return;
                synchronisingSharedPages = true;
                try
                {
                    var targets = new Dictionary<Guid, Game>();
                    foreach (var page in ShareablePages.Where(p => p != "Trophies"))
                    {
                        var members = SharedGroup(profile, after.Id, page);
                        if (members == null) continue;
                        foreach (var field in EditionPageCopy.GetFields(page))
                        {
                            var property = typeof(Game).GetProperty(field);
                            var value = JToken.FromObject(new { Value = property.GetValue(after) })["Value"];
                            if (before != null && JToken.DeepEquals(value,
                                JToken.FromObject(new { Value = property.GetValue(before) })["Value"])) continue;
                            foreach (var id in members.Where(g => g != after.Id))
                            {
                                if (!targets.TryGetValue(id, out var target))
                                {
                                    var saved = API.Instance.Database.Games.Get(id);
                                    if (saved == null) continue;
                                    targets[id] = target = saved.GetCopy();
                                }
                                property.SetValue(target, value.Type == JTokenType.Null ? null : value.ToObject(property.PropertyType));
                            }
                        }
                    }
                    foreach (var target in targets.Values) API.Instance.Database.Games.Update(target);
                }
                finally { synchronisingSharedPages = false; }
            }
        }

        // Trophies uses one real catalogue for linked editions. Its global icon
        // cache is URL-addressed, so snapshots contain no edition-relative paths.
        private static void CopySharedPageResources(Guid source, Guid target, string page)
        {
            if (source == target || page != "Trophies") return;
            var root = API.Instance?.Paths?.ExtensionsDataPath;
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Extension storage is unavailable.");
            root = Path.Combine(root, "72ba1fc9-490b-4fa4-96f0-0a154b56c1db", "catalogues");
            var file = Path.Combine(root, source.ToString("D") + ".json");
            var destination = Path.Combine(root, target.ToString("D") + ".json");
            if (!File.Exists(file))
            {
                if (File.Exists(destination))
                {
                    File.Copy(destination, destination + ".previous", true);
                    File.Delete(destination);
                }
                return;
            }
            var json = JObject.Parse(File.ReadAllText(file));
            if (json.Value<Guid?>("SharedSourcePluginId") == null)
                json["SharedSourcePluginId"] = API.Instance.Database.Games.Get(source)?.PluginId ?? Guid.Empty;
            Directory.CreateDirectory(root);
            var temp = destination + ".sharing.tmp";
            File.WriteAllText(temp, json.ToString(Formatting.Indented));
            if (File.Exists(destination)) File.Replace(temp, destination, destination + ".previous", true);
            else File.Move(temp, destination);
        }

        private static void DetachSharedPagesForRemoval(GameEditionProfile profile, Guid id)
        {
            foreach (var page in profile.SharedPages.ToList())
            {
                var group = SharedGroup(profile, id, page.Key);
                if (group == null) continue;
                if (group[0] == id && group.Count > 1)
                    CopySharedPageResources(id, group.First(g => g != id), page.Key);
                group.Remove(id);
                page.Value.RemoveAll(g => g.Count < 2);
            }
        }
    }
}

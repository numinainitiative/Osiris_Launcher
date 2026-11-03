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
    public static class EditionPageSharing
    {
        public static Guid ResolveOwner(Guid gameId, string page) => GameEditions.GetSharedPageOwner(gameId, page);
        public static Game ResolveGame(Game game, string page) => game == null ? null :
            API.Instance?.Database?.Games?.Get(ResolveOwner(game.Id, page)) ?? game;
    }
    internal static partial class GameEditions
    {
        private static bool sharingEventsAttached, synchronisingSharedPages;
        private static readonly string[] ShareablePages = { "General", "Advanced", "Media", "Actions", "Links", "Scripts", "Gallery", "Screenshots", "StatsInsights", "HowLongToBeat", "Exophase", "Trophies" };
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
                try { SynchronizeSharedPageFields(change.OldData, change.NewData); }
                catch (Exception error) { Trace.TraceError("Edition page mirror update failed: " + error); }
        }
        private static void EnsureMirrors(GameEditionProfile profile)
        {
            if (profile == null) return;
            if (profile.PageMirrors == null) profile.PageMirrors = new Dictionary<Guid, Dictionary<string, Guid>>();
            // Existing shared groups become directed mirrors of their canonical
            // source. No files are changed merely by reading the old document.
            foreach (var page in profile.SharedPages.ToList())
                foreach (var group in page.Value.Where(g => g != null && g.Count > 1))
                {
                    var owner = group.FirstOrDefault(id => API.Instance.Database.Games.Get(id) != null);
                    if (owner == Guid.Empty) continue;
                    foreach (var id in group.Where(id => id != owner && profile.Editions.Any(e => e.GameProfileId == id)))
                    {
                        if (!profile.PageMirrors.TryGetValue(id, out var mirrors)) profile.PageMirrors[id] = mirrors = new Dictionary<string, Guid>();
                        if (!mirrors.ContainsKey(page.Key)) mirrors[page.Key] = owner;
                    }
                }
            profile.SharedPages.Clear();
            foreach (var pages in profile.PageMirrors.Values)
                foreach (var page in pages.Keys.Where(p => !CanSharePage(p)).ToList()) pages.Remove(page);
        }
        internal static Guid? GetPageMirrorSource(Guid id, string page)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(id); EnsureMirrors(profile);
                return profile != null && page != null && profile.PageMirrors.TryGetValue(id, out var pages) && pages.TryGetValue(page, out var source)
                    ? (Guid?)source : null;
            }
        }
        internal static Guid GetSharedPageOwner(Guid id, string page)
        {
            lock (SyncRoot)
            {
                var seen = new HashSet<Guid>(); var current = id;
                while (seen.Add(current))
                {
                    var next = GetPageMirrorSource(current, page);
                    if (!next.HasValue || API.Instance.Database.Games.Get(next.Value) == null) return current;
                    current = next.Value;
                }
                return id;
            }
        }
        internal static bool IsPageShared(Guid id, string page) => GetPageMirrorSource(id, page).HasValue;
        internal static Guid[] GetPageDependents(Guid source, string page)
        {
            lock (SyncRoot)
            {
                var profile = GetProfile(source);
                return profile == null ? new Guid[0] : profile.Editions.Select(e => e.GameProfileId)
                    .Where(id => id != source && GetSharedPageOwner(id, page) == source).ToArray();
            }
        }
        internal static void ValidateMirrorDrafts(IDictionary<Guid, Dictionary<string, Guid?>> drafts)
        {
            lock (SyncRoot)
            {
                foreach (var group in drafts.GroupBy(d => GetProfile(d.Key)))
                {
                    var profile = group.Key; EnsureMirrors(profile);
                    if (profile == null) continue;
                    var preview = JsonConvert.DeserializeObject<Dictionary<Guid, Dictionary<string, Guid>>>(JsonConvert.SerializeObject(profile.PageMirrors));
                    foreach (var draft in group)
                    {
                        if (!preview.TryGetValue(draft.Key, out var pages)) preview[draft.Key] = pages = new Dictionary<string, Guid>();
                        foreach (var page in draft.Value)
                        {
                            if (page.Value.HasValue)
                            {
                                if (!profile.Editions.Any(e => e.GameProfileId == page.Value.Value)) throw new InvalidOperationException("A mirror source is no longer available.");
                                pages[page.Key] = page.Value.Value;
                            }
                            else pages.Remove(page.Key);
                        }
                    }
                    foreach (var draft in preview)
                        foreach (var page in draft.Value.Keys)
                        {
                            var seen = new HashSet<Guid>(); var id = draft.Key;
                            while (preview.TryGetValue(id, out var pages) && pages.TryGetValue(page, out var next))
                            {
                                if (!seen.Add(id)) throw new InvalidOperationException("The pending page mirrors form a loop. Disable one mirror or choose another source before saving.");
                                id = next;
                            }
                        }
                }
            }
        }
        internal static void CommitPageSharing(Guid id, IDictionary<string, Guid?> changes)
        {
            if (changes == null || changes.Count == 0) return;
            lock (SyncRoot)
            {
                var profile = GetProfile(id); EnsureMirrors(profile);
                if (profile == null) throw new InvalidOperationException("This edition is no longer available.");
                var previous = JsonConvert.SerializeObject(profile.PageMirrors);
                try { foreach (var change in changes) SetPageSharing(id, change.Key, change.Value); }
                catch
                {
                    profile.PageMirrors = JsonConvert.DeserializeObject<Dictionary<Guid, Dictionary<string, Guid>>>(previous);
                    Save(); throw;
                }
            }
        }
        internal static void SetPageSharing(Guid id, string page, Guid? sourceId)
        {
            if (!CanSharePage(page)) throw new InvalidOperationException("Mirroring is unavailable for this page.");
            lock (SyncRoot)
            {
                var profile = GetProfile(id); EnsureMirrors(profile);
                var current = API.Instance?.Database?.Games?.Get(id);
                if (profile?.IndependentProfiles != true || current == null || GroupIsRunning(profile, current))
                    throw new InvalidOperationException("Stop the game before changing page mirrors.");
                if (sourceId.HasValue)
                {
                    if (sourceId == id || !profile.Editions.Any(e => e.GameProfileId == sourceId) || API.Instance.Database.Games.Get(sourceId.Value) == null)
                        throw new InvalidOperationException("Choose another edition of this game.");
                    var seen = new HashSet<Guid> { id }; var check = sourceId;
                    while (check.HasValue)
                    {
                        if (!seen.Add(check.Value)) throw new InvalidOperationException("These editions would mirror each other in a loop. Choose another source.");
                        check = GetPageMirrorSource(check.Value, page);
                    }
                }
                var previous = JsonConvert.SerializeObject(profile.PageMirrors);
                BackupProfiles(profile);
                try
                {
                    var oldOwner = GetSharedPageOwner(id, page);
                    if (oldOwner != id) CopySharedPageResources(oldOwner, id, page);
                    if (!profile.PageMirrors.TryGetValue(id, out var pages)) profile.PageMirrors[id] = pages = new Dictionary<string, Guid>();
                    if (sourceId.HasValue) pages[page] = sourceId.Value;
                    else pages.Remove(page);
                    Save();
                }
                catch
                {
                    profile.PageMirrors = JsonConvert.DeserializeObject<Dictionary<Guid, Dictionary<string, Guid>>>(previous);
                    throw;
                }
            }
            RaiseChanged();
            if (sourceId.HasValue && page == "Advanced")
                SynchronizeSharedPageFields(null, API.Instance.Database.Games.Get(sourceId.Value));
        }
        // Resolve every dependent against its source. Only positive native
        // activity deltas contribute back; other dependent fields stay read-only.
        internal static void SynchronizeSharedPageFields(Game before, Game after)
        {
            if (after == null) return;
            lock (SyncRoot)
            {
                if (synchronisingSharedPages) return;
                var profile = GetProfile(after.Id); EnsureMirrors(profile);
                if (profile?.IndependentProfiles != true) return;
                synchronisingSharedPages = true;
                try
                {
                    // Native session counters still originate on the physical
                    // edition being played. Contribute its positive activity
                    // delta to the shared Advanced source, without merging IDs
                    // or giving a dependent editor write access to other fields.
                    if (before != null && GetPageMirrorSource(after.Id, "Advanced").HasValue)
                    {
                        var owner = API.Instance.Database.Games.Get(GetSharedPageOwner(after.Id, "Advanced"))?.GetCopy();
                        if (owner != null && owner.Id != after.Id)
                        {
                            var activityChanged = false;
                            if (after.Playtime > before.Playtime) { owner.Playtime += Math.Min(ulong.MaxValue - owner.Playtime, after.Playtime - before.Playtime); activityChanged = true; }
                            if (after.PlayCount > before.PlayCount) { owner.PlayCount += Math.Min(ulong.MaxValue - owner.PlayCount, after.PlayCount - before.PlayCount); activityChanged = true; }
                            if (after.LastActivity.HasValue && (!owner.LastActivity.HasValue || after.LastActivity > owner.LastActivity)) { owner.LastActivity = after.LastActivity; activityChanged = true; }
                            if (activityChanged) API.Instance.Database.Games.Update(owner);
                        }
                    }
                    foreach (var entry in profile.PageMirrors.ToList())
                    {
                        var saved = API.Instance.Database.Games.Get(entry.Key);
                        if (saved == null) continue;
                        var target = saved.GetCopy(); var changed = false;
                        foreach (var page in entry.Value.Keys.Where(p => p != "Trophies").ToList())
                        {
                            var source = API.Instance.Database.Games.Get(GetSharedPageOwner(entry.Key, page));
                            if (source == null || source.Id == target.Id) continue;
                            foreach (var field in EditionPageCopy.GetFields(page))
                            {
                                var property = typeof(Game).GetProperty(field);
                                var value = JToken.FromObject(new { Value = property.GetValue(source) })["Value"];
                                if (JToken.DeepEquals(value, JToken.FromObject(new { Value = property.GetValue(target) })["Value"])) continue;
                                property.SetValue(target, value.Type == JTokenType.Null ? null : value.ToObject(property.PropertyType)); changed = true;
                            }
                        }
                        if (changed) API.Instance.Database.Games.Update(target);
                    }
                }
                finally { synchronisingSharedPages = false; }
            }
        }
        private static void CopySharedPageResources(Guid source, Guid target, string page)
        {
            if (source == target) return;
            if (page != "Trophies") { CopyMirrorPageResources(source, target, page); return; }
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
            EnsureMirrors(profile);
            foreach (var entry in profile.PageMirrors.ToList())
                foreach (var page in entry.Value.Where(p => p.Value == id).ToList())
                {
                    CopySharedPageResources(GetSharedPageOwner(entry.Key, page.Key), entry.Key, page.Key);
                    entry.Value.Remove(page.Key);
                }
            profile.PageMirrors.Remove(id);
        }
    }
}

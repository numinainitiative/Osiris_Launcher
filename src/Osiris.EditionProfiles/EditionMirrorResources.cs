using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    internal static partial class GameEditions
    {
        internal static void CopyMirrorPageResources(Guid source, Guid target, string page)
        {
            if (source == target) return;
            var database = API.Instance.Database;
            var from = database.GetFileStoragePath(source);
            var to = database.GetFileStoragePath(target);
            if (page == "HowLongToBeat" || page == "Exophase") { CopyExtensionSettings(source, target, page); return; }
            Func<string, bool> include = file => page == "Media" ? IsMediaFile(file) || file == "OsirisDynamicMedia.ini" :
                page == "Gallery" ? IsGalleryFile(file) || file == "OsirisGallery.ini" :
                page == "Screenshots" ? file == "OsirisScreenshotsGallery.ini" :
                page == "StatsInsights" && file == "OsirisStatsInsights.ini";
            if (page == "Media" && Directory.Exists(to))
                foreach (var file in Directory.EnumerateFiles(to).Where(f => include(Path.GetFileName(f)) && !f.EndsWith(".previous", StringComparison.OrdinalIgnoreCase)))
                { File.Copy(file, file + ".previous", true); File.Delete(file); }
            if (Directory.Exists(from))
                foreach (var file in Directory.EnumerateFiles(from).Where(f => include(Path.GetFileName(f)) && !f.EndsWith(".previous", StringComparison.OrdinalIgnoreCase)))
                    CopyResourceFile(file, Path.Combine(to, Path.GetFileName(file)));
            var ini = page == "Screenshots" ? "OsirisScreenshotsGallery.ini" : page == "StatsInsights" ? "OsirisStatsInsights.ini" : page == "Gallery" ? "OsirisGallery.ini" : null;
            if (ini != null && !File.Exists(Path.Combine(from, ini)))
            {
                Directory.CreateDirectory(to);
                var old = Path.Combine(to, ini);
                if (File.Exists(old)) File.Copy(old, old + ".previous", true);
                File.WriteAllText(old, "Enabled=True\r\n" + (page == "Gallery" ? "Source=Steam\r\n" : ""));
            }
            if (page == "Screenshots" && Directory.Exists(Path.Combine(from, "Screenshots")))
                foreach (var file in Directory.EnumerateFiles(Path.Combine(from, "Screenshots"), "*.png"))
                    CopyResourceFile(file, Path.Combine(to, "Screenshots", Path.GetFileName(file)));
            if (page == "Media")
            {
                var saved = database.Games.Get(target)?.GetCopy();
                var owner = database.Games.Get(source);
                if (saved != null && owner != null)
                {
                    EditionPageCopy.Apply(owner, saved, "Media");
                    RebaseEditionMedia(saved);
                    var previous = synchronisingSharedPages;
                    synchronisingSharedPages = true;
                    try { database.Games.Update(saved); }
                    finally { synchronisingSharedPages = previous; }
                }
            }
        }

        private static void CopyResourceFile(string source, string target)
        {
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (File.Exists(target)) File.Copy(target, target + ".previous", true);
            File.Copy(source, target, true);
        }

        internal static void EnsureEditionMediaOwnership(Game game)
        {
            if (game == null || IsPageShared(game.Id, "Media")) return;
            var copy = game.GetCopy();
            if (RebaseEditionMedia(copy)) { API.Instance.Database.Games.Update(copy); copy.CopyDiffTo(game); }
        }
        private static bool RebaseEditionMedia(Game game)
        {
            var profile = GetProfile(game.Id);
            if (profile?.IndependentProfiles != true) return false;
            var changed = false;
            foreach (var field in EditionPageCopy.GetFields("Media"))
            {
                var property = typeof(Game).GetProperty(field);
                var path = property.GetValue(game) as string;
                if (string.IsNullOrEmpty(path)) continue;
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile) continue;
                var full = Path.IsPathRooted(path) ? path : API.Instance.Database.GetFullFilePath(path);
                if (!File.Exists(full)) continue;
                var other = profile.Editions.Where(e => e.GameProfileId != game.Id).SelectMany(e => new[] { e.GameProfileId, e.EditionId }).Any(id =>
                    full.StartsWith(API.Instance.Database.GetFileStoragePath(id).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
                if (!other) continue;
                CopyResourceFile(full, Path.Combine(API.Instance.Database.GetFileStoragePath(game.Id), Path.GetFileName(full)));
                property.SetValue(game, Path.Combine(game.Id.ToString(), Path.GetFileName(full))); changed = true;
            }
            return changed;
        }

        // Bypass the read alias only when materializing an independent snapshot.
        // Updating the loaded store's cache and its own writer keeps the plugin
        // and its on-disk dictionary consistent without aliasing native IDs.
        private static void CopyExtensionSettings(Guid source, Guid target, string page)
        {
            var id = page == "HowLongToBeat" ? "fba3e63d-d1a1-4b9d-91c6-091a1220377d" : "26131977-669a-4ef7-a66c-026122a24089";
            var plugin = PlayniteApplication.Current?.Extensions?.GenericPlugins.FirstOrDefault(p => p.Id == Guid.Parse(id));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var store = plugin?.GetType().GetField("gameSettingsStore", flags)?.GetValue(plugin);
            var format = page == "HowLongToBeat" ? "D" : "N";
            if (store != null)
            {
                var gate = store.GetType().GetField("syncRoot", flags).GetValue(store);
                lock (gate)
                {
                    var value = store.GetType().GetMethod("Load", flags).Invoke(store, new object[] { source });
                    var cloned = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), value.GetType());
                    var cache = (IDictionary)store.GetType().GetField("settingsByGame", flags).GetValue(store);
                    cache[target.ToString(format)] = cloned;
                    store.GetType().GetMethod(page == "HowLongToBeat" ? "SaveAll" : "WriteAll", flags).Invoke(store, null);
                }
                return;
            }
            var folder = page == "HowLongToBeat" ? "HowLongToBeat_" + id : "Exophase_" + id;
            var file = Path.Combine(API.Instance.Paths.ExtensionsDataPath, "Extras", folder, "game-settings.json");
            if (!File.Exists(file)) return;
            var document = JObject.Parse(File.ReadAllText(file));
            var valueToken = document[source.ToString(format)];
            if (valueToken == null) document.Remove(target.ToString(format));
            else document[target.ToString(format)] = valueToken.DeepClone();
            var temporary = file + ".mirror.tmp";
            File.WriteAllText(temporary, document.ToString(Formatting.Indented));
            File.Replace(temporary, file, file + ".previous", true);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    internal static class EditionPageCopy
    {
        private static readonly Dictionary<string, string[]> Fields = new Dictionary<string, string[]>
        {
            ["General"] = new[] { "Name", "SortingName", "PlatformIds", "GenreIds", "DeveloperIds", "PublisherIds",
                "FeatureIds", "TagIds", "AgeRatingIds", "SeriesIds", "ReleaseDate", "SourceId", "Version", "Description" },
            ["Advanced"] = new[] { "LastActivity", "Playtime", "PlayCount", "CompletionStatusId", "Added", "Favorite", "Hidden", "Notes" },
            ["Links"] = new[] { "Links" },
            ["Actions"] = new[] { "GameActions", "IncludeLibraryPluginAction" },
            ["Scripts"] = new[] { "PreScript", "PostScript", "GameStartedScript", "UseGlobalPreScript", "UseGlobalPostScript", "UseGlobalGameStartedScript" },
            ["Installation"] = new[] { "InstallDirectory", "InstallSize", "IsInstalled", "OverrideInstallState", "LastSizeScanDate" }
        };

        internal static void Apply(Game source, Game draft, string page)
        {
            if (source == null || draft == null || source.Id == draft.Id) throw new ArgumentException("Choose a different edition.");
            if (!Fields.TryGetValue(page, out var names)) throw new ArgumentException("This page has no native fields to copy.");
            foreach (var name in names)
            {
                var property = typeof(Game).GetProperty(name);
                if (property == null || !property.CanWrite) throw new InvalidOperationException("Missing page field: " + name);
                var original = property.GetValue(source);
                var copy = original == null ? null : JsonConvert.DeserializeObject(JsonConvert.SerializeObject(original), property.PropertyType);
                property.SetValue(draft, copy);
            }
        }

        // Native metadata selectors maintain their own selection lists until Save.
        internal static void ApplyGeneralSelections(object nativeModel, Game source)
        {
            var lists = new Dictionary<string, string> { ["Platforms"] = "PlatformIds", ["Genres"] = "GenreIds",
                ["Developers"] = "DeveloperIds", ["Publishers"] = "PublisherIds", ["Features"] = "FeatureIds",
                ["Tags"] = "TagIds", ["AgeRatings"] = "AgeRatingIds", ["Series"] = "SeriesIds" };
            foreach (var pair in lists)
            {
                var list = nativeModel?.GetType().GetProperty(pair.Key)?.GetValue(nativeModel);
                var items = list as IEnumerable;
                if (items == null) throw new InvalidOperationException("The metadata selector is unavailable: " + pair.Key);
                var selected = typeof(Game).GetProperty(pair.Value)?.GetValue(source) as IEnumerable<Guid>;
                var ids = new HashSet<Guid>(selected ?? Enumerable.Empty<Guid>());
                foreach (var row in items)
                {
                    var item = row.GetType().GetProperty("Item")?.GetValue(row) as DatabaseObject;
                    var property = row.GetType().GetProperty("Selected");
                    if (item != null && property != null)
                        property.SetValue(row, (bool?)(ids.Contains(item.Id) || (item.Id == Guid.Empty && ids.Count == 0)));
                }
            }
        }
    }
}

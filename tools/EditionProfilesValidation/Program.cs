using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

static class Program
{
    static Dictionary<Guid, Game> games = new Dictionary<Guid, Game>();
    static string sandbox;
    static Assembly theme;
    static Type editions;
    static int checks;
    static bool failNextAdd;
    static EventHandler<ItemUpdatedEventArgs<Game>> gameUpdates;
    static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    sealed class Proxy : RealProxy
    {
        readonly string role;
        public Proxy(Type type, string role) : base(type) { this.role = role; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                var method = (MethodInfo)call.MethodBase;
                var type = method.ReturnType;
                object value = null;
                if (role == "games")
                {
                    if (call.MethodName == "add_ItemUpdated") gameUpdates += (EventHandler<ItemUpdatedEventArgs<Game>>)call.Args[0];
                    else if (call.MethodName == "remove_ItemUpdated") gameUpdates -= (EventHandler<ItemUpdatedEventArgs<Game>>)call.Args[0];
                    else if (call.MethodName == "Get" || call.MethodName == "get_Item")
                        value = games.TryGetValue((Guid)call.Args[0], out var game) ? game : null;
                    else if (call.MethodName == "Add" || call.MethodName == "Update")
                    {
                        var game = (Game)call.Args[0];
                        games.TryGetValue(game.Id, out var previous);
                        games[game.Id] = game;
                        if (call.MethodName == "Update") gameUpdates?.Invoke(null, new ItemUpdatedEventArgs<Game>(previous, game));
                        if (call.MethodName == "Add" && failNextAdd)
                        { failNextAdd = false; throw new IOException("Injected database write failure"); }
                    }
                    else if (call.MethodName == "Remove") games.Remove(((Game)call.Args[0]).Id);
                    else if (call.MethodName == "GetEnumerator") value = games.Values.ToList().GetEnumerator();
                    else if (call.MethodName == "get_Count") value = games.Count;
                }
                else if (call.MethodName == "GetFileStoragePath")
                {
                    value = Path.Combine(sandbox, "files", call.Args[0].ToString());
                    Directory.CreateDirectory((string)value);
                }
                if (call.MethodName == "GetFullFilePath") value = Path.Combine(sandbox,"files",(string)call.Args[0]);
                if (call.MethodName == "get_IsOpen") value = true;
                if (call.MethodName == "get_ExtensionsDataPath") value = Path.Combine(sandbox, "extensions-data");
                if (type.IsInterface && value == null)
                    value = new Proxy(type, call.MethodName == "get_Games" ? "games" : call.MethodName).GetTransparentProxy();
                else if (type != typeof(void) && type.IsValueType && value == null) value = Activator.CreateInstance(type);
                return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
            }
            catch (Exception e) { return new ReturnMessage(e, call); }
        }
    }
    static object Call(string method, params object[] args) => editions.GetMethod(method, Flags).Invoke(null, args);
    static void Check(bool yes, string label) { if (!yes) throw new Exception(label); checks++; }
    [STAThread]
    static int Main(string[] args)
    {
        var app = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            var file = Path.Combine(app, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        try { Run(args); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Run(string[] args)
    {
        new Application();
        sandbox = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(sandbox);
        theme = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        editions = theme.GetType("OsirisTheme.GameEditions", true);
        var nativePaths = Assembly.LoadFrom(Path.Combine(args[1], "Playnite.dll")).GetType("Playnite.PlaynitePaths", true);
        nativePaths.GetProperty("ConfigRootPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(null, sandbox);
        var paths = theme.GetType("OsirisTheme.OsirisPaths", true);
        var settings = (string)paths.GetProperty("SettingsDirectory", Flags).GetValue(null);
        Check(settings.StartsWith(sandbox + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "Validation must be confined to its explicit sandbox: " + settings);
        typeof(API).GetProperty("Instance").SetValue(null,
            new Proxy(typeof(IPlayniteAPI), "api").GetTransparentProxy());
        var steam = new Game { Id = Guid.NewGuid(), Name = "Integrated original", PluginId = Guid.NewGuid(),
            GameId = "platform-id", Description = "Original description", Version = "original-version",
            InstallDirectory = "original-folder", Playtime = 900, PlayCount = 3, LastActivity = DateTime.Now };
        games[steam.Id] = steam;
        Check((bool)Call("ApplySelection", steam.Id, new[] { "Steam", "DRM free" }), "Create independent group");
        Check(games.Count == 2, "Separate actual database record created");
        var local = games.Values.Single(g => g.Id != steam.Id);
        Check(local.IsCustomGame && local.PluginId == Guid.Empty && string.IsNullOrEmpty(local.GameId), "Local identity has no platform ownership");
        Check(local.Playtime == 0 && local.PlayCount == 0 && !local.LastActivity.HasValue, "New statistics empty");
        Check(string.IsNullOrEmpty(local.Description) && string.IsNullOrEmpty(local.Version) && string.IsNullOrEmpty(local.InstallDirectory), "General/Advanced/Installation independent and initially blank");
        Check(local.GameActions == null || local.GameActions.Count == 0, "Actions not copied from platform");
        Check((bool)Call("SupportsLocalStorage", local) && !(bool)Call("SupportsLocalStorage", steam), "Installation permission follows real edition identity");
        var choice = ((IEnumerable)Call("GetEditions", steam.Id)).Cast<object>().Single(e =>
            (string)e.GetType().GetProperty("Label").GetValue(e) == "DRM free");
        var localEdition = (Guid)choice.GetType().GetProperty("EditionId").GetValue(choice);
        local.Description = "Independent description"; local.Version = "DRM build";
        local.InstallDirectory = "local-folder"; local.Playtime = 44; local.PlayCount = 1;
        Check((bool)Call("SwitchEdition", steam.Id, localEdition), "Switch to local profile");
        Check((Guid)Call("GetActiveProfileId", steam.Id) == local.Id, "Whole profile identity switches");
        Check((bool)Call("IsGroupEntry", local.Id) && !(bool)Call("IsGroupEntry", steam.Id), "Exactly one grouped library entry");
        typeof(Game).GetProperty("IsRunning").SetValue(local, true);
        Check(!(bool)Call("SwitchEdition", steam.Id, steam.Id), "Cannot switch a running profile out from under tracking");
        Check(!(bool)Call("RemoveEdition", steam.Id, localEdition, steam.Id), "Cannot remove a running profile");
        typeof(Game).GetProperty("IsRunning").SetValue(local, false);
        Check((Guid)Call("GetStorageId", local.Id, "Media") == local.Id, "Media uses physical profile key");
        Call("ApplyActiveEditionToCopy", local.GetCopy(), local.Id);
        Check(steam.Description == "Original description" && steam.Version == "original-version" && steam.InstallDirectory == "original-folder", "Original metadata/installation never overwritten");
        Check(steam.Playtime == 900 && local.Playtime == 44, "Separate playtime preserved");
        var settingsType = theme.GetType("OsirisTheme.LocalStorageSettings", true);
        var store = theme.GetType("OsirisTheme.LocalStorageSettingsStore", true);
        var storage = Activator.CreateInstance(settingsType, true);
        settingsType.GetProperty("Enabled").SetValue(storage, true);
        settingsType.GetProperty("ArchivePath").SetValue(storage, "local-archive");
        store.GetMethod("Save", Flags).Invoke(null, new[] { (object)local.Id, storage });
        Check((bool)Call("SwitchEdition", local.Id, steam.Id), "Switch back to integrated original");
        var originalStorage = store.GetMethod("Load", Flags).Invoke(null, new object[] { steam.Id });
        Check(!(bool)settingsType.GetProperty("Enabled").GetValue(originalStorage), "Archive enablement does not leak to original");
        Check(local.Description == "Independent description" && local.PlayCount == 1, "Full local state survives switching away");
        // Persist/reload grouping with a fresh static document.
        editions.GetField("document", Flags).SetValue(null, null);
        Check(((IEnumerable)Call("GetEditions", local.Id)).Cast<object>().Count() == 2, "Group reload resolves from either physical profile");
        Check((bool)Call("RemoveEdition", steam.Id, localEdition, steam.Id), "Remove local edition");
        Check(games.Count == 1 && games.ContainsKey(steam.Id) && steam.Playtime == 900, "Original survives child deletion");
        Check(Directory.GetFiles(Path.Combine(settings, "EditionProfileRecovery")).Length >= 2, "Recovery snapshots made before changes");
        Check((bool)Call("ApplySelection", steam.Id, new[] { "Steam", "Second local" }), "Re-add independent child");
        local = games.Values.Single(g => g.Id != steam.Id);
        var installSentinel = Path.Combine(sandbox, "installed-game", "keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(installSentinel)); File.WriteAllText(installSentinel, "game bytes");
        steam.InstallDirectory = Path.GetDirectoryName(installSentinel);
        Check((bool)Call("RemoveEdition", steam.Id, steam.Id, local.Id), "Original edition may also be removed");
        Check(games.Count == 1 && games.ContainsKey(local.Id) && File.Exists(installSentinel), "Removing original keeps local and does not uninstall files");
        Check((Guid)Call("GetActiveProfileId", steam.Id) == local.Id, "Old group navigation resolves after original removal");
        Check((bool)Call("ApplySelection", local.Id, new[] { "Second local", "Another" }), "Add sibling after original removal");
        var survivor = games.Values.Single(g => g.Id != local.Id);
        Call("RemoveProfile", local.Id);
        games.Remove(local.Id); // Native Remove Game calls this after detaching grouping.
        Check(((IEnumerable)Call("GetEditions", survivor.Id)).Cast<object>().Count() == 1 &&
            (Guid)Call("GetActiveProfileId", local.Id) == survivor.Id, "Native removal preserves sibling and old navigation");
        local = survivor;
        Call("RemoveProfile", local.Id);
        Check(((IEnumerable)Call("GetEditions", local.Id)).Cast<object>().Count() == 0, "Native removal of final edition clears grouping");
        ValidateLegacy(settings);
        ValidatePageCopy();
        ValidatePageSharing();
        ValidateEditorDrafts();
        ValidateEditionControls();
        ValidateLegacyRemoval(settings);
        ValidateAllPageMirrors(args.Length > 4 ? args[4] : null);
        ValidateBackgroundMetadataQueue();
        ValidateMarkup(args[2]);
        Console.WriteLine("PASS " + checks + " independent edition profile checks; isolated mock database and sandbox files only.");
    }
    static void ValidateLegacy(string settings)
    {
        games.Clear();
        var originalId = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var game = new Game { Id = originalId, Name = "Legacy", PluginId = Guid.NewGuid(), GameId = "steam-id",
            Description = "Previously shared metadata", Playtime = 800, InstallDirectory = "active-local" };
        games[game.Id] = game;
        File.WriteAllText(Path.Combine(settings, "GameEditions.json"), JsonConvert.SerializeObject(new {
            SchemaVersion = 3, Profiles = new[] { new { GameId = originalId, ActiveEditionId = second, Editions = new[] {
                new { EditionId = first, Label = "Steam", IsDefault = true, Pages = new { Installation = new { InstallDirectory = "steam-install", InstallSize = 12UL } } },
                new { EditionId = second, Label = "Local", IsDefault = false, Pages = new { Installation = new { InstallDirectory = "active-local", InstallSize = 22UL } } }
            } } }
        }));
        editions.GetField("document", Flags).SetValue(null, null);
        failNextAdd = true;
        var failed = false;
        try { Call("SwitchEdition", originalId, first); } catch (TargetInvocationException) { failed = true; }
        Check(failed && games.Count == 1 && games[originalId].InstallDirectory == "active-local", "Failed migration rolls database back without orphan editions");
        Check(!(bool)Call("IsIndependent", originalId), "Failed migration leaves legacy grouping recoverable");
        Check((bool)Call("SwitchEdition", originalId, first), "Legacy group migrates on switch");
        Check(games.Count == 2 && games.ContainsKey(second), "Legacy edition becomes separate database identity");
        Check(games[originalId].InstallDirectory == "steam-install" && games[second].InstallDirectory == "active-local", "Both legacy installations retained");
        Check(games[originalId].PluginId != Guid.Empty && games[second].IsCustomGame, "Migrated original retains integration, added edition local");
        Check(games[originalId].Playtime == 800 && games[second].Playtime == 0, "Historical shared time kept on original without duplication");
        Check(games[second].Description == "Previously shared metadata", "Existing shared metadata retained as initial child copy");
        games[second].Description = "Changed independently";
        Check(games[originalId].Description == "Previously shared metadata", "Migrated metadata independent after conversion");
        Check((Guid)Call("PrepareProfileForEdit", originalId) == originalId, "Editor receives selected physical game identity");
    }
    static void ValidateMarkup(string source)
    {
        var markup = System.Xml.Linq.XDocument.Load(Path.Combine(source, "Views", "MainWindow.xaml"));
        var ns = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var toggle = markup.Descendants().Single(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditLocalStorageEnabled");
        Check(toggle.Attribute("IsEnabled") == null, "Installation toggle no longer inherits parent-only XAML restriction");
        Check(markup.Descendants().Any(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditCopyEditionPage"), "Copy page button present beside edition selector");
    }

    sealed class MetadataModel
    {
        public object Platforms { get; set; }
        public object Genres { get; set; }
        public object Developers { get; set; }
        public object Publishers { get; set; }
        public object Features { get; set; }
        public object Tags { get; set; }
        public object AgeRatings { get; set; }
        public object Series { get; set; }
    }
    static void ValidatePageCopy()
    {
        var copier = theme.GetType("OsirisTheme.EditionPageCopy", true);
        var apply = copier.GetMethod("Apply", Flags);
        Action<Game, Game, string> copy = (from, to, page) => apply.Invoke(null, new object[] { from, to, page });
        var genre = Guid.NewGuid();
        var from = new Game { Name = "Source name", PluginId = Guid.NewGuid(), GameId = "platform-identity",
            Description = "Source overview", Version = "Source version", GenreIds = new List<Guid> { genre },
            GameActions = new System.Collections.ObjectModel.ObservableCollection<GameAction> {
                new GameAction { Name = "Source action", Path = "launcher", TrackingPath = "tracking.exe", TrackingMode = (TrackingMode)7 } },
            Links = new System.Collections.ObjectModel.ObservableCollection<Link> { new Link("Source link", "https://example.com") },
            Playtime = 4200, PreScript = "source script", InstallDirectory = "source install" };
        var persisted = new Game { Name = "Target name", PluginId = Guid.Empty, GameId = "target-identity", Notes = "Keep other page draft" };
        var draft = persisted.GetCopy();
        copy(from, draft, "General");
        Check(draft.Name == from.Name && draft.Description == from.Description && draft.GenreIds.SequenceEqual(from.GenreIds), "General page copies its metadata");
        Check(draft.Id == persisted.Id && draft.GameId == persisted.GameId && draft.PluginId == persisted.PluginId, "Page copy cannot replace edition identity or integration");
        Check(draft.Notes == persisted.Notes && draft.Playtime == 0 && string.IsNullOrEmpty(draft.InstallDirectory), "General copy leaves other pages unchanged");
        Check(persisted.Name == "Target name" && string.IsNullOrEmpty(persisted.Description), "Copy changes draft only; Cancel has nothing to persist");
        draft.GenreIds.Clear();
        Check(from.GenreIds.Count == 1, "Copied metadata collections are not mirrored");
        from.Description = "Later source edit";
        Check(draft.Description == "Source overview", "Later source edits do not change copied page");
        copy(from, draft, "Actions");
        draft.GameActions[0].TrackingPath = "target-only.exe";
        Check(from.GameActions[0].TrackingPath == "tracking.exe" && (int)draft.GameActions[0].TrackingMode == 7,
            "Actions deep-copy independent tracking path/playtime choices");
        copy(from, draft, "Links"); draft.Links[0].Name = "Target link";
        Check(from.Links[0].Name == "Source link", "Links deep-copy without shared objects");
        copy(from, draft, "Scripts");
        Check(draft.PreScript == from.PreScript && persisted.PreScript != draft.PreScript, "Script page remains staged");
        copy(from, draft, "Advanced");
        Check(draft.Playtime == from.Playtime && draft.PluginId == Guid.Empty && draft.GameId == "target-identity", "Advanced copies editable fields but protects native identities");
        copy(from, draft, "Installation");
        Check(draft.InstallDirectory == from.InstallDirectory && draft.IsCustomGame, "Installation copy keeps local management eligibility");
        var listType = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Playnite")
            .GetType("System.SelectableDbItemList", false);
        if (listType == null) throw new Exception("Native metadata list type unavailable");
        var model = new MetadataModel();
        foreach (var property in typeof(MetadataModel).GetProperties())
            property.SetValue(model, Activator.CreateInstance(listType, new object[] {
                new DatabaseObject[] { new Genre { Id = genre, Name = "Selected genre" }, new Genre { Id = Guid.NewGuid(), Name = "Other genre" } },
                new Guid[0], new Guid[0], false }));
        copier.GetMethod("ApplyGeneralSelections", Flags).Invoke(null, new object[] { model, from });
        var rows = (IEnumerable)model.Genres;
        var selectedRows = rows.Cast<object>().Where(row => Equals(row.GetType().GetProperty("Selected").GetValue(row), true)).ToList();
        Check(selectedRows.Count == 1 && ((DatabaseObject)selectedRows[0].GetType().GetProperty("Item").GetValue(selectedRows[0])).Id == genre,
            "Native metadata picker selections reflect copied General fields");
    }

    static void ValidatePageSharing()
    {
        games.Clear();
        var source = new Game { Id = Guid.NewGuid(), Name = "Sharing source", PluginId = Guid.NewGuid(), GameId = "remote-id",
            Playtime = 1800, PlayCount = 4, Description = "Source description" };
        games[source.Id] = source;
        Check((bool)Call("ApplySelection", source.Id, new[] { "Original", "Local", "Third" }), "Create three sharing-test profiles");
        var members = games.Values.Where(g => g.Id != source.Id).ToList();
        var local = members[0]; var third = members[1];
        Check((bool)Call("CanSharePage", "Advanced") && (bool)Call("CanSharePage", "Trophies") && !(bool)Call("CanSharePage", "DangerZone"),
            "Sharing supports Advanced/Trophies and excludes destructive pages");
        Check(!(bool)Call("IsPageShared", local.Id, "Advanced"), "New editions remain independent by default");
        Call("InitializePageSharing");
        var copier = theme.GetType("OsirisTheme.EditionPageCopy", true).GetMethod("Apply", Flags);
        var draft = local.GetCopy(); copier.Invoke(null, new object[] { source, draft, "Advanced" });
        Check(!(bool)Call("IsPageShared", local.Id, "Advanced") && games[local.Id].Playtime == 0,
            "Filling sharing preview leaves saved graph and native record untouched (Cancel)");
        Call("CommitPageSharing", local.Id, new Dictionary<string, Guid?> { ["Advanced"] = source.Id });
        API.Instance.Database.Games.Update(draft);
        Check(!(bool)Call("IsPageShared", source.Id, "Advanced") && (bool)Call("IsPageShared", local.Id, "Advanced") && (Guid)Call("GetSharedPageOwner", local.Id, "Advanced") == source.Id,
            "Only the dependent edition is mirrored, with the selected source as owner");
        Check(games[local.Id].Playtime == 1800 && games[local.Id].PlayCount == 4, "Selected source supplies initial shared Advanced values");
        var edit = games[local.Id].GetCopy(); edit.Playtime = 2400; edit.PlayCount = 5;
        API.Instance.Database.Games.Update(edit);
        Check(games[source.Id].Playtime == 2400 && games[source.Id].PlayCount == 5 && games[local.Id].Playtime == 2400,
            "Played time/count from either physical edition contribute to shared Advanced counters");
        Check(games[source.Id].PluginId == source.PluginId && games[local.Id].PluginId == Guid.Empty && games[source.Id].GameId == "remote-id",
            "Sharing never changes native profile/integration identities");
        Check(string.IsNullOrEmpty(games[local.Id].Description) && games[third.Id].Playtime == 0,
            "Other pages and unlinked third edition remain independent");
        edit = games[source.Id].GetCopy(); edit.Notes = "Updated from original"; edit.Playtime = 2400; edit.PlayCount = 5;
        API.Instance.Database.Games.Update(edit);
        Check(games[local.Id].Notes == edit.Notes && games[local.Id].Playtime == 2400,
            "Source updates flow into the mirrored edition");
        Call("SetPageSharing", third.Id, "Advanced", local.Id);
        Call("SynchronizeSharedPageFields", null, games[local.Id]);
        Check(games[third.Id].Playtime == 2400 && (Guid)Call("GetSharedPageOwner", third.Id, "Advanced") == source.Id,
            "Joining a shared member uses its existing sharing group");
        editions.GetField("document", Flags).SetValue(null, null);
        Check((bool)Call("IsPageShared", third.Id, "Advanced"), "Sharing graph survives document reload");
        Call("SetPageSharing", local.Id, "Advanced", null);
        edit = games[source.Id].GetCopy(); edit.Playtime = 3000; API.Instance.Database.Games.Update(edit);
        Check(games[local.Id].Playtime == 2400 && games[third.Id].Playtime == 2400,
            "Disabling a mirror keeps a copy; dependents continue following their selected edition");
        var foreign = new Game { Id = Guid.NewGuid(), Name = "Unrelated game" }; games[foreign.Id] = foreign;
        var rejected = false;
        try { Call("SetPageSharing", local.Id, "Advanced", foreign.Id); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected && !(bool)Call("IsPageShared", local.Id, "Advanced"), "Cannot share across unrelated game groups");
        rejected = false;
        typeof(Game).GetProperty("IsRunning").SetValue(games[source.Id], true);
        try { Call("SetPageSharing", local.Id, "Advanced", source.Id); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected, "Running group blocks sharing changes");
        typeof(Game).GetProperty("IsRunning").SetValue(games[source.Id], false);
        rejected = false;
        try { Call("CommitPageSharing", local.Id, new Dictionary<string, Guid?> { ["General"] = source.Id, ["Advanced"] = foreign.Id }); }
        catch (TargetInvocationException) { rejected = true; }
        Check(rejected && !(bool)Call("IsPageShared", local.Id, "General"), "Failed batch restores complete sharing graph");
        var catalogues = Path.Combine(sandbox, "extensions-data", "72ba1fc9-490b-4fa4-96f0-0a154b56c1db", "catalogues");
        Directory.CreateDirectory(catalogues);
        File.WriteAllText(Path.Combine(catalogues, source.Id + ".json"), JsonConvert.SerializeObject(new {
            AppId = 292030, SharedSourcePluginId = (Guid?)null, NativeProvider = source.PluginId.ToString("D"), Trophies = new[] {
                new { Id = "earned", Name = "Earned trophy", NativeUnlockedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) } } }));
        Call("SetPageSharing", local.Id, "Trophies", source.Id);
        Check((Guid)Call("GetSharedPageOwner", local.Id, "Trophies") == source.Id, "Linked trophies use a single source catalogue");
        Call("SetPageSharing", local.Id, "Trophies", null);
        var retained = JObject.Parse(File.ReadAllText(Path.Combine(catalogues, local.Id + ".json")));
        Check((Guid)retained["SharedSourcePluginId"] == source.PluginId && retained["Trophies"][0]["NativeUnlockedUtc"] != null,
            "Unlink preserves earned progress and original provider filtering in independent snapshot");
        Call("SetPageSharing", local.Id, "Trophies", source.Id);
        Call("SetPageSharing", third.Id, "Trophies", local.Id);
        Check((bool)Call("RemoveEdition", source.Id, source.Id, local.Id), "Shared canonical edition can be removed");
        Check(File.Exists(Path.Combine(catalogues, local.Id + ".json")) &&
            (Guid)Call("GetSharedPageOwner", third.Id, "Trophies") == local.Id,
            "Deleting shared canonical member transfers catalogue to survivor");
        Check(games[third.Id].Playtime == 2400, "Deleting original preserves independent Advanced values");
        var markup = System.Xml.Linq.XDocument.Load(Path.Combine("Development/Contents and Stuff/Development (AI)/Development/ThemeSource", "Views", "MainWindow.xaml"));
        var ns = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var toggle = markup.Descendants().Single(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditMirrorEnabled");
        var sourceBox = markup.Descendants().Single(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditMirrorSource");
        Check((string)toggle.Attribute("Style") == "{StaticResource OsirisGameEditCheckBoxStyle}" && (string)sourceBox.Attribute("Style") == "{StaticResource OsirisGameEditComboBoxStyle}",
            "Page mirror reuses canonical switch and dropdown styles");
        Check(!markup.Descendants().Any(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditEditions" || (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditShareEditionPage"),
            "Obsolete General editions field and Share page button removed");
    }

    public sealed class DraftNativeModel
    {
        public Game EditingGame { get; set; }
        public List<Guid> SelectedGenreIds { get; set; } = new List<Guid>();
        public bool Closed, Saved;
        public void CloseView(bool first, bool second) { Closed = true; }
        public void ConfirmDialog(bool close)
        {
            EditingGame.GenreIds = SelectedGenreIds.ToList();
            API.Instance.Database.Games.Update(EditingGame.GetCopy()); Saved = true; Closed = true;
        }
    }
    public sealed class DraftTrophyView : System.Windows.Controls.UserControl
    {
        public Guid SavedId;
        public System.Windows.Controls.TextBox Input = new System.Windows.Controls.TextBox();
        public DraftTrophyView() { Content = Input; }
        public void SaveForOsiris(Guid id) { SavedId = id; }
    }
    static void ValidateEditorDrafts()
    {
        games.Clear();
        var standard = new Game { Id = Guid.NewGuid(), Name = "Standard saved", PluginId = Guid.NewGuid(), GameId = "native-id" };
        games[standard.Id] = standard;
        Call("ApplySelection", standard.Id, new[] { "Standard", "Premium" });
        var premium = games.Values.Single(g => g.Id != standard.Id);
        var type = theme.GetType("OsirisTheme.GameEditPageController+GameEditPageState", true);
        var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var root = new System.Windows.Controls.Grid(); NameScope.SetNameScope(root, new NameScope());
        var page = new System.Windows.Controls.StackPanel(); root.Children.Add(page);
        var title = new System.Windows.Controls.TextBox { Name = "PART_OsirisGameEditName" }; page.Children.Add(title); root.RegisterName(title.Name, title);
        title.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("EditingGame.Name") {
            Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus });
        var enabled = new System.Windows.Controls.CheckBox { Name = "PART_OsirisGameEditStatsInsightsEnabled" }; page.Children.Add(enabled); root.RegisterName(enabled.Name,enabled);
        var trophies = new System.Windows.Controls.ContentControl { Name = "PART_OsirisGameEditTrophiesHost" }; page.Children.Add(trophies); root.RegisterName(trophies.Name,trophies);
        var selector = new System.Windows.Controls.ComboBox { Name = "PART_OsirisGameEditEdition" }; root.Children.Add(selector); root.RegisterName(selector.Name,selector);
        var general = new System.Windows.Controls.Button { Name = "PART_OsirisGameEditGeneral" };
        var state = Activator.CreateInstance(type, instanceFlags, null, new object[] { root }, null);
        Action<string, object> set = (name,value) => type.GetField(name,instanceFlags).SetValue(state,value);
        Func<string, object> get = name => type.GetField(name,instanceFlags).GetValue(state);
        Func<string, object[], object> invoke = (name,arguments) => type.GetMethod(name,instanceFlags).Invoke(state,arguments);
        set("generalButton",general); set("nameTextBox", title); set("editionComboBox",selector);
        ((IDictionary)get("pages")).Add(general,page);
        var choices = ((IEnumerable)Call("GetEditions",standard.Id)).Cast<object>().ToList(); selector.ItemsSource=choices;
        var labels = (IList)get("stagedEditionLabels"); labels.Add("Standard"); labels.Add("Premium");
        var cache = (IDictionary)get("editionDrafts");
        DraftNativeModel activeModel = null;
        Action<Game,string,bool> activate = (saved,name,toggle) => {
            activeModel = new DraftNativeModel { EditingGame=saved.GetCopy(), SelectedGenreIds=new List<Guid> { Guid.NewGuid() } };
            activeModel.EditingGame.Description=name+" overview";
            activeModel.EditingGame.Notes=name+" notes"; activeModel.EditingGame.Playtime=(ulong)(toggle ? 120 : 240);
            activeModel.EditingGame.GameActions = new System.Collections.ObjectModel.ObservableCollection<GameAction> { new GameAction { Name=name+" action", Path=name+".exe" } };
            set("game",saved);set("stagedGame",activeModel.EditingGame);set("nativeEditModel",activeModel);set("nativeEditModelClosed",false);set("activeEditionId",saved.Id);
            page.DataContext=activeModel; title.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty,name);
            enabled.IsChecked=toggle;enabled.Tag=saved.Id;
            var view=new DraftTrophyView();view.Input.Text=name+" unsaved catalogue";trophies.Content=view;trophies.Tag=saved.Id;
        };
        activate(standard,"Standard draft",true); var standardModel=activeModel; var standardTrophies=trophies.Content;
        Check(standardModel.EditingGame.Name=="Standard saved", "Lost-focus name edit initially remains in control");
        var first=invoke("CaptureEditionDraft",new object[0]);cache.Add(standard.Id,first);
        Check(standardModel.EditingGame.Name=="Standard draft", "Switch capture flushes focused text binding into the native draft");
        var temp=Path.Combine(sandbox,"draft-owned-image.tmp");File.WriteAllText(temp,"temporary draft fixture");
        var owned=(ICollection<string>)get("temporaryLogoPaths");owned.Add(temp);
        invoke("SuspendEditionDraft",new object[0]);
        Check(!standardModel.Closed && File.Exists(temp), "Suspending does not cancel the native model or delete pending media");
        activate(premium,"Premium draft",false);var premiumModel=activeModel;var premiumTrophies=trophies.Content;
        var pending=(IDictionary)get("pendingPageSharing");pending.Add("Scripts",standard.Id);
        Call("SwitchEdition",standard.Id,premium.Id);
        Action<Guid> switchTo = id => {
            selector.SelectedItem=choices.Single(c=>(Guid)c.GetType().GetProperty("EditionId").GetValue(c)==id);
            invoke("OnEditionChanged",new object[] { selector, new System.Windows.Controls.SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,new object[0],new object[0]) });
        };
        switchTo(standard.Id);
        Check(((IDictionary)get("pendingPageSharing")).Count==0 && !(bool)Call("IsPageShared",premium.Id,"Scripts"),"Pending sharing remains per-edition and is not committed by switching");
        Check(title.Text=="Standard draft" && ReferenceEquals(get("nativeEditModel"),standardModel),"Actual edition-switch handler restores Standard text and original native model");
        Check(ReferenceEquals(trophies.Content,standardTrophies) && ((DraftTrophyView)trophies.Content).Input.Text.Contains("Standard draft"),"Trophies unimported text and staged editor instance survive switching");
        Check(enabled.IsChecked==true && Equals(enabled.Tag,standard.Id),"Per-edition extension control value and save identity restored");
        Check(((Game)get("stagedGame")).Playtime==120 && ((Game)get("stagedGame")).GameActions[0].Path=="Standard draft.exe","Advanced and Actions drafts survive edition switch");
        Check(games[standard.Id].Name=="Standard saved" && games[premium.Id].Name!="Premium draft", "Edition switching does not auto-save either game");
        title.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty,"Standard edited twice");
        switchTo(premium.Id);
        Check(title.Text=="Premium draft" && ReferenceEquals(get("nativeEditModel"),premiumModel),"Premium draft survives round trip independently");
        Check(((IDictionary)get("pendingPageSharing")).Contains("Scripts"),"Unsaved sharing choice returns with its edition");
        ((IDictionary)get("pendingPageSharing")).Clear();
        Check(enabled.IsChecked==false && ReferenceEquals(trophies.Content,premiumTrophies),"Premium extension editor and setting remain distinct");
        switchTo(standard.Id);Check(title.Text=="Standard edited twice","Repeated switches retain newer Standard edits");
        switchTo(premium.Id);
        Check((bool)invoke("SaveEditionSession",new object[0]),"Single Save commits the complete multi-edition session");
        Check(games[standard.Id].Name=="Standard edited twice" && games[premium.Id].Name=="Premium draft","Single Save persists both edition titles");
        Check(games[standard.Id].GenreIds.SequenceEqual(standardModel.SelectedGenreIds) && games[premium.Id].GenreIds.SequenceEqual(premiumModel.SelectedGenreIds),"Native metadata selector models retain separate selections through Save");
        Check(games[standard.Id].Playtime==120 && games[premium.Id].Playtime==240,"Saved Advanced data stays per edition");
        Check(games[standard.Id].Description=="Standard draft overview" && games[premium.Id].Description=="Premium draft overview","Saved General descriptions remain distinct after multiple switches");
        Check(((DraftTrophyView)standardTrophies).SavedId==standard.Id && ((DraftTrophyView)premiumTrophies).SavedId==premium.Id,"Trophies draft saves target the correct profile");
        Check(standardModel.Saved && premiumModel.Saved && cache.Count==0,"Every native model saved and session cache released");
        // Cancel releases cached native models and only owned draft files.
        activate(games[standard.Id],"Cancelled Standard",true);var cancelled=activeModel;
        owned.Add(temp);var cancelDraft=invoke("CaptureEditionDraft",new object[0]);cache.Add(standard.Id,cancelDraft);
        invoke("SuspendEditionDraft",new object[0]);activate(games[premium.Id],"Cancelled Premium",false);
        invoke("DiscardEditionSession",new object[0]);
        Check(cancelled.Closed && !cancelled.Saved && !File.Exists(temp),"Cancel closes suspended native draft and removes only pending temporary media");
        Check(games[standard.Id].Name=="Standard edited twice" && games[premium.Id].Name=="Premium draft","Cancel leaves persisted edition data intact");
        // Invalid drafts are selected before any edition is committed.
        var freshSaved=games[standard.Id];activate(freshSaved," ",true);cache.Add(standard.Id,invoke("CaptureEditionDraft",new object[0]));
        invoke("SuspendEditionDraft",new object[0]);activate(games[premium.Id],"Should not save yet",false);
        Check(!(bool)invoke("SaveEditionSession",new object[0]) && games[premium.Id].Name=="Premium draft","Invalid inactive edition blocks all saves before committing another draft");
        Check(((Game)get("game")).Id==standard.Id && title.Text==" ","Invalid draft is restored for correction");
        invoke("DiscardEditionSession",new object[0]);
    }

    static void ValidateEditionControls()
    {
        games.Clear();
        var original = new Game { Id=Guid.NewGuid(), Name="Edition controls", PluginId=Guid.NewGuid(), Description="Original overview", Playtime=999, Notes="Original notes" };
        games[original.Id]=original;
        var added=Call("CreateNamedEdition",original.Id,"Premium");
        var addedId=(Guid)added.GetType().GetProperty("EditionId").GetValue(added);
        var premium=(Game)Call("GetEditionProfile",original.Id,addedId);
        Check(premium != null && premium.IsCustomGame && premium.Playtime==0 && string.IsNullOrEmpty(premium.Description) && premium.GameActions==null,
            "Add Edition creates an empty independent local profile without inheriting metadata/actions/time");
        Check(games[original.Id].Description=="Original overview" && games[original.Id].PluginId==original.PluginId,
            "Adding an edition preserves the existing profile and integration");
        var rejected=false;try { Call("CreateNamedEdition",original.Id,"premium"); } catch(TargetInvocationException) { rejected=true; }
        Check(rejected && games.Count==2,"Duplicate edition names rejected without extra profiles");
        Call("SwitchEdition",original.Id,addedId);
        Check((Guid)Call("ResolveEditionForRemoval",original.Id)==premium.Id,"Grid/list stale grouping key resolves deletion to the selected physical edition");
        var gridTargets=((IEnumerable)Call("ResolveEditionRemovalTargets",new[]{original,premium},true)).Cast<Game>().ToList();
        var editorTargets=((IEnumerable)Call("ResolveEditionRemovalTargets",new[]{original},false)).Cast<Game>().ToList();
        Check(gridTargets.Count==1 && gridTargets[0].Id==premium.Id && editorTargets.Count==1 && editorTargets[0].Id==original.Id,
            "Actual removal target resolver deduplicates grouped grid entries and preserves explicit edit-menu selection");
        Call("SetPageSharing",premium.Id,"Advanced",original.Id);
        rejected=false;try { Call("SetPageSharing",original.Id,"Advanced",premium.Id); } catch(TargetInvocationException) { rejected=true; }
        Check(rejected && !(bool)Call("IsPageShared",original.Id,"Advanced"),"Mirror cycles rejected without changing the source");
        rejected=false;
        try { Call("ValidateMirrorDrafts",new Dictionary<Guid,Dictionary<string,Guid?>> {
            [original.Id]=new Dictionary<string,Guid?> { ["General"]=premium.Id },
            [premium.Id]=new Dictionary<string,Guid?> { ["General"]=original.Id } }); }
        catch(TargetInvocationException) { rejected=true; }
        Check(rejected && !(bool)Call("IsPageShared",original.Id,"General") && !(bool)Call("IsPageShared",premium.Id,"General"),"Unsaved mirror cycles across editions are rejected before any save");
        Call("SetPageSharing",premium.Id,"Advanced",null);
        var type=theme.GetType("OsirisTheme.GameEditPageController+GameEditPageState",true);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var root=new System.Windows.Controls.Grid();NameScope.SetNameScope(root,new NameScope());
        var panel=new System.Windows.Controls.Grid();root.Children.Add(panel);
        var toggle=new System.Windows.Controls.CheckBox {Name="PART_OsirisGameEditMirrorEnabled"};root.RegisterName(toggle.Name,toggle);root.Children.Add(toggle);
        var sourceBox=new System.Windows.Controls.ComboBox {Name="PART_OsirisGameEditMirrorSource"};root.RegisterName(sourceBox.Name,sourceBox);root.Children.Add(sourceBox);
        var advanced=new System.Windows.Controls.Button {Name="PART_OsirisGameEditAdvanced",Padding=new Thickness(36,0,8,0),Content=new System.Windows.Controls.TextBlock {Text="Advanced"}};
        var state=Activator.CreateInstance(type,flags,null,new object[]{root},null);
        Action<string,object> set=(name,value)=>type.GetField(name,flags).SetValue(state,value);
        Func<string,object> get=name=>type.GetField(name,flags).GetValue(state);
        Action<string> run=name=>type.GetMethod(name,flags).Invoke(state,new object[0]);
        var resetStats=new System.Windows.Controls.Button();set("resetStatsButton",resetStats);
        set("game",premium);set("stagedGame",premium.GetCopy());set("activeEditionId",addedId);set("selectedPageButton",advanced);
        ((IDictionary)get("pages")).Add(advanced,panel);
        var danger=new System.Windows.Controls.Button {Name="PART_OsirisGameEditDangerZone",Content="Danger Zone"};
        ((IDictionary)get("pages")).Add(danger,new System.Windows.Controls.Grid());
        foreach(var choice in ((IEnumerable)Call("GetEditions",premium.Id))) ((IList)get("editionChoices")).Add(choice);
        run("InitializeEditionControls");run("UpdateMirrorControls");
        Check(Equals(danger.Content,"Danger Zone") && !((IDictionary)get("mirrorBadges")).Contains(danger),"Danger Zone retains its string-template label and never receives a link wrapper");
        Check(toggle.IsChecked==false && panel.IsEnabled && !sourceBox.IsEnabled,"Mirror starts disabled with independent editable page");
        toggle.IsChecked=true;
        Check(!panel.IsEnabled && sourceBox.IsEnabled && !resetStats.IsEnabled && ((Game)get("stagedGame")).Notes=="Original notes", "Enabling mirror fills from source, makes page read-only and disables indirect target stats reset");
        Check(!(bool)Call("IsPageShared",premium.Id,"Advanced") && ((IDictionary)get("pendingPageSharing")).Contains("Advanced"),"Mirror switch stages its choice until Save");
        var badges=(IDictionary)get("mirrorBadges");var badge=(System.Windows.Controls.Viewbox)badges[advanced];
        var paths=((System.Windows.Controls.Canvas)badge.Child).Children.Cast<System.Windows.Shapes.Path>().ToList();
        Check(badge.Visibility==Visibility.Visible && paths.Count==2 && ((System.Windows.Media.SolidColorBrush)paths[0].Stroke).Color.G==205,"Provided two-path link vector is displayed in green on mirrored navigation item");
        toggle.IsChecked=false;
        Check(panel.IsEnabled && resetStats.IsEnabled && badge.Visibility==Visibility.Collapsed && !sourceBox.IsEnabled && ((Game)get("stagedGame")).Notes=="Original notes", "Turning mirror off keeps copied values, unlocks editing and hides indicator");
        Call("SetPageSharing",original.Id,"Advanced",premium.Id);
        run("UpdateMirrorControls");
        Check(sourceBox.Items.Count==0 && !toggle.IsEnabled,"Source dropdown excludes editions that would create a mirror cycle");
        run("DisposeEditionControls");
        Check(advanced.Content is System.Windows.Controls.TextBlock,"Disposing restores original navigation content without duplicate icons");
        // Cancel a freshly created edition removes only that provisional profile.
        var provisional=Call("CreateNamedEdition",original.Id,"Provisional");
        var provisionalId=(Guid)provisional.GetType().GetProperty("EditionId").GetValue(provisional);
        ((ICollection<Guid>)get("createdSessionEditions")).Add(provisionalId);
        type.GetMethod("OnRootIsVisibleChanged",flags).Invoke(state,new object[]{root,new DependencyPropertyChangedEventArgs(UIElement.IsVisibleProperty,true,false)});
        Check(!games.ContainsKey(provisionalId) && games.ContainsKey(original.Id) && games.ContainsKey(premium.Id),"Leaving Edit cancels only the unsaved new edition, preserving existing editions");
    }

    static void ValidateLegacyRemoval(string settings)
    {
        games.Clear();
        var originalId=Guid.NewGuid();var first=Guid.NewGuid();var selected=Guid.NewGuid();var plugin=Guid.NewGuid();
        games[originalId]=new Game {Id=originalId,Name="Legacy deletion",PluginId=plugin,GameId="native",Description="Keep original"};
        File.WriteAllText(Path.Combine(settings,"GameEditions.json"),JsonConvert.SerializeObject(new {
            SchemaVersion=3,Profiles=new[] {new {GameId=originalId,ActiveEditionId=selected,Editions=new[] {
                new {EditionId=first,Label="Standard",IsDefault=true,Pages=new {}},
                new {EditionId=selected,Label="Premium",IsDefault=false,Pages=new {}}
            }}}
        }));
        editions.GetField("document",Flags).SetValue(null,null);
        Check((Guid)Call("ResolveEditionForRemoval",originalId)==selected && games.Count==2,
            "Legacy grid deletion first creates independent records and resolves only selected edition");
        Call("RemoveProfile",selected);games.Remove(selected);
        Check(games.ContainsKey(originalId) && games[originalId].PluginId==plugin && games[originalId].Description=="Keep original" && ((IEnumerable)Call("GetEditions",originalId)).Cast<object>().Count()==1,
            "Deleting selected legacy edition preserves original profile and grouping after conversion");
    }

    static void ValidateAllPageMirrors(string extensionStage)
    {
        games.Clear();
        var source=new Game {Id=Guid.NewGuid(),Name="Shared page source",PluginId=Guid.NewGuid(),GameId="source-id",Playtime=900};
        games[source.Id]=source;Call("ApplySelection",source.Id,new[]{"Standard","Premium"});
        var target=games.Values.Single(g=>g.Id!=source.Id);target.GameId="target-id";target.InstallDirectory="own-install";
        var pages=new[]{"General","Advanced","Media","Links","Actions","Scripts","Gallery","Screenshots","StatsInsights","Trophies","HowLongToBeat","Exophase"};
        Check(pages.All(page=>(bool)Call("CanSharePage",page)) && !(bool)Call("CanSharePage","Installation") && !(bool)Call("CanSharePage","DangerZone"),
            "Every requested page supports mirroring except unique Installation and Danger Zone");
        var from=API.Instance.Database.GetFileStoragePath(source.Id);var to=API.Instance.Database.GetFileStoragePath(target.Id);
        File.WriteAllText(Path.Combine(from,"Icon.png"),"source artwork fixture");
        source.Icon=Path.Combine(source.Id.ToString(),"Icon.png");API.Instance.Database.Games.Update(source);
        Call("SetPageSharing",target.Id,"Media",source.Id);Call("SynchronizeSharedPageFields",null,source);
        Check(games[target.Id].Icon==source.Icon && (Guid)Call("GetStorageId",target.Id,"Media")==source.Id,"Media shares source artwork and role/rotation storage");
        Call("SetPageSharing",target.Id,"Media",null);
        Check(games[target.Id].Icon.StartsWith(target.Id.ToString()) && File.ReadAllText(Path.Combine(to,"Icon.png"))=="source artwork fixture" && File.Exists(Path.Combine(from,"Icon.png")),
            "Unlinking Media clones owned artwork and rebases references without removing source assets");
        File.WriteAllText(Path.Combine(from,"OsirisStatsInsights.ini"),"Enabled=False");
        File.WriteAllText(Path.Combine(to,"OsirisStatsInsights.ini"),"Enabled=True");
        Call("SetPageSharing",target.Id,"StatsInsights",source.Id);
        Check((Guid)Call("GetStorageId",target.Id,"StatsInsights")==source.Id,"Stats Insights has a scoped settings owner");
        Directory.CreateDirectory(Path.Combine(from,"Screenshots"));File.WriteAllText(Path.Combine(from,"Screenshots","fixture.png"),"synthetic fixture, not a screen capture");
        File.WriteAllText(Path.Combine(from,"OsirisScreenshotsGallery.ini"),"Enabled=False");
        Call("SetPageSharing",target.Id,"Screenshots",source.Id);
        Check((Guid)Call("GetStorageId",target.Id,"Screenshots")==source.Id,"Screenshots resolves the selected page owner");
        Call("SetPageSharing",target.Id,"Screenshots",null);
        Check(File.Exists(Path.Combine(to,"Screenshots","fixture.png")) && File.Exists(Path.Combine(from,"Screenshots","fixture.png")),"Screenshots unlink keeps an independent snapshot without deleting originals");
        File.WriteAllText(Path.Combine(from,"GalleryImage1.png"),"gallery image");File.WriteAllText(Path.Combine(from,"Trailer1.mp4"),"gallery trailer");
        Call("SetPageSharing",target.Id,"Gallery",source.Id);Call("SetPageSharing",target.Id,"Gallery",null);
        Check(File.Exists(Path.Combine(to,"GalleryImage1.png")) && File.Exists(Path.Combine(to,"Trailer1.mp4")),"Gallery unlink preserves artwork, trailers and settings in target storage");
        Call("SetPageSharing",target.Id,"Advanced",source.Id);Call("SynchronizeSharedPageFields",null,source);
        Check(games[target.Id].Playtime==900 && games[target.Id].Id==target.Id && games[target.Id].PluginId==Guid.Empty && games[target.Id].GameId=="target-id" && games[target.Id].InstallDirectory=="own-install",
            "Advanced shares played time while database ID, native game ID, library ownership and Installation remain unique");
        if (string.IsNullOrEmpty(extensionStage)) return;
        var staged=JArray.Parse(File.ReadAllText(Path.Combine(extensionStage,"manifest.json")));
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        foreach(var page in new[]{"HowLongToBeat","Exophase"})
        {
            var assembly=Assembly.LoadFrom((string)staged.Single(e=>(string)e["Page"]==page)["Staged"]);
            var type=assembly.GetTypes().Single(t=>t.Name==(page=="HowLongToBeat"?"CompletionTimeGameSettingsStore":"ExophaseGameSettingsStore"));
            var settingsType=assembly.GetTypes().Single(t=>t.Name==(page=="HowLongToBeat"?"CompletionTimeGameSettings":"ExophaseGameSettings"));
            var id=page=="HowLongToBeat"?"fba3e63d-d1a1-4b9d-91c6-091a1220377d":"26131977-669a-4ef7-a66c-026122a24089";
            var folder=Path.Combine(sandbox,"extensions-data","Extras",page+"_"+id);
            var store=Activator.CreateInstance(type,flags,null,new object[]{folder},null);
            var value=Activator.CreateInstance(settingsType,true);settingsType.GetProperty("Enabled").SetValue(value,false);
            type.GetMethod("Save",flags).Invoke(store,new object[]{source.Id,value});
            Call("SetPageSharing",target.Id,page,source.Id);
            var loaded=type.GetMethod("Load",flags).Invoke(store,new object[]{target.Id});
            Check(!(bool)settingsType.GetProperty("Enabled").GetValue(loaded),page+" installed compatibility adapter reads source settings without changing native identity");
            Call("SetPageSharing",target.Id,page,null);
            var reopened=Activator.CreateInstance(type,flags,null,new object[]{folder},null);
            loaded=type.GetMethod("Load",flags).Invoke(reopened,new object[]{target.Id});
            Check(!(bool)settingsType.GetProperty("Enabled").GetValue(loaded),page+" unlink materializes its own cached-settings snapshot");
        }
        var shots=Assembly.LoadFrom((string)staged.Single(e=>(string)e["Page"]=="Screenshots")["Staged"]);
        var collector=shots.GetTypes().Single(t=>t.Name=="ScreenshotCollector");
        Call("SetPageSharing",target.Id,"Screenshots",source.Id);
        var folderPath=(string)collector.GetMethod("GameFolder",flags).Invoke(null,new object[]{sandbox,target.Id});
        Check(folderPath==Path.Combine(sandbox,"library","files",source.Id.ToString("D"),"Screenshots"),"Screenshot capture adapter stores a launched target's captures in its explicitly linked screenshot page");
        Call("SetPageSharing",target.Id,"Screenshots",null);
        folderPath=(string)collector.GetMethod("GameFolder",flags).Invoke(null,new object[]{sandbox,target.Id});
        Check(folderPath.Contains(target.Id.ToString("D")),"Disabling screenshot mirror restores per-edition capture destination");
        var stats=Assembly.LoadFrom((string)staged.Single(e=>(string)e["Page"]=="StatsInsights")["Staged"]);
        var providerType=stats.GetTypes().Single(t=>t.Name=="StatsInsightsProvider");
        var constructor=providerType.GetConstructors(flags).Single();
        var constructorArgs=new object[constructor.GetParameters().Length];constructorArgs[0]=API.Instance;
        var provider=constructor.Invoke(constructorArgs);
        Check(!(bool)providerType.GetMethod("Enabled",flags).Invoke(provider,new object[]{games[target.Id]}),"Installed Stats adapter respects linked enablement without linking its session ledger/database IDs");
        Call("SetPageSharing",target.Id,"StatsInsights",null);
        Check(File.ReadAllText(Path.Combine(to,"OsirisStatsInsights.ini")).Contains("False"),"Unlinked Stats page retains an independent settings copy");
    }

    static void ValidateBackgroundMetadataQueue()
    {
        // An unshown WPF window still enforces dispatcher ownership. No screen
        // capture, mouse input, real game, account or network request is involved.
        var window = new Window();
        Application.Current.MainWindow = window;
        var updates = theme.GetType("OsirisTheme.OsirisUpdateActions", true);
        var queue = updates.GetMethod("QueueIntegratedMetadataFill", Flags);
        System.Threading.Tasks.Task.Run(() => queue.Invoke(null, new object[] {
            new[] { new Game { PluginId = Guid.NewGuid() } } })).GetAwaiter().GetResult();
        var frame = new System.Windows.Threading.DispatcherFrame();
        window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        var pending = updates.GetField("pendingIntegratedMetadata", Flags).GetValue(null);
        Check((int)pending.GetType().GetProperty("Count").GetValue(pending) == 1,
            "Background library events queue metadata on UI dispatcher without cross-thread window access");
        Application.Current.MainWindow = null;
    }
}

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
        Check((bool)Call("IsPageShared", source.Id, "Advanced") && (Guid)Call("GetSharedPageOwner", local.Id, "Advanced") == source.Id,
            "Sharing applies to both members, with source as owner");
        Check(games[local.Id].Playtime == 1800 && games[local.Id].PlayCount == 4, "Selected source supplies initial shared Advanced values");
        var edit = games[local.Id].GetCopy(); edit.Playtime = 2400; edit.PlayCount = 5;
        API.Instance.Database.Games.Update(edit);
        Check(games[source.Id].Playtime == 2400 && games[source.Id].PlayCount == 5,
            "Actual SDK update event propagates playtime/count from local member");
        Check(games[source.Id].PluginId == source.PluginId && games[local.Id].PluginId == Guid.Empty && games[source.Id].GameId == "remote-id",
            "Sharing never changes native profile/integration identities");
        Check(string.IsNullOrEmpty(games[local.Id].Description) && games[third.Id].Playtime == 0,
            "Other pages and unlinked third edition remain independent");
        edit = games[source.Id].GetCopy(); edit.Notes = "Updated from original";
        API.Instance.Database.Games.Update(edit);
        Check(games[local.Id].Notes == edit.Notes && games[local.Id].Playtime == 2400,
            "Bidirectional updates change only modified shared fields");
        Call("SetPageSharing", third.Id, "Advanced", local.Id);
        Call("SynchronizeSharedPageFields", null, games[local.Id]);
        Check(games[third.Id].Playtime == 2400 && (Guid)Call("GetSharedPageOwner", third.Id, "Advanced") == source.Id,
            "Joining a shared member uses its existing sharing group");
        editions.GetField("document", Flags).SetValue(null, null);
        Check((bool)Call("IsPageShared", third.Id, "Advanced"), "Sharing graph survives document reload");
        Call("SetPageSharing", local.Id, "Advanced", null);
        edit = games[source.Id].GetCopy(); edit.Playtime = 3000; API.Instance.Database.Games.Update(edit);
        Check(games[local.Id].Playtime == 2400 && games[third.Id].Playtime == 3000,
            "Stop sharing retains independent copy and leaves other links intact");
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
        Check(games[third.Id].Playtime == 3000, "Deleting original preserves shared Advanced values");
        var markup = System.Xml.Linq.XDocument.Load(Path.Combine("Development/Contents and Stuff/Development (AI)/Development/ThemeSource", "Views", "MainWindow.xaml"));
        var ns = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var share = markup.Descendants().Single(e => (string)e.Attribute(ns + "Name") == "PART_OsirisGameEditShareEditionPage");
        Check((string)share.Attribute("Style") == "{StaticResource OsirisGameEditActionButtonStyle}" && (string)share.Attribute("Height") == "38",
            "Share page reuses canonical Copy page geometry/style");
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

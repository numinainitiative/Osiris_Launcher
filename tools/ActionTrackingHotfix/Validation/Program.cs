using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json;
using Playnite.SDK.Models;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using Playnite.Behaviors;

internal static class Program
{
    static int checks;
    [STAThread]
    static int Main(string[] args)
    {
        var app = Path.GetFullPath(args[0]);
        var core = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            var name = new AssemblyName(e.Name).Name;
            var path = name == "Playnite" ? core : Path.Combine(app, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { return Run(args, app, core); }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static int Run(string[] args, string app, string core)
    {
        new Application();
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        Assembly.LoadFrom(core);
        var theme = Assembly.LoadFrom(Path.GetFullPath(args[2]));
        var options = theme.GetType("OsirisTheme.ActionTrackingOptions", true);
        var ensure = options.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic);
        var disabled = (TrackingMode)5;
        var action = new GameAction { Name = "External launcher", Type = GameActionType.File, IsPlayAction = true, TrackingMode = disabled };
        var copy = JsonConvert.DeserializeObject<GameAction>(JsonConvert.SerializeObject(action));
        Check(copy.TrackingMode == disabled && copy.Name == action.Name, "saved action roundtrip preserves disabled mode");
        var combo = MakeCombo(action);
        ensure.Invoke(null, new object[] { combo }); ensure.Invoke(null, new object[] { combo });
        Check(combo.Items.OfType<SelectorBehaviors.EnumSelector>().Count(i => Equals(i.Value, disabled)) == 1 && Equals(combo.SelectedValue, disabled), "disabled choice is unique and saved selection restores");
        combo.SelectedValue = TrackingMode.Process;
        Check(action.TrackingMode == TrackingMode.Process, "normal tracking modes still bind two-way");
        combo.SelectedValue = disabled;
        Check(action.TrackingMode == disabled, "disabled choice binds to the action");
        var tracked = new GameAction { Type = GameActionType.File, TrackingMode = TrackingMode.Process };
        var other = MakeCombo(tracked); ensure.Invoke(null, new object[] { other });
        Check(tracked.TrackingMode == TrackingMode.Process && Equals(other.SelectedValue, TrackingMode.Process), "adding option does not rewrite another action");
        var emulator = MakeCombo(new GameAction { Type = GameActionType.Emulator }); ensure.Invoke(null, new object[] { emulator });
        Check(!emulator.Items.OfType<SelectorBehaviors.EnumSelector>().Any(i => Equals(i.Value, disabled)), "disabled mode is not exposed for emulator actions");
        var game = new Game { Name = "Time-only launch validation", InstallDirectory = "", Playtime = 123, PlayCount = 2 };
        var controllerType = Assembly.LoadFrom(core).GetType("Playnite.Controllers.GenericPlayController", true);
        var controller = (PlayController)Activator.CreateInstance(controllerType, new object[] { null, game, null, null });
        int started = 0, stopped = 0; ulong length = ulong.MaxValue, rawLength = 0;
        var ended = new ManualResetEvent(false);
        var suppress = Assembly.LoadFrom(core).GetType("Playnite.GamesEditor", true)
            .GetMethod("OsirisSuppressActionPlaytime", BindingFlags.Static | BindingFlags.NonPublic);
        typeof(PlayController).GetEvent("Started", BindingFlags.Instance | BindingFlags.NonPublic).GetAddMethod(true)
            .Invoke(controller, new object[] { new EventHandler<GameStartedEventArgs>((s, e) => { started++; game.PlayCount++; game.LastActivity = DateTime.Now; }) });
        typeof(PlayController).GetEvent("Stopped", BindingFlags.Instance | BindingFlags.NonPublic).GetAddMethod(true)
            .Invoke(controller, new object[] { new EventHandler<GameStoppedEventArgs>((s, e) => { rawLength = e.SessionLength; suppress.Invoke(null, new object[] { e }); stopped++; length = e.SessionLength; ended.Set(); }) });
        action.Path = Path.GetFullPath(args[3]);
        action.WorkingDir = Path.GetDirectoryName(action.Path);
        action.Arguments = "";
        action.InitialTrackingDelay = 0; action.TrackingFrequency = 100;
        var startingArgs = new OnGameStartingEventArgs();
        typeof(OnGameStartingEventArgs).GetProperty("SourceAction").SetValue(startingArgs, action, null);
        controllerType.GetMethod("Start", new[] { typeof(GameAction), typeof(bool), typeof(OnGameStartingEventArgs) })
            .Invoke(controller, new object[] { action, true, startingArgs });
        Check(ended.WaitOne(15000), "normal running-game monitor reaches actual process exit");
        Check(started == 1 && stopped == 1 && rawLength >= 1 && length == 0, "real lifecycle stays intact but positive elapsed time is suppressed");
        Check(game.Playtime == 123 && game.PlayCount == 3 && game.LastActivity != null, "play count and last-played lifecycle stay active while time is unchanged");
        ended.Reset(); started = 0; stopped = 0; rawLength = 0;
        game.InstallDirectory = Path.GetDirectoryName(action.Path);
        var directoryController = (PlayController)Activator.CreateInstance(controllerType, new object[] { null, game, null, null });
        typeof(PlayController).GetEvent("Started", BindingFlags.Instance | BindingFlags.NonPublic).GetAddMethod(true)
            .Invoke(directoryController, new object[] { new EventHandler<GameStartedEventArgs>((s, e) => started++) });
        typeof(PlayController).GetEvent("Stopped", BindingFlags.Instance | BindingFlags.NonPublic).GetAddMethod(true)
            .Invoke(directoryController, new object[] { new EventHandler<GameStoppedEventArgs>((s, e) => { rawLength = e.SessionLength; suppress.Invoke(null, new object[] { e }); stopped++; length = e.SessionLength; ended.Set(); }) });
        controllerType.GetMethod("Start", new[] { typeof(GameAction), typeof(bool), typeof(OnGameStartingEventArgs) })
            .Invoke(directoryController, new object[] { action, true, startingArgs });
        Check(ended.WaitOne(15000) && started == 1 && stopped == 1 && rawLength >= 1 && length == 0,
            "valid installation directory detects real game lifecycle and suppresses only elapsed time");
        directoryController.Dispose();
        var normalStop = new GameStoppedEventArgs { SessionLength = 99 };
        typeof(GameStoppedEventArgs).GetProperty("Source", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(normalStop, controller, null);
        action.TrackingMode = TrackingMode.Process;
        suppress.Invoke(null, new object[] { normalStop });
        Check(normalStop.SessionLength == 99, "normal action elapsed time is not suppressed");
        controller.Dispose();
        Console.WriteLine("PASS action tracking checks=" + checks);
        return 0;
    }
    static ComboBox MakeCombo(GameAction action)
    {
        var combo = new ComboBox { Name = "ComboTrackingMode", DataContext = action, DisplayMemberPath = "DisplayText", SelectedValuePath = "Value" };
        foreach (TrackingMode value in Enum.GetValues(typeof(TrackingMode))) combo.Items.Add(new SelectorBehaviors.EnumSelector { Value = value, DisplayText = value.ToString() });
        combo.SetBinding(ComboBox.SelectedValueProperty, new Binding("TrackingMode") { Mode = BindingMode.TwoWay });
        return combo;
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        checks++; Console.WriteLine("PASS " + name);
    }
}

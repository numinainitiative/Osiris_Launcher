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
        if(args.Contains("--native-only"))goto NativeChecks;
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
        // Never launch Steam in the fixture. Exercise real host StartTracking with
        // deliberately wrong launcher callbacks and a delayed real child process.
        var helperPath=Path.Combine(Path.GetDirectoryName(core),"Osiris.LaunchLifecycle.dll");
        if(File.Exists(helperPath))
        {
            var probeFolder=Path.Combine(Path.GetTempPath(),"OsirisSteamHandoff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(probeFolder);
            var probe=Path.Combine(probeFolder,"witcher3.exe");File.Copy(Path.GetFullPath(args[3]),probe);
            foreach(var mode in new[]{(TrackingMode)6,(TrackingMode)7})
            {
                var steamGame=new Game {Name="Synthetic Steam handoff",InstallDirectory=probeFolder};
                var steamController=(PlayController)Activator.CreateInstance(controllerType,new object[]{null,steamGame,null,null});
                var steamAction=new GameAction {Type=GameActionType.URL,Path="steam://run/292030",TrackingMode=mode,TrackingPath=probe};
                var roundTrip=JsonConvert.DeserializeObject<GameAction>(JsonConvert.SerializeObject(steamAction));
                Check(roundTrip.TrackingPath==probe && roundTrip.Path==steamAction.Path && roundTrip.TrackingMode==mode,"launch path, tracking exe and time preference round-trip independently");
                var steamArgs=new OnGameStartingEventArgs();typeof(OnGameStartingEventArgs).GetProperty("SourceAction").SetValue(steamArgs,steamAction,null);
                controllerType.GetProperty("StartingArgs",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(steamController,steamArgs,null);
                var didStart=0;var didStop=0;ulong steamLength=999;var steamDone=new ManualResetEvent(false);
                typeof(PlayController).GetEvent("Started",BindingFlags.Instance|BindingFlags.NonPublic).GetAddMethod(true).Invoke(steamController,new object[]{new EventHandler<GameStartedEventArgs>((s,e)=>didStart++)});
                typeof(PlayController).GetEvent("Stopped",BindingFlags.Instance|BindingFlags.NonPublic).GetAddMethod(true).Invoke(steamController,new object[]{new EventHandler<GameStoppedEventArgs>((s,e)=>{suppress.Invoke(null,new object[]{e});steamLength=e.SessionLength;didStop++;steamDone.Set();})});
                controllerType.GetMethod("StartTracking").Invoke(steamController,new object[]{new Func<bool>(()=>false),new Func<int>(()=>0),null,null,100,0});
                Thread.Sleep(250);Check(didStart==0 && didStop==0,"Steam launcher callbacks cannot start/stop game before real process appears");
                using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(probe){UseShellExecute=false,CreateNoWindow=true}))
                {
                    Check(steamDone.WaitOne(15000),"Steam lifecycle follows actual delayed game process through exit");
                    child.WaitForExit();
                }
                Check(didStart==1 && didStop==1,"Steam handoff emits exactly one real Started and Stopped event");
                Check(mode==(TrackingMode)7?steamLength==0:steamLength>=1,"Steam elapsed time obeys original saved action mode");
                Check(steamAction.TrackingMode==mode,"Lifecycle handoff never rewrites saved action mode");
                steamController.Dispose();steamDone.Dispose();
            }
            File.Delete(probe);Directory.Delete(probeFolder);
            var launchFolder=Path.Combine(Path.GetTempPath(),"OsirisExplicitLaunch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(launchFolder);
            var gameFolder=Path.Combine(launchFolder,"Game");Directory.CreateDirectory(gameFolder);
            var launcher=Path.Combine(launchFolder,"launcher.exe");var actualExe=Path.Combine(gameFolder,"actualgame.exe");
            File.Copy(Path.GetFullPath(args[3]),launcher);File.Copy(Path.GetFullPath(args[3]),actualExe);
            foreach(var mode in new[]{(TrackingMode)6,(TrackingMode)7})
            {
                var localGame=new Game {Name="Explicit exe real launch",InstallDirectory="",Id=Guid.NewGuid()};
                var localAction=new GameAction {Type=GameActionType.File,IsPlayAction=true,Path=launcher,Arguments="\""+actualExe+"\"",WorkingDir=launchFolder,TrackingPath=actualExe,TrackingMode=mode,TrackingFrequency=100};
                var localArgs=new OnGameStartingEventArgs();typeof(OnGameStartingEventArgs).GetProperty("SourceAction").SetValue(localArgs,localAction,null);
                var localController=(PlayController)Activator.CreateInstance(controllerType,new object[]{null,localGame,null,null});
                var startedEvent=new ManualResetEvent(false);var stoppedEvent=new ManualResetEvent(false);int startCount=0,stopCount=0;ulong elapsed=999;
                var screenshotSource=Path.Combine(launchFolder,"Captures"+((int)mode));Directory.CreateDirectory(screenshotSource);
                using(var screenshots=new Osiris.Extensions.ScreenshotsGallery.ScreenshotCollector(launchFolder,()=>screenshotSource,message=>Console.WriteLine(message)))
                {
                    typeof(PlayController).GetEvent("Started",BindingFlags.Instance|BindingFlags.NonPublic).GetAddMethod(true).Invoke(localController,new object[]{new EventHandler<GameStartedEventArgs>((s,e)=>{startCount++;screenshots.Start(localGame.Id);startedEvent.Set();})});
                    typeof(PlayController).GetEvent("Stopped",BindingFlags.Instance|BindingFlags.NonPublic).GetAddMethod(true).Invoke(localController,new object[]{new EventHandler<GameStoppedEventArgs>((s,e)=>{suppress.Invoke(null,new object[]{e});elapsed=e.SessionLength;stopCount++;screenshots.Stop(localGame.Id);stoppedEvent.Set();})});
                    controllerType.GetMethod("Start",new[]{typeof(GameAction),typeof(bool),typeof(OnGameStartingEventArgs)}).Invoke(localController,new object[]{localAction,true,localArgs});
                    Check(startedEvent.WaitOne(10000),"real launch follows separate tracking exe even with no game installation directory");
                    Thread.Sleep(250);Check(!stoppedEvent.WaitOne(0),"exiting intermediary launcher does not stop actual game session");
                    var pixels=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{0,0,255,255},4);
                    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pixels));
                    using(var output=File.Create(Path.Combine(screenshotSource,"synthetic.png")))encoder.Save(output);
                    screenshots.Poll();screenshots.Poll();
                    Check(Directory.GetFiles(Osiris.Extensions.ScreenshotsGallery.ScreenshotCollector.GameFolder(launchFolder,localGame.Id),"*.png").Length==1,"native game lifecycle captures synthetic screenshot after launcher handoff");
                    Check(stoppedEvent.WaitOne(15000) && startCount==1 && stopCount==1,"real action yields exactly one start and one actual exit");
                    Check(mode==(TrackingMode)7?elapsed==0:elapsed>=1,"full launch preserves independent playtime toggle");
                    Check(localAction.Path==launcher && localAction.TrackingPath==actualExe && localAction.TrackingMode==mode,"normalization only edits transient launch clone");
                }
                localController.Dispose();startedEvent.Dispose();stoppedEvent.Dispose();
            }
        }
        var editorType=options.GetNestedType("Editor",BindingFlags.NonPublic);
        var uiAction=new GameAction {Type=GameActionType.URL,IsPlayAction=true,Path="steam://run/292030",TrackingMode=disabled};
        var uiRoot=new UserControl {DataContext=uiAction};var columns=new Grid();var left=new Grid();var right=new Grid();uiRoot.Content=columns;columns.Children.Add(left);columns.Children.Add(right);
        for(var i=0;i<9;i++){left.RowDefinitions.Add(new RowDefinition());right.RowDefinitions.Add(new RowDefinition());}
        var uiCombo=MakeCombo(uiAction);ensure.Invoke(null,new object[]{uiCombo});Grid.SetRow(uiCombo,3);left.Children.Add(uiCombo);left.Children.Add(new TextBlock {Text="Tracking mode"});
        var tracking=new TextBox();Grid.SetRow(tracking,8);right.Children.Add(tracking);
        var uiEditor=Activator.CreateInstance(editorType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{uiRoot,uiCombo,tracking},null);
        editorType.GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(uiEditor,null);
        var toggle=left.Children.OfType<CheckBox>().Single();
        var uiFrame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>uiFrame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(uiFrame);
        Check(toggle.IsChecked==false && uiCombo.Visibility==Visibility.Collapsed,"Actions replace tracking-mode dropdown with independent playtime control");
        editorType.GetField("editingPath",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(uiEditor,true);
        tracking.Text=@"C:\Games\Witcher\bin\x64\witcher3.exe";
        Check(uiAction.TrackingMode==(TrackingMode)7 && uiAction.Path=="steam://run/292030" && uiAction.TrackingPath==tracking.Text,"editing tracking exe leaves Steam launching path and time-off preference untouched");
        toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,true);Check(uiAction.TrackingMode==(TrackingMode)6 && uiAction.TrackingPath==tracking.Text,"playtime can be enabled without changing tracking exe");
        toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,false);Check(uiAction.TrackingMode==(TrackingMode)7,"playtime can be disabled independently");
        tracking.Text="";Check(uiAction.TrackingMode==disabled,"clearing exe restores automatic detection while preserving time-off preference");
        toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,true);Check(uiAction.TrackingMode==TrackingMode.Default,"automatic detection can also count time");
        var unchanged=new GameAction {Type=GameActionType.File,IsPlayAction=true,TrackingMode=TrackingMode.Directory,TrackingPath=@"C:\Legacy"};
        uiRoot.DataContext=unchanged;uiCombo.DataContext=unchanged;
        editorType.GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(uiEditor,null);
        Check(unchanged.TrackingMode==TrackingMode.Directory && unchanged.TrackingPath==@"C:\Legacy","loading an existing legacy action does not rewrite its configuration");
        NativeChecks:
        var desktop=Assembly.LoadFrom(Path.Combine(app,"Osiris.DesktopEngine.exe"));
        var nativePathType=desktop.GetType("Playnite.DesktopApp.Controls.PathSelectionBox",true);
        foreach(var controlType in new[]{typeof(TextBox),typeof(ComboBox),nativePathType})Application.Current.Resources[controlType]=new Style(controlType);
        foreach(var controlType in desktop.GetTypes().Where(t=>typeof(FrameworkElement).IsAssignableFrom(t) && t.Name.EndsWith("NumericBox")))Application.Current.Resources[controlType]=new Style(controlType);
        Application.Current.Resources["BaseTextBlockStyle"]=new Style(typeof(TextBlock));
        Application.Current.Resources["BooleanToVisibilityConverter"]=new BooleanToVisibilityConverter();
        Application.Current.Resources["InvertedBooleanToVisibilityConverter"]=Activator.CreateInstance(Assembly.LoadFrom(core).GetType("Playnite.Converters.InvertedBooleanToVisibilityConverter",true));
        var nativeEditor=(UserControl)Activator.CreateInstance(desktop.GetType("Playnite.DesktopApp.Controls.GameTaskView",true));
        var nativeAction=new GameAction {Type=GameActionType.URL,IsPlayAction=true,Path="steam://run/292030",TrackingPath=@"C:\Games\witcher3.exe",TrackingMode=(TrackingMode)7};
        nativeEditor.DataContext=nativeAction;nativeEditor.ApplyTemplate();nativeEditor.Measure(new Size(1000,700));nativeEditor.Arrange(new Rect(0,0,1000,700));
        var nativeCombo=(ComboBox)nativeEditor.FindName("ComboTrackingMode");ensure.Invoke(null,new object[]{nativeCombo});
        var nativeFrame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>nativeFrame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(nativeFrame);
        Check(nativeCombo.Visibility==Visibility.Collapsed && nativeAction.TrackingMode==(TrackingMode)7,"real native Actions editor installs the playtime control without changing saved specific-exe preference");
        Check(nativeEditor.GetValue((DependencyProperty)options.GetField("EditorProperty",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))!=null,"real native tracking path row is adapted and file browse is available");
        var nativeState=nativeEditor.GetValue((DependencyProperty)options.GetField("EditorProperty",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null));
        var nativeTarget=(TextBox)nativeState.GetType().GetField("path",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(nativeState);
        Check((bool)nativePathType.GetProperty("ShowFileSelector").GetValue(nativeTarget,null) && !(bool)nativePathType.GetProperty("ShowDirectorySelector").GetValue(nativeTarget,null),"native tracking path browses files rather than directories");
        Check(((Grid)nativeTarget.Parent).RowDefinitions[Grid.GetRow(nativeTarget)].Height.IsAuto,"tracking path stays visible with playtime disabled");
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

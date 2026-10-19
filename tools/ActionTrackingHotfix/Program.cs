using dnlib.DotNet;
using dnlib.DotNet.Emit;

// Development-only inherited-runtime patch: stage outside the installation.
if (args.Length != 3 || !new[] { "--patch", "--verify" }.Contains(args[0]))
    throw new ArgumentException("Usage: --patch <App directory> <output Playnite.dll> | --verify <App directory> <patched Playnite.dll>");
var app = Path.GetFullPath(args[1]);
var sourcePath = Path.Combine(app, "Playnite.dll");
var outputPath = Path.GetFullPath(args[2]);
using var sdk = ModuleDefMD.Load(Path.Combine(app, "Playnite.SDK.dll"));
var tracking = sdk.GetTypes().Single(t => t.FullName == "Playnite.SDK.Models.TrackingMode");
if (tracking.Fields.Any(f => f.HasConstant && Convert.ToInt32(f.Constant.Value) == 5))
    throw new InvalidOperationException("Reserved Disabled value 5 is already in use.");
if (args[0] == "--verify") { Verify(outputPath); return; }
if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Never patch installation files in place.");
using (var core = ModuleDefMD.Load(sourcePath))
{
    var controller = core.GetTypes().Single(t => t.FullName == "Playnite.Controllers.GenericPlayController");
    var editor = core.GetTypes().Single(t => t.FullName == "Playnite.GamesEditor");
    if (editor.Methods.Any(m => m.Name == "OsirisSuppressActionPlaytime"))
    {
        Verify(sourcePath); File.Copy(sourcePath, outputPath, true); return;
    }
    if (controller.Methods.Any(m => m.Name == "OsirisFinishUntrackedLaunch"))
        throw new InvalidOperationException("Obsolete immediate-stop prototype is not a valid input.");
    var start = controller.Methods.Single(m => m.Name == "Start" && m.MethodSig.Params.Count == 3);
    var getter = start.Body.Instructions.First(i => i.Operand is IMethod m && m.Name == "get_TrackingMode");
    var at = start.Body.Instructions.IndexOf(getter) - 1;
    var anchor = start.Body.Instructions[at];
    if (anchor.OpCode.Code != Code.Ldloc_1 ||
        !start.Body.Instructions.Take(at).Any(i => i.Operand is IMethod m && m.FullName.Contains("ProcessStarter::StartProcess")) ||
        !start.Body.Instructions.Take(at).Any(i => i.Operand is IMethod m && m.FullName.Contains("ProcessStarter::StartUrl")))
        throw new InvalidOperationException("Expected normal File/URL launch boundary missing.");
    IMethod ImportSdk(string type, string method) => core.Import(sdk.GetTypes().Single(t => t.FullName == type).Methods.Single(m => m.Name == method));
    var gameGetter = (IMethod)start.Body.Instructions.First(i => i.Operand is IMethod m && m.Name == "get_Game").Operand;
    var installGetter = ImportSdk("Playnite.SDK.Models.Game", "get_InstallDirectory");
    var trackingSetter = ImportSdk("Playnite.SDK.Models.GameAction", "set_TrackingMode");
    var exists = new MemberRefUser(core, "Exists", MethodSig.CreateStatic(core.CorLibTypes.Boolean, core.CorLibTypes.String),
        new TypeRefUser(core, "System.IO", "Directory", core.CorLibTypes.AssemblyRef));
    // Normalize only the launch clone's monitor mode. The saved SourceAction
    // retains 5 so stop handling can suppress time without losing lifecycle.
    anchor.OpCode = OpCodes.Nop; anchor.Operand = null;
    var continuation = Instruction.Create(OpCodes.Ldloc_1);
    var fallback = Instruction.Create(OpCodes.Ldc_I4_0);
    var setMode = Instruction.Create(OpCodes.Callvirt, trackingSetter);
    var guard = new[]
    {
        Instruction.Create(OpCodes.Ldloc_1), Instruction.Create(OpCodes.Callvirt, (IMethod)getter.Operand),
        Instruction.Create(OpCodes.Ldc_I4, 5), Instruction.Create(OpCodes.Bne_Un, continuation),
        Instruction.Create(OpCodes.Ldloc_1), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, gameGetter),
        Instruction.Create(OpCodes.Callvirt, installGetter), Instruction.Create(OpCodes.Call, exists),
        Instruction.Create(OpCodes.Brfalse, fallback), Instruction.Create(OpCodes.Ldc_I4_2), Instruction.Create(OpCodes.Br, setMode),
        fallback, setMode, continuation
    };
    for (int i = 0; i < guard.Length; i++) start.Body.Instructions.Insert(at + 1 + i, guard[i]);
    start.Body.SimplifyBranches(); start.Body.OptimizeBranches();
    var stopped = editor.Methods.Single(m => m.Name == "Controllers_Stopped");
    var sourceGetter = (IMethod)stopped.Body.Instructions.First(i => i.Operand is IMethod m && m.Name == "get_Source").Operand;
    var startingGetter = controller.Methods.Single(m => m.Name == "get_StartingArgs");
    var sourceActionGetter = ImportSdk("Playnite.SDK.Events.OnGameStartingEventArgs", "get_SourceAction");
    var lengthSetter = ImportSdk("Playnite.SDK.Plugins.GameStoppedEventArgs", "set_SessionLength");
    var helper = new MethodDefUser("OsirisSuppressActionPlaytime",
        MethodSig.CreateStatic(core.CorLibTypes.Void, stopped.MethodSig.Params[1]),
        MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
        { Body = new CilBody { InitLocals = true } };
    var ctrlLocal = new Local(controller.ToTypeSig());
    var startingLocal = new Local(startingGetter.ReturnType);
    var actionLocal = new Local(sourceActionGetter.MethodSig.RetType);
    helper.Body.Variables.Add(ctrlLocal); helper.Body.Variables.Add(startingLocal); helper.Body.Variables.Add(actionLocal);
    var done = Instruction.Create(OpCodes.Ret);
    foreach (var i in new[]
    {
        Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Brfalse, done),
        Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Callvirt, sourceGetter),
        Instruction.Create(OpCodes.Isinst, controller), Instruction.Create(OpCodes.Stloc, ctrlLocal),
        Instruction.Create(OpCodes.Ldloc, ctrlLocal), Instruction.Create(OpCodes.Brfalse, done),
        Instruction.Create(OpCodes.Ldloc, ctrlLocal), Instruction.Create(OpCodes.Call, startingGetter),
        Instruction.Create(OpCodes.Stloc, startingLocal), Instruction.Create(OpCodes.Ldloc, startingLocal),
        Instruction.Create(OpCodes.Brfalse, done), Instruction.Create(OpCodes.Ldloc, startingLocal),
        Instruction.Create(OpCodes.Callvirt, sourceActionGetter), Instruction.Create(OpCodes.Stloc, actionLocal),
        Instruction.Create(OpCodes.Ldloc, actionLocal), Instruction.Create(OpCodes.Brfalse, done),
        Instruction.Create(OpCodes.Ldloc, actionLocal), Instruction.Create(OpCodes.Callvirt, (IMethod)getter.Operand),
        Instruction.Create(OpCodes.Ldc_I4, 5), Instruction.Create(OpCodes.Bne_Un, done),
        Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldc_I4_0),
        Instruction.Create(OpCodes.Conv_U8), Instruction.Create(OpCodes.Callvirt, lengthSetter), done
    }) helper.Body.Instructions.Add(i);
    helper.Body.OptimizeMacros(); helper.Body.OptimizeBranches();
    editor.Methods.Add(helper);
    stopped.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, helper));
    stopped.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_2));
    stopped.Body.SimplifyBranches(); stopped.Body.OptimizeBranches();
    core.Write(outputPath);
}
Verify(outputPath);

static void Verify(string path)
{
    using var core = ModuleDefMD.Load(path);
    var controller = core.GetTypes().Single(t => t.FullName == "Playnite.Controllers.GenericPlayController");
    var editor = core.GetTypes().Single(t => t.FullName == "Playnite.GamesEditor");
    var helper = editor.Methods.Single(m => m.Name == "OsirisSuppressActionPlaytime");
    var stopped = editor.Methods.Single(m => m.Name == "Controllers_Stopped");
    if (stopped.Body.Instructions[0].OpCode.Code != Code.Ldarg_2 ||
        stopped.Body.Instructions[1].Operand is not IMethod policy || policy.Name != helper.Name ||
        !helper.Body.Instructions.Any(i => i.Operand is IMethod m && m.Name == "set_SessionLength") ||
        !helper.Body.Instructions.Any(i => i.IsLdcI4() && i.GetLdcI4Value() == 5) ||
        helper.Body.Instructions.Any(i => i.Operand is IMethod m && (m.Name == "InvokeOnStopped" || m.Name == "InvokeOnStarted")))
        throw new InvalidOperationException("Time-only policy verification failed.");
    var start = controller.Methods.Single(m => m.Name == "Start" && m.MethodSig.Params.Count == 3);
    if (!start.Body.Instructions.Any(i => i.Operand is IMethod m && m.Name == "set_TrackingMode") ||
        !start.Body.Instructions.Any(i => i.IsLdcI4() && i.GetLdcI4Value() == 5) ||
        !start.Body.Instructions.Any(i => i.Operand is IMethod m && m.Name == "StartTracking") ||
        controller.Methods.Any(m => m.Name == "OsirisFinishUntrackedLaunch"))
        throw new InvalidOperationException("Real lifecycle monitoring must remain intact.");
    Console.WriteLine("Verified time-only Disabled=5: running lifecycle retained; elapsed-time persistence suppressed: " + path);
}

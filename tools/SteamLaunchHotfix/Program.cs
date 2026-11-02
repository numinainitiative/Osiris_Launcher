using dnlib.DotNet;
using dnlib.DotNet.Emit;
if(args.Length!=4 || (args[0]!="--patch" && args[0]!="--verify"))throw new ArgumentException("--patch/--verify <source core> <runtime helper dll> <output core>");
var source=Path.GetFullPath(args[1]);var helper=Path.GetFullPath(args[2]);var output=Path.GetFullPath(args[3]);
if(args[0]=="--patch" && source.Equals(output,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Stage outside installation.");
using var runtime=ModuleDefMD.Load(helper);
using var core=ModuleDefMD.Load(args[0]=="--verify"?output:source);
using(var sdk=ModuleDefMD.Load(Path.Combine(Path.GetDirectoryName(source)!,"Playnite.SDK.dll")))
{
 var modes=sdk.GetTypes().Single(t=>t.FullName=="Playnite.SDK.Models.TrackingMode");
 if(modes.Fields.Any(f=>f.HasConstant && (Convert.ToInt32(f.Constant.Value)==6 || Convert.ToInt32(f.Constant.Value)==7)))throw new InvalidOperationException("Reserved executable tracking modes are already used by this SDK.");
}
var controller=core.GetTypes().Single(t=>t.FullName=="Playnite.Controllers.GenericPlayController");
var track=controller.Methods.Single(m=>m.Name=="StartTracking" && m.MethodSig.Params.Count==6);
bool Patched()=>track.Body.Instructions.Any(i=>i.Operand is IMethod m && m.FullName.Contains("Osiris.LaunchLifecycle.SteamSession::WrapTracking"));
if(args[0]=="--patch")
{
 var policy=runtime.GetTypes().Single(t=>t.FullName=="Osiris.LaunchLifecycle.SteamSession");
 var start=controller.Methods.Single(m=>m.Name=="Start" && m.MethodSig.Params.Count==3);
 if(!start.Body.Instructions.Any(i=>i.Operand is IMethod m && m.Name=="NormalizeAction"))
 {
  var expand=start.Body.Instructions.ToList().FindIndex(i=>i.Operand is IMethod m && m.Name=="ExpandVariables" && m.MethodSig.RetType.FullName=="Playnite.SDK.Models.GameAction");
  if(expand<0 || start.Body.Instructions[expand+1].OpCode.Code!=Code.Stloc_1)throw new InvalidDataException("Expected expanded action clone boundary missing.");
  start.Body.Instructions.Insert(expand+2,Instruction.Create(OpCodes.Ldloc_1));
  start.Body.Instructions.Insert(expand+3,Instruction.Create(OpCodes.Call,core.Import(policy.Methods.Single(m=>m.Name=="NormalizeAction"))));
  start.Body.SimplifyBranches();start.Body.OptimizeBranches();
 }
 if(!Patched())
 {
  var type=runtime.GetTypes().Single(t=>t.FullName=="Osiris.LaunchLifecycle.SteamSession");
  var first=core.Import(type.Methods.Single(m=>m.Name=="WrapTracking"));var second=core.Import(type.Methods.Single(m=>m.Name=="WrapStartup"));
  var injected=new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,first),Instruction.Create(OpCodes.Starg,track.Parameters[1]),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,second),Instruction.Create(OpCodes.Starg,track.Parameters[2])};
  for(var i=0;i<injected.Length;i++)track.Body.Instructions.Insert(i,injected[i]);
  track.Body.SimplifyBranches();track.Body.OptimizeBranches();
 }
 var suppress=core.GetTypes().Single(t=>t.FullName=="Playnite.GamesEditor").Methods.Single(m=>m.Name=="OsirisSuppressActionPlaytime");
 if(!suppress.Body.Instructions.Any(i=>i.IsLdcI4() && i.GetLdcI4Value()==7))
 {
  var check=suppress.Body.Instructions.ToList().FindIndex(i=>i.IsLdcI4() && i.GetLdcI4Value()==5);
  if(check<2 || suppress.Body.Instructions[check+1].OpCode.FlowControl!=FlowControl.Cond_Branch)throw new InvalidDataException("Expected time-only suppression guard missing.");
  var done=(Instruction)suppress.Body.Instructions[check+1].Operand;
  var suppressStart=suppress.Body.Instructions[check+2];
  var load=suppress.Body.Instructions[check-2];var getter=suppress.Body.Instructions[check-1];
  suppress.Body.Instructions[check+1].OpCode=OpCodes.Beq;suppress.Body.Instructions[check+1].Operand=suppressStart;
  var extra=new[]{new Instruction(load.OpCode,load.Operand),new Instruction(getter.OpCode,getter.Operand),Instruction.Create(OpCodes.Ldc_I4,7),Instruction.Create(OpCodes.Bne_Un,done)};
  for(var n=0;n<extra.Length;n++)suppress.Body.Instructions.Insert(check+2+n,extra[n]);
  suppress.Body.SimplifyBranches();suppress.Body.OptimizeBranches();
 }
 core.Write(output);
}
if(!Patched() || !track.Body.Instructions.Any(i=>i.Operand is IMethod m && m.Name=="WrapStartup"))throw new InvalidDataException("Steam lifecycle patch not verified.");
var editor=core.GetTypes().Single(t=>t.FullName=="Playnite.GamesEditor");
if(!editor.Methods.Any(m=>m.Name=="OsirisSuppressActionPlaytime"))throw new InvalidDataException("Time-only policy must remain present.");
if(!controller.Methods.Single(m=>m.Name=="Start" && m.MethodSig.Params.Count==3).Body.Instructions.Any(i=>i.Operand is IMethod m && m.Name=="NormalizeAction"))throw new InvalidDataException("Explicit executable tracking normalization missing.");
if(!editor.Methods.Single(m=>m.Name=="OsirisSuppressActionPlaytime").Body.Instructions.Any(i=>i.IsLdcI4() && i.GetLdcI4Value()==7))throw new InvalidDataException("Independent time-off policy missing.");
Console.WriteLine("Verified explicit executable lifecycle; original SourceAction and independent time-only policy preserved.");

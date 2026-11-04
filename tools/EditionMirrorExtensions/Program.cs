using dnlib.DotNet;
using dnlib.DotNet.Emit;

// Scoped Development compatibility adapter. It adds only page-owner resolution
// to the installed extension's existing settings/presentation/storage boundaries;
// no replacement of its launch tracking, sessions, library identity or imports.
if (args.Length < 3) throw new ArgumentException("theme.dll plugin.dll page [--inspect|--verify]");
using var theme = ModuleDefMD.Load(args[0]);
using var module = ModuleDefMD.Load(args[1]);
var page = args[2];
var inspect = args.Contains("--inspect"); var verify = args.Contains("--verify");
var bridge = theme.Types.Single(t => t.FullName == "OsirisTheme.EditionPageSharing" || t.FullName == "Osiris.Extensions.EditionMirrorBridge");
var importer = new Importer(module);
var resolveId = importer.Import(bridge.Methods.Single(m => m.Name == "ResolveOwner"));
var resolveGame = importer.Import(bridge.Methods.Single(m => m.Name == "ResolveGame"));
var changed = 0; var boundaries = 0;
if (!inspect && (page == "HowLongToBeat" || page == "Exophase"))
{
    var storeName = page == "HowLongToBeat" ? "CompletionTimeGameSettingsStore" : "ExophaseGameSettingsStore";
    var store = module.GetTypes().Single(t => t.Name == storeName);
    if (!new[] { "syncRoot", "settingsByGame" }.All(name => store.Fields.Any(f => f.Name == name)) ||
        !store.Methods.Any(m => m.Name == (page == "HowLongToBeat" ? "SaveAll" : "WriteAll") && m.MethodSig.Params.Count == 0))
        throw new InvalidOperationException("Unsupported independent snapshot store: " + storeName);
}
foreach (var type in module.GetTypes())
foreach (var method in type.Methods.Where(m => m.HasBody).ToList())
{
    if (inspect)
    {
        if (method.Parameters.Any(p => p.Type.FullName == "System.Guid") || method.Name == "GameContextChanged" || method.Name == "Resolve" || method.Name == "SelectStoredResult")
            Console.WriteLine(method.FullName);
        continue;
    }
    if (page == "Screenshots" && method.Name == "GameContextChanged" && method.Body.Instructions.Count >= 4 &&
        method.Body.Instructions[2].Operand is IMethod oldContext && oldContext.Name == "ResolveGame")
    {
        if (verify) throw new InvalidOperationException("Screenshot view still aliases its physical context: " + method.FullName);
        for (var i = 0; i < 4; i++) method.Body.Instructions.RemoveAt(0);
        changed++;
    }
    bool idStore = (page == "HowLongToBeat" && type.Name == "CompletionTimeGameSettingsStore" && new[] { "Load", "Save", "StoreFetchedResult" }.Contains(method.Name.String)) ||
        (page == "Exophase" && type.Name == "ExophaseGameSettingsStore" && new[] { "Load", "Save" }.Contains(method.Name.String)) ||
        (page == "Screenshots" && type.Name == "ScreenshotCollector" && new[] { "GameFolder", "IsEnabled" }.Contains(method.Name.String));
    // Screenshots keeps its physical context ID. Its folder/enablement calls
    // resolve owners every refresh, including links changed without navigation.
    var gameContext = method.Name == "GameContextChanged" && new[] { "Gallery", "HowLongToBeat", "Exophase" }.Contains(page);
    var gameResolve = page == "Exophase" && type.Name == "ExophaseActivityResolver" && method.Name == "Resolve" ||
        page == "HowLongToBeat" && method.Name == "SelectStoredResult" ||
        page == "Gallery" && type.Name == "SteamScreenshotsControl" && method.Name == "LoadControlData";
    var parameter = idStore ? method.Parameters.FirstOrDefault(p => p.Type.FullName == "System.Guid") :
        gameContext ? method.Parameters.LastOrDefault(p => p.Type.FullName == "Playnite.SDK.Models.Game") :
        gameResolve ? method.Parameters.FirstOrDefault(p => p.Type.FullName == "Playnite.SDK.Models.Game") : null;
    if (parameter != null)
    {
        boundaries++;
        var resolver = idStore ? resolveId : resolveGame;
        // Reuse the existing helper API so an older helper already loaded by
        // another extension remains compatible with this scoped projection.
        var contextPage = page == "Exophase" && gameResolve ? "ExophaseActivity" : page;
        var exists = method.Body.Instructions.Take(5).Any(i => i.Operand is IMethod m && m.FullName == resolver.FullName);
        if (exists && method.Body.Instructions[1].OpCode == OpCodes.Ldstr && (string)method.Body.Instructions[1].Operand != contextPage)
        {
            if (verify) throw new InvalidOperationException("Wrong owner projection: " + method.FullName);
            method.Body.Instructions[1].Operand = contextPage;
            changed++;
        }
        if (!exists)
        {
            if (verify) throw new InvalidOperationException("Missing owner boundary: " + method.FullName);
            var code = new[] { Instruction.Create(OpCodes.Ldarg, parameter), Instruction.Create(OpCodes.Ldstr, contextPage),
                Instruction.Create(OpCodes.Call, resolver), Instruction.Create(OpCodes.Starg, parameter) };
            for (var i = 0; i < code.Length; i++) method.Body.Instructions.Insert(i, code[i]);
            method.Body.KeepOldMaxStack = false; changed++;
        }
    }
    if (page == "StatsInsights" && type.Name == "StatsInsightsProvider" && method.Name == "Enabled" || page == "Gallery")
    {
        var instructions = method.Body.Instructions;
        for (var i = instructions.Count - 1; i >= 0; i--)
        {
            if (instructions[i].Operand is not IMethod called || called.Name != "GetFileStoragePath" || called.MethodSig?.Params.Count != 1) continue;
            boundaries++;
            if (i > 0 && instructions[i - 1].Operand is IMethod prior && prior.FullName == resolveId.FullName) continue;
            if (verify) throw new InvalidOperationException("Missing file-storage boundary: " + method.FullName);
            var oldCall = instructions[i];
            var first = Instruction.Create(OpCodes.Ldstr, page);
            foreach (var instruction in instructions)
            {
                if (ReferenceEquals(instruction.Operand, oldCall)) instruction.Operand = first;
                else if (instruction.Operand is IList<Instruction> targets)
                    for (var j = 0; j < targets.Count; j++) if (ReferenceEquals(targets[j], oldCall)) targets[j] = first;
            }
            foreach (var handler in method.Body.ExceptionHandlers)
            {
                if (handler.TryStart == oldCall) handler.TryStart = first;
                if (handler.HandlerStart == oldCall) handler.HandlerStart = first;
                if (handler.FilterStart == oldCall) handler.FilterStart = first;
            }
            instructions.Insert(i, first); instructions.Insert(i + 1, Instruction.Create(OpCodes.Call, resolveId));
            method.Body.KeepOldMaxStack = false; changed++;
        }
    }
}
if (inspect) return;
var minimum = page switch { "HowLongToBeat" => 4, "Exophase" => 4, "Screenshots" => 2, "Gallery" => 5, "StatsInsights" => 1, _ => throw new ArgumentException("Unknown page") };
if (boundaries < minimum) throw new InvalidOperationException($"Unsupported {page} extension: found {boundaries}, expected at least {minimum} exact boundaries.");
if (changed > 0)
{
    var target = args[1] + ".mirror.tmp"; module.Write(target); module.Dispose(); File.Copy(target, args[1], true); File.Delete(target);
}
Console.WriteLine($"{page}: {boundaries} owner boundaries verified; patched {changed}. Native identities/session lifecycle unchanged.");

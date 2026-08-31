using dnlib.DotNet;
using dnlib.DotNet.Emit;

const int invalidArgumentsExitCode = 2;
const int verificationFailedExitCode = 3;

if (args.Length == 2 && args[0] == "--verify")
{
    var assemblyPath = Path.GetFullPath(args[1]);
    using var module = ModuleDefMD.Load(assemblyPath);
    if (!IsProgramUpdateCheckDisabled(module))
    {
        Console.Error.WriteLine($"Inherited program updates are not disabled: {assemblyPath}");
        return verificationFailedExitCode;
    }

    Console.WriteLine($"Verified inherited program updates are disabled: {assemblyPath}");
    return 0;
}

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine(
        "Usage: InheritedProgramUpdateGuard <input assembly> [output assembly]\n" +
        "       InheritedProgramUpdateGuard --verify <assembly>");
    return invalidArgumentsExitCode;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args.Length == 2
    ? args[1]
    : Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        Path.GetFileNameWithoutExtension(inputPath) +
            ".program-updates-disabled" +
            Path.GetExtension(inputPath)));

if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Input and output must be different files.");
    return invalidArgumentsExitCode;
}

using (var module = ModuleDefMD.Load(inputPath))
{
    var getter = GetUpdateAvailableGetter(module);
    if (!IsDisabledBody(getter))
    {
        var callsLatestVersion = getter.Body.Instructions.Any(instruction =>
            instruction.Operand is IMethod method && method.Name == "GetLatestVersion");
        if (!callsLatestVersion)
        {
            throw new InvalidOperationException(
                "The IsUpdateAvailable getter has an unexpected shape; refusing to patch it.");
        }

        getter.Body.ExceptionHandlers.Clear();
        getter.Body.Variables.Clear();
        getter.Body.Instructions.Clear();
        getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        getter.Body.InitLocals = false;
        getter.Body.MaxStack = 1;
    }

    module.Write(outputPath);
}

using (var outputModule = ModuleDefMD.Load(outputPath))
{
    if (!IsProgramUpdateCheckDisabled(outputModule))
    {
        throw new InvalidOperationException("Output verification failed after patching.");
    }
}

Console.WriteLine($"Disabled inherited program updates: {outputPath}");
return 0;

static bool IsProgramUpdateCheckDisabled(ModuleDef module) =>
    IsDisabledBody(GetUpdateAvailableGetter(module));

static MethodDef GetUpdateAvailableGetter(ModuleDef module)
{
    if (module.Assembly?.Name != "Playnite" ||
        module.Assembly.Version.Major != 10 ||
        module.Assembly.Version.Minor != 56)
    {
        throw new InvalidOperationException(
            "Expected the inherited 10.56 core assembly; refusing to continue.");
    }

    var updaterType = module.Find("Playnite.Updater", false)
        ?? throw new InvalidOperationException("Playnite.Updater was not found.");
    var property = updaterType.Properties.FirstOrDefault(candidate =>
        candidate.Name == "IsUpdateAvailable")
        ?? throw new InvalidOperationException("Updater.IsUpdateAvailable was not found.");
    var getter = property.GetMethod
        ?? throw new InvalidOperationException("Updater.IsUpdateAvailable has no getter.");
    if (!getter.HasBody)
    {
        throw new InvalidOperationException("Updater.IsUpdateAvailable has no method body.");
    }

    return getter;
}

static bool IsDisabledBody(MethodDef getter)
{
    var meaningfulInstructions = getter.Body.Instructions
        .Where(instruction => instruction.OpCode != OpCodes.Nop)
        .ToArray();
    return meaningfulInstructions.Length == 2 &&
        meaningfulInstructions[0].OpCode == OpCodes.Ldc_I4_0 &&
        meaningfulInstructions[1].OpCode == OpCodes.Ret;
}

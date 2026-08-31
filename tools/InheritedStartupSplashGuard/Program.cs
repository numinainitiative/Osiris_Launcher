using dnlib.DotNet;
using dnlib.DotNet.Emit;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: InheritedStartupSplashGuard [--verify] <assembly> [assembly...]");
    return 2;
}

var verifyOnly = false;
var assemblyPaths = new List<string>();
foreach (var argument in args)
{
    if (string.Equals(argument, "--verify", StringComparison.OrdinalIgnoreCase))
    {
        verifyOnly = true;
        continue;
    }

    assemblyPaths.Add(Path.GetFullPath(argument));
}

if (assemblyPaths.Count == 0)
{
    Console.Error.WriteLine("Usage: InheritedStartupSplashGuard [--verify] <assembly> [assembly...]");
    return 2;
}

foreach (var assemblyPath in assemblyPaths)
{
    if (!File.Exists(assemblyPath))
    {
        throw new FileNotFoundException("Assembly was not found.", assemblyPath);
    }

    PatchResult result;
    var patchedPath = assemblyPath + ".startupsplash.patched";
    using (var module = ModuleDefMD.Load(assemblyPath))
    {
        result = DisableStartupSplash(module, verifyOnly);
        if (!verifyOnly && result.Changed)
        {
            module.Write(patchedPath);
        }
    }

    if (!verifyOnly && result.Changed)
    {
        File.Copy(patchedPath, assemblyPath, true);
        File.Delete(patchedPath);
    }

    Console.WriteLine(
        $"{Path.GetFileName(assemblyPath)}: startup splash blocks disabled={result.StartupSplashBlocksDisabled}, " +
        $"changed={result.Changed}");
}

return 0;

static PatchResult DisableStartupSplash(ModuleDef module, bool verifyOnly)
{
    var splashBlocksDisabled = 0;
    var changed = false;

    foreach (var type in module.GetTypes())
    {
        foreach (var method in type.Methods.Where(method => method.HasBody))
        {
            var instructions = method.Body.Instructions;
            for (var index = 0; index < instructions.Count; index++)
            {
                if (!IsSplashScreenConstructorCall(instructions[index]))
                {
                    continue;
                }

                var startIndex = GetSplashBlockStart(instructions, index);
                var endIndex = GetSplashBlockEnd(instructions, index);
                if (startIndex is null || endIndex is null)
                {
                    throw new InvalidOperationException(
                        $"{method.FullName} creates a WPF splash screen with an unexpected instruction shape.");
                }

                if (IsDisabledBlock(instructions, startIndex.Value, endIndex.Value))
                {
                    splashBlocksDisabled++;
                    continue;
                }

                if (verifyOnly)
                {
                    throw new InvalidOperationException(
                        $"{method.FullName} still creates the inherited WPF startup splash screen.");
                }

                for (var instructionIndex = startIndex.Value; instructionIndex <= endIndex.Value; instructionIndex++)
                {
                    instructions[instructionIndex].OpCode = OpCodes.Nop;
                    instructions[instructionIndex].Operand = null;
                }

                splashBlocksDisabled++;
                changed = true;
            }
        }
    }

    return new PatchResult(changed, splashBlocksDisabled);
}

static bool IsSplashScreenConstructorCall(Instruction instruction)
{
    if (instruction.OpCode.Code != Code.Newobj ||
        instruction.Operand is not IMethod method)
    {
        return false;
    }

    return string.Equals(method.DeclaringType.FullName, "System.Windows.SplashScreen", StringComparison.Ordinal) &&
           string.Equals(method.Name, ".ctor", StringComparison.Ordinal);
}

static int? GetSplashBlockStart(IList<Instruction> instructions, int constructorIndex)
{
    if (constructorIndex < 1 ||
        instructions[constructorIndex - 1].OpCode.Code != Code.Ldstr ||
        instructions[constructorIndex - 1].Operand is not string resourceName)
    {
        return null;
    }

    return resourceName.Equals("SplashScreen.png", StringComparison.OrdinalIgnoreCase)
        ? constructorIndex - 1
        : null;
}

static int? GetSplashBlockEnd(IList<Instruction> instructions, int constructorIndex)
{
    for (var index = constructorIndex + 1; index < Math.Min(instructions.Count, constructorIndex + 8); index++)
    {
        if (IsSplashScreenShowCall(instructions[index]))
        {
            return index;
        }
    }

    return null;
}

static bool IsSplashScreenShowCall(Instruction instruction)
{
    if (instruction.OpCode.Code is not (Code.Call or Code.Callvirt) ||
        instruction.Operand is not IMethod method)
    {
        return false;
    }

    return string.Equals(method.DeclaringType.FullName, "System.Windows.SplashScreen", StringComparison.Ordinal) &&
           string.Equals(method.Name, "Show", StringComparison.Ordinal);
}

static bool IsDisabledBlock(IList<Instruction> instructions, int startIndex, int endIndex)
{
    return Enumerable.Range(startIndex, endIndex - startIndex + 1)
        .All(index => instructions[index].OpCode.Code == Code.Nop);
}

internal readonly record struct PatchResult(bool Changed, int StartupSplashBlocksDisabled);

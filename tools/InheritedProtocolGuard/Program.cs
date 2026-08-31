using dnlib.DotNet;
using dnlib.DotNet.Emit;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: InheritedProtocolGuard [--verify] <assembly> [assembly...]");
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
    Console.Error.WriteLine("Usage: InheritedProtocolGuard [--verify] <assembly> [assembly...]");
    return 2;
}

foreach (var assemblyPath in assemblyPaths)
{
    if (!File.Exists(assemblyPath))
    {
        throw new FileNotFoundException("Assembly was not found.", assemblyPath);
    }

    PatchResult result;
    var patchedPath = assemblyPath + ".protocolhooks.patched";
    using (var module = ModuleDefMD.Load(assemblyPath))
    {
        result = DisableHooks(module, verifyOnly);
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
        $"{Path.GetFileName(assemblyPath)}: protocol registrations disabled={result.ProtocolRegistrationCallsDisabled}, " +
        $"addon install handlers disabled={result.AddonInstallHandlersDisabled}, " +
        $"extension file install handlers disabled={result.ExtensionFileInstallHandlersDisabled}, changed={result.Changed}");
}

return 0;

static PatchResult DisableHooks(ModuleDef module, bool verifyOnly)
{
    var protocolRegistrationCalls = 0;
    var protocolRegistrationCallsDisabled = 0;
    var addonInstallHandlers = 0;
    var addonInstallHandlersDisabled = 0;
    var extensionFileInstallHandlers = 0;
    var extensionFileInstallHandlersDisabled = 0;
    var changed = false;

    foreach (var type in module.GetTypes())
    {
        foreach (var method in type.Methods.Where(method => method.HasBody))
        {
            var instructions = method.Body.Instructions;
            for (var index = 0; index < instructions.Count; index++)
            {
                var instruction = instructions[index];
                if (IsCallTo(instruction, "Playnite.SystemIntegration", "RegisterPlayniteUriProtocol") ||
                    IsCallTo(instruction, "Playnite.SystemIntegration", "RegisterFileExtensions"))
                {
                    protocolRegistrationCalls++;
                    if (instruction.OpCode.Code == Code.Nop)
                    {
                        protocolRegistrationCallsDisabled++;
                        continue;
                    }

                    if (verifyOnly)
                    {
                        throw new InvalidOperationException(
                            $"{method.FullName} still calls {((IMethod)instruction.Operand).Name}.");
                    }

                    instruction.OpCode = OpCodes.Nop;
                    instruction.Operand = null;
                    protocolRegistrationCallsDisabled++;
                    changed = true;
                }

                if (IsCallTo(method.Body.Instructions[index], "Playnite.PlayniteApplication", "InstallOnlineAddon"))
                {
                    addonInstallHandlers++;
                    if (IsInstallOnlineAddonCallDisabled(instructions, index))
                    {
                        addonInstallHandlersDisabled++;
                        continue;
                    }

                    if (verifyOnly)
                    {
                        throw new InvalidOperationException(
                            $"{method.FullName} still executes inherited install-addon URI requests.");
                    }

                    DisableInstallOnlineAddonCall(instructions, index);
                    addonInstallHandlersDisabled++;
                    changed = true;
                }

                if (IsCallTo(method.Body.Instructions[index], "Playnite.PlayniteApplication", "InstallThemeFile") ||
                    IsCallTo(method.Body.Instructions[index], "Playnite.PlayniteApplication", "InstallExtensionFile"))
                {
                    extensionFileInstallHandlers++;
                    if (IsInstanceStringCallDisabled(instructions, index))
                    {
                        extensionFileInstallHandlersDisabled++;
                        continue;
                    }

                    if (verifyOnly)
                    {
                        throw new InvalidOperationException(
                            $"{method.FullName} still executes inherited extension or theme file install requests.");
                    }

                    DisableInstanceStringCall(instructions, index);
                    extensionFileInstallHandlersDisabled++;
                    changed = true;
                }
            }
        }
    }

    if (!verifyOnly && protocolRegistrationCalls == 0)
    {
        Console.WriteLine("No active inherited protocol or extension registration calls were found.");
    }

    if (!verifyOnly && addonInstallHandlers == 0)
    {
        Console.WriteLine("No active inherited install-addon URI handler calls were found.");
    }

    return new PatchResult(
        changed,
        protocolRegistrationCallsDisabled,
        addonInstallHandlersDisabled,
        extensionFileInstallHandlersDisabled);
}

static bool IsCallTo(Instruction instruction, string declaringType, string methodName)
{
    if (instruction.OpCode.Code is not (Code.Call or Code.Callvirt) ||
        instruction.Operand is not IMethod method)
    {
        return false;
    }

    return string.Equals(method.DeclaringType.FullName, declaringType, StringComparison.Ordinal) &&
           string.Equals(method.Name, methodName, StringComparison.Ordinal);
}

static bool IsInstallOnlineAddonCallDisabled(IList<Instruction> instructions, int callIndex)
{
    return GetInstallOnlineAddonCallStart(instructions, callIndex) is { } startIndex &&
           Enumerable.Range(startIndex, callIndex - startIndex + 1)
               .All(index => instructions[index].OpCode.Code == Code.Nop);
}

static void DisableInstallOnlineAddonCall(IList<Instruction> instructions, int callIndex)
{
    var startIndex = GetInstallOnlineAddonCallStart(instructions, callIndex)
        ?? throw new InvalidOperationException("InstallOnlineAddon call has an unexpected instruction shape.");

    for (var index = startIndex; index <= callIndex; index++)
    {
        instructions[index].OpCode = OpCodes.Nop;
        instructions[index].Operand = null;
    }
}

static bool IsInstanceStringCallDisabled(IList<Instruction> instructions, int callIndex)
{
    return GetInstanceStringCallStart(instructions, callIndex) is { } startIndex &&
           Enumerable.Range(startIndex, callIndex - startIndex + 1)
               .All(index => instructions[index].OpCode.Code == Code.Nop);
}

static void DisableInstanceStringCall(IList<Instruction> instructions, int callIndex)
{
    var startIndex = GetInstanceStringCallStart(instructions, callIndex)
        ?? throw new InvalidOperationException("Install file call has an unexpected instruction shape.");

    for (var index = startIndex; index <= callIndex; index++)
    {
        instructions[index].OpCode = OpCodes.Nop;
        instructions[index].Operand = null;
    }
}

static int? GetInstallOnlineAddonCallStart(IList<Instruction> instructions, int callIndex)
{
    if (callIndex < 4)
    {
        return null;
    }

    if (instructions[callIndex - 1].OpCode.Code == Code.Ldelem_Ref &&
        instructions[callIndex - 2].IsLdcI4() &&
        instructions[callIndex - 2].GetLdcI4Value() == 1 &&
        instructions[callIndex - 4].OpCode.Code == Code.Ldarg_0)
    {
        return callIndex - 4;
    }

    return null;
}

static int? GetInstanceStringCallStart(IList<Instruction> instructions, int callIndex)
{
    if (callIndex < 2)
    {
        return null;
    }

    if (instructions[callIndex - 2].OpCode.Code == Code.Ldarg_0)
    {
        return callIndex - 2;
    }

    return null;
}

internal readonly record struct PatchResult(
    bool Changed,
    int ProtocolRegistrationCallsDisabled,
    int AddonInstallHandlersDisabled,
    int ExtensionFileInstallHandlersDisabled);

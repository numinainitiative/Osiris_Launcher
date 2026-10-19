using System;
using System.Linq;
using Mono.Cecil;

class Program
{
    static void Main(string[] args)
    {
        // Keep each installation's own theme; patch only the confirmed local delete call.
        using var theme = AssemblyDefinition.ReadAssembly(args[0]);
        using var helper = AssemblyDefinition.ReadAssembly(args[1]);
        var method = theme.MainModule.Types.Single(t => t.Name == "LocalLibraryActionButton").Methods.Single(m => m.Name == "ExecuteLocalUninstall");
        var calls = method.Body.Instructions.Where(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "System.IO.Directory" && m.Name == "Delete").ToArray();
        if (calls.Length != 1) throw new Exception("Expected exactly one original uninstall delete call; no patch written.");
        var replacement = helper.MainModule.Types.Single(t => t.FullName == "Osiris.UninstallSafety.InstallFolderDeletion").Methods.Single(m => m.Name == "Delete");
        calls[0].Operand = theme.MainModule.ImportReference(replacement);
        theme.Write(args[2]);
        using var verify = AssemblyDefinition.ReadAssembly(args[2]);
        var verified = verify.MainModule.Types.Single(t => t.Name == "LocalLibraryActionButton").Methods.Single(m => m.Name == "ExecuteLocalUninstall");
        if (!verified.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == replacement.DeclaringType.FullName && m.Name == "Delete")) throw new Exception("Patch verification failed.");
        Console.WriteLine("Verified uninstall-only patch: " + args[2]);
    }
}

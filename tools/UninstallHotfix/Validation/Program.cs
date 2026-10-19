using System;
using System.IO;
using Osiris.UninstallSafety;
class Program
{
    static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "OsirisUninstallValidation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        var file = Path.Combine(root, "nested", "server.dll");
        File.WriteAllText(file, "synthetic disposable fixture");
        File.SetAttributes(file, FileAttributes.ReadOnly | FileAttributes.Archive);
        InstallFolderDeletion.Delete(root, true);
        if (Directory.Exists(root)) throw new Exception("Read-only uninstall failed");
        try { InstallFolderDeletion.Delete(Path.GetPathRoot(root), true); throw new Exception("Root not rejected"); } catch (IOException) { }
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "keep.txt"), "fixture");
        try { InstallFolderDeletion.Delete(root, false); throw new Exception("Non-recursive delete not rejected"); } catch (IOException) { }
        if (!File.Exists(Path.Combine(root, "keep.txt"))) throw new Exception("Non-recursive rejection altered files");
        using (var locked = new FileStream(Path.Combine(root, "keep.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { InstallFolderDeletion.Delete(root, true); throw new Exception("Locked file was deleted"); } catch (IOException) { }
            if (!File.Exists(Path.Combine(root, "keep.txt"))) throw new Exception("Locked file disappeared");
        }
        InstallFolderDeletion.Delete(root, true);
        Console.WriteLine("6 disposable uninstall checks passed.");
    }
}

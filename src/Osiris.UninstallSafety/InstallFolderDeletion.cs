using System;
using System.Collections.Generic;
using System.IO;

namespace Osiris.UninstallSafety
{
    public static class InstallFolderDeletion
    {
        // Called only after Osiris validates the game folder and the user confirms.
        public static void Delete(string path, bool recursive)
        {
            var root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0 || string.Equals(root, Path.GetPathRoot(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Osiris refused to delete a drive root.");
            var folders = new List<string>();
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(root);
            // Inspect first, never traverse junctions/symlinks outside the game folder.
            while (pending.Count > 0)
            {
                var folder = pending.Pop();
                RefuseLink(folder);
                folders.Add(folder);
                foreach (var entry in Directory.GetFileSystemEntries(folder))
                {
                    var attributes = File.GetAttributes(entry);
                    RefuseLink(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!recursive) throw new IOException("Installation folder is not empty.");
                        pending.Push(entry);
                    }
                    else files.Add(entry);
                }
            }
            if (!recursive && files.Count > 0) throw new IOException("Installation folder is not empty.");
            foreach (var file in files)
            {
                ClearReadOnly(file);
                File.Delete(file);
            }
            for (var i = folders.Count - 1; i >= 0; i--)
            {
                ClearReadOnly(folders[i]);
                Directory.Delete(folders[i], false);
            }
        }
        private static void RefuseLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Osiris cannot uninstall a folder containing symbolic links or junctions.");
        }
        private static void ClearReadOnly(string path)
        {
            RefuseLink(path);
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}

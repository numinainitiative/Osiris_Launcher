using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal static class ResourceIconPatcher
{
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private static readonly IntPtr RtIcon = new IntPtr(3);
    private static readonly IntPtr RtGroupIcon = new IntPtr(14);

    private delegate bool EnumResourceNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
    private delegate bool EnumResourceLanguageProc(IntPtr module, IntPtr type, IntPtr name, ushort language, IntPtr parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResourceNameProc callback, IntPtr parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumResourceLanguages(IntPtr module, IntPtr type, IntPtr name, EnumResourceLanguageProc callback, IntPtr parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BeginUpdateResource(string fileName, bool deleteExistingResources);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr update, bool discard);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResourceEx(IntPtr module, IntPtr type, IntPtr name, ushort language);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr resource);

    private sealed class ResourceName
    {
        public ushort? Id { get; set; }
        public string Text { get; set; }
        public List<ushort> Languages { get; private set; }

        public ResourceName()
        {
            Languages = new List<ushort>();
        }

        public IntPtr Allocate(out bool allocated)
        {
            if (Id.HasValue)
            {
                allocated = false;
                return new IntPtr(Id.Value);
            }

            allocated = true;
            return Marshal.StringToHGlobalUni(Text);
        }
    }

    private sealed class IconImage
    {
        public byte Width { get; set; }
        public byte Height { get; set; }
        public byte ColorCount { get; set; }
        public byte Reserved { get; set; }
        public ushort Planes { get; set; }
        public ushort BitsPerPixel { get; set; }
        public byte[] Data { get; set; }
    }

    private static bool IsIntegerResource(IntPtr value)
    {
        return (value.ToInt64() >> 16) == 0;
    }

    private static List<ResourceName> ReadGroups(string executable)
    {
        var groups = new List<ResourceName>();
        var module = LoadLibraryEx(executable, IntPtr.Zero, LoadLibraryAsDataFile);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to inspect executable resources. Win32 error " + Marshal.GetLastWin32Error());
        }

        try
        {
            EnumResourceNames(module, RtGroupIcon, (handle, type, name, parameter) =>
            {
                var resource = new ResourceName();
                if (IsIntegerResource(name))
                {
                    resource.Id = unchecked((ushort)name.ToInt64());
                }
                else
                {
                    resource.Text = Marshal.PtrToStringUni(name);
                }

                bool allocated;
                var resourcePointer = resource.Allocate(out allocated);
                try
                {
                    EnumResourceLanguages(module, RtGroupIcon, resourcePointer, (languageModule, languageType, languageName, language, languageParameter) =>
                    {
                        resource.Languages.Add(language);
                        return true;
                    }, IntPtr.Zero);
                }
                finally
                {
                    if (allocated)
                    {
                        Marshal.FreeHGlobal(resourcePointer);
                    }
                }

                if (resource.Languages.Count == 0)
                {
                    resource.Languages.Add(0);
                }

                groups.Add(resource);
                return true;
            }, IntPtr.Zero);
        }
        finally
        {
            FreeLibrary(module);
        }

        if (groups.Count == 0)
        {
            groups.Add(new ResourceName { Id = 32512 });
            groups[0].Languages.Add(0);
        }

        return groups;
    }

    private static List<IconImage> ReadIcon(string iconPath)
    {
        using (var stream = File.OpenRead(iconPath))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1)
            {
                throw new InvalidDataException("The supplied file is not a Windows icon.");
            }

            var count = reader.ReadUInt16();
            var entries = new List<Tuple<IconImage, uint, uint>>();
            for (var index = 0; index < count; index++)
            {
                var image = new IconImage
                {
                    Width = reader.ReadByte(),
                    Height = reader.ReadByte(),
                    ColorCount = reader.ReadByte(),
                    Reserved = reader.ReadByte(),
                    Planes = reader.ReadUInt16(),
                    BitsPerPixel = reader.ReadUInt16()
                };
                var size = reader.ReadUInt32();
                var offset = reader.ReadUInt32();
                entries.Add(Tuple.Create(image, size, offset));
            }

            var images = new List<IconImage>();
            foreach (var entry in entries)
            {
                stream.Position = entry.Item3;
                entry.Item1.Data = reader.ReadBytes(checked((int)entry.Item2));
                images.Add(entry.Item1);
            }

            return images;
        }
    }

    private static byte[] BuildGroup(List<IconImage> images, ushort firstResourceId)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);
            for (var index = 0; index < images.Count; index++)
            {
                var image = images[index];
                writer.Write(image.Width);
                writer.Write(image.Height);
                writer.Write(image.ColorCount);
                writer.Write(image.Reserved);
                writer.Write(image.Planes);
                writer.Write(image.BitsPerPixel);
                writer.Write((uint)image.Data.Length);
                writer.Write((ushort)(firstResourceId + index));
            }
            return stream.ToArray();
        }
    }

    private static void Patch(string executable, string iconPath)
    {
        var groups = ReadGroups(executable);
        var images = ReadIcon(iconPath);
        const ushort firstIconId = 5000;
        var groupData = BuildGroup(images, firstIconId);
        var update = BeginUpdateResource(executable, false);
        if (update == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to open executable for icon replacement. Win32 error " + Marshal.GetLastWin32Error());
        }

        var committed = false;
        try
        {
            foreach (var group in groups)
            {
                foreach (var language in group.Languages)
                {
                    for (var index = 0; index < images.Count; index++)
                    {
                        var data = images[index].Data;
                        if (!UpdateResource(update, RtIcon, new IntPtr(firstIconId + index), language, data, (uint)data.Length))
                        {
                            throw new InvalidOperationException("Unable to replace icon image. Win32 error " + Marshal.GetLastWin32Error());
                        }
                    }

                    bool allocated;
                    var groupName = group.Allocate(out allocated);
                    try
                    {
                        if (!UpdateResource(update, RtGroupIcon, groupName, language, groupData, (uint)groupData.Length))
                        {
                            throw new InvalidOperationException("Unable to replace icon group. Win32 error " + Marshal.GetLastWin32Error());
                        }
                    }
                    finally
                    {
                        if (allocated)
                        {
                            Marshal.FreeHGlobal(groupName);
                        }
                    }
                }
            }

            if (!EndUpdateResource(update, false))
            {
                throw new InvalidOperationException("Unable to commit executable resources. Win32 error " + Marshal.GetLastWin32Error());
            }
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                EndUpdateResource(update, true);
            }
        }
    }

    private static byte[] ReadResource(IntPtr module, IntPtr type, IntPtr name, ushort language)
    {
        var resource = FindResourceEx(module, type, name, language);
        if (resource == IntPtr.Zero) throw new InvalidDataException("Icon resource is missing.");
        var bytes = new byte[SizeofResource(module, resource)];
        var address = LockResource(LoadResource(module, resource));
        if (address == IntPtr.Zero) throw new InvalidDataException("Icon resource could not be read.");
        Marshal.Copy(address, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void Verify(string executable, string iconPath)
    {
        var groups = ReadGroups(executable);
        var frames = ReadIcon(iconPath);
        var module = LoadLibraryEx(executable, IntPtr.Zero, LoadLibraryAsDataFile);
        if (module == IntPtr.Zero) throw new InvalidDataException("Could not load resources for verification.");
        try
        {
            foreach (var group in groups)
            {
                bool allocated;
                var name = group.Allocate(out allocated);
                try
                {
                    foreach (var language in group.Languages)
                    {
                        using (var reader = new BinaryReader(new MemoryStream(ReadResource(module, RtGroupIcon, name, language))))
                        {
                            if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1 || reader.ReadUInt16() != frames.Count)
                                throw new InvalidDataException("Icon group header differs from the supplied icon.");
                            foreach (var frame in frames)
                            {
                                if (reader.ReadByte() != frame.Width || reader.ReadByte() != frame.Height ||
                                    reader.ReadByte() != frame.ColorCount || reader.ReadByte() != frame.Reserved ||
                                    reader.ReadUInt16() != frame.Planes || reader.ReadUInt16() != frame.BitsPerPixel ||
                                    reader.ReadUInt32() != frame.Data.Length)
                                    throw new InvalidDataException("Icon frame metadata differs from the supplied icon.");
                                var id = reader.ReadUInt16();
                                if (!ReadResource(module, RtIcon, new IntPtr(id), language).SequenceEqual(frame.Data))
                                    throw new InvalidDataException("Icon image bytes differ from the supplied icon.");
                            }
                        }
                    }
                }
                finally { if (allocated) Marshal.FreeHGlobal(name); }
            }
        }
        finally { FreeLibrary(module); }
        Console.WriteLine("Verified all icon groups/languages and " + frames.Count + " frame sizes: " + executable);
    }

    private static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--verify")
        {
            foreach (var executable in args.Skip(2)) Verify(Path.GetFullPath(executable), Path.GetFullPath(args[1]));
            return 0;
        }
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ResourceIconPatcher [--verify] <icon.ico> <executable> [executable...]");
            return 2;
        }

        for (var index = 1; index < args.Length; index++)
        {
            Patch(Path.GetFullPath(args[index]), Path.GetFullPath(args[0]));
            Console.WriteLine("Updated " + args[index]);
        }

        return 0;
    }
}

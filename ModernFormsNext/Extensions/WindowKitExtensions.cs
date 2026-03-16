using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;

namespace ModernFormsNext
{
    internal static class WindowKitExtensions
    {
        public static Keys AddModifiers (Keys keys, RawInputModifiers modifiers)
        {
            if (modifiers.HasFlag (RawInputModifiers.Alt))
                keys |= ModernFormsNext.Keys.Alt;
            if (modifiers.HasFlag (RawInputModifiers.Control))
                keys |= ModernFormsNext.Keys.Control;
            if (modifiers.HasFlag (RawInputModifiers.Shift))
                keys |= ModernFormsNext.Keys.Shift;

            return keys;
        }

        public static string? GetFullPath (this IStorageFile file)
        {
            if (file is BclStorageFile path)
                return path.FileInfo.FullName;

            return null;
        }

        public static string? GetFullPath (this IStorageFolder file)
        {
            if (file is BclStorageFolder path)
                return path.DirectoryInfo.FullName;

            return null;
        }
    }
}

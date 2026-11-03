using System;

namespace ModernFormsNext.Drawing;

// Shared by runtime and code generation so both admit the same portable identifiers.
internal static class ProjectAssetPath
{
    internal const string ContentPrefix = "Assets/Content/";
    internal const string EmbeddedPrefix = "Assets/Embedded/";
    internal const string ImageProperty = "ProjectImageAsset";

    internal static bool IsImageControl(string? typeName)
    {
        var name = typeName?.Trim();
        if (name?.StartsWith("global::", StringComparison.Ordinal) == true)
        {
            name = name[8..];
            if (!name.StartsWith("ModernFormsNext.", StringComparison.Ordinal))
                return false;
        }
        if (name?.StartsWith("ModernFormsNext.", StringComparison.Ordinal) == true)
            name = name["ModernFormsNext.".Length..];
        return name is "PictureBox" or "Button" or "CheckBox" or "Label" or "LinkLabel" or "RadioButton" or "FormTitleBar";
    }

    internal static bool IsValidId(string? id)
    {
        if (id is null || id.Length > 240 ||
            !(id.StartsWith(ContentPrefix, StringComparison.Ordinal)
                || id.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)))
            return false;

        foreach (var segment in id.Split('/'))
        {
            if (segment.Length == 0 || segment[0] is '.' or ' ' || segment[^1] is '.' or ' ')
                return false;
            foreach (char character in segment)
                if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or ' '))
                    return false;

            var stem = segment.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL"
                || (stem.Length == 4 && char.IsAsciiDigit(stem[3])
                    && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))))
                return false;
        }

        return true;
    }
}

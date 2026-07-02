namespace ModernFormsNext.Designer.Services;

internal static class DesignerDocumentPath
{
    public static string? NormalizeDesignPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var extension = Path.GetExtension(fullPath);

            if (string.Equals(extension, ".mfdesign", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(fullPath);

            if (fileName.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^".Designer".Length];

            return Path.Combine(directory, $"{fileName}.mfdesign");
        }
        catch
        {
            return path;
        }
    }
}

namespace ModernFormsNext.Designer.Services;

internal static class DesignerDocumentPath
{
    public static string? NormalizeDesignPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var fullPath = IOPath.GetFullPath(path);
            var extension = IOPath.GetExtension(fullPath);

            if (string.Equals(extension, ".mfdesign", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            var directory = IOPath.GetDirectoryName(fullPath) ?? string.Empty;
            var fileName = IOPath.GetFileNameWithoutExtension(fullPath);

            if (fileName.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^".Designer".Length];

            return IOPath.Combine(directory, $"{fileName}.mfdesign");
        }
        catch
        {
            return path;
        }
    }
}

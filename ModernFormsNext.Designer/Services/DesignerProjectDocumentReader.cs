using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Designer.Services;

// Companions and inherited designs share the canonical serializer admission.
// Host context supplies the root. Document data cannot widen it or follow links.
internal static class DesignerProjectDocumentReader
{
    internal static string RequireProjectPath(string projectDirectory, string path)
    {
        var root = ProjectImageAsset.RequireLocalRoot(projectDirectory);
        var fullPath = IOPath.GetFullPath(path);
        var relative = IOPath.GetRelativePath(root, fullPath);
        if (IOPath.IsPathRooted(relative) || relative.Split(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
            .Any(segment => segment is "." or ".." or ""))
            throw new IOException("The design companion must belong to the current project.");

        var current = root;
        foreach (var segment in relative.Split(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar))
        {
            current = IOPath.Combine(current, segment);
            ProjectImageAsset.RequireOrdinaryEntry(current);
        }
        return fullPath;
    }

    internal static DesignDocument Load(string projectDirectory, string path)
    {
        if (!string.Equals(IOPath.GetExtension(path), ".mfdesign", StringComparison.OrdinalIgnoreCase))
            throw new IOException("A design companion must be an .mfdesign document.");
        return DesignDocumentSerializer.Default.Load(RequireProjectPath(projectDirectory, path));
    }
}

using System.Security.Cryptography;
using System.Text;

namespace ModernFormsNext.Designer.ExecutablePreview;

internal static class PreviewManifest
{
    internal const int MaxFiles = 16;
    internal const long MaxFileBytes = 32 * 1024 * 1024;
    internal const long MaxTotalBytes = 64 * 1024 * 1024;

    internal static string GetDigest(IEnumerable<(string Name, string Hash)> files)
    {
        // Policy prefix invalidates a prior digest if the approved authority ever changes.
        var manifest = new StringBuilder("ModernFormsNext Windows LPAC static preview v1\n");
        foreach (var file in files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            manifest.Append(file.Name).Append(':').Append(file.Hash).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    internal static bool IsAssemblyName(string? value)
        => PreviewWire.IsIdentifier(value, qualified: true) && !value!.Contains('+');
}

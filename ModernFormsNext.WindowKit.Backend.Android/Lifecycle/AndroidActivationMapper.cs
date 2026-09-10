using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

internal static class AndroidActivationMapper
{
    internal static PlatformApplicationActivation Create(string? data, IEnumerable<string>? streams = null)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        int totalLength = 0;
        if (streams is not null)
        {
            foreach (string file in streams)
            {
                // ClipData and EXTRA_STREAM can duplicate the same items. Bound raw
                // enumeration as well as the detached payload before URI parsing begins.
                if (++count > PlatformApplicationActivation.MaximumItemCount * 2 + 1)
                    throw new ArgumentException("Too many Android activation items.", nameof(streams));
                if (string.IsNullOrWhiteSpace(file) || file.Length > PlatformApplicationActivation.MaximumItemLength)
                    throw new ArgumentException("Invalid Android activation item length.", nameof(streams));
                if (!files.Add(file)) continue;
                totalLength += file.Length;
                if (files.Count > PlatformApplicationActivation.MaximumItemCount ||
                    totalLength > PlatformApplicationActivation.MaximumTotalLength)
                    throw new ArgumentException("Android activation payload exceeds the supported limits.", nameof(streams));
                if (!Uri.TryCreate(file, UriKind.Absolute, out _))
                    throw new ArgumentException("Android stream identifiers must be absolute URIs.", nameof(streams));
            }
        }
        if (files.Count > 0) return new PlatformApplicationActivation(PlatformActivationKind.Files, files: files);
        if (string.IsNullOrEmpty(data)) return new PlatformApplicationActivation(PlatformActivationKind.Launch);
        if (data.Length > PlatformApplicationActivation.MaximumItemLength)
            throw new ArgumentException("Android activation URI exceeds the supported length.", nameof(data));
        if (!Uri.TryCreate(data, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("Android activation requires an absolute URI.", nameof(data));
        // Content-provider identifiers remain URIs, never guessed filesystem paths. Permission
        // grants and stream access remain native/backend responsibilities.
        if (uri.Scheme is "file" or "content")
            return new PlatformApplicationActivation(PlatformActivationKind.Files, files: [uri.AbsoluteUri]);
        return new PlatformApplicationActivation(uri.Scheme is "http" or "https"
            ? PlatformActivationKind.Uri : PlatformActivationKind.Protocol, uri: uri);
    }
}

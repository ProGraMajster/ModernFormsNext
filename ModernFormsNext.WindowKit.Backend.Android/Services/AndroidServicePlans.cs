using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// Deterministic policy shared by the real Intent builder and ordinary net10 contract tests.
internal static class AndroidServicePlans
{
    private static readonly Dictionary<string, string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["txt"] = "text/plain", ["log"] = "text/plain", ["csv"] = "text/csv",
        ["json"] = "application/json", ["pdf"] = "application/pdf", ["xml"] = "application/xml",
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif", ["webp"] = "image/webp", ["svg"] = "image/svg+xml",
        ["mp3"] = "audio/mpeg", ["mp4"] = "video/mp4", ["zip"] = "application/zip"
    };

    internal static string? MimeForExtension(string extension) => KnownTypes.GetValueOrDefault(extension.TrimStart('.'));
    internal static bool IsMime(string value)
    {
        var parts = value.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && (!p.Contains('*') || p == "*") && p.All(c => char.IsAsciiLetterOrDigit(c) || "!#$&^_.+-*".Contains(c))) &&
            (parts[0] != "*" || parts[1] == "*");
    }
    internal static string ValidateMime(string mime)
        => IsMime(mime) ? mime.ToLowerInvariant() : throw new ArgumentException("Expected a MIME type, not a file extension.", nameof(mime));

    internal static string[] MimeTypes(IReadOnlyList<FilePickerFileType>? filters, Func<string, string?>? nativeLookup = null)
    {
        if (filters is null || filters.Count == 0) return ["*/*"];
        var result = new List<string>();
        foreach (var filter in filters)
        {
            if (filter.MimeTypes is { Count: > 0 })
            {
                foreach (var mime in filter.MimeTypes) result.Add(ValidateMime(mime));
                continue;
            }
            if (filter.Patterns is not { Count: > 0 }) return ["*/*"];
            foreach (var pattern in filter.Patterns)
            {
                // SAF supports MIME, not arbitrary globs. Widen an unrepresentable filter
                // rather than silently exclude a file the caller intended to permit.
                if (!pattern.StartsWith("*.") || pattern[2..].IndexOfAny(['*', '?', '/', '\\']) >= 0) return ["*/*"];
                var extension = pattern[2..].ToLowerInvariant();
                string? mime = MimeForExtension(extension) ?? nativeLookup?.Invoke(extension);
                if (mime is null) return ["*/*"];
                result.Add(mime);
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static Uri[] UniqueContentUris(IEnumerable<Uri> uris)
        => uris.Where(u => u.IsAbsoluteUri && u.Scheme == "content" && !string.IsNullOrEmpty(u.Host))
            .DistinctBy(u => u.AbsoluteUri, StringComparer.Ordinal).ToArray();

    internal static Uri[] SelectUris(IEnumerable<Uri> clipData, Uri? data)
    {
        var result = UniqueContentUris(clipData);
        return result.Length > 0 || data is null ? result : UniqueContentUris([data]);
    }
    internal static void ValidateUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri) throw new ArgumentException("An absolute URI is required.", nameof(uri));
    }
    internal static bool IsExternalUri(Uri uri)
    {
        ValidateUri(uri);
        return uri.Scheme is not ("file" or "content" or "intent" or "javascript" or "data");
    }
    internal static string ViewAction(Uri uri) => uri.Scheme == "tel" ? "android.intent.action.DIAL" : "android.intent.action.VIEW";

    internal static ShareIntentPlan? Share(PlatformShareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);
        var mime = request.MimeType is null ? null : ValidateMime(request.MimeType);
        if (request.Items.Count > 32) throw new ArgumentException("At most 32 share attachments are supported.", nameof(request));
        if (request.Items.Any(u => u is null || !u.IsAbsoluteUri || u.Scheme != "content" || string.IsNullOrEmpty(u.Host)))
            return null;
        var items = UniqueContentUris(request.Items);
        if (items.Length == 0 && string.IsNullOrEmpty(request.Text))
            throw new ArgumentException("Share text or attachments are required.", nameof(request));
        return new(items.Length > 1 ? "android.intent.action.SEND_MULTIPLE" : "android.intent.action.SEND",
            mime ?? (items.Length == 0 ? "text/plain" : "*/*"), items, items.Length > 0);
    }

    internal sealed record ShareIntentPlan(string Action, string Mime, Uri[] Items, bool GrantReadAccess);

    internal static string SuggestedName(string? name, string? extension)
    {
        string value = string.IsNullOrWhiteSpace(name) ? "document" : System.IO.Path.GetFileName(name);
        if (!string.IsNullOrWhiteSpace(extension) && !System.IO.Path.HasExtension(value))
            value += "." + extension.TrimStart('.');
        return value;
    }
}

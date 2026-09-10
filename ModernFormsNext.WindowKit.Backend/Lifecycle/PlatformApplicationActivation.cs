using System.Collections.ObjectModel;

namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Identifies an explicit activation source without interpreting or executing its data.</summary>
public enum PlatformActivationKind
{
    /// <summary>A normal application launch.</summary>
    Launch,
    /// <summary>A launch or reactivation with command-line arguments.</summary>
    Arguments,
    /// <summary>Explicit file or document activation.</summary>
    Files,
    /// <summary>An explicit URI or deep-link activation.</summary>
    Uri,
    /// <summary>An explicit registered-protocol activation.</summary>
    Protocol,
    /// <summary>A notification activation descriptor; native support is backend-dependent.</summary>
    Notification,
    /// <summary>Activation forwarded by a secondary instance; this does not implement IPC.</summary>
    SecondaryInstance
}

/// <summary>Contains immutable, bounded activation data supplied explicitly by an application or backend.</summary>
/// <remarks>
/// Arguments and file identifiers are copied, never auto-detected, opened, executed, or logged.
/// File identifiers may be paths or platform-neutral document URI strings; this type grants no
/// access to their contents. Native permission objects remain owned by the backend. Diagnostics
/// should report only <see cref="Kind"/> and counts unless the application explicitly requests data.
/// </remarks>
public sealed class PlatformApplicationActivation
{
    /// <summary>Gets the maximum number of arguments or file identifiers in either list.</summary>
    public const int MaximumItemCount = 64;
    /// <summary>Gets the maximum UTF-16 character count of one argument, file identifier, or URI.</summary>
    public const int MaximumItemLength = 4096;
    /// <summary>Gets the maximum combined UTF-16 character count of all payload strings.</summary>
    public const int MaximumTotalLength = 65536;

    /// <summary>Creates activation data with explicit kind and copied payloads.</summary>
    /// <param name="kind">The declared activation kind; no kind is inferred from strings.</param>
    /// <param name="arguments">Optional command-line arguments; empty arguments are preserved.</param>
    /// <param name="files">Optional non-empty file or document identifiers.</param>
    /// <param name="uri">Optional absolute URI, copied without executing its scheme.</param>
    /// <exception cref="ArgumentException">A required payload is missing, invalid, or exceeds a bound.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The activation kind is undefined.</exception>
    public PlatformApplicationActivation(
        PlatformActivationKind kind,
        IEnumerable<string>? arguments = null,
        IEnumerable<string>? files = null,
        Uri? uri = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        int totalLength = 0;
        Arguments = Copy(arguments, nameof(arguments), false, ref totalLength);
        Files = Copy(files, nameof(files), true, ref totalLength);
        if (uri is not null)
        {
            if (!uri.IsAbsoluteUri) throw new ArgumentException("Activation URIs must be absolute.", nameof(uri));
            ValidateLength(uri.OriginalString, nameof(uri), ref totalLength);
            Uri = new Uri(uri.OriginalString, UriKind.Absolute);
        }
        if (kind == PlatformActivationKind.Arguments && Arguments.Count == 0)
            throw new ArgumentException("Argument activation requires at least one argument.", nameof(arguments));
        if (kind == PlatformActivationKind.Files && Files.Count == 0)
            throw new ArgumentException("File activation requires at least one file identifier.", nameof(files));
        if (kind is PlatformActivationKind.Uri or PlatformActivationKind.Protocol && Uri is null)
            throw new ArgumentException("URI or protocol activation requires an absolute URI.", nameof(uri));
        Kind = kind;
    }

    /// <summary>Gets the explicit activation kind.</summary>
    public PlatformActivationKind Kind { get; }
    /// <summary>Gets the immutable copy of command-line arguments.</summary>
    public IReadOnlyList<string> Arguments { get; }
    /// <summary>Gets the immutable copy of file or document identifiers.</summary>
    public IReadOnlyList<string> Files { get; }
    /// <summary>Gets the copied absolute URI, or <see langword="null"/>.</summary>
    public Uri? Uri { get; }

    private static ReadOnlyCollection<string> Copy(
        IEnumerable<string>? source, string parameter, bool requireContent, ref int totalLength)
    {
        var result = new List<string>();
        if (source is not null)
        {
            foreach (string item in source)
            {
                if (result.Count == MaximumItemCount)
                    throw new ArgumentException($"Activation lists may contain at most {MaximumItemCount} items.", parameter);
                if (item is null || requireContent && string.IsNullOrWhiteSpace(item))
                    throw new ArgumentException("Activation items cannot be null and file identifiers must have content.", parameter);
                ValidateLength(item, parameter, ref totalLength);
                result.Add(item);
            }
        }
        return result.AsReadOnly();
    }

    private static void ValidateLength(string value, string parameter, ref int totalLength)
    {
        if (value.Length > MaximumItemLength || value.Length > MaximumTotalLength - totalLength)
            throw new ArgumentException("The activation payload exceeds its documented character limit.", parameter);
        totalLength += value.Length;
    }
}

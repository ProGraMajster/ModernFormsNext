using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;

namespace ModernFormsNext.Testing;

/// <summary>Provides isolated in-memory clipboard content through the production clipboard contract.</summary>
/// <remarks>
/// <para>Operations complete immediately on the host UI thread and never access the OS clipboard.</para>
/// <para>
/// Supported values are null, strings, byte arrays, string arrays, and immutable primitive numeric,
/// Boolean and character values. Arrays are copied on both write and read. No arbitrary object
/// serialization, conversion, file access, native formats, or native clipboard interoperability is
/// simulated. Unsupported values and duplicate formats reject the entire write atomically.
/// </para>
/// </remarks>
public sealed class TestClipboard : IClipboard
{
    private const int MaximumFormats = 256;
    private const int MaximumValueLength = 4 * 1024 * 1024;
    private readonly UiTestDispatcher dispatcher;
    private Dictionary<string, object?> values = new(StringComparer.Ordinal);
    private bool disposed;

    internal TestClipboard(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <inheritdoc/>
    public Task<string?> GetTextAsync()
    {
        VerifyAccess();
        return Task.FromResult(values.TryGetValue(DataFormats.Text, out var value) ? value as string : null);
    }

    /// <inheritdoc/>
    /// <remarks>Replaces all formats with text; null clears all content.</remarks>
    public Task SetTextAsync(string? text)
    {
        VerifyAccess();
        if (text is { Length: > MaximumValueLength })
            throw new ArgumentException("Clipboard text exceeds the four-million-character limit.", nameof(text));
        values = text is null ? new(StringComparer.Ordinal) : new(StringComparer.Ordinal) { [DataFormats.Text] = text };
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync()
    {
        VerifyAccess();
        values.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// At most 256 distinct nonempty format identifiers are accepted. Each string or byte array
    /// is limited to 4,194,304 elements, and string arrays to 65,536 entries and 4,194,304 total
    /// characters. The data object's getters run during this call, before any content is replaced.
    /// </remarks>
    public Task SetDataObjectAsync(IDataObject data)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(data);
        var replacement = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var format in data.GetDataFormats())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(format);
            if (replacement.Count >= MaximumFormats)
                throw new ArgumentException("The clipboard contains too many formats.", nameof(data));
            if (!replacement.TryAdd(format, CopyValue(data.Get(format))))
                throw new ArgumentException($"The clipboard format '{format}' is duplicated.", nameof(data));
        }

        // User-supplied getters may dispose the host or reenter this fake. Do not publish data
        // into an expired host after those callbacks have completed.
        VerifyAccess();
        values = replacement;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Returns ordinally sorted identifiers in a detached array.</remarks>
    public Task<string[]> GetFormatsAsync()
    {
        VerifyAccess();
        return Task.FromResult(values.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    /// <inheritdoc/>
    public Task<object?> GetDataAsync(string format)
    {
        VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return Task.FromResult(values.TryGetValue(format, out var value) ? CopyValue(value) : null);
    }

    internal void Dispose()
    {
        if (disposed)
            return;
        dispatcher.VerifyAccess();
        disposed = true;
        values.Clear();
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispatcher.VerifyAccess();
    }

    private static object? CopyValue(object? value)
    {
        switch (value)
        {
            case null or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return value;
            case string text when text.Length <= MaximumValueLength:
                return text;
            case byte[] bytes when bytes.Length <= MaximumValueLength:
                return bytes.ToArray();
            case string[] strings when strings.Length <= 65536:
                long characters = 0;
                foreach (var item in strings)
                {
                    if (item is null || (characters += item.Length) > MaximumValueLength)
                        throw new ArgumentException("Clipboard string arrays require non-null strings within the character limit.", nameof(value));
                }
                return strings.ToArray();
            default:
                throw new ArgumentException("The clipboard value is unsupported or exceeds the bounded in-memory limits.", nameof(value));
        }
    }
}

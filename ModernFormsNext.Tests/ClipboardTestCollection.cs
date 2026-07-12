using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[CollectionDefinition("Clipboard", DisableParallelization = true)]
public sealed class ClipboardTestCollection;

internal static class ClipboardTestService
{
    public static InMemoryClipboard GetOrRegister()
    {
        var clipboard = AvaloniaGlobals.GetService<IClipboard>();
        if (clipboard is InMemoryClipboard existing)
            return existing;

        if (clipboard is not null)
            throw new InvalidOperationException("Tests require the shared in-memory clipboard service.");

        var created = new InMemoryClipboard();
        AvaloniaGlobals.AddService<IClipboard>(created);
        return created;
    }

    internal sealed class InMemoryClipboard : IClipboard
    {
        public string? Text { get; set; }

        public Task ClearAsync()
        {
            Text = null;
            return Task.CompletedTask;
        }

        public Task<object?> GetDataAsync(string format) => Task.FromResult<object?>(null);

        public Task<string[]> GetFormatsAsync() => Task.FromResult(Array.Empty<string>());

        public Task<string?> GetTextAsync() => Task.FromResult(Text);

        public Task SetDataObjectAsync(IDataObject data) => Task.CompletedTask;

        public Task SetTextAsync(string? value)
        {
            Text = value;
            return Task.CompletedTask;
        }
    }
}

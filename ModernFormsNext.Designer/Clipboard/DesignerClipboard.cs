namespace ModernFormsNext.Designer.Clipboard;

/// <summary>
/// Stores Designer clipboard content without depending on the runtime or operating-system clipboard.
/// </summary>
/// <remarks>
/// Content is an immutable string payload. Keeping the abstraction data-only allows one Designer
/// session to copy between open documents without retaining controls, selections, histories, or
/// host services from the source document.
/// </remarks>
internal interface IDesignerClipboard
{
    event EventHandler? Changed;

    string? Content { get; }

    void SetContent(string content);

    void Clear();
}

internal sealed class DesignerClipboard : IDesignerClipboard
{
    private string? content;

    public event EventHandler? Changed;

    public string? Content => content;

    public void SetContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (string.Equals(this.content, content, StringComparison.Ordinal))
            return;

        this.content = content;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (content is null)
            return;

        content = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerEventDescriptor
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public Func<string?> GetHandlerName { get; init; } = () => null;

    public Func<string?, (bool Success, string? Error)> CommitHandlerName { get; init; } = _ => (false, "The event cannot be edited.");

    public string GetValueText()
        => GetHandlerName() ?? string.Empty;

    public bool TryCommit(string text, out string? error)
    {
        var handlerName = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var result = CommitHandlerName(handlerName);
        error = result.Error;
        return result.Success;
    }
}

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyDescriptor
{
    public required string Name { get; init; }

    public string Path { get; init; } = string.Empty;

    public required string DisplayName { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public required Type ValueType { get; init; }

    public bool IsReadOnly { get; init; }

    public bool IsVisible { get; init; } = true;

    public bool IsAdvanced { get; init; }

    public bool IsExpanded { get; set; }

    public int Depth { get; init; }

    public bool HasDialogEditor { get; init; }

    public Func<DesignerPropertyDialogContext, Task<bool>>? DialogEditor { get; init; }

    public List<DesignerPropertyDescriptor> Children { get; } = [];

    public bool HasChildren => Children.Count > 0;

    public string Identity => string.IsNullOrWhiteSpace(Path) ? Name : Path;

    public bool ShouldSerialize { get; init; } = true;

    public IReadOnlyList<string>? StandardValues { get; init; }

    public Func<object?> GetValue { get; init; } = () => null;

    public Func<string, (bool Success, string? Error)> CommitText { get; init; } = _ => (false, "The property is read-only.");

    public string GetValueText()
        => DesignerPropertyValueEditor.ToDisplayString(GetValue());

    public bool TryCommit(string text, out string? error)
    {
        if (IsReadOnly)
        {
            error = "The property is read-only.";
            return false;
        }

        var result = CommitText(text);
        error = result.Error;
        return result.Success;
    }
}

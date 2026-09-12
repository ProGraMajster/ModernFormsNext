namespace ModernFormsNext;

public partial class RichTextBox
{
    /// <inheritdoc/>
    protected override void InvalidateAccessibleTextLayout()
    {
        cachedRichTextBlock = null;
        base.InvalidateAccessibleTextLayout();
    }
}

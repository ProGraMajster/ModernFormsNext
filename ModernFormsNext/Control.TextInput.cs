using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

public partial class Control
{
    /// <summary>Gets the text-service adapter for this control's existing editor, if any.</summary>
    /// <returns>A stable borrowed adapter, or null for a control that does not edit text.</returns>
    /// <remarks>
    /// Override to expose a custom document editor without inheriting TextBox. The adapter must
    /// use the existing document, raise state notifications and report caret geometry in this
    /// control's logical client coordinates. Called on the UI thread. The host owns focus and
    /// native session lifetime; returning an adapter neither selects nor disposes the control.
    /// </remarks>
    protected virtual ITextInputClient? GetTextInputClient() => null;

    internal ITextInputClient? QueryTextInputClient() => GetTextInputClient();

    private ControlTextInputHost? FindTextInputHost()
    {
        for (var current = this; current is not null; current = current.Parent)
            if (current is IControlTextInputRoot root) return root.TextInputHost;
        return null;
    }

    internal void NotifyTextInputFocusChanged() => FindTextInputHost()?.Refresh();
    internal IDisposable? BeginTextInputTreeChange() => FindTextInputHost()?.BeginTreeChange(this);
}

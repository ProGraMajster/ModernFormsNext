using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    internal ControlTextInputHost TextInputHost { get; private set; } = null!;

    /// <summary>Gets the revocable text-service client for the current canonical focused control.</summary>
    /// <remarks>
    /// Use on the UI thread. Each focus/activation session has a separate borrowed instance;
    /// retained clients reject input after retirement. Caret geometry uses logical window-client
    /// coordinates. This property does not create a focus owner or a document.
    /// </remarks>
    public ITextInputClient? TextInputClient => TextInputHost.Client;

    /// <summary>Gets detached session metadata without entered text or editor references.</summary>
    public TextInputDiagnostics TextInputDiagnostics => TextInputHost.GetDiagnostics();

    /// <summary>Requests software keyboard visibility from the optional native text-service feature.</summary>
    /// <param name="visible">True to show the current editor's keyboard; false to dismiss it.</param>
    /// <returns>Whether the backend accepted the request; actual visibility remains native policy.</returns>
    /// <remarks>Call on the UI thread. The Windows IMM32 adapter does not implement touch-keyboard visibility.</remarks>
    public bool RequestSoftwareKeyboard(bool visible) => TextInputHost.SetKeyboardVisible(visible);

    /// <summary>Temporarily relinquishes or reacquires shared text-service ownership.</summary>
    /// <param name="active">False to retire the current connection; true to reacquire canonical focus.</param>
    /// <remarks>
    /// Use on the UI thread for native editor handoff. Retirement preserves visible provisional
    /// text and prevents late callbacks from reaching another editor. Normal native window
    /// activation/deactivation also updates this state; this does not move framework focus.
    /// A Form defers reacquisition while its active popup owns text input.
    /// </remarks>
    public void SetTextInputActive(bool active)
    {
        // Nonactivating native popups borrow their owner's text method. A repeated owner
        // activation or an older popup's Hide must not replace the current popup client.
        if (active && this is Form && Application.ActivePopupWindow is { } popup &&
            ReferenceEquals(popup.ParentForm, this) && popup.OwnsParentTextInput)
            active = false;
        TextInputHost.SetActive(active);
    }

    private void InitializeTextInput()
    {
        TextInputHost = new(adapter, () => adapter.SelectedControl);
        // A constructed window has no native activation yet. Popups acquire ownership in
        // Show, ordinary windows in Activated; selecting a child during setup must not lend IME.
        TextInputHost.SetActive(false);
        TextInputHost.Attach(window.TryGetFeature(typeof(ITextInputMethod)) as ITextInputMethod);
    }
}

namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Displays standard messages using the operating system's own dialog UI.</summary>
/// <remarks>
/// Calls require the UI thread and return a task without waiting for dismissal. Windows queues
/// MessageBox on that thread's dispatcher and enters the native modal loop only in the queued
/// callback; it revalidates ownership before showing. Implementations must release
/// callbacks and native resources on completion, host loss and shutdown. No custom content is supported.
/// </remarks>
public interface IPlatformMessageDialogService
{
    /// <summary>Shows a native message and returns the zero-based index of the selected standard button.</summary>
    /// <param name="owner">A live, visible backend window, or null for platform-defined unowned presentation.</param>
    /// <param name="request">Immutable plain-text content and standard button/icon choices.</param>
    /// <param name="cancellationToken">Cancellation dismisses the dialog and cancels the task; it is not a button choice.</param>
    /// <returns>
    /// Index in this semantic order (independent of visual order): OK; OK, Cancel; Yes, No;
    /// Yes, No, Cancel; or Retry, Cancel. This neutral transport avoids a dependency on the
    /// control assembly's DialogResult. The framework facade converts the index to DialogResult.
    /// </returns>
    /// <exception cref="PlatformServiceException">The host is unavailable/lost, another native request is busy, or the backend shut down.</exception>
    Task<int> ShowAsync(IWindowBaseImpl? owner, PlatformMessageDialogRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Specifies a standard native message dialog's buttons in semantic order.</summary>
public enum MessageBoxButtons
{
    /// <summary>An acknowledgement button.</summary>
    OK,
    /// <summary>Acknowledge or cancel.</summary>
    OKCancel,
    /// <summary>Answer yes or no; Android Back/outside tap cannot dismiss.</summary>
    YesNo,
    /// <summary>Answer yes, no, or cancel.</summary>
    YesNoCancel,
    /// <summary>Retry the operation or cancel.</summary>
    RetryCancel
}

/// <summary>Specifies the meaning of a native message icon; the platform may adapt its visual representation.</summary>
public enum MessageBoxIcon
{
    /// <summary>No icon.</summary>
    None,
    /// <summary>Information; Android uses its system information drawable.</summary>
    Information,
    /// <summary>Warning; Android uses its system alert drawable.</summary>
    Warning,
    /// <summary>Error; Android adapts this to its system alert drawable.</summary>
    Error,
    /// <summary>Question; Android omits the icon because no standard question drawable exists.</summary>
    Question
}

/// <summary>Immutable platform-neutral content for a standard native message dialog.</summary>
/// <remarks>Contains no Activity, HWND, custom view, native flags or control objects. Text is never logged by the services.</remarks>
public sealed class PlatformMessageDialogRequest
{
    /// <summary>Creates and validates a request without showing UI.</summary>
    /// <param name="message">Plain text body; empty is allowed, null and embedded NUL are rejected.</param>
    /// <param name="title">Plain text title; empty is allowed, null and embedded NUL are rejected.</param>
    /// <param name="buttons">Standard buttons; their semantic order defines the returned index.</param>
    /// <param name="icon">Semantic icon hint.</param>
    public PlatformMessageDialogRequest(string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);
        if (message.Contains('\0') || title.Contains('\0')) throw new ArgumentException("Native dialog text cannot contain NUL.");
        if (!Enum.IsDefined(buttons)) throw new ArgumentOutOfRangeException(nameof(buttons));
        if (!Enum.IsDefined(icon)) throw new ArgumentOutOfRangeException(nameof(icon));
        Message = message; Title = title; Buttons = buttons; Icon = icon;
    }
    /// <summary>Gets the plain text body.</summary>
    public string Message { get; }
    /// <summary>Gets the plain text title.</summary>
    public string Title { get; }
    /// <summary>Gets the standard button set.</summary>
    public MessageBoxButtons Buttons { get; }
    /// <summary>Gets the semantic icon hint.</summary>
    public MessageBoxIcon Icon { get; }
    /// <summary>Gets the number of valid selection indices.</summary>
    public int ButtonCount => Buttons == MessageBoxButtons.OK ? 1 : Buttons == MessageBoxButtons.YesNoCancel ? 3 : 2;
    /// <summary>Gets the Cancel button index, or -1 if no Cancel choice exists.</summary>
    public int CancelButtonIndex => Buttons switch
    {
        MessageBoxButtons.OKCancel or MessageBoxButtons.RetryCancel => 1,
        MessageBoxButtons.YesNoCancel => 2,
        _ => -1
    };
}

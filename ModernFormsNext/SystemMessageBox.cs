using System;
using System.Threading;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext;

/// <summary>Shows an OS-native message dialog, independently of the framework-rendered MessageBoxForm.</summary>
/// <remarks>
/// Call on the UI thread. Both backends return a task without waiting for dismissal. Android
/// shows AlertDialog immediately; Windows schedules MessageBoxW on the owning UI dispatcher.
/// That scheduled callback uses the native modal loop, never a worker thread. Keep the owner
/// alive until completion. No shared nested loop or synchronous compatibility API is provided.
/// Owner must be live and visible. Android uses that Form's current presentation, never an
/// unrelated Activity. Null owner uses an eligible resumed Activity on Android and a task-modal
/// unowned dialog on Windows. Unavailable, Busy, HostLost and Shutdown are explicit service errors.
/// Back/outside tap on Android select Cancel only for button sets containing Cancel. Recreation
/// retires the dialog with HostLost, without replay. Cancellation cancels the task, not a user choice.
/// Native UI owns styling, layout, focus and accessibility; this API creates no framework controls.
/// Android OK/Cancel use system strings. Yes/No/Retry use mfn_message_* Android resources with
/// English defaults and Polish qualifiers; applications can supply other locale qualifiers.
/// </remarks>
/// <example>
/// <code>
/// using ModernFormsNext.WindowKit.Platform.Services;
/// DialogResult result = await SystemMessageBox.ShowAsync(this,
///     "Delete the selected file?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
/// if (result == DialogResult.Yes) DeleteSelectedFile();
/// </code>
/// </example>
public static class SystemMessageBox
{
    /// <summary>Shows a standard native message using an optional visible Form owner.</summary>
    /// <param name="owner">The owning Form, or null for the platform's documented no-owner behavior.</param>
    /// <param name="message">Non-null plain text body.</param>
    /// <param name="title">Non-null plain text title.</param>
    /// <param name="buttons">Standard buttons; defaults to OK.</param>
    /// <param name="icon">Semantic icon hint; defaults to no icon.</param>
    /// <param name="cancellationToken">Pre-cancellation shows nothing; later cancellation dismisses native UI.</param>
    /// <returns>The existing DialogResult corresponding to the user's choice.</returns>
    /// <exception cref="PlatformServiceException">The service cannot present, loses its host, is busy, or shuts down.</exception>
    public static async Task<DialogResult> ShowAsync(Form? owner, string message, string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.VerifyAccess();
        cancellationToken.ThrowIfCancellationRequested();
        var request = new PlatformMessageDialogRequest(message, title, buttons, icon);
        if (owner is not null) {
            ObjectDisposedException.ThrowIf(owner.IsBackendClosed, owner);
            if (!owner.Visible) throw new PlatformServiceException(PlatformServiceStatus.Unavailable);
        }
        var service = AvaloniaGlobals.GetService<IPlatformMessageDialogService>()
            ?? throw new PlatformServiceException(PlatformServiceStatus.NotSupported);
        // Only neutral result mapping remains after await. Do not depend on a UI context that
        // may already have stopped when shutdown completes the backend request.
        int index = await service.ShowAsync(owner?.window, request, cancellationToken).ConfigureAwait(false);
        if ((uint)index >= request.ButtonCount)
            throw new InvalidOperationException("The native message service returned an invalid button index.");
        return (buttons, index) switch {
            (MessageBoxButtons.OK or MessageBoxButtons.OKCancel, 0) => DialogResult.OK,
            (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, 0) => DialogResult.Yes,
            (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, 1) => DialogResult.No,
            (MessageBoxButtons.RetryCancel, 0) => DialogResult.Retry,
            _ => DialogResult.Cancel
        };
    }
}

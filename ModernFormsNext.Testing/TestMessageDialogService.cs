using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.Testing;

/// <summary>Isolated, deterministic native message substitute; never opens operating-system UI.</summary>
/// <remarks>Configure and call on the TestHost UI thread. Outcomes are one-shot; recorded content is cleared on host disposal.</remarks>
public sealed class TestMessageDialogService : IPlatformMessageDialogService
{
    private readonly UiTestDispatcher dispatcher;
    private bool disposed;
    internal TestMessageDialogService(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;
    /// <summary>Gets or sets the next user choice; it resets to OK. Configure a choice valid for the requested buttons.</summary>
    public DialogResult NextResult { get; set; } = DialogResult.OK;
    /// <summary>Gets or sets a one-shot service failure status, such as Unavailable.</summary>
    public PlatformServiceStatus NextStatus { get; set; } = PlatformServiceStatus.Success;
    /// <summary>Gets or sets a one-shot exception, including OperationCanceledException.</summary>
    public Exception? NextException { get; set; }
    /// <summary>Gets the last request accepted by the fake, or null before use/disposal.</summary>
    public PlatformMessageDialogRequest? LastRequest { get; private set; }
    /// <summary>Gets the exact backend owner passed by the production facade, or null for an unowned call.</summary>
    public IWindowBaseImpl? LastOwner { get; private set; }
    /// <inheritdoc/>
    public Task<int> ShowAsync(IWindowBaseImpl? owner, PlatformMessageDialogRequest request, CancellationToken cancellationToken = default)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request; LastOwner = owner;
        var result = NextResult; NextResult = DialogResult.OK;
        var status = NextStatus; NextStatus = PlatformServiceStatus.Success;
        var error = NextException; NextException = null;
        if (error is not null) return Task.FromException<int>(error);
        if (status != PlatformServiceStatus.Success) return Task.FromException<int>(new PlatformServiceException(status));
        int index = (request.Buttons, result) switch {
            (MessageBoxButtons.OK or MessageBoxButtons.OKCancel, DialogResult.OK) => 0,
            (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, DialogResult.Yes) => 0,
            (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, DialogResult.No) => 1,
            (MessageBoxButtons.RetryCancel, DialogResult.Retry) => 0,
            (_, DialogResult.Cancel) => request.CancelButtonIndex,
            _ => -1
        };
        if (index < 0) throw new InvalidOperationException("Configure a result belonging to the requested buttons.");
        return Task.FromResult(index);
    }
    internal void Dispose() { disposed = true; LastRequest = null; LastOwner = null; NextException = null; }
}

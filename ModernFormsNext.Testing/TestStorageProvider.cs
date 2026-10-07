using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;

namespace ModernFormsNext.Testing;

/// <summary>Returns caller-configured storage selections through the production dialog facades.</summary>
/// <remarks>
/// No native UI is opened. Empty/null selections model user cancel. Caller cancellation throws,
/// and NextException models provider, lifetime or native failures. Returned items remain caller-owned.
/// </remarks>
public sealed class TestStorageProvider : BclStorageProvider
{
    private readonly UiTestDispatcher dispatcher;
    private bool disposed;
    internal TestStorageProvider(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;
    /// <summary>Gets or sets the files returned by the next open request.</summary>
    public IReadOnlyList<IStorageFile> OpenFiles { get; set; } = Array.Empty<IStorageFile>();
    /// <summary>Gets or sets the file returned by save; null models user cancellation.</summary>
    public IStorageFile? SaveFile { get; set; }
    /// <summary>Gets or sets the folder selection; empty models user cancellation.</summary>
    public IReadOnlyList<IStorageFolder> Folders { get; set; } = Array.Empty<IStorageFolder>();
    /// <summary>Gets or sets a one-shot failure thrown before returning a selection.</summary>
    public Exception? NextException { get; set; }
    /// <inheritdoc/>
    public override bool CanOpen { get { Verify(); return true; } }
    /// <inheritdoc/>
    public override bool CanSave => CanOpen;
    /// <inheritdoc/>
    public override bool CanPickFolder => CanOpen;
    /// <inheritdoc/>
    public override Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        Check(options);
        return Task.FromResult<IReadOnlyList<IStorageFile>>(options.AllowMultiple ? OpenFiles.ToArray() : OpenFiles.Take(1).ToArray());
    }
    /// <inheritdoc/>
    public override Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
    { Check(options); return Task.FromResult(SaveFile); }
    /// <inheritdoc/>
    public override Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
    { Check(options); return Task.FromResult<IReadOnlyList<IStorageFolder>>(Folders.ToArray()); }
    private void Check(PickerOptions options)
    {
        Verify(); ArgumentNullException.ThrowIfNull(options); options.CancellationToken.ThrowIfCancellationRequested();
        var error = NextException; NextException = null;
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private void Verify() { dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(disposed, this); }
    internal void Dispose() { disposed = true; OpenFiles = []; SaveFile = null; Folders = []; NextException = null; }
}

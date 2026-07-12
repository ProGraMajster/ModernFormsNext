using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ModernFormsNext;

/// <summary>
/// Provides selection context and asynchronous deferral support for a hosted Markdown insertion
/// request.
/// </summary>
/// <remarks>
/// A host that must await a custom dialog should call <see cref="GetDeferral"/> before its first
/// await and dispose the returned object after setting the request result. The editor applies the
/// result only after every deferral completes and only when the source version still matches the
/// version captured for the request.
/// </remarks>
public abstract class MarkdownEditorInsertRequestEventArgs : CancelEventArgs
{
    private readonly object syncRoot = new();
    private readonly TaskCompletionSource<bool> deferralsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int deferralCount = 1;
    private bool completionReached;

    internal MarkdownEditorInsertRequestEventArgs(string selectedText, int selectionStart, int selectionLength)
    {
        SelectedText = selectedText;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the host supplied a result for this request.
    /// </summary>
    /// <remarks>
    /// The editor performs no mutation unless this property is <see langword="true"/> and
    /// <see cref="CancelEventArgs.Cancel"/> is <see langword="false"/>.
    /// </remarks>
    public bool Handled { get; set; }

    /// <summary>
    /// Gets the source text selected when the request started.
    /// </summary>
    public string SelectedText { get; }

    /// <summary>
    /// Gets the zero-based UTF-16 start of the selection captured for the request.
    /// </summary>
    public int SelectionStart { get; }

    /// <summary>
    /// Gets the number of UTF-16 code units selected when the request started.
    /// </summary>
    public int SelectionLength { get; }

    /// <summary>
    /// Defers completion of the request while the host awaits an asynchronous operation.
    /// </summary>
    /// <returns>
    /// An object that completes this deferral when disposed. Dispose it exactly once, normally
    /// with a <c>using</c> declaration in an asynchronous event handler.
    /// </returns>
    /// <exception cref="InvalidOperationException">The request has already completed.</exception>
    public IDisposable GetDeferral()
    {
        lock (syncRoot)
        {
            if (completionReached)
                throw new InvalidOperationException("The Markdown insertion request has already completed.");

            deferralCount++;
        }

        return new RequestDeferral(CompleteDeferral);
    }

    internal Task DeferralsCompleted => deferralsCompleted.Task;

    internal void CompleteEventRaise() => CompleteDeferral();

    private void CompleteDeferral()
    {
        var complete = false;
        lock (syncRoot)
        {
            if (deferralCount <= 0)
                return;

            deferralCount--;
            if (deferralCount == 0)
            {
                completionReached = true;
                complete = true;
            }
        }

        if (complete)
            deferralsCompleted.TrySetResult(true);
    }

    private sealed class RequestDeferral : IDisposable
    {
        private Action? complete;

        public RequestDeferral(Action complete) => this.complete = complete;

        public void Dispose() => System.Threading.Interlocked.Exchange(ref complete, null)?.Invoke();
    }
}

/// <summary>
/// Provides data for <see cref="MarkdownEditor.InsertLinkRequested"/>.
/// </summary>
public sealed class InsertLinkRequestEventArgs : MarkdownEditorInsertRequestEventArgs
{
    internal InsertLinkRequestEventArgs(
        string selectedText,
        int selectionStart,
        int selectionLength,
        string suggestedText,
        string suggestedUrl)
        : base(selectedText, selectionStart, selectionLength)
    {
        SuggestedText = suggestedText;
        SuggestedUrl = suggestedUrl;
        Text = suggestedText;
        Url = suggestedUrl;
    }

    /// <summary>
    /// Gets the link label suggested from the selection or existing link at the caret.
    /// </summary>
    public string SuggestedText { get; }

    /// <summary>
    /// Gets the link destination suggested from an existing link at the caret.
    /// </summary>
    public string SuggestedUrl { get; }

    /// <summary>
    /// Gets or sets the final visible link label supplied by the host.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the final link destination supplied by the host.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Provides data for <see cref="MarkdownEditor.InsertImageRequested"/>.
/// </summary>
public sealed class InsertImageRequestEventArgs : MarkdownEditorInsertRequestEventArgs
{
    internal InsertImageRequestEventArgs(
        string selectedText,
        int selectionStart,
        int selectionLength,
        string source,
        string altText,
        string? title)
        : base(selectedText, selectionStart, selectionLength)
    {
        Source = source;
        AltText = altText;
        Title = title;
    }

    /// <summary>
    /// Gets or sets the final image source supplied by the host.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="Source"/> is inserted directly or copied as a local asset.
    /// </summary>
    public MarkdownImageSourceKind SourceKind { get; set; } = MarkdownImageSourceKind.Reference;

    /// <summary>
    /// Gets or sets the final alternative text supplied by the host.
    /// </summary>
    public string AltText { get; set; }

    /// <summary>
    /// Gets or sets asset-copy options used when <see cref="SourceKind"/> is
    /// <see cref="MarkdownImageSourceKind.LocalFile"/>.
    /// </summary>
    /// <remarks>
    /// The host must configure both destination directories. This value is ignored for direct
    /// references and may be <see langword="null"/> until a local file is selected.
    /// </remarks>
    public MarkdownImageAssetOptions? AssetOptions { get; set; }

    /// <summary>
    /// Gets or sets a controlled host error that cancels insertion without changing the source.
    /// </summary>
    /// <remarks>
    /// Use this property for expected picker or host-service failures. The editor forwards the
    /// message through <see cref="MarkdownEditor.ImageInsertFailed"/> after validating that the
    /// request snapshot is still current.
    /// </remarks>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the optional image title supplied by the host.
    /// </summary>
    public string? Title { get; set; }
}

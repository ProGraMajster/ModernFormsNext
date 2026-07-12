using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    private readonly MarkdownEditorInlineElementLocator inlineElementLocator = new();
    private CancellationTokenSource? activeImageAssetOperation;
    private int sourceVersion;

    /// <summary>
    /// Occurs when the editor requests link data from its host.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MarkdownEditorInsertRequestEventArgs.Handled"/> after supplying a non-empty URL. An
    /// asynchronous handler must obtain a deferral before awaiting its dialog. No source mutation
    /// occurs when the request is cancelled, unhandled, stale, read-only, or completed after the
    /// editor is disposed.
    /// </remarks>
    [Category("Action")]
    [Description("Occurs when the host should collect data for a Markdown link.")]
    public event EventHandler<InsertLinkRequestEventArgs>? InsertLinkRequested;

    /// <summary>
    /// Occurs when the editor requests image data from its host.
    /// </summary>
    /// <remarks>
    /// The host may provide a direct reference or select a local file and configure
    /// <see cref="InsertImageRequestEventArgs.AssetOptions"/>. The editor does not open a picker or
    /// assume a project asset directory.
    /// </remarks>
    [Category("Action")]
    [Description("Occurs when the host should collect data for a Markdown image.")]
    public event EventHandler<InsertImageRequestEventArgs>? InsertImageRequested;

    /// <summary>
    /// Occurs when a hosted image request or local asset operation fails without modifying source.
    /// </summary>
    [Category("Action")]
    [Description("Occurs when Markdown image insertion or asset copying fails.")]
    public event EventHandler<MarkdownImageInsertFailedEventArgs>? ImageInsertFailed;

    /// <summary>
    /// Requests link data from the host and starts insertion without blocking the UI thread.
    /// </summary>
    /// <remarks>
    /// Use <see cref="RequestInsertLinkAsync"/> when the caller needs to observe completion. The
    /// built-in toolbar and Ctrl+K use this same public request path.
    /// </remarks>
    public void RequestInsertLink()
    {
        var request = RequestInsertLinkAsync();
        if (request.IsCompleted)
            request.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Requests link data from the host and asynchronously reports whether a link was applied.
    /// </summary>
    /// <returns><see langword="true"/> when the source was changed; otherwise <see langword="false"/>.</returns>
    public async Task<bool> RequestInsertLinkAsync()
    {
        if (!CanStartInsertionRequest())
            return false;

        var snapshot = CreateRequestSnapshot(image: false);
        var existing = snapshot.ExistingElement;
        var suggestedText = existing?.Text
            ?? (snapshot.SelectedText.Length > 0 ? snapshot.SelectedText : "link");
        var args = new InsertLinkRequestEventArgs(
            snapshot.SelectedText,
            snapshot.Selection.Start,
            snapshot.Selection.Length,
            suggestedText,
            existing?.Destination ?? string.Empty);

        try
        {
            OnInsertLinkRequested(args);
        }
        finally
        {
            args.CompleteEventRaise();
        }

        await args.DeferralsCompleted;
        if (!CanApplyInsertionRequest(snapshot, args)
            || string.IsNullOrWhiteSpace(args.Url))
        {
            RestoreRequestSelection(snapshot);
            return false;
        }

        ApplyRequestReplacement(snapshot, CreateLinkMarkdown(args.Text ?? string.Empty, args.Url));
        return true;
    }

    /// <summary>
    /// Requests image data from the host and starts insertion without blocking the UI thread.
    /// </summary>
    /// <remarks>Use <see cref="RequestInsertImageAsync"/> to observe completion.</remarks>
    public void RequestInsertImage()
    {
        var request = RequestInsertImageAsync();
        if (request.IsCompleted)
            request.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Requests image data from the host and asynchronously reports whether an image was applied.
    /// </summary>
    /// <returns><see langword="true"/> when the source was changed; otherwise <see langword="false"/>.</returns>
    public async Task<bool> RequestInsertImageAsync()
    {
        if (!CanStartInsertionRequest())
            return false;

        var snapshot = CreateRequestSnapshot(image: true);
        var existing = snapshot.ExistingElement;
        var args = new InsertImageRequestEventArgs(
            snapshot.SelectedText,
            snapshot.Selection.Start,
            snapshot.Selection.Length,
            existing?.Destination ?? string.Empty,
            existing?.Text ?? (snapshot.SelectedText.Length > 0 ? snapshot.SelectedText : "image"),
            existing?.Title);

        try
        {
            OnInsertImageRequested(args);
        }
        finally
        {
            args.CompleteEventRaise();
        }

        await args.DeferralsCompleted;
        if (!IsInsertionSnapshotCurrent(snapshot) || args.Cancel || !args.Handled)
        {
            RestoreRequestSelection(snapshot);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(args.ErrorMessage))
        {
            ReportImageFailure(args.Source, args.ErrorMessage, null);
            RestoreRequestSelection(snapshot);
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.Source))
        {
            ReportImageFailure(args.Source, "The image source cannot be empty.", null);
            RestoreRequestSelection(snapshot);
            return false;
        }

        var finalSource = args.Source;
        var assetChanged = false;
        if (args.SourceKind == MarkdownImageSourceKind.LocalFile)
        {
            if (args.AssetOptions is null)
            {
                ReportImageFailure(args.Source, "Local image insertion requires host-provided asset options.", null);
                RestoreRequestSelection(snapshot);
                return false;
            }

            var result = await CopyImageAssetAsync(args.Source, args.AssetOptions, CancellationToken.None);
            if (!result.IsSuccess)
            {
                if (result.Status == MarkdownImageAssetStatus.Failed)
                    ReportImageFailure(
                        args.Source,
                        result.ErrorMessage ?? "The image asset could not be copied.",
                        result.Exception);
                RestoreRequestSelection(snapshot);
                return false;
            }

            finalSource = result.MarkdownSource!;
            assetChanged = result.Status == MarkdownImageAssetStatus.Copied;
        }

        if (!CanApplyInsertionRequest(snapshot, args))
        {
            RestoreRequestSelection(snapshot);
            return false;
        }

        ApplyRequestReplacement(snapshot, CreateImageMarkdown(args.AltText ?? string.Empty, finalSource, args.Title));
        if (assetChanged)
            RefreshPreviewImages();
        return true;
    }

    /// <summary>
    /// Copies a host-selected local image and inserts or edits its Markdown element asynchronously.
    /// </summary>
    /// <remarks>
    /// This method is the common integration point for a host file picker and future drag/drop or
    /// image-clipboard adapters. The current selection and source version are captured before I/O;
    /// stale or disposed editors are never modified. A successful insertion is one undo record.
    /// </remarks>
    /// <param name="localFilePath">The local image path supplied by the host.</param>
    /// <param name="options">Host-configured asset destination and validation options.</param>
    /// <param name="altText">Optional alt text; selection or an existing image label is used when omitted.</param>
    /// <param name="title">Optional image title.</param>
    /// <param name="cancellationToken">Cancels copying before source mutation.</param>
    /// <returns><see langword="true"/> when Markdown was changed; otherwise <see langword="false"/>.</returns>
    public async Task<bool> InsertImageAssetAsync(
        string localFilePath,
        MarkdownImageAssetOptions options,
        string? altText = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localFilePath);
        ArgumentNullException.ThrowIfNull(options);
        if (!CanStartInsertionRequest())
            return false;

        var snapshot = CreateRequestSnapshot(image: true);
        var label = altText
            ?? snapshot.ExistingElement?.Text
            ?? (snapshot.SelectedText.Length > 0 ? snapshot.SelectedText : "image");
        var result = await CopyImageAssetAsync(localFilePath, options, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Status == MarkdownImageAssetStatus.Failed)
                ReportImageFailure(
                    localFilePath,
                    result.ErrorMessage ?? "The image asset could not be copied.",
                    result.Exception);
            RestoreRequestSelection(snapshot);
            return false;
        }

        if (!IsInsertionSnapshotCurrent(snapshot))
        {
            RestoreRequestSelection(snapshot);
            return false;
        }

        ApplyRequestReplacement(snapshot, CreateImageMarkdown(label, result.MarkdownSource!, title));
        if (result.Status == MarkdownImageAssetStatus.Copied)
            RefreshPreviewImages();
        return true;
    }

    /// <summary>Raises <see cref="InsertLinkRequested"/>.</summary>
    /// <param name="e">The request context.</param>
    protected virtual void OnInsertLinkRequested(InsertLinkRequestEventArgs e)
        => InsertLinkRequested?.Invoke(this, e);

    /// <summary>Raises <see cref="InsertImageRequested"/>.</summary>
    /// <param name="e">The request context.</param>
    protected virtual void OnInsertImageRequested(InsertImageRequestEventArgs e)
        => InsertImageRequested?.Invoke(this, e);

    /// <summary>Raises <see cref="ImageInsertFailed"/>.</summary>
    /// <param name="e">The controlled failure information.</param>
    protected virtual void OnImageInsertFailed(MarkdownImageInsertFailedEventArgs e)
        => ImageInsertFailed?.Invoke(this, e);

    private bool CanStartInsertionRequest()
        => !disposed && !ReadOnly && !DesignMode;

    private bool CanApplyInsertionRequest(
        MarkdownInsertionRequestSnapshot snapshot,
        MarkdownEditorInsertRequestEventArgs args)
        => IsInsertionSnapshotCurrent(snapshot) && args.Handled && !args.Cancel;

    private bool IsInsertionSnapshotCurrent(MarkdownInsertionRequestSnapshot snapshot)
        => !disposed && !ReadOnly && sourceVersion == snapshot.SourceVersion;

    private MarkdownInsertionRequestSnapshot CreateRequestSnapshot(bool image)
    {
        var selection = GetSurfaceSelection();
        var selectedText = selection.Length > 0
            ? Markdown.Substring(selection.Start, selection.Length)
            : string.Empty;
        var existing = inlineElementLocator.Find(Markdown, selection.Start, selection.Length, image);
        var target = existing is null
            ? selection
            : new MarkdownSelection(existing.Start, existing.Length);

        return new MarkdownInsertionRequestSnapshot(
            sourceVersion,
            selection,
            target,
            selectedText,
            existing);
    }

    private void ApplyRequestReplacement(MarkdownInsertionRequestSnapshot snapshot, string replacement)
    {
        ExecuteCommand(() => ReplaceRange(
            snapshot.Target.Start,
            snapshot.Target.Length,
            replacement,
            snapshot.Target.Start + replacement.Length,
            0));
        FocusEditorAfterRequest();
    }

    private async Task<MarkdownImageAssetResult> CopyImageAssetAsync(
        string source,
        MarkdownImageAssetOptions options,
        CancellationToken cancellationToken)
    {
        CancelActiveImageAssetOperation();
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activeImageAssetOperation = operation;
        try
        {
            return await MarkdownImageAssetProcessor.CopyAsync(source, options, operation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MarkdownImageAssetResult(MarkdownImageAssetStatus.Cancelled);
        }
        finally
        {
            if (ReferenceEquals(activeImageAssetOperation, operation))
            {
                activeImageAssetOperation = null;
                operation.Dispose();
            }
        }
    }

    private void CancelActiveImageAssetOperation()
    {
        var operation = activeImageAssetOperation;
        if (operation is null)
            return;

        activeImageAssetOperation = null;
        operation.Cancel();
        operation.Dispose();
    }

    private void FocusEditorAfterRequest()
    {
        if (!disposed && ViewMode != MarkdownEditorViewMode.Preview)
            editorSurface.Select();
    }

    private void ReportImageFailure(string source, string message, Exception? exception)
        => OnImageInsertFailed(new MarkdownImageInsertFailedEventArgs(source, message, exception));

    private void RestoreRequestSelection(MarkdownInsertionRequestSnapshot snapshot)
    {
        if (disposed || sourceVersion != snapshot.SourceVersion)
            return;

        editorSurface.Select(snapshot.Selection.Start, snapshot.Selection.Length);
        FocusEditorAfterRequest();
    }

    private static string CreateLinkMarkdown(string text, string url)
        => "["
            + MarkdownEditorMarkdownEscaping.EscapeLabel(text)
            + "](" + MarkdownEditorMarkdownEscaping.FormatDestination(url) + ")";

    private static string CreateImageMarkdown(string altText, string source, string? title)
    {
        var builder = "!["
            + MarkdownEditorMarkdownEscaping.EscapeLabel(altText)
            + "](" + MarkdownEditorMarkdownEscaping.FormatDestination(source);
        if (!string.IsNullOrEmpty(title))
            builder += " \"" + MarkdownEditorMarkdownEscaping.EscapeTitle(title) + "\"";
        return builder + ")";
    }

    private sealed record MarkdownInsertionRequestSnapshot(
        int SourceVersion,
        MarkdownSelection Selection,
        MarkdownSelection Target,
        string SelectedText,
        MarkdownEditorInlineElement? ExistingElement);
}

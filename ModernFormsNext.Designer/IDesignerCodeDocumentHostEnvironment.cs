namespace ModernFormsNext.Designer;

/// <summary>Optionally connects Designer event editing to a host's live C# text buffers.</summary>
/// <remarks>
/// Implement alongside <see cref="IDesignerHostEnvironment"/>. Calls start on the Designer UI
/// thread and must complete asynchronously without blocking either application's dispatcher.
/// Never evaluate project code. Reject unavailable, read-only, stale or oversized documents
/// instead of falling back to disk. The Designer currently edits the paired user <c>.cs</c> file.
/// Hosts without this capability use the bounded standalone file writer and report that code
/// navigation is unavailable. Honor cancellation before dispatching a queued request. Once an
/// editor accepts an insertion it can complete; the Designer retains that user method and guards
/// the model binding separately rather than attempting a destructive cross-file rollback.
/// </remarks>
/// <example>
/// <code>
/// // Start on the Designer UI thread; the host marshals its own editor work.
/// if (environment is IDesignerCodeDocumentHostEnvironment codeHost)
/// {
///     var source = await codeHost.ReadCodeDocumentAsync(codePath, cancellationToken);
///     if (source is not null)
///         await codeHost.NavigateToCodeAsync(codePath, source, 0, cancellationToken);
/// }
/// </code>
/// </example>
public interface IDesignerCodeDocumentHostEnvironment
{
    /// <summary>Reads the current text, including unsaved editor changes.</summary>
    /// <param name="path">Absolute path of the paired user C# document.</param>
    /// <param name="cancellationToken">Cancels an outstanding host request.</param>
    /// <returns>Current source text, or null when the document cannot safely be read.</returns>
    Task<string?> ReadCodeDocumentAsync(string path, CancellationToken cancellationToken);

    /// <summary>Applies one source insertion only if the live buffer still matches the snapshot.</summary>
    /// <param name="path">Absolute path of the paired user C# document.</param>
    /// <param name="expectedText">Exact text previously read from the host.</param>
    /// <param name="insertionOffset">Zero-based UTF-16 insertion offset in that snapshot.</param>
    /// <param name="insertedText">Method text to insert without replacing existing user text.</param>
    /// <param name="cancellationToken">Cancels a queued edit before it is applied.</param>
    /// <returns>True after the edit succeeds; false on conflicts, denied edits or unavailable buffers.</returns>
    /// <remarks>Use the host's query-edit and undo infrastructure. Leave saving to the user.</remarks>
    Task<bool> InsertCodeAsync(string path, string expectedText, int insertionOffset,
        string insertedText, CancellationToken cancellationToken);

    /// <summary>Shows the current code document and places its caret at a handler.</summary>
    /// <param name="path">Absolute path of the paired user C# document.</param>
    /// <param name="expectedText">Text used to calculate the caret location; reject stale text.</param>
    /// <param name="offset">Zero-based UTF-16 caret offset.</param>
    /// <param name="cancellationToken">Cancels navigation before the host performs it.</param>
    /// <returns>True when the host navigated to the requested current location.</returns>
    Task<bool> NavigateToCodeAsync(string path, string expectedText, int offset,
        CancellationToken cancellationToken);
}

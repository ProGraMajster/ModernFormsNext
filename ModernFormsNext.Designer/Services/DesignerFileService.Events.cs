using ModernFormsNext.Designing;
using ModernFormsNext.Designer.Recovery;
using System.Text;

namespace ModernFormsNext.Designer.Services;

internal sealed partial class DesignerFileService
{
    private readonly SemaphoreSlim eventAnalysisGate = new(1, 1);

    private async Task<T> AnalyzeEventSourceAsync<T>(Func<T> analyze, CancellationToken cancellationToken)
    {
        await eventAnalysisGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(analyze, cancellationToken).ConfigureAwait(false); }
        finally { eventAnalysisGate.Release(); }
    }

    internal async Task<IReadOnlyList<string>> ReadCompatibleEventHandlersAsync(DesignDocument document,
        string parameters, CancellationToken cancellationToken)
    {
        var path = GetFormCodePath(document) ?? throw new InvalidOperationException("Save the design document before choosing a handler.");
        var identity = new DesignDocument { ClassName = document.ClassName, Namespace = document.Namespace };
        var projectPath = environment?.CurrentProjectPath;
        var source = environment is IDesignerCodeDocumentHostEnvironment host
            ? await host.ReadCodeDocumentAsync(path, cancellationToken)
            : await AnalyzeEventSourceAsync(() => DesignerProjectUserControlDiscovery.ReadEventSourceText(path), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (source is null) throw new IOException("The host could not read the live C# buffer.");
        // Only immutable source and detached identity data reach the worker. No Control, session,
        // editor or host service is accessed there; the caller resumes on its UI context.
        return await AnalyzeEventSourceAsync(() => DesignerEventSource.CompatibleMethods(identity, path, source, parameters, projectPath, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<DesignerPreparedEventHandler> PrepareEventHandlerAsync(DesignDocument document,
        string name, string parameters, bool create, CancellationToken cancellationToken)
    {
        var path = GetFormCodePath(document);
        if (path is null)
            return new(string.Empty, string.Empty, DesignerEventSourcePlan.Failed("Save the design document before editing event source."));
        var identity = new DesignDocument { ClassName = document.ClassName, Namespace = document.Namespace };
        var projectPath = environment?.CurrentProjectPath;
        var text = environment is IDesignerCodeDocumentHostEnvironment host
            ? await host.ReadCodeDocumentAsync(path, cancellationToken).ConfigureAwait(false)
            : await AnalyzeEventSourceAsync(() => DesignerProjectUserControlDiscovery.ReadEventSourceText(path), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (text is null)
            return new(path, string.Empty, DesignerEventSourcePlan.Failed("The host could not read the current C# buffer; no disk fallback was used."));
        var plan = await AnalyzeEventSourceAsync(() => DesignerEventSource.Plan(identity, path, text, name, parameters,
            projectPath, create, cancellationToken: cancellationToken), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new(path, text, plan);
    }

    internal async Task<bool> ApplyEventHandlerAsync(DesignerPreparedEventHandler prepared, CancellationToken cancellationToken)
    {
        if (!prepared.Plan.Succeeded) return false;
        cancellationToken.ThrowIfCancellationRequested();
        if (prepared.Plan.Insertion.Length == 0) return true;
        if (environment is IDesignerCodeDocumentHostEnvironment host)
            return await host.InsertCodeAsync(prepared.Path, prepared.Source, prepared.Plan.Offset,
                prepared.Plan.Insertion, cancellationToken);
        return await AnalyzeEventSourceAsync(() => ApplyStandaloneEventHandler(prepared, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static bool ApplyStandaloneEventHandler(DesignerPreparedEventHandler prepared, CancellationToken cancellationToken)
    {
        var current = DesignerProjectUserControlDiscovery.ReadEventSourceText(prepared.Path);
        if (!string.Equals(current, prepared.Source, StringComparison.Ordinal)) return false;
        // The standalone writer preserves UTF-8 BOM state. Other encodings are rejected explicitly
        // rather than silently recoding user source; live editor hosts preserve their own encoding.
        using (var stream = new FileStream(prepared.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Span<byte> prefix = stackalloc byte[3];
            var count = stream.Read(prefix);
            if (count >= 2 && (prefix[0] == 0xff && prefix[1] == 0xfe || prefix[0] == 0xfe && prefix[1] == 0xff))
                throw new InvalidDataException("Use a live code editor host for non-UTF-8 event source.");
        }
        var bytes = DesignerProjectUserControlDiscovery.ReadEventSourceBytes(prepared.Path);
        var bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var decoded = new UTF8Encoding(false, true).GetString(bom ? bytes.AsSpan(3) : bytes);
        if (decoded != prepared.Source) return false;
        cancellationToken.ThrowIfCancellationRequested();
        DesignerAtomicFileWriter.WriteUtf8(prepared.Path, prepared.Plan.UpdatedText,
            new DesignerEventSourceCommitter(bytes), bom);
        return true;
    }

    internal Task<bool> NavigateToEventHandlerAsync(DesignerPreparedEventHandler prepared, int offset,
        CancellationToken cancellationToken)
        => environment is IDesignerCodeDocumentHostEnvironment host
            ? host.NavigateToCodeAsync(prepared.Path,
                prepared.Plan.Insertion.Length == 0 ? prepared.Source : prepared.Plan.UpdatedText, offset, cancellationToken)
            : Task.FromResult(false);
}

internal sealed record DesignerPreparedEventHandler(string Path, string Source, DesignerEventSourcePlan Plan);

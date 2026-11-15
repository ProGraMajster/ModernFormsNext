using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.ExecutablePreview;

internal interface IExecutablePreviewBackend
{
    IExecutablePreviewSnapshot Capture(string project, IReadOnlyList<string> files);
}

internal interface IExecutablePreviewSnapshot : IDisposable
{
    string Digest { get; }
    IReadOnlyList<string> Summary { get; }
    event Action? SourcesChanged;
    Task VerifyAsync(CancellationToken ct);
    Task<PreviewOutcome> RenderAsync(PreviewKey key, PreviewRequest request, CancellationToken ct);
}

// All mutable editor-facing state is owned by the UI thread. The worker can supply pixels
// and bounded diagnostics only. Document transactions, history and selection stay in Session.
internal sealed class ExecutablePreviewController : IDisposable
{
    private const int MaxEntries = 32;
    private const int MaxCacheBytes = 16 * 1024 * 1024;
    private readonly DesignerSession session;
    private readonly IExecutablePreviewBackend backend;
    private readonly IDesignerUiDispatcher dispatcher;
    private readonly Dictionary<DesignControlNode, Entry> entries = new(ReferenceEqualityComparer.Instance);
    private Approval? approval;
    private Task? activeTask;
    private Task cleanup = Task.CompletedTask;
    private CancellationTokenSource? activeRender;
    private long revision;
    private long nextRequest;
    private int cacheBytes;
    private bool disposed;

    internal ExecutablePreviewController(DesignerSession session, IExecutablePreviewBackend backend, IDesignerUiDispatcher? dispatcher = null)
    {
        this.session = session;
        this.backend = backend;
        this.dispatcher = dispatcher ?? DesignerUiDispatcher.Instance;
        session.DocumentChanged += OnDocumentChanged;
        session.DocumentTabsChanged += OnTabsChanged;
    }

    internal bool IsEnabled => approval is not null;
    internal event Action? Changed;

    internal Task<IExecutablePreviewSnapshot> CaptureAsync(string project, IReadOnlyList<string> files)
        => Task.Run(() => backend.Capture(project, files));

    // Must be called only after confirmation in DesignerSettingsDialog. A captured
    // snapshot contains exactly the bytes shown in the approval dialog.
    internal async Task EnableAsync(string project, IExecutablePreviewSnapshot candidate)
    {
        if (disposed || !string.Equals(project, session.CurrentProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            Retire(candidate);
            return;
        }

        Disable();
        var pending = new Approval(project, candidate);
        approval = pending;
        candidate.SourcesChanged += pending.OnSourcesChanged = () => dispatcher.Post(() =>
        {
            if (ReferenceEquals(approval, pending))
                Disable("Build artifacts changed. Approve the rebuilt artifacts to resume isolated preview.");
        });

        var verification = Task.Run(() => candidate.VerifyAsync(pending.Lifetime.Token));
        pending.Work = verification;
        try
        {
            await verification;
            if (!ReferenceEquals(approval, pending))
                return;
            pending.Verified = true;
            session.Log("Isolated preview enabled for the approved build.");
            Changed?.Invoke();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (ReferenceEquals(approval, pending))
                Disable("Isolated preview is unavailable. Windows could not verify the required worker policy or build.");
        }
    }

    internal void Retry()
    {
        if (approval?.Verified != true)
            return;
        ResetFrames();
    }

    internal void Disable(string? message = null)
    {
        Approval? previous = approval;
        approval = null;
        if (previous is not null)
        {
            previous.Snapshot.SourcesChanged -= previous.OnSourcesChanged;
            // The queued Dispose waits for the active worker to exit before touching its directories.
            Retire(previous.Snapshot, previous.Work, previous.Lifetime);
        }
        ResetFrames();
        if (message is not null)
            session.Log(message);
    }

    internal Task Cleanup => cleanup;

    internal void Discard(IExecutablePreviewSnapshot snapshot) => Retire(snapshot);

    private void Retire(IExecutablePreviewSnapshot snapshot, Task? work = null, CancellationTokenSource? lifetime = null)
    {
        lifetime?.Cancel();
        var removal = Task.Run(async () =>
        {
            try
            {
                if (work is not null)
                {
                    try { await work.ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { }
                }
                snapshot.Dispose();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Exception text can contain private paths. Cleanup diagnostics stay generic.
                DesignerDiagnosticLog.Write("Isolated preview staging cleanup did not finish.");
            }
            finally { lifetime?.Dispose(); }
        });
        cleanup = cleanup.IsCompleted ? removal : Task.WhenAll(cleanup, removal);
    }

    internal SKBitmap? GetRaster(DesignControlNode node, int width, int height, double scale)
    {
        if (approval is not { Verified: true } accepted ||
            width is < 1 or > PreviewWire.MaxEdge || height is < 1 or > PreviewWire.MaxEdge)
            return null;
        if (entries.TryGetValue(node, out var old) &&
            old.Width == width && old.Height == height && old.Scale == scale)
            return old.Bitmap;
        if (activeTask is not null || width * height * 4 > MaxCacheBytes - cacheBytes || entries.Count >= MaxEntries)
            return null;

        string type = session.GetCustomControlMetadata(node.TypeName)?.FullName ??
            DesignerProjectUserControlDiscovery.NormalizeTypeName(node.TypeName);
        if (!PreviewWire.IsIdentifier(type, qualified: true))
            return null;
        var request = new PreviewRequest(accepted.Snapshot.Digest, type, width, height, scale,
            node.Properties.Where(item => item.Value.Kind != DesignPropertyValueKind.Object &&
                item.Key is not ("Name" or "Bounds" or "Size" or "Location" or "Width" or "Height" or "Left" or "Top" or "Parent"))
                .Take(33).Select(item => new PreviewProperty(item.Key, JsonSerializer.SerializeToElement(item.Value.Value))).ToArray());

        Release(old);
        entries[node] = new(width, height, scale, null);
        var key = new PreviewKey(accepted.Generation, ++nextRequest, revision, entries.Count);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(accepted.Lifetime.Token);
        activeRender = cancellation;
        var task = Task.Run(async () =>
        {
            try
            {
                return await accepted.Snapshot.RenderAsync(key, request, cancellation.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return new PreviewOutcome(null, PreviewPhase.Startup,
                    error is InvalidDataException ? PreviewFault.Unsupported : PreviewFault.Unavailable);
            }
        });
        activeTask = task;
        accepted.Work = task;
        _ = task.ContinueWith(completed => dispatcher.Post(() =>
            Complete(accepted, key, request, node, cancellation, completed)), TaskScheduler.Default);
        return null;
    }

    private void Complete(Approval accepted, PreviewKey key, PreviewRequest request, DesignControlNode node,
        CancellationTokenSource cancellation, Task<PreviewOutcome> task)
    {
        activeTask = null;
        activeRender = null;
        bool cancelled = cancellation.IsCancellationRequested;
        cancellation.Dispose();
        if (disposed)
            return;
        if (task.IsCompletedSuccessfully && !cancelled && ReferenceEquals(approval, accepted) &&
            key.Session == accepted.Generation && key.Revision == revision)
        {
            var outcome = task.Result;
            if (outcome.Pixels is { } pixels && PreviewWire.IsRaster(pixels, request))
            {
                var bitmap = new SKBitmap(request.Width, request.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
                entries[node] = new(request.Width, request.Height, request.Scale, bitmap);
                cacheBytes += pixels.Length;
                session.Log($"Isolated preview rendered {SafeNode(node)}.");
            }
            else
            {
                string source = outcome.Source is { } name ? $" ({name})" : "";
                session.Log($"Isolated preview of {SafeNode(node)}: {outcome.Phase} / {outcome.Fault}{source}.");
                if (outcome.Fault == PreviewFault.Unavailable)
                    Disable("Isolated preview stopped. Reapprove the build to try again.");
            }
        }
        Changed?.Invoke();
    }

    private static string SafeNode(DesignControlNode node)
        => PreviewWire.IsIdentifier(node.Name) ? node.Name : "(unnamed control)";

    private void OnTabsChanged(object? sender, EventArgs e)
    {
        if (approval is not null && !string.Equals(approval.Project, session.CurrentProjectPath, StringComparison.OrdinalIgnoreCase))
            Disable();
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => ResetFrames();

    private void ResetFrames()
    {
        revision++;
        activeRender?.Cancel();
        foreach (var entry in entries.Values)
            Release(entry);
        entries.Clear();
        Changed?.Invoke();
    }

    private void Release(Entry? entry)
    {
        if (entry?.Bitmap is not { } bitmap)
            return;
        cacheBytes -= bitmap.ByteCount;
        bitmap.Dispose();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        session.DocumentChanged -= OnDocumentChanged;
        session.DocumentTabsChanged -= OnTabsChanged;
        Changed = null;
        Disable();
    }

    private sealed record Entry(int Width, int Height, double Scale, SKBitmap? Bitmap);

    private sealed class Approval(string project, IExecutablePreviewSnapshot snapshot)
    {
        internal string Project => project;
        internal IExecutablePreviewSnapshot Snapshot => snapshot;
        internal Guid Generation { get; } = Guid.NewGuid();
        internal CancellationTokenSource Lifetime { get; } = new();
        internal Task? Work { get; set; }
        internal Action? OnSourcesChanged { get; set; }
        internal bool Verified { get; set; }
    }
}

using ModernFormsNext.VisualStudioExtension;

namespace ModernFormsNext.VisualStudioDesignerHost;

// Bounded, synchronized requests. The existing VS state poll accepts a request, then handles
// its text buffer on the VS UI thread. No Designer UI thread waits synchronously for that poll.
internal sealed class DesignerCodeRequestBroker : IDisposable
{
    private readonly Queue<Pending> queued = new();
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    private bool disposed;
    private readonly object gate = new();

    internal async Task<(bool Success, string Text)> RequestAsync(string operation, string designPath,
        string sourcePath, string expected, int offset, string insertion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expected.Length > DesignerCodeRequest.MaximumTextCharacters || offset < 0 || offset > expected.Length
            || insertion.Length > DesignerCodeRequest.MaximumInsertionCharacters) return (false, string.Empty);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var request = new DesignerCodeRequest(Guid.NewGuid().ToString("N"), operation, designPath,
            sourcePath, expected, offset, insertion, DateTime.UtcNow.AddSeconds(30).Ticks);
        if (!request.IsAuthorized(designPath)) return (false, string.Empty);
        var item = new Pending(request, timeout.Token);
        lock (gate)
        {
            if (disposed || pending.Count >= 8 || queued.Count >= 8) return (false, string.Empty);
            pending.Add(request.Id, item);
            queued.Enqueue(item);
        }
        try { return await item.Completion.Task.WaitAsync(timeout.Token); }
        finally { lock (gate) pending.Remove(request.Id); }
    }

    internal string Poll(string currentDesignPath)
    {
        lock (gate)
        {
            while (queued.Count != 0)
            {
                var item = queued.Dequeue();
                if (pending.ContainsKey(item.Request.Id) && !item.CancellationToken.IsCancellationRequested
                    && item.Request.IsAuthorized(currentDesignPath))
                { item.Dispatched = true; return item.Request.Encode(); }
                item.Completion.TrySetResult((false, string.Empty));
            }
            return "CODE_NONE";
        }
    }

    internal bool Complete(string response, string currentDesignPath)
    {
        lock (gate)
        {
            if (response.Length > DesignerCodeRequest.MaximumFrameCharacters) return false;
            var parts = response.Split('\t');
            if (parts.Length != 4 || !pending.TryGetValue(parts[1], out var item) || !item.Dispatched
                || item.CancellationToken.IsCancellationRequested || !item.Request.IsAuthorized(currentDesignPath)
                || !DesignerCodeRequest.TryReadResult(response, item.Request.Id, out var success, out var text)) return false;
            return item.Completion.TrySetResult((success, text));
        }
    }

    internal void CancelAll()
    {
        lock (gate)
        {
            foreach (var item in pending.Values) item.Completion.TrySetResult((false, string.Empty));
            queued.Clear();
        }
    }

    public void Dispose() { lock (gate) { disposed = true; CancelAll(); } }

    private sealed class Pending(DesignerCodeRequest request, CancellationToken cancellationToken)
    {
        internal DesignerCodeRequest Request { get; } = request;
        internal CancellationToken CancellationToken { get; } = cancellationToken;
        internal bool Dispatched { get; set; }
        internal TaskCompletionSource<(bool Success, string Text)> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

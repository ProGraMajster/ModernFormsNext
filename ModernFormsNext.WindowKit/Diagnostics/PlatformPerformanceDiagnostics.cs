namespace ModernFormsNext.WindowKit.Diagnostics;

/// <summary>Provides optional allocation-free native frame ingress on the current UI thread.</summary>
internal static class PlatformPerformanceDiagnostics
{
    [ThreadStatic] private static PlatformPerformanceRegistration? current;
    private static long nextGeneration;

    /// <summary>Gets whether native callers should capture metadata and start a scope.</summary>
    internal static bool IsEnabled => current is { IsActive: true };

    /// <summary>Registers one recorder on this thread; registration is an explicit opt-in allocation.</summary>
    internal static PlatformPerformanceRegistration Register(IPlatformPerformanceSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (current is { IsActive: true })
            throw new InvalidOperationException("A performance recorder is already registered on this thread.");
        return current = new PlatformPerformanceRegistration(sink,
            Interlocked.Increment(ref nextGeneration), Environment.CurrentManagedThreadId);
    }

    /// <summary>Begins a native frame when recording is enabled; the source is never retained here.</summary>
    internal static PlatformPerformanceFrameScope BeginFrame(object source, in PlatformRenderInfo info)
        => current?.BeginFrame(source, in info) ?? default;

    internal static void Revoke(PlatformPerformanceRegistration registration)
    {
        if (ReferenceEquals(current, registration)) current = null;
    }
}

/// <summary>Owns one recorder registration and bounded scope guards, without storing frame data.</summary>
internal sealed class PlatformPerformanceRegistration : IDisposable
{
    internal const int MaximumScopeDepth = 64;
    private readonly ScopeState[] scopes = new ScopeState[MaximumScopeDepth];
    private IPlatformPerformanceSink? sink;
    private int depth;
    private long sequence;
    private bool inCallback;

    internal PlatformPerformanceRegistration(IPlatformPerformanceSink sink, long generation, int ownerThreadId)
    {
        this.sink = sink;
        Generation = generation;
        OwnerThreadId = ownerThreadId;
    }

    internal long Generation { get; }
    internal int OwnerThreadId { get; }
    internal bool IsActive => sink is not null;
    internal long RecorderFailureCount { get; private set; }
    internal long OmittedScopeCount { get; private set; }

    internal PlatformPerformanceFrameScope BeginFrame(object source, in PlatformRenderInfo info)
    {
        if (sink is not { } recorder || Environment.CurrentManagedThreadId != OwnerThreadId) return default;
        if (inCallback || depth == MaximumScopeDepth)
        {
            OmittedScopeCount++;
            return default;
        }

        long token;
        inCallback = true;
        try { token = recorder.BeginFrame(source, in info); }
        catch
        {
            // Recording failure must not escape WM_PAINT/ViewRoot or replace a paint exception.
            RecorderFailureCount++;
            return default;
        }
        finally { inCallback = false; }
        if (token == 0) return default;
        if (!ReferenceEquals(sink, recorder))
        {
            // A recorder can revoke itself while opening the token. The registration had
            // no slot to drain yet, so close that captured token without retargeting a new sink.
            try { recorder.EndFrame(token, completed: false); }
            catch { RecorderFailureCount++; }
            return default;
        }

        int index = depth++;
        long nonce = ++sequence;
        scopes[index] = new ScopeState { Nonce = nonce, Token = token };
        return new PlatformPerformanceFrameScope(this, index, nonce);
    }

    internal void Complete(int index, long nonce)
    {
        if (Owns(index, nonce) && !scopes[index].DisposeRequested) scopes[index].Completed = true;
    }

    internal void UpdateInfo(int index, long nonce, in PlatformRenderInfo info)
    {
        if (!Owns(index, nonce) || scopes[index].DisposeRequested || inCallback || sink is not { } recorder) return;
        inCallback = true;
        try { recorder.UpdateFrame(scopes[index].Token, in info); }
        catch { RecorderFailureCount++; }
        finally { inCallback = false; }
    }

    internal void End(int index, long nonce)
    {
        if (!Owns(index, nonce)) return;
        scopes[index].DisposeRequested = true;
        // An out-of-order copied parent scope cannot pop the active child. Once that child
        // unwinds, requested ancestors are drained in order, each exactly once.
        while (depth > 0 && scopes[depth - 1].DisposeRequested)
        {
            ScopeState scope = scopes[--depth];
            scopes[depth] = default;
            DeliverEnd(scope.Token, scope.Completed);
        }
    }

    private bool Owns(int index, long nonce)
        => sink is not null && Environment.CurrentManagedThreadId == OwnerThreadId &&
            index >= 0 && index < depth && scopes[index].Nonce == nonce;

    private void DeliverEnd(long token, bool completed)
    {
        if (sink is not { } recorder) return;
        bool wasInCallback = inCallback;
        inCallback = true;
        try { recorder.EndFrame(token, completed); }
        catch { RecorderFailureCount++; }
        finally { inCallback = wasInCallback; }
    }

    /// <summary>Revokes new ingress first, ends outstanding work as incomplete and releases the sink.</summary>
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != OwnerThreadId)
            throw new InvalidOperationException("Performance registration must be disposed on its owning thread.");
        if (sink is null) return;
        PlatformPerformanceDiagnostics.Revoke(this);
        try
        {
            while (depth > 0)
            {
                ScopeState scope = scopes[--depth];
                scopes[depth] = default;
                DeliverEnd(scope.Token, completed: false);
            }
        }
        finally { sink = null; }
    }

    private struct ScopeState
    {
        internal long Nonce;
        internal long Token;
        internal bool Completed;
        internal bool DisposeRequested;
    }
}

/// <summary>A concrete, copy-safe frame scope; Complete marks success and Dispose always unwinds.</summary>
internal readonly struct PlatformPerformanceFrameScope : IDisposable
{
    private readonly PlatformPerformanceRegistration? registration;
    private readonly int index;
    private readonly long nonce;

    internal PlatformPerformanceFrameScope(PlatformPerformanceRegistration registration, int index, long nonce)
    {
        this.registration = registration;
        this.index = index;
        this.nonce = nonce;
    }

    internal void Complete() => registration?.Complete(index, nonce);
    internal void UpdateInfo(in PlatformRenderInfo info) => registration?.UpdateInfo(index, nonce, in info);
    public void Dispose() => registration?.End(index, nonce);
}

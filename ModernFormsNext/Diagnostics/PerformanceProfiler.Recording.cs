using System.Drawing;
using ModernFormsNext.WindowKit.Diagnostics;
using SkiaSharp;

namespace ModernFormsNext.Diagnostics;

public sealed partial class PerformanceProfiler
{
    // Charge elapsed time at transitions to the currently active frame and each active category.
    // Nested scopes of the same category contribute once. Work crossing a frame boundary is split
    // at that boundary instead of being charged afterward to an unrelated window.
    private long Advance()
    {
        long now = Math.Max(lastTimestamp, timestamp());
        long delta = now - lastTimestamp;
        lastTimestamp = now;
        if (delta == 0) return now;
        Work target = frameDepth == 0 ? unframed : frameStack[frameDepth - 1].Work;
        for (int category = 0; category < ActivityKinds; category++) {
            if (activeCategories[category] == 0) continue;
            Add(ref target.Durations[category], delta);
            if (frameDepth == 0) Add(ref pendingUnframed.Durations[category], delta);
        }
        if (options.DetailedControls && !renderingOverlay)
            for (int index = 0; index < detailCount; index++) {
                DetailState detail = details[index]!;
                for (int category = 0; category < ActivityKinds; category++)
                    if (detail.Active[category] != 0) Add(ref detail.Work.Durations[category], delta);
            }
        return now;
    }

    internal long BeginActivity(PerformanceActivityKind category, Control? control)
    {
        if (disposed || (uint)category >= ActivityKinds) return 0;
        int index = -1;
        for (int i = 0; i < activities.Length; i++) if (activities[i].Token == 0) { index = i; break; }
        if (index < 0) { droppedScopes++; return 0; }
        Advance();
        long token = NextToken(ActivityCapacity, index);
        if (token == 0) return 0;
        int detail = GetDetail(control);
        if (control is null && category == PerformanceActivityKind.ShaderCreation) detail = ActiveControlDetail();
        activities[index] = new() { Token = token, Category = (int)category, Detail = detail };
        activeCategories[(int)category]++;
        if (detail >= 0) details[detail]!.Active[(int)category]++;
        return token;
    }

    internal PerformanceScope BeginInput(Control? control)
    {
        if (activeCategories[(int)PerformanceActivityKind.Input] == 0) Count(PerformanceCounterKind.InputEvents, 1, control);
        return new(this, BeginActivity(PerformanceActivityKind.Input, control));
    }

    internal void CompleteActivity(long token)
    {
        if (disposed || token == 0) return;
        VerifyAccess();
        int index = (int)((token - 1) % ActivityCapacity);
        if (activities[index].Token == token) activities[index].Completed = true;
    }

    internal void EndActivity(long token)
    {
        if (disposed || token == 0) return;
        VerifyAccess();
        int index = (int)((token - 1) % ActivityCapacity);
        var activity = activities[index];
        if (activity.Token != token) return;
        Advance();
        activities[index] = default;
        activeCategories[activity.Category]--;
        if (activity.Detail >= 0) details[activity.Detail]!.Active[activity.Category]--;
    }

    internal void Count(PerformanceCounterKind counter, long delta, Control? control)
    {
        if (disposed || renderingOverlay || (uint)counter >= CounterKinds || delta < 0) return;
        CountCore(counter, delta, GetDetail(control));
    }

    private void CountCore(PerformanceCounterKind counter, long delta, int detail)
    {
        Work target = frameDepth == 0 ? unframed : frameStack[frameDepth - 1].Work;
        Add(ref target.Counters[(int)counter], delta);
        if (frameDepth == 0) Add(ref pendingUnframed.Counters[(int)counter], delta);
        if (detail >= 0) Add(ref details[detail]!.Work.Counters[(int)counter], delta);
    }

    private int GetDetail(Control? control)
    {
        if (!options.DetailedControls || renderingOverlay || control is null) return -1;
        if (detailIdentities.TryGetValue(control, out var existing)) return existing.Index;
        if (detailCount == details.Length) { droppedDetails++; return -1; }
        int index = detailCount++;
        details[index] = new(control.GetType().FullName ?? control.GetType().Name);
        detailIdentities.Add(control, new(index));
        return index;
    }

    private SourceIdentity? GetSource(object source)
    {
        if (sources.TryGetValue(source, out var identity)) return identity;
        if (sourceCount == options.SourceCapacity) { droppedSources++; return null; }
        identity = new(++sourceCount);
        sources.Add(source, identity);
        return identity;
    }

    private int ActiveControlDetail()
    {
        // Resource factories do not take controls. Attribute their work to the newest
        // live control scope without retaining a control or allocating an ambient stack.
        long newest = 0;
        int detail = -1;
        if (options.DetailedControls)
            foreach (var activity in activities)
                if (activity.Detail >= 0 && activity.Token > newest) { newest = activity.Token; detail = activity.Detail; }
        return detail;
    }

    private long NextToken(int capacity, int index)
    {
        if (tokenSequence >= long.MaxValue / capacity - 1) { droppedScopes++; return 0; }
        return ++tokenSequence * capacity + index + 1;
    }

    long IPlatformPerformanceSink.BeginFrame(object source, in PlatformRenderInfo info)
        => BeginFrame(source, ConvertInfo(in info));
    void IPlatformPerformanceSink.EndFrame(long token, bool completed) => EndFrame(token, completed);
    void IPlatformPerformanceSink.UpdateFrame(long token, in PlatformRenderInfo info)
    {
        var frame = FindFrame(token);
        if (frame is not null) frame.Info = Merge(frame.Info, ConvertInfo(in info), preserveNativeBoundary: false);
    }

    private long BeginFrame(object source, in PerformanceRenderInfo info)
    {
        if (disposed) return 0;
        if (frameDepth == frameStack.Length) { droppedScopes++; return 0; }
        SourceIdentity? identity = GetSource(source);
        if (identity is null) return 0;
        long now = Advance();
        int index = frameDepth;
        long token = NextToken(FrameDepthCapacity, index);
        if (token == 0) return 0;
        FrameState frame = frameStack[frameDepth++];
        frame.Token = token; frame.SourceId = identity.Id; frame.Start = now; frame.Info = info;
        frame.EndRequested = frame.Completed = frame.RenderCompleted = false;
        frame.RegionCount = 0; frame.Work.Clear();
        frame.ThreadWork = frameDepth == 1 ? Copy(pendingUnframed) : default;
        if (frameDepth == 1) pendingUnframed.Clear();
        frame.Interval = identity.LastStart is { } previous && identity.Generation == info.HostGeneration
            ? ToTime(now - previous) : null;
        identity.LastStart = now; identity.Generation = info.HostGeneration;
        frame.Allocated = options.TrackAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        if (options.TrackGarbageCollections) {
            frame.Gen0 = GC.CollectionCount(0); frame.Gen1 = GC.CollectionCount(1); frame.Gen2 = GC.CollectionCount(2);
        }
        return token;
    }

    private FrameState? FindFrame(long token)
    {
        if (token == 0 || disposed) return null;
        int index = (int)((token - 1) % FrameDepthCapacity);
        return index < frameDepth && frameStack[index].Token == token ? frameStack[index] : null;
    }

    private void EndFrame(long token, bool completed)
    {
        FrameState? requested = FindFrame(token);
        if (requested is null || requested.EndRequested) return;
        requested.EndRequested = true; requested.Completed = completed;
        while (frameDepth > 0 && frameStack[frameDepth - 1].EndRequested) {
            long now = Advance();
            FrameState frame = frameStack[--frameDepth];
            TimeSpan duration = ToTime(now - frame.Start);
            var metric = new PerformanceFrameMetrics {
                Sequence = ++totalFrames, SourceId = frame.SourceId, RenderInfo = frame.Info,
                Started = ToTime(frame.Start - started), Duration = duration, Interval = frame.Interval,
                Work = Copy(frame.Work), ThreadWorkSincePreviousFrame = frame.ThreadWork,
                Completed = frame.Completed, IsSlow = duration > options.SlowFrameThreshold,
                AllocatedBytes = options.TrackAllocations ? Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - frame.Allocated) : null,
                Gen0Collections = options.TrackGarbageCollections ? Math.Max(0, GC.CollectionCount(0) - frame.Gen0) : null,
                Gen1Collections = options.TrackGarbageCollections ? Math.Max(0, GC.CollectionCount(1) - frame.Gen1) : null,
                Gen2Collections = options.TrackGarbageCollections ? Math.Max(0, GC.CollectionCount(2) - frame.Gen2) : null
            };
            frames[frameNext] = metric; frameNext = (frameNext + 1) % frames.Length; frameCount = Math.Min(frameCount + 1, frames.Length);
            if (metric.IsSlow) {
                slowFrames[slowNext] = metric; slowNext = (slowNext + 1) % slowFrames.Length; slowCount = Math.Min(slowCount + 1, slowFrames.Length);
            }
            lastRegionCount = frame.RegionCount;
            frame.Regions.AsSpan(0, frame.RegionCount).CopyTo(lastRegions);
            frame.Token = 0;
        }
    }

    internal PerformanceRenderScope BeginRender(Control root, in PerformanceRenderInfo info)
    {
        if (disposed) return default;
        long ownedFrame = frameDepth == 0 ? BeginFrame(root, info) : 0;
        if (frameDepth > 0) {
            FrameState frame = frameStack[frameDepth - 1];
            frame.Info = Merge(frame.Info, info, preserveNativeBoundary: true);
            if (!rootIdentities.TryGetValue(root, out var rootIdentity)) {
                if (rootCount < roots.Length) {
                    rootIdentity = new(rootCount);
                    roots[rootCount++] = new(root);
                    rootIdentities.Add(root, rootIdentity);
                }
            }
            if (rootIdentity is not null) rootIdentity.SourceId = frame.SourceId;
        }
        return new(this, ownedFrame, BeginActivity(PerformanceActivityKind.Render, root));
    }

    internal void CompleteRender(long frame, long activity)
    {
        EndActivity(activity);
        if (FindFrame(frame) is { } state) state.RenderCompleted = true;
    }

    internal void EndRender(long frame, long activity)
    {
        EndActivity(activity);
        if (FindFrame(frame) is { } state) EndFrame(frame, state.RenderCompleted);
    }

    internal PerformanceResourceToken CaptureResource(PerformanceCounterKind created, PerformanceCounterKind released, Control? control)
    {
        if (disposed || renderingOverlay) return default;
        if (resources.Count == 4096 || resourceSequence == long.MaxValue) { droppedScopes++; return default; }
        int detail = GetDetail(control);
        if (control is null) detail = ActiveControlDetail();
        long token = ++resourceSequence;
        resources.Add(token, new(released, detail));
        CountCore(created, 1, detail);
        return new(weakSelf, token);
    }

    internal void ReleaseResource(long token)
    {
        // Explicit UI-thread disposal is observed; finalizers never invoke or post UI work.
        if (disposed || Environment.CurrentManagedThreadId != ownerThread) return;
        if (resources.Remove(token, out var lease)) CountCore(lease.Disposed, 1, lease.Detail);
    }

    internal void RecordRegion(Control control, RectangleF bounds, PerformanceRegionKind kind)
    {
        if (!ShouldRecordRegions || frameDepth == 0 ||
            !float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) ||
            bounds.Width < 0 || bounds.Height < 0) return;
        FrameState frame = frameStack[frameDepth - 1];
        if (frame.RegionCount == frame.Regions.Length) { droppedRegions++; return; }
        int detail = GetDetail(control);
        frame.Regions[frame.RegionCount++] = new() { SourceId = frame.SourceId, ControlId = detail + 1, Bounds = bounds, Kind = kind };
    }

    internal void RenderOverlay(SKCanvas canvas, Control root, int width, int height, double canvasScale)
    {
        if (disposed || renderingOverlay || (!overlayOptions.Visible && !ShouldRecordRegions)) return;
        long source = rootIdentities.TryGetValue(root, out var identity) ? identity.SourceId : 0;
        int count = 0;
        int start = (frameNext - frameCount + frames.Length) % frames.Length;
        for (int i = 0; i < frameCount; i++) {
            var frame = frames[(start + i) % frames.Length];
            if (frame.SourceId == source) overlayFrames[count++] = frame;
        }
        var data = new PerformanceOverlayData {
            LatestFrame = count == 0 ? null : overlayFrames[count - 1], Frames = overlayFrames.AsSpan(0, count),
            Regions = frameDepth > 0 ? frameStack[frameDepth - 1].Regions.AsSpan(0, frameStack[frameDepth - 1].RegionCount) : ReadOnlySpan<PerformanceRegion>.Empty,
            UnframedWork = Copy(unframed)
        };
        using var scope = new PerformanceScope(this, BeginActivity(PerformanceActivityKind.Overlay, null));
        renderingOverlay = true;
        try { PerformanceOverlayRenderer.Render(canvas, in data, overlayOptions, width, height, canvasScale); }
        catch { ownRecorderFailures++; }
        finally { renderingOverlay = false; }
    }

    private static void Add(ref long target, long delta) => target = delta > long.MaxValue - target ? long.MaxValue : target + delta;

    private static PerformanceRenderInfo ConvertInfo(in PlatformRenderInfo info) => new() {
        Backend = info.Backend switch { PlatformRenderBackend.Windows => "Windows", PlatformRenderBackend.Android => "Android",
            PlatformRenderBackend.Headless => "Headless", _ => "Unknown" }, Renderer = "Skia",
        Boundary = info.Boundary switch { PlatformRenderBoundary.SharedRender => PerformanceFrameBoundary.SharedRender,
            PlatformRenderBoundary.OffscreenCapture => PerformanceFrameBoundary.OffscreenCapture, _ => PerformanceFrameBoundary.NativePaint },
        Acceleration = info.RenderMode switch { PlatformRenderMode.Software => PerformanceAcceleration.Software,
            PlatformRenderMode.Hardware => PerformanceAcceleration.Hardware, _ => PerformanceAcceleration.Unknown },
        LogicalWidth = Dimension(info.LogicalWidth), LogicalHeight = Dimension(info.LogicalHeight),
        PixelWidth = info.PixelWidth, PixelHeight = info.PixelHeight, Scale = info.Scale ?? 0,
        PixelFormat = info.Format, RowBytes = info.RowBytes, BackingBytes = info.BackingBytes,
        HostGeneration = info.HostGeneration, BackingGeneration = info.BackingGeneration,
        PresentationCpuTime = info.PresentationCpuTime,
        IsOffscreen = info.Boundary == PlatformRenderBoundary.OffscreenCapture,
        Redraw = info.FullRedraw switch { true => PerformanceRedraw.FullSurface, false => PerformanceRedraw.PartialSurface, _ => PerformanceRedraw.Unknown }
    };

    private static int Dimension(double? value) => value is > 0 and <= int.MaxValue && double.IsFinite(value.Value) ? (int)value.Value : 0;

    private static PerformanceRenderInfo Merge(in PerformanceRenderInfo prior, in PerformanceRenderInfo fresh, bool preserveNativeBoundary)
        => prior with {
            Backend = fresh.Backend is not null and not "Unknown" ? fresh.Backend : prior.Backend,
            Renderer = fresh.Renderer ?? prior.Renderer,
            Boundary = preserveNativeBoundary && prior.Boundary != PerformanceFrameBoundary.SharedRender ? prior.Boundary : fresh.Boundary,
            Acceleration = fresh.Acceleration != PerformanceAcceleration.Unknown ? fresh.Acceleration : prior.Acceleration,
            Scale = fresh.Scale > 0 ? fresh.Scale : prior.Scale,
            LogicalWidth = fresh.LogicalWidth > 0 ? fresh.LogicalWidth : prior.LogicalWidth,
            LogicalHeight = fresh.LogicalHeight > 0 ? fresh.LogicalHeight : prior.LogicalHeight,
            PixelWidth = fresh.PixelWidth ?? prior.PixelWidth, PixelHeight = fresh.PixelHeight ?? prior.PixelHeight,
            PixelFormat = fresh.PixelFormat ?? prior.PixelFormat, RowBytes = fresh.RowBytes ?? prior.RowBytes,
            BackingBytes = fresh.BackingBytes ?? prior.BackingBytes, HostGeneration = fresh.HostGeneration ?? prior.HostGeneration,
            BackingGeneration = fresh.BackingGeneration ?? prior.BackingGeneration,
            PresentationCpuTime = fresh.PresentationCpuTime ?? prior.PresentationCpuTime,
            IsOffscreen = prior.IsOffscreen || fresh.IsOffscreen,
            Redraw = fresh.Redraw != PerformanceRedraw.Unknown ? fresh.Redraw : prior.Redraw
        };
}

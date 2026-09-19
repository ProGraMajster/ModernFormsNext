namespace ModernFormsNext.WindowKit.Diagnostics;

/// <summary>Connects native callback boundaries to the single optional Core recorder.</summary>
/// <remarks>
/// Calls are synchronous and UI-thread owned. The source identity is borrowed only for BeginFrame;
/// implementations map it weakly and must not retain it in a frame or exported snapshot. This
/// internal transport performs no aggregation, scheduling or application notification.
/// </remarks>
internal interface IPlatformPerformanceSink
{
    /// <summary>Begins a frame and returns its opaque nonzero recorder token, or zero when omitted.</summary>
    long BeginFrame(object source, in PlatformRenderInfo info);

    /// <summary>Updates facts discovered during actual backing creation, without starting another frame.</summary>
    void UpdateFrame(long token, in PlatformRenderInfo info);

    /// <summary>Ends a previously accepted frame; false identifies failure or revoked work.</summary>
    void EndFrame(long token, bool completed);
}

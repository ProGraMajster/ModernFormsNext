namespace ModernFormsNext.Animations;

/// <summary>
/// Carries one scheduler-computed frame to higher-level animation definitions.
/// </summary>
/// <remarks>
/// This internal value keeps raw and eased progress together so custom definitions do not need a
/// second clock. It is allocated on the stack and never retained by the scheduler.
/// </remarks>
internal readonly record struct AnimationFrame(
    float Progress,
    float EasedProgress,
    TimeSpan Elapsed,
    TimeSpan Duration,
    CancellationToken CancellationToken);

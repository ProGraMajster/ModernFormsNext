namespace ModernFormsNext.Animations;

/// <summary>
/// Defines a typed, seekable animation composed of normalized keyframes.
/// </summary>
/// <typeparam name="T">The interpolated value type.</typeparam>
/// <remarks>
/// Segment easing belongs to the ending keyframe. Keyframe positions must be added in
/// non-decreasing order and remain in the inclusive range 0..1. A definition contains at most
/// <see cref="MaximumKeyframeCount"/> keyframes.
/// </remarks>
public sealed class KeyframeAnimation<T> : AnimationDefinition
{
    /// <summary>Gets the maximum number of keyframes accepted by one definition.</summary>
    public const int MaximumKeyframeCount = 256;

    private readonly Control target;
    private readonly Action<T> update;
    private readonly IAnimationInterpolator<T> interpolator;
    private readonly List<Frame> keyframes = [];
    private KeyframeDuplicatePositionPolicy duplicatePositionPolicy;

    private KeyframeAnimation(
        Control target,
        Action<T> update,
        IAnimationInterpolator<T> interpolator)
    {
        this.target = target;
        this.update = update;
        this.interpolator = interpolator;
        Key = $"Keyframes:{typeof(T).FullName}";
    }

    /// <summary>
    /// Creates a keyframe animation using the built-in interpolator for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// No built-in interpolator exists for <typeparamref name="T"/>. Use the overload that accepts
    /// an interpolator.
    /// </exception>
    public static KeyframeAnimation<T> Create(Control target, Action<T> update)
        => Create(target, update, AnimationInterpolatorResolver.Get<T>());

    /// <summary>Creates a keyframe animation with an explicit typed interpolator.</summary>
    public static KeyframeAnimation<T> Create(
        Control target,
        Action<T> update,
        IAnimationInterpolator<T> interpolator)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(interpolator);
        return new KeyframeAnimation<T>(target, update, interpolator);
    }

    /// <summary>Gets or sets the explicit duplicate-position policy.</summary>
    public KeyframeDuplicatePositionPolicy DuplicatePositionPolicy
    {
        get => duplicatePositionPolicy;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            duplicatePositionPolicy = value;
        }
    }

    /// <summary>Gets the number of declared keyframes.</summary>
    public int Count => keyframes.Count;

    /// <summary>Adds a typed keyframe and optional easing for the segment ending at it.</summary>
    /// <param name="position">A finite normalized position from 0 through 1.</param>
    /// <param name="value">The value at the exact position.</param>
    /// <param name="easing">Optional easing for the segment ending at this frame.</param>
    /// <returns>This definition.</returns>
    public KeyframeAnimation<T> Keyframe(
        float position,
        T value,
        Func<float, float>? easing = null)
    {
        ValidateProgress(position, nameof(position));
        if (keyframes.Count >= MaximumKeyframeCount)
            throw new InvalidOperationException(
                $"A keyframe animation cannot contain more than {MaximumKeyframeCount} frames.");

        if (keyframes.Count > 0)
        {
            Frame previous = keyframes[^1];
            if (position < previous.Position)
                throw new ArgumentException(
                    "Keyframes must be declared in non-decreasing position order.",
                    nameof(position));
            if (position == previous.Position)
            {
                if (DuplicatePositionPolicy == KeyframeDuplicatePositionPolicy.Reject)
                    throw new ArgumentException(
                        "A keyframe already exists at this position.",
                        nameof(position));
                if (DuplicatePositionPolicy == KeyframeDuplicatePositionPolicy.ReplacePrevious)
                {
                    keyframes[^1] = new Frame(position, value, easing);
                    return this;
                }
            }
        }

        keyframes.Add(new Frame(position, value, easing));
        return this;
    }

    /// <summary>Samples a value deterministically without starting the scheduler.</summary>
    /// <param name="progress">A finite normalized timeline position from 0 through 1.</param>
    /// <returns>The exact endpoint or interpolated segment value.</returns>
    public T Sample(float progress)
    {
        ValidateProgress(progress, nameof(progress));
        EnsureFrames();
        return SampleCore(keyframes, progress);
    }

    /// <summary>Samples and immediately applies a value without scheduling an animation.</summary>
    /// <param name="progress">A finite normalized timeline position from 0 through 1.</param>
    /// <returns>The applied value.</returns>
    /// <remarks>The update callback runs synchronously on the calling thread.</remarks>
    public T Seek(float progress)
    {
        T value = Sample(progress);
        update(value);
        return value;
    }

    internal override Control? BoundTarget => target;

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
    }

    internal override Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        EnsureFrames();
        Frame[] snapshot = keyframes.ToArray();
        return ScheduleAsync(
            scope,
            target,
            ResolveKey(),
            reverse,
            (_, progress) => update(SampleCore(snapshot, progress)));
    }

    private T SampleCore(IReadOnlyList<Frame> frames, float progress)
    {
        if (progress <= frames[0].Position)
            return frames[0].Value;
        if (progress >= frames[^1].Position)
            return frames[^1].Value;

        int leftIndex = 0;
        while (leftIndex + 1 < frames.Count && frames[leftIndex + 1].Position <= progress)
            leftIndex++;

        Frame left = frames[leftIndex];
        if (left.Position == progress || leftIndex == frames.Count - 1)
            return left.Value;

        Frame right = frames[leftIndex + 1];
        float segmentLength = right.Position - left.Position;
        if (segmentLength <= 0f)
            return right.Value;

        float segmentProgress = (progress - left.Position) / segmentLength;
        float easedProgress = right.Easing is null
            ? segmentProgress
            : right.Easing(segmentProgress);
        if (!float.IsFinite(easedProgress))
            throw new InvalidOperationException("A keyframe easing function returned NaN or infinity.");
        return interpolator.Interpolate(left.Value, right.Value, easedProgress);
    }

    private void EnsureFrames()
    {
        if (keyframes.Count == 0)
            throw new InvalidOperationException("A keyframe animation requires at least one keyframe.");
    }

    private static void ValidateProgress(float progress, string parameterName)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                progress,
                "Keyframe progress must be finite and in the inclusive range 0 through 1.");
    }

    private readonly record struct Frame(
        float Position,
        T Value,
        Func<float, float>? Easing);
}

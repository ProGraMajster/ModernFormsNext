using System.ComponentModel;

namespace ModernFormsNext.Animations;

/// <summary>Applies a visual-only scale multiplier while pointer or keyboard activation is held.</summary>
public sealed class PressScaleEffect : InteractionEffect
{
    private readonly HashSet<int> pointers = [];
    private float pressedScale = 0.97f;
    private TimeSpan pressDuration = TimeSpan.FromMilliseconds(80);
    private TimeSpan releaseDuration = TimeSpan.FromMilliseconds(120);
    private Func<float, float> easing = Easings.CubicOut;
    private bool keyboardPressed;
    private float currentScale = 1f;

    /// <summary>Gets or sets the uniform held scale multiplier.</summary>
    public float PressedScale
    {
        get => pressedScale;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Pressed scale must be finite and positive.");
            pressedScale = value;
        }
    }

    /// <summary>Gets or sets the down-transition duration.</summary>
    public TimeSpan PressDuration
    {
        get => pressDuration;
        set => pressDuration = ValidateDuration(value, nameof(PressDuration));
    }

    /// <summary>Gets or sets the release-transition duration.</summary>
    public TimeSpan ReleaseDuration
    {
        get => releaseDuration;
        set => releaseDuration = ValidateDuration(value, nameof(ReleaseDuration));
    }

    /// <summary>Gets or sets the easing used in both directions.</summary>
    [Browsable(false)]
    public Func<float, float> Easing
    {
        get => easing;
        set => easing = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc/>
    protected override void OnPointerDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        pointers.Add(e.PointerId);
        AnimateTo(PressedScale, PressDuration);
    }

    /// <inheritdoc/>
    protected override void OnPointerUp(MouseEventArgs e)
    {
        pointers.Remove(e.PointerId);
        if (pointers.Count == 0 && !keyboardPressed)
            AnimateTo(1f, ReleaseDuration);
    }

    /// <inheritdoc/>
    protected override void OnPointerCanceled(int? pointerId)
    {
        if (pointerId is { } id)
            pointers.Remove(id);
        else
        {
            pointers.Clear();
            keyboardPressed = false;
        }
        if (pointers.Count > 0 || keyboardPressed)
            return;
        if (Target?.Enabled == true)
            AnimateTo(1f, ReleaseDuration);
        else
            CancelCore();
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.KeyCode.In(Keys.Space, Keys.Enter) || keyboardPressed)
            return;
        keyboardPressed = true;
        AnimateTo(PressedScale, PressDuration);
    }

    /// <inheritdoc/>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!e.KeyCode.In(Keys.Space, Keys.Enter))
            return;
        keyboardPressed = false;
        if (pointers.Count == 0)
            AnimateTo(1f, ReleaseDuration);
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        pointers.Clear();
        keyboardPressed = false;
        currentScale = 1f;
        Target?.RemoveInteractionScale(this);
    }

    /// <inheritdoc/>
    protected override void CancelCore()
    {
        pointers.Clear();
        keyboardPressed = false;
        base.CancelCore();
        currentScale = 1f;
        Target?.SetInteractionScale(this, 1f);
    }

    private void AnimateTo(float targetScale, TimeSpan duration)
    {
        if (Target is not { Enabled: true } target)
            return;
        if (target.Site?.DesignMode == true)
        {
            currentScale = targetScale;
            target.SetInteractionScale(this, currentScale);
            return;
        }

        Scheduler.Animate(
            this,
            "PressScale",
            currentScale,
            targetScale,
            AnimationInterpolators.Float,
            value =>
            {
                currentScale = value;
                target.SetInteractionScale(this, value);
            },
            new AnimationOptions
            {
                Duration = duration,
                Easing = Easing,
                ReplacementMode = AnimationReplacementMode.Replace
            });
    }

    private static TimeSpan ValidateDuration(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, value, "Effect duration cannot be negative.");
        return value;
    }
}

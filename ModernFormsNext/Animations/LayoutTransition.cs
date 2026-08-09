using System.ComponentModel;

namespace ModernFormsNext.Animations;

/// <summary>
/// Configures an opt-in transition between successive logical layout bounds of a control.
/// </summary>
/// <remarks>
/// <para>
/// The control's public bounds remain the logical target throughout a transition. Rendering and
/// hit testing use an internal presentation rectangle that approaches that target on the shared
/// <see cref="AnimationScheduler"/>. Assign an instance to <see cref="Control.LayoutTransition"/>
/// to enable animated layout for that control.
/// </para>
/// <para>
/// This is code-first UI configuration and should be changed on the UI thread. A future designer
/// editor can expose known easing functions without changing the transition or control contracts.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// panel.LayoutTransition = new LayoutTransition
/// {
///     Duration = TimeSpan.FromMilliseconds(250),
///     Easing = Easings.EaseOut
/// };
/// </code>
/// </example>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class LayoutTransition
{
    private bool enabled = true;
    private TimeSpan duration = TimeSpan.FromMilliseconds(250);
    private Func<float, float> easing = Easings.EaseOut;

    /// <summary>
    /// Gets or sets whether logical bounds changes should animate their presentation geometry.
    /// </summary>
    /// <remarks>
    /// Disabling an active transition immediately synchronizes presentation bounds with logical
    /// bounds and unregisters the control from the shared scheduler.
    /// </remarks>
    [DefaultValue(true)]
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
                return;

            enabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the unscaled duration of each layout transition.
    /// </summary>
    /// <remarks>
    /// A duration less than or equal to zero applies the logical target immediately and does not
    /// start the scheduler tick source. Positive values are still subject to the shared
    /// <see cref="AnimationPolicy.DurationScale"/> and reduced-motion policy.
    /// </remarks>
    [DefaultValue(typeof(TimeSpan), "00:00:00.250")]
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (duration == value)
                return;

            duration = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the easing function applied to intermediate layout progress.
    /// </summary>
    /// <remarks>
    /// The function follows the same finite-result contract as <see cref="AnimationOptions.Easing"/>.
    /// Built-in functions from <see cref="Easings"/> are recommended. Delegate-valued easing is
    /// hidden from ordinary property-grid serialization until a known-easing editor is added.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The assigned function is null.</exception>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<float, float> Easing
    {
        get => easing;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (easing == value)
                return;

            easing = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal event EventHandler? Changed;

    /// <inheritdoc/>
    public override string ToString()
        => Enabled ? $"{Duration.TotalMilliseconds:0.##} ms" : "Disabled";
}

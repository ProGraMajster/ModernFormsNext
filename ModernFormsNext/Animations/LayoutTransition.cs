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
/// This configuration should be changed on the UI thread. The Designer stores a closed subset
/// of known easing identifiers and scalar options; custom delegates remain code-first.
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
    private bool applyToDescendants;
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
            RaiseChanged();
        }
    }

    /// <summary>Gets or sets whether this configuration is the default for descendant controls.</summary>
    /// <remarks>
    /// False preserves per-control configuration. When true, descendants without a local
    /// <see cref="Control.LayoutTransition"/> borrow the nearest opted-in ancestor's configuration.
    /// A local configuration always wins, including a disabled or zero-duration one. Set this
    /// flag on a disabled configuration to opt an entire branch out of an outer policy.
    /// Each descendant owns its own scheduler entry; the ancestor owns no child animation handles.
    /// Changes retarget eligible active transitions from their current presentation, without
    /// starting transitions for already settled bounds or running layout. Parent resize commits
    /// still suppress redundant descendant easing. Zero-area rectangles always snap.
    /// Configure on the UI thread. The value may be shared and is not disposed by a control.
    /// </remarks>
    /// <example><code>
    /// container.LayoutTransition = new LayoutTransition { ApplyToDescendants = true };
    /// child.LayoutTransition = new LayoutTransition { Enabled = false }; // Local opt-out.
    /// </code></example>
    [DefaultValue(false)]
    public bool ApplyToDescendants
    {
        get => applyToDescendants;
        set
        {
            if (applyToDescendants == value) return;
            applyToDescendants = value;
            RaiseChanged();
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
            RaiseChanged();
        }
    }

    /// <summary>
    /// Gets or sets the easing function applied to intermediate layout progress.
    /// </summary>
    /// <remarks>
    /// The function follows the same finite-result contract as <see cref="AnimationOptions.Easing"/>.
    /// Built-in functions from <see cref="Easings"/> are recommended. Delegate-valued easing is
    /// hidden from ordinary property-grid serialization. The Designer's transition editor stores
    /// only stable built-in easing identifiers, without serializing or executing application delegates.
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
            RaiseChanged();
        }
    }

    internal event EventHandler? Changed;

    private void RaiseChanged()
    {
        if (Changed is not { } handlers) return;
        List<Exception>? failures = null;
        // Shared configurations may have owners in different branches. An application
        // invalidation callback must not prevent later owners from retiring stale policies.
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception failure) { (failures ??= []).Add(failure); }
        }
        if (failures is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 })
            throw new AggregateException("Layout transition configuration callbacks failed.", failures);
    }

    /// <inheritdoc/>
    public override string ToString()
        => Enabled ? $"{Duration.TotalMilliseconds:0.##} ms" : "Disabled";
}

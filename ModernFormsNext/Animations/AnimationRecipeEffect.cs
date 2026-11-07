using ModernFormsNext.AnimationRecipes;

namespace ModernFormsNext.Animations;

/// <summary>Attaches a bounded sequence of presentation animations to a control's click.</summary>
/// <remarks>
/// The recipe contains only framework property and easing identifiers, finite values and durations.
/// Attach and remove the effect on the control's UI thread. A new click cancels the previous run and
/// animates from the current presentation values. The shared scheduler honors reduced motion.
/// Removing or disabling the effect cancels its run and leaves the current values in place.
/// </remarks>
/// <example>
/// <code>
/// button.InteractionEffects.Add(new AnimationRecipeEffect(
///     "Click\nScale 0.95 90 EaseOut\nScale 1 160 BlendEaseInOut 0.6"));
/// </code>
/// </example>
public sealed class AnimationRecipeEffect : InteractionEffect
{
    private readonly RecipeStep[] steps;
    private AnimationRun? run;

    /// <summary>Creates an effect from an invariant animation recipe.</summary>
    /// <param name="recipe">Click followed by one to eight property, value, duration and easing rows.</param>
    /// <exception cref="ArgumentException">The recipe is unsupported, malformed or exceeds its budget.</exception>
    public AnimationRecipeEffect(string recipe)
    {
        if (!AnimationRecipeSyntax.TryRead(recipe, out steps, out string? error))
            throw new ArgumentException(error, nameof(recipe));
    }

    /// <inheritdoc/>
    protected override void OnAttached() => Target!.Click += HandleClick;

    /// <inheritdoc/>
    protected override void OnDetached() => Target!.Click -= HandleClick;

    /// <inheritdoc/>
    protected override void CancelCore()
    {
        run?.Cancel();
        run = null;
        base.CancelCore();
    }

    private void HandleClick(object? sender, EventArgs args)
    {
        if (!Enabled || Target is not { Enabled: true } target || target.Site?.DesignMode == true)
            return;
        run?.Cancel();
        run = CreateDefinition(target).Start(target, Scheduler);
    }

    // The Designer calls the same definition builder on its own framework control. It never
    // attaches the click handler or accepts a project control, easing or animation instance.
    internal AnimationDefinition CreateDefinition(Control target)
    {
        var definitions = new AnimationDefinition[steps.Length];
        for (int index = 0; index < steps.Length; index++)
        {
            RecipeStep step = steps[index];
            var options = new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(step.DurationMilliseconds),
                Easing = GetEasing(step)
            };
            definitions[index] = step.Property switch
            {
                RecipeProperty.Opacity => target.FadeTo(step.Value, options),
                RecipeProperty.Scale => target.ScaleTo(step.Value, options),
                RecipeProperty.Rotation => target.RotateTo(step.Value, options),
                _ => throw new InvalidOperationException("Unexpected recipe property.")
            };
        }
        return Animation.Sequence(definitions);
    }

    private static Func<float, float> GetEasing(RecipeStep step) => step.Easing switch
    {
        RecipeEasing.Linear => Easings.Linear,
        RecipeEasing.EaseIn => Easings.EaseIn,
        RecipeEasing.EaseOut => Easings.EaseOut,
        RecipeEasing.EaseInOut => Easings.EaseInOut,
        // The only parameter changes the strength of a fixed framework curve.
        RecipeEasing.BlendEaseInOut => progress =>
            progress + (Easings.EaseInOut(progress) - progress) * step.Strength,
        _ => throw new InvalidOperationException("Unexpected recipe easing.")
    };
}

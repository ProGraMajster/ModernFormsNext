using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Layout;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>Provides opt-in presentation geometry for scheduler-backed layout transitions.</summary>
public partial class Control
{
    private const string LayoutTransitionAnimationKey = "Control.LayoutTransition";
    private static readonly int s_layoutTransitionProperty = PropertyStore.CreateKey();
    private static readonly int s_layoutPresentationStateProperty = PropertyStore.CreateKey();
    private int animatedLayoutSizeCommitDepth;

    /// <summary>
    /// Gets or sets the optional transition used when this control's logical bounds change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see langword="null"/> value preserves the existing immediate layout behavior. When a
    /// transition is configured, <see cref="Bounds"/>, <see cref="Location"/>, <see cref="Size"/>,
    /// and their scalar counterparts continue to expose the logical target. Only rendering,
    /// clipping, accessibility geometry, and hit testing observe the interpolated presentation
    /// rectangle.
    /// </para>
    /// <para>
    /// Bounds produced by Dock, Anchor, FlowLayoutPanel, and TableLayoutPanel use the same path as
    /// bounds assigned by application code. A replacement target starts from the current
    /// presentation rectangle. Layout is not rerun on animation frames.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// card.LayoutTransition = new LayoutTransition
    /// {
    ///     Duration = TimeSpan.FromMilliseconds(220),
    ///     Easing = Easings.EaseOut
    /// };
    /// card.Bounds = new Rectangle(100, 50, 400, 200);
    /// </code>
    /// </example>
    [DefaultValue(null)]
    public LayoutTransition? LayoutTransition
    {
        get => (LayoutTransition?)Properties.GetObject(s_layoutTransitionProperty);
        set
        {
            LayoutTransition? previous = LayoutTransition;
            if (ReferenceEquals(previous, value))
                return;

            if (previous is not null)
                previous.Changed -= OnLayoutTransitionConfigurationChanged;

            Properties.SetObject(s_layoutTransitionProperty, value);

            if (value is not null)
                value.Changed += OnLayoutTransitionConfigurationChanged;

            OnLayoutTransitionConfigurationChanged(value, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the current unscaled presentation bounds used by rendering and hit testing.
    /// </summary>
    internal RectangleF PresentationBounds
    {
        get
        {
            if (Properties.GetObject(s_layoutPresentationStateProperty) is LayoutPresentationState state)
                return state.Current;

            Rectangle logical = Bounds;
            return new RectangleF(logical.X, logical.Y, logical.Width, logical.Height);
        }
    }

    internal bool HasActiveLayoutTransition
        => Properties.GetObject(s_layoutPresentationStateProperty) is LayoutPresentationState { IsAnimating: true };

    internal bool HasDistinctPresentationBounds
    {
        get
        {
            if (Properties.GetObject(s_layoutPresentationStateProperty) is not LayoutPresentationState state)
                return false;

            Rectangle logical = Bounds;
            return state.Current.X != logical.X ||
                state.Current.Y != logical.Y ||
                state.Current.Width != logical.Width ||
                state.Current.Height != logical.Height;
        }
    }

    internal bool HasDistinctPresentationSize
    {
        get
        {
            if (Properties.GetObject(s_layoutPresentationStateProperty) is not LayoutPresentationState state)
                return false;

            return state.Current.Width != Width || state.Current.Height != Height;
        }
    }

    internal void BeginAnimatedLayoutSizeCommit()
        => animatedLayoutSizeCommitDepth++;

    internal void EndAnimatedLayoutSizeCommit()
    {
        if (animatedLayoutSizeCommitDepth <= 0)
            throw new InvalidOperationException("An animated layout size commit was not active.");

        animatedLayoutSizeCommitDepth--;
    }

    internal RectangleF ScaledPresentationBounds
    {
        get
        {
            RectangleF bounds = PresentationBounds;
            SizeF factor = ScaleFactor;
            return new RectangleF(
                bounds.X * factor.Width,
                bounds.Y * factor.Height,
                bounds.Width * factor.Width,
                bounds.Height * factor.Height);
        }
    }

    internal bool PresentationContains(Point parentPoint)
    {
        RectangleF bounds = ScaledPresentationBounds;
        return bounds.Width > 0f && bounds.Height > 0f &&
            parentPoint.X >= bounds.Left && parentPoint.X < bounds.Right &&
            parentPoint.Y >= bounds.Top && parentPoint.Y < bounds.Bottom;
    }

    internal Point ParentPresentationPointToClient(Point parentPoint)
    {
        RectangleF presentation = ScaledPresentationBounds;
        float localX = parentPoint.X - presentation.X;
        float localY = parentPoint.Y - presentation.Y;
        int targetWidth = ScaledWidth;
        int targetHeight = ScaledHeight;

        int x = presentation.Width > 0f && targetWidth > 0
            ? (int)MathF.Floor(localX * targetWidth / presentation.Width)
            : 0;
        int y = presentation.Height > 0f && targetHeight > 0
            ? (int)MathF.Floor(localY * targetHeight / presentation.Height)
            : 0;
        return new Point(x, y);
    }

    internal Point ClientPointToParentPresentation(Point clientPoint)
    {
        RectangleF presentation = ScaledPresentationBounds;
        int targetWidth = ScaledWidth;
        int targetHeight = ScaledHeight;
        float x = targetWidth > 0
            ? presentation.X + (clientPoint.X * presentation.Width / targetWidth)
            : presentation.X;
        float y = targetHeight > 0
            ? presentation.Y + (clientPoint.Y * presentation.Height / targetHeight)
            : presentation.Y;
        return new Point(
            (int)MathF.Round(x, MidpointRounding.AwayFromZero),
            (int)MathF.Round(y, MidpointRounding.AwayFromZero));
    }

    internal void DrawBackBuffer(SKCanvas canvas, SKBitmap buffer, float parentOffsetX = 0f, float parentOffsetY = 0f)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(buffer);

        bool hasRenderTransform =
            EffectiveOpacity < 0.999f ||
            Math.Abs(EffectiveRotation) > 0.0001f ||
            Math.Abs(EffectiveScaleX - 1f) > 0.0001f ||
            Math.Abs(EffectiveScaleY - 1f) > 0.0001f ||
            Math.Abs(EffectiveTranslationX) > 0.0001f ||
            Math.Abs(EffectiveTranslationY) > 0.0001f;

        bool hasPresentationTransform = HasDistinctPresentationBounds;
        if (!hasRenderTransform && !hasPresentationTransform)
        {
            canvas.DrawBitmap(buffer, parentOffsetX + ScaledLeft, parentOffsetY + ScaledTop);
            return;
        }

        RectangleF presentation = ScaledPresentationBounds;
        float drawX = parentOffsetX + presentation.X + (EffectiveTranslationX * ScaleFactor.Width);
        float drawY = parentOffsetY + presentation.Y + (EffectiveTranslationY * ScaleFactor.Height);
        float drawWidth = presentation.Width;
        float drawHeight = presentation.Height;

        if (!hasRenderTransform)
        {
            canvas.DrawBitmap(buffer, new SKRect(drawX, drawY, drawX + drawWidth, drawY + drawHeight));
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, (byte)(255f * EffectiveOpacity))
        };

        canvas.Save();
        canvas.Translate(drawX, drawY);
        canvas.Translate(drawWidth / 2f, drawHeight / 2f);
        canvas.RotateDegrees(EffectiveRotation);
        canvas.Scale(EffectiveScaleX, EffectiveScaleY);
        canvas.Translate(-drawWidth / 2f, -drawHeight / 2f);
        if (hasPresentationTransform)
            canvas.DrawBitmap(buffer, new SKRect(0f, 0f, drawWidth, drawHeight), paint);
        else
            canvas.DrawBitmap(buffer, 0f, 0f, paint);
        canvas.Restore();
    }

    internal void UpdateLayoutPresentationBounds(Rectangle oldLogicalBounds, Rectangle newLogicalBounds)
    {
        if (oldLogicalBounds == newLogicalBounds)
            return;

        LayoutPresentationState? state =
            Properties.GetObject(s_layoutPresentationStateProperty) as LayoutPresentationState;
        RectangleF from = state?.Current ?? new RectangleF(
            oldLogicalBounds.X,
            oldLogicalBounds.Y,
            oldLogicalBounds.Width,
            oldLogicalBounds.Height);

        LayoutTransition? transition = LayoutTransition;
        if (!CanAnimateLayout(transition, from, newLogicalBounds))
        {
            if (state is not null)
                ResetLayoutPresentationBounds(cancelScheduledAnimation: true);
            return;
        }

        BeginLayoutPresentationTransition(from, newLogicalBounds, transition!);
    }

    internal void ResetLayoutPresentationAfterOwnerCancellation()
    {
        if (Properties.GetObject(s_layoutPresentationStateProperty) is not LayoutPresentationState state)
            return;

        state.Generation++;
        RectangleF previous = state.Current;
        Rectangle logical = Bounds;
        Properties.SetObject(s_layoutPresentationStateProperty, null);
        InvalidatePresentationChange(previous, new RectangleF(logical.X, logical.Y, logical.Width, logical.Height));
    }

    internal void DisposeLayoutTransitionConfiguration()
    {
        if (LayoutTransition is { } transition)
            transition.Changed -= OnLayoutTransitionConfigurationChanged;
        Properties.SetObject(s_layoutTransitionProperty, null);
        Properties.SetObject(s_layoutPresentationStateProperty, null);
    }

    private bool CanAnimateLayout(LayoutTransition? transition, RectangleF from, Rectangle target)
    {
        if (transition is null || !transition.Enabled || transition.Duration <= TimeSpan.Zero)
            return false;
        if (!Created || Parent is null || !Visible || Site?.DesignMode == true)
            return false;
        if (from.Width <= 0f || from.Height <= 0f || target.Width <= 0 || target.Height <= 0)
            return false;
        if (HasAncestorCommittingAnimatedLayoutSize())
            return false;

        AnimationScheduler scheduler = AnimationSchedulerOverride ?? AnimationScheduler.Default;
        return !scheduler.Policy.ShouldCompleteImmediately;
    }

    private bool HasAncestorCommittingAnimatedLayoutSize()
    {
        for (Control? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.animatedLayoutSizeCommitDepth > 0)
                return true;
        }

        return false;
    }

    private void BeginLayoutPresentationTransition(
        RectangleF from,
        Rectangle target,
        LayoutTransition transition)
    {
        var targetPresentation = new RectangleF(target.X, target.Y, target.Width, target.Height);
        if (from == targetPresentation)
        {
            ResetLayoutPresentationBounds(cancelScheduledAnimation: true);
            return;
        }

        LayoutPresentationState state =
            Properties.GetObject(s_layoutPresentationStateProperty) as LayoutPresentationState ?? new();
        state.Current = from;
        state.IsAnimating = true;
        int generation = ++state.Generation;
        Properties.SetObject(s_layoutPresentationStateProperty, state);

        AnimationScheduler scheduler = AnimationSchedulerOverride ?? AnimationScheduler.Default;
        var options = new AnimationOptions
        {
            Duration = transition.Duration,
            // Layout evaluates the configured easing inside its update callback so a fault can
            // release presentation state before the scheduler marks the entry faulted. The shared
            // scheduler still owns timing, replacement, terminal state, and diagnostics.
            Easing = Easings.Linear,
            ReplacementMode = AnimationReplacementMode.Replace
        };

        try
        {
            scheduler.StartFrames(
                this,
                LayoutTransitionAnimationKey,
                frame =>
                {
                    if (state.Generation != generation)
                        return;

                    try
                    {
                        float easedProgress = frame.Progress switch
                        {
                            <= 0f => 0f,
                            >= 1f => 1f,
                            _ => transition.Easing(frame.Progress)
                        };
                        if (!float.IsFinite(easedProgress))
                            throw new InvalidOperationException(
                                "The layout transition easing function returned NaN or infinity.");

                        RectangleF previous = state.Current;
                        RectangleF current = frame.Progress >= 1f
                            ? targetPresentation
                            : AnimationInterpolators.RectangleF.Interpolate(from, targetPresentation, easedProgress);
                        state.Current = current;
                        if (frame.Progress >= 1f)
                        {
                            state.IsAnimating = false;
                            Properties.SetObject(s_layoutPresentationStateProperty, null);
                        }

                        if (previous != current)
                            InvalidatePresentationChange(previous, current);
                    }
                    catch
                    {
                        // A scheduler fault removes the entry but cannot know about the control's
                        // presentation cache. Release it here so a bad custom easing or invalidation
                        // handler cannot strand the control at an intermediate visual rectangle.
                        if (state.Generation == generation)
                        {
                            RectangleF previous = state.Current;
                            state.Generation++;
                            state.IsAnimating = false;
                            Properties.SetObject(s_layoutPresentationStateProperty, null);
                            try
                            {
                                if (previous != targetPresentation)
                                    InvalidatePresentationChange(previous, targetPresentation);
                            }
                            catch
                            {
                                // Preserve the original callback failure reported by the scheduler.
                            }
                        }

                        throw;
                    }
                },
                options);
        }
        catch
        {
            state.Generation++;
            state.IsAnimating = false;
            Properties.SetObject(s_layoutPresentationStateProperty, null);
            InvalidatePresentationChange(from, targetPresentation);
            throw;
        }
    }

    private void OnLayoutTransitionConfigurationChanged(object? sender, EventArgs e)
    {
        LayoutPresentationState? state =
            Properties.GetObject(s_layoutPresentationStateProperty) as LayoutPresentationState;
        if (state is null)
            return;

        Rectangle logical = Bounds;
        LayoutTransition? transition = LayoutTransition;
        if (!CanAnimateLayout(transition, state.Current, logical))
        {
            ResetLayoutPresentationBounds(cancelScheduledAnimation: true);
            return;
        }

        BeginLayoutPresentationTransition(state.Current, logical, transition!);
    }

    private void ResetLayoutPresentationBounds(bool cancelScheduledAnimation)
    {
        if (Properties.GetObject(s_layoutPresentationStateProperty) is not LayoutPresentationState state)
            return;

        state.Generation++;
        if (cancelScheduledAnimation)
        {
            AnimationScheduler scheduler = AnimationSchedulerOverride ?? AnimationScheduler.Default;
            scheduler.Cancel(this, LayoutTransitionAnimationKey);
        }

        RectangleF previous = state.Current;
        Rectangle logical = Bounds;
        var current = new RectangleF(logical.X, logical.Y, logical.Width, logical.Height);
        Properties.SetObject(s_layoutPresentationStateProperty, null);
        InvalidatePresentationChange(previous, current);
    }

    private void InvalidatePresentationChange(RectangleF previous, RectangleF current)
    {
        Control compositionOwner = Parent ?? this;
        compositionOwner.SetState(States.IsDirty, true);

        RectangleF union = RectangleF.Union(previous, current);
        var invalidated = Rectangle.FromLTRB(
            (int)MathF.Floor(union.Left),
            (int)MathF.Floor(union.Top),
            (int)MathF.Ceiling(union.Right),
            (int)MathF.Ceiling(union.Bottom));

        if (FindWindow() is { } window)
            Application.RequestVisualInvalidation(window);
        compositionOwner.OnInvalidated(new EventArgs<Rectangle>(invalidated));
    }

    private sealed class LayoutPresentationState
    {
        public RectangleF Current;
        public int Generation;
        public bool IsAnimating;
    }
}

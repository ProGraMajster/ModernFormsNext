using ModernFormsNext.Animations;

namespace ModernFormsNext;

/// <summary>Identifies the outcome of a theme apply request.</summary>
public enum ThemeApplyStatus
{
    /// <summary>The theme was committed.</summary>
    Applied,
    /// <summary>The request was canceled before commit.</summary>
    Canceled,
    /// <summary>Validation, resolution, commit, or rollback failed.</summary>
    Failed
}

/// <summary>Identifies the observable state of a theme transition.</summary>
public enum ThemeTransitionStatus
{
    /// <summary>No animated transition was requested.</summary>
    None,
    /// <summary>The transition is active.</summary>
    Running,
    /// <summary>The transition reached its target values.</summary>
    Completed,
    /// <summary>The transition was canceled or replaced.</summary>
    Canceled,
    /// <summary>The transition callback faulted.</summary>
    Failed
}

/// <summary>Configures an animated theme transition.</summary>
public sealed class ThemeTransitionOptions
{
    private TimeSpan duration = TimeSpan.FromMilliseconds(250);
    private ThemeEasing easing = ThemeEasing.EaseInOut;
    private AnimationReplacementMode replacementMode = AnimationReplacementMode.Replace;

    /// <summary>Gets or sets whether compatible values should animate.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the unscaled transition duration.</summary>
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (value < TimeSpan.Zero || value > ThemeSecurityLimits.MaximumAnimationDuration)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The transition duration is outside the supported range.");
            duration = value;
        }
    }

    /// <summary>Gets or sets the stable easing applied by the shared scheduler.</summary>
    public ThemeEasing Easing
    {
        get => easing;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The theme easing is not defined.");
            easing = value;
        }
    }

    /// <summary>
    /// Gets or sets whether a request replaces a running transition or is canceled while that
    /// transition remains active.
    /// </summary>
    public AnimationReplacementMode ReplacementMode
    {
        get => replacementMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The replacement mode is not defined.");
            replacementMode = value;
        }
    }

    /// <summary>
    /// Gets or sets whether a platform reduced-motion request must complete immediately.
    /// </summary>
    /// <remarks>The scheduler's global animation policy always remains authoritative.</remarks>
    public bool RespectReducedMotion { get; set; } = true;

    internal ThemeTransitionOptions Clone()
        => new()
        {
            Enabled = Enabled,
            Duration = Duration,
            Easing = Easing,
            ReplacementMode = ReplacementMode,
            RespectReducedMotion = RespectReducedMotion
        };
}

/// <summary>Configures one atomic theme apply request.</summary>
public sealed class ThemeApplyOptions
{
    private ThemeVariant systemFallbackVariant = ThemeVariant.Light;

    /// <summary>Gets or sets transition behavior.</summary>
    public ThemeTransitionOptions Transition { get; set; } = new();

    /// <summary>Gets or sets the explicit fallback when platform theme detection is unavailable.</summary>
    public ThemeVariant SystemFallbackVariant
    {
        get => systemFallbackVariant;
        set
        {
            if (value is not (ThemeVariant.Light or ThemeVariant.Dark))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The system fallback must be Light or Dark.");
            systemFallbackVariant = value;
        }
    }

    internal ThemeApplyOptions Clone()
        => new()
        {
            Transition = (Transition ?? throw new InvalidOperationException("Transition options cannot be null.")).Clone(),
            SystemFallbackVariant = SystemFallbackVariant
        };
}

/// <summary>Controls and observes one optional animated theme transition.</summary>
public sealed class ThemeTransitionHandle
{
    private readonly TaskCompletionSource<ThemeTransitionStatus> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action cancel;
    private int state;

    internal ThemeTransitionHandle(Action cancel)
    {
        this.cancel = cancel;
        state = (int)ThemeTransitionStatus.Running;
    }

    /// <summary>Gets the current thread-safe transition state.</summary>
    public ThemeTransitionStatus State => (ThemeTransitionStatus)Volatile.Read(ref state);

    /// <summary>Gets a task that completes with the terminal transition state.</summary>
    public Task<ThemeTransitionStatus> Completion => completion.Task;

    /// <summary>Cancels the transition. The manager snaps the active theme to its final values.</summary>
    public void Cancel() => cancel();

    internal void Complete(ThemeTransitionStatus status)
    {
        if (status == ThemeTransitionStatus.Running)
            throw new ArgumentOutOfRangeException(nameof(status));
        if (Interlocked.CompareExchange(ref state, (int)status, (int)ThemeTransitionStatus.Running) ==
            (int)ThemeTransitionStatus.Running)
        {
            completion.TrySetResult(status);
        }
    }
}

/// <summary>Contains the outcome of one theme apply request.</summary>
public sealed class ThemeApplyResult
{
    internal ThemeApplyResult(
        ThemeApplyStatus status,
        ThemeResolvedSnapshot? snapshot,
        IReadOnlyList<ThemeDiagnostic> diagnostics,
        Exception? exception = null,
        ThemeTransitionHandle? transition = null)
    {
        Status = status;
        Snapshot = snapshot;
        Diagnostics = diagnostics;
        Exception = exception;
        Transition = transition;
    }

    /// <summary>Gets whether the theme commit succeeded.</summary>
    public bool Success => Status == ThemeApplyStatus.Applied;
    /// <summary>Gets the request outcome.</summary>
    public ThemeApplyStatus Status { get; }
    /// <summary>Gets the committed snapshot, when successful.</summary>
    public ThemeResolvedSnapshot? Snapshot { get; }
    /// <summary>Gets validation and resolution diagnostics.</summary>
    public IReadOnlyList<ThemeDiagnostic> Diagnostics { get; }
    /// <summary>Gets the safe apply exception, when one occurred.</summary>
    public Exception? Exception { get; }
    /// <summary>Gets the optional transition handle.</summary>
    public ThemeTransitionHandle? Transition { get; }
}

/// <summary>Provides data before a theme commit.</summary>
public sealed class ThemeChangingEventArgs : EventArgs
{
    internal ThemeChangingEventArgs(ThemeResolvedSnapshot? previous, ThemeResolvedSnapshot next)
    {
        Previous = previous;
        Next = next;
    }

    /// <summary>Gets the previously active snapshot.</summary>
    public ThemeResolvedSnapshot? Previous { get; }
    /// <summary>Gets the validated snapshot proposed for commit.</summary>
    public ThemeResolvedSnapshot Next { get; }
    /// <summary>Gets or sets whether the request should be canceled before commit.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Provides data after a theme commit.</summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    internal ThemeChangedEventArgs(ThemeResolvedSnapshot? previous, ThemeResolvedSnapshot current, ThemeTransitionHandle? transition)
    {
        Previous = previous;
        Current = current;
        Transition = transition;
    }

    /// <summary>Gets the previous snapshot.</summary>
    public ThemeResolvedSnapshot? Previous { get; }
    /// <summary>Gets the newly committed snapshot.</summary>
    public ThemeResolvedSnapshot Current { get; }
    /// <summary>Gets the transition that started after commit, if any.</summary>
    public ThemeTransitionHandle? Transition { get; }
}

/// <summary>Provides data when validation, commit, or rollback fails.</summary>
public sealed class ThemeApplyFailedEventArgs : EventArgs
{
    internal ThemeApplyFailedEventArgs(ThemeDefinition theme, ThemeApplyResult result)
    {
        try
        {
            Theme = theme.Clone();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // Invalid user-provided brush state can be the reason Apply failed in the first place.
            // Preserve safe identity metadata so the failure event remains observable without
            // attempting to expose or share the offending mutable value.
            Theme = new ThemeDefinition(theme.Id, theme.Name)
            {
                SchemaVersion = theme.SchemaVersion,
                Description = theme.Description,
                Author = theme.Author,
                BaseTheme = theme.BaseTheme,
                Variant = theme.Variant
            };
        }
        Result = result;
    }

    /// <summary>
    /// Gets an isolated copy of the rejected definition, or its safe identity metadata when an
    /// invalid mutable value prevented a complete copy.
    /// </summary>
    public ThemeDefinition Theme { get; }
    /// <summary>Gets the failure result.</summary>
    public ThemeApplyResult Result { get; }
}

/// <summary>Provides data when an animated transition terminates.</summary>
public sealed class ThemeTransitionCompletedEventArgs : EventArgs
{
    internal ThemeTransitionCompletedEventArgs(string themeId, ThemeTransitionStatus status)
    {
        ThemeId = themeId;
        Status = status;
    }

    /// <summary>Gets the target theme identifier.</summary>
    public string ThemeId { get; }
    /// <summary>Gets the terminal transition status.</summary>
    public ThemeTransitionStatus Status { get; }
}

/// <summary>Contains safe information about the last theme failure.</summary>
/// <param name="ErrorType">The stable exception type name or validation category.</param>
/// <param name="Message">A safe message that does not expose environment paths.</param>
/// <param name="Timestamp">The UTC timestamp at which the failure was recorded.</param>
public sealed record ThemeFailureInfo(string ErrorType, string Message, DateTimeOffset Timestamp);

/// <summary>Provides a read-only, thread-safe ThemeManager diagnostic snapshot.</summary>
public sealed class ThemeManagerDiagnostics
{
    internal ThemeManagerDiagnostics(
        string? activeThemeId,
        string? activeThemeName,
        ThemeVariant activeVariant,
        int schemaVersion,
        IReadOnlyList<string> baseChain,
        ThemeTokenCounts tokenCounts,
        TimeSpan lastApplyDuration,
        ThemeTransitionStatus transitionState,
        IReadOnlyList<ThemeDiagnostic> validationDiagnostics,
        ThemeFailureInfo? lastFailure,
        long successfulSwitches,
        long canceledSwitches,
        long failedSwitches)
    {
        ActiveThemeId = activeThemeId;
        ActiveThemeName = activeThemeName;
        ActiveVariant = activeVariant;
        SchemaVersion = schemaVersion;
        BaseChain = baseChain;
        TokenCounts = tokenCounts;
        LastApplyDuration = lastApplyDuration;
        TransitionState = transitionState;
        ValidationDiagnostics = validationDiagnostics;
        LastFailure = lastFailure;
        SuccessfulSwitches = successfulSwitches;
        CanceledSwitches = canceledSwitches;
        FailedSwitches = failedSwitches;
    }

    /// <summary>Gets the active theme identifier.</summary>
    public string? ActiveThemeId { get; }
    /// <summary>Gets the active theme name.</summary>
    public string? ActiveThemeName { get; }
    /// <summary>Gets the effective variant.</summary>
    public ThemeVariant ActiveVariant { get; }
    /// <summary>Gets the active schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets the resolved base chain.</summary>
    public IReadOnlyList<string> BaseChain { get; }
    /// <summary>Gets resolved token counts.</summary>
    public ThemeTokenCounts TokenCounts { get; }
    /// <summary>Gets the duration of the latest apply pipeline through commit.</summary>
    public TimeSpan LastApplyDuration { get; }
    /// <summary>Gets the current or most recent transition state.</summary>
    public ThemeTransitionStatus TransitionState { get; }
    /// <summary>Gets diagnostics from the latest validation.</summary>
    public IReadOnlyList<ThemeDiagnostic> ValidationDiagnostics { get; }
    /// <summary>Gets safe details of the last failure.</summary>
    public ThemeFailureInfo? LastFailure { get; }
    /// <summary>Gets the number of successful commits.</summary>
    public long SuccessfulSwitches { get; }
    /// <summary>Gets the number of canceled requests or transitions.</summary>
    public long CanceledSwitches { get; }
    /// <summary>Gets the number of failed requests or transitions.</summary>
    public long FailedSwitches { get; }
}

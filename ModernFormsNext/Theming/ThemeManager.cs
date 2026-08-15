using System.Diagnostics;
using ModernFormsNext.Animations;

namespace ModernFormsNext;

/// <summary>
/// Validates, resolves, and atomically applies application-wide ModernFormsNext themes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Current"/> has application lifetime. Apply requests are thread-safe; validation and
/// inheritance resolution may run on the calling thread, while resource commit, rollback,
/// transition callbacks, and events run on the UI dispatcher thread. No callback is invoked while
/// the manager lock is held.
/// </para>
/// <para>
/// The active snapshot is immutable. Theme resources are a dedicated final fallback below
/// application resources, so applying a theme cannot overwrite unrelated
/// <see cref="Application.Resources"/> entries.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ThemeApplyResult result = ThemeManager.Current.Apply(
///     BuiltInThemes.Dark,
///     new ThemeApplyOptions
///     {
///         Transition = new ThemeTransitionOptions
///         {
///             Enabled = true,
///             Duration = TimeSpan.FromMilliseconds(200)
///         }
///     });
/// </code>
/// </example>
public sealed class ThemeManager
{
    private const string TransitionAnimationKey = "ThemeManager.Transition";
    private static readonly Lazy<ThemeManager> CurrentInstance =
        new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly object sync = new();
    private readonly Dictionary<string, ThemeDefinition> registeredThemes = new(StringComparer.Ordinal);
    private readonly AnimationScheduler scheduler;
    private readonly IThemeDispatcher dispatcher;
    private readonly IThemeEnvironment environment;
    private readonly IThemeLegacyStore legacyStore;
    private readonly ThemeSecurityLimits limits;
    private readonly ResourceDictionary resources;
    private ThemeDefinition? activeDefinition;
    private ThemeResolvedSnapshot? activeSnapshot;
    private ThemeTransitionRuntime? currentTransition;
    private IReadOnlyList<ThemeDiagnostic> lastDiagnostics = Array.Empty<ThemeDiagnostic>();
    private ThemeFailureInfo? lastFailure;
    private TimeSpan lastApplyDuration;
    private ThemeTransitionStatus transitionState = ThemeTransitionStatus.None;
    private long successfulSwitches;
    private long canceledSwitches;
    private long failedSwitches;
    private long latestRequest;

    private ThemeManager()
        : this(
            AnimationScheduler.Default,
            new DefaultThemeDispatcher(),
            new DefaultThemeEnvironment(),
            new ThemeSecurityLimits(),
            Application.ThemeResourcesInternal,
            new DefaultThemeLegacyStore(),
            initializeBuiltIn: true)
    {
    }

    internal ThemeManager(
        AnimationScheduler scheduler,
        IThemeDispatcher dispatcher,
        IThemeEnvironment environment,
        ThemeSecurityLimits limits,
        ResourceDictionary? resources = null,
        IThemeLegacyStore? legacyStore = null,
        bool initializeBuiltIn = false)
    {
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.legacyStore = legacyStore ?? new DefaultThemeLegacyStore();
        this.limits = (limits ?? throw new ArgumentNullException(nameof(limits))).CloneValidated();
        this.resources = resources ?? new ResourceDictionary();

        RegisterCore(BuiltInThemes.Base);
        RegisterCore(BuiltInThemes.Light);
        RegisterCore(BuiltInThemes.Dark);

        if (initializeBuiltIn)
        {
            ThemeDefinition light = BuiltInThemes.Light;
            ThemeResolutionResult resolution = Resolve(light, ThemeVariant.Light);
            if (!resolution.Success)
                throw new InvalidOperationException("The built-in light theme failed internal validation.");

            activeDefinition = light.Clone();
            activeSnapshot = resolution.Snapshot;
            this.resources.ReplaceSnapshot(resolution.Snapshot!.CreateResourceEntries());
            Dictionary<string, object> legacy = this.legacyStore.GetSnapshot();
            this.legacyStore.Replace(ThemeLegacyProjector.Create(resolution.Snapshot, legacy));
        }
    }

    /// <summary>Gets the process-wide application ThemeManager.</summary>
    public static ThemeManager Current => CurrentInstance.Value;

    /// <summary>Gets an isolated authoring copy of the active theme.</summary>
    public ThemeDefinition? ActiveTheme
    {
        get
        {
            lock (sync)
                return activeDefinition?.Clone();
        }
    }

    /// <summary>Gets the immutable active resolved snapshot.</summary>
    public ThemeResolvedSnapshot? ActiveSnapshot
    {
        get
        {
            lock (sync)
                return activeSnapshot;
        }
    }

    /// <summary>Gets the effective light, dark, or custom active variant.</summary>
    public ThemeVariant ActiveVariant
    {
        get
        {
            lock (sync)
                return activeSnapshot?.Variant ?? ThemeVariant.Custom;
        }
    }

    /// <summary>Gets the current resolved theme-resource dictionary as a read-only view.</summary>
    public IReadOnlyDictionary<object, object?> Resources => resources;

    /// <summary>Occurs on the UI thread after validation and before any commit mutation.</summary>
    public event EventHandler<ThemeChangingEventArgs>? ThemeChanging;

    /// <summary>Occurs on the UI thread after commit and transition start, not after transition completion.</summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>Occurs on the UI thread after a failed apply and any required rollback.</summary>
    public event EventHandler<ThemeApplyFailedEventArgs>? ThemeApplyFailed;

    /// <summary>Occurs on the UI thread when an animated transition completes, is canceled, or faults.</summary>
    public event EventHandler<ThemeTransitionCompletedEventArgs>? ThemeTransitionCompleted;

    /// <summary>Registers an isolated theme for inheritance lookup.</summary>
    /// <param name="theme">The mutable authoring definition to copy.</param>
    /// <param name="replace">Whether to replace an existing registration with the same ID.</param>
    /// <exception cref="ArgumentException">The definition is invalid or the ID is already registered.</exception>
    public void Register(ThemeDefinition theme, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ThemeValidationResult validation = new ThemeResolver(
            static _ => null,
            static () => ThemeVariant.Light,
            limits).ValidateWithoutBases(theme);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Diagnostics.First(static item => item.Severity == ThemeDiagnosticSeverity.Error).Message, nameof(theme));

        ThemeDefinition copy = theme.Clone();
        lock (sync)
        {
            if (!replace && registeredThemes.ContainsKey(copy.Id))
                throw new ArgumentException($"Theme '{copy.Id}' is already registered.", nameof(theme));
            registeredThemes[copy.Id] = copy;
        }
    }

    /// <summary>Removes a registered custom theme.</summary>
    /// <param name="themeId">The stable theme ID.</param>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    /// <remarks>Built-in registrations cannot be removed.</remarks>
    public bool Unregister(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);
        if (themeId is BuiltInThemes.BaseThemeId or BuiltInThemes.LightThemeId or BuiltInThemes.DarkThemeId)
            return false;
        lock (sync)
            return registeredThemes.Remove(themeId);
    }

    /// <summary>Validates and resolves a theme without applying it.</summary>
    /// <param name="theme">The definition to validate.</param>
    /// <param name="systemFallbackVariant">The fallback for a System definition.</param>
    /// <returns>All validation and inheritance diagnostics.</returns>
    public ThemeValidationResult Validate(
        ThemeDefinition theme,
        ThemeVariant systemFallbackVariant = ThemeVariant.Light)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (systemFallbackVariant is not (ThemeVariant.Light or ThemeVariant.Dark))
            throw new ArgumentOutOfRangeException(nameof(systemFallbackVariant));
        ThemeResolutionResult resolution = Resolve(theme, systemFallbackVariant);
        return new ThemeValidationResult(resolution.Diagnostics);
    }

    /// <summary>Applies a theme and blocks until the UI-thread commit has completed.</summary>
    /// <param name="theme">The mutable authoring definition, which is copied immediately.</param>
    /// <param name="options">
    /// Optional transition and System fallback behavior. Omitting this value applies the theme
    /// immediately without starting an animation.
    /// </param>
    /// <returns>The commit result. An animation may still be running.</returns>
    /// <remarks>
    /// Calling this method from a background thread waits for the dispatcher. Prefer
    /// <see cref="ApplyAsync(ThemeDefinition, ThemeApplyOptions?, CancellationToken)"/> in async code.
    /// </remarks>
    public ThemeApplyResult Apply(ThemeDefinition theme, ThemeApplyOptions? options = null)
        => ApplyAsync(theme, options).GetAwaiter().GetResult();

    /// <summary>Validates on the caller thread and atomically commits on the UI thread.</summary>
    /// <param name="theme">The mutable authoring definition, which is copied immediately.</param>
    /// <param name="options">
    /// Optional transition and System fallback behavior. Omitting this value applies the theme
    /// immediately without starting an animation.
    /// </param>
    /// <param name="cancellationToken">Cancels the request before commit.</param>
    /// <returns>The commit result. Observe <see cref="ThemeApplyResult.Transition"/> for animation completion.</returns>
    public async Task<ThemeApplyResult> ApplyAsync(
        ThemeDefinition theme,
        ThemeApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var stopwatch = Stopwatch.StartNew();
        long requestId = Interlocked.Increment(ref latestRequest);
        ThemeDefinition definition;
        ThemeApplyOptions applyOptions;
        try
        {
            definition = theme.Clone();
            applyOptions = (options ?? new ThemeApplyOptions()).Clone();
        }
        catch (Exception exception)
        {
            IReadOnlyList<ThemeDiagnostic> diagnostics = exception is NotSupportedException
                ? new[]
                {
                    new ThemeDiagnostic(
                        "THEME_VALUE_UNSUPPORTED",
                        ThemeDiagnosticSeverity.Error,
                        "The theme contains a mutable value that cannot be safely cloned.",
                        "brushes")
                }
                : Array.Empty<ThemeDiagnostic>();
            return await FailAsync(theme, diagnostics, exception, stopwatch.Elapsed).ConfigureAwait(false);
        }

        ThemeResolutionResult resolution = Resolve(definition, applyOptions.SystemFallbackVariant);
        SetLastDiagnostics(resolution.Diagnostics);
        if (!resolution.Success)
            return await FailAsync(definition, resolution.Diagnostics, null, stopwatch.Elapsed).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested || requestId != Interlocked.Read(ref latestRequest))
            return CancelResult(resolution.Diagnostics, stopwatch.Elapsed);

        try
        {
            ThemeApplyResult result = await dispatcher.InvokeAsync(
                () => Commit(requestId, definition, resolution.Snapshot!, resolution.Diagnostics, applyOptions),
                cancellationToken).ConfigureAwait(false);
            SetLastApplyDuration(stopwatch.Elapsed);
            return result;
        }
        catch (OperationCanceledException)
        {
            return CancelResult(resolution.Diagnostics, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            return await FailAsync(definition, resolution.Diagnostics, exception, stopwatch.Elapsed).ConfigureAwait(false);
        }
    }

    /// <summary>Cancels the active transition and snaps the committed theme to its final values.</summary>
    /// <returns><see langword="true"/> when a running transition was canceled.</returns>
    public bool CancelTransition()
    {
        ThemeTransitionRuntime? runtime;
        lock (sync)
            runtime = currentTransition;
        return runtime is not null && CancelTransition(runtime.RequestId);
    }

    /// <summary>Gets a safe, read-only diagnostic snapshot.</summary>
    /// <returns>A point-in-time copy that can be inspected from any thread.</returns>
    public ThemeManagerDiagnostics GetDiagnostics()
    {
        lock (sync)
        {
            ThemeResolvedSnapshot? snapshot = activeSnapshot;
            return new ThemeManagerDiagnostics(
                snapshot?.Id,
                snapshot?.Name,
                snapshot?.Variant ?? ThemeVariant.Custom,
                snapshot?.SchemaVersion ?? 0,
                snapshot?.BaseChain.ToArray() ?? Array.Empty<string>(),
                snapshot?.Counts ?? default,
                lastApplyDuration,
                transitionState,
                lastDiagnostics.ToArray(),
                lastFailure,
                successfulSwitches,
                canceledSwitches,
                failedSwitches);
        }
    }

    internal ThemeVariant ResolveSystemVariant(ThemeVariant fallback) => environment.GetSystemVariant(fallback);

    private static ThemeManager CreateDefault() => new();

    private void RegisterCore(ThemeDefinition theme)
        => registeredThemes.Add(theme.Id, theme.Clone());

    private ThemeResolutionResult Resolve(ThemeDefinition definition, ThemeVariant fallback)
        => new ThemeResolver(FindRegistered, () => environment.GetSystemVariant(fallback), limits).Resolve(definition);

    private ThemeDefinition? FindRegistered(string id)
    {
        lock (sync)
            return registeredThemes.TryGetValue(id, out ThemeDefinition? theme) ? theme.Clone() : null;
    }

    private ThemeApplyResult Commit(
        long requestId,
        ThemeDefinition definition,
        ThemeResolvedSnapshot snapshot,
        IReadOnlyList<ThemeDiagnostic> diagnostics,
        ThemeApplyOptions options)
    {
        if (requestId != Interlocked.Read(ref latestRequest))
            return CancelResult(diagnostics, TimeSpan.Zero);

        lock (sync)
        {
            if (currentTransition is not null &&
                options.Transition.ReplacementMode == AnimationReplacementMode.IgnoreNew)
            {
                return CancelResult(diagnostics, TimeSpan.Zero);
            }
        }

        ThemeResolvedSnapshot? previousSnapshot;
        ThemeDefinition? previousDefinition;
        lock (sync)
        {
            previousSnapshot = activeSnapshot;
            previousDefinition = activeDefinition;
        }

        var changing = new ThemeChangingEventArgs(previousSnapshot, snapshot);
        ThemeChanging?.Invoke(this, changing);
        if (changing.Cancel)
            return CancelResult(diagnostics, TimeSpan.Zero);

        Dictionary<object, object?> oldResources = resources.GetSnapshot();
        Dictionary<string, object> oldLegacy = legacyStore.GetSnapshot();
        CancelCurrentTransitionForReplacement();

        Dictionary<object, object?> targetResources = snapshot.CreateResourceEntries();
        Dictionary<string, object> targetLegacy = ThemeLegacyProjector.Create(snapshot, oldLegacy);
        bool transitionEnabled = options.Transition.Enabled &&
            options.Transition.Duration > TimeSpan.Zero &&
            !environment.IsDesignMode &&
            !scheduler.Policy.ShouldCompleteImmediately &&
            (!options.Transition.RespectReducedMotion || !environment.IsReducedMotionRequested);
        ThemeTransitionPlan? plan = transitionEnabled
            ? ThemeTransitionPlan.Create(oldResources, targetResources, oldLegacy, targetLegacy)
            : null;
        if (plan?.HasAnimations != true)
            plan = null;

        ThemeTransitionRuntime? runtime = null;
        try
        {
            ResourceDictionaryChange[] resourceChanges = resources.ReplaceSnapshot(plan?.Resources ?? targetResources);
            legacyStore.Replace(plan?.LegacyValues ?? targetLegacy);
            lock (sync)
            {
                activeDefinition = definition.Clone();
                activeSnapshot = snapshot;
            }

            using (Application.BeginVisualInvalidationBatch())
            {
                resources.PublishChanges(resourceChanges);
                legacyStore.NotifyChanged();
            }

            if (plan is not null)
            {
                var transitionHandle = new ThemeTransitionHandle(() => CancelTransition(requestId));
                var animationOptions = new AnimationOptions
                {
                    Duration = options.Transition.Duration,
                    Easing = options.Transition.Easing switch
                    {
                        ThemeEasing.Linear => Easings.Linear,
                        ThemeEasing.EaseIn => Easings.EaseIn,
                        ThemeEasing.EaseOut => Easings.EaseOut,
                        ThemeEasing.EaseInOut => Easings.EaseInOut,
                        _ => Easings.Linear
                    },
                    ReplacementMode = options.Transition.ReplacementMode
                };
                AnimationHandle animation = scheduler.Start(
                    this,
                    TransitionAnimationKey,
                    progress => plan.Apply(progress, resources, legacyStore),
                    animationOptions);
                runtime = new ThemeTransitionRuntime(requestId, snapshot.Id, plan, animation, transitionHandle);
                lock (sync)
                {
                    currentTransition = runtime;
                    transitionState = ThemeTransitionStatus.Running;
                }
            }

            lock (sync)
            {
                successfulSwitches++;
                lastFailure = null;
                lastDiagnostics = diagnostics.ToArray();
                if (runtime is null)
                    transitionState = ThemeTransitionStatus.None;
            }

            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(previousSnapshot, snapshot, runtime?.PublicHandle));
            if (runtime is not null)
                _ = ObserveTransitionAsync(runtime);
            return new ThemeApplyResult(ThemeApplyStatus.Applied, snapshot, diagnostics, transition: runtime?.PublicHandle);
        }
        catch (Exception commitException)
        {
            runtime?.Animation.Cancel();
            Exception? rollbackException = null;
            try
            {
                ResourceDictionaryChange[] rollbackChanges = resources.ReplaceSnapshot(oldResources);
                legacyStore.Replace(oldLegacy);
                lock (sync)
                {
                    activeDefinition = previousDefinition;
                    activeSnapshot = previousSnapshot;
                    if (ReferenceEquals(currentTransition, runtime))
                        currentTransition = null;
                    transitionState = ThemeTransitionStatus.Failed;
                }
                using (Application.BeginVisualInvalidationBatch())
                {
                    resources.PublishChanges(rollbackChanges);
                    legacyStore.NotifyChanged();
                }
            }
            catch (Exception exception)
            {
                rollbackException = exception;
            }

            if (runtime is not null)
                _ = ObserveTransitionAsync(runtime);

            throw rollbackException is null
                ? new InvalidOperationException("Theme commit failed and was rolled back.", commitException)
                : new AggregateException("Theme commit and rollback both failed.", commitException, rollbackException);
        }
    }

    private void CancelCurrentTransitionForReplacement()
    {
        ThemeTransitionRuntime? runtime;
        lock (sync)
            runtime = currentTransition;
        if (runtime is null)
            return;
        runtime.SnapOnCancel = false;
        runtime.Animation.Cancel();
    }

    private bool CancelTransition(long requestId)
    {
        ThemeTransitionRuntime? runtime;
        lock (sync)
            runtime = currentTransition?.RequestId == requestId ? currentTransition : null;
        if (runtime is null)
            return false;

        runtime.SnapOnCancel = true;
        runtime.Animation.Cancel();
        return true;
    }

    private async Task ObserveTransitionAsync(ThemeTransitionRuntime runtime)
    {
        AnimationState animationState = await runtime.Animation.Completion.ConfigureAwait(false);
        dispatcher.Post(() => CompleteTransition(runtime, animationState));
    }

    private void CompleteTransition(ThemeTransitionRuntime runtime, AnimationState animationState)
    {
        ThemeTransitionStatus status = animationState switch
        {
            AnimationState.Completed => ThemeTransitionStatus.Completed,
            AnimationState.Canceled => ThemeTransitionStatus.Canceled,
            AnimationState.Faulted => ThemeTransitionStatus.Failed,
            _ => ThemeTransitionStatus.Failed
        };

        bool isCurrent;
        lock (sync)
            isCurrent = ReferenceEquals(currentTransition, runtime);

        if (isCurrent &&
            (status == ThemeTransitionStatus.Completed ||
             status == ThemeTransitionStatus.Failed ||
             status == ThemeTransitionStatus.Canceled && runtime.SnapOnCancel))
        {
            try
            {
                runtime.Plan.Apply(1f, resources, legacyStore);
            }
            catch
            {
                status = ThemeTransitionStatus.Failed;
            }
        }

        lock (sync)
        {
            if (ReferenceEquals(currentTransition, runtime))
            {
                currentTransition = null;
                transitionState = status;
            }
            if (status == ThemeTransitionStatus.Canceled)
                canceledSwitches++;
            else if (status == ThemeTransitionStatus.Failed)
                failedSwitches++;
        }

        try
        {
            // Complete the public task only after synchronous completion observers have run. Code
            // awaiting the handle can then safely inspect event-driven state without racing the
            // final callback on the UI dispatcher.
            ThemeTransitionCompleted?.Invoke(this, new ThemeTransitionCompletedEventArgs(runtime.ThemeId, status));
        }
        finally
        {
            runtime.PublicHandle.Complete(status);
        }
    }

    private ThemeApplyResult CancelResult(IReadOnlyList<ThemeDiagnostic> diagnostics, TimeSpan duration)
    {
        lock (sync)
        {
            canceledSwitches++;
            lastApplyDuration = duration;
            lastDiagnostics = diagnostics.ToArray();
        }
        return new ThemeApplyResult(ThemeApplyStatus.Canceled, null, diagnostics);
    }

    private async Task<ThemeApplyResult> FailAsync(
        ThemeDefinition theme,
        IReadOnlyList<ThemeDiagnostic> diagnostics,
        Exception? exception,
        TimeSpan duration)
    {
        var result = new ThemeApplyResult(ThemeApplyStatus.Failed, null, diagnostics, exception);
        lock (sync)
        {
            failedSwitches++;
            lastApplyDuration = duration;
            lastDiagnostics = diagnostics.ToArray();
            lastFailure = new ThemeFailureInfo(
                exception?.GetType().Name ?? "ThemeValidation",
                exception?.Message ?? diagnostics.FirstOrDefault(static item => item.Severity == ThemeDiagnosticSeverity.Error)?.Message ?? "Theme validation failed.",
                DateTimeOffset.UtcNow);
        }

        try
        {
            await dispatcher.InvokeAsync(() =>
            {
                ThemeApplyFailed?.Invoke(this, new ThemeApplyFailedEventArgs(theme, result));
                return true;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Diagnostics remain available even when a user failure-event handler throws.
        }
        return result;
    }

    private void SetLastDiagnostics(IReadOnlyList<ThemeDiagnostic> diagnostics)
    {
        lock (sync)
            lastDiagnostics = diagnostics.ToArray();
    }

    private void SetLastApplyDuration(TimeSpan duration)
    {
        lock (sync)
            lastApplyDuration = duration;
    }

    private sealed class ThemeTransitionRuntime(
        long requestId,
        string themeId,
        ThemeTransitionPlan plan,
        AnimationHandle animation,
        ThemeTransitionHandle publicHandle)
    {
        public long RequestId { get; } = requestId;
        public string ThemeId { get; } = themeId;
        public ThemeTransitionPlan Plan { get; } = plan;
        public AnimationHandle Animation { get; } = animation;
        public ThemeTransitionHandle PublicHandle { get; } = publicHandle;
        public volatile bool SnapOnCancel;
    }
}

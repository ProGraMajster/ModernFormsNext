using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;
using SkiaSharp;

namespace ModernFormsNext;

public partial class ToolTip
{
    private const string PresentationChannel = "ToolTip.Presentation";
    private const int EntranceMilliseconds = 120, ExitMilliseconds = 80;
    private const float EntranceOffset = 6;
    private long requestVersion, presentationVersion, servicesVersion;
    private int presentationThread;
    private bool presentationDisposed, presentationRetiring, hiding, highContrast;
    private bool supportsFading;
    private float presentationOpacity = 1;
    private TimeSpan? lastHiddenAt;
    private AnimationHandle? presentationAnimation;
    private IPopupPresentation? nativePresentation;
    private Form? pendingForm;
    private Control? popupCallbackAnchor;
    private Control? manualAnchor;
    private ApplicationLifecycle? observedLifecycle;
    private IPlatformSettings? observedSettings;
    private IPlatformDispatcher? presentationDispatcher;
    private SKColor? contrastBackground, contrastForeground;

    private void VerifyPresentationAccess()
    {
        ObjectDisposedException.ThrowIf(presentationDisposed || presentationRetiring, this);
        if (presentationThread != 0 && presentationThread != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("ToolTip presentation must be used on its owning UI thread.");
    }

    private bool IsCurrentRequest(long request)
        => !presentationDisposed && !presentationRetiring && request == requestVersion;

    private bool IsCurrentPresentation(long version, PopupWindow window, ToolTipPopupControl target)
        => !presentationDisposed && !presentationRetiring && version == presentationVersion &&
            ReferenceEquals(popup, window) && ReferenceEquals(popupControl, target) &&
            !window.InputBindingsClosed && !target.IsDisposed && !target.Disposing;

    private bool CanPresent(Control anchor, Form form)
    {
        if (presentationDisposed || presentationRetiring || !active || anchor.IsDisposed || anchor.Disposing ||
            !anchor.Visible || !ReferenceEquals(anchor.FindForm(), form) || form.InputBindingsClosed ||
            !form.Visible || (!ShowAlways && !form.IsActive)) return false;
        var state = Application.Lifecycle.Snapshot;
        // Desktop process deactivation is distinct from mobile background/suspension. Unknown
        // legacy lifecycle support falls back to the owner's actual visibility/activation.
        return state.State is not (PlatformApplicationLifecycleState.Background or PlatformApplicationLifecycleState.NoHost) &&
            state.Phase is not (PlatformApplicationPhase.Suspended or PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited);
    }

    private void PresentToolTip(long request, Control anchor, Form form, string text, string title,
        Size size, Point location, int duration)
    {
        if (!EnsurePopup(form, request)) return;
        var window = popup!;
        var target = popupControl!;
        long version = ++presentationVersion;
        CancelPresentationAnimation();
        if (!IsCurrentRequest(request) || !IsCurrentPresentation(version, window, target)) return;
        bool reuse = window.Visible;
        bool announced = reuse && target.PresentationVisible && !hiding;
        hiding = false;
        try
        {
            ObservePresentationServices();
            if (!IsCurrentRequest(request) || !IsCurrentPresentation(version, window, target)) return;
            ObserveManualAnchor(tools.ContainsKey(anchor) ? null : anchor);
            if (!CanPresent(anchor, form)) { RetirePresentation(immediate: true); return; }
            displayedControl = anchor;
            target.Configure(this, form, anchor, text, title, ToolTipIcon, size);
            if (!IsCurrentRequest(request) || !IsCurrentPresentation(version, window, target)) return;
            window.Size = size;
            window.Style.BackgroundColor = target.CurrentStyle.GetBackgroundColor();
            nativePresentation = window.window.TryGetFeature(typeof(IPopupPresentation)) as IPopupPresentation;
            nativePresentation?.SetPassive();
            supportsFading = nativePresentation?.TrySetOpacity(reuse ? presentationOpacity : 1) == true;
            if (!IsCurrentRequest(request) || !IsCurrentPresentation(version, window, target)) return;
            var scheduler = AnimationScheduler.Default;
            scheduler.RefreshPlatformPolicy();
            bool motion = !highContrast && !scheduler.Policy.ShouldCompleteImmediately;
            float fromOpacity = reuse && supportsFading ? presentationOpacity : UseFading && supportsFading && motion ? 0 : 1;
            float fromTranslation = reuse ? target.TranslationY : UseAnimation && motion ? EntranceOffset : 0;
            ApplyPresentationFrame(version, window, target, fromOpacity, fromTranslation);
            if (!IsCurrentPresentation(version, window, target)) return;
            window.Show(location);
            if (!IsCurrentRequest(request) || !IsCurrentPresentation(version, window, target) || !window.Visible) return;
            StartAutoPopTimer(duration);
            if (announced && fromOpacity == 1 && fromTranslation == 0)
            {
                target.SetPresentationVisible(true);
                return;
            }
            StartPresentationTransition(version, window, target, scheduler, fromOpacity, fromTranslation, showing: true);
        }
        catch (Exception failure)
        {
            try { if (IsCurrentRequest(request)) RetirePresentation(immediate: true); }
            catch (Exception cleanup) { throw new AggregateException("Tooltip presentation and cleanup failed.", failure, cleanup); }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    private void StartPresentationTransition(long version, PopupWindow window, ToolTipPopupControl target,
        AnimationScheduler scheduler, float fromOpacity, float fromTranslation, bool showing)
    {
        bool motion = !highContrast && !scheduler.Policy.ShouldCompleteImmediately;
        bool fade = UseFading && supportsFading && motion;
        float toOpacity = showing || !fade ? 1 : 0;
        float toTranslation = showing || !UseAnimation || !motion ? 0 : -EntranceOffset;
        bool changes = fromOpacity != toOpacity || fromTranslation != toTranslation;
        var handle = scheduler.Start(target, PresentationChannel, progress =>
        {
            ApplyPresentationFrame(version, window, target,
                fromOpacity + (toOpacity - fromOpacity) * progress,
                fromTranslation + (toTranslation - fromTranslation) * progress);
        }, new AnimationOptions
        {
            Duration = motion && changes ? TimeSpan.FromMilliseconds(showing ? EntranceMilliseconds : ExitMilliseconds) : TimeSpan.Zero,
            Easing = Easings.EaseOut
        });
        if (!IsCurrentPresentation(version, window, target)) { handle.Cancel(); return; }
        presentationAnimation = handle;
        // This is the scheduler's existing private UI-dispatcher continuation, not a Task.Run
        // or another timer. Cancellation from a worker cannot run popup operations on that worker.
        var completion = handle.FrameworkCompletion.On(scheduler).GetAwaiter();
        void Finish()
        {
            AnimationState state;
            try { state = completion.GetResult(); }
            catch (Exception dispatchFailure)
            {
                Trace.TraceError("Tooltip completion dispatcher is unavailable: {0}", dispatchFailure);
                return; // A retired dispatcher cannot safely perform another UI operation.
            }
            if (!IsCurrentPresentation(version, window, target)) return;
            presentationAnimation = null;
            try
            {
                if (state == AnimationState.Faulted)
                {
                    Trace.TraceError("Tooltip animation failed: {0}", handle.Exception);
                    RetirePresentation(immediate: true);
                }
                else if (showing)
                {
                    SettleVisiblePresentation(version, window, target);
                }
                else HidePresentationWindow(window, target);
            }
            catch (Exception failure)
            {
                // The scheduler isolates tick callbacks, so report the original observer failure
                // explicitly after cleanup instead of silently leaving a completed popup alive.
                List<Exception>? failures = [failure];
                if (IsCurrentPresentation(version, window, target))
                    Attempt(() => RetirePresentation(immediate: true), ref failures);
                Trace.TraceError("Tooltip completion failed after owned cleanup: {0}", failure);
                ThrowPresentationFailures(failures);
            }
        }
        if (completion.IsCompleted) Finish();
        else completion.OnCompleted(Finish);
    }

    private void ApplyPresentationFrame(long version, PopupWindow window, ToolTipPopupControl target, float opacity, float translation)
    {
        if (!IsCurrentPresentation(version, window, target)) return;
        presentationOpacity = Math.Clamp(opacity, 0, 1);
        if (supportsFading && nativePresentation?.TrySetOpacity(presentationOpacity) != true)
        {
            supportsFading = false;
            presentationOpacity = 1; // Optional native support refused; retain an opaque popup.
        }
        if (IsCurrentPresentation(version, window, target)) target.TranslationY = translation;
    }

    private void RetirePresentation(bool immediate)
    {
        ++requestVersion;
        popupCallbackAnchor = null;
        ClearPending();
        autoPopTimer.Stop();
        if (hiding && !immediate) return; // Repeated Hide cannot extend the exit deadline.
        var window = popup;
        var target = popupControl;
        long version = ++presentationVersion;
        CancelPresentationAnimation();
        if (window is null || target is null) { ReleasePresentationServicesIfIdle(); return; }
        if (displayedControl is not null) lastHiddenAt = AnimationScheduler.Default.ClockTime;
        hiding = true;
        List<Exception>? failures = null;
        Attempt(() => target.SetPresentationVisible(false), ref failures);
        // Semantic observers may replace this presentation or throw. A failed notification
        // must not strand the old native window; a replacement owns its own lifetime.
        if (version == presentationVersion && ReferenceEquals(popup, window))
        {
            if (failures is not null || immediate || presentationRetiring || presentationDisposed || !window.Visible || window.InputBindingsClosed)
                Attempt(() => HidePresentationWindow(window, target), ref failures);
            else
                Attempt(() => StartPresentationTransition(version, window, target, AnimationScheduler.Default,
                    presentationOpacity, target.TranslationY, showing: false), ref failures);
        }
        ThrowPresentationFailures(failures);
    }

    private void HidePresentationWindow(PopupWindow window, ToolTipPopupControl target)
    {
        long request = requestVersion;
        List<Exception>? failures = null;
        if (window.Visible && !window.InputBindingsClosed) Attempt(window.Hide, ref failures);
        if (request == requestVersion && ReferenceEquals(popup, window) && !window.Visible)
        {
            displayedControl = null;
            hiding = false;
            Attempt(() => ObserveManualAnchor(null), ref failures);
            // Keep hidden native alpha until the next Show, avoiding an opaque final flash.
            if (!target.IsDisposed) Attempt(() => target.TranslationY = 0, ref failures);
            Attempt(ReleasePresentationServicesIfIdle, ref failures);
        }
        ThrowPresentationFailures(failures);
    }

    private void CancelPresentationAnimation()
    {
        var old = presentationAnimation;
        presentationAnimation = null;
        old?.Cancel();
    }

    private void SettlePresentation()
    {
        if (popup is not { Visible: true } window || popupControl is not { } target) return;
        if (hiding) { RetirePresentation(immediate: true); return; }
        long version = ++presentationVersion;
        CancelPresentationAnimation();
        SettleVisiblePresentation(version, window, target);
    }

    private void SettleVisiblePresentation(long version, PopupWindow window, ToolTipPopupControl target)
    {
        try
        {
            ApplyPresentationFrame(version, window, target, 1, 0);
            if (IsCurrentPresentation(version, window, target)) target.SetPresentationVisible(true);
        }
        catch (Exception failure)
        {
            List<Exception>? failures = [failure];
            if (IsCurrentPresentation(version, window, target))
                Attempt(() => RetirePresentation(immediate: true), ref failures);
            ThrowPresentationFailures(failures);
        }
    }

    private void PopupPresentationVisibilityChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, popup) || popup!.Visible) return;
        long version = ++presentationVersion;
        var window = popup;
        var target = popupControl;
        CancelPresentationAnimation();
        List<Exception>? failures = null;
        if (target is not null) Attempt(() => target.SetPresentationVisible(false), ref failures);
        if (version == presentationVersion && ReferenceEquals(popup, window) && !window.Visible)
        {
            displayedControl = null;
            hiding = false;
            Attempt(() => ObserveManualAnchor(null), ref failures);
            Attempt(ReleasePresentationServicesIfIdle, ref failures);
        }
        ThrowPresentationFailures(failures);
    }

    private void PopupPresentationClosed(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, popup)) return;
        ++requestVersion;
        ClosePresentationPopup();
        ReleasePresentationServicesIfIdle();
    }

    private void ClosePresentationPopup()
    {
        ++presentationVersion;
        CancelPresentationAnimation();
        autoPopTimer.Stop();
        var old = popup;
        var target = popupControl;
        popup = null; popupControl = null; popupParentForm = null;
        nativePresentation = null; supportsFading = false; displayedControl = null; hiding = false;
        ObserveManualAnchor(null);
        List<Exception>? failures = null;
        if (old is not null)
        {
            old.VisibleChanged -= PopupPresentationVisibilityChanged;
            old.Closed -= PopupPresentationClosed;
            if (!old.InputBindingsClosed) Attempt(old.Close, ref failures);
        }
        if (target is not null) Attempt(target.Dispose, ref failures);
        ThrowPresentationFailures(failures);
    }

    private void ObserveManualAnchor(Control? anchor)
    {
        if (ReferenceEquals(manualAnchor, anchor)) return;
        var previous = manualAnchor;
        manualAnchor = anchor;
        if (previous is not null)
        {
            previous.Disposed -= Control_Disposed;
            previous.ParentChanged -= AnchorPresentationChanged;
            previous.VisibleChanged -= AnchorPresentationChanged;
        }
        if (anchor is not null)
        {
            anchor.Disposed += Control_Disposed;
            anchor.ParentChanged += AnchorPresentationChanged;
            anchor.VisibleChanged += AnchorPresentationChanged;
        }
    }

    private void AnchorPresentationChanged(object? sender, EventArgs e)
    {
        if (sender is not Control anchor) return;
        if (ReferenceEquals(anchor, pendingControl) && (pendingForm is null || !CanPresent(anchor, pendingForm))) ClearPending();
        if (ReferenceEquals(anchor, displayedControl) && (popupParentForm is null || !CanPresent(anchor, popupParentForm)))
            RetirePresentation(immediate: true);
    }

    private void ObservePendingForm(Form? form)
    {
        if (ReferenceEquals(pendingForm, form)) return;
        var previous = pendingForm;
        pendingForm = form;
        if (previous is not null)
        {
            previous.Deactivated -= PendingOwnerChanged;
            previous.VisibleChanged -= PendingOwnerChanged;
            previous.Closed -= PendingOwnerChanged;
        }
        if (form is not null)
        {
            form.Deactivated += PendingOwnerChanged;
            form.VisibleChanged += PendingOwnerChanged;
            form.Closed += PendingOwnerChanged;
        }
    }

    private void PendingOwnerChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, pendingForm) && (pendingControl is null || !CanPresent(pendingControl, pendingForm!))) ClearPending();
    }

    private void ObservePresentationServices()
    {
        if (observedLifecycle is not null) return;
        presentationThread = Environment.CurrentManagedThreadId;
        long version = ++servicesVersion;
        observedLifecycle = Application.Lifecycle;
        presentationDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        observedSettings = AvaloniaGlobals.GetService<IPlatformSettings>();
        observedLifecycle.LifecycleChanged += PresentationLifecycleChanged;
        if (observedSettings is not null)
        {
            observedSettings.ColorValuesChanged += PresentationColorsChanged;
            var settings = observedSettings;
            var colors = settings.GetColorValues();
            if (version == servicesVersion && ReferenceEquals(settings, observedSettings)) ApplyPresentationColors(colors);
        }
    }

    private void PresentationLifecycleChanged(object? sender, PlatformApplicationLifecycleEventArgs e)
    {
        if (presentationDisposed || presentationRetiring || !ReferenceEquals(sender, observedLifecycle)) return;
        var state = observedLifecycle!.Snapshot;
        if (state.State is PlatformApplicationLifecycleState.Background or PlatformApplicationLifecycleState.NoHost ||
            state.Phase is PlatformApplicationPhase.Suspended or PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
            RetirePresentation(immediate: true);
    }

    private void PresentationColorsChanged(object? sender, PlatformColorValues colors)
    {
        long version = servicesVersion;
        void Apply()
        {
            if (presentationDisposed || presentationRetiring || version != servicesVersion || !ReferenceEquals(sender, observedSettings)) return;
            ApplyPresentationColors(colors);
            ApplyVisibleStyle();
            if (highContrast) SettlePresentation();
        }
        if (presentationThread == Environment.CurrentManagedThreadId) Apply();
        else if (presentationDispatcher is { } dispatcher) dispatcher.Post(Apply);
        else WindowKit.Threading.Dispatcher.UIThread.Post(Apply);
    }

    private void ApplyPresentationColors(PlatformColorValues colors)
    {
        highContrast = colors.ContrastPreference == ColorContrastPreference.High;
        contrastBackground = highContrast && colors.BackgroundColor is { } back ? new SKColor(back.R, back.G, back.B, back.A) : null;
        contrastForeground = highContrast && colors.ForegroundColor is { } fore ? new SKColor(fore.R, fore.G, fore.B, fore.A) : null;
    }

    internal SKColor PresentationBackColor => contrastBackground ?? BackColor;
    internal SKColor PresentationForeColor => contrastForeground ?? ForeColor;
    internal SKColor PresentationBorderColor => highContrast ? PresentationForeColor : BorderColor;
    internal SKColor PresentationIconForeground => highContrast ? PresentationBackColor : IconForegroundColor;

    private void ReleasePresentationServicesIfIdle()
    {
        if (pendingControl is not null || popup?.Visible == true) return;
        ReleasePresentationServices();
    }

    private void ReleasePresentationServices()
    {
        ++servicesVersion;
        var lifecycle = observedLifecycle;
        var settings = observedSettings;
        observedLifecycle = null; observedSettings = null; presentationDispatcher = null;
        highContrast = false; contrastBackground = contrastForeground = null;
        List<Exception>? failures = null;
        if (lifecycle is not null) Attempt(() => lifecycle.LifecycleChanged -= PresentationLifecycleChanged, ref failures);
        if (settings is not null) Attempt(() => settings.ColorValuesChanged -= PresentationColorsChanged, ref failures);
        ThrowPresentationFailures(failures);
    }

    private void DisposePresentation()
    {
        if (presentationDisposed || presentationRetiring) return;
        VerifyPresentationAccess();
        presentationRetiring = true;
        ++requestVersion; ++presentationVersion;
        List<Exception>? failures = null;
        Attempt(() => RetirePresentation(immediate: true), ref failures);
        foreach (var anchor in tools.Keys.ToArray()) Attempt(() => Detach(anchor), ref failures);
        tools.Clear();
        Attempt(() => ObservePendingForm(null), ref failures);
        Attempt(() => ObserveManualAnchor(null), ref failures);
        Attempt(ClosePresentationPopup, ref failures);
        Attempt(ReleasePresentationServices, ref failures);
        delayTimer.Tick -= DelayTimer_Tick;
        autoPopTimer.Tick -= AutoPopTimer_Tick;
        Attempt(delayTimer.Dispose, ref failures);
        Attempt(autoPopTimer.Dispose, ref failures);
        onPopup = null; onDraw = null;
        presentationDisposed = true;
        Attempt(() => base.Dispose(true), ref failures);
        ThrowPresentationFailures(failures);
    }

    private static void Attempt(Action action, ref List<Exception>? failures)
    {
        try { action(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
    }

    private static void ThrowPresentationFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException("Tooltip cleanup observers failed.", failures);
    }
}

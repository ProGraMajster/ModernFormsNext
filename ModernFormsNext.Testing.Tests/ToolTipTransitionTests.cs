using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ToolTipTransitionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void IndependentEffectsUseBoundedFramesAndFadeTheWholePopup(bool animation, bool fading)
    {
        using var f = new Fixture(animation, fading);
        f.Tip.Show("Bounded presentation", f.First);
        var popup = f.Popup;
        var control = f.Control;
        Assert.Equal(animation ? 6f : 0f, control.TranslationY);
        Assert.Equal(1f, control.Opacity);
        Assert.Equal(fading ? 0 : 255, f.Corner().Alpha);
        Assert.Equal(animation || fading ? 1 : 0, ActiveCount);
        f.Advance(60);
        if (animation) Assert.InRange(control.TranslationY, .01f, 5.99f);
        if (fading) Assert.InRange(f.Corner().Alpha, (byte)1, (byte)254);
        f.Advance(60);
        Assert.Equal(0f, control.TranslationY);
        Assert.Equal(255, f.Corner().Alpha);
        Assert.False(control.AccessibilityObject.State.HasFlag(AccessibleStates.Invisible));
        Assert.Equal(0, ActiveCount);
        f.Tip.Hide(f.First);
        Assert.True(control.AccessibilityObject.State.HasFlag(AccessibleStates.Invisible));
        if (animation || fading)
        {
            Assert.True(popup.IsVisible);
            f.Advance(40);
            if (animation) Assert.InRange(control.TranslationY, -5.99f, -.01f);
            if (fading) Assert.InRange(f.Corner().Alpha, (byte)1, (byte)254);
            f.Advance(40);
        }
        Assert.False(popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void SharedImmediatePolicySettlesEntranceAndExit(int policy, bool changeWhileRunning)
    {
        using var f = new Fixture();
        void SetPolicy()
        {
            var value = AnimationScheduler.Default.Policy;
            if (policy == 0) value.ReducedMotion = true;
            else if (policy == 1) value.AnimationsEnabled = false;
            else value.DurationScale = 0;
        }
        if (!changeWhileRunning) SetPolicy();
        f.Tip.Show("Respect preferences", f.First);
        if (changeWhileRunning) { f.Advance(30); SetPolicy(); f.Advance(0); }
        Assert.True(f.Popup.IsVisible);
        Assert.Equal(0f, f.Control.TranslationY);
        Assert.Equal(255, f.Corner().Alpha);
        Assert.Equal(0, ActiveCount);
        f.Tip.Hide(f.First);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void ReplacingAnExitingPopupCannotBeHiddenByItsOldCompletion()
    {
        using var f = new Fixture();
        f.Tip.Show("First", f.First);
        f.Advance(120);
        var original = f.Popup;
        f.Tip.Hide(f.First);
        f.Advance(40);
        f.Tip.Show("Replacement", f.Second);
        Assert.Same(original, f.Popup);
        Assert.Equal(1, ActiveCount);
        f.Advance(40); // The old exit would finish now.
        Assert.True(original.IsVisible);
        f.Advance(80);
        Assert.Equal("Replacement", f.Control.AccessibilityObject.Name);
        Assert.True(original.IsVisible);
        Assert.Equal(255, f.Corner().Alpha);
        Assert.Equal(0, ActiveCount);
        f.Tip.Hide(f.Second);
        f.Advance(80);
        Assert.False(original.IsVisible);
    }

    [Fact]
    public void RepeatedHideDoesNotExtendTheExitDeadline()
    {
        using var f = new Fixture();
        f.Tip.Show("Deadline", f.First);
        f.Advance(120);
        f.Tip.Hide(f.First);
        f.Advance(60);
        f.Tip.Hide(f.First);
        f.Advance(20);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void AHideNotificationCanPresentAnotherAnchorWithoutLosingItsLifetime()
    {
        using var f = new Fixture(false, false);
        f.Tip.Show("First", f.First);
        bool replaced = false;
        f.Control.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId != AccessibleEvents.Hide || replaced) return;
            replaced = true;
            f.Tip.Show("Replacement", f.Second);
        };
        f.Popup.Hide();
        Assert.True(replaced);
        Assert.True(f.Popup.IsVisible);
        Assert.Equal("Replacement", f.Control.AccessibilityObject.Name);
        f.Tip.Hide(f.Second);
        Assert.False(f.Popup.IsVisible);
    }

    [Fact]
    public void AThrowingHideObserverCannotStrandTheWindowOrItsAnimation()
    {
        using var f = new Fixture();
        f.Tip.Show("Observer", f.First);
        f.Advance(120);
        var expected = new InvalidOperationException("Benign observer failure");
        bool raised = false;
        f.Control.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId == AccessibleEvents.Hide && !raised) { raised = true; throw expected; }
        };
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => f.Tip.Hide(f.First)));
        Assert.True(raised);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
        f.Tip.Show("Recovered", f.Second);
        f.Advance(120);
        Assert.True(f.Popup.IsVisible);
    }

    [Fact]
    public void DynamicHighContrastSettlesMotionAndRepaintsTheWholeBackground()
    {
        using var f = new Fixture();
        f.Tip.BackColor = SKColors.Coral;
        f.Tip.ForeColor = SKColors.Navy;
        f.Tip.Show("Contrast", f.First);
        f.Advance(30);
        f.Host.Services.Settings.SetPreferences(new PlatformColorValues
        {
            ContrastPreference = ColorContrastPreference.High,
            BackgroundColor = Color.Black, ForegroundColor = Color.White
        });
        Assert.Equal(0, ActiveCount);
        Assert.Equal(0f, f.Control.TranslationY);
        Assert.Equal(SKColors.Black, f.Popup.Window.CurrentStyle.GetBackgroundColor());
        Assert.Equal(SKColors.Black, f.Corner());
        Assert.Equal(SKColors.White, f.Control.CurrentStyle.GetForegroundColor());
        Assert.Equal(SKColors.Coral, f.Tip.BackColor);
        Assert.Equal(SKColors.Navy, f.Tip.ForeColor);
        f.Tip.Hide(f.First);
        Assert.False(f.Popup.IsVisible);
    }

    [Fact]
    public void MissingOptionalOpacitySupportUsesAnOpaqueImmediateFadeFallback()
    {
        using var f = new Fixture(false, true);
        f.Tip.Show("Capability discovery", f.First);
        f.Advance(120);
        f.Tip.Hide(f.First);
        f.Advance(80);
        f.Popup.SetPresentationSupport(false);
        f.Tip.Show("Opaque fallback", f.First);
        Assert.Equal(255, f.Corner().Alpha);
        Assert.Equal(0, ActiveCount);
        f.Tip.Hide(f.First);
        Assert.False(f.Popup.IsVisible);
    }

    [Theory]
    [InlineData(PlatformApplicationLifecycleState.Background)]
    [InlineData(PlatformApplicationLifecycleState.NoHost)]
    public void KnownUnavailableHostRetiresShowAlwaysAndDoesNotResurrectIt(PlatformApplicationLifecycleState state)
    {
        using var f = new Fixture();
        f.Tip.ShowAlways = true;
        f.Tip.Show("Bound to host", f.First);
        f.Advance(30);
        f.Host.Services.Lifecycle.SetState(state);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
        f.Host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        f.Advance(1000);
        Assert.False(f.Popup.IsVisible);
    }

    [Fact]
    public void SemanticIdentityAndAnnouncementsStayStableAcrossIntermediateFrames()
    {
        using var f = new Fixture();
        f.Tip.ToolTipTitle = "Title";
        f.Tip.Show("Explanation", f.First);
        var peer = f.Control.AccessibilityObject;
        Assert.Equal(34, (int)peer.ControlType);
        Assert.Equal(AccessibleControlType.ToolTip, peer.ControlType);
        Assert.Equal("Title. Explanation", peer.Name);
        Assert.True(peer.State.HasFlag(AccessibleStates.Invisible));
        List<AccessibleEvents> events = [];
        peer.ClientNotification += (_, e) => events.Add(e.EventId);
        f.Advance(30); f.Advance(30); f.Advance(30);
        Assert.DoesNotContain(AccessibleEvents.Show, events);
        Assert.DoesNotContain(AccessibleEvents.NameChange, events);
        f.Advance(30);
        Assert.Equal(1, events.Count(value => value == AccessibleEvents.Show));
        Assert.False(peer.State.HasFlag(AccessibleStates.Invisible));
        var identity = peer.RuntimeId;
        f.Tip.Show("Updated explanation", f.Second);
        Assert.Equal(identity, peer.RuntimeId);
        Assert.Equal("Title. Updated explanation", peer.Name);
        Assert.Equal(1, events.Count(value => value == AccessibleEvents.NameChange));
        f.Advance(1000);
        Assert.Equal(1, events.Count(value => value == AccessibleEvents.Show));
    }

    [Fact]
    public void RetainingARetiredPopupControlDoesNotRetainItsComponent()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        var (control, reference) = CreateRetiredControl(window, anchor);
        for (int index = 0; index < 3; index++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(reference.IsAlive);
        GC.KeepAlive(control);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Control, WeakReference) CreateRetiredControl(TestWindowHost window, Control anchor)
    {
        var tip = new ToolTip { UseAnimation = false, UseFading = false };
        tip.Show("Retired", anchor);
        var control = Assert.Single(Assert.Single(window.Popups).Window.Controls);
        var reference = new WeakReference(tip);
        tip.Dispose();
        return (control, reference);
    }

    private static int ActiveCount => AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;

    [Fact]
    public void SharedNativeFixtureUsesTheSameProductionPopupAndInputLeases()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        using var window = host.Show(form, 380, 440);
        List<string> output = [];
        using var fixture = new ModernFormsNext.Validation.ToolTipCertification(form, output.Add);
        fixture.Begin();
        host.Clock.Advance(TimeSpan.FromMilliseconds(120));
        fixture.VerifySettled();
        fixture.BeginExit();
        host.Clock.Advance(TimeSpan.FromMilliseconds(80));
        Assert.False(fixture.Popup.Visible);
        fixture.ExercisePassiveMenu();
        Assert.Contains(output, line => line.StartsWith("TOOLTIP:PASS:", StringComparison.Ordinal));
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void AutomaticInitialReshowAndAutopopDeadlinesUseTheExistingMonotonicClock()
    {
        using var f = new Fixture(false, false);
        f.Tip.InitialDelay = 100;
        f.Tip.ReshowDelay = 20;
        f.Tip.AutoPopDelay = 200;
        f.Tip.SetToolTip(f.First, "First hover");
        f.Tip.SetToolTip(f.Second, "Second hover");
        f.Window.Input.Move(f.First);
        f.Advance(99);
        Assert.Empty(f.Window.Popups);
        f.Advance(1);
        Assert.True(f.Popup.IsVisible);
        f.Window.Input.Move(f.Second);
        f.Advance(19);
        Assert.False(f.Popup.IsVisible);
        f.Advance(1);
        Assert.True(f.Popup.IsVisible);
        Assert.Equal("Second hover", f.Control.AccessibilityObject.Name);
        f.Advance(199);
        Assert.True(f.Popup.IsVisible);
        f.Advance(1);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void ExplicitDurationStartsDismissalAtItsDeadlineAndKeepsTheBoundedExit()
    {
        using var f = new Fixture();
        f.Tip.Show("Timed", f.First, 200);
        f.Advance(120);
        f.Advance(79);
        Assert.False(f.Control.AccessibilityObject.State.HasFlag(AccessibleStates.Invisible));
        f.Advance(1);
        Assert.True(f.Control.AccessibilityObject.State.HasFlag(AccessibleStates.Invisible));
        Assert.True(f.Popup.IsVisible);
        f.Advance(79);
        Assert.True(f.Popup.IsVisible);
        f.Advance(1);
        Assert.False(f.Popup.IsVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerInactivityCancelsPendingAutomaticShowUnlessShowAlways(bool showAlways)
    {
        using var f = new Fixture(false, false);
        f.Tip.ShowAlways = showAlways;
        f.Tip.InitialDelay = 100;
        f.Tip.SetToolTip(f.First, "Pending");
        f.Window.Input.Move(f.First);
        f.Advance(50);
        f.Window.SetActive(false);
        f.Advance(50);
        Assert.Equal(showAlways, f.Window.Popups.Any(popup => popup.IsVisible));
        Assert.False(f.Window.FormRoot!.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DesktopDeactivationDoesNotBecomeMobileBackground(bool showAlways)
    {
        using var f = new Fixture(false, false);
        f.Tip.ShowAlways = showAlways;
        f.Tip.Show("Desktop", f.First);
        f.Window.SetActive(false);
        f.Host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground,
            isActive: false, hostCount: 1, hostGeneration: 1));
        Assert.Equal(showAlways, f.Popup.IsVisible);
        Assert.False(f.Window.FormRoot!.IsActive);
        f.Tip.ShowAlways = false;
        Assert.False(f.Popup.IsVisible);
    }

    [Fact]
    public void SwitchingOwnersClosesTheOldPopupWithoutLeavingOldWork()
    {
        using var f = new Fixture();
        f.Tip.ShowAlways = true;
        f.Tip.Show("First owner", f.First);
        f.Advance(30);
        var original = f.Popup;
        var other = new Form();
        var anchor = other.Controls.Add(new Button());
        using var second = f.Host.Show(other);
        f.Tip.Show("Second owner", anchor);
        var replacement = Assert.Single(second.Popups);
        Assert.True(original.IsClosed);
        Assert.True(replacement.IsVisible);
        Assert.Equal(1, ActiveCount);
        f.Advance(120);
        Assert.True(replacement.IsVisible);
        Assert.Equal("Second owner", Assert.Single(replacement.Window.Controls).AccessibilityObject.Name);
        Assert.Equal(0, ActiveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HidingOrReparentingAManualAnchorImmediatelyRetiresItsPresentation(bool reparent)
    {
        using var f = new Fixture();
        f.Tip.Show("Anchor lifetime", f.First);
        f.Advance(30);
        using var detached = new Panel();
        if (reparent) detached.Controls.Add(f.First);
        else f.First.Visible = false;
        Assert.False(f.Popup.IsVisible);
        f.Advance(1000);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void PopupReentrancyAndCancellationDoNotOverwriteTheNewerPresentation()
    {
        using var f = new Fixture(false, false);
        bool replace = true;
        f.Tip.Popup += (_, e) =>
        {
            e.ToolTipSize = new Size(140, 42);
            if (replace) { replace = false; f.Tip.Show("Newer", f.Second); e.Cancel = true; }
        };
        f.Tip.Show("Older", f.First);
        Assert.Equal("Newer", f.Control.AccessibilityObject.Name);
        Assert.Equal(new Size(140, 42), f.Popup.Window.Size);
        f.Tip.Hide(f.Second);
        Assert.False(f.Popup.IsVisible);
    }

    [Fact]
    public void FinalizerPathDoesNotTouchManagedPresentationOnAWorker()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        using var tip = new FinalizerProbe { UseAnimation = false, UseFading = false };
        tip.Show("Still owned", anchor);
        var popup = Assert.Single(window.Popups);
        Exception? failure = null;
        var worker = new Thread(() => { try { tip.FinalizeOnly(); } catch (Exception error) { failure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.True(popup.IsVisible);
        tip.Hide(anchor);
        Assert.False(popup.IsVisible);
    }

    private sealed class FinalizerProbe : ToolTip
    {
        public void FinalizeOnly() => Dispose(false);
    }

    [Fact]
    public void FailedAnimatedShowNotificationStillRetiresTheOwnedPopup()
    {
        using var f = new Fixture();
        f.Tip.Show("Notification cleanup", f.First);
        var expected = new InvalidOperationException("Benign show observer failure");
        bool raised = false;
        f.Control.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId == AccessibleEvents.Show && !raised) { raised = true; throw expected; }
        };
        using var report = new StringWriter();
        using var listener = new TextWriterTraceListener(report);
        Trace.Listeners.Add(listener);
        try { f.Advance(120); }
        finally { Trace.Listeners.Remove(listener); }
        Assert.True(raised);
        // Production scheduler ticks isolate update/completion faults. Verify its explicit
        // diagnostic and owned cleanup, rather than expecting a synchronous Show exception.
        Assert.Contains(expected.Message, report.ToString());
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void ExplicitWorkerDisposalIsRejectedBeforeMutatingAUiOwnedComponent()
    {
        using var f = new Fixture(false, false);
        f.Tip.Show("UI owned", f.First);
        Exception? failure = null;
        var worker = new Thread(() => { try { f.Tip.Dispose(); } catch (Exception error) { failure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(f.Popup.IsVisible);
        f.Tip.Show("Still usable", f.Second);
        Assert.Equal("Still usable", f.Control.AccessibilityObject.Name);
    }

    [Fact]
    public void AFailedNotificationWhileChangingEffectsCannotStrandTheWindow()
    {
        using var f = new Fixture();
        f.Tip.Show("Settling cleanup", f.First);
        var expected = new InvalidOperationException("Benign settle observer failure");
        bool raised = false;
        f.Control.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId == AccessibleEvents.Show && !raised) { raised = true; throw expected; }
        };
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => f.Tip.UseAnimation = false));
        Assert.True(raised);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void PlatformMotionChangesCompleteAnExitWithoutWaitingForAnotherDeadline()
    {
        using var f = new Fixture();
        f.Tip.Show("Native preference", f.First);
        f.Advance(120);
        f.Tip.Hide(f.First);
        f.Advance(20);
        f.Host.Services.AnimationSettings.SetPreferences(reducedMotion: true);
        f.Advance(0);
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
        f.Tip.Show("Immediate preference", f.Second);
        Assert.Equal(255, f.Corner().Alpha);
        Assert.Equal(0f, f.Control.TranslationY);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void ShowAlwaysDoesNotOutliveAHiddenOwner()
    {
        using var f = new Fixture();
        f.Tip.ShowAlways = true;
        f.Tip.Show("Owner lifetime", f.First);
        var popup = f.Popup;
        f.Window.FormRoot!.Hide();
        Assert.False(popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    [Fact]
    public void SuspensionImmediatelyRetiresShowAlwaysWithARetainedHost()
    {
        using var f = new Fixture();
        f.Tip.ShowAlways = true;
        f.Tip.Show("Suspended", f.First);
        f.Host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Suspended, PlatformApplicationLifecycleState.Background,
            isActive: false, hostCount: 1, hostGeneration: 1));
        Assert.False(f.Popup.IsVisible);
        Assert.Equal(0, ActiveCount);
    }

    private sealed class Fixture : IDisposable
    {
        public ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        public TestWindowHost Window { get; }
        public Button First { get; }
        public Button Second { get; }
        public ToolTip Tip { get; }
        public TestPopupHost Popup => Assert.Single(Window.Popups);
        public Control Control => Assert.Single(Popup.Window.Controls);
        public Fixture(bool animation = true, bool fading = true)
        {
            var form = new Form();
            First = form.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 100, 30) });
            Second = form.Controls.Add(new Button { Bounds = new Rectangle(10, 60, 100, 30) });
            Window = Host.Show(form, 400, 300);
            Tip = new ToolTip { UseAnimation = animation, UseFading = fading, BorderRadius = 0, BorderWidth = 0 };
        }
        public void Advance(int milliseconds) => Host.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        public SKColor Corner()
        {
            using var image = Popup.CaptureRenderedSnapshot();
            return image.GetPixel(0, 0);
        }
        public void Dispose() { try { Tip.Dispose(); } finally { Host.Dispose(); } }
    }
}

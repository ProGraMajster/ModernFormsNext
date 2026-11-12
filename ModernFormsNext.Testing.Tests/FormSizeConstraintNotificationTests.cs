using System.Drawing;
using System.Reflection;
using Xunit;
using BackendSize = ModernFormsNext.WindowKit.Size;

namespace ModernFormsNext.Testing.Tests;

public sealed class FormSizeConstraintNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoercionCommitsBothLimitsBeforeNotifyingExplicitThenPairedProperty(bool minimum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        Size expected = minimum ? new(600, 450) : new(100, 90);

        Set(form, minimum, expected);

        Assert.Equal(expected, form.MinimumSize);
        Assert.Equal(expected, form.MaximumSize);
        Assert.Equal(expected, form.Size);
        Assert.Equal(minimum ? new[] { "Minimum", "Maximum" } : ["Maximum", "Minimum"], events);
        AssertBackendPair(form);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SinglePropertyAndRepeatedValuesOnlyNotifyRealChanges(bool minimum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        Size value = minimum ? new(250, 180) : new(550, 420);

        Set(form, minimum, value);
        Set(form, minimum, value);
        form.MinimumSize = form.MinimumSize;
        form.MaximumSize = form.MaximumSize;

        Assert.Equal(new[] { minimum ? "Minimum" : "Maximum" }, events);
        Assert.Equal(new Size(400, 300), form.Size);
    }

    [Fact]
    public void EmptyLimitsRemainUnboundedAndNotifyOnlyTheClearedProperty()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);

        form.MinimumSize = Size.Empty;
        Assert.Equal(new Size(500, 400), form.MaximumSize);
        form.MaximumSize = Size.Empty;
        Assert.Equal(new[] { "Minimum", "Maximum" }, events);
        form.Size = new Size(1, 1);
        Assert.Equal(new Size(1, 1), form.Size);
        form.Size = new Size(2000, 1500);
        Assert.Equal(new Size(2000, 1500), form.Size);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantConstraintChangeSupersedesTheOldPairedNotification(bool minimum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        Size outer = minimum ? new(600, 450) : new(100, 90);
        Size nested = minimum ? new(700, 500) : new(50, 40);
        var events = Observe(form);
        EventHandler redirect = (_, _) => {
            if (Get(form, minimum) == outer) Set(form, minimum, nested);
        };
        if (minimum) form.MinimumSizeChanged += redirect;
        else form.MaximumSizeChanged += redirect;

        Set(form, minimum, outer);

        Assert.Equal(nested, form.MinimumSize);
        Assert.Equal(nested, form.MaximumSize);
        Assert.Equal(minimum ? new[] { "Minimum", "Minimum", "Maximum" }
            : ["Maximum", "Maximum", "Minimum"], events);
    }

    [Fact]
    public void LaterSubscriberDoesNotReceiveAnObsoleteNotificationAfterReentry()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        List<Size> seen = [];
        form.MinimumSizeChanged += (_, _) => {
            if (form.MinimumSize.Width == 600) form.MinimumSize = new Size(700, 500);
        };
        form.MinimumSizeChanged += (_, _) => seen.Add(form.MinimumSize);

        form.MinimumSize = new Size(600, 450);

        Assert.Equal(new[] { new Size(700, 500) }, seen);
    }

    [Fact]
    public void ReturningToTheSamePairDoesNotReviveTheOldNotificationBatch()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        bool redirected = false;
        form.MinimumSizeChanged += (_, _) => {
            if (redirected) return;
            redirected = true;
            form.MinimumSize = new Size(700, 500);
            form.MaximumSize = new Size(600, 450);
        };

        form.MinimumSize = new Size(600, 450);

        Assert.Equal(new Size(600, 450), form.MinimumSize);
        Assert.Equal(form.MinimumSize, form.MaximumSize);
        Assert.Equal(new[] { "Minimum", "Minimum", "Maximum", "Maximum", "Minimum" }, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoOpReentryDoesNotSuppressThePairedNotification(bool minimum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        EventHandler noOp = (_, _) => Set(form, minimum, Get(form, minimum));
        if (minimum) form.MinimumSizeChanged += noOp;
        else form.MaximumSizeChanged += noOp;

        Set(form, minimum, minimum ? new(600, 450) : new(100, 90));

        Assert.Equal(minimum ? new[] { "Minimum", "Maximum" } : ["Maximum", "Minimum"], events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CloseOrDisposeStopsRemainingSubscribersAndPairedNotification(bool minimum, bool dispose)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        int later = 0;
        EventHandler close = (_, _) => { if (dispose) form.Dispose(); else form.Close(); };
        if (minimum) form.MinimumSizeChanged += close;
        else form.MaximumSizeChanged += close;
        form.MinimumSizeChanged += (_, _) => later++;
        form.MaximumSizeChanged += (_, _) => later++;

        Set(form, minimum, minimum ? new(600, 450) : new(100, 90));

        Assert.Equal(0, later);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingObserversPreserveThePairAndStillNotifyCurrentChanges(bool bothThrow)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var first = new InvalidOperationException("Minimum observer failed.");
        var second = new ArgumentException("Maximum observer failed.");
        var events = Observe(form);
        form.MinimumSizeChanged += (_, _) => throw first;
        form.MaximumSizeChanged += (_, _) => { if (bothThrow) throw second; };

        var failure = Record.Exception(() => form.MinimumSize = new Size(600, 450));

        if (bothThrow) Assert.Equal(new Exception[] { first, second },
            Assert.IsType<AggregateException>(failure).Flatten().InnerExceptions);
        else Assert.Same(first, failure);
        Assert.Equal(new[] { "Minimum", "Maximum" }, events);
        Assert.Equal(new Size(600, 450), form.MinimumSize);
        Assert.Equal(form.MinimumSize, form.MaximumSize);
        AssertBackendPair(form);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseOrDisposeDuringClampingStopsConstraintNotifications(bool dispose)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        form.Resize += (_, _) => { if (dispose) form.Dispose(); else form.Close(); };

        form.MaximumSize = new Size(100, 90);

        Assert.Empty(events);
    }

    [Fact]
    public void ConstraintReplacementDuringClampingSupersedesTheOuterNotifications()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        form.Resize += (_, _) => form.MaximumSize = new Size(300, 200);

        form.MaximumSize = new Size(100, 90);

        Assert.Equal(new[] { "Maximum" }, events);
        Assert.Equal(new Size(100, 90), form.MinimumSize);
        Assert.Equal(new Size(300, 200), form.MaximumSize);
    }

    [Fact]
    public void ResizeObserverFailureDoesNotHideAlreadyCommittedConstraintChanges()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = CreateForm();
        var events = Observe(form);
        var failure = new InvalidOperationException("Resize observer failed.");
        form.Resize += (_, _) => throw failure;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            form.MaximumSize = new Size(100, 90)));

        Assert.Equal(new[] { "Maximum", "Minimum" }, events);
        AssertBackendPair(form);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantProtectedHookDoesNotRepublishTheSupersededAssignment(bool minimum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new ConstraintHookForm {
            Size = new Size(400, 300), MinimumSize = new Size(200, 150), MaximumSize = new Size(500, 400)
        };
        var events = Observe(form);
        Size outer = minimum ? new(600, 450) : new(100, 90);
        Size nested = minimum ? new(700, 500) : new(50, 40);
        form.BeforeBase = changedMinimum => {
            if (changedMinimum == minimum && Get(form, minimum) == outer) Set(form, minimum, nested);
        };

        Set(form, minimum, outer);

        Assert.Equal(nested, form.MinimumSize);
        Assert.Equal(nested, form.MaximumSize);
        Assert.Equal(minimum ? new[] { "Minimum", "Maximum" } : ["Maximum", "Minimum"], events);
    }

    private sealed class ConstraintHookForm : Form
    {
        internal Action<bool>? BeforeBase { get; set; }
        protected override void OnMinimumSizeChanged(EventArgs e) { BeforeBase?.Invoke(true); base.OnMinimumSizeChanged(e); }
        protected override void OnMaximumSizeChanged(EventArgs e) { BeforeBase?.Invoke(false); base.OnMaximumSizeChanged(e); }
    }

    private static Form CreateForm() => new() {
        Size = new Size(400, 300), MinimumSize = new Size(200, 150), MaximumSize = new Size(500, 400)
    };

    private static Size Get(Form form, bool minimum) => minimum ? form.MinimumSize : form.MaximumSize;

    private static void Set(Form form, bool minimum, Size value)
    {
        if (minimum) form.MinimumSize = value;
        else form.MaximumSize = value;
    }

    private static List<string> Observe(Form form)
    {
        List<string> events = [];
        void Record(string name) { AssertBackendPair(form); events.Add(name); }
        form.MinimumSizeChanged += (_, _) => Record("Minimum");
        form.MaximumSizeChanged += (_, _) => Record("Maximum");
        return events;
    }

    private static void AssertBackendPair(Form form)
    {
        // The headless backend intentionally exposes no second public constraints API.
        // Inspect its stored pair to distinguish correct backend handoff from only updating Form.
        var backend = Assert.IsType<HeadlessWindowImpl>(typeof(WindowBase)
            .GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
        var minimum = (BackendSize)typeof(HeadlessWindowImpl)
            .GetField("minimumSize", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backend)!;
        var maximum = (BackendSize)typeof(HeadlessWindowImpl)
            .GetField("maximumSize", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backend)!;
        Assert.Equal(new BackendSize(form.MinimumSize.Width, form.MinimumSize.Height), minimum);
        Assert.Equal(new BackendSize(form.MaximumSize.Width, form.MaximumSize.Height), maximum);
    }
}

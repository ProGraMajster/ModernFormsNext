using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class HostReentrancyTests
{
    [Fact]
    public void FinalizerPathLeavesManagedBindingsCallbacksAndHostedTreeUntouched()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new FinalizerProbeForm { UseSystemDecorations = true };
        var button = form.Controls.Add(new Button { Name = "retained", Text = "Still active" });
        var inputCalls = 0;
        var commandCalls = 0;
        var commandQueries = 0;
        var windowInputs = form.InputBindings;
        var localInputs = button.InputBindings;
        windowInputs.Add(new KeyBinding(new DelegateCommand(() => inputCalls++), new KeyGesture(Keys.F2)));
        localInputs.Add(new KeyBinding(new DelegateCommand(() => inputCalls += 10), new KeyGesture(Keys.F3)));
        var command = new RoutedCommand("Retained managed route");
        var windowCommands = form.CommandBindings;
        var localCommands = button.CommandBindings;
        windowCommands.Add(new CommandBinding(command,
            (_, args) => { commandCalls += 10; args.Handled = true; },
            (_, args) => { commandQueries++; args.CanExecute = true; }));
        localCommands.Add(new CommandBinding(command,
            (_, _) => commandCalls++,
            (_, args) => { commandQueries++; args.CanExecute = true; }));
        var window = host.Show(form);
        Assert.True(window.Input.Focus(button));
        string tree = window.CaptureTree().Dump();
        var disposed = 0;
        var buttonDisposed = false;
        var closing = 0;
        var closed = 0;
        form.Disposed += (_, _) => disposed++;
        button.Disposed += (_, _) => buttonDisposed = true;
        form.Closing += (_, _) => closing++;
        form.Closed += (_, _) => closed++;

        form.SimulateFinalizer();

        Assert.Equal(0, disposed);
        Assert.Equal(0, closing);
        Assert.Equal(0, closed);
        Assert.Equal(0, inputCalls);
        Assert.Equal(0, commandCalls);
        Assert.Equal(0, commandQueries);
        Assert.Single(windowInputs);
        Assert.Single(localInputs);
        Assert.Single(windowCommands);
        Assert.Single(localCommands);
        Assert.Same(windowInputs, form.InputBindings);
        Assert.Same(localCommands, button.CommandBindings);
        Assert.Same(button, Assert.Single(form.Controls));
        Assert.False(buttonDisposed);
        Assert.False(window.IsClosed);
        Assert.Equal(tree, window.CaptureTree().Dump());
        window.Input.PressKey(Keys.F2);
        window.Input.PressKey(Keys.F3);
        command.Execute(null, button);
        Assert.Equal(11, inputCalls);
        Assert.Equal(11, commandCalls);

        window.Close();

        Assert.Equal(1, disposed);
        Assert.Equal(1, closing);
        Assert.Equal(1, closed);
        Assert.Empty(windowInputs);
        Assert.Empty(localInputs);
        Assert.Empty(windowCommands);
        Assert.Empty(localCommands);
        Assert.Throws<ObjectDisposedException>(() => form.InputBindings);
        Assert.True(buttonDisposed);
    }

    [Fact]
    public void WindowCloseAndDisposeInsideClosingDoNotRepeatCallbacks()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var window = host.Show(form);
        var calls = 0;
        form.Closing += (_, _) =>
        {
            calls++;
            Assert.True(window.IsClosed);
            window.Close();
            window.Dispose();
        };

        window.Close();

        Assert.Equal(1, calls);
        Assert.True(window.IsClosed);
        Assert.Empty(host.Windows);
        Assert.True(host.Dispatcher.CheckAccess());
        var nextWindow = host.Show(new Form());
        Assert.False(nextWindow.IsClosed);
    }

    [Fact]
    public void HostDisposeFromExplicitWindowClosingWaitsForCallbackAndWindowCleanup()
    {
        var originalDispatcher = Dispatcher.UIThread;
        var host = ModernFormsTestHost.Create();
        var form = new Form();
        var window = host.Show(form);
        var callbackCompleted = false;
        form.Closing += (_, _) =>
        {
            host.Dispose();
            host.Dispose();
            Assert.False(window.Backend.IsDisposed);
            Assert.True(host.Dispatcher.CheckAccess());
            Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
            Assert.True(host.Services.Clipboard.SetTextAsync("still alive until close returns").IsCompletedSuccessfully);
            Assert.Throws<ObjectDisposedException>(() => host.Show(new Panel()));
            callbackCompleted = true;
        };

        window.Close();

        Assert.True(callbackCompleted);
        Assert.True(window.IsClosed);
        Assert.Null(window.Backend.Input);
        Assert.Null(window.Backend.Closed);
        Assert.Same(originalDispatcher, Dispatcher.UIThread);
        Assert.Throws<ObjectDisposedException>(host.GetDiagnostics);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void HostDisposeFromHostCloseWaitsUntilAllWindowCallbacksFinish()
    {
        var host = ModernFormsTestHost.Create();
        var first = host.Show(new Form());
        var second = host.Show(new Form());
        var order = new List<string>();
        first.FormRoot!.Closing += (_, _) =>
        {
            order.Add("first");
            host.Dispose();
            host.Close();
            Assert.False(second.IsClosed);
            Assert.True(host.Dispatcher.CheckAccess());
        };
        second.FormRoot!.Closing += (_, _) =>
        {
            order.Add("second");
            host.Dispose();
            Assert.True(host.Dispatcher.CheckAccess());
        };

        host.Close();

        Assert.Equal(new[] { "first", "second" }, order);
        Assert.True(first.IsClosed);
        Assert.True(second.IsClosed);
        Assert.Throws<ObjectDisposedException>(host.GetDiagnostics);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void HostDisposeInsideAlreadyDisposingCallbackIsNoOp()
    {
        var host = ModernFormsTestHost.Create();
        var window = host.Show(new Form());
        var calls = 0;
        window.FormRoot!.Closing += (_, _) =>
        {
            calls++;
            host.Dispose();
            window.Close();
            Assert.True(host.Dispatcher.CheckAccess());
            Assert.Throws<ObjectDisposedException>(() => new Form());
        };

        host.Dispose();
        host.Dispose();

        Assert.Equal(1, calls);
        Assert.True(window.IsClosed);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void DeferredDisposalPreservesOriginalClosingFailureAfterRestoration()
    {
        var originalClipboard = AvaloniaGlobals.GetService<IClipboard>();
        var host = ModernFormsTestHost.Create();
        var window = host.Show(new Form());
        var expected = new ClosingFailure();
        window.FormRoot!.Closing += (_, _) =>
        {
            host.Dispose();
            throw expected;
        };

        Assert.Same(expected, Assert.Throws<ClosingFailure>(window.Close));

        Assert.True(window.IsClosed);
        Assert.Same(originalClipboard, AvaloniaGlobals.GetService<IClipboard>());
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void CombinedClosingClosedAndQueueFailuresDoNotPreventIndependentRestoration()
    {
        var originalDispatcher = Dispatcher.UIThread;
        var originalClipboard = AvaloniaGlobals.GetService<IClipboard>();
        var originalLifecycle = PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>();
        var host = ModernFormsTestHost.Create();
        var first = host.Show(new Form());
        var second = host.Show(new Form());
        first.FormRoot!.Closing += (_, _) =>
        {
            host.Dispose();
            Action? replenish = null;
            replenish = () => host.Dispatcher.Post(replenish!);
            host.Dispatcher.Post(replenish);
            throw new ClosingFailure();
        };
        first.FormRoot.Closed += (_, _) => throw new ClosedFailure();

        var failure = Assert.Throws<AggregateException>(first.Close);
        var failures = failure.Flatten().InnerExceptions;

        Assert.Contains(failures, item => item is ClosingFailure);
        Assert.Contains(failures, item => item is ClosedFailure);
        Assert.Contains(failures, item => item is InvalidOperationException && item.Message.Contains("exceeded 4096", StringComparison.Ordinal));
        Assert.True(first.IsClosed);
        Assert.True(second.IsClosed);
        Assert.Null(first.Backend.Closed);
        Assert.Null(first.Backend.Input);
        Assert.Same(originalDispatcher, Dispatcher.UIThread);
        Assert.Same(originalClipboard, AvaloniaGlobals.GetService<IClipboard>());
        Assert.Same(originalLifecycle, PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>());
        using var next = ModernFormsTestHost.Create();
        Assert.Equal(0, next.Dispatcher.PendingWorkCount);
    }

    [Fact]
    public void ThrowingClosedHandlerCannotRetainBackendCallbacks()
    {
        using var host = ModernFormsTestHost.Create();
        var window = host.Show(new Form());
        window.FormRoot!.Closed += (_, _) =>
        {
            window.Dispose();
            throw new ClosedFailure();
        };

        Assert.Throws<ClosedFailure>(window.Close);

        Assert.True(window.IsClosed);
        Assert.Null(window.Backend.Closed);
        Assert.Null(window.Backend.Input);
        Assert.Null(window.Backend.Paint);
        Assert.Null(window.Backend.Closing);
        Assert.Null(window.Backend.Resized);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void ResourceObserverFailureDoesNotPreventRestorationOfOtherEntriesOrScopes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var retainedKey = "retained." + suffix;
        var failingKey = "failing." + suffix;
        var otherKey = "other." + suffix;
        Application.Resources[retainedKey] = "baseline";
        var host = ModernFormsTestHost.Create();
        Application.Resources[retainedKey] = "changed";
        Application.Resources[failingKey] = "temporary";
        Application.Resources[otherKey] = "temporary";
        EventHandler<ResourceChangedEventArgs> handler = (_, args) =>
        {
            if (Equals(args.Key, failingKey) && args.ChangeKind == ResourceChangeKind.Removed)
                throw new ResourceFailure();
        };
        Application.Resources.ResourceChanged += handler;
        try
        {
            var failure = Assert.Throws<AggregateException>(host.Dispose);
            Assert.Contains(failure.Flatten().InnerExceptions, item => item is ResourceFailure);
            Assert.Equal("baseline", Application.Resources[retainedKey]);
            Assert.False(Application.Resources.ContainsKey(failingKey));
            Assert.False(Application.Resources.ContainsKey(otherKey));
            using var next = ModernFormsTestHost.Create();
            Assert.Empty(next.Windows);
        }
        finally
        {
            Application.Resources.ResourceChanged -= handler;
            host.Dispose();
            Application.Resources.Remove(retainedKey);
            Application.Resources.Remove(failingKey);
            Application.Resources.Remove(otherKey);
        }
    }

    private sealed class ClosingFailure : Exception;
    private sealed class ClosedFailure : Exception;
    private sealed class ResourceFailure : Exception;

    private sealed class FinalizerProbeForm : Form
    {
        internal void SimulateFinalizer() => base.Dispose(false);
    }
}

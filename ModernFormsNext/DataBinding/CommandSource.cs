using System.Windows.Input;

namespace ModernFormsNext.DataBinding;

// One binding behavior for all action sources. Only the subscription callback may arrive from
// another thread; all source reads, predicate evaluation and state changes occur on the UI thread.
internal sealed class CommandSource(ICommandBindingTargetProvider owner) : IDisposable
{
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private ICommand? command;
    private object? parameter;
    private Subscription? subscription;
    private int version;
    private bool disposed;

    internal ICommand? Command
    {
        get => command;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(command, value))
                return;

            subscription?.Dispose();
            subscription = null;
            command = value;
            version++;
            if (value is not null)
                subscription = new Subscription(this, value, ownerThreadId);
            Refresh();
        }
    }

    internal object? Parameter
    {
        get => parameter;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(parameter, value))
                return;

            parameter = value;
            version++;
            Refresh();
        }
    }

    internal bool CanExecute()
    {
        VerifyAccess();
        return Refresh() && owner.Enabled;
    }

    internal void Execute()
    {
        VerifyAccess();
        if (!owner.Enabled)
            return;

        // Click handlers can replace the command or parameter. Snapshot only after Click, then
        // reject the snapshot if predicate/EnabledChanged callbacks mutate the binding again.
        ICommand? current = command;
        object? currentParameter = parameter;
        int currentVersion = version;
        if (current is not null && Refresh() && currentVersion == version && owner.Enabled)
        {
            // The sealed framework helper can consume this fresh guarded evaluation. Calling
            // its public Execute would evaluate again outside our fail-closed/version guard.
            // Arbitrary ICommand implementations retain their own normal Execute contract.
            if (current is DelegateCommand delegateCommand)
                delegateCommand.ExecuteCore(currentParameter);
            else
                current.Execute(currentParameter);
        }
    }

    private bool Refresh()
    {
        if (disposed || owner.IsCommandSourceDisposed)
            return false;

        int currentVersion = version;
        bool available;
        try
        {
            available = command?.CanExecute(parameter) ?? true;
        }
        catch
        {
            // Keep the assignment and its subscription so a later notification/removal can
            // recover. Never expose a throwing predicate in the Enabled getter.
            if (!disposed && !owner.IsCommandSourceDisposed && version == currentVersion)
                owner.SetCommandEnabled(false);
            throw;
        }

        if (disposed || owner.IsCommandSourceDisposed || version != currentVersion)
            return false;

        owner.SetCommandEnabled(available);
        return available && !disposed && !owner.IsCommandSourceDisposed && version == currentVersion;
    }

    private void Requery(Subscription sender)
    {
        // Event invocation lists and dispatcher queues can outlive detach. Identity, not the
        // command's Equals implementation or event sender, identifies the current attachment.
        if (disposed || owner.IsCommandSourceDisposed || !ReferenceEquals(subscription, sender))
            return;
        VerifyAccess();
        Refresh();
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed || owner.IsCommandSourceDisposed, owner);
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Command source state must be accessed on its owning UI thread.");
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        version++;
        subscription?.Dispose();
        subscription = null;
        command = null;
        parameter = null;
    }

    private sealed class Subscription
    {
        private readonly WeakReference<CommandSource> target;
        private readonly int ownerThreadId;
        private ICommand? command;

        internal Subscription(CommandSource target, ICommand command, int ownerThreadId)
        {
            this.target = new WeakReference<CommandSource>(target);
            this.command = command;
            this.ownerThreadId = ownerThreadId;
            command.CanExecuteChanged += OnCanExecuteChanged;
        }

        internal void Dispose()
        {
            ICommand? previous = Interlocked.Exchange(ref command, null);
            if (previous is not null)
                previous.CanExecuteChanged -= OnCanExecuteChanged;
        }

        private void OnCanExecuteChanged(object? sender, EventArgs e)
        {
            if (Environment.CurrentManagedThreadId == ownerThreadId)
                Deliver();
            else
                // Do not capture the source/control/parameter in queued work. Resolve the
                // application's dispatcher here, after backend startup, instead of caching a
                // fallback dispatcher when an unattached control is constructed.
                Application.RunOnUIThread(Deliver);
        }

        private void Deliver()
        {
            if (target.TryGetTarget(out CommandSource? source))
                source.Requery(this);
            else
                Dispose();
        }
    }
}

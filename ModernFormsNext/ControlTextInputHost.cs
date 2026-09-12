using System.Drawing;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

// A borrowed text-service session over canonical selection, not another focus owner or document.
internal sealed class ControlTextInputHost(Control root, Func<Control?> selected) : IDisposable
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly List<Control> observedAncestry = [];
    private readonly Queue<string> history = new();
    private Session? session;
    private ITextInputMethod? method;
    private bool active = true;
    private bool disposed;
    private bool refreshing;
    private bool refreshPending;
    private long generation;
    private long methodVersion;
    private int treeChangeDepth;
    private Control? GetSelectedControl() => selected();

    public ITextInputClient? Client { get { Refresh(); return session; } }
    // Pure metadata: accessibility reads must not acquire or retire an IME connection.
    internal bool IsActive => active && !disposed;
    public event EventHandler? ClientChanged;

    internal bool IsCompositionEditingKey(Keys key)
    {
        if ((key & (Keys.Control | Keys.Alt | Keys.Meta | Keys.AltGraph)) != 0) return false;
        var code = key & Keys.KeyCode;
        if (code is not (Keys.Enter or Keys.Escape or Keys.Left or Keys.Right or Keys.Up or Keys.Down or
            Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Back or Keys.Delete or Keys.Tab)) return false;
        return Client?.GetState(0)?.HasComposition == true;
    }

    public TextInputDiagnostics GetDiagnostics()
    {
        Refresh();
        var state = session?.GetState(0);
        return new(generation, active && !disposed, session is not null, method is not null,
            state?.Revision ?? 0, state?.SelectionStart ?? -1, state?.SelectionEnd ?? -1,
            state?.CompositionStart ?? -1, state?.CompositionEnd ?? -1,
            state?.Options.Scope ?? TextInputScope.Text, Array.AsReadOnly(history.ToArray()));
    }

    public void Attach(ITextInputMethod? value)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ReferenceEquals(method, value)) return;
        var version = ++methodVersion;
        var previous = method;
        method = null;
        // Replacing a native borrower also revokes the client it could have retained.
        // Complete both managed retirement and native detach before reporting either failure.
        Exception? failure = null;
        try { Retire(); }
        catch (Exception error) { failure = error; }
        try {
            // Finish can synchronously reattach this very same method to a fresh session.
            if (!ReferenceEquals(method, previous)) previous?.SetClient(null);
        }
        catch (Exception error) {
            failure = failure is null ? error : new AggregateException("Text input retirement and native detach failed.", failure, error);
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        // Native callbacks can dispose this host or attach a newer method synchronously.
        if (disposed || version != methodVersion) return;
        Refresh();
        if (disposed || version != methodVersion) return;
        method = value;
        PublishNativeClient();
    }

    public bool SetKeyboardVisible(bool visible)
    {
        VerifyAccess();
        Refresh();
        return !disposed && method is not null && (!visible || session is not null) && method.SetKeyboardVisible(visible);
    }

    public void SetActive(bool value)
    {
        VerifyAccess();
        if (disposed || active == value) return;
        active = value;
        Refresh();
    }

    public void Refresh()
    {
        VerifyAccess();
        if (refreshing) { refreshPending = true; return; }
        refreshing = true;
        try
        {
            int attempts = 0;
            do
            {
                // Count every pass, including null custom clients and unchanged selection.
                if (++attempts > 32)
                    throw new InvalidOperationException("Text input focus did not settle after 32 notifications.");
                refreshPending = false;
                var owner = !disposed && active && treeChangeDepth == 0 ? selected() : null;
                if (!Available(owner)) owner = null;
                if (session is not null && ReferenceEquals(session.Owner, owner)) continue;
                if (session is null && owner is null) continue;
                if (session is null && owner?.QueryTextInputClient() is null) continue;
                Retire();
                // Finish callbacks may have changed selection. Never bind the earlier candidate.
                owner = !disposed && active && treeChangeDepth == 0 ? selected() : null;
                if (Available(owner) && owner!.QueryTextInputClient() is { } client)
                {
                    if (!CanBind(owner)) { refreshPending = true; continue; }
                    var candidate = new Session(this, owner, client, ++generation);
                    // Custom event accessors run user code. Publish only after both subscriptions
                    // and the original focus/lifetime have survived those callbacks.
                    if (!CanBind(owner))
                    {
                        candidate.Retire(finishComposition: false);
                        refreshPending = true;
                        continue;
                    }
                    session = candidate;
                    for (var ancestor = owner; ancestor is not null; ancestor = ancestor.Parent)
                    {
                        observedAncestry.Add(ancestor);
                        ancestor.ParentChanged += OnAvailabilityChanged;
                        ancestor.EnabledChanged += OnAvailabilityChanged;
                        ancestor.VisibleChanged += OnAvailabilityChanged;
                        ancestor.Disposed += OnAvailabilityChanged;
                        ancestor.Invalidated += OnGeometryChanged;
                        if (ReferenceEquals(ancestor, root)) break;
                    }
                    Record("SessionStarted");
                }
                PublishNativeClient();
                ClientChanged?.Invoke(this, EventArgs.Empty);
            } while (refreshPending);
        }
        finally { refreshing = false; }
    }

    private void PublishNativeClient()
    {
        var nativeMethod = method;
        if (nativeMethod is null) return;
        var offered = session;
        long version = methodVersion;
        try { nativeMethod.SetClient(offered); }
        catch (Exception failure)
        {
            // A failed callback may have retained the offered client before throwing. Revoke
            // that offer and make explicit reattachment retryable, without undoing a newer
            // configuration installed by reentrant application/native code.
            if (version == methodVersion && ReferenceEquals(method, nativeMethod))
            {
                method = null;
                methodVersion++;
                try { if (ReferenceEquals(session, offered)) Retire(); }
                catch (Exception cleanup) { failure = new AggregateException("Native text attachment and session cleanup failed.", failure, cleanup); }
                try { if (!ReferenceEquals(method, nativeMethod)) nativeMethod.SetClient(null); }
                catch (Exception cleanup) { failure = new AggregateException("Native text attachment and detach failed.", failure, cleanup); }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    public IDisposable? BeginTreeChange(Control subtree)
    {
        VerifyAccess();
        if (session?.Owner is { } owner && Contains(subtree, owner))
        {
            treeChangeDepth++;
            try { Retire(); }
            catch { treeChangeDepth--; throw; }
            return new TreeChange(this);
        }
        return null;
    }

    private bool Available(Control? control) => control is { IsDisposed: false, Disposing: false, Selected: true } &&
        !root.IsDisposed && !root.Disposing && control.Enabled && control.Visible && Contains(root, control);

    private bool CanBind(Control owner) => !disposed && active && treeChangeDepth == 0 &&
        Available(owner) && ReferenceEquals(selected(), owner);

    private static bool Contains(Control ancestor, Control descendant)
    {
        for (var current = descendant; current is not null; current = current.Parent)
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private void Retire()
    {
        var previous = session;
        if (previous is null) return;
        session = null;
        foreach (var ancestor in observedAncestry)
        {
            ancestor.ParentChanged -= OnAvailabilityChanged;
            ancestor.EnabledChanged -= OnAvailabilityChanged;
            ancestor.VisibleChanged -= OnAvailabilityChanged;
            ancestor.Disposed -= OnAvailabilityChanged;
            ancestor.Invalidated -= OnGeometryChanged;
        }
        observedAncestry.Clear();
        Record("SessionRetired");
        var nativeMethod = method;
        // Revoke the externally retained proxy before finish callbacks or native cancellation.
        Exception? failure = null;
        try { previous.Retire(); }
        catch (Exception error) { failure = error; }
        try {
            // A callback can attach another native method. Never clear that newer borrower.
            if (ReferenceEquals(method, nativeMethod) && session is null) nativeMethod?.SetClient(null);
        }
        catch (Exception error) {
            failure = failure is null ? error : new AggregateException("Text input retirement and native detach failed.", failure, error);
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void OnAvailabilityChanged(object? sender, EventArgs args) => Refresh();
    private void OnGeometryChanged(object? sender, EventArgs<Rectangle> args)
    {
        if (refreshing || session is null) return;
        Refresh();
        session?.NotifyStateChanged();
    }

    private TextInputState ToHost(Control owner, TextInputState state)
    {
        var rectangle = state.CaretRectangle;
        PointF[] corners = [new((float)rectangle.Left, (float)rectangle.Top),
            new((float)rectangle.Right, (float)rectangle.Top),
            new((float)rectangle.Left, (float)rectangle.Bottom),
            new((float)rectangle.Right, (float)rectangle.Bottom)];
        for (int i = 0; i < corners.Length; i++) corners[i] = ToHostPoint(owner, corners[i]);
        float left = corners.Min(point => point.X), top = corners.Min(point => point.Y);
        double? baseline = state.CaretBaseline is { } value
            ? ToHostPoint(owner, new((float)rectangle.Left, (float)value)).Y : null;
        return state.WithCaretRectangle(new(left, top, corners.Max(point => point.X) - left,
            corners.Max(point => point.Y) - top), baseline);
    }

    private PointF ToHostPoint(Control owner, PointF point)
    {
        // Control rendering/presentation helpers operate in the scaled back-buffer coordinates.
        // Text-service contracts use logical coordinates at both ends, so cross this boundary once.
        var scale = owner.ScaleFactor;
        point = new(point.X * scale.Width, point.Y * scale.Height);
        for (var current = owner; current is not null && !ReferenceEquals(current, root); current = current.Parent)
            point = current.ClientPointToParentPresentation(point);
        // ControlAdapter adds the managed window border while drawing child back buffers.
        // It is already measured in physical buffer pixels, so include it before unscaling.
        if (root is ControlAdapter adapter)
        {
            var border = adapter.ParentForm.CurrentStyle.Border;
            point = new(point.X + border.Left.GetWidth(), point.Y + border.Top.GetWidth());
        }
        float hostScale = (float)root.Scaling;
        return new(point.X / hostScale, point.Y / hostScale);
    }

    private bool PerformFallback(Control owner, TextInputAction action)
    {
        if (action is TextInputAction.Next or TextInputAction.Previous)
            return root.SelectNextControl(owner, action == TextInputAction.Next, true, true, true);
        return action == TextInputAction.Done && SetKeyboardVisible(false);
    }

    private void Record(string kind)
    {
        if (history.Count == 64) history.Dequeue();
        history.Enqueue(kind);
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Text input sessions require their owning UI thread.");
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed) return;
        disposed = true;
        methodVersion++;
        var nativeMethod = method;
        method = null;
        ClientChanged = null;
        Exception? failure = null;
        try { Retire(); }
        catch (Exception error) { failure = error; }
        try { nativeMethod?.SetClient(null); }
        catch (Exception error) {
            failure = failure is null ? error : new AggregateException("Text input disposal and native detach failed.", failure, error);
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Session : ITextInputClient
    {
        private readonly WeakReference<ControlTextInputHost> hostReference;
        private ITextInputClient? client;
        private bool editing;
        private bool notifying;
        private bool notifyPending;
        internal Control? Owner { get; private set; }
        internal long Generation { get; }

        public Session(ControlTextInputHost host, Control owner, ITextInputClient client, long generation)
        {
            hostReference = new(host);
            Owner = owner;
            this.client = client;
            Generation = generation;
            try {
                client.StateChanged += OnStateChanged;
                if (host.CanBind(owner)) client.CompositionChanged += OnCompositionChanged;
            }
            catch (Exception failure) {
                this.client = null;
                Owner = null;
                List<Exception> failures = [failure];
                Attempt(() => client.StateChanged -= OnStateChanged, failures);
                Attempt(() => client.CompositionChanged -= OnCompositionChanged, failures);
                ThrowFailures(failures);
            }
        }

        public event EventHandler? StateChanged;
        public event EventHandler<TextCompositionEventArgs>? CompositionChanged;

        private ControlTextInputHost? CurrentHost()
        {
            if (!hostReference.TryGetTarget(out var host)) return null;
            host.VerifyAccess();
            return !host.disposed && host.active && host.treeChangeDepth == 0 && ReferenceEquals(host.session, this) &&
                host.Available(Owner) && ReferenceEquals(host.GetSelectedControl(), Owner) ? host : null;
        }

        public TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null)
        {
            if (maximumTextLength < 0 || maximumTextLength > TextInputState.MaximumTextLength)
                throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
            if (textStart < 0) throw new ArgumentOutOfRangeException(nameof(textStart));
            if (CurrentHost() is not { } host || client is null) return null;
            var state = client.GetState(maximumTextLength, textStart);
            if (state is null || CurrentHost() is null || Owner is null) return null;
            if (state.Text.Length > maximumTextLength)
                throw new InvalidOperationException("The text client exceeded the requested surrounding-text budget.");
            return host.ToHost(Owner, state);
        }

        private bool Edit(Func<ITextInputClient, bool> operation)
        {
            if (editing || CurrentHost() is null || client is null) return false;
            editing = true;
            bool result = false;
            Exception? failure = null;
            try { result = operation(client); }
            catch (Exception error) { failure = error; }
            finally { editing = false; }
            try { if (notifyPending) NotifyStateChanged(); }
            catch (Exception notificationFailure) {
                if (failure is not null)
                    throw new AggregateException("Text input and state notification both failed.", failure, notificationFailure);
                throw;
            }
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        public bool CommitText(string text, int newCursorPosition = 1) => Edit(value => value.CommitText(text, newCursorPosition));
        public bool SetComposingText(string text, int newCursorPosition = 1) => Edit(value => value.SetComposingText(text, newCursorPosition));
        public bool SetComposingRegion(int start, int end) => Edit(value => value.SetComposingRegion(start, end));
        public bool SetSelection(int start, int end) => Edit(value => value.SetSelection(start, end));
        public bool FinishComposition() => Edit(value => value.FinishComposition());
        public bool CancelComposition() => Edit(value => value.CancelComposition());
        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false)
            => Edit(value => value.DeleteSurroundingText(beforeLength, afterLength, inCodePoints));
        public bool PerformEditorAction(TextInputAction action) => Edit(value =>
        {
            if (value.PerformEditorAction(action)) return true;
            return CurrentHost() is { } host && Owner is { } owner && host.PerformFallback(owner, action);
        });

        private void OnStateChanged(object? sender, EventArgs args) => NotifyStateChanged();
        internal void NotifyStateChanged()
        {
            if (CurrentHost() is null) return;
            if (editing) { notifyPending = true; return; }
            if (notifying) { notifyPending = true; return; }
            notifying = true;
            try
            {
                int iterations = 0;
                do
                {
                    notifyPending = false;
                    var failures = new List<Exception>();
                    if (StateChanged is { } handlers)
                        foreach (EventHandler handler in handlers.GetInvocationList())
                            Attempt(() => handler(this, EventArgs.Empty), failures);
                    ThrowFailures(failures);
                    if (++iterations >= 32 && notifyPending)
                        throw new InvalidOperationException("Text input state did not settle after 32 notifications.");
                } while (notifyPending && CurrentHost() is not null);
            }
            finally { notifying = false; }
        }

        private void OnCompositionChanged(object? sender, TextCompositionEventArgs args)
        {
            if (CurrentHost() is not { } host) return;
            host.Record("Composition" + args.Stage);
            CompositionChanged?.Invoke(this, args);
        }

        internal void Retire(bool finishComposition = true)
        {
            var previousClient = client;
            var owner = Owner;
            client = null;
            Owner = null;
            StateChanged = null;
            CompositionChanged = null;
            if (previousClient is null) return;
            var failures = new List<Exception>();
            Attempt(() => previousClient.StateChanged -= OnStateChanged, failures);
            Attempt(() => previousClient.CompositionChanged -= OnCompositionChanged, failures);
            if (finishComposition && owner is { IsDisposed: false }) Attempt(() => previousClient.FinishComposition(), failures);
            ThrowFailures(failures);
        }

        private static void Attempt(Action action, List<Exception> failures)
        {
            try { action(); }
            catch (Exception failure) { failures.Add(failure); }
        }

        private static void ThrowFailures(List<Exception> failures)
        {
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Text input client callbacks failed.", failures);
        }
    }

    private sealed class TreeChange(ControlTextInputHost host) : IDisposable
    {
        private ControlTextInputHost? owner = host;
        public void Dispose()
        {
            var current = owner;
            if (current is null) return;
            owner = null;
            current.treeChangeDepth--;
            current.Refresh();
        }
    }
}

internal interface IControlTextInputRoot
{
    ControlTextInputHost? TextInputHost { get; }
}

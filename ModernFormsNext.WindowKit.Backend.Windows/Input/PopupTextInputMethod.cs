using System.Runtime.ExceptionServices;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;

// Nonactivating popups keep the owner's keyboard HWND. This lease changes only the
// offered text client and its coordinate system; shared WindowBase routes raw input.
internal sealed class PopupTextInputMethod(Imm32TextInputMethod parent, Func<Rect, Rect?> transform) : ITextInputMethod, IDisposable
{
    private Imm32TextInputMethod? parent = parent;
    private Func<Rect, Rect?>? transform = transform;
    private Lease? lease;
    private long generation;
    private readonly int threadId = Environment.CurrentManagedThreadId;

    public void SetClient(ITextInputClient? client)
    {
        VerifyAccess();
        if (parent is null)
        {
            if (client is not null) throw new ObjectDisposedException(nameof(PopupTextInputMethod));
            return;
        }
        if (ReferenceEquals(lease?.Client, client)) { Refresh(); return; }
        var nativeParent = parent;
        long observed = ++generation;
        Lease? old = lease;
        lease = null;
        List<Exception>? errors = null;
        if (old is not null)
        {
            Attempt(ref errors, () => nativeParent.ReleaseClient(old));
            Attempt(ref errors, old.Dispose);
        }
        if (parent is not null && observed == generation && client is not null)
        {
            Lease offered = new(client, transform!);
            lease = offered;
            Attempt(ref errors, () => nativeParent.SetClient(offered));
        }
        Throw(errors);
    }

    public bool SetKeyboardVisible(bool visible)
    {
        VerifyAccess();
        return parent?.SetKeyboardVisible(visible) ?? false;
    }

    internal void Refresh()
    {
        VerifyAccess();
        if (lease is not null) parent?.Refresh();
    }

    public void Dispose()
    {
        VerifyAccess();
        var nativeParent = parent;
        if (nativeParent is null) return;
        ++generation;
        Lease? old = lease;
        lease = null;
        parent = null;
        transform = null;
        if (old is null) return;
        try { nativeParent.ReleaseClient(old); }
        finally { old.Dispose(); }
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Popup text input requires its owning UI thread.");
    }

    internal sealed class Lease : ITextInputClient, IDisposable
    {
        internal ITextInputClient? Client { get; private set; }
        private Func<Rect, Rect?>? transform;
        public event EventHandler? StateChanged;
        public event EventHandler<TextCompositionEventArgs>? CompositionChanged;

        internal Lease(ITextInputClient client, Func<Rect, Rect?> transform)
        {
            Client = client;
            this.transform = transform;
            try
            {
                client.StateChanged += OnStateChanged;
                client.CompositionChanged += OnCompositionChanged;
            }
            catch (Exception exception)
            {
                // A custom event accessor can throw after attaching its delegate. No
                // partially constructed lease may retain the popup or borrowed client.
                Client = null;
                this.transform = null;
                List<Exception>? errors = [exception];
                Attempt(ref errors, () => client.StateChanged -= OnStateChanged);
                Attempt(ref errors, () => client.CompositionChanged -= OnCompositionChanged);
                Throw(errors);
            }
        }

        public TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null)
        {
            var current = Client;
            var state = current?.GetState(maximumTextLength, textStart);
            if (state is null || !ReferenceEquals(current, Client) || transform is null) return null;
            var map = transform;
            Rect? rectangle = map(state.CaretRectangle);
            if (rectangle is not { } value || !ReferenceEquals(current, Client)) return null;
            double? baseline = null;
            if (state.CaretBaseline is { } sourceBaseline)
            {
                Rect? mappedBaseline = map(new Rect(state.CaretRectangle.X, sourceBaseline, 0, 0));
                if (mappedBaseline is null || !ReferenceEquals(current, Client)) return null;
                baseline = mappedBaseline.Value.Y;
            }
            return state.WithCaretRectangle(value, baseline);
        }

        public bool CommitText(string text, int newCursorPosition = 1) => Client?.CommitText(text, newCursorPosition) ?? false;
        public bool SetComposingText(string text, int newCursorPosition = 1) => Client?.SetComposingText(text, newCursorPosition) ?? false;
        public bool SetComposingRegion(int start, int end) => Client?.SetComposingRegion(start, end) ?? false;
        public bool SetSelection(int start, int end) => Client?.SetSelection(start, end) ?? false;
        public bool FinishComposition() => Client?.FinishComposition() ?? false;
        public bool CancelComposition() => Client?.CancelComposition() ?? false;
        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false) => Client?.DeleteSurroundingText(beforeLength, afterLength, inCodePoints) ?? false;
        public bool PerformEditorAction(TextInputAction action) => Client?.PerformEditorAction(action) ?? false;

        private void OnStateChanged(object? sender, EventArgs args)
        {
            if (Client is not null) StateChanged?.Invoke(this, args);
        }

        private void OnCompositionChanged(object? sender, TextCompositionEventArgs args)
        {
            if (Client is not null) CompositionChanged?.Invoke(this, args);
        }

        public void Dispose()
        {
            var old = Client;
            Client = null;
            transform = null;
            StateChanged = null;
            CompositionChanged = null;
            if (old is null) return;
            try { old.StateChanged -= OnStateChanged; }
            finally { old.CompositionChanged -= OnCompositionChanged; }
        }
    }

    private static void Attempt(ref List<Exception>? errors, Action action)
    {
        try { action(); }
        catch (Exception exception) { (errors ??= new()).Add(exception); }
    }

    private static void Throw(List<Exception>? errors)
    {
        if (errors is null) return;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        throw new AggregateException(errors);
    }
}

using Android.Views;
using Android.Widget;
using ModernFormsNext.WindowKit.Platform;
using NativeView = Android.Views.View;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal sealed class AndroidPresentationNativeViews(FrameLayout root, NativeView framework, Func<bool> current, Func<bool> visible, Func<bool> enabled)
    : INativeViewHostProvider, IDisposable
{
    private readonly FrameLayout presentationRoot = root;
    private readonly NativeView frameworkView = framework;
    private readonly List<Session> sessions = [];
    private bool disposed;
    public NativeViewCapabilities Capabilities => NativeViewCapabilities.Baseline;
    public bool IsAvailable => !disposed && current() && presentationRoot.IsAttachedToWindow;
    public bool IsVisible => IsAvailable && visible();
    public bool IsEnabled => IsAvailable && enabled();
    // The stable Android window feature publishes presentation replacement, not this root.
    public event Action? Changed { add { } remove { } }
    public void FocusFramework() { if (IsAvailable) frameworkView.RequestFocus(); }
    public INativeViewSession CreateSession(INativeViewFactory factory, INativeViewHostCallbacks callbacks)
    {
        if (!IsAvailable) throw new InvalidOperationException("The Android presentation is unavailable.");
        var session = new Session(this, factory, callbacks);
        sessions.Add(session);
        return session;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Windowing.AndroidWindowingPlatform.Complete(sessions.ToArray().Select(s => (Action)s.Dispose));
    }

    private sealed class Session : INativeViewSession
    {
        private readonly AndroidPresentationNativeViews owner;
        private readonly INativeViewHostCallbacks callbacks;
        private readonly HostContainer container;
        private readonly AndroidNativeViewSite site;
        private readonly FrameLayout.LayoutParams parameters = new(1, 1);
        private ViewTreeObserver? observer;
        private INativeViewPeer? peer;
        private NativeViewPlacement? last;
        private bool disposed, focusTransaction;
        private readonly Dictionary<NativeView, bool> disabledStates = [];
        private INativeViewSession? lastAbove;
        private bool ordered;
        internal Session(AndroidPresentationNativeViews owner, INativeViewFactory factory, INativeViewHostCallbacks callbacks)
        {
            this.owner = owner; this.callbacks = callbacks;
            container = new HostContainer(owner.presentationRoot.Context!, this)
            { Visibility = ViewStates.Invisible, Focusable = true, FocusableInTouchMode = true };
            container.SetClipChildren(true);
            site = new(container, callbacks, () => !disposed && owner.IsAvailable);
            try
            {
                owner.presentationRoot.AddView(container, parameters);
                peer = factory.CreatePeer(site) ?? throw new InvalidOperationException("The factory returned no peer lease.");
                observer = owner.presentationRoot.ViewTreeObserver;
                if (observer is not null) observer.GlobalFocusChange += Focus;
            }
            catch { Dispose(); throw; }
        }
        private bool Contains(NativeView? view)
        {
            for (var current = view; current is not null; current = current.Parent as NativeView)
                if (Equals(current, container)) return true;
            return false;
        }
        private void Focus(object? sender, ViewTreeObserver.GlobalFocusChangeEventArgs e)
        {
            if (disposed || !site.IsCurrent || focusTransaction || !Contains(e.NewFocus)) return;
            focusTransaction = true;
            try { if (!site.TryFocus()) callbacks.RestoreFocus(); }
            finally { focusTransaction = false; }
            // Native loss deliberately never clears the shared owner. A newer native/shared
            // gain commits through its own canonical transaction, protected by the site epoch.
        }
        private bool EnterPointer()
        {
            if (!site.IsCurrent || last is not { Visible: true, Enabled: true } || !site.TryFocus()) return false;
            // Android buttons need not be focusable in touch mode. Keep their keyboard
            // boundary native instead of leaving focus on the previous Skia input surface.
            RequestFocus();
            return site.IsCurrent && last is { Visible: true, Enabled: true };
        }
        private bool PointerInsideClip(float x, float y) => site.IsCurrent && last is { Visible: true } placement &&
            x >= placement.PixelClip.X && y >= placement.PixelClip.Y &&
            x < placement.PixelClip.Right && y < placement.PixelClip.Bottom;
        private bool Tab(bool forward)
        {
            if (!site.IsCurrent) return true;
            if (!(peer?.TryMoveFocus(forward) ?? false)) site.MoveFocus(forward);
            return true;
        }
        public void Update(NativeViewPlacement placement)
        {
            if (disposed || !site.IsCurrent) return;
            var old = last; last = placement;
            if (old?.PixelBounds != placement.PixelBounds)
            {
                var b = placement.PixelBounds;
                parameters.Width = b.Width; parameters.Height = b.Height;
                parameters.LeftMargin = b.X; parameters.TopMargin = b.Y;
                container.LayoutParameters = parameters;
                if (old?.PixelBounds.Size != b.Size) peer?.Resize(b.Size);
                if (disposed || !site.IsCurrent) return;
            }
            if (old?.PixelClip != placement.PixelClip)
            {
                var c = placement.PixelClip;
                using var clip = new global::Android.Graphics.Rect(c.X, c.Y, c.Right, c.Bottom);
                container.ClipBounds = clip;
            }
            if (disposed || !site.IsCurrent) return;
            if (old?.Enabled != placement.Enabled)
            {
                container.Enabled = placement.Enabled;
                SetEnabled(container, placement.Enabled);
            }
            if (disposed || !site.IsCurrent) return;
            if (old?.Visible != placement.Visible)
                container.Visibility = placement.Visible ? ViewStates.Visible : ViewStates.Invisible;
        }
        // Android ViewGroup.Enabled does not propagate to descendants.
        private void SetEnabled(ViewGroup group, bool enabled)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                var child = group.GetChildAt(i)!;
                if (!enabled)
                {
                    disabledStates.TryAdd(child, child.Enabled);
                    child.Enabled = false;
                }
                else if (disabledStates.Remove(child, out bool original)) child.Enabled = original;
                if (child is ViewGroup nested) SetEnabled(nested, enabled);
            }
        }
        public void RequestFocus()
        {
            if (!site.IsCurrent || !callbacks.IsFocused || last is not { Visible: true, Enabled: true } || focusTransaction) return;
            focusTransaction = true;
            try
            {
                peer?.RequestFocus();
                if (site.IsCurrent && callbacks.IsFocused && !container.HasFocus) container.RequestFocus();
            }
            finally { focusTransaction = false; }
        }
        public void ReturnFocus() { if (!disposed && Contains(container.FindFocus())) owner.FocusFramework(); }
        public void PlaceAbove(INativeViewSession? previous)
        {
            if (disposed || (ordered && ReferenceEquals(lastAbove, previous))) return;
            ordered = true;
            lastAbove = previous;
            // Shared ordering invokes foremost to backmost. Android index is back-to-front.
            int index = previous is Session prior ? owner.presentationRoot.IndexOfChild(prior.container) : owner.presentationRoot.ChildCount;
            if (owner.presentationRoot.IndexOfChild(container) < index) index--;
            owner.presentationRoot.RemoveView(container);
            if (disposed || !site.IsCurrent) return;
            owner.presentationRoot.AddView(container, Math.Clamp(index, 1, owner.presentationRoot.ChildCount), parameters);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.sessions.Remove(this);
            if (observer?.IsAlive == true) observer.GlobalFocusChange -= Focus;
            observer = null;
            var previous = peer; peer = null;
            Windowing.AndroidWindowingPlatform.Complete([
                () => SetEnabled(container, true),
                () => disabledStates.Clear(),
                () => previous?.Dispose(),
                () => owner.presentationRoot.RemoveView(container),
                container.Dispose,
                parameters.Dispose
            ]);
        }
        private sealed class HostContainer(global::Android.Content.Context context, Session session) : FrameLayout(context)
        {
            public override bool DispatchTouchEvent(MotionEvent? e)
            {
                // ClipBounds clips drawing, not Android hit testing. Reject a new gesture in
                // clipped-out airspace so the existing sibling Skia surface can receive it.
                // Once accepted, native gesture capture continues across clip boundaries.
                if (e?.ActionMasked == MotionEventActions.Down && !session.PointerInsideClip(e.GetX(), e.GetY())) return false;
                return base.DispatchTouchEvent(e);
            }
            public override bool OnInterceptTouchEvent(MotionEvent? e)
            {
                if (e?.ActionMasked == MotionEventActions.Down && !session.EnterPointer()) return true;
                return base.OnInterceptTouchEvent(e);
            }
            public override bool OnTouchEvent(MotionEvent? e) => true;
            public override bool DispatchKeyEvent(KeyEvent? e)
            {
                if (e?.KeyCode == Keycode.Tab)
                    return e.Action != KeyEventActions.Down || session.Tab(!e.IsShiftPressed);
                return base.DispatchKeyEvent(e);
            }
        }
    }
}

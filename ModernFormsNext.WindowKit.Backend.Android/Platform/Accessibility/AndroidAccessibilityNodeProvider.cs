using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using NativeAction = Android.Views.Accessibility.Action;
using NativeRect = Android.Graphics.Rect;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>
/// Owns Android's virtual descendant boundary for one Skia host. Native node wrappers are created
/// for individual requests and transferred to Android; neither the session nor host caches them.
/// </summary>
internal sealed class AndroidAccessibilityNodeProvider : AccessibilityNodeProvider
{
    private readonly AndroidSkiaHostView host;
    private readonly AndroidAccessibilitySession session;
    private readonly AndroidAccessibilityDispatch dispatch;
    private bool eventPosted;
    private bool disposed;
    private int generation;

    internal AndroidAccessibilityNodeProvider(AndroidSkiaHostView host, IPlatformAccessibilityHost semantics)
    {
        this.host = host;
        session = new(semantics);
        dispatch = new(new AndroidMainThreadDispatcher(),
            () => AndroidLogger.Write("Accessibility callback failed; request ignored."));
        session.EventsPending += ScheduleEvents;
    }

    internal void Attach() => dispatch.Run(() => { session.Attach(); return true; }, false);

    internal void InvalidateGeometry() => dispatch.Run(() => { session.InvalidateGeometry(); return true; }, false);

    internal void Detach() => dispatch.Run(() =>
    {
        generation++;
        eventPosted = false;
        session.Detach();
        return true;
    }, false);

    public override AccessibilityNodeInfo? CreateAccessibilityNodeInfo(int virtualViewId)
        => dispatch.Run(() => CreateNode(virtualViewId), null);

    private AccessibilityNodeInfo? CreateNode(int id)
    {
        if (disposed || session.Find(id) is not { } node) return null;
        AccessibilityNodeInfo info = ObtainNode(id);
        try
        {
            bool root = id == AndroidAccessibilitySession.HostId;
            if (root)
            {
                host.InitializeAccessibilityHostNode(info);
                // Native View defaults can advertise clear focus and other actions which do
                // not have a canonical equivalent. Publish only the supported mapping below.
                foreach (var action in info.ActionList?.ToArray() ?? []) info.RemoveAction(action);
            }
            else
            {
                int parentId = node.Parent is { } parent ? session.Register(parent) : AndroidAccessibilitySession.InvalidId;
                if (parentId == AndroidAccessibilitySession.InvalidId) { info.Dispose(); return null; }
                info.SetParent(host, parentId);
            }
            info.SetSource(host, id);
            info.PackageName = host.Context?.PackageName ?? string.Empty;
            var properties = Read(node);
            info.ClassName = root ? "android.view.ViewGroup" : properties.ClassName;
            info.Enabled = host.Enabled && properties.Enabled;
            info.Focusable = properties.Focusable;
            info.Focused = properties.Focused && host.HasFocus;
            info.Selected = properties.Selected;
            info.Checkable = properties.Checkable;
            if (OperatingSystem.IsAndroidVersionAtLeast(36))
                info.CheckedState = (CheckedState)((node.State & Mixed) != 0 ? 2 : properties.Checked ? 1 : 0);
            else
                info.Checked = properties.Checked;
            info.Editable = properties.Editable;
            info.Password = properties.Password;
            info.Text = properties.Text;
            // Edit labels must not replace entered text. API 26+ exposes the label as HintText;
            // older Android receives the label separately as ContentDescription.
            info.ContentDescription = node.GetControlType() == 5 ? null
                : node.GetControlType() == 10 && OperatingSystem.IsAndroidVersionAtLeast(26) ? null : properties.Label;
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && node.GetControlType() == 10)
                info.HintText = properties.Label;
            if (OperatingSystem.IsAndroidVersionAtLeast(24)) info.ImportantForAccessibility = properties.Important;
            if (OperatingSystem.IsAndroidVersionAtLeast(28)) info.ScreenReaderFocusable = properties.Important
                && (!string.IsNullOrEmpty(properties.Label) || properties.Focusable);
            if (OperatingSystem.IsAndroidVersionAtLeast(30)) info.StateDescription = properties.StateDescription;
            if (OperatingSystem.IsAndroidVersionAtLeast(34)) info.AccessibilityDataSensitive = properties.Password;
            // Extras keep supplemental metadata distinct from the spoken label and current text.
            // Values are never copied here. AutomationId is metadata, not an Android resource ID.
            info.Extras?.PutString("ModernFormsNext.Help", properties.Help);
            info.Extras?.PutString("ModernFormsNext.AutomationId", node.GetAutomationId());
            if (!OperatingSystem.IsAndroidVersionAtLeast(30) && properties.StateDescription is { } state)
                info.Extras?.PutString("androidx.view.accessibility.AccessibilityNodeInfoCompat.STATE_DESCRIPTION_KEY", state);

            var geometry = Geometry(node);
            using var screen = ToNative(geometry.Screen);
            info.SetBoundsInScreen(screen);
            Rect parentBounds = root || node.Parent is not { } semanticParent ? default : semanticParent.Bounds;
            Rect bounds = node.Bounds;
            using var relative = ToNative(AndroidAccessibilityBounds.ToScreen(
                new(bounds.X - parentBounds.X, bounds.Y - parentBounds.Y, bounds.Width, bounds.Height), host.Density, 0, 0));
            // Android deprecated parent bounds in API 29, but older services still need them.
#pragma warning disable CA1422
            info.SetBoundsInParent(relative);
#pragma warning restore CA1422
            info.VisibleToUser = geometry.Visible;
            info.AccessibilityFocused = session.AccessibilityFocusId == id;

            var actions = Actions(node);
            info.Clickable = actions.Contains(ActionClick);
            info.LongClickable = false;
            info.Scrollable = actions.Contains(ActionScrollForward) || actions.Contains(ActionScrollBackward);
            foreach (int action in actions)
            {
                if (action == ActionSetProgress && !OperatingSystem.IsAndroidVersionAtLeast(24)) continue;
                using var nativeAction = new AccessibilityNodeInfo.AccessibilityAction(action, (string?)null);
                info.AddAction(nativeAction);
            }
            if (geometry.Visible || info.AccessibilityFocused)
            {
                using var focus = new AccessibilityNodeInfo.AccessibilityAction(
                    info.AccessibilityFocused ? ActionClearAccessibilityFocus : ActionAccessibilityFocus, (string?)null);
                info.AddAction(focus);
            }
            foreach (int child in session.Children(node)) info.AddChild(host, child);
            if (properties.Range is { } range)
            {
#pragma warning disable CA1422
                using var nativeRange = AccessibilityNodeInfo.RangeInfo.Obtain(RangeType.Float,
                    (float)range.Minimum, (float)range.Maximum, (float)range.Value);
#pragma warning restore CA1422
                info.SetRangeInfo(nativeRange);
            }
            if (Collection(node) is { } collection)
            {
#pragma warning disable CA1422
                using var nativeCollection = AccessibilityNodeInfo.CollectionInfo.Obtain(collection.Rows,
                    collection.Columns, false, (SelectionMode)collection.SelectionMode);
#pragma warning restore CA1422
                info.SetCollectionInfo(nativeCollection);
            }
            else if (node.GetControlType() == 13 && node.Parent is { } list && Collection(list) is not null)
            {
                for (int i = 0, count = list.GetChildCount(); i < count; i++)
                {
                    if (!ReferenceEquals(list.GetChild(i), node)) continue;
#pragma warning disable CA1422
                    using var item = AccessibilityNodeInfo.CollectionItemInfo.Obtain(i, 1, 0, 1, false, properties.Selected);
#pragma warning restore CA1422
                    info.SetCollectionItemInfo(item);
                    break;
                }
            }
            return info;
        }
        catch
        {
            info.Dispose();
            throw; // The dispatcher boundary converts failure to null without logging metadata.
        }
    }

    private AccessibilityNodeInfo ObtainNode(int id)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) return new AccessibilityNodeInfo(host, id);
#pragma warning disable CA1422
        return AccessibilityNodeInfo.Obtain(host, id)!;
#pragma warning restore CA1422
    }

    public override bool PerformAction(int virtualViewId, NativeAction action, Bundle? arguments)
        => dispatch.Run(() =>
        {
            if (disposed || session.Find(virtualViewId) is not { } node) return false;
            int actionId = (int)action;
            object? parameter = null;
            if (actionId == ActionSetText)
            {
                const string key = "ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE";
                if (arguments?.ContainsKey(key) != true) return false;
                parameter = arguments.GetCharSequence(key);
                if (parameter is not string) return false;
            }
            else if (actionId == ActionSetProgress)
            {
                const string key = "android.view.accessibility.action.ARGUMENT_PROGRESS_VALUE";
                if (!OperatingSystem.IsAndroidVersionAtLeast(24) || arguments?.ContainsKey(key) != true) return false;
                parameter = (double)arguments.GetFloat(key, float.NaN);
            }
            // Android services add routing metadata even to argument-free actions such as Click.
            // Only documented action arguments become canonical parameters; ignore native extras.
            if (actionId == ActionFocus && (!Actions(node).Contains(actionId) || !host.RequestFocus())) return false;
            bool performed = session.Perform(virtualViewId, actionId, parameter, Geometry(node).Visible);
            // ViewRootImpl tracks the focused virtual descendant from this event. Deliver focus
            // synchronously so a service's immediately following FindFocus sees the new target.
            if (performed && actionId is ActionAccessibilityFocus or ActionClearAccessibilityFocus)
                SendEvents();
            return performed;
        }, false);

    public override AccessibilityNodeInfo? FindFocus(NodeFocus focus)
        => dispatch.Run(() => disposed || focus == NodeFocus.Input && !host.HasFocus ? null
            : CreateNode(session.FindFocus(focus == NodeFocus.Accessibility)), null);

    public override IList<AccessibilityNodeInfo>? FindAccessibilityNodeInfosByText(string? text, int virtualViewId)
        => dispatch.Run<IList<AccessibilityNodeInfo>?>(() =>
        {
            List<AccessibilityNodeInfo> result = [];
            if (disposed || string.IsNullOrWhiteSpace(text) || session.Find(virtualViewId) is not { } start) return result;
            HashSet<long> visited = [];
            Stack<IPlatformAccessibleObject> remaining = new();
            remaining.Push(start);
            try
            {
                // A text search explicitly requires traversal; ordinary ID queries never do this.
                while (remaining.TryPop(out var node) && visited.Count < 10000 && result.Count < 100)
                {
                    if (!visited.Add(node.GetRuntimeId()) || node.GetAccessibilityView() == 4) continue;
                    var properties = Read(node);
                    if ((properties.Label?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
                            || properties.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        && CreateNode(session.Register(node)) is { } info) result.Add(info);
                    for (int i = node.GetChildCount() - 1; i >= 0; i--)
                        if (node.GetChild(i) is { } child) remaining.Push(child);
                }
                return result;
            }
            catch
            {
                foreach (var info in result) info.Dispose();
                throw;
            }
        }, null);

    internal bool DispatchHover(MotionEvent? motion)
        => dispatch.Run(() =>
        {
            if (disposed || motion is null || host.Context?.GetSystemService(Context.AccessibilityService)
                is not AccessibilityManager { IsEnabled: true, IsTouchExplorationEnabled: true }) return false;
            int id;
            if (motion.ActionMasked == MotionEventActions.HoverExit) id = AndroidAccessibilitySession.InvalidId;
            else if (motion.ActionMasked is MotionEventActions.HoverEnter or MotionEventActions.HoverMove)
                id = session.HitTest((int)(motion.GetX() / host.Density), (int)(motion.GetY() / host.Density), Viewport());
            else return false;
            bool handled = id != AndroidAccessibilitySession.InvalidId || session.HoveredId != AndroidAccessibilitySession.InvalidId;
            session.Hover(id);
            return handled;
        }, false);

    private Rect Viewport()
    {
        using var visible = new NativeRect();
        if (!host.IsShown || !host.GetLocalVisibleRect(visible)) return default;
        for (View? ancestor = host; ancestor is not null; ancestor = ancestor.Parent as View)
            if (ancestor.Alpha <= 0) return default;
        double density = host.Density;
        return new(visible.Left / density, visible.Top / density, visible.Width() / density, visible.Height() / density);
    }

    private (Rect Screen, bool Visible) Geometry(IPlatformAccessibleObject node)
    {
        if (session.Root is not { } root) return default;
        Rect clipped = AndroidAccessibilityBounds.Clip(node, root, Viewport());
        int[] location = new int[2];
        host.GetLocationOnScreen(location);
        Rect screen = AndroidAccessibilityBounds.ToScreen(clipped, host.Density, location[0], location[1]);
        return (screen, host.IsAttachedToWindow && host.WindowVisibility == ViewStates.Visible
            && host.Alpha > 0 && AndroidAccessibilityBounds.Valid(screen));
    }

    private static NativeRect ToNative(Rect bounds) => new((int)bounds.X, (int)bounds.Y, (int)bounds.Right, (int)bounds.Bottom);

    private void ScheduleEvents()
    {
        if (disposed || eventPosted) return;
        eventPosted = true;
        int postedGeneration = generation;
        if (!host.PostDelayed(() =>
        {
            if (disposed || generation != postedGeneration) return;
            eventPosted = false;
            dispatch.Run(() => { SendEvents(); return true; }, false);
        }, 50)) eventPosted = false;
    }

    private void SendEvents()
    {
        var events = session.DrainEvents();
        if (host.Context?.GetSystemService(Context.AccessibilityService) is not AccessibilityManager { IsEnabled: true }) return;
        foreach (var change in events)
        {
            var node = session.Find(change.Id);
            if (node is null) continue;
#pragma warning disable CA1422
            using var nativeEvent = AccessibilityEvent.Obtain((EventTypes)change.Type)!;
#pragma warning restore CA1422
            nativeEvent.SetSource(host, change.Id);
            nativeEvent.PackageName = host.Context?.PackageName;
            nativeEvent.ClassName = ClassName(node.GetControlType());
            nativeEvent.Enabled = (node.State & Unavailable) == 0;
            nativeEvent.Password = node.GetIsSensitive() || (node.State & Protected) != 0;
            nativeEvent.ContentChangeTypes = (ContentChangeTypes)change.Changes;
            // Deliberately no Text, BeforeText, ContentDescription or event extras: services
            // query fresh nodes, and sensitive input cannot escape through a stale event payload.
            host.Parent?.RequestSendAccessibilityEvent(host, nativeEvent);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            Detach();
            session.Dispose();
            disposed = true;
        }
        base.Dispose(disposing);
    }
}

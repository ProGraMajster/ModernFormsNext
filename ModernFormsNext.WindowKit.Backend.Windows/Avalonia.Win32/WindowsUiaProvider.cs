using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

/// <summary>
/// Adapts one canonical ModernFormsNext semantic object to the managed Windows UI Automation
/// provider contracts.
/// </summary>
/// <remarks>
/// Pattern interfaces are implemented by the wrapper but are returned from
/// <see cref="GetPatternProvider(int)"/> only when the canonical semantic object advertises the
/// corresponding capability. All framework state is read or mutated through the UI dispatcher.
/// </remarks>
[GeneratedComClass]
internal partial class WindowsUiaProvider :
    IRawElementProviderSimple,
    IRawElementProviderFragment,
    IInvokeProvider,
    IToggleProvider,
    IValueProvider,
    IRangeValueProvider,
    IExpandCollapseProvider,
    ISelectionProvider,
    ISelectionItemProvider,
    IScrollItemProvider,
    IRawElementProviderSimpleAbi,
    IRawElementProviderFragmentAbi,
    IInvokeProviderAbi,
    IToggleProviderAbi,
    IValueProviderAbi,
    IRangeValueProviderAbi,
    IExpandCollapseProviderAbi,
    ISelectionProviderAbi,
    ISelectionItemProviderAbi,
    IScrollItemProviderAbi
{
    private const int ActionInvoke = 1 << 0;
    private const int ActionToggle = 1 << 1;
    private const int ActionSelect = 1 << 2;
    private const int ActionExpand = 1 << 3;
    private const int ActionCollapse = 1 << 4;
    private const int ActionSetValue = 1 << 5;
    private const int ActionScrollIntoView = 1 << 9;
    private const int ActionFocus = 1 << 10;

    private const int SelectionTakeFocus = 1;
    private const int SelectionTakeSelection = 2;
    private const int SelectionAdd = 8;
    private const int SelectionRemove = 16;

    private const int StateUnavailable = 0x1;
    private const int StateSelected = 0x2;
    private const int StateFocused = 0x4;
    private const int StateChecked = 0x10;
    private const int StateMixed = 0x20;
    private const int StateReadOnly = 0x40;
    private const int StateExpanded = 0x200;
    private const int StateCollapsed = 0x400;
    private const int StateInvisible = 0x8000;
    private const int StateOffscreen = 0x10000;
    private const int StateFocusable = 0x100000;
    private const int StateMultiSelectable = 0x1000000;

    private const int ViewRaw = 1;
    private const int ViewControl = 2;
    private const int ViewContent = 3;
    private const int ViewHidden = 4;

    private readonly WeakReference<IPlatformAccessibleObject> target;
    private long[] lastChildRuntimeIds;

    internal WindowsUiaProvider(
        WindowsUiaProviderContext context,
        IPlatformAccessibleObject platformObject,
        bool isRoot)
    {
        Context = context;
        target = new WeakReference<IPlatformAccessibleObject>(platformObject);
        IsRoot = isRoot;
        lastChildRuntimeIds = CaptureChildRuntimeIds(platformObject);
    }

    /// <summary>
    /// Gets the provider context shared by every element in this HWND fragment.
    /// </summary>
    protected WindowsUiaProviderContext Context { get; }

    /// <summary>
    /// Gets whether this provider represents the fragment root.
    /// </summary>
    protected bool IsRoot { get; }

    /// <summary>
    /// Gets the current semantic object for tests and cache identity checks.
    /// </summary>
    internal IPlatformAccessibleObject PlatformObject
        => Read(static node => node);

    /// <inheritdoc/>
    public ProviderOptions ProviderOptions => ProviderOptions.ServerSideProvider;

    /// <inheritdoc/>
    public virtual IRawElementProviderSimple? HostRawElementProvider => null;

    /// <inheritdoc/>
    public object? GetPatternProvider(int patternId)
    {
        return Read<object?>(node =>
        {
            int actions = node.GetSupportedActions();

            if (patternId == WindowsUiaIds.InvokePattern && HasAction(actions, ActionInvoke))
                return this;
            if (patternId == WindowsUiaIds.TogglePattern && HasAction(actions, ActionToggle))
                return this;
            if (patternId == WindowsUiaIds.ValuePattern && SupportsValue(node, actions))
                return this;
            if (patternId == WindowsUiaIds.RangeValuePattern && node.GetRangeValue() is not null)
                return this;
            if (patternId == WindowsUiaIds.ExpandCollapsePattern && SupportsExpandCollapse(node, actions))
                return this;
            if (patternId == WindowsUiaIds.SelectionPattern && IsSelectionContainer(node))
                return this;
            if (patternId == WindowsUiaIds.SelectionItemPattern && HasAction(actions, ActionSelect))
                return this;
            if (patternId == WindowsUiaIds.ScrollItemPattern && HasAction(actions, ActionScrollIntoView))
                return this;

            return null;
        });
    }

    /// <inheritdoc/>
    public object? GetPropertyValue(int propertyId)
    {
        return Read<object?>(node =>
        {
            int state = node.State;

            if (propertyId == WindowsUiaIds.NameProperty)
                return node.Name ?? string.Empty;
            if (propertyId == WindowsUiaIds.AutomationIdProperty)
                return node.GetAutomationId() ?? string.Empty;
            if (propertyId == WindowsUiaIds.ControlTypeProperty)
                return WindowsUiaControlTypeMapper.Map(node.GetControlType());
            if (propertyId == WindowsUiaIds.IsEnabledProperty)
                return !HasState(state, StateUnavailable);
            if (propertyId == WindowsUiaIds.IsKeyboardFocusableProperty)
                return HasState(state, StateFocusable) || HasAction(node.GetSupportedActions(), ActionFocus);
            if (propertyId == WindowsUiaIds.HasKeyboardFocusProperty)
                return HasState(state, StateFocused);
            if (propertyId == WindowsUiaIds.BoundingRectangleProperty)
                return WindowsUiaCoordinateConverter.ToBoundingRectangle(node.Bounds);
            if (propertyId == WindowsUiaIds.IsOffscreenProperty)
                return IsOffscreen(node, state);
            if (propertyId == WindowsUiaIds.HelpTextProperty)
                return node.Help ?? node.Description ?? string.Empty;
            if (propertyId == WindowsUiaIds.IsPasswordProperty)
                return node.GetIsSensitive() || HasState(state, unchecked((int)0x20000000));
            if (propertyId == WindowsUiaIds.IsControlElementProperty)
                return IsControlElement(node.GetAccessibilityView());
            if (propertyId == WindowsUiaIds.IsContentElementProperty)
                return IsContentElement(node.GetAccessibilityView());
            if (propertyId == WindowsUiaIds.ClassNameProperty)
                return node.GetClassName() ?? WindowsUiaControlTypeMapper.GetClassName(node.GetControlType());
            if (propertyId == WindowsUiaIds.FrameworkIdProperty)
                return "ModernFormsNext";
            if (propertyId == WindowsUiaIds.ValueProperty)
            {
                if (node.GetIsSensitive())
                    throw new WindowsUiaAccessDeniedException();

                return SupportsValue(node, node.GetSupportedActions())
                    ? node.Value ?? string.Empty
                    : null;
            }

            return null;
        });
    }

    /// <inheritdoc/>
    public virtual IRawElementProviderFragment? Navigate(NavigateDirection direction)
    {
        return Read(node =>
        {
            IPlatformAccessibleObject? result = direction switch
            {
                NavigateDirection.Parent => node.Parent,
                NavigateDirection.FirstChild => node.GetChildCount() > 0 ? node.GetChild(0) : null,
                NavigateDirection.LastChild => node.GetChildCount() > 0 ? node.GetChild(node.GetChildCount() - 1) : null,
                NavigateDirection.NextSibling => node.Navigate(PlatformAccessibleNavigation.Next),
                NavigateDirection.PreviousSibling => node.Navigate(PlatformAccessibleNavigation.Previous),
                _ => null
            };

            return result is null ? null : Context.GetOrCreate(result);
        });
    }

    /// <inheritdoc/>
    public int[] GetRuntimeId()
        => Read(node => CreateRuntimeId(GetEffectiveRuntimeId(node)));

    /// <inheritdoc/>
    public UiaRect BoundingRectangle
        => Read(static node => WindowsUiaCoordinateConverter.ToBoundingRectangle(node.Bounds));

    /// <inheritdoc/>
    public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots() => null;

    /// <inheritdoc/>
    public void SetFocus()
    {
        Mutate(node =>
        {
            if (HasAction(node.GetSupportedActions(), ActionFocus) && node.PerformUiaAction(ActionFocus))
                return;

            // Logical items can route focus through their canonical Select implementation even
            // when they do not own a Control or advertise the general Focus action.
            node.Select(SelectionTakeFocus);
        });
    }

    /// <inheritdoc/>
    public IRawElementProviderFragmentRoot FragmentRoot => Context.Root;

    /// <summary>
    /// Compares the current canonical children with the event snapshot kept by this provider so a
    /// shared Reorder notification can become the narrowest correct UIA structure-change event.
    /// The snapshot contains runtime IDs only and does not form a second semantic hierarchy.
    /// </summary>
    internal WindowsUiaStructureChange ConsumeStructureChange()
    {
        return Read(node =>
        {
            long[] currentIds = CaptureChildRuntimeIds(node);
            HashSet<long> previousSet = lastChildRuntimeIds.ToHashSet();
            HashSet<long> currentSet = currentIds.ToHashSet();
            long[] added = currentIds
                .Where(id => !previousSet.Contains(id))
                .ToArray();
            long[] removed = lastChildRuntimeIds
                .Where(id => !currentSet.Contains(id))
                .ToArray();

            lastChildRuntimeIds = currentIds;

            if (added.Length == 1 && removed.Length == 0)
            {
                return new WindowsUiaStructureChange(
                    this,
                    StructureChangeType.ChildAdded,
                    RuntimeId: null);
            }

            if (removed.Length == 1 && added.Length == 0)
            {
                return new WindowsUiaStructureChange(
                    this,
                    StructureChangeType.ChildRemoved,
                    CreateRuntimeId(removed[0]));
            }

            if (added.Length > 0 && removed.Length == 0)
                return new WindowsUiaStructureChange(this, StructureChangeType.ChildrenBulkAdded, RuntimeId: null);

            if (removed.Length > 0 && added.Length == 0)
                return new WindowsUiaStructureChange(this, StructureChangeType.ChildrenBulkRemoved, RuntimeId: null);

            return new WindowsUiaStructureChange(this, StructureChangeType.ChildrenReordered, RuntimeId: null);
        });
    }

    /// <inheritdoc/>
    void IInvokeProvider.Invoke()
        => PerformRequiredAction(ActionInvoke);

    /// <inheritdoc/>
    public ToggleState ToggleState
        => Read(static node =>
        {
            int state = node.State;
            if (HasState(state, StateMixed))
                return ToggleState.Indeterminate;
            if (HasState(state, StateChecked))
                return ToggleState.On;
            return ToggleState.Off;
        });

    /// <inheritdoc/>
    void IToggleProvider.Toggle()
        => PerformRequiredAction(ActionToggle);

    /// <inheritdoc/>
    string IValueProvider.Value
        => Read(static node => node.GetIsSensitive()
            ? throw new WindowsUiaAccessDeniedException()
            : node.Value ?? string.Empty);

    /// <inheritdoc/>
    bool IValueProvider.IsReadOnly
        => Read(static node => HasState(node.State, StateReadOnly)
            || !HasAction(node.GetSupportedActions(), ActionSetValue));

    /// <inheritdoc/>
    void IValueProvider.SetValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Mutate(node =>
        {
            if (HasState(node.State, StateUnavailable))
                throw new WindowsUiaElementNotEnabledException();
            if (HasState(node.State, StateReadOnly) || !HasAction(node.GetSupportedActions(), ActionSetValue))
                throw new InvalidOperationException("The semantic value is read-only.");
            if (!node.PerformUiaAction(ActionSetValue, value))
                throw new InvalidOperationException("The semantic object rejected the value.");
        });
    }

    /// <inheritdoc/>
    double IRangeValueProvider.Value => ReadRange(static range => range.Value);

    /// <inheritdoc/>
    bool IRangeValueProvider.IsReadOnly => ReadRange(static range => range.IsReadOnly);

    /// <inheritdoc/>
    double IRangeValueProvider.Maximum => ReadRange(static range => range.Maximum);

    /// <inheritdoc/>
    double IRangeValueProvider.Minimum => ReadRange(static range => range.Minimum);

    /// <inheritdoc/>
    double IRangeValueProvider.LargeChange => ReadRange(static range => range.LargeChange);

    /// <inheritdoc/>
    double IRangeValueProvider.SmallChange => ReadRange(static range => range.SmallChange);

    /// <inheritdoc/>
    void IRangeValueProvider.SetValue(double value)
    {
        Mutate(node =>
        {
            PlatformAccessibleRangeValue range = node.GetRangeValue()
                ?? throw new InvalidOperationException("The semantic object does not expose a numeric range.");

            if (HasState(node.State, StateUnavailable))
                throw new WindowsUiaElementNotEnabledException();
            if (range.IsReadOnly || !HasAction(node.GetSupportedActions(), ActionSetValue))
                throw new InvalidOperationException("The semantic range is read-only.");
            if (double.IsNaN(value) || value < range.Minimum || value > range.Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (!node.PerformUiaAction(ActionSetValue, value))
                throw new InvalidOperationException("The semantic object rejected the range value.");
        });
    }

    /// <inheritdoc/>
    public ExpandCollapseState ExpandCollapseState
        => Read(static node =>
        {
            int state = node.State;
            if (HasState(state, StateExpanded))
                return ExpandCollapseState.Expanded;
            if (HasState(state, StateCollapsed))
                return ExpandCollapseState.Collapsed;
            return ExpandCollapseState.LeafNode;
        });

    /// <inheritdoc/>
    void IExpandCollapseProvider.Expand()
        => PerformRequiredAction(ActionExpand);

    /// <inheritdoc/>
    void IExpandCollapseProvider.Collapse()
        => PerformRequiredAction(ActionCollapse);

    /// <inheritdoc/>
    bool ISelectionProvider.CanSelectMultiple
        => Read(static node => HasState(node.State, StateMultiSelectable));

    /// <inheritdoc/>
    bool ISelectionProvider.IsSelectionRequired => false;

    /// <inheritdoc/>
    IRawElementProviderSimple[] ISelectionProvider.GetSelection()
    {
        return Read(node =>
        {
            var selected = new List<IRawElementProviderSimple>();
            int count = node.GetChildCount();
            for (int index = 0; index < count; index++)
            {
                if (node.GetChild(index) is { } child && HasState(child.State, StateSelected))
                    selected.Add(Context.GetOrCreate(child));
            }

            return selected.ToArray();
        });
    }

    /// <inheritdoc/>
    bool ISelectionItemProvider.IsSelected
        => Read(static node => HasState(node.State, StateSelected));

    /// <inheritdoc/>
    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer
        => Read(node => node.Parent is { } parent ? Context.GetOrCreate(parent) : null);

    /// <inheritdoc/>
    void ISelectionItemProvider.Select()
        => PerformRequiredAction(ActionSelect);

    /// <inheritdoc/>
    void ISelectionItemProvider.AddToSelection()
    {
        Mutate(node =>
        {
            if (HasState(node.State, StateSelected))
                return;
            if (node.Parent is not { } parent || !HasState(parent.State, StateMultiSelectable))
                throw new InvalidOperationException("The selection container does not support multiple selection.");

            node.Select(SelectionAdd);
        });
    }

    /// <inheritdoc/>
    void ISelectionItemProvider.RemoveFromSelection()
    {
        Mutate(node =>
        {
            if (!HasState(node.State, StateSelected))
                return;
            if (node.Parent is not { } parent || !HasState(parent.State, StateMultiSelectable))
                throw new InvalidOperationException("The selection container does not support multiple selection.");

            node.Select(SelectionRemove);
        });
    }

    /// <inheritdoc/>
    void IScrollItemProvider.ScrollIntoView()
        => PerformRequiredAction(ActionScrollIntoView);

    /// <summary>
    /// Reads a semantic value on the owning UI thread.
    /// </summary>
    protected T Read<T>(Func<IPlatformAccessibleObject, T> callback)
        => Context.Read(target, IsRoot, callback);

    /// <summary>
    /// Mutates semantic state on the owning UI thread.
    /// </summary>
    protected void Mutate(Action<IPlatformAccessibleObject> callback)
        => Context.Mutate(target, IsRoot, callback);

    private static bool HasAction(int actions, int action) => (actions & action) != 0;

    private static bool HasState(int state, int flag) => (state & flag) != 0;

    private static bool SupportsValue(IPlatformAccessibleObject node, int actions)
        => node.GetRangeValue() is null
            && (HasAction(actions, ActionSetValue) || node.GetControlType() == 10);

    private static bool SupportsExpandCollapse(IPlatformAccessibleObject node, int actions)
        => HasAction(actions, ActionExpand)
            || HasAction(actions, ActionCollapse)
            || HasState(node.State, StateExpanded)
            || HasState(node.State, StateCollapsed);

    private static bool IsSelectionContainer(IPlatformAccessibleObject node)
        => node.GetControlType() is 11 or 12 or 14 or 16;

    private static bool IsControlElement(int view)
        => view is ViewControl or ViewContent;

    private static bool IsContentElement(int view)
        => view == ViewContent;

    private static bool IsOffscreen(IPlatformAccessibleObject node, int state)
        => HasState(state, StateInvisible)
            || HasState(state, StateOffscreen)
            || node.Bounds.Width <= 0
            || node.Bounds.Height <= 0;

    internal static int[] CreateRuntimeId(long id)
        =>
        [
            WindowsUiaIds.AppendRuntimeId,
            unchecked((int)(id & uint.MaxValue)),
            unchecked((int)(id >> 32))
        ];

    private static long GetEffectiveRuntimeId(IPlatformAccessibleObject node)
    {
        long id = node.GetRuntimeId();
        return id == 0 ? RuntimeHelpers.GetHashCode(node) : id;
    }

    private static long[] CaptureChildRuntimeIds(IPlatformAccessibleObject node)
    {
        int count = node.GetChildCount();
        var runtimeIds = new List<long>(count);
        for (int index = 0; index < count; index++)
        {
            if (node.GetChild(index) is { } child)
                runtimeIds.Add(GetEffectiveRuntimeId(child));
        }

        return runtimeIds.ToArray();
    }

    private T ReadRange<T>(Func<PlatformAccessibleRangeValue, T> selector)
        => Read(node => selector(node.GetRangeValue()
            ?? throw new InvalidOperationException("The semantic object does not expose a numeric range.")));

    private void PerformRequiredAction(int action)
    {
        Mutate(node =>
        {
            if (HasState(node.State, StateUnavailable))
                throw new WindowsUiaElementNotEnabledException();
            if (!HasAction(node.GetSupportedActions(), action) || !node.PerformUiaAction(action))
                throw new InvalidOperationException("The semantic action is not available.");
        });
    }
}

internal readonly record struct WindowsUiaStructureChange(
    WindowsUiaProvider Provider,
    StructureChangeType ChangeType,
    int[]? RuntimeId);

/// <summary>
/// Represents the fragment root associated with one ModernFormsNext HWND.
/// </summary>
[GeneratedComClass]
internal sealed partial class WindowsUiaRootProvider :
    WindowsUiaProvider,
    IRawElementProviderFragmentRoot,
    IRawElementProviderFragmentRootAbi,
    IDisposable
{
    private WindowsUiaRootProvider(WindowsUiaProviderContext context, IPlatformAccessibleObject root)
        : base(context, root, isRoot: true)
    {
    }

    /// <summary>
    /// Creates a UIA fragment root and initializes its lifecycle-bound provider cache.
    /// </summary>
    public static WindowsUiaRootProvider Create(
        IntPtr hwnd,
        IPlatformAccessibleObject root,
        Dispatcher dispatcher)
        => Create(hwnd, root, new WindowsUiaDispatcher(dispatcher));

    /// <summary>
    /// Creates a UIA fragment root using an explicit dispatcher boundary.
    /// </summary>
    internal static WindowsUiaRootProvider Create(
        IntPtr hwnd,
        IPlatformAccessibleObject root,
        IWindowsUiaDispatcher dispatcher,
        IWindowsUiaEventSink? eventSink = null)
    {
        var context = new WindowsUiaProviderContext(
            hwnd,
            dispatcher,
            eventSink ?? WindowsUiaNativeEventSink.Instance);
        var provider = new WindowsUiaRootProvider(context, root);
        context.Initialize(root, provider);
        return provider;
    }

    /// <inheritdoc/>
    public override IRawElementProviderSimple? HostRawElementProvider => null;

    /// <inheritdoc/>
    public override IRawElementProviderFragment? Navigate(NavigateDirection direction)
        => direction == NavigateDirection.Parent ? null : base.Navigate(direction);

    /// <inheritdoc/>
    public IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
    {
        return Read(node => node.HitTest((int)Math.Round(x), (int)Math.Round(y)) is { } hit
            ? Context.GetOrCreate(hit)
            : null);
    }

    /// <inheritdoc/>
    public IRawElementProviderFragment? GetFocus()
    {
        return Read(node => node.GetFocused() is { } focused
            ? Context.GetOrCreate(focused)
            : null);
    }

    /// <summary>
    /// Raises a native UIA notification for a canonical semantic event when clients are listening.
    /// </summary>
    public void RaiseNotification(IPlatformAccessibleObject source, int eventId)
        => Context.RaiseNotification(source, eventId);

    /// <inheritdoc/>
    public void Dispose() => Context.Dispose();

    /// <summary>
    /// Disconnects this provider from UIAutomationCore after the window message that destroyed the
    /// HWND has returned.
    /// </summary>
    public void Disconnect() => WindowsUiaNativeMethods.DisconnectProvider(this);
}

/// <summary>
/// Owns the dispatcher, weak identity cache, and lifecycle of one HWND UIA fragment.
/// </summary>
internal sealed class WindowsUiaProviderContext : IDisposable
{
    private static readonly TimeSpan DispatcherTimeout = TimeSpan.FromSeconds(10);
    private readonly ConditionalWeakTable<IPlatformAccessibleObject, WindowsUiaProvider> providers = new();
    private readonly IWindowsUiaDispatcher dispatcher;
    private readonly IWindowsUiaEventSink eventSink;
    private bool disposed;

    public WindowsUiaProviderContext(
        IntPtr hwnd,
        IWindowsUiaDispatcher dispatcher,
        IWindowsUiaEventSink eventSink)
    {
        Hwnd = hwnd;
        this.dispatcher = dispatcher;
        this.eventSink = eventSink;
    }

    public IntPtr Hwnd { get; }

    public WindowsUiaRootProvider Root { get; private set; } = null!;

    public void Initialize(IPlatformAccessibleObject root, WindowsUiaRootProvider provider)
    {
        Root = provider;
        providers.Add(root, provider);
    }

    public WindowsUiaProvider GetOrCreate(IPlatformAccessibleObject node)
    {
        ThrowIfDisposed();
        return providers.GetValue(node, value => new WindowsUiaProvider(this, value, isRoot: false));
    }

    public T Read<T>(
        WeakReference<IPlatformAccessibleObject> target,
        bool isRoot,
        Func<IPlatformAccessibleObject, T> callback)
        => OnUiThread(() =>
        {
            IPlatformAccessibleObject node = Resolve(target, isRoot);
            return callback(node);
        });

    public void Mutate(
        WeakReference<IPlatformAccessibleObject> target,
        bool isRoot,
        Action<IPlatformAccessibleObject> callback)
        => OnUiThread(() => callback(Resolve(target, isRoot)));

    public void RaiseNotification(IPlatformAccessibleObject source, int eventId)
    {
        if (disposed || !eventSink.ClientsAreListening)
            return;

        try
        {
            WindowsUiaProvider provider = GetOrCreate(source);
            WindowsUiaEventMapper.Raise(provider, source, eventId, this, eventSink);
        }
        catch (WindowsUiaElementNotAvailableException)
        {
            // A semantic object can be removed between the shared notification and provider lookup.
        }
        catch (Exception exception)
        {
            // Do not include semantic values in diagnostics: they may contain user-entered or
            // password data. UIA event failures must not escape the native window callback.
            Trace.TraceError("ModernFormsNext UIA notification failed: {0}", exception.GetType().Name);
        }
    }

    public void Dispose() => disposed = true;

    private T OnUiThread<T>(Func<T> callback)
    {
        ThrowIfDisposed();

        try
        {
            return dispatcher.CheckAccess()
                ? callback()
                : dispatcher.Invoke(callback, DispatcherTimeout);
        }
        catch (WindowsUiaElementNotAvailableException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new WindowsUiaElementNotAvailableException("The ModernFormsNext UI thread did not accept the automation request.", exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new WindowsUiaElementNotAvailableException("The ModernFormsNext dispatcher is shutting down.", exception);
        }
    }

    private void OnUiThread(Action callback)
    {
        ThrowIfDisposed();

        try
        {
            if (dispatcher.CheckAccess())
                callback();
            else
                dispatcher.Invoke(callback, DispatcherTimeout);
        }
        catch (WindowsUiaElementNotAvailableException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new WindowsUiaElementNotAvailableException("The ModernFormsNext UI thread did not accept the automation request.", exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new WindowsUiaElementNotAvailableException("The ModernFormsNext dispatcher is shutting down.", exception);
        }
    }

    private IPlatformAccessibleObject Resolve(
        WeakReference<IPlatformAccessibleObject> target,
        bool isRoot)
    {
        ThrowIfDisposed();
        if (!target.TryGetTarget(out IPlatformAccessibleObject? node))
            throw new WindowsUiaElementNotAvailableException("The semantic object is no longer available.");
        if (!isRoot && node.Parent is null)
            throw new WindowsUiaElementNotAvailableException("The semantic object is detached from its accessibility tree.");

        return node;
    }

    private void ThrowIfDisposed()
    {
        if (disposed || Hwnd == IntPtr.Zero)
            throw new WindowsUiaElementNotAvailableException("The native window is no longer available.");
    }
}

/// <summary>
/// Provides the narrow dispatcher operations needed at the native UIA callback boundary.
/// </summary>
internal interface IWindowsUiaDispatcher
{
    bool CheckAccess();

    T Invoke<T>(Func<T> callback, TimeSpan timeout);

    void Invoke(Action callback, TimeSpan timeout);
}

/// <summary>
/// Routes UIA callbacks through the framework's canonical UI dispatcher.
/// </summary>
internal sealed class WindowsUiaDispatcher : IWindowsUiaDispatcher
{
    private readonly Dispatcher dispatcher;

    public WindowsUiaDispatcher(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public bool CheckAccess() => dispatcher.CheckAccess();

    public T Invoke<T>(Func<T> callback, TimeSpan timeout)
        => dispatcher.Invoke(callback, DispatcherPriority.Send, CancellationToken.None, timeout);

    public void Invoke(Action callback, TimeSpan timeout)
        => dispatcher.Invoke(callback, DispatcherPriority.Send, CancellationToken.None, timeout);
}

/// <summary>
/// Maps canonical control type identifiers to Windows UI Automation control type identifiers.
/// </summary>
internal static class WindowsUiaControlTypeMapper
{
    public static int Map(int controlType)
        => controlType switch
        {
            2 => 50032,
            3 => 50033,
            4 => 50026,
            5 => 50020,
            6 => 50000,
            7 => 50002,
            8 => 50013,
            9 => 50002,
            10 => 50004,
            11 => 50003,
            12 => 50008,
            13 => 50007,
            14 => 50023,
            15 => 50024,
            16 => 50018,
            17 => 50019,
            18 => 50009,
            19 => 50011,
            20 => 50015,
            21 => 50012,
            22 => 50014,
            23 => 50032,
            24 => 50006,
            25 => 50021,
            26 => 50038,
            _ => 50025
        };

    public static string GetClassName(int controlType)
        => controlType switch
        {
            2 => "Form",
            23 => "Dialog",
            3 => "Pane",
            4 => "Group",
            5 => "Text",
            6 => "Button",
            7 => "CheckBox",
            8 => "RadioButton",
            9 => "Switch",
            10 => "TextBox",
            11 => "ComboBox",
            12 => "List",
            13 => "ListItem",
            14 => "TreeView",
            15 => "TreeItem",
            16 => "TabControl",
            17 => "TabItem",
            18 => "Menu",
            19 => "MenuItem",
            20 => "TrackBar",
            21 => "ProgressBar",
            22 => "ScrollBar",
            24 => "Image",
            25 => "ToolBar",
            26 => "Separator",
            _ => "Custom"
        };
}

/// <summary>
/// Converts canonical screen bounds to the physical screen rectangle expected by Windows UIA.
/// </summary>
internal static class WindowsUiaCoordinateConverter
{
    public static UiaRect ToBoundingRectangle(WindowKit.Rect bounds)
    {
        // The ModernFormsNext Windows host's PointToScreen path already applies RenderScaling and
        // returns physical desktop pixels. Applying DPI scaling again here would double-scale UIA
        // rectangles at 125%, 150%, and other non-default monitor scales.
        if (!double.IsFinite(bounds.X)
            || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width)
            || !double.IsFinite(bounds.Height)
            || bounds.Width <= 0
            || bounds.Height <= 0)
        {
            return new UiaRect(0, 0, 0, 0);
        }

        return new UiaRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }
}

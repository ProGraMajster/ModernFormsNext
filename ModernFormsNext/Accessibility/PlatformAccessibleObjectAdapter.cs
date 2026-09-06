using System.Drawing;
using System.Runtime.CompilerServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.Accessibility;

/// <summary>
/// Adapts a ModernFormsNext <see cref="AccessibleObject"/> to the WindowKit accessibility bridge.
/// </summary>
/// <remarks>
/// The adapter keeps the main framework's WinForms-like accessibility API separate from backend
/// contracts. Backend projects consume <see cref="IPlatformAccessibleObject"/> and do not need a
/// reference back to the main ModernFormsNext assembly.
/// </remarks>
internal sealed class PlatformAccessibleObjectAdapter : IPlatformUiaAccessibleObject,
    IPlatformAccessibilityNotifications, IPlatformAccessibilitySelection
{
    private static readonly ConditionalWeakTable<AccessibleObject, PlatformAccessibleObjectAdapter> s_cache = new();
    private readonly AccessibleObject accessible_object;

    private PlatformAccessibleObjectAdapter(AccessibleObject accessibleObject)
    {
        accessible_object = accessibleObject;
        // The adapter and peer have the same conditional-weak-table lifetime. Native providers
        // unsubscribe when detached; no control or native view is retained by a global listener.
        accessible_object.ClientNotification += (_, e) =>
            AccessibilityNotification?.Invoke((int)e.EventId, e.ObjectId, e.ChildId);
    }

    public event Action<int, int, int>? AccessibilityNotification;

    // Only the existing ListBox logical peer implements independent removal in its selection
    // flags override. Do not infer that capability from a custom peer's role or class name.
    public bool CanClearSelection => accessible_object.ControlType == AccessibleControlType.ListItem
        && accessible_object.Parent is Control.ControlAccessibleObject
        { Owner: ListBox { SelectionMode: SelectionMode.MultiSimple or SelectionMode.MultiExtended } };

    /// <summary>
    /// Gets or creates an adapter for the specified accessible object.
    /// </summary>
    /// <param name="accessibleObject">The accessible object to adapt.</param>
    /// <returns>The adapted object, or <see langword="null"/> when <paramref name="accessibleObject"/> is <see langword="null"/>.</returns>
    public static IPlatformAccessibleObject? From(AccessibleObject? accessibleObject)
        => accessibleObject is null ? null : s_cache.GetValue(accessibleObject, static value => new PlatformAccessibleObjectAdapter(value));

    /// <inheritdoc/>
    public long RuntimeId => accessible_object.RuntimeId;

    /// <inheritdoc/>
    public string? AutomationId => accessible_object.AutomationId;

    /// <inheritdoc/>
    public int ControlType => (int)accessible_object.ControlType;

    /// <inheritdoc/>
    public int View => (int)accessible_object.View;

    /// <inheritdoc/>
    public string? ClassName
        => accessible_object is Control.ControlAccessibleObject { Owner: { } owner }
            ? owner.GetType().Name
            : accessible_object.ControlType.ToString();

    /// <inheritdoc/>
    public Rect Bounds => ToRect(accessible_object.Bounds);

    /// <inheritdoc/>
    public string? DefaultAction => accessible_object.DefaultAction;

    /// <inheritdoc/>
    public string? Description => accessible_object.Description;

    /// <inheritdoc/>
    public string? Help => accessible_object.Help;

    /// <inheritdoc/>
    public string? KeyboardShortcut => accessible_object.KeyboardShortcut;

    /// <inheritdoc/>
    public string? Name
    {
        get => accessible_object.Name;
        set => accessible_object.Name = value;
    }

    /// <inheritdoc/>
    public IPlatformAccessibleObject? Parent => From(accessible_object.Parent);

    /// <inheritdoc/>
    public int Role => (int)accessible_object.Role;

    /// <inheritdoc/>
    public int State => (int)accessible_object.State;

    /// <inheritdoc/>
    public bool IsSensitive => accessible_object.IsSensitive;

    /// <inheritdoc/>
    public PlatformAccessibleRangeValue? RangeValue
        => accessible_object.RangeValue is { } range
            ? new PlatformAccessibleRangeValue(
                range.Value,
                range.Minimum,
                range.Maximum,
                range.SmallChange,
                range.LargeChange,
                range.IsReadOnly)
            : null;

    /// <inheritdoc/>
    public int SupportedActions => (int)accessible_object.SupportedActions;

    /// <inheritdoc/>
    public string? Value
    {
        get => accessible_object.Value;
        set => accessible_object.Value = value;
    }

    /// <inheritdoc/>
    public void DoDefaultAction() => accessible_object.DoDefaultAction();

    /// <inheritdoc/>
    public bool PerformAction(int action, object? parameter = null)
        => accessible_object.PerformAction((AccessibleActions)action, parameter);

    /// <inheritdoc/>
    public int GetHelpTopic(out string? fileName) => accessible_object.GetHelpTopic(out fileName);

    /// <inheritdoc/>
    public IPlatformAccessibleObject? GetChild(int index) => From(accessible_object.GetChild(index));

    /// <inheritdoc/>
    public int GetChildCount() => accessible_object.GetChildCount();

    /// <inheritdoc/>
    public IPlatformAccessibleObject? GetFocused() => From(accessible_object.GetFocused());

    /// <inheritdoc/>
    public IPlatformAccessibleObject? GetSelected() => From(accessible_object.GetSelected());

    /// <inheritdoc/>
    public IPlatformAccessibleObject? HitTest(int x, int y) => From(accessible_object.HitTest(x, y));

    /// <inheritdoc/>
    public IPlatformAccessibleObject? Navigate(PlatformAccessibleNavigation direction)
        => From(accessible_object.Navigate(ToAccessibleNavigation(direction)));

    /// <inheritdoc/>
    public void Select(int flags) => accessible_object.Select((AccessibleSelection)flags);

    private static AccessibleNavigation ToAccessibleNavigation(PlatformAccessibleNavigation direction)
        => direction switch
        {
            PlatformAccessibleNavigation.Up => AccessibleNavigation.Up,
            PlatformAccessibleNavigation.Down => AccessibleNavigation.Down,
            PlatformAccessibleNavigation.Left => AccessibleNavigation.Left,
            PlatformAccessibleNavigation.Right => AccessibleNavigation.Right,
            PlatformAccessibleNavigation.Next => AccessibleNavigation.Next,
            PlatformAccessibleNavigation.Previous => AccessibleNavigation.Previous,
            PlatformAccessibleNavigation.FirstChild => AccessibleNavigation.FirstChild,
            PlatformAccessibleNavigation.LastChild => AccessibleNavigation.LastChild,
            _ => AccessibleNavigation.Next
        };

    private static Rect ToRect(Rectangle rectangle)
        => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}

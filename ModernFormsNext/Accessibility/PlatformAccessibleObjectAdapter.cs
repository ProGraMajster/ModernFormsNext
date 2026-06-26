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
internal sealed class PlatformAccessibleObjectAdapter : IPlatformAccessibleObject
{
    private static readonly ConditionalWeakTable<AccessibleObject, PlatformAccessibleObjectAdapter> s_cache = new();
    private readonly AccessibleObject accessible_object;

    private PlatformAccessibleObjectAdapter(AccessibleObject accessibleObject)
    {
        accessible_object = accessibleObject;
    }

    /// <summary>
    /// Gets or creates an adapter for the specified accessible object.
    /// </summary>
    /// <param name="accessibleObject">The accessible object to adapt.</param>
    /// <returns>The adapted object, or <see langword="null"/> when <paramref name="accessibleObject"/> is <see langword="null"/>.</returns>
    public static IPlatformAccessibleObject? From(AccessibleObject? accessibleObject)
        => accessibleObject is null ? null : s_cache.GetValue(accessibleObject, static value => new PlatformAccessibleObjectAdapter(value));

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
    public string? Value
    {
        get => accessible_object.Value;
        set => accessible_object.Value = value;
    }

    /// <inheritdoc/>
    public void DoDefaultAction() => accessible_object.DoDefaultAction();

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

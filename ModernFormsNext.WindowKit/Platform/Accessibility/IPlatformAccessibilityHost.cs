using ModernFormsNext.WindowKit;

namespace ModernFormsNext.WindowKit.Platform.Accessibility
{
    /// <summary>
    /// Exposes a platform-neutral accessibility tree for a top-level window.
    /// </summary>
    /// <remarks>
    /// Window backends query this interface from the input root assigned to a native window.
    /// The host keeps backend projects independent from ModernFormsNext control types while still
    /// allowing native accessibility providers to inspect names, roles, states, bounds, and child
    /// relationships.
    /// </remarks>
    public interface IPlatformAccessibilityHost
    {
        /// <summary>
        /// Gets the root accessible object for the host, or <see langword="null"/> when no
        /// accessibility tree is currently available.
        /// </summary>
        IPlatformAccessibleObject? AccessibilityRoot { get; }
    }

    /// <summary>
    /// Represents one object in a platform-neutral accessibility tree.
    /// </summary>
    /// <remarks>
    /// Values returned by this interface are intentionally backend-neutral. Roles, states, and
    /// event identifiers are exposed as integers so the shared ModernFormsNext project can keep
    /// WinForms-compatible values while backends translate them to their native accessibility
    /// systems.
    /// </remarks>
    public interface IPlatformAccessibleObject
    {
        /// <summary>
        /// Gets the object bounds in screen coordinates, measured in logical pixels.
        /// </summary>
        Rect Bounds { get; }

        /// <summary>
        /// Gets the default action description for the object.
        /// </summary>
        string? DefaultAction { get; }

        /// <summary>
        /// Gets the supplemental description for the object.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets help text associated with the object.
        /// </summary>
        string? Help { get; }

        /// <summary>
        /// Gets the keyboard shortcut associated with the object.
        /// </summary>
        string? KeyboardShortcut { get; }

        /// <summary>
        /// Gets or sets the accessible name for the object.
        /// </summary>
        string? Name { get; set; }

        /// <summary>
        /// Gets the parent object in the accessibility tree.
        /// </summary>
        IPlatformAccessibleObject? Parent { get; }

        /// <summary>
        /// Gets the semantic role identifier for the object.
        /// </summary>
        int Role { get; }

        /// <summary>
        /// Gets the state flags for the object.
        /// </summary>
        int State { get; }

        /// <summary>
        /// Gets or sets the current value exposed by the object.
        /// </summary>
        string? Value { get; set; }

        /// <summary>
        /// Performs the object's default action when one is available.
        /// </summary>
        void DoDefaultAction();

        /// <summary>
        /// Gets the help topic identifier and help file associated with this object.
        /// </summary>
        /// <param name="fileName">When this method returns, contains the help file name, if one exists.</param>
        /// <returns>The help topic identifier, or <c>0</c> when no topic is available.</returns>
        int GetHelpTopic(out string? fileName);

        /// <summary>
        /// Gets a child accessible object by zero-based index.
        /// </summary>
        /// <param name="index">The zero-based child index.</param>
        /// <returns>The child object, or <see langword="null"/> when the index is invalid.</returns>
        IPlatformAccessibleObject? GetChild(int index);

        /// <summary>
        /// Gets the number of child accessible objects.
        /// </summary>
        /// <returns>The number of child objects.</returns>
        int GetChildCount();

        /// <summary>
        /// Gets the focused object inside this subtree.
        /// </summary>
        /// <returns>The focused object, or <see langword="null"/> when no object is focused.</returns>
        IPlatformAccessibleObject? GetFocused();

        /// <summary>
        /// Gets the selected object inside this subtree.
        /// </summary>
        /// <returns>The selected object, or <see langword="null"/> when no object is selected.</returns>
        IPlatformAccessibleObject? GetSelected();

        /// <summary>
        /// Gets the object at a screen coordinate.
        /// </summary>
        /// <param name="x">The screen X coordinate in logical pixels.</param>
        /// <param name="y">The screen Y coordinate in logical pixels.</param>
        /// <returns>The object at the coordinate, or <see langword="null"/> when none is found.</returns>
        IPlatformAccessibleObject? HitTest(int x, int y);

        /// <summary>
        /// Navigates to a related object in the accessibility tree.
        /// </summary>
        /// <param name="direction">The navigation direction.</param>
        /// <returns>The related object, or <see langword="null"/> when navigation cannot be completed.</returns>
        IPlatformAccessibleObject? Navigate(PlatformAccessibleNavigation direction);

        /// <summary>
        /// Selects or focuses the object according to platform-neutral selection flags.
        /// </summary>
        /// <param name="flags">The selection behavior flags.</param>
        void Select(int flags);
    }

    /// <summary>
    /// Specifies common platform-neutral directions for accessibility tree navigation.
    /// </summary>
    public enum PlatformAccessibleNavigation
    {
        /// <summary>
        /// Navigate to a related object above the starting object.
        /// </summary>
        Up = 0x1,

        /// <summary>
        /// Navigate downward to a related object.
        /// </summary>
        Down = 0x2,

        /// <summary>
        /// Navigate to a related object on the left.
        /// </summary>
        Left = 0x3,

        /// <summary>
        /// Navigate to a related object on the right.
        /// </summary>
        Right = 0x4,

        /// <summary>
        /// Navigate to the next sibling object.
        /// </summary>
        Next = 0x5,

        /// <summary>
        /// Navigate to the previous sibling object.
        /// </summary>
        Previous = 0x6,

        /// <summary>
        /// Navigate to the first child object.
        /// </summary>
        FirstChild = 0x7,

        /// <summary>
        /// Navigate to the last child object.
        /// </summary>
        LastChild = 0x8
    }
}

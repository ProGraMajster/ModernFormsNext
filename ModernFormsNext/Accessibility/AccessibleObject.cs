using System;
using System.Drawing;

namespace ModernFormsNext.Accessibility;

/// <summary>
/// Provides platform-neutral information about an object that can be exposed to assistive technologies.
/// </summary>
/// <remarks>
/// <para>
/// This type intentionally describes the ModernFormsNext accessibility tree without depending on
/// Windows UI Automation, MSAA, COM, native handles, or any other backend-specific API. Platform
/// backends may adapt instances of this class to native accessibility providers.
/// </para>
/// <para>
/// The API mirrors the most common WinForms <c>AccessibleObject</c> members so that applications
/// migrating from WinForms can keep their accessibility metadata and custom accessible objects
/// close to their original shape.
/// </para>
/// </remarks>
public class AccessibleObject
{
    /// <summary>
    /// Occurs when <see cref="NotifyClients(AccessibleEvents, int, int)"/> is called for this object.
    /// </summary>
    /// <remarks>
    /// The shared framework raises this event as a platform-neutral notification. Native backends
    /// can listen to it and translate the event into the platform accessibility system.
    /// </remarks>
    public event EventHandler<AccessibleObjectNotificationEventArgs>? ClientNotification;

    /// <summary>
    /// Gets the object bounds in screen coordinates.
    /// </summary>
    public virtual Rectangle Bounds => Rectangle.Empty;

    /// <summary>
    /// Gets a localized description of the object's default action.
    /// </summary>
    public virtual string? DefaultAction => null;

    /// <summary>
    /// Gets a localized description of the object's visual appearance.
    /// </summary>
    public virtual string? Description => null;

    /// <summary>
    /// Gets help text that describes what the object does or how it should be used.
    /// </summary>
    public virtual string? Help => null;

    /// <summary>
    /// Gets the keyboard shortcut or access key associated with the object.
    /// </summary>
    public virtual string? KeyboardShortcut => null;

    /// <summary>
    /// Gets or sets the accessible name presented to assistive technologies.
    /// </summary>
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets the parent object in the accessibility tree.
    /// </summary>
    public virtual AccessibleObject? Parent => null;

    /// <summary>
    /// Gets the semantic role of the object.
    /// </summary>
    public virtual AccessibleRole Role => AccessibleRole.Default;

    /// <summary>
    /// Gets the current accessibility state flags for the object.
    /// </summary>
    public virtual AccessibleStates State => AccessibleStates.None;

    /// <summary>
    /// Gets or sets the current value exposed by the object.
    /// </summary>
    public virtual string? Value { get; set; }

    /// <summary>
    /// Performs the default action for the object, when one is available.
    /// </summary>
    public virtual void DoDefaultAction()
    {
    }

    /// <summary>
    /// Gets a child accessible object by zero-based index.
    /// </summary>
    /// <param name="index">The zero-based child index.</param>
    /// <returns>The child accessible object, or <see langword="null"/> when the index is invalid.</returns>
    public virtual AccessibleObject? GetChild(int index) => null;

    /// <summary>
    /// Gets the number of child accessible objects.
    /// </summary>
    /// <returns>The number of child objects.</returns>
    public virtual int GetChildCount() => 0;

    /// <summary>
    /// Gets the object that currently has keyboard focus within this subtree.
    /// </summary>
    /// <returns>The focused object, or <see langword="null"/> when no child object is focused.</returns>
    public virtual AccessibleObject? GetFocused() => null;

    /// <summary>
    /// Gets the help topic identifier and file associated with this object.
    /// </summary>
    /// <param name="fileName">When this method returns, contains the help file name, if one exists.</param>
    /// <returns>The help topic identifier, or <c>0</c> when no topic is available.</returns>
    public virtual int GetHelpTopic(out string? fileName)
    {
        fileName = null;
        return 0;
    }

    /// <summary>
    /// Gets the selected object within this subtree.
    /// </summary>
    /// <returns>The selected object, or <see langword="null"/> when no selection is available.</returns>
    public virtual AccessibleObject? GetSelected() => null;

    /// <summary>
    /// Gets the accessible object at the specified screen coordinate.
    /// </summary>
    /// <param name="x">The screen X coordinate in logical pixels.</param>
    /// <param name="y">The screen Y coordinate in logical pixels.</param>
    /// <returns>The object at the coordinate, or <see langword="null"/> when none is found.</returns>
    public virtual AccessibleObject? HitTest(int x, int y) => Bounds.Contains(x, y) ? this : null;

    /// <summary>
    /// Navigates to a related object in the accessibility tree.
    /// </summary>
    /// <param name="navdir">The direction to navigate.</param>
    /// <returns>The related object, or <see langword="null"/> when navigation cannot be completed.</returns>
    public virtual AccessibleObject? Navigate(AccessibleNavigation navdir) => null;

    /// <summary>
    /// Raises a platform-neutral accessibility notification for this object.
    /// </summary>
    /// <param name="accEvent">The event being reported.</param>
    public void NotifyClients(AccessibleEvents accEvent) => NotifyClients(accEvent, 0, 0);

    /// <summary>
    /// Raises a platform-neutral accessibility notification for a child of this object.
    /// </summary>
    /// <param name="accEvent">The event being reported.</param>
    /// <param name="childID">The child identifier, or <c>0</c> for this object.</param>
    public void NotifyClients(AccessibleEvents accEvent, int childID) => NotifyClients(accEvent, 0, childID);

    /// <summary>
    /// Raises a platform-neutral accessibility notification for this object.
    /// </summary>
    /// <param name="accEvent">The event being reported.</param>
    /// <param name="objectID">A platform object identifier. Shared ModernFormsNext code normally passes <c>0</c>.</param>
    /// <param name="childID">The child identifier, or <c>0</c> for this object.</param>
    public virtual void NotifyClients(AccessibleEvents accEvent, int objectID, int childID)
    {
        ClientNotification?.Invoke(this, new AccessibleObjectNotificationEventArgs(accEvent, objectID, childID));
    }

    /// <summary>
    /// Selects or focuses this object according to the supplied flags.
    /// </summary>
    /// <param name="flags">Selection behavior flags.</param>
    public virtual void Select(AccessibleSelection flags)
    {
    }
}

/// <summary>
/// Provides data for <see cref="AccessibleObject.ClientNotification"/>.
/// </summary>
public sealed class AccessibleObjectNotificationEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessibleObjectNotificationEventArgs"/> class.
    /// </summary>
    /// <param name="eventId">The accessibility event being reported.</param>
    /// <param name="objectId">The platform object identifier, or <c>0</c> for platform-neutral notifications.</param>
    /// <param name="childId">The child identifier, or <c>0</c> for the object itself.</param>
    public AccessibleObjectNotificationEventArgs(AccessibleEvents eventId, int objectId, int childId)
    {
        EventId = eventId;
        ObjectId = objectId;
        ChildId = childId;
    }

    /// <summary>
    /// Gets the accessibility event being reported.
    /// </summary>
    public AccessibleEvents EventId { get; }

    /// <summary>
    /// Gets the platform object identifier, or <c>0</c> for platform-neutral notifications.
    /// </summary>
    public int ObjectId { get; }

    /// <summary>
    /// Gets the child identifier, or <c>0</c> for the object itself.
    /// </summary>
    public int ChildId { get; }
}

using System;
using System.Drawing;
using System.Threading;

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
    private static long s_nextRuntimeId;
    private readonly long runtime_id = CreateRuntimeId();

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
    /// Gets or sets the stable developer-defined identifier used by semantic automation consumers.
    /// </summary>
    /// <remarks>
    /// The value identifies the same logical element across repeated queries while that element
    /// exists. It is distinct from <see cref="RuntimeId"/> and need not be globally unique. Control
    /// implementations normally fall back to <see cref="Control.Name"/> when no explicit value is
    /// supplied.
    /// </remarks>
    public virtual string? AutomationId { get; set; }

    /// <summary>
    /// Gets the normalized platform-neutral control type.
    /// </summary>
    public virtual AccessibleControlType ControlType => AccessibleControlType.Custom;

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
    /// Gets a stable identifier for this accessible object within the current process session.
    /// </summary>
    /// <remarks>
    /// Runtime identifiers are assigned lazily with object construction and are not persistent
    /// across application runs. Logical-item implementations should cache their accessible object
    /// for the lifetime of the represented item so this value remains stable across reorderings.
    /// </remarks>
    public long RuntimeId => runtime_id;

    /// <summary>
    /// Gets whether the object contains sensitive data that must be redacted from semantic output,
    /// snapshots, diagnostics, and logs.
    /// </summary>
    public virtual bool IsSensitive => false;

    /// <summary>
    /// Gets optional numeric range metadata for sliders, progress indicators, and similar objects.
    /// </summary>
    public virtual AccessibleRangeValue? RangeValue => null;

    /// <summary>
    /// Gets the current accessibility state flags for the object.
    /// </summary>
    public virtual AccessibleStates State => AccessibleStates.None;

    /// <summary>
    /// Gets the semantic actions supported by this object.
    /// </summary>
    public virtual AccessibleActions SupportedActions => AccessibleActions.None;

    /// <summary>
    /// Gets or sets the current value exposed by the object.
    /// </summary>
    /// <remarks>
    /// Objects for which <see cref="IsSensitive"/> is <see langword="true"/> must return a redacted
    /// value, normally <see cref="string.Empty"/>, rather than exposing the underlying content.
    /// </remarks>
    public virtual string? Value { get; set; }

    /// <summary>
    /// Gets the projection in which this object participates.
    /// </summary>
    public virtual AccessibilityView View => AccessibilityView.Control;

    /// <summary>
    /// Performs the default action for the object, when one is available.
    /// </summary>
    public virtual void DoDefaultAction()
    {
    }

    /// <summary>
    /// Performs one supported platform-neutral semantic action.
    /// </summary>
    /// <param name="action">Exactly one action flag declared by <see cref="SupportedActions"/>.</param>
    /// <param name="parameter">
    /// Optional action data. <see cref="AccessibleActions.SetValue"/> accepts the natural semantic
    /// value type of the represented object. Actions without parameters require <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the supported request was accepted; otherwise
    /// <see langword="false"/> for unsupported actions, invalid parameters, unavailable objects, or
    /// rejected state changes.
    /// </returns>
    /// <remarks>
    /// Mutable framework state is UI-thread-affine. Callers on a platform callback thread must
    /// dispatch to the owning UI thread before invoking this method. Implementations must use normal
    /// control APIs and must not invoke private event handlers or discover actions through reflection.
    /// </remarks>
    public virtual bool PerformAction(AccessibleActions action, object? parameter = null) => false;

    /// <summary>
    /// Gets a child accessible object by zero-based index.
    /// </summary>
    /// <param name="index">The zero-based child index.</param>
    /// <returns>The child accessible object, or <see langword="null"/> when the index is invalid.</returns>
    /// <remarks>
    /// Implementations may return logical children that are not <see cref="Control"/> instances.
    /// Removed, disposed, and <see cref="AccessibilityView.Hidden"/> children must not remain in the
    /// active child sequence.
    /// </remarks>
    public virtual AccessibleObject? GetChild(int index) => null;

    /// <summary>
    /// Gets the number of child accessible objects.
    /// </summary>
    /// <returns>The number of active child objects.</returns>
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

    private static long CreateRuntimeId()
    {
        while (true)
        {
            long current = Volatile.Read(ref s_nextRuntimeId);
            if (current == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The process-local accessibility runtime identifier space has been exhausted.");
            }

            long next = current + 1;
            if (Interlocked.CompareExchange(ref s_nextRuntimeId, next, current) == current)
                return next;
        }
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

using System;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Layout;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_accessibilityObjectProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleDefaultActionDescriptionProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleDescriptionProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleNameProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleRoleProperty = PropertyStore.CreateKey();
    private bool raising_accessibility_notification;

    /// <summary>
    /// Gets or sets the default action description exposed to assistive technologies.
    /// </summary>
    /// <remarks>
    /// Set this property when a custom control has an activation action that is not obvious from
    /// its role or text. Changing the value raises a platform-neutral
    /// <see cref="AccessibleEvents.DefaultActionChange"/> notification.
    /// </remarks>
    public string? AccessibleDefaultActionDescription
    {
        get => Properties.GetObject<string>(s_accessibleDefaultActionDescriptionProperty);
        set
        {
            if (AccessibleDefaultActionDescription == value)
                return;

            SetNullableAccessibilityString(s_accessibleDefaultActionDescriptionProperty, value);
            NotifyAccessibilityClients(AccessibleEvents.DefaultActionChange);
        }
    }

    /// <summary>
    /// Gets or sets the description exposed to assistive technologies.
    /// </summary>
    /// <remarks>
    /// Use this for additional context that is not part of the visual label. Changing the value
    /// raises a platform-neutral <see cref="AccessibleEvents.DescriptionChange"/> notification.
    /// </remarks>
    public string? AccessibleDescription
    {
        get => Properties.GetObject<string>(s_accessibleDescriptionProperty);
        set
        {
            if (AccessibleDescription == value)
                return;

            SetNullableAccessibilityString(s_accessibleDescriptionProperty, value);
            NotifyAccessibilityClients(AccessibleEvents.DescriptionChange);
        }
    }

    /// <summary>
    /// Gets or sets the accessible name exposed to assistive technologies.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value lets the control derive its name from its text or name.
    /// An empty string is preserved as an explicit accessible name, matching WinForms semantics.
    /// Changing the value raises a platform-neutral <see cref="AccessibleEvents.NameChange"/>
    /// notification.
    /// </remarks>
    /// <example>
    /// <code>
    /// var saveButton = new Button
    /// {
    ///     Text = "Save",
    ///     AccessibleName = "Save document",
    ///     AccessibleDescription = "Writes the current document to disk.",
    ///     AccessibleRole = AccessibleRole.PushButton
    /// };
    /// </code>
    /// </example>
    public string? AccessibleName
    {
        get => Properties.GetObject<string>(s_accessibleNameProperty);
        set
        {
            if (AccessibleName == value)
                return;

            SetNullableAccessibilityString(s_accessibleNameProperty, value);
            NotifyAccessibilityClients(AccessibleEvents.NameChange);
        }
    }

    /// <summary>
    /// Gets or sets the semantic role exposed to assistive technologies.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="AccessibleRole.Default"/>, which lets
    /// <see cref="ControlAccessibleObject"/> choose an appropriate generic role. Changing the value
    /// raises a platform-neutral <see cref="AccessibleEvents.StateChange"/> notification.
    /// </remarks>
    public AccessibleRole AccessibleRole
    {
        get => Properties.GetEnum(s_accessibleRoleProperty, AccessibleRole.Default);
        set
        {
            SourceGenerated.EnumValidator.Validate(value);

            if (AccessibleRole == value)
                return;

            if (value == AccessibleRole.Default)
                Properties.RemoveInteger(s_accessibleRoleProperty);
            else
                Properties.SetEnum(s_accessibleRoleProperty, value);

            NotifyAccessibilityClients(AccessibleEvents.StateChange);
        }
    }

    /// <summary>
    /// Gets the accessible object that represents this control.
    /// </summary>
    /// <remarks>
    /// The object is created lazily by <see cref="CreateAccessibilityInstance"/> and cached for the
    /// lifetime of the control. Accessing this property does not create native accessibility
    /// providers by itself.
    /// </remarks>
    public AccessibleObject AccessibilityObject
    {
        get
        {
            if (Properties.GetObject(s_accessibilityObjectProperty) is not AccessibleObject accessibleObject)
            {
                accessibleObject = CreateAccessibilityInstance()
                    ?? throw new InvalidOperationException($"{nameof(CreateAccessibilityInstance)} must not return null.");

                accessibleObject.ClientNotification += AccessibilityObject_ClientNotification;
                Properties.SetObject(s_accessibilityObjectProperty, accessibleObject);
            }

            return accessibleObject;
        }
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="AccessibilityObject"/> has already been created.
    /// </summary>
    internal bool IsAccessibilityObjectCreated => Properties.ContainsObject(s_accessibilityObjectProperty);

    /// <summary>
    /// Creates the accessible object used by <see cref="AccessibilityObject"/>.
    /// </summary>
    /// <returns>A new accessible object for this control.</returns>
    /// <remarks>
    /// Derived controls can override this method to expose richer child objects, roles, values,
    /// or actions. Override implementations should not return <see langword="null"/>.
    /// </remarks>
    protected virtual AccessibleObject CreateAccessibilityInstance() => new ControlAccessibleObject(this);

    /// <summary>
    /// Raises a platform-neutral accessibility notification for this control.
    /// </summary>
    /// <param name="accEvent">The accessibility event being reported.</param>
    public void NotifyAccessibilityClients(AccessibleEvents accEvent) => NotifyAccessibilityClients(accEvent, 0, 0);

    /// <summary>
    /// Raises a platform-neutral accessibility notification for a child of this control.
    /// </summary>
    /// <param name="accEvent">The accessibility event being reported.</param>
    /// <param name="childID">The child identifier, or <c>0</c> for this control.</param>
    public void NotifyAccessibilityClients(AccessibleEvents accEvent, int childID)
        => NotifyAccessibilityClients(accEvent, 0, childID);

    /// <summary>
    /// Raises a platform-neutral accessibility notification for this control.
    /// </summary>
    /// <param name="accEvent">The accessibility event being reported.</param>
    /// <param name="objectID">A platform object identifier. Shared ModernFormsNext code normally passes <c>0</c>.</param>
    /// <param name="childID">The child identifier, or <c>0</c> for this control.</param>
    public void NotifyAccessibilityClients(AccessibleEvents accEvent, int objectID, int childID)
    {
        if (IsAccessibilityObjectCreated)
        {
            raising_accessibility_notification = true;

            try
            {
                AccessibilityObject.NotifyClients(accEvent, objectID, childID);
            }
            finally
            {
                raising_accessibility_notification = false;
            }
        }

        NotifyPlatformAccessibilityClients(accEvent, objectID, childID);
    }

    /// <summary>
    /// Raises the <see cref="QueryAccessibilityHelp"/> event.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnQueryAccessibilityHelp(QueryAccessibilityHelpEventArgs e)
        => (Events[s_queryAccessibilityHelpEvent] as QueryAccessibilityHelpEventHandler)?.Invoke(this, e);

    private void SetNullableAccessibilityString(int key, string? value)
    {
        if (value is null)
            Properties.RemoveObject(key);
        else
            Properties.SetObject(key, value);
    }

    private void AccessibilityObject_ClientNotification(object? sender, AccessibleObjectNotificationEventArgs e)
    {
        if (!raising_accessibility_notification)
            NotifyPlatformAccessibilityClients(e.EventId, e.ObjectId, e.ChildId);
    }

    private void NotifyPlatformAccessibilityClients(AccessibleEvents accEvent, int objectID, int childID)
    {
        var service = AvaloniaGlobals.GetService<IPlatformAccessibilityService>();
        var owner = FindWindow();

        if (service is null || owner is null)
            return;

        service.NotifyClients(owner.window, (int)accEvent, objectID, childID);
    }
}

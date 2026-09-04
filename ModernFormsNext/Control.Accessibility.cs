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
    private static readonly int s_accessibleAutomationIdProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleControlTypeProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleDescriptionProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleNameProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibleRoleProperty = PropertyStore.CreateKey();
    private static readonly int s_accessibilityViewProperty = PropertyStore.CreateKey();
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
    /// Gets or sets the stable developer-defined semantic automation identifier for this control.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value lets <see cref="AccessibleObject.AutomationId"/> fall
    /// back to <see cref="Name"/>. This identifier is distinct from the process-local
    /// <see cref="AccessibleObject.RuntimeId"/> and does not change the accessible name.
    /// </remarks>
    public string? AccessibleAutomationId
    {
        get => Properties.GetObject<string>(s_accessibleAutomationIdProperty);
        set
        {
            if (AccessibleAutomationId == value)
                return;

            SetNullableAccessibilityString(s_accessibleAutomationIdProperty, value);
            NotifyAccessibilityClients(AccessibleEvents.StateChange);
        }
    }

    /// <summary>
    /// Gets or sets the normalized platform-neutral semantic type for this control.
    /// </summary>
    /// <remarks>
    /// <see cref="AccessibleControlType.Default"/> lets <see cref="ControlAccessibleObject"/> infer a
    /// type while <see cref="AccessibleRole"/> continues to provide WinForms/MSAA compatibility.
    /// </remarks>
    public AccessibleControlType AccessibleControlType
    {
        get => Properties.GetEnum(s_accessibleControlTypeProperty, AccessibleControlType.Default);
        set
        {
            SourceGenerated.EnumValidator.Validate(value);

            if (AccessibleControlType == value)
                return;

            if (value == AccessibleControlType.Default)
                Properties.RemoveInteger(s_accessibleControlTypeProperty);
            else
                Properties.SetEnum(s_accessibleControlTypeProperty, value);

            NotifyAccessibilityClients(AccessibleEvents.StateChange);
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
    /// Gets or sets the accessibility-tree projection for this control.
    /// </summary>
    /// <remarks>
    /// <see cref="ModernFormsNext.Accessibility.AccessibilityView.Default"/> lets the framework infer
    /// a suitable projection. Setting <see cref="ModernFormsNext.Accessibility.AccessibilityView.Hidden"/>
    /// excludes the control from its parent's active accessibility children without changing the
    /// visual tree or <see cref="Visible"/> property.
    /// </remarks>
    public AccessibilityView AccessibilityView
    {
        get => Properties.GetEnum(s_accessibilityViewProperty, AccessibilityView.Default);
        set
        {
            SourceGenerated.EnumValidator.Validate(value);

            if (AccessibilityView == value)
                return;

            if (value == AccessibilityView.Default)
                Properties.RemoveInteger(s_accessibilityViewProperty);
            else
                Properties.SetEnum(s_accessibilityViewProperty, value);

            NotifyAccessibilityClients(AccessibleEvents.StateChange);
            Parent?.NotifyAccessibilityClients(AccessibleEvents.Reorder);
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

                // An accessible object can outlive its control when a platform adapter or test
                // retains the peer. Keep the notification route weak so the event subscription
                // does not defeat ControlAccessibleObject's weak owner contract.
                var notificationForwarder = new AccessibilityNotificationForwarder(this);
                accessibleObject.ClientNotification += notificationForwarder.OnClientNotification;
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

    private sealed class AccessibilityNotificationForwarder
    {
        private readonly WeakReference<Control> owner_reference;

        public AccessibilityNotificationForwarder(Control owner)
        {
            owner_reference = new WeakReference<Control>(owner);
        }

        public void OnClientNotification(object? sender, AccessibleObjectNotificationEventArgs e)
        {
            if (owner_reference.TryGetTarget(out Control? owner))
                owner.AccessibilityObject_ClientNotification(sender, e);
        }
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

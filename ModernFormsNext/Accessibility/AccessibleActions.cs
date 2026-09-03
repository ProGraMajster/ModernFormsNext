namespace ModernFormsNext.Accessibility;

/// <summary>
/// Specifies platform-neutral operations that an <see cref="AccessibleObject"/> can perform.
/// </summary>
/// <remarks>
/// <para>
/// These values describe framework behavior rather than Windows UI Automation patterns or Android
/// accessibility actions. Platform adapters translate supported values to their native equivalents.
/// </para>
/// <para>
/// Pass exactly one flag to <see cref="AccessibleObject.PerformAction(AccessibleActions, object?)"/>.
/// The flags form is used so callers can efficiently inspect <see cref="AccessibleObject.SupportedActions"/>.
/// </para>
/// </remarks>
[Flags]
public enum AccessibleActions
{
    /// <summary>
    /// The object exposes no semantic action.
    /// </summary>
    None = 0,

    /// <summary>
    /// Activates the object's primary command.
    /// </summary>
    Invoke = 1 << 0,

    /// <summary>
    /// Changes a two-state or three-state object to its next state.
    /// </summary>
    Toggle = 1 << 1,

    /// <summary>
    /// Selects the object in its owning container.
    /// </summary>
    Select = 1 << 2,

    /// <summary>
    /// Reveals the object's logical children or popup content.
    /// </summary>
    Expand = 1 << 3,

    /// <summary>
    /// Hides the object's logical children or popup content.
    /// </summary>
    Collapse = 1 << 4,

    /// <summary>
    /// Replaces the object's editable value using the action parameter.
    /// </summary>
    SetValue = 1 << 5,

    /// <summary>
    /// Increases a numeric value by its small-change amount.
    /// </summary>
    Increment = 1 << 6,

    /// <summary>
    /// Decreases a numeric value by its small-change amount.
    /// </summary>
    Decrement = 1 << 7,

    /// <summary>
    /// Scrolls the object or viewport according to the action parameter.
    /// </summary>
    Scroll = 1 << 8,

    /// <summary>
    /// Scrolls the containing viewport until the object is visible.
    /// </summary>
    ScrollIntoView = 1 << 9,

    /// <summary>
    /// Gives the object normal framework keyboard focus.
    /// </summary>
    Focus = 1 << 10
}


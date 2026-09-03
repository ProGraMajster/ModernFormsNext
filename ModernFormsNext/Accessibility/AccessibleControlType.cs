namespace ModernFormsNext.Accessibility;

/// <summary>
/// Identifies the normalized, platform-neutral kind of user-interface element represented by an
/// <see cref="AccessibleObject"/>.
/// </summary>
/// <remarks>
/// <see cref="AccessibleRole"/> preserves WinForms and MSAA-compatible role values. This type is the
/// canonical ModernFormsNext control classification used by future platform adapters and automation
/// consumers; it is intentionally not a copy of a Windows UI Automation enumeration.
/// </remarks>
public enum AccessibleControlType
{
    /// <summary>
    /// The framework should infer the control type from the represented object.
    /// </summary>
    Default,

    /// <summary>
    /// A custom element without a more specific normalized type.
    /// </summary>
    Custom,

    /// <summary>
    /// A top-level application window.
    /// </summary>
    Window,

    /// <summary>
    /// A structural pane within a window.
    /// </summary>
    Pane,

    /// <summary>
    /// A logical group of related elements.
    /// </summary>
    Group,

    /// <summary>
    /// Read-only textual content.
    /// </summary>
    Text,

    /// <summary>
    /// A command button.
    /// </summary>
    Button,

    /// <summary>
    /// A check box.
    /// </summary>
    CheckBox,

    /// <summary>
    /// A mutually exclusive radio button.
    /// </summary>
    RadioButton,

    /// <summary>
    /// A switch that changes between discrete states.
    /// </summary>
    Switch,

    /// <summary>
    /// An editable or read-only text field.
    /// </summary>
    Edit,

    /// <summary>
    /// A combo box with a popup list.
    /// </summary>
    ComboBox,

    /// <summary>
    /// A list container.
    /// </summary>
    List,

    /// <summary>
    /// An item in a list container.
    /// </summary>
    ListItem,

    /// <summary>
    /// A hierarchical tree container.
    /// </summary>
    Tree,

    /// <summary>
    /// An item in a hierarchical tree.
    /// </summary>
    TreeItem,

    /// <summary>
    /// A tab container.
    /// </summary>
    Tab,

    /// <summary>
    /// A selectable tab header.
    /// </summary>
    TabItem,

    /// <summary>
    /// A menu container.
    /// </summary>
    Menu,

    /// <summary>
    /// A command or submenu entry in a menu.
    /// </summary>
    MenuItem,

    /// <summary>
    /// A slider with a user-adjustable numeric value.
    /// </summary>
    Slider,

    /// <summary>
    /// A read-only progress indicator.
    /// </summary>
    ProgressBar,

    /// <summary>
    /// A horizontal or vertical scroll bar.
    /// </summary>
    ScrollBar,

    /// <summary>
    /// A dialog window.
    /// </summary>
    Dialog,

    /// <summary>
    /// An image or other non-text graphic.
    /// </summary>
    Image,

    /// <summary>
    /// A toolbar containing commands.
    /// </summary>
    ToolBar,

    /// <summary>
    /// A visual or semantic separator.
    /// </summary>
    Separator
}

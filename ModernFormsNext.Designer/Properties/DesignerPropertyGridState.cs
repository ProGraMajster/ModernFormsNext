using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using System.ComponentModel;
using System.Reflection;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyGridState
{
    private const int MinimumControlSize = 8;

    private static readonly string[] CategoryOrder =
    [
        "Design",
        "Layout",
        "Appearance",
        "Behavior",
        "Data",
        "Misc"
    ];

    private static readonly HashSet<string> UnsupportedLayoutProperties = new(StringComparer.Ordinal)
    {
    };

    private static readonly FixedEventDefinition[] FixedEvents =
    [
        new("Click", "Click", "Action", "Occurs when the control is clicked."),
        new("MouseDown", "MouseDown", "Mouse", "Occurs when a mouse button is pressed over the control."),
        new("MouseMove", "MouseMove", "Mouse", "Occurs when the mouse pointer moves over the control."),
        new("MouseUp", "MouseUp", "Mouse", "Occurs when a mouse button is released over the control."),
        new("KeyDown", "KeyDown", "Keyboard", "Occurs when a key is pressed while the control has focus."),
        new("KeyUp", "KeyUp", "Keyboard", "Occurs when a key is released while the control has focus."),
        new("TextChanged", "TextChanged", "Property Changed", "Occurs when the Text property changes."),
        new("SizeChanged", "SizeChanged", "Property Changed", "Occurs when the control size changes.")
    ];

    private readonly DesignerSession playgroundState;
    private readonly Func<IReadOnlyList<DesignerPropertyDescriptor>>? detachedPropertyProvider;
    private readonly Func<string>? detachedHeaderName;
    private readonly Func<string>? detachedHeaderType;
    private readonly DesignMetadataReader metadataReader = new();
    private readonly HashSet<string> expandedProperties = new(StringComparer.Ordinal)
    {
        "Bounds",
        "Location",
        "Size"
    };
    private DesignDocument? lastDocument;
    private DesignControlNode? lastSelectedNode;
    private bool hasSelectionSnapshot;

    public DesignerPropertyGridState(DesignerSession playgroundState)
    {
        this.playgroundState = playgroundState;
        playgroundState.SelectionChanged += (_, _) => Refresh();
        playgroundState.DocumentChanged += (_, _) => Refresh();
        Refresh();
    }

    internal DesignerPropertyGridState(
        DesignerSession playgroundState,
        Func<string> headerName,
        Func<string> headerType,
        Func<IReadOnlyList<DesignerPropertyDescriptor>> propertyProvider)
    {
        this.playgroundState = playgroundState ?? throw new ArgumentNullException(nameof(playgroundState));
        detachedHeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
        detachedHeaderType = headerType ?? throw new ArgumentNullException(nameof(headerType));
        detachedPropertyProvider = propertyProvider ?? throw new ArgumentNullException(nameof(propertyProvider));
        Refresh();
    }

    public event EventHandler? Changed;

    public DesignerPropertyGridMode Mode { get; private set; } = DesignerPropertyGridMode.Properties;

    public DesignerPropertySortMode SortMode { get; private set; } = DesignerPropertySortMode.Categorized;

    public string HeaderName { get; private set; } = "No selection";

    public DesignerSession Session => playgroundState;

    public bool SupportsEvents => detachedPropertyProvider is null;

    public string HeaderType { get; private set; } = string.Empty;

    public IReadOnlyList<DesignerPropertyDescriptor> Properties { get; private set; } = [];

    public IReadOnlyList<DesignerEventDescriptor> Events { get; private set; } = [];

    public IReadOnlyList<DesignerPropertyGridRow> Rows { get; private set; } = [];

    public DesignerPropertyDescriptor? SelectedProperty { get; private set; }

    public DesignerEventDescriptor? SelectedEvent { get; private set; }

    public DesignerPropertyDescriptor? EditingProperty { get; private set; }

    public DesignerEventDescriptor? EditingEvent { get; private set; }

    public string EditingText { get; private set; } = string.Empty;

    public int EditingSelectionStart { get; private set; } = -1;

    public int EditingSelectionEnd { get; private set; } = -1;

    public int EditingCaretIndex { get; private set; }

    public bool IsEditing { get; private set; }

    public string DescriptionTitle
        => Mode == DesignerPropertyGridMode.Events
            ? SelectedEvent?.DisplayName ?? "Event"
            : SelectedProperty?.DisplayName ?? "Property";

    public string DescriptionText
        => Mode == DesignerPropertyGridMode.Events
            ? SelectedEvent?.Description ?? "Select an event to see its description."
            : SelectedProperty?.Description ?? "Select a property to see its description.";

    public void SetMode(DesignerPropertyGridMode mode)
    {
        if (mode == DesignerPropertyGridMode.Events && !SupportsEvents)
            return;

        if (Mode == mode)
            return;

        Mode = mode;
        RebuildRows();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSortMode(DesignerPropertySortMode sortMode)
    {
        if (SortMode == sortMode)
            return;

        SortMode = sortMode;
        RebuildRows();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleExpansion(DesignerPropertyDescriptor property)
    {
        if (!property.HasChildren)
            return;

        if (!expandedProperties.Add(property.Identity))
            expandedProperties.Remove(property.Identity);

        ApplyExpansionState(Properties);
        RebuildRows();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectRow(DesignerPropertyGridRow row)
    {
        if (IsEditing)
            CancelEditing();

        if (row.Property is not null)
        {
            SelectedProperty = row.Property;
            Mode = DesignerPropertyGridMode.Properties;
        }
        else if (row.Event is not null)
        {
            SelectedEvent = row.Event;
            Mode = DesignerPropertyGridMode.Events;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void BeginEditing()
    {
        if (Mode == DesignerPropertyGridMode.Properties && SelectedProperty is { IsReadOnly: false } property)
        {
            EditingProperty = property;
            EditingEvent = null;
            EditingText = property.GetValueText();
            IsEditing = true;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Mode == DesignerPropertyGridMode.Events && SelectedEvent is { } eventDescriptor)
        {
            EditingProperty = null;
            EditingEvent = eventDescriptor;
            EditingText = eventDescriptor.GetValueText();
            IsEditing = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateEditingText(string text)
    {
        if (!IsEditing || EditingText == text)
            return;

        EditingText = text;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateEditingSelection(int selectionStart, int selectionEnd, int caretIndex)
    {
        if (!IsEditing)
            return;

        if (EditingSelectionStart == selectionStart
            && EditingSelectionEnd == selectionEnd
            && EditingCaretIndex == caretIndex)
        {
            return;
        }

        EditingSelectionStart = selectionStart;
        EditingSelectionEnd = selectionEnd;
        EditingCaretIndex = caretIndex;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelEditing()
    {
        if (!IsEditing)
            return;

        ClearEditingState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool CommitEditing()
    {
        if (!IsEditing)
            return false;

        var text = EditingText;
        var committed = CommitSelectedValue(text);

        if (!committed)
            return false;

        EditingProperty = null;
        EditingEvent = null;
        EditingText = string.Empty;
        IsEditing = false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ToggleSelectedBoolean()
    {
        if (Mode != DesignerPropertyGridMode.Properties || SelectedProperty is not { IsReadOnly: false } property)
            return false;

        var type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;

        if (type != typeof(bool))
            return false;

        var current = property.GetValue() is bool boolValue && boolValue;
        return CommitSelectedProperty(current ? "False" : "True");
    }

    public bool CommitSelectedValue(string text)
    {
        if (Mode == DesignerPropertyGridMode.Properties)
            return CommitSelectedProperty(text);

        return CommitSelectedEvent(text);
    }

    public bool TryCreateDefaultEventHandler(
        out DesignerEventDescriptor? eventDescriptor,
        out string handlerName)
    {
        eventDescriptor = SelectedEvent;
        handlerName = string.Empty;

        if (Mode != DesignerPropertyGridMode.Events || eventDescriptor is null)
            return false;

        if (!string.IsNullOrWhiteSpace(eventDescriptor.GetValueText()))
            return false;

        handlerName = CreateDefaultEventHandlerName(HeaderName, eventDescriptor.Name);

        if (!eventDescriptor.TryCommit(handlerName, out var error))
        {
            playgroundState.Log($"Event '{eventDescriptor.DisplayName}' was not changed: {error}");
            return false;
        }

        playgroundState.NotifyDocumentChanged();
        playgroundState.Log($"Added event {HeaderName}.{eventDescriptor.DisplayName} -> {handlerName}.");
        return true;
    }

    public void Refresh()
    {
        if (detachedPropertyProvider is not null)
        {
            RefreshDetachedProperties();
            return;
        }

        var document = playgroundState.Document;
        var selectedNode = playgroundState.SelectedNode;
        var selectedObjectChanged = hasSelectionSnapshot
            && (!ReferenceEquals(lastDocument, document) || !ReferenceEquals(lastSelectedNode, selectedNode));
        var selectedPropertyName = SelectedProperty?.Identity;
        var selectedEventName = SelectedEvent?.Name;
        var editingPropertyName = EditingProperty?.Identity;
        var editingEventName = EditingEvent?.Name;

        if (selectedObjectChanged && IsEditing)
            ClearEditingState();

        HeaderName = playgroundState.SelectedNode?.Name ?? playgroundState.Document.FormName;
        HeaderType = GetSelectedObjectTypeName();
        Properties = BuildPropertyDescriptors();
        Events = BuildEventDescriptors();
        ApplyExpansionState(Properties);
        SelectedProperty = FindPropertyByIdentity(selectedPropertyName)
            ?? Properties.FirstOrDefault();
        SelectedEvent = Events.FirstOrDefault(eventDescriptor => eventDescriptor.Name == selectedEventName)
            ?? Events.FirstOrDefault();
        EditingProperty = IsEditing && editingPropertyName is not null
            ? FindPropertyByIdentity(editingPropertyName)
            : null;
        EditingEvent = IsEditing && editingEventName is not null
            ? Events.FirstOrDefault(eventDescriptor => eventDescriptor.Name == editingEventName)
            : null;

        if (IsEditing && EditingProperty is null && EditingEvent is null)
        {
            IsEditing = false;
            EditingText = string.Empty;
        }

        RebuildRows();
        lastDocument = document;
        lastSelectedNode = selectedNode;
        hasSelectionSnapshot = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshDetachedProperties()
    {
        string? selectedPropertyName = SelectedProperty?.Identity;
        string? editingPropertyName = EditingProperty?.Identity;

        HeaderName = detachedHeaderName!();
        HeaderType = detachedHeaderType!();
        Properties = detachedPropertyProvider!()
            .Where(property => property.IsVisible)
            .ToArray();
        Events = [];
        ApplyExpansionState(Properties);
        SelectedProperty = FindPropertyByIdentity(selectedPropertyName)
            ?? Properties.FirstOrDefault();
        SelectedEvent = null;
        EditingProperty = IsEditing && editingPropertyName is not null
            ? FindPropertyByIdentity(editingPropertyName)
            : null;
        EditingEvent = null;

        if (IsEditing && EditingProperty is null)
            ClearEditingState();

        Mode = DesignerPropertyGridMode.Properties;
        RebuildRows();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ClearEditingState()
    {
        EditingProperty = null;
        EditingEvent = null;
        EditingText = string.Empty;
        EditingSelectionStart = -1;
        EditingSelectionEnd = -1;
        EditingCaretIndex = 0;
        IsEditing = false;
    }

    private bool CommitSelectedProperty(string text)
    {
        if (SelectedProperty is null)
            return false;

        if (!SelectedProperty.TryCommit(text, out var error))
        {
            playgroundState.Log($"Property '{SelectedProperty.DisplayName}' was not changed: {error}");
            return false;
        }

        var propertyName = SelectedProperty.DisplayName;
        if (detachedPropertyProvider is null)
            playgroundState.NotifyDocumentChanged();
        playgroundState.Log($"Updated {HeaderName}.{propertyName}.");
        return true;
    }

    private bool CommitSelectedEvent(string text)
    {
        if (SelectedEvent is null)
            return false;

        if (!SelectedEvent.TryCommit(text, out var error))
        {
            playgroundState.Log($"Event '{SelectedEvent.DisplayName}' was not changed: {error}");
            return false;
        }

        playgroundState.NotifyDocumentChanged();
        playgroundState.Log($"Updated event {HeaderName}.{SelectedEvent.DisplayName}.");
        return true;
    }

    private void RebuildRows()
    {
        Rows = Mode == DesignerPropertyGridMode.Events
            ? BuildEventRows()
            : BuildPropertyRows();
    }

    private IReadOnlyList<DesignerPropertyGridRow> BuildPropertyRows()
    {
        var visibleProperties = Properties
            .Where(property => property.IsVisible)
            .ToArray();

        if (SortMode == DesignerPropertySortMode.Alphabetical)
        {
            return visibleProperties
                .OrderBy(property => property.DisplayName, StringComparer.Ordinal)
                .ThenBy(property => property.Name, StringComparer.Ordinal)
                .SelectMany(FlattenPropertyRows)
                .ToArray();
        }

        return visibleProperties
            .GroupBy(property => property.Category)
            .OrderBy(group => GetCategorySortKey(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => new[] { new DesignerPropertyGridRow(group.Key) }
                .Concat(group
                    .OrderBy(property => property.DisplayName, StringComparer.Ordinal)
                    .ThenBy(property => property.Name, StringComparer.Ordinal)
                    .SelectMany(FlattenPropertyRows)))
            .ToArray();
    }

    private IEnumerable<DesignerPropertyGridRow> FlattenPropertyRows(DesignerPropertyDescriptor property)
    {
        yield return new DesignerPropertyGridRow(property);

        if (!property.IsExpanded)
            yield break;

        foreach (var child in property.Children.Where(child => child.IsVisible))
        {
            foreach (var row in FlattenPropertyRows(child))
                yield return row;
        }
    }

    private IReadOnlyList<DesignerPropertyGridRow> BuildEventRows()
    {
        if (SortMode == DesignerPropertySortMode.Alphabetical)
        {
            return Events
                .OrderBy(eventDescriptor => eventDescriptor.DisplayName, StringComparer.Ordinal)
                .ThenBy(eventDescriptor => eventDescriptor.Name, StringComparer.Ordinal)
                .Select(eventDescriptor => new DesignerPropertyGridRow(eventDescriptor))
                .ToArray();
        }

        return Events
            .GroupBy(eventDescriptor => eventDescriptor.Category)
            .OrderBy(group => GetCategorySortKey(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => new[] { new DesignerPropertyGridRow(group.Key) }
                .Concat(group
                    .OrderBy(eventDescriptor => eventDescriptor.DisplayName, StringComparer.Ordinal)
                    .ThenBy(eventDescriptor => eventDescriptor.Name, StringComparer.Ordinal)
                    .Select(eventDescriptor => new DesignerPropertyGridRow(eventDescriptor))))
            .ToArray();
    }

    private IReadOnlyList<DesignerPropertyDescriptor> BuildPropertyDescriptors()
    {
        var node = playgroundState.SelectedNode;
        var descriptors = new List<DesignerPropertyDescriptor>();

        if (node is null)
            AddRootDescriptors(descriptors);
        else
            AddNodeDescriptors(descriptors, node);

        AddMetadataDescriptors(descriptors, node);

        return descriptors
            .Where(property => property.IsVisible)
            .ToArray();
    }

    private void ApplyExpansionState(IEnumerable<DesignerPropertyDescriptor> properties)
    {
        foreach (var property in properties)
        {
            property.IsExpanded = expandedProperties.Contains(property.Identity);
            ApplyExpansionState(property.Children);
        }
    }

    private DesignerPropertyDescriptor? FindPropertyByIdentity(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return null;

        return EnumerateProperties(Properties)
            .FirstOrDefault(property => string.Equals(property.Identity, identity, StringComparison.Ordinal));
    }

    private static IEnumerable<DesignerPropertyDescriptor> EnumerateProperties(IEnumerable<DesignerPropertyDescriptor> properties)
    {
        foreach (var property in properties)
        {
            yield return property;

            foreach (var child in EnumerateProperties(property.Children))
                yield return child;
        }
    }

    private void AddFormDescriptors(List<DesignerPropertyDescriptor> descriptors)
    {
        AddCommonRootDescriptors(descriptors, isUserControl: false);
        descriptors.Add(FormProperty("StartPosition", "StartPosition", "Layout", "The initial position used when the form is shown.", typeof(FormStartPosition), FormStartPosition.CenterScreen));
        descriptors.Add(FormProperty("WindowState", "WindowState", "Layout", "The initial window state.", typeof(FormWindowState), FormWindowState.Normal));
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "Text",
            DisplayName = "Text",
            Category = "Appearance",
            Description = "Text displayed by the form title.",
            ValueType = typeof(string),
            GetValue = () => playgroundState.Document.FormName,
            CommitText = text =>
            {
                playgroundState.Document.FormName = text;
                return (true, null);
            }
        });
        descriptors.Add(FormProperty("AllowMaximize", "AllowMaximize", "Behavior", "Determines whether the form title bar allows maximizing.", typeof(bool), true));
        descriptors.Add(FormProperty("AllowMinimize", "AllowMinimize", "Behavior", "Determines whether the form title bar allows minimizing.", typeof(bool), true));
        descriptors.Add(FormProperty("Resizeable", "Resizeable", "Behavior", "Determines whether the form can be resized by the user.", typeof(bool), true));
        descriptors.Add(FormProperty("UseSystemDecorations", "UseSystemDecorations", "Appearance", "Determines whether the operating system draws the form decorations.", typeof(bool), false));
        descriptors.Add(ReadOnly("Enabled", "Enabled", "Behavior", "The form is enabled in the playground.", typeof(bool), () => true));
        descriptors.Add(ReadOnly("Visible", "Visible", "Behavior", "The form is visible in the playground.", typeof(bool), () => true));
    }

    private void AddRootDescriptors(List<DesignerPropertyDescriptor> descriptors)
    {
        if (playgroundState.Document.RootKind != DesignRootKind.UserControl)
        {
            AddFormDescriptors(descriptors);
            return;
        }

        AddUserControlDescriptors(descriptors);
    }

    private void AddUserControlDescriptors(List<DesignerPropertyDescriptor> descriptors)
    {
        AddCommonRootDescriptors(descriptors, isUserControl: true);
        descriptors.Add(FormProperty("Text", "Text", "Appearance", "Text associated with the UserControl.", typeof(string), string.Empty));
        descriptors.Add(FormProperty("Dock", "Dock", "Layout", "Determines how the UserControl docks when its generated defaults are used.", typeof(DockStyle), DockStyle.None));
        descriptors.Add(FormProperty("Anchor", "Anchor", "Layout", "Determines the edges to which the UserControl is anchored.", typeof(AnchorStyles), AnchorStyles.Top | AnchorStyles.Left));
        descriptors.Add(FormProperty("Padding", "Padding", "Layout", "Spacing inside the UserControl in logical pixels.", typeof(Padding), Padding.Empty));
        descriptors.Add(FormProperty("Margin", "Margin", "Layout", "Spacing outside the UserControl in logical pixels.", typeof(Padding), new Padding(3)));
        descriptors.Add(FormProperty("MinimumSize", "MinimumSize", "Layout", "The minimum UserControl size in logical pixels.", typeof(System.Drawing.Size), System.Drawing.Size.Empty));
        descriptors.Add(FormProperty("MaximumSize", "MaximumSize", "Layout", "The maximum UserControl size in logical pixels.", typeof(System.Drawing.Size), System.Drawing.Size.Empty));
        descriptors.Add(FormProperty("AutoScroll", "AutoScroll", "Layout", "Determines whether scrollbars appear for content beyond the bounds.", typeof(bool), false));
        descriptors.Add(FormProperty("AutoSize", "AutoSize", "Layout", "Determines whether the UserControl sizes itself to its content.", typeof(bool), false));
        descriptors.Add(FormProperty("Enabled", "Enabled", "Behavior", "Determines whether the UserControl can receive user interaction.", typeof(bool), true));
        descriptors.Add(FormProperty("Visible", "Visible", "Behavior", "Determines whether the UserControl is visible at runtime.", typeof(bool), true));
        descriptors.Add(FormProperty("TabStop", "TabStop", "Behavior", "Determines whether keyboard navigation can focus the UserControl itself.", typeof(bool), false));
    }

    private void AddCommonRootDescriptors(
        List<DesignerPropertyDescriptor> descriptors,
        bool isUserControl)
    {
        var rootDisplayName = isUserControl ? "UserControl" : "form";
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "Name",
            DisplayName = "(Name)",
            Category = "Design",
            Description = isUserControl
                ? "The UserControl name assigned by generated initialization code."
                : "The form name used by the designer and generated code.",
            ValueType = typeof(string),
            GetValue = () => playgroundState.Document.FormName,
            CommitText = text =>
            {
                var value = text.Trim();

                if (string.IsNullOrWhiteSpace(value))
                    return (false, $"The {rootDisplayName} name cannot be empty.");

                playgroundState.Document.FormName = value;
                return (true, null);
            }
        });
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "Namespace",
            DisplayName = "Namespace",
            Category = "Design",
            Description = "The C# namespace used by generated designer code.",
            ValueType = typeof(string),
            GetValue = () => playgroundState.Document.Namespace,
            CommitText = text =>
            {
                var value = text.Trim();

                if (!string.IsNullOrWhiteSpace(value)
                    && !value.Split('.').All(DesignDocumentValidator.IsValidCSharpIdentifier))
                {
                    return (false, "The namespace must be a valid C# namespace.");
                }

                playgroundState.Document.Namespace = value;
                return (true, null);
            }
        });
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "ClassName",
            DisplayName = "ClassName",
            Category = "Design",
            Description = isUserControl
                ? "The C# partial UserControl class name generated for this document."
                : "The C# partial class name generated for this form.",
            ValueType = typeof(string),
            GetValue = () => playgroundState.Document.ClassName,
            CommitText = text =>
            {
                var value = text.Trim();

                if (string.IsNullOrWhiteSpace(value))
                    return (false, "The class name cannot be empty.");

                if (!DesignDocumentValidator.IsValidCSharpIdentifier(value))
                    return (false, "The class name must be a valid C# identifier.");

                playgroundState.Document.ClassName = value;
                return (true, null);
            }
        });
        descriptors.Add(ReadOnly("Type", "Type", "Design", "The runtime object type.", typeof(string), playgroundState.GetRootTypeName));
        descriptors.Add(ReadOnly(
            "MemberVisibility",
            "MemberVisibility",
            "Design",
            isUserControl ? "Design roots do not emit a separate designer field." : "Forms do not emit a separate designer field.",
            typeof(DesignerMemberVisibility),
            () => DesignerMemberVisibility.None));
        var originDescription = isUserControl
            ? "The root origin is controlled by its parent when the UserControl is used."
            : "The form origin is controlled by the host window.";
        descriptors.Add(ReadOnly("X", "X", "Layout", originDescription, typeof(int), () => 0));
        descriptors.Add(ReadOnly("Y", "Y", "Layout", originDescription, typeof(int), () => 0));
        descriptors.Add(DesignerPropertyDescriptorFactory.CreateDocumentSize(playgroundState.Document));
        descriptors.Add(InteractionEffectsProperty(playgroundState.Document.Properties));
        AddAnimationDescriptors(descriptors, playgroundState.Document.Properties);
        descriptors.Add(FormSize("Width", "Width", size => size.Width, (width, height) => new DesignSize(width, height)));
        descriptors.Add(FormSize("Height", "Height", size => size.Height, (width, height) => new DesignSize(width, height)));
    }

    private void AddNodeDescriptors(List<DesignerPropertyDescriptor> descriptors, DesignControlNode node)
    {
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "Name",
            DisplayName = "(Name)",
            Category = "Design",
            Description = "The field name used by the designer and generated code.",
            ValueType = typeof(string),
            GetValue = () => node.Name,
            CommitText = text =>
            {
                var value = text.Trim();

                if (string.IsNullOrWhiteSpace(value))
                    return (false, "The control name cannot be empty.");

                if (!DesignDocumentValidator.IsValidCSharpIdentifier(value))
                    return (false, "The control name must be a valid C# identifier.");

                if (playgroundState.EnumerateNodes().Any(item => !ReferenceEquals(item.Node, node) && item.Node.Name == value))
                    return (false, $"A control named '{value}' already exists.");

                node.Name = value;
                return (true, null);
            }
        });

        descriptors.Add(ReadOnly("Type", "Type", "Design", "The runtime control type.", typeof(string), () => GetNodeTypeName(node)));
        descriptors.Add(new DesignerPropertyDescriptor
        {
            Name = "MemberVisibility",
            DisplayName = "MemberVisibility",
            Category = "Design",
            Description = "Controls whether the designer emits a field and which access modifier it uses.",
            ValueType = typeof(DesignerMemberVisibility),
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(typeof(DesignerMemberVisibility)),
            GetValue = () => node.MemberVisibility,
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(DesignerMemberVisibility), out var value, out var error))
                    return (false, error);

                node.MemberVisibility = (DesignerMemberVisibility)value!;
                return (true, null);
            }
        });

        descriptors.Add(DesignerPropertyDescriptorFactory.CreateNodeBounds(node));
        descriptors.Add(DesignerPropertyDescriptorFactory.CreateNodeLocation(node));
        descriptors.Add(DesignerPropertyDescriptorFactory.CreateNodeSize(node));
        descriptors.Add(Bounds("X", "X", "Horizontal position in logical pixels.", bounds => bounds.X, (bounds, value) => new DesignBounds(value, bounds.Y, bounds.Width, bounds.Height)));
        descriptors.Add(Bounds("Y", "Y", "Vertical position in logical pixels.", bounds => bounds.Y, (bounds, value) => new DesignBounds(bounds.X, value, bounds.Width, bounds.Height)));
        descriptors.Add(Bounds("Width", "Width", "Width in logical pixels.", bounds => bounds.Width, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, value, bounds.Height), requirePositive: true));
        descriptors.Add(Bounds("Height", "Height", "Height in logical pixels.", bounds => bounds.Height, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, bounds.Width, value), requirePositive: true));

        AddSpecialContainerDescriptors(descriptors, node);

        if (ShouldShowModelProperty(node, "Text"))
            descriptors.Add(NodeProperty(node, "Text", "Text", "Appearance", "Text displayed by the control.", typeof(string), string.Empty));

        if (ShouldShowModelProperty(node, "Enabled"))
            descriptors.Add(NodeProperty(node, "Enabled", "Enabled", "Behavior", "Determines whether the control can receive user interaction.", typeof(bool), true));

        if (ShouldShowModelProperty(node, "Visible"))
            descriptors.Add(NodeProperty(node, "Visible", "Visible", "Behavior", "Determines whether the control is visible.", typeof(bool), true));
    }

    private void AddSpecialContainerDescriptors(List<DesignerPropertyDescriptor> descriptors, DesignControlNode node)
    {
        descriptors.Add(InteractionEffectsProperty(node.Properties));
        AddAnimationDescriptors(descriptors, node.Properties);

        if (DesignerSpecialContainers.IsTabControl(node))
            descriptors.Add(TabPagesProperty(node));

        var parent = playgroundState.FindParent(node);

        if (parent is not null && DesignerSpecialContainers.IsTableLayoutPanel(parent))
        {
            descriptors.Add(TableLayoutChildProperty(
                node,
                DesignerSpecialContainers.TableColumnPropertyName,
                "Column",
                "The zero-based TableLayoutPanel column that contains this control.",
                defaultValue: 0,
                minimumValue: 0));
            descriptors.Add(TableLayoutChildProperty(
                node,
                DesignerSpecialContainers.TableRowPropertyName,
                "Row",
                "The zero-based TableLayoutPanel row that contains this control.",
                defaultValue: 0,
                minimumValue: 0));
            descriptors.Add(TableLayoutChildProperty(
                node,
                DesignerSpecialContainers.TableColumnSpanPropertyName,
                "ColumnSpan",
                "The number of TableLayoutPanel columns spanned by this control.",
                defaultValue: 1,
                minimumValue: 1));
            descriptors.Add(TableLayoutChildProperty(
                node,
                DesignerSpecialContainers.TableRowSpanPropertyName,
                "RowSpan",
                "The number of TableLayoutPanel rows spanned by this control.",
                defaultValue: 1,
                minimumValue: 1));
        }
    }

    private static void AddAnimationDescriptors(
        List<DesignerPropertyDescriptor> descriptors,
        IDictionary<string, DesignPropertyValue> properties)
    {
        descriptors.Add(TransitionProperty(properties, isLayout: true));
        descriptors.Add(TransitionProperty(properties, isLayout: false));
    }

    private static DesignerPropertyDescriptor TransitionProperty(
        IDictionary<string, DesignPropertyValue> properties,
        bool isLayout)
    {
        string name = isLayout
            ? LayoutTransitionDesignValue.PropertyName
            : VisualStateTransitionDesignValue.PropertyName;
        return new DesignerPropertyDescriptor
        {
            Name = name,
            DisplayName = isLayout ? "Layout Transition" : "Visual State Transitions",
            Category = isLayout ? "Behavior" : "Appearance",
            Description = isLayout
                ? "Configures logical-to-presentation bounds animation using the runtime LayoutTransition API."
                : "Configures ordered runtime visual-state transition pairs.",
            ValueType = typeof(string),
            IsReadOnly = true,
            HasDialogEditor = true,
            DialogEditor = DesignerPropertyDialogEditors.Transition(properties, isLayout),
            GetValue = () =>
            {
                if (!properties.TryGetValue(name, out var value))
                    return "(none)";
                if (isLayout)
                {
                    return LayoutTransitionDesignValue.TryRead(value, out bool enabled, out double duration, out _, out _)
                        ? enabled ? $"{duration:0.##} ms" : "Disabled"
                        : "(invalid)";
                }
                return VisualStateTransitionDesignValue.TryRead(value, out var transitions, out _)
                    ? $"{transitions.Count} transition{(transitions.Count == 1 ? string.Empty : "s")}" : "(invalid)";
            }
        };
    }

    private static DesignerPropertyDescriptor InteractionEffectsProperty(
        IDictionary<string, DesignPropertyValue> properties)
        => new()
        {
            Name = InteractionEffectDesignValue.PropertyName,
            DisplayName = "InteractionEffects",
            Category = "Behavior",
            Description = "Edits ordered built-in and explicitly source-described interaction effects without running project code in the Designer.",
            ValueType = typeof(string),
            IsReadOnly = true,
            HasDialogEditor = true,
            DialogEditor = DesignerPropertyDialogEditors.InteractionEffects(properties),
            GetValue = () =>
            {
                properties.TryGetValue(InteractionEffectDesignValue.PropertyName, out DesignPropertyValue? value);
                return InteractionEffectDesignValue.TryRead(value, out IReadOnlyList<DesignPropertyValue> effects, out _)
                    ? $"{effects.Count} effect{(effects.Count == 1 ? string.Empty : "s")}"
                    : "(invalid)";
            }
        };

    private static DesignerPropertyDescriptor TabPagesProperty(DesignControlNode tabControl)
        => new()
        {
            Name = "TabPages",
            DisplayName = "TabPages",
            Category = "Data",
            Description = "Opens the TabPage collection editor for this TabControl.",
            ValueType = typeof(string),
            IsReadOnly = true,
            ShouldSerialize = false,
            HasDialogEditor = true,
            DialogEditor = DesignerPropertyDialogEditors.TabPages(tabControl),
            GetValue = () =>
            {
                var count = tabControl.Children.Count(DesignerSpecialContainers.IsTabPage);
                return $"{count} TabPage{(count == 1 ? string.Empty : "s")}";
            }
        };

    private static DesignerPropertyDescriptor TableLayoutChildProperty(
        DesignControlNode node,
        string propertyName,
        string displayName,
        string description,
        int defaultValue,
        int minimumValue)
        => new()
        {
            Name = propertyName,
            DisplayName = displayName,
            Category = "Layout",
            Description = description,
            ValueType = typeof(int),
            GetValue = () => DesignerSpecialContainers.GetInt(node, propertyName, defaultValue),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                var intValue = (int)value!;

                if (intValue < minimumValue)
                    return (false, $"{displayName} must be at least {minimumValue}.");

                DesignerSpecialContainers.SetInt(node, propertyName, intValue);
                return (true, null);
            }
        };

    private void AddMetadataDescriptors(List<DesignerPropertyDescriptor> descriptors, DesignControlNode? node)
    {
        if (node is null || playgroundState.IsProjectUserControlType(node.TypeName))
            return;

        var controlType = playgroundState.ResolveControlType(node);

        if (controlType is null)
            return;

        var existingNames = new HashSet<string>(descriptors.Select(property => property.Name), StringComparer.Ordinal);
        var existingDisplayNames = new HashSet<string>(descriptors.Select(property => property.DisplayName), StringComparer.Ordinal);
        var defaultInstance = TryCreateDefaultControlInstance(controlType);

        foreach (var metadata in metadataReader.ReadControl(controlType).Properties)
        {
            if (metadata.IsHidden || existingNames.Contains(metadata.Name) || existingDisplayNames.Contains(metadata.DisplayName))
                continue;

            var fallback = metadata.HasDefaultValue
                ? metadata.DefaultValue
                : TryReadDefaultPropertyValue(defaultInstance, metadata.Property);
            fallback = NormalizeDesignerFallback(metadata.Name, metadata.PropertyType, fallback);
            var category = metadata.Category ?? GuessPropertyCategory(metadata.Name, metadata.PropertyType);
            if (TryCreateBoundsAliasDescriptor(node, metadata.Name, metadata.DisplayName, metadata.Description, out var boundsAliasDescriptor))
            {
                descriptors.Add(boundsAliasDescriptor);
                existingNames.Add(metadata.Name);
                existingDisplayNames.Add(metadata.DisplayName);
                continue;
            }

            var unsupported = IsUnsupportedLayoutProperty(metadata.Name);
            var description = GetPropertyDescription(
                metadata.Description ?? "Designer metadata does not provide a description for this property.",
                unsupported);
            var complexCanEdit = metadata.Serialize
                && !unsupported
                && (!metadata.ReadOnly || (Nullable.GetUnderlyingType(metadata.PropertyType) ?? metadata.PropertyType) == typeof(ControlStyle));

            if (DesignerPropertyDescriptorFactory.TryCreateRuntimeDescriptor(
                node,
                metadata.Name,
                metadata.DisplayName,
                category,
                description,
                metadata.PropertyType,
                fallback,
                complexCanEdit,
                unsupported || metadata.Visibility == DesignPropertyVisibility.Advanced,
                out var complexDescriptor))
            {
                descriptors.Add(complexDescriptor!);
                existingNames.Add(metadata.Name);
                existingDisplayNames.Add(metadata.DisplayName);
                continue;
            }

            var simpleType = DesignMetadataReader.IsSimpleSerializableType(metadata.PropertyType);
            var editable = simpleType && metadata.Serialize && !metadata.ReadOnly && !unsupported;

            descriptors.Add(new DesignerPropertyDescriptor
            {
                Name = metadata.Name,
                DisplayName = metadata.DisplayName,
                Category = category,
                Description = description,
                ValueType = metadata.PropertyType,
                IsReadOnly = !editable,
                IsAdvanced = unsupported || metadata.Visibility == DesignPropertyVisibility.Advanced,
                ShouldSerialize = metadata.Serialize,
                StandardValues = DesignerPropertyValueEditor.GetStandardValues(metadata.PropertyType),
                GetValue = () => GetNodePropertyValue(node, metadata.Name, metadata.PropertyType, fallback),
                CommitText = text =>
                {
                    if (!editable)
                        return (false, "This property is not editable by the MVP property grid.");

                    if (!DesignerPropertyValueEditor.TryConvert(text, metadata.PropertyType, out var value, out var error))
                        return (false, error);

                    node.Properties[metadata.Name] = DesignerPropertyValueEditor.ToDesignPropertyValue(value, metadata.PropertyType);
                    return (true, null);
                }
            });
        }

        AddRuntimeReflectionDescriptors(descriptors, node, controlType, defaultInstance);
    }

    private void AddRuntimeReflectionDescriptors(
        List<DesignerPropertyDescriptor> descriptors,
        DesignControlNode node,
        Type controlType,
        object? defaultInstance)
    {
        var existingNames = new HashSet<string>(descriptors.Select(property => property.Name), StringComparer.Ordinal);
        var existingDisplayNames = new HashSet<string>(descriptors.Select(property => property.DisplayName), StringComparer.Ordinal);

        foreach (var property in controlType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsInspectableRuntimeProperty)
            .OrderBy(property => GuessPropertyCategory(property.Name, property.PropertyType), StringComparer.Ordinal)
            .ThenBy(property => property.Name, StringComparer.Ordinal))
        {
            if (existingNames.Contains(property.Name) || IsHiddenByDesignerAttributes(property))
                continue;

            var metadata = metadataReader.ReadProperty(property);
            var displayName = metadata.DisplayName;

            if (existingDisplayNames.Contains(displayName))
                continue;

            var canWrite = property.SetMethod is { IsPublic: true, IsStatic: false };
            var fallback = metadata.HasDefaultValue
                ? metadata.DefaultValue
                : TryReadDefaultPropertyValue(defaultInstance, property);
            fallback = NormalizeDesignerFallback(property.Name, property.PropertyType, fallback);
            var category = metadata.Category ?? GuessPropertyCategory(property.Name, property.PropertyType);
            if (TryCreateBoundsAliasDescriptor(node, property.Name, displayName, metadata.Description, out var boundsAliasDescriptor))
            {
                descriptors.Add(boundsAliasDescriptor);
                existingNames.Add(property.Name);
                existingDisplayNames.Add(displayName);
                continue;
            }

            var unsupported = IsUnsupportedLayoutProperty(property.Name);
            var description = GetPropertyDescription(metadata.Description ?? "Runtime property discovered by reflection.", unsupported);
            var runtimeType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var complexCanEdit = !unsupported && (canWrite || runtimeType == typeof(ControlStyle));

            if (DesignerPropertyDescriptorFactory.TryCreateRuntimeDescriptor(
                node,
                property.Name,
                displayName,
                category,
                description,
                property.PropertyType,
                fallback,
                complexCanEdit,
                unsupported || metadata.Visibility == DesignPropertyVisibility.Advanced,
                out var complexDescriptor))
            {
                descriptors.Add(complexDescriptor!);
                existingNames.Add(property.Name);
                existingDisplayNames.Add(displayName);
                continue;
            }

            var simpleType = DesignMetadataReader.IsSimpleSerializableType(property.PropertyType);
            var editable = simpleType && canWrite && !unsupported;

            descriptors.Add(new DesignerPropertyDescriptor
            {
                Name = property.Name,
                DisplayName = displayName,
                Category = category,
                Description = description,
                ValueType = property.PropertyType,
                IsReadOnly = !editable,
                IsAdvanced = unsupported || !simpleType || metadata.Visibility == DesignPropertyVisibility.Advanced,
                ShouldSerialize = editable,
                StandardValues = DesignerPropertyValueEditor.GetStandardValues(property.PropertyType),
                GetValue = () => GetNodePropertyValue(node, property.Name, property.PropertyType, fallback),
                CommitText = text =>
                {
                    if (!editable)
                        return (false, "This property does not have a simple designer editor yet.");

                    if (!DesignerPropertyValueEditor.TryConvert(text, property.PropertyType, out var value, out var error))
                        return (false, error);

                    node.Properties[property.Name] = DesignerPropertyValueEditor.ToDesignPropertyValue(value, property.PropertyType);
                    return (true, null);
                }
            });

            existingNames.Add(property.Name);
            existingDisplayNames.Add(displayName);
        }
    }

    private IReadOnlyList<DesignerEventDescriptor> BuildEventDescriptors()
    {
        var node = playgroundState.SelectedNode;
        var bindings = node?.Events ?? playgroundState.Document.Events;
        var descriptors = new SortedDictionary<string, DesignerEventDescriptor>(StringComparer.Ordinal);

        foreach (var fixedEvent in FixedEvents)
            descriptors[fixedEvent.Name] = CreateEventDescriptor(bindings, fixedEvent.Name, fixedEvent.DisplayName, fixedEvent.Category, fixedEvent.Description, handlerType: null);

        var selectedType = node is null
            ? playgroundState.GetRootControlType()
            : playgroundState.IsProjectUserControlType(node.TypeName)
                ? null
                : playgroundState.ResolveControlType(node);

        if (selectedType is { } controlType)
        {
            foreach (var metadata in metadataReader.ReadControl(controlType).Events)
            {
                descriptors[metadata.Name] = CreateEventDescriptor(
                    bindings,
                    metadata.Name,
                    metadata.DisplayName,
                    metadata.Category ?? "Misc",
                    metadata.Description ?? "Designer metadata does not provide a description for this event.",
                    handlerType: null);
            }

            foreach (var eventInfo in controlType
                .GetEvents(BindingFlags.Instance | BindingFlags.Public)
                .Where(eventInfo => !IsHiddenByDesignerAttributes(eventInfo)))
            {
                var metadata = metadataReader.ReadEvent(eventInfo);
                var existing = descriptors.TryGetValue(eventInfo.Name, out var existingDescriptor)
                    ? existingDescriptor
                    : null;
                descriptors[eventInfo.Name] = CreateEventDescriptor(
                    bindings,
                    eventInfo.Name,
                    metadata.DisplayName,
                    metadata.Category ?? existing?.Category ?? GuessEventCategory(eventInfo.Name),
                    metadata.Description ?? existing?.Description ?? "Runtime event discovered by reflection.",
                    eventInfo.EventHandlerType);
            }
        }

        return descriptors.Values
            .OrderBy(eventDescriptor => GetCategorySortKey(eventDescriptor.Category))
            .ThenBy(eventDescriptor => eventDescriptor.Category, StringComparer.Ordinal)
            .ThenBy(eventDescriptor => eventDescriptor.DisplayName, StringComparer.Ordinal)
            .ThenBy(eventDescriptor => eventDescriptor.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private DesignerEventDescriptor CreateEventDescriptor(
        SortedDictionary<string, string?> bindings,
        string name,
        string displayName,
        string category,
        string description,
        Type? handlerType)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = category,
            Description = description,
            HandlerType = handlerType,
            GetHandlerName = () => bindings.TryGetValue(name, out var handlerName) ? handlerName : null,
            CommitHandlerName = handlerName =>
            {
                if (!string.IsNullOrWhiteSpace(handlerName) && !DesignDocumentValidator.IsValidCSharpIdentifier(handlerName))
                    return (false, "The handler name must be a valid C# identifier.");

                bindings[name] = handlerName;
                return (true, null);
            }
        };

    private static string CreateDefaultEventHandlerName(string objectName, string eventName)
    {
        var baseName = $"{SanitizeIdentifierPart(objectName)}_{SanitizeIdentifierPart(eventName)}";
        return DesignDocumentValidator.IsValidCSharpIdentifier(baseName)
            ? baseName
            : $"handler_{baseName}";
    }

    private static string SanitizeIdentifierPart(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "control" : value.Trim();
        var chars = text
            .Select((character, index) =>
                (index == 0 ? char.IsLetter(character) || character == '_' : char.IsLetterOrDigit(character) || character == '_')
                    ? character
                    : '_')
            .ToArray();
        var result = new string(chars);

        return string.IsNullOrWhiteSpace(result) ? "control" : result;
    }

    private DesignerPropertyDescriptor FormSize(
        string name,
        string displayName,
        Func<DesignSize, int> getValue,
        Func<int, int, DesignSize> createSize)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = "Layout",
            Description = $"{displayName} in logical pixels.",
            ValueType = typeof(int),
            GetValue = () => getValue(playgroundState.Document.Size),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                var sizeValue = (int)value!;

                if (sizeValue < 1)
                    return (false, "The design root size must be greater than zero.");

                var size = playgroundState.Document.Size;
                playgroundState.Document.Size = name == "Width"
                    ? createSize(sizeValue, size.Height)
                    : createSize(size.Width, sizeValue);
                return (true, null);
            }
        };

    private DesignerPropertyDescriptor FormProperty(
        string name,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? defaultValue)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(valueType),
            GetValue = () => GetFormPropertyValue(name, valueType, defaultValue),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, valueType, out var value, out var error))
                    return (false, error);

                playgroundState.Document.Properties[name] = DesignerPropertyValueEditor.ToDesignPropertyValue(value, valueType);
                return (true, null);
            }
        };

    private DesignerPropertyDescriptor Bounds(
        string name,
        string displayName,
        string description,
        Func<DesignBounds, int> getValue,
        Func<DesignBounds, int, DesignBounds> createBounds,
        bool requirePositive = false)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = "Layout",
            Description = description,
            ValueType = typeof(int),
            GetValue = () => getValue(playgroundState.SelectedNode!.Bounds),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                var intValue = (int)value!;

                if (requirePositive && intValue < MinimumControlSize)
                    return (false, $"The value must be at least {MinimumControlSize}.");

                var node = playgroundState.SelectedNode!;
                node.Bounds = createBounds(node.Bounds, intValue);
                return (true, null);
            }
        };

    private static bool TryCreateBoundsAliasDescriptor(
        DesignControlNode node,
        string name,
        string displayName,
        string? description,
        out DesignerPropertyDescriptor descriptor)
    {
        descriptor = name switch
        {
            "Left" => BoundsAlias(
                node,
                name,
                displayName,
                description ?? "The left edge of the control in parent-local coordinates.",
                bounds => bounds.X,
                (bounds, value) => (true, new DesignBounds(value, bounds.Y, bounds.Width, bounds.Height), null)),
            "Top" => BoundsAlias(
                node,
                name,
                displayName,
                description ?? "The top edge of the control in parent-local coordinates.",
                bounds => bounds.Y,
                (bounds, value) => (true, new DesignBounds(bounds.X, value, bounds.Width, bounds.Height), null)),
            "Right" => BoundsAlias(
                node,
                name,
                displayName,
                description ?? "The right edge of the control in parent-local coordinates.",
                bounds => bounds.Right,
                (bounds, value) =>
                {
                    var width = value - bounds.X;

                    return width < MinimumControlSize
                        ? (false, bounds, $"Right must keep Width at least {MinimumControlSize}.")
                        : (true, new DesignBounds(bounds.X, bounds.Y, width, bounds.Height), null);
                }),
            "Bottom" => BoundsAlias(
                node,
                name,
                displayName,
                description ?? "The bottom edge of the control in parent-local coordinates.",
                bounds => bounds.Bottom,
                (bounds, value) =>
                {
                    var height = value - bounds.Y;

                    return height < MinimumControlSize
                        ? (false, bounds, $"Bottom must keep Height at least {MinimumControlSize}.")
                        : (true, new DesignBounds(bounds.X, bounds.Y, bounds.Width, height), null);
                }),
            _ => null!
        };

        return descriptor is not null;
    }

    private static DesignerPropertyDescriptor BoundsAlias(
        DesignControlNode node,
        string name,
        string displayName,
        string description,
        Func<DesignBounds, int> getValue,
        Func<DesignBounds, int, (bool Success, DesignBounds Bounds, string? Error)> updateBounds)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = "Layout",
            Description = description,
            ValueType = typeof(int),
            IsAdvanced = true,
            GetValue = () => getValue(node.Bounds),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                var result = updateBounds(node.Bounds, (int)value!);

                if (!result.Success)
                    return (false, result.Error);

                node.Bounds = result.Bounds;
                return (true, null);
            }
        };

    private DesignerPropertyDescriptor NodeProperty(
        DesignControlNode node,
        string name,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? defaultValue)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(valueType),
            GetValue = () => GetNodePropertyValue(node, name, valueType, defaultValue),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, valueType, out var value, out var error))
                    return (false, error);

                node.Properties[name] = DesignerPropertyValueEditor.ToDesignPropertyValue(value, valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor ReadOnly(
        string name,
        string displayName,
        string category,
        string description,
        Type valueType,
        Func<object?> getValue)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            IsReadOnly = true,
            ShouldSerialize = false,
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(valueType),
            GetValue = getValue
        };

    private static object? GetNodePropertyValue(DesignControlNode node, string name, Type valueType, object? defaultValue)
    {
        if (!node.Properties.TryGetValue(name, out var value))
            return defaultValue;

        try
        {
            return DesignerPropertyValueEditor.FromDesignPropertyValue(value, valueType);
        }
        catch
        {
            return defaultValue;
        }
    }

    private object? GetFormPropertyValue(string name, Type valueType, object? defaultValue)
    {
        if (!playgroundState.Document.Properties.TryGetValue(name, out var value))
            return defaultValue;

        try
        {
            return DesignerPropertyValueEditor.FromDesignPropertyValue(value, valueType);
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool IsInspectableRuntimeProperty(PropertyInfo property)
        => property.GetMethod is { IsPublic: true, IsStatic: false }
        && property.GetIndexParameters().Length == 0;

    private static bool IsHiddenByDesignerAttributes(PropertyInfo property)
    {
        var designable = property.GetCustomAttribute<DesignablePropertyAttribute>(inherit: true);

        if (property.GetCustomAttribute<DesignerHiddenAttribute>(inherit: true) is not null)
            return true;

        if (designable is not null)
            return designable.Visibility == DesignPropertyVisibility.Hidden;

        if (property.GetCustomAttribute<BrowsableAttribute>(inherit: true)?.Browsable == false)
            return true;

        return property.GetCustomAttribute<DesignerSerializationVisibilityAttribute>(inherit: true)?.Visibility
            == DesignerSerializationVisibility.Hidden;
    }

    private static bool IsHiddenByDesignerAttributes(EventInfo eventInfo)
    {
        var designable = eventInfo.GetCustomAttribute<DesignableEventAttribute>(inherit: true);

        if (designable is not null)
            return !designable.Visible;

        return eventInfo.GetCustomAttribute<BrowsableAttribute>(inherit: true)?.Browsable == false;
    }

    private static object? TryCreateDefaultControlInstance(Type controlType)
    {
        try
        {
            return Activator.CreateInstance(controlType);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryReadDefaultPropertyValue(object? defaultInstance, PropertyInfo property)
    {
        if (defaultInstance is null)
            return null;

        try
        {
            return property.GetValue(defaultInstance);
        }
        catch
        {
            return null;
        }
    }

    private static string GuessPropertyCategory(string propertyName, Type propertyType)
    {
        if (propertyName is "Name" or "Tag" or "Site" or "Parent" or "Controls")
            return "Design";

        if (propertyName is "Bounds" or "Location" or "Size" or "X" or "Y" or "Left" or "Top" or "Right" or "Bottom"
            or "Width" or "Height" or "Dock" or "Anchor" or "Margin" or "Padding" or "MinimumSize" or "MaximumSize"
            or "AutoSize" or "AutoSizeMode")
        {
            return "Layout";
        }

        if (propertyName.Contains("Style", StringComparison.Ordinal)
            || propertyName.Contains("Color", StringComparison.Ordinal)
            || propertyName.Contains("Brush", StringComparison.Ordinal)
            || propertyName.Contains("Font", StringComparison.Ordinal)
            || propertyName is "Text" or "TextAlign" or "Image" or "BackgroundImage" or "Cursor")
        {
            return "Appearance";
        }

        if (propertyName is "Enabled" or "Visible" or "TabStop" or "TabIndex" or "CanFocus" or "CanSelect"
            || propertyName.StartsWith("Allow", StringComparison.Ordinal)
            || propertyName.StartsWith("Use", StringComparison.Ordinal))
        {
            return "Behavior";
        }

        if (propertyName.Contains("Data", StringComparison.Ordinal)
            || propertyName.Contains("Binding", StringComparison.Ordinal))
        {
            return "Data";
        }

        return propertyType.IsEnum || propertyType == typeof(bool)
            ? "Behavior"
            : "Misc";
    }

    private static bool IsUnsupportedLayoutProperty(string propertyName)
        => UnsupportedLayoutProperties.Contains(propertyName);

    private static object? NormalizeDesignerFallback(string propertyName, Type propertyType, object? fallback)
    {
        // Some ModernFormsNext controls set a runtime Dock value in their constructor
        // because they are commonly used as top/bottom bars. A design document, however,
        // treats a missing Dock entry as DockStyle.None. Keep the property grid aligned
        // with the designer model instead of leaking constructor defaults into generated UI.
        return propertyName == "Dock" && propertyType == typeof(ModernFormsNext.DockStyle)
            ? ModernFormsNext.DockStyle.None
            : fallback;
    }

    private static string GetPropertyDescription(string description, bool unsupported)
        => unsupported
            ? description + " Editing is not implemented in ModernFormsNext Designer yet, so this property is shown read-only."
            : description;

    private static string GuessEventCategory(string eventName)
    {
        if (eventName.Contains("Mouse", StringComparison.Ordinal)
            || eventName.Contains("Click", StringComparison.Ordinal))
        {
            return "Mouse";
        }

        if (eventName.Contains("Key", StringComparison.Ordinal))
            return "Keyboard";

        if (eventName.EndsWith("Changed", StringComparison.Ordinal))
            return "Property Changed";

        if (eventName.Contains("Focus", StringComparison.Ordinal)
            || eventName.Contains("Enter", StringComparison.Ordinal)
            || eventName.Contains("Leave", StringComparison.Ordinal))
        {
            return "Focus";
        }

        return "Events";
    }

    private bool ShouldShowModelProperty(DesignControlNode node, string propertyName)
    {
        if (playgroundState.IsProjectUserControlType(node.TypeName))
            return true;

        var controlType = playgroundState.ResolveControlType(node);

        if (controlType is null)
            return true;

        var property = controlType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        if (property is null)
            return false;

        return !metadataReader.ReadProperty(property).IsHidden;
    }

    private string GetSelectedObjectTypeName()
    {
        var node = playgroundState.SelectedNode;

        if (node is null)
            return playgroundState.GetRootTypeName();

        return GetNodeTypeName(node);
    }

    private string GetNodeTypeName(DesignControlNode node)
        => playgroundState.IsProjectUserControlType(node.TypeName)
            ? DesignerProjectUserControlDiscovery.NormalizeTypeName(node.TypeName)
            : playgroundState.ResolveControlType(node)?.FullName ?? node.TypeName;

    private static int GetCategorySortKey(string category)
    {
        var index = Array.IndexOf(CategoryOrder, category);
        return index >= 0 ? index : CategoryOrder.Length;
    }

    private readonly record struct FixedEventDefinition(
        string Name,
        string DisplayName,
        string Category,
        string Description);
}

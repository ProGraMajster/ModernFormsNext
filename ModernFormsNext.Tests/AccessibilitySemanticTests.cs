using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AccessibilitySemanticCollection
{
    public const string Name = "Accessibility semantics";
}

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class AccessibilitySemanticTests
{
    [Fact]
    public void ButtonExposesNameTypeAndNormalInvokeAction()
    {
        using var root = new VisibleRootControl();
        using var button = root.Controls.Add(new Button { Text = "Save" });
        int clicks = 0;
        button.Click += (_, _) => clicks++;

        AccessibleObject accessible = button.AccessibilityObject;

        Assert.Equal("Save", accessible.Name);
        Assert.Equal(AccessibleRole.PushButton, accessible.Role);
        Assert.Equal(AccessibleControlType.Button, accessible.ControlType);
        AssertHasFlag(accessible.SupportedActions, AccessibleActions.Invoke);
        Assert.True(accessible.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1, clicks);

        accessible.DoDefaultAction();
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void ControlMetadataKeepsAutomationAndRuntimeIdentitySeparate()
    {
        using var root = new VisibleRootControl();
        using var button = root.Controls.Add(new Button
        {
            Name = "saveButton",
            Text = "Save",
            AccessibleAutomationId = "document.save"
        });

        AccessibleObject first = button.AccessibilityObject;
        long runtimeId = first.RuntimeId;

        Assert.Same(first, button.AccessibilityObject);
        Assert.Equal(runtimeId, button.AccessibilityObject.RuntimeId);
        Assert.Equal("document.save", first.AutomationId);
        Assert.Equal("Save", first.Name);

        button.Name = "renamedButton";
        Assert.Equal("document.save", first.AutomationId);
        Assert.Equal(runtimeId, first.RuntimeId);

        button.AccessibleAutomationId = null;
        Assert.Equal("renamedButton", first.AutomationId);
        button.Name = string.Empty;
        Assert.Equal(string.Empty, first.AutomationId);
        Assert.Equal(runtimeId, first.RuntimeId);
    }

    [Fact]
    public void CheckBoxToggleUpdatesCheckedAndMixedStates()
    {
        using var root = new VisibleRootControl();
        using var checkBox = root.Controls.Add(new CheckBox { Text = "Remember" });
        AccessibleObject accessible = checkBox.AccessibilityObject;

        Assert.Equal(AccessibleControlType.CheckBox, accessible.ControlType);
        AssertDoesNotHaveFlag(accessible.State, AccessibleStates.Checked);
        Assert.True(accessible.PerformAction(AccessibleActions.Toggle));
        Assert.True(checkBox.Checked);
        AssertHasFlag(accessible.State, AccessibleStates.Checked);

        checkBox.ThreeState = true;
        checkBox.CheckState = CheckState.Indeterminate;
        AssertHasFlag(accessible.State, AccessibleStates.Mixed);
    }

    [Fact]
    public void RadioButtonSelectUsesNormalMutuallyExclusiveBehavior()
    {
        using var root = new VisibleRootControl();
        using var first = root.Controls.Add(new RadioButton { Text = "First", Checked = true });
        using var second = root.Controls.Add(new RadioButton { Text = "Second" });

        Assert.True(second.AccessibilityObject.PerformAction(AccessibleActions.Select));
        Assert.False(first.Checked);
        Assert.True(second.Checked);
        AssertHasFlag(second.AccessibilityObject.State, AccessibleStates.Selected);
    }

    [Fact]
    public void SwitchToggleUsesTheControlStateMachine()
    {
        using var root = new VisibleRootControl();
        using var toggle = root.Controls.Add(new Switch { Text = "Wi-Fi" });

        Assert.Equal(AccessibleControlType.Switch, toggle.AccessibilityObject.ControlType);
        Assert.True(toggle.AccessibilityObject.PerformAction(AccessibleActions.Toggle));
        Assert.True(toggle.IsToggled);
        AssertHasFlag(toggle.AccessibilityObject.State, AccessibleStates.Checked);
    }

    [Fact]
    public void TextBoxSeparatesNameValueReadOnlyAndSensitiveData()
    {
        const string sampleSensitiveValue = "semantic-test-sensitive-value";
        using var root = new VisibleRootControl();
        using var textBox = root.Controls.Add(new TextBox
        {
            Name = "passwordField",
            Text = sampleSensitiveValue
        });

        AccessibleObject accessible = textBox.AccessibilityObject;

        Assert.Equal(AccessibleControlType.Edit, accessible.ControlType);
        Assert.Equal("passwordField", accessible.Name);
        Assert.Equal(sampleSensitiveValue, accessible.Value);
        Assert.False(accessible.IsSensitive);
        AssertHasFlag(accessible.SupportedActions, AccessibleActions.SetValue);

        textBox.ReadOnly = true;
        AssertHasFlag(accessible.State, AccessibleStates.ReadOnly);
        AssertDoesNotHaveFlag(accessible.SupportedActions, AccessibleActions.SetValue);

        textBox.ReadOnly = false;
        textBox.PasswordCharacter = '*';
        Assert.True(accessible.IsSensitive);
        Assert.True(string.IsNullOrEmpty(accessible.Value));
        Assert.Equal("passwordField", accessible.Name);
        AssertHasFlag(accessible.State, AccessibleStates.Protected);
        Assert.False((accessible.ToString() ?? string.Empty).Contains(sampleSensitiveValue, StringComparison.Ordinal));
        Assert.True(accessible.PerformAction(AccessibleActions.SetValue, "replacement"));
        Assert.Equal("replacement", textBox.Text);
        Assert.True(string.IsNullOrEmpty(accessible.Value));
        Assert.False((accessible.ToString() ?? string.Empty).Contains("replacement", StringComparison.Ordinal));

        textBox.ReadOnly = true;
        AssertDoesNotHaveFlag(accessible.SupportedActions, AccessibleActions.SetValue);
        Assert.False(accessible.PerformAction(AccessibleActions.SetValue, "rejected"));
        textBox.Text = string.Empty;
        Assert.True(string.IsNullOrEmpty(accessible.Value));

        textBox.ReadOnly = false;
        textBox.PasswordCharacter = null;
        Assert.False(accessible.IsSensitive);
        Assert.Equal(string.Empty, accessible.Value);

        string diagnosticProjection = $"{accessible.Name}|{(accessible.IsSensitive ? "<redacted>" : accessible.Value)}";
        Assert.False(diagnosticProjection.Contains(sampleSensitiveValue, StringComparison.Ordinal));
    }

    [Fact]
    public void ComboBoxExposesCollapsedStateAndSelectableLogicalItems()
    {
        using var form = new Form(CreateWindowImplementation());
        using var comboBox = form.Controls.Add(new ComboBox { Name = "choice" });
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");

        AccessibleObject accessible = comboBox.AccessibilityObject;
        AccessibleObject beta = Assert.IsAssignableFrom<AccessibleObject>(accessible.GetChild(1));

        Assert.Equal(AccessibleControlType.ComboBox, accessible.ControlType);
        AssertHasFlag(accessible.State, AccessibleStates.Collapsed);
        AssertHasFlag(accessible.SupportedActions, AccessibleActions.Expand);
        AssertDoesNotHaveFlag(accessible.SupportedActions, AccessibleActions.Collapse);
        Assert.True(accessible.PerformAction(AccessibleActions.Expand));
        AssertHasFlag(accessible.State, AccessibleStates.Expanded);
        AssertHasFlag(accessible.SupportedActions, AccessibleActions.Collapse);
        AssertDoesNotHaveFlag(accessible.SupportedActions, AccessibleActions.Expand);
        Assert.True(accessible.PerformAction(AccessibleActions.Collapse));
        Assert.Equal(2, accessible.GetChildCount());
        Assert.Equal(AccessibleControlType.ListItem, beta.ControlType);
        Assert.True(beta.PerformAction(AccessibleActions.Select));
        Assert.Equal(1, comboBox.SelectedIndex);
        Assert.Same(beta, accessible.GetSelected());
    }

    [Fact]
    public void ListBoxItemsKeepIdentityAcrossMoveAndDetachAfterRemoval()
    {
        using var root = new VisibleRootControl();
        using var listBox = root.Controls.Add(new ListBox());
        var alpha = new NamedItem("Alpha");
        var beta = new NamedItem("Beta");
        listBox.Items.Add(alpha);
        listBox.Items.Add(beta);

        AccessibleObject list = listBox.AccessibilityObject;
        AccessibleObject betaNode = Assert.IsAssignableFrom<AccessibleObject>(list.GetChild(1));
        long runtimeId = betaNode.RuntimeId;

        Assert.True(betaNode.PerformAction(AccessibleActions.Select));
        Assert.Same(betaNode, list.GetSelected());

        listBox.Items.Move(1, 0);
        Assert.Same(betaNode, list.GetChild(0));
        Assert.Equal(runtimeId, betaNode.RuntimeId);
        Assert.Equal(0, listBox.SelectedIndex);
        Assert.Same(betaNode, list.GetSelected());

        listBox.Items.Remove(beta);
        Assert.Null(betaNode.Parent);
        Assert.Equal(AccessibilityView.Hidden, betaNode.View);
        Assert.Null(list.GetSelected());
    }

    [Fact]
    public void MultiSelectListBoxSemanticItemsHonorAddAndRemoveSelectionFlags()
    {
        using var root = new VisibleRootControl();
        using var listBox = root.Controls.Add(new ListBox { SelectionMode = SelectionMode.MultiSimple });
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");

        AccessibleObject first = Assert.IsAssignableFrom<AccessibleObject>(listBox.AccessibilityObject.GetChild(0));
        AccessibleObject second = Assert.IsAssignableFrom<AccessibleObject>(listBox.AccessibilityObject.GetChild(1));

        first.Select(AccessibleSelection.AddSelection);
        second.Select(AccessibleSelection.AddSelection);
        Assert.Equal(new[] { 0, 1 }, listBox.Items.SelectedIndexes);

        first.Select(AccessibleSelection.RemoveSelection);
        Assert.Equal(new[] { 1 }, listBox.Items.SelectedIndexes);
    }

    [Fact]
    public void EqualListAndComboOccurrencesKeepDistinctIdentityAcrossReorder()
    {
        using var root = new VisibleRootControl();
        using var listBox = root.Controls.Add(new ListBox());
        using var comboBox = root.Controls.Add(new ComboBox());
        var repeatedItem = new NamedItem("Repeated");

        listBox.Items.Add(repeatedItem);
        listBox.Items.Add(repeatedItem);
        comboBox.Items.Add(repeatedItem);
        comboBox.Items.Add(repeatedItem);

        AssertOccurrenceIdentityAcrossMove(listBox.AccessibilityObject, listBox.Items);
        AssertOccurrenceIdentityAcrossMove(comboBox.AccessibilityObject, comboBox.Items);
    }

    [Fact]
    public void EqualTreeItemTextDoesNotCollideAndReinsertionIsDeterministic()
    {
        using var root = new VisibleRootControl();
        using var treeView = root.Controls.Add(new TreeView());
        var first = new TreeViewItem("Repeated");
        var second = new TreeViewItem("Repeated");
        treeView.Items.Add(first);
        treeView.Items.Add(second);

        AccessibleObject tree = treeView.AccessibilityObject;
        AccessibleObject firstNode = Assert.IsAssignableFrom<AccessibleObject>(tree.GetChild(0));
        AccessibleObject secondNode = Assert.IsAssignableFrom<AccessibleObject>(tree.GetChild(1));
        long firstId = firstNode.RuntimeId;
        long secondId = secondNode.RuntimeId;

        Assert.NotSame(firstNode, secondNode);
        Assert.NotEqual(firstId, secondId);

        treeView.Items.Remove(first);
        Assert.Null(firstNode.Parent);
        treeView.Items.Insert(1, first);

        Assert.Same(firstNode, tree.GetChild(1));
        Assert.Same(secondNode, tree.GetChild(0));
        Assert.Equal(firstId, firstNode.RuntimeId);
        Assert.Equal(secondId, secondNode.RuntimeId);
    }

    [Fact]
    public void ListViewItemsExposeSelectionAndDetachOnRemoval()
    {
        using var root = new VisibleRootControl();
        using var listView = root.Controls.Add(new ListView());
        ListViewItem first = listView.Items.Add("First");
        ListViewItem second = listView.Items.Add("Second");

        AccessibleObject list = listView.AccessibilityObject;
        AccessibleObject secondNode = Assert.IsAssignableFrom<AccessibleObject>(list.GetChild(1));

        Assert.True(secondNode.PerformAction(AccessibleActions.Select));
        Assert.Same(second, listView.SelectedItem);
        AssertHasFlag(secondNode.State, AccessibleStates.Selected);
        Assert.Same(secondNode, list.GetSelected());

        listView.Items.Remove(second);
        Assert.Null(secondNode.Parent);
        Assert.Equal(AccessibilityView.Hidden, secondNode.View);
        Assert.Null(list.GetSelected());
        Assert.Same(first, listView.Items[0]);
    }

    [Fact]
    public void TreeItemsExposeHierarchyExpansionSelectionAndRemoval()
    {
        using var root = new VisibleRootControl();
        using var treeView = root.Controls.Add(new TreeView());
        var child = new TreeViewItem("Child");
        var branch = treeView.Items.Add(new TreeViewItem("Branch", child));

        AccessibleObject tree = treeView.AccessibilityObject;
        AccessibleObject branchNode = Assert.IsAssignableFrom<AccessibleObject>(tree.GetChild(0));
        AccessibleObject childNode = Assert.IsAssignableFrom<AccessibleObject>(branchNode.GetChild(0));

        Assert.Equal(AccessibleControlType.TreeItem, branchNode.ControlType);
        AssertHasFlag(branchNode.State, AccessibleStates.Collapsed);
        Assert.Same(branchNode, childNode.Parent);
        Assert.True(branchNode.PerformAction(AccessibleActions.Expand));
        Assert.True(branch.Expanded);
        AssertHasFlag(branchNode.State, AccessibleStates.Expanded);
        Assert.True(childNode.PerformAction(AccessibleActions.Select));
        Assert.Same(child, treeView.SelectedItem);
        Assert.Same(childNode, tree.GetSelected());

        treeView.Items.Remove(branch);
        Assert.Null(branchNode.Parent);
        Assert.Null(childNode.Parent);
        Assert.Equal(AccessibilityView.Hidden, branchNode.View);
        Assert.Null(tree.GetSelected());
    }

    [Fact]
    public void TabItemsExposeStableSelectionAndDetachOnRemoval()
    {
        using var root = new VisibleRootControl();
        using var tabs = root.Controls.Add(new TabControl());
        TabPage first = tabs.TabPages.Add("First");
        TabPage second = tabs.TabPages.Add("Second");
        tabs.SelectedTabPage = first;

        AccessibleObject tabRoot = tabs.AccessibilityObject;
        AccessibleObject firstNode = Assert.IsAssignableFrom<AccessibleObject>(tabRoot.GetChild(0));
        AccessibleObject secondNode = Assert.IsAssignableFrom<AccessibleObject>(tabRoot.GetChild(1));

        Assert.Equal(AccessibleControlType.TabItem, firstNode.ControlType);
        AssertHasFlag(firstNode.State, AccessibleStates.Selected);
        Assert.True(secondNode.PerformAction(AccessibleActions.Select));
        Assert.Same(second, tabs.SelectedTabPage);
        Assert.Same(secondNode, tabRoot.GetSelected());

        tabs.TabPages.Remove(second);
        Assert.Null(secondNode.Parent);
        Assert.Equal(AccessibilityView.Hidden, secondNode.View);
    }

    [Fact]
    public void TrackBarExposesWritableRangeAndUsesSmallChangeActions()
    {
        using var root = new VisibleRootControl();
        using var trackBar = root.Controls.Add(new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            SmallChange = 5,
            LargeChange = 20,
            Value = 20
        });
        AccessibleObject accessible = trackBar.AccessibilityObject;
        AccessibleRangeValue range = Assert.IsType<AccessibleRangeValue>(accessible.RangeValue);

        Assert.Equal(20, range.Value);
        Assert.Equal(0, range.Minimum);
        Assert.Equal(100, range.Maximum);
        Assert.Equal(5, range.SmallChange);
        Assert.Equal(20, range.LargeChange);
        Assert.False(range.IsReadOnly);
        Assert.True(accessible.PerformAction(AccessibleActions.SetValue, 30d));
        Assert.Equal(30, trackBar.Value);
        Assert.True(accessible.PerformAction(AccessibleActions.Increment));
        Assert.Equal(35, trackBar.Value);
        Assert.True(accessible.PerformAction(AccessibleActions.Decrement));
        Assert.Equal(30, trackBar.Value);
        Assert.False(accessible.PerformAction(AccessibleActions.SetValue, 101d));
    }

    [Fact]
    public void ProgressBarExposesReadOnlyRangeWithoutSetValue()
    {
        using var root = new VisibleRootControl();
        using var progressBar = root.Controls.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 200,
            Step = 10,
            Value = 80
        });
        AccessibleObject accessible = progressBar.AccessibilityObject;
        AccessibleRangeValue range = Assert.IsType<AccessibleRangeValue>(accessible.RangeValue);

        Assert.Equal(AccessibleControlType.ProgressBar, accessible.ControlType);
        Assert.Equal(80, range.Value);
        Assert.True(range.IsReadOnly);
        AssertHasFlag(accessible.State, AccessibleStates.ReadOnly);
        AssertDoesNotHaveFlag(accessible.SupportedActions, AccessibleActions.SetValue);
        Assert.False(accessible.PerformAction(AccessibleActions.SetValue, 100d));
    }

    [Fact]
    public void MenuItemsExposeCommandsSubmenusAndHierarchy()
    {
        using var form = new Form(CreateWindowImplementation());
        using var menu = form.Controls.Add(new Menu());
        int invocations = 0;
        MenuItem file = menu.Items.Add("File");
        file.Items.Add("Open", onClick: (_, _) => invocations++);
        menu.Items.Add("Exit", onClick: (_, _) => invocations++);

        AccessibleObject menuRoot = menu.AccessibilityObject;
        AccessibleObject fileNode = Assert.IsAssignableFrom<AccessibleObject>(menuRoot.GetChild(0));
        AccessibleObject openNode = Assert.IsAssignableFrom<AccessibleObject>(fileNode.GetChild(0));
        AccessibleObject exitNode = Assert.IsAssignableFrom<AccessibleObject>(menuRoot.GetChild(1));

        Assert.Equal(AccessibleControlType.MenuItem, fileNode.ControlType);
        AssertHasFlag(fileNode.SupportedActions, AccessibleActions.Expand);
        Assert.True(fileNode.PerformAction(AccessibleActions.Expand));
        AssertHasFlag(fileNode.SupportedActions, AccessibleActions.Collapse);
        Assert.True(fileNode.PerformAction(AccessibleActions.Collapse));
        Assert.Same(fileNode, openNode.Parent);
        AssertHasFlag(openNode.SupportedActions, AccessibleActions.Invoke);
        Assert.True(exitNode.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void FormExposesWindowSemanticsAndUserControlHierarchy()
    {
        using var form = new Form(CreateWindowImplementation()) { Text = "Settings" };
        using var button = form.Controls.Add(new Button { Text = "Apply" });

        AccessibleObject window = form.AccessibilityObject;

        Assert.Equal("Settings", window.Name);
        Assert.Equal(AccessibleRole.Window, window.Role);
        Assert.Equal(AccessibleControlType.Window, window.ControlType);
        Assert.Equal(1, window.GetChildCount());
        Assert.Same(button.AccessibilityObject, window.GetChild(0));

        form.Text = "Preferences";
        Assert.Equal("Preferences", window.Name);
    }

    [Fact]
    public async Task ModalFormExposesDialogSemanticsWhileShown()
    {
        using var parent = new Form(CreateWindowImplementation());
        using var dialog = new Form(CreateWindowImplementation())
        {
            Text = "Confirm",
            StartPosition = FormStartPosition.Manual
        };

        Task<DialogResult> completion = dialog.ShowDialog(parent);

        Assert.Equal(AccessibleRole.Dialog, dialog.AccessibilityObject.Role);
        Assert.Equal(AccessibleControlType.Dialog, dialog.AccessibilityObject.ControlType);

        dialog.DialogResult = DialogResult.OK;
        Assert.Equal(DialogResult.OK, await completion);
    }

    [Fact]
    public void VisibilityViewParentAndDisposalFilterTheActiveTree()
    {
        using var firstRoot = new VisibleRootControl();
        using var secondRoot = new VisibleRootControl();
        var child = firstRoot.Controls.Add(new Button { Text = "Action" });
        AccessibleObject childObject = child.AccessibilityObject;

        Assert.Same(childObject, firstRoot.AccessibilityObject.GetChild(0));

        child.AccessibilityView = AccessibilityView.Hidden;
        Assert.Equal(0, firstRoot.AccessibilityObject.GetChildCount());
        child.AccessibilityView = AccessibilityView.Control;

        child.Visible = false;
        Assert.Equal(0, firstRoot.AccessibilityObject.GetChildCount());
        child.Visible = true;

        secondRoot.Controls.Add(child);
        Assert.Same(secondRoot.AccessibilityObject, childObject.Parent);
        Assert.Equal(0, firstRoot.AccessibilityObject.GetChildCount());
        Assert.Equal(1, secondRoot.AccessibilityObject.GetChildCount());

        child.Dispose();
        Assert.Null(childObject.Parent);
        Assert.Equal(AccessibilityView.Hidden, childObject.View);
        Assert.Equal(0, secondRoot.AccessibilityObject.GetChildCount());
    }

    [Fact]
    public void DynamicControlChangesRaiseExistingSemanticNotifications()
    {
        using var root = new VisibleRootControl();
        AccessibleObject rootObject = root.AccessibilityObject;
        var rootEvents = new List<AccessibleEvents>();
        rootObject.ClientNotification += (_, e) => rootEvents.Add(e.EventId);

        using var first = root.Controls.Add(new Button { Text = "First" });
        using var second = root.Controls.Add(new Button { Text = "Second" });
        root.Controls.SetChildIndex(second, 0);
        root.Controls.Remove(first);

        Assert.True(rootEvents.Count(value => value == AccessibleEvents.Reorder) >= 4);

        var childEvents = new List<AccessibleEvents>();
        second.AccessibilityObject.ClientNotification += (_, e) => childEvents.Add(e.EventId);
        second.AccessibleName = "Renamed";
        second.Enabled = false;
        second.Enabled = true;
        second.Bounds = new Rectangle(10, 20, 80, 30);
        second.Select();

        Assert.Contains(AccessibleEvents.NameChange, childEvents);
        Assert.Contains(AccessibleEvents.StateChange, childEvents);
        Assert.Contains(AccessibleEvents.LocationChange, childEvents);
        Assert.Contains(AccessibleEvents.Focus, childEvents);
    }

    [Fact]
    public void CollectionSelectionAndPopupNotificationsAreSpecificAndNotDuplicated()
    {
        using var root = new VisibleRootControl();
        using var listBox = root.Controls.Add(new ListBox());
        AccessibleObject listBoxObject = listBox.AccessibilityObject;
        var listBoxEvents = new List<AccessibleEvents>();
        listBoxObject.ClientNotification += (_, e) => listBoxEvents.Add(e.EventId);

        listBox.Items.Add("Item");

        Assert.Single(listBoxEvents, value => value == AccessibleEvents.Reorder);
        Assert.DoesNotContain(AccessibleEvents.Selection, listBoxEvents);

        using var listView = root.Controls.Add(new ListView());
        ListViewItem first = listView.Items.Add("First");
        ListViewItem second = listView.Items.Add("Second");
        var listViewEvents = new List<AccessibleEvents>();
        listView.AccessibilityObject.ClientNotification += (_, e) => listViewEvents.Add(e.EventId);

        listView.SelectedItem = first;
        Assert.Equal([AccessibleEvents.Selection, AccessibleEvents.ValueChange], listViewEvents);

        listViewEvents.Clear();
        listView.SelectedItem = second;
        Assert.Equal(
            [AccessibleEvents.SelectionRemove, AccessibleEvents.Selection, AccessibleEvents.ValueChange],
            listViewEvents);

        listViewEvents.Clear();
        listView.Items[1] = new ListViewItem { Text = "Replacement", Selected = true };
        Assert.Equal(
            [AccessibleEvents.Reorder, AccessibleEvents.SelectionRemove, AccessibleEvents.Selection],
            listViewEvents);

        using var form = new Form(CreateWindowImplementation());
        using var comboBox = form.Controls.Add(new ComboBox());
        using var menu = form.Controls.Add(new Menu());
        MenuItem submenu = menu.Items.Add("Submenu");
        submenu.Items.Add("Child");
        AccessibleObject submenuObject = Assert.IsAssignableFrom<AccessibleObject>(
            menu.AccessibilityObject.GetChild(0));
        var comboEvents = new List<AccessibleEvents>();
        var menuRootEvents = new List<AccessibleEvents>();
        var menuItemEvents = new List<AccessibleEvents>();
        comboBox.AccessibilityObject.ClientNotification += (_, e) => comboEvents.Add(e.EventId);
        menu.AccessibilityObject.ClientNotification += (_, e) => menuRootEvents.Add(e.EventId);
        submenuObject.ClientNotification += (_, e) => menuItemEvents.Add(e.EventId);

        comboBox.Items.Add("Selected");
        comboBox.SelectedIndex = 0;
        comboEvents.Clear();
        comboBox.Items.RemoveAt(0);

        Assert.Equal(
            [AccessibleEvents.Reorder, AccessibleEvents.Selection, AccessibleEvents.ValueChange],
            comboEvents);

        comboEvents.Clear();
        Assert.True(comboBox.AccessibilityObject.PerformAction(AccessibleActions.Expand));
        Assert.True(comboBox.AccessibilityObject.PerformAction(AccessibleActions.Collapse));
        Assert.True(submenuObject.PerformAction(AccessibleActions.Expand));
        Assert.True(submenuObject.PerformAction(AccessibleActions.Collapse));

        Assert.Equal(2, comboEvents.Count(value => value == AccessibleEvents.StateChange));
        Assert.Equal(2, menuRootEvents.Count(value => value == AccessibleEvents.StateChange));
        Assert.Equal(2, menuItemEvents.Count(value => value == AccessibleEvents.StateChange));
    }

    [Fact]
    public void CustomRenderedControlCanExposeLogicalChildStateBoundsAndAction()
    {
        using var root = new VisibleRootControl();
        using var custom = root.Controls.Add(new CustomSemanticControl());
        var customObject = Assert.IsType<CustomSemanticAccessibleObject>(custom.AccessibilityObject);
        AccessibleObject child = Assert.IsAssignableFrom<AccessibleObject>(customObject.GetChild(0));

        Assert.Equal(AccessibleControlType.Group, customObject.ControlType);
        Assert.Equal(AccessibleRole.Grouping, customObject.Role);
        Assert.Equal("Painted action", child.Name);
        Assert.Equal("painted.action", child.AutomationId);
        Assert.Equal(new Rectangle(4, 5, 60, 20), child.Bounds);
        AssertHasFlag(child.State, AccessibleStates.Focusable);
        Assert.Same(customObject, child.Parent);
        Assert.True(child.PerformAction(AccessibleActions.Invoke));
        Assert.True(customObject.ActionInvoked);
    }

    [Fact]
    public void FocusActionUsesFrameworkKeyboardFocus()
    {
        using var root = new VisibleRootControl();
        using var button = root.Controls.Add(new Button { Text = "Focus me" });

        AssertHasFlag(button.AccessibilityObject.SupportedActions, AccessibleActions.Focus);
        Assert.True(button.AccessibilityObject.PerformAction(AccessibleActions.Focus));
        Assert.True(button.Focused);
        AssertHasFlag(button.AccessibilityObject.State, AccessibleStates.Focused);
    }

    [Fact]
    public void UnsupportedOrMalformedActionsAreRejectedDeterministically()
    {
        using var root = new VisibleRootControl();
        using var button = root.Controls.Add(new Button { Text = "Action" });

        Assert.False(button.AccessibilityObject.PerformAction(AccessibleActions.Toggle));
        Assert.False(button.AccessibilityObject.PerformAction(AccessibleActions.Invoke | AccessibleActions.Focus));
        Assert.False(button.AccessibilityObject.PerformAction(AccessibleActions.Invoke, "unexpected"));

        button.Enabled = false;
        Assert.False(button.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
    }

    [Fact]
    public void SupportedActionsAreExecutableForRepresentativeControlsAndLogicalItems()
    {
        using var form = new Form(CreateWindowImplementation());
        using var button = form.Controls.Add(new Button { Text = "Action" });
        using var checkBox = form.Controls.Add(new CheckBox { Text = "Check" });
        using var radioButton = form.Controls.Add(new RadioButton { Text = "Choice" });
        using var toggle = form.Controls.Add(new Switch { Text = "Toggle" });
        using var textBox = form.Controls.Add(new TextBox { Name = "editor" });
        using var comboBox = form.Controls.Add(new ComboBox());
        using var trackBar = form.Controls.Add(new TrackBar { Minimum = 0, Maximum = 10, Value = 5 });
        using var listBox = form.Controls.Add(new ListBox());
        using var listView = form.Controls.Add(new ListView());
        using var treeView = form.Controls.Add(new TreeView());
        using var tabs = form.Controls.Add(new TabControl());
        using var menu = form.Controls.Add(new Menu());
        using var progressBar = form.Controls.Add(new ProgressBar());

        comboBox.Items.Add("Combo item");
        listBox.Items.Add("List item");
        listView.Items.Add("List view item");
        treeView.Items.Add(new TreeViewItem("Branch", new TreeViewItem("Child")));
        tabs.TabPages.Add("Tab");
        MenuItem submenu = menu.Items.Add("Submenu");
        submenu.Items.Add("Child command");
        menu.Items.Add("Command");

        AccessibleObject comboItem = Assert.IsAssignableFrom<AccessibleObject>(comboBox.AccessibilityObject.GetChild(0));
        AccessibleObject listItem = Assert.IsAssignableFrom<AccessibleObject>(listBox.AccessibilityObject.GetChild(0));
        AccessibleObject listViewItem = Assert.IsAssignableFrom<AccessibleObject>(listView.AccessibilityObject.GetChild(0));
        AccessibleObject treeItem = Assert.IsAssignableFrom<AccessibleObject>(treeView.AccessibilityObject.GetChild(0));
        AccessibleObject tabItem = Assert.IsAssignableFrom<AccessibleObject>(tabs.AccessibilityObject.GetChild(0));
        AccessibleObject submenuItem = Assert.IsAssignableFrom<AccessibleObject>(menu.AccessibilityObject.GetChild(0));
        AccessibleObject commandItem = Assert.IsAssignableFrom<AccessibleObject>(menu.AccessibilityObject.GetChild(1));

        AccessibleObject[] representatives =
        [
            button.AccessibilityObject,
            checkBox.AccessibilityObject,
            radioButton.AccessibilityObject,
            toggle.AccessibilityObject,
            textBox.AccessibilityObject,
            comboBox.AccessibilityObject,
            trackBar.AccessibilityObject,
            listBox.AccessibilityObject,
            listView.AccessibilityObject,
            treeView.AccessibilityObject,
            tabs.AccessibilityObject,
            menu.AccessibilityObject,
            progressBar.AccessibilityObject,
            comboItem,
            listItem,
            listViewItem,
            treeItem,
            tabItem,
            submenuItem,
            commandItem
        ];

        foreach (AccessibleObject accessible in representatives)
            AssertEveryAdvertisedActionCanExecute(accessible);

        // Expanding changes the advertised action from Expand to Collapse. Exercise the second
        // state as a separate contract snapshot.
        AssertEveryAdvertisedActionCanExecute(submenuItem);
        AssertEveryAdvertisedActionCanExecute(comboBox.AccessibilityObject);
        AssertEveryAdvertisedActionCanExecute(comboBox.AccessibilityObject);
    }

    [Fact]
    public void UnavailableOrInapplicableActionsAreNotAdvertised()
    {
        using var root = new VisibleRootControl();
        using var button = root.Controls.Add(new Button());
        using var toggle = root.Controls.Add(new Switch { AutoToggle = false });
        using var comboBox = root.Controls.Add(new ComboBox());
        using var listBox = root.Controls.Add(new ListBox { SelectionMode = SelectionMode.None });
        using var menu = root.Controls.Add(new Menu());
        listBox.Items.Add("Item");
        MenuItem submenu = menu.Items.Add("Submenu");
        submenu.Items.Add("Child");

        button.Enabled = false;
        Assert.Equal(AccessibleActions.None, button.AccessibilityObject.SupportedActions);
        AssertDoesNotHaveFlag(toggle.AccessibilityObject.SupportedActions, AccessibleActions.Toggle);
        AssertDoesNotHaveFlag(comboBox.AccessibilityObject.SupportedActions, AccessibleActions.Expand);
        AssertDoesNotHaveFlag(
            Assert.IsAssignableFrom<AccessibleObject>(listBox.AccessibilityObject.GetChild(0)).SupportedActions,
            AccessibleActions.Select);
        AssertDoesNotHaveFlag(
            Assert.IsAssignableFrom<AccessibleObject>(menu.AccessibilityObject.GetChild(0)).SupportedActions,
            AccessibleActions.Expand);
    }

    [Fact]
    public void RuntimeIdsArePositiveAndUniqueUnderConcurrentConstruction()
    {
        const int count = 512;
        var runtimeIds = new long[count];

        Parallel.For(0, count, index => runtimeIds[index] = new AccessibleObject().RuntimeId);

        Assert.All(runtimeIds, runtimeId => Assert.True(runtimeId > 0));
        Assert.Equal(count, runtimeIds.Distinct().Count());
    }

    [Fact]
    public void RetainedPeerDoesNotKeepItsControlOrLogicalOwnerAlive()
    {
        (AccessibleObject controlPeer, WeakReference controlReference) = CreateCollectibleControlPeer();
        (AccessibleObject itemPeer, WeakReference ownerReference, WeakReference itemReference) =
            CreateCollectibleLogicalItemPeer();

        CollectGarbage();

        Assert.False(controlReference.IsAlive);
        Assert.Null(Assert.IsType<Control.ControlAccessibleObject>(controlPeer).Owner);
        Assert.Equal(AccessibleActions.None, controlPeer.SupportedActions);
        Assert.False(ownerReference.IsAlive);
        Assert.False(itemReference.IsAlive);
        Assert.Null(itemPeer.Parent);
        Assert.Equal(AccessibilityView.Hidden, itemPeer.View);
        Assert.Equal(AccessibleActions.None, itemPeer.SupportedActions);
        GC.KeepAlive(controlPeer);
        GC.KeepAlive(itemPeer);
    }

    [Fact]
    public void RangeValueRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccessibleRangeValue(0, 10, 5, 1, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccessibleRangeValue(11, 0, 10, 1, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccessibleRangeValue(1, 0, 10, -1, 1, false));
    }

    private sealed class VisibleRootControl : Control
    {
        public override bool Visible
        {
            get => true;
            set { }
        }
    }

    private static void AssertHasFlag(AccessibleActions value, AccessibleActions flag)
        => Assert.Equal(flag, value & flag);

    private static void AssertHasFlag(AccessibleStates value, AccessibleStates flag)
        => Assert.Equal(flag, value & flag);

    private static void AssertDoesNotHaveFlag(AccessibleActions value, AccessibleActions flag)
        => Assert.Equal(AccessibleActions.None, value & flag);

    private static void AssertDoesNotHaveFlag(AccessibleStates value, AccessibleStates flag)
        => Assert.Equal(AccessibleStates.None, value & flag);

    private static void AssertOccurrenceIdentityAcrossMove(
        AccessibleObject root,
        ListBoxItemCollection items)
    {
        AccessibleObject firstNode = Assert.IsAssignableFrom<AccessibleObject>(root.GetChild(0));
        AccessibleObject secondNode = Assert.IsAssignableFrom<AccessibleObject>(root.GetChild(1));
        long firstId = firstNode.RuntimeId;
        long secondId = secondNode.RuntimeId;

        Assert.NotSame(firstNode, secondNode);
        Assert.NotEqual(firstId, secondId);

        items.Move(0, 1);

        Assert.Same(firstNode, root.GetChild(1));
        Assert.Same(secondNode, root.GetChild(0));
        Assert.Equal(firstId, firstNode.RuntimeId);
        Assert.Equal(secondId, secondNode.RuntimeId);
    }

    private static void AssertEveryAdvertisedActionCanExecute(AccessibleObject accessible)
    {
        AccessibleActions advertised = accessible.SupportedActions;

        foreach (AccessibleActions action in Enum.GetValues<AccessibleActions>())
        {
            if (action == AccessibleActions.None || (advertised & action) == 0)
                continue;

            object? parameter = action == AccessibleActions.SetValue
                ? accessible.ControlType == AccessibleControlType.Edit
                    ? "updated"
                    : accessible.RangeValue?.Value
                : null;

            Assert.True(
                accessible.PerformAction(action, parameter),
                $"{accessible.ControlType} advertised {action} but rejected it.");
        }
    }

    private static IWindowImpl CreateWindowImplementation()
        => DispatchProxy.Create<IWindowImpl, WindowImplProxy>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (AccessibleObject Peer, WeakReference ControlReference) CreateCollectibleControlPeer()
    {
        var control = new Button();
        return (control.AccessibilityObject, new WeakReference(control));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (AccessibleObject Peer, WeakReference OwnerReference, WeakReference ItemReference)
        CreateCollectibleLogicalItemPeer()
    {
        var root = new VisibleRootControl();
        var owner = root.Controls.Add(new ListBox());
        var item = new NamedItem("Collectible");
        owner.Items.Add(item);
        AccessibleObject peer = Assert.IsAssignableFrom<AccessibleObject>(owner.AccessibilityObject.GetChild(0));
        return (peer, new WeakReference(owner), new WeakReference(item));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class NamedItem
    {
        private readonly string text;

        public NamedItem(string text)
        {
            this.text = text;
        }

        public override string ToString() => text;
    }

    private sealed class CustomSemanticControl : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance()
            => new CustomSemanticAccessibleObject(this);
    }

    private sealed class CustomSemanticAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly CustomSemanticChild child;

        public CustomSemanticAccessibleObject(CustomSemanticControl owner)
            : base(owner)
        {
            child = new CustomSemanticChild(this, () => ActionInvoked = true);
        }

        public bool ActionInvoked { get; private set; }

        public override AccessibleControlType ControlType => AccessibleControlType.Group;

        public override AccessibleRole Role => AccessibleRole.Grouping;

        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            yield return child;
        }
    }

    private sealed class CustomSemanticChild : AccessibleObject
    {
        private readonly WeakReference<AccessibleObject> parent;
        private readonly Action invoke;

        public CustomSemanticChild(AccessibleObject parent, Action invoke)
        {
            this.parent = new WeakReference<AccessibleObject>(parent);
            this.invoke = invoke;
            AutomationId = "painted.action";
        }

        public override Rectangle Bounds => new(4, 5, 60, 20);

        public override AccessibleControlType ControlType => AccessibleControlType.Button;

        public override string? Name
        {
            get => "Painted action";
            set { }
        }

        public override AccessibleObject? Parent
            => parent.TryGetTarget(out AccessibleObject? value) ? value : null;

        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleStates State => AccessibleStates.Focusable;

        public override AccessibleActions SupportedActions => AccessibleActions.Invoke;

        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action != AccessibleActions.Invoke || parameter is not null)
                return false;

            invoke();
            return true;
        }
    }

    private class WindowImplProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITopLevelImpl.CreatePopup))
                return DispatchProxy.Create<IPopupImpl, WindowImplProxy>();

            if (targetMethod?.Name is "get_RenderScaling" or "get_DesktopScaling")
                return 1d;

            Type? returnType = targetMethod?.ReturnType;
            return returnType is not null && returnType != typeof(void) && returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }
}

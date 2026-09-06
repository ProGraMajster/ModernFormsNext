using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;
using Rect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public class AndroidAccessibilityMapperTests
{
    [Theory]
    [InlineData(AccessibleControlType.Default, "android.view.View")]
    [InlineData(AccessibleControlType.Custom, "android.view.View")]
    [InlineData(AccessibleControlType.Window, "android.app.Dialog")]
    [InlineData(AccessibleControlType.Pane, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.Group, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.Text, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Button, "android.widget.Button")]
    [InlineData(AccessibleControlType.CheckBox, "android.widget.CheckBox")]
    [InlineData(AccessibleControlType.RadioButton, "android.widget.RadioButton")]
    [InlineData(AccessibleControlType.Switch, "android.widget.Switch")]
    [InlineData(AccessibleControlType.Edit, "android.widget.EditText")]
    [InlineData(AccessibleControlType.ComboBox, "android.widget.Spinner")]
    [InlineData(AccessibleControlType.List, "android.widget.ListView")]
    [InlineData(AccessibleControlType.ListItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Tree, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.TreeItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Tab, "android.widget.TabWidget")]
    [InlineData(AccessibleControlType.TabItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Menu, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.MenuItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Slider, "android.widget.SeekBar")]
    [InlineData(AccessibleControlType.ProgressBar, "android.widget.ProgressBar")]
    [InlineData(AccessibleControlType.ScrollBar, "android.view.View")]
    [InlineData(AccessibleControlType.Dialog, "android.app.Dialog")]
    [InlineData(AccessibleControlType.Image, "android.widget.ImageView")]
    [InlineData(AccessibleControlType.ToolBar, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.Separator, "android.view.View")]
    public void UsesAndroidClasses(AccessibleControlType type, string expected)
        => Assert.Equal(expected, ClassName((int)type));

    [Fact]
    public void EditorKeepsLabelHelpAndTextSeparate()
    {
        var peer = new TestPeer { Type = AccessibleControlType.Edit, Name = "Account",
            Value = "entered value", DescriptionText = "Help", Actions = AccessibleActions.SetValue };
        var result = Read(Adapt(peer));
        Assert.Equal("Account", result.Label);
        Assert.Equal("entered value", result.Text);
        Assert.Equal("Help", result.Help);
        Assert.Null(result.StateDescription);
        Assert.True(result.Editable);
    }

    [Theory]
    [InlineData(true, AccessibleStates.None)]
    [InlineData(false, AccessibleStates.Protected)]
    public void SensitivePeerValueIsNeverRead(bool sensitive, AccessibleStates state)
    {
        var peer = new TestPeer { Type = AccessibleControlType.Edit, Sensitive = sensitive,
            States = state, ThrowOnValueRead = true, Name = "Password" };
        var result = Read(Adapt(peer));
        Assert.True(result.Password);
        Assert.Null(result.Text);
        Assert.Null(result.StateDescription);
        Assert.Null(result.Range);
        Assert.Equal("Password", result.Label);
    }

    [Fact]
    public void PasswordTextBoxSetTextUsesNormalChangeEventAndRedactsReadback()
    {
        using var root = new Panel();
        using var surface = new SkiaControlSurface(root);
        var editor = new TextBox { AccessibleName = "Password", PasswordCharacter = '*', Size = new(120, 30) };
        root.Controls.Add(editor);
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;
        string input = Guid.NewGuid().ToString();
        Assert.True(AndroidAccessibilityMapper.PerformAction(Adapt(editor.AccessibilityObject), ActionSetText, input));
        Assert.Equal(1, changes);
        Assert.True(editor.Text == input); // Assertion failures do not print the password.
        Assert.Null(Read(Adapt(editor.AccessibilityObject)).Text);
        editor.ReadOnly = true;
        Assert.False(AndroidAccessibilityMapper.PerformAction(Adapt(editor.AccessibilityObject), ActionSetText, ""));
        editor.ReadOnly = false;
        editor.Enabled = false;
        Assert.DoesNotContain(ActionSetText, Actions(Adapt(editor.AccessibilityObject)));
    }

    [Fact]
    public void MapsMixedAndInputStates()
    {
        var peer = new TestPeer { Type = AccessibleControlType.CheckBox,
            States = AccessibleStates.Mixed | AccessibleStates.Focusable | AccessibleStates.Focused | AccessibleStates.Selected };
        var state = Read(Adapt(peer));
        Assert.True(state.Checkable);
        Assert.False(state.Checked);
        Assert.Equal("Mixed", state.StateDescription);
        Assert.True(state.Focused && state.Focusable && state.Selected && state.Enabled);
    }

    [Fact]
    public void SwitchUsesNativeOnOffStateInsteadOfItsNumericValue()
    {
        // Semantic tests do not pump a UI frame clock or exercise visual transitions.
        using var control = new Switch { Animate = false };
        var peer = Adapt(control.AccessibilityObject);
        Assert.Null(Read(peer).StateDescription);
        Assert.False(Read(peer).Checked);
        control.Toggle();
        Assert.Null(Read(peer).StateDescription);
        Assert.True(Read(peer).Checked);
        control.Mode = SwitchMode.ThreeState;
        control.Value = 0;
        Assert.Equal("Mixed", Read(peer).StateDescription);
    }

    [Theory]
    [InlineData(AccessibleActions.Invoke, ActionClick)]
    [InlineData(AccessibleActions.Toggle, ActionClick)]
    [InlineData(AccessibleActions.Select, ActionClick)]
    [InlineData(AccessibleActions.Select, ActionSelect)]
    [InlineData(AccessibleActions.Focus, ActionFocus)]
    [InlineData(AccessibleActions.Expand, ActionExpand)]
    [InlineData(AccessibleActions.Collapse, ActionCollapse)]
    [InlineData(AccessibleActions.ScrollIntoView, ActionShowOnScreen)]
    public void DispatchesOnlyAdvertisedCanonicalAction(AccessibleActions canonical, int android)
    {
        var peer = new TestPeer { Actions = canonical };
        Assert.True(AndroidAccessibilityMapper.PerformAction(Adapt(peer), android, null));
        Assert.Equal(canonical, peer.LastAction);
        Assert.False(AndroidAccessibilityMapper.PerformAction(Adapt(peer), android, new object()));
        peer.States = AccessibleStates.Unavailable;
        Assert.Empty(Actions(Adapt(peer)));
    }

    [Fact]
    public void LeafAndMissingContractsDoNotAdvertiseCapabilities()
    {
        var peer = new TestPeer { Type = AccessibleControlType.TreeItem, Actions = AccessibleActions.Scroll };
        Assert.Empty(Actions(Adapt(peer)));
        Assert.False(AndroidAccessibilityMapper.PerformAction(Adapt(peer), 2, null));
        Assert.False(AndroidAccessibilityMapper.PerformAction(Adapt(peer), 32, null));
        Assert.False(AndroidAccessibilityMapper.PerformAction(Adapt(peer), 16908343, null));
    }

    [Fact]
    public void RangesRespectReadOnlyLimitsAndInvalidInput()
    {
        var peer = new TestPeer { Type = AccessibleControlType.Slider,
            Range = new(5, 0, 10, 1, 2, false),
            Actions = AccessibleActions.SetValue | AccessibleActions.Increment | AccessibleActions.Decrement };
        var node = Adapt(peer);
        Assert.Equal(5, Read(node).Range!.Value.Value);
        Assert.Contains(ActionSetProgress, Actions(node));
        Assert.True(AndroidAccessibilityMapper.PerformAction(node, ActionSetProgress, 7d));
        Assert.Equal(7d, peer.LastParameter);
        Assert.False(AndroidAccessibilityMapper.PerformAction(node, ActionSetProgress, double.NaN));
        Assert.False(AndroidAccessibilityMapper.PerformAction(node, ActionSetProgress, 11d));
        peer.Range = new(10, 0, 10, 1, 2, false);
        Assert.DoesNotContain(ActionScrollForward, Actions(node));
        peer.Range = new(5, 0, 10, 1, 2, true);
        Assert.DoesNotContain(ActionSetProgress, Actions(node));
        Assert.DoesNotContain(ActionScrollBackward, Actions(node));
    }

    [Theory]
    [InlineData(1, 10, 20, 3, 4, 16, 28)]
    [InlineData(2.5, 10, 20, 3, 4, 40, 70)]
    public void DensityAndHostOffsetAreAppliedOnce(double density, double x, double y,
        double width, double height, double right, double bottom)
    {
        Rect result = AndroidAccessibilityBounds.ToScreen(new(x, y, width, height), density, 3 * density, 4 * density);
        Assert.Equal(right, result.Right);
        Assert.Equal(bottom, result.Bottom);
    }

    [Fact]
    public void BoundsRoundOutwardAndRejectInvalidScale()
    {
        Assert.Equal(new Rect(-2, -2, 4, 4), AndroidAccessibilityBounds.ToScreen(new(-1, -1, 2, 2), 1.5, 0, 0));
        Assert.Equal(default, AndroidAccessibilityBounds.ToScreen(new(0, 0, 10, 10), double.NaN, 0, 0));
        Assert.False(AndroidAccessibilityBounds.Valid(new(0, 0, 0, 10)));
    }

    [Fact]
    public void VisibilityIncludesAncestorsViewportAndLogicalRows()
    {
        var root = new TestPeer { Rectangle = new(0, 0, 100, 100) };
        var row = root.Add(new TestPeer { Type = AccessibleControlType.TreeItem, Rectangle = new(0, 0, 100, 20) });
        var child = row.Add(new TestPeer { Rectangle = new(10, 30, 100, 20) });
        Assert.Equal(new Rect(10, 30, 90, 20), AndroidAccessibilityBounds.Clip(Adapt(child), Adapt(root), new(0, 0, 100, 100)));
        root.States = AccessibleStates.Invisible;
        Assert.Equal(default, AndroidAccessibilityBounds.Clip(Adapt(child), Adapt(root), new(0, 0, 100, 100)));
        root.States = AccessibleStates.None;
        child.States = AccessibleStates.Offscreen;
        Assert.Equal(default, AndroidAccessibilityBounds.Clip(Adapt(child), Adapt(root), new(0, 0, 100, 100)));
    }

    [Fact]
    public void CollectionsUseOnlyKnownFlatListDimensions()
    {
        var list = new TestPeer { Type = AccessibleControlType.List, States = AccessibleStates.MultiSelectable };
        list.Add(new TestPeer { Type = AccessibleControlType.ListItem });
        list.Add(new TestPeer { Type = AccessibleControlType.ListItem });
        Assert.Equal(new AndroidCollection(2, 1, 2), Collection(Adapt(list)));
        list.Add(new TestPeer { Type = AccessibleControlType.Button });
        Assert.Null(Collection(Adapt(list)));
        list.Type = AccessibleControlType.Tree;
        Assert.Null(Collection(Adapt(list)));
    }

    [Theory]
    [InlineData(AccessibilityView.Default, true)]
    [InlineData(AccessibilityView.Raw, false)]
    [InlineData(AccessibilityView.Control, true)]
    [InlineData(AccessibilityView.Content, true)]
    public void RawKeepsHierarchyWithoutNormalImportance(AccessibilityView view, bool important)
        => Assert.Equal(important, Read(Adapt(new TestPeer { Projection = view })).Important);

    internal static IPlatformAccessibleObject Adapt(AccessibleObject peer) => PlatformAccessibleObjectAdapter.From(peer)!;
}

internal sealed class TestPeer : AccessibleObject
{
    internal AccessibleControlType Type = AccessibleControlType.Custom;
    internal AccessibleStates States;
    internal AccessibleActions Actions;
    internal AccessibilityView Projection = AccessibilityView.Control;
    internal bool Sensitive, ThrowOnValueRead;
    internal Rectangle Rectangle = new(0, 0, 100, 30);
    internal string? DescriptionText;
    internal AccessibleRangeValue? Range;
    internal TestPeer? ParentPeer;
    internal List<TestPeer> Children = [];
    internal AccessibleActions LastAction;
    internal object? LastParameter;
    private string? value;
    public override AccessibleControlType ControlType => Type;
    public override AccessibleStates State => States;
    public override AccessibleActions SupportedActions => Actions;
    public override AccessibilityView View => Projection;
    public override bool IsSensitive => Sensitive;
    public override Rectangle Bounds => Rectangle;
    public override string? Description => DescriptionText;
    public override AccessibleRangeValue? RangeValue => Range;
    public override AccessibleObject? Parent => ParentPeer;
    public override string? Value { get => ThrowOnValueRead ? throw new InvalidOperationException() : value; set => this.value = value; }
    public override int GetChildCount() => Children.Count;
    public override AccessibleObject? GetChild(int index) => index >= 0 && index < Children.Count ? Children[index] : null;
    public override AccessibleObject? GetFocused() => (State & AccessibleStates.Focused) != 0 ? this
        : Children.Select(c => c.GetFocused()).FirstOrDefault(c => c is not null);
    internal TestPeer Add(TestPeer child) { child.ParentPeer = this; Children.Add(child); return child; }
    public override bool PerformAction(AccessibleActions action, object? parameter = null)
    {
        LastAction = action;
        LastParameter = parameter;
        if (action == AccessibleActions.Focus) States |= AccessibleStates.Focused;
        return true;
    }
}

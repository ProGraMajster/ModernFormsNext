using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[Collection("Android accessibility text")]
public sealed class AndroidAccessibilityPrivacyTests
{
    [Fact]
    public void ProtectedAncestorPreventsEveryStringAndRangeGetter()
    {
        var parent = new TestPeer { States = AccessibleStates.Protected };
        var peer = new ReentrantPeer { ParentPeer = parent, RejectPayload = true };
        var node = Adapt(peer);
        var properties = Read(node);
        AssertRedacted(properties);
        _ = Actions(node); // Native CreateNode also builds actions before its final privacy check.
        Assert.Null(ReadAutomationId(node));
        Assert.Equal(0, peer.Reads);
        Assert.True(CanExposeProperties(node, properties, null));
    }

    [Theory]
    [InlineData("Value")]
    [InlineData("Name")]
    [InlineData("Help")]
    [InlineData("Description")]
    [InlineData("Range")]
    [InlineData("View")]
    public void ReentrantPayloadGetterCannotReturnPreviouslyReadMetadata(string trigger)
    {
        var parent = new TestPeer();
        var peer = new ReentrantPeer { ParentPeer = parent };
        peer.ReadCallback = property => { if (property == trigger) parent.States |= AccessibleStates.Protected; };
        var properties = Read(Adapt(peer));
        AssertRedacted(properties);
    }

    [Fact]
    public void OwnPasswordKeepsExplicitLabelHelpAndIdWhileValueIsNeverRead()
    {
        using var editor = new TextBox { AccessibleName = "Password", AccessibleDescription = "Enter the account password",
            AccessibleAutomationId = "account.password", PasswordCharacter = '*', Text = "never exposed" };
        using var surface = new SkiaControlSurface(editor);
        surface.Resize(220, 50);
        var node = Adapt(editor.AccessibilityObject);
        var properties = Read(node);
        Assert.True(properties.Password);
        Assert.Equal("Password", properties.Label);
        Assert.Equal("Enter the account password", properties.Help);
        Assert.Equal("account.password", ReadAutomationId(node));
        Assert.Null(properties.Text);
        Assert.Null(properties.Range);
        Assert.Contains(ActionSetText, Actions(node));
        Assert.True(AndroidAccessibilityMapper.PerformAction(node, ActionSetText, "new private value"));
        Assert.Equal("new private value", editor.Text);
        Assert.Null(Read(node).Text);
        Assert.True(CanExposeProperties(node, properties, "account.password"));
    }

    [Fact]
    public void FinalNativeCheckRejectsOwnPasswordMetadataAfterParentBecomesProtected()
    {
        var parent = new TestPeer();
        var peer = new ReentrantPeer { ParentPeer = parent, Sensitive = true };
        var node = Adapt(peer);
        var properties = Read(node);
        string? id = ReadAutomationId(node);
        Assert.True(properties.Password);
        Assert.NotNull(properties.Label);
        parent.States = AccessibleStates.Protected;
        Assert.False(CanExposeProperties(node, properties, id));
        Assert.Null(ReadAutomationId(node));
    }

    [Fact]
    public void AutomationIdGetterCannotExportMetadataAfterChangingParentPrivacy()
    {
        var parent = new TestPeer();
        var peer = new ReentrantPeer { ParentPeer = parent };
        peer.ReadCallback = property => { if (property == "Id") parent.Sensitive = true; };
        Assert.Null(ReadAutomationId(Adapt(peer)));
    }

    [Fact]
    public void ExistingGridGeneratedNamesAndValuesAreRedactedUnderProtectedContainer()
    {
        using var parent = new ProtectedPanel();
        var grid = parent.Controls.Add(new DataGridView());
        grid.Columns.Add("private column"); grid.Rows.Add("private value");
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(400, 200);
        var cell = grid.AccessibilityObject.GridProvider!.GetItem(0, 0)!;
        var node = Adapt(cell);
        Assert.Equal("private column", Read(node).Label);
        parent.Protected = true;
        AssertRedacted(Read(node));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProtectedRangeActionsDoNotInspectPayloadOrAdvertiseAdjustment(bool protectDuringRead)
    {
        var parent = new TestPeer { States = protectDuringRead ? AccessibleStates.None : AccessibleStates.Protected };
        var peer = new ReentrantPeer { ParentPeer = parent, Type = AccessibleControlType.Spinner,
            Actions = AccessibleActions.SetValue | AccessibleActions.Increment | AccessibleActions.Decrement | AccessibleActions.Focus,
            RejectPayload = !protectDuringRead };
        if (protectDuringRead) peer.ReadCallback = property => { if (property == "Range") parent.States |= AccessibleStates.Protected; };
        var node = Adapt(peer);
        var actions = Actions(node);
        Assert.DoesNotContain(ActionSetProgress, actions);
        Assert.DoesNotContain(ActionSetText, actions);
        Assert.DoesNotContain(ActionScrollForward, actions);
        Assert.DoesNotContain(ActionScrollBackward, actions);
        Assert.Contains(ActionFocus, actions);
        Assert.Equal(protectDuringRead ? 1 : 0, peer.Reads);
    }

    private static IPlatformAccessibleObject Adapt(AccessibleObject peer) => PlatformAccessibleObjectAdapter.From(peer)!;
    private static void AssertRedacted(AndroidAccessibilityProperties properties)
    {
        Assert.True(properties.Password);
        Assert.Null(properties.Label);
        Assert.Null(properties.Text);
        Assert.Null(properties.Help);
        Assert.Null(properties.StateDescription);
        Assert.Null(properties.Range);
    }

    private sealed class ProtectedPanel : Panel
    {
        internal bool Protected;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(ProtectedPanel owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleStates State => base.State | (owner.Protected ? AccessibleStates.Protected : 0);
        }
    }

    private sealed class ReentrantPeer : AccessibleObject
    {
        internal AccessibleObject? ParentPeer;
        internal bool RejectPayload, Sensitive;
        internal AccessibleControlType Type = AccessibleControlType.Edit;
        internal AccessibleActions Actions;
        internal int Reads;
        internal Action<string>? ReadCallback;
        private string ReadValue(string property)
        {
            Reads++;
            if (RejectPayload) throw new InvalidOperationException("A protected getter was executed.");
            ReadCallback?.Invoke(property);
            return "private " + property;
        }
        public override AccessibleObject? Parent => ParentPeer;
        public override AccessibleControlType ControlType => Type;
        public override AccessibleActions SupportedActions => Actions;
        public override bool IsSensitive => Sensitive;
        public override string? Name { get => ReadValue("Name"); set { } }
        public override string? Value { get => ReadValue("Value"); set { } }
        public override string? Help { get { _ = ReadValue("Help"); return null; } }
        public override string? Description => ReadValue("Description");
        public override string? AutomationId { get => ReadValue("Id"); set { } }
        public override AccessibleRangeValue? RangeValue { get { _ = ReadValue("Range"); return new(1, 0, 2, 1, 1, false); } }
        public override AccessibilityView View { get { ReadCallback?.Invoke("View"); return AccessibilityView.Control; } }
    }
}

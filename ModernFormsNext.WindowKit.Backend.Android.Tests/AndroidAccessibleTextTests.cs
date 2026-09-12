using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[CollectionDefinition("Android accessibility text", DisableParallelization = true)]
public sealed class AndroidAccessibleTextCollection { }

[Collection("Android accessibility text")]
public sealed class AndroidAccessibleTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionAndReverseExtendedMovementUseExistingDocument(bool readOnly)
    {
        using var f = new Fixture(new TextBox { Text = "A😀BC", ReadOnly = readOnly });
        Assert.Contains(ActionSetTextSelection, Actions(f.Node));
        Assert.True(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(5, 5)));
        Assert.True(PerformAction(f.Node, ActionPreviousText, new TextMovementRequest(1, true)));
        Assert.True(PerformAction(f.Node, ActionPreviousText, new TextMovementRequest(1, true)));
        var state = f.Editor.QueryTextInputClient()!.GetState()!;
        Assert.Equal((5, 3), (state.SelectionStart, state.SelectionEnd));
        Assert.True(PerformAction(f.Node, ActionPreviousText, new TextMovementRequest(1, true)));
        state = f.Editor.QueryTextInputClient()!.GetState()!;
        Assert.Equal((5, 1), (state.SelectionStart, state.SelectionEnd));
        Assert.Equal("A😀BC", f.Editor.Text);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(1, 99)]
    public void InvalidSelectionDoesNotFinishComposition(int start, int end)
    {
        using var f = new Fixture(new TextBox { Text = "text" });
        var client = f.Editor.QueryTextInputClient()!;
        Assert.True(client.SetComposingText("中"));
        Assert.False(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(start, end)));
        Assert.True(client.GetState()!.HasComposition);
    }

    [Fact]
    public void CurrentRouteGuardRunsBeforeSelectionAndSensitiveTextHasNoActions()
    {
        using var f = new Fixture(new TextBox { Text = "abcd" });
        Assert.False(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(0, 4), () => false));
        Assert.False(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(0, null)));
        Assert.False(PerformAction(f.Node, ActionNextText, new TextMovementRequest(3, false)));
        f.Editor.TextInputOptions = new() { Scope = TextInputScope.Password };
        Assert.DoesNotContain(ActionSetTextSelection, Actions(f.Node));
        Assert.Null(Read(f.Node).Text);
        Assert.False(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(0, 4)));
    }

    [Fact]
    public void ExplicitSelectionAcceptsPreeditAndClearSelectionUsesActualCaret()
    {
        using var f = new Fixture(new RichTextBox { Text = "abcd" });
        var client = f.Editor.QueryTextInputClient()!;
        Assert.True(client.SetSelection(0, 4));
        Assert.True(client.SetComposingText("日本"));
        Assert.True(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(2, 0)));
        Assert.False(client.GetState()!.HasComposition);
        Assert.True(PerformAction(f.Node, ActionSetTextSelection, new TextSelectionRequest(null, null)));
        Assert.Equal((0, 0), (client.GetState()!.SelectionStart, client.GetState()!.SelectionEnd));
        Assert.Equal("日本", f.Editor.Text);
    }

    [Fact]
    public void ProtectedAncestorStopsNativeCapabilityBeforeCustomProviderAndValueGetters()
    {
        using var parent = new ProtectedPanel();
        var editor = parent.Controls.Add(new GuardedEditor { Text = "protected document" });
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(400, 150);
        var node = PlatformAccessibleObjectAdapter.From(editor.AccessibilityObject)!;
        Assert.NotNull(node.GetTextProvider());
        editor.ProviderReads = editor.ValueReads = 0;
        parent.Protected = true;
        editor.RejectReads = true;
        Assert.Null(node.GetTextProvider());
        Assert.Null(Read(node).Text);
        Assert.DoesNotContain(ActionSetTextSelection, Actions(node));
        Assert.False(PerformAction(node, ActionSetTextSelection, new TextSelectionRequest(0, 2)));
        Assert.Equal(0, editor.ProviderReads);
        Assert.Equal(0, editor.ValueReads);
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

    private sealed class GuardedEditor : TextBox
    {
        internal bool RejectReads;
        internal int ProviderReads, ValueReads;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(GuardedEditor owner) : ControlAccessibleObject(owner)
        {
            public override string? Value {
                get { owner.ValueReads++; return owner.RejectReads ? throw new InvalidOperationException("private text") : base.Value; }
                set => base.Value = value;
            }
            public override AccessibleTextProvider? TextProvider {
                get { owner.ProviderReads++; return owner.RejectReads ? throw new InvalidOperationException("private provider") : base.TextProvider; }
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Panel root = new();
        private readonly SkiaControlSurface surface;
        internal TextBox Editor { get; }
        internal IPlatformAccessibleObject Node => PlatformAccessibleObjectAdapter.From(Editor.AccessibilityObject)!;
        internal Fixture(TextBox editor)
        {
            Editor = root.Controls.Add(editor);
            editor.Bounds = new Rectangle(0, 0, 400, 100);
            surface = new(root); surface.Resize(500, 200); editor.Select();
        }
        public void Dispose() { surface.Dispose(); root.Dispose(); }
    }
}

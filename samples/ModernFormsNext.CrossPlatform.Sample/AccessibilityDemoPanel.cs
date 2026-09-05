using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>A compact shared-control fixture for Android accessibility and TalkBack checks.</summary>
/// <remarks>Opened explicitly by the Android sample's ACCESSIBILITY_DEMO intent extra.</remarks>
internal sealed class AccessibilityDemoPanel : Panel
{
    internal readonly Button InvokeButton = new() { Text = "Invoke sample" };
    internal readonly CheckBox Check = new() { Text = "Check sample" };
    internal readonly TextBox Editor = new() { AccessibleName = "Editor sample", Text = "Initial text" };
    internal readonly TextBox Password = new() { AccessibleName = "Password sample", PasswordCharacter = '*' };
    internal readonly ListBox List = new() { AccessibleName = "List sample" };
    internal readonly TreeView Tree = new() { AccessibleName = "Tree sample" };
    internal readonly TrackBar Slider = new() { AccessibleName = "Slider sample", Minimum = 0, Maximum = 100, Value = 25 };
    internal readonly Button Dynamic = new() { Text = "Add or remove item" };
    internal int Invocations;

    internal AccessibilityDemoPanel()
    {
        Dock = DockStyle.Fill;
        AccessibleName = "Accessibility sample";
        BackColor = SkiaSharp.SKColors.White;
        InvokeButton.Click += (_, _) => Invocations++;
        List.Items.Add("First item"); List.Items.Add("Second item");
        var branch = Tree.Items.Add("Branch item");
        branch.Items.Add("Leaf item");
        var tabs = new TabControl { AccessibleName = "Tabs sample" };
        tabs.TabPages.Add(new TabPage { Text = "First tab" });
        tabs.TabPages.Add(new TabPage { Text = "Second tab" });
        var menu = new Menu { AccessibleName = "Menu sample" };
        menu.Items.Add("Menu command", onClick: (_, _) => Invocations++);
        var listView = new ListView { AccessibleName = "ListView sample" };
        listView.Items.Add("ListView item");
        Dynamic.Click += (_, _) =>
        {
            if (List.Items.Count > 2) List.Items.RemoveAt(2);
            else List.Items.Add("Dynamic item");
        };
        Controls.AddRange([
            InvokeButton, Check,
            new RadioButton { Text = "Radio sample" }, new Switch { AccessibleName = "Switch sample" },
            Editor, Password,
            new ComboBox { AccessibleName = "Combo sample" }, new Button { Text = "Disabled sample", Enabled = false },
            List, Tree, tabs, listView, Slider,
            new ProgressBar { AccessibleName = "Progress sample", Value = 40 }, menu,
            new LogicalButton { Text = "Custom action", AccessibleName = "Custom group" },
            Dynamic, new Label { Text = "TalkBack: review each control" },
            new Button { Text = "Hidden sample", AccessibilityView = AccessibilityView.Hidden }
        ]);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int width = Math.Max(120, (Width - 36) / 2);
        for (int i = 0; i < Controls.Count; i++)
        {
            int row = i / 2;
            int y = 12 + row * 48 + (row > 4 ? 58 : 0) + (row > 5 ? 58 : 0);
            Controls[i].SetBounds(12 + (i % 2) * (width + 12), y, width, row is 4 or 5 ? 100 : 40);
        }
    }

    private sealed class LogicalButton : Button
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new GroupPeer(this);

        private sealed class GroupPeer : ControlAccessibleObject
        {
            private readonly ChildPeer child;
            internal GroupPeer(LogicalButton owner) : base(owner) => child = new(this, owner);
            public override AccessibleControlType ControlType => AccessibleControlType.Group;
            public override AccessibleActions SupportedActions => AccessibleActions.None;
            protected override IEnumerable<AccessibleObject> GetAccessibilityChildren() { yield return child; }
        }

        private sealed class ChildPeer(GroupPeer parent, LogicalButton owner) : AccessibleObject
        {
            private readonly WeakReference<LogicalButton> ownerReference = new(owner);
            public override AccessibleObject Parent => parent;
            public override string? Name { get => "Logical action"; set { } }
            public override AccessibleControlType ControlType => AccessibleControlType.Button;
            public override Rectangle Bounds => parent.Bounds;
            public override AccessibilityView View => parent.View;
            public override AccessibleStates State => parent.State;
            public override AccessibleActions SupportedActions => AccessibleActions.Invoke;
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (action != AccessibleActions.Invoke || parameter is not null
                    || parent.View == AccessibilityView.Hidden
                    || !ownerReference.TryGetTarget(out var target) || !target.Enabled) return false;
                target.PerformClick();
                NotifyClients(AccessibleEvents.StateChange);
                return true;
            }
        }
    }
}

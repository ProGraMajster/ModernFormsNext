using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.DataBinding;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>Finite navigator currency, editing and binding fixture for real platform hosts.</summary>
/// <remarks>UI thread only. Uses production controls/services and in-memory data, with no persistence.</remarks>
internal sealed class BindingNavigatorCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly BindingSource source = new();
    private readonly Label caption;
    private int assertions;

    internal Panel Root { get; }
    internal BindingNavigator Navigator { get; }
    internal int Position => source.Position;
    internal int PaintCount => ((PaintRoot)Root).PaintCount;
    internal Rectangle ContentPixelBounds => new(caption.PointToScreen(Point.Empty), caption.LogicalToDeviceUnits(caption.Size));

    internal BindingNavigatorCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        source.DataSource = new BindingList<Record> { new() { Name = "First record" }, new() { Name = "Second record" } };
        Root = owner.Add(new PaintRoot
        {
            Dock = DockStyle.Fill, Name = "NavigatorCertification", BindingContext = new BindingContext()
        });
        Navigator = Root.Controls.Add(new BindingNavigator(source)
        {
            Name = "BindingNavigator", WrapContents = true, Height = 150
        });
        caption = Root.Controls.Add(new Label
        {
            Name = "CurrentRecord", Bounds = new(12, 180, 306, 50), BackColor = SKColors.PaleGreen,
            TextAlign = ContentAlignment.MiddleLeft
        });
        caption.DataBindings.Add(nameof(Label.Text), source, nameof(Record.Name), true, DataSourceUpdateMode.Never);
        Root.Controls.Add(new Label
        {
            Bounds = new(12, 250, 306, 70), Multiline = true,
            Text = "Native BindingNavigator\nSource currency and local edits.\nNo persistence or network work."
        });
        Root.BringToFront();
    }

    internal void Exercise()
    {
        var rows = new BindingList<Record> { new() { Name = "First record" }, new() { Name = "Second record" } };
        source.DataSource = rows;
        Check(Root.Created && Navigator.Created && Navigator.PositionTextBox.Created, "attached-production-tree");
        Check(Navigator.PositionTextBox.Text == "1" && Navigator.CountLabel.Text == "of 2", "initial-currency");
        Check(!Navigator.MovePreviousButton.Enabled && Navigator.MoveNextButton.Enabled, "navigation-boundaries");
        var peer = Navigator.PositionTextBox.AccessibilityObject;
        long identity = peer.RuntimeId;
        Check(Navigator.MoveNextButton.AccessibilityObject.PerformAction(AccessibleActions.Invoke), "semantic-navigation");
        Check(source.Position == 1 && Navigator.PositionTextBox.Text == "2" && caption.Text == "Second record", "bound-current-update");
        Navigator.MoveFirstButton.PerformClick();
        Navigator.AddNewButton.PerformClick();
        Check(source.Count == 3 && Navigator.CountLabel.Text == "of 3", "add-new");
        Navigator.DeleteButton.PerformClick();
        Check(source.Count == 2 && Navigator.CountLabel.Text == "of 2", "delete-current");
        var row = (Record)source.Current!;
        row.BeginEdit();
        string original = row.Name;
        row.Name = "Draft record";
        Navigator.CancelButton.PerformClick();
        Check(row.Name == original, "cancel-local-edit");
        row.BeginEdit();
        row.Name = "Saved current record";
        Navigator.SaveButton.PerformClick();
        row.BeginEdit();
        row.Name = "Later draft";
        Navigator.CancelButton.PerformClick();
        Check(row.Name == "Saved current record", "end-edit-without-persistence");
        var defaultSave = Navigator.SaveButton.Command;
        object token = new();
        object? observed = null;
        Navigator.SaveButton.Command = new DelegateCommand(value => observed = value);
        Navigator.SaveButton.CommandParameter = token;
        Navigator.SaveButton.PerformClick();
        Check(ReferenceEquals(observed, token), "application-command-and-parameter");
        Navigator.SaveButton.Command = defaultSave;
        Navigator.SaveButton.CommandParameter = null;
        source.DataSource = Array.AsReadOnly(rows.ToArray());
        Check(!Navigator.AddNewButton.Enabled && !Navigator.DeleteButton.Enabled
            && !Navigator.SaveButton.Enabled && !Navigator.CancelButton.Enabled, "read-only-edit-policy");
        source.DataSource = rows;
        Navigator.RightToLeft = RightToLeft.Yes;
        Root.PerformLayout(); Navigator.PerformLayout();
        Check(Navigator.MoveFirstButton.RightToLeft == RightToLeft.Yes, "canonical-rtl-inheritance");
        Navigator.RightToLeft = RightToLeft.No;
        Check(peer.RuntimeId == identity && ReferenceEquals(peer, Navigator.PositionTextBox.AccessibilityObject), "stable-semantic-identity");
        Check(!Navigator.CancelButton.CausesValidation, "cancel-validation-opt-out");
        PreparePosition(false);
        output($"BINDING_NAVIGATOR:PASS:{assertions}");
    }

    internal void PreparePosition(bool alternate)
    {
        source.Position = alternate ? 1 : 0;
        Navigator.RefreshItems();
        Check(Navigator.PositionTextBox.Text == (alternate ? "2" : "1"), "current-position-presentation");
        Check(caption.Text == ((Record)source.Current!).Name, "data-binding-presentation");
        Root.Invalidate();
    }

    internal void CheckRetainedState()
    {
        Check(ReferenceEquals(Navigator.BindingSource, source) && source.Count == 2 && source.Position == 1,
            "retained-source-and-currency");
        Check(Navigator.PositionTextBox.Text == "2" && caption.Text == ((Record)source.Current!).Name,
            "retained-bound-presentation");
        Root.Invalidate();
        output("BINDING_NAVIGATOR:RECREATED:PASS");
    }

    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException($"BindingNavigator: {category}; position={source.Position}; editor={Navigator.PositionTextBox.Text}; caption={caption.Text}");
        assertions++;
        output("BINDING_NAVIGATOR:CHECK:" + category);
    }

    public void Dispose()
    {
        owner.Remove(Root);
        Root.Dispose();
        source.Dispose();
    }

    private sealed class PaintRoot : Panel
    {
        internal int PaintCount;
        protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); PaintCount++; }
    }

    private sealed class Record : IEditableObject, INotifyPropertyChanged
    {
        private string name = "", original = "";
        private bool editing;
        public Record() { }
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name
        {
            get => name;
            set { if (name == value) return; name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
        }
        public void BeginEdit() { if (editing) return; editing = true; original = Name; }
        public void EndEdit() => editing = false;
        public void CancelEdit() { if (!editing) return; editing = false; Name = original; }
    }
}

using System;
using System.ComponentModel;
using System.Linq;
using ModernFormsNext;
using ModernFormsNext.DataBinding;

namespace ControlGallery.Panels;

/// <summary>Demonstrates source currency, local editing and application-owned navigator actions.</summary>
public sealed class BindingNavigatorPanel : BasePanel
{
    private readonly BindingSource source = new();
    private readonly BindingNavigator navigator;
    private readonly Label status;
    private readonly DelegateCommand save;
    private BindingList<Person> people = Seed();
    private int saves;
    private bool readOnly, retiring;

    /// <summary>Creates an in-memory example without networking or persistence.</summary>
    public BindingNavigatorPanel()
    {
        BindingContext = new BindingContext();
        source.DataSource = people;
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 260 });
        tools.Controls.Add(new Label
        {
            Bounds = new(12, 8, 900, 28),
            Text = "Navigate, type a one-based position, edit the name, and Save / Cancel the local edit."
        });
        navigator = tools.Controls.Add(new BindingNavigator(source)
        {
            Dock = DockStyle.None, Bounds = new(12, 44, 940, 90), WrapContents = true,
            CountItemFormat = "Records: {0}"
        });
        var editor = tools.Controls.Add(new TextBox { Bounds = new(12, 150, 300, 32), AccessibleName = "Current name" });
        editor.DataBindings.Add(nameof(TextBox.Text), source, nameof(Person.Name), true, DataSourceUpdateMode.OnPropertyChanged);
        var mode = tools.Controls.Add(new Button { Bounds = new(328, 150, 160, 32), Text = "Editable / read-only" });
        var empty = tools.Controls.Add(new Button { Bounds = new(500, 150, 120, 32), Text = "Empty list" });
        var reset = tools.Controls.Add(new Button { Bounds = new(632, 150, 120, 32), Text = "Reset list" });
        var rtl = tools.Controls.Add(new Button { Bounds = new(764, 150, 120, 32), Text = "RTL / LTR" });
        tools.Controls.Add(new Label
        {
            Bounds = new(12, 198, 920, 52), Multiline = true,
            Text = "Save uses an application command: EndEdit plus an in-memory counter. No data is written.\nCancel uses the source item's ordinary IEditableObject rollback."
        });
        status = Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 100, Multiline = true });
        save = new DelegateCommand(() => { source.EndEdit(); saves++; UpdateStatus(); }, () => source.Count > 0 && source.AllowEdit);
        navigator.SaveButton.Command = save;
        mode.Click += (_, _) =>
        {
            readOnly = !readOnly;
            source.DataSource = readOnly ? Array.AsReadOnly(people.ToArray()) : people;
            editor.ReadOnly = readOnly;
            UpdateStatus();
        };
        empty.Click += (_, _) => { people = new(); ResetSource(); };
        reset.Click += (_, _) => { people = Seed(); ResetSource(); };
        rtl.Click += (_, _) => navigator.RightToLeft = navigator.RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;
        source.PositionChanged += (_, _) => UpdateStatus();
        source.ListChanged += (_, _) => UpdateStatus();
        UpdateStatus();

        void ResetSource() { readOnly = false; editor.ReadOnly = false; source.DataSource = people; UpdateStatus(); }
    }

    private void UpdateStatus()
    {
        if (retiring) return;
        save.RaiseCanExecuteChanged();
        status.Text = $"Position {source.Position + 1} / {source.Count}; {(readOnly ? "read-only" : "editable")}\nApplication save calls: {saves}. This example remains in memory.";
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !retiring)
        {
            retiring = true;
            navigator.BindingSource = null;
            source.Dispose();
        }
        base.Dispose(disposing);
    }

    private static BindingList<Person> Seed() => new() { new() { Name = "Ada" }, new() { Name = "Grace" }, new() { Name = "مرحبا · שלום" } };

    private sealed class Person : INotifyPropertyChanged, IEditableObject
    {
        public Person() { }
        private string name = "", before = "";
        private bool editing;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name
        {
            get => name;
            set { if (name == value) return; name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
        }
        public void BeginEdit() { if (editing) return; editing = true; before = Name; }
        public void EndEdit() => editing = false;
        public void CancelEdit() { if (!editing) return; editing = false; Name = before; }
    }
}

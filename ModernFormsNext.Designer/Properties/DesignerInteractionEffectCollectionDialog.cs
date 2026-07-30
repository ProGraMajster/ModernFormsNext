using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerInteractionEffectCollectionDialog : Form
{
    private readonly DesignerSession session;
    private readonly DesignControlNode control;
    private readonly List<DesignerInteractionEffectEntry> entries;
    private readonly ListBox effectList;
    private readonly TextBox propertyEditor;
    private readonly Label errorLabel;
    private int loadedIndex = -1;
    private bool changingSelection;

    public DesignerInteractionEffectCollectionDialog(DesignerSession session, DesignControlNode control)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.control = control ?? throw new ArgumentNullException(nameof(control));

        control.Properties.TryGetValue(InteractionEffectDesignValue.PropertyName, out DesignPropertyValue? stored);
        if (!InteractionEffectDesignerRegistry.TryReadCollection(stored, out entries, out string? error))
            throw new InvalidOperationException(error);

        Text = "Interaction Effects Collection Editor";
        Name = "DesignerInteractionEffectCollectionDialog";
        Size = new System.Drawing.Size(760, 520);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Left = 18, Top = 16, Width = 240, Height = 22, Text = "Members (runtime order)" });
        effectList = Controls.Add(new ListBox { Left = 18, Top = 42, Width = 250, Height = 330 });

        var addRipple = Controls.Add(new Button { Left = 18, Top = 382, Width = 116, Height = 30, Text = "Add Ripple" });
        var addPress = Controls.Add(new Button { Left = 142, Top = 382, Width = 126, Height = 30, Text = "Add PressScale" });
        var remove = Controls.Add(new Button { Left = 18, Top = 418, Width = 78, Height = 30, Text = "Remove" });
        var up = Controls.Add(new Button { Left = 104, Top = 418, Width = 48, Height = 30, Text = "Up" });
        var down = Controls.Add(new Button { Left = 160, Top = 418, Width = 56, Height = 30, Text = "Down" });

        Controls.Add(new Label { Left = 292, Top = 16, Width = 420, Height = 22, Text = "Selected effect properties (Property=Value)" });
        propertyEditor = Controls.Add(new TextBox
        {
            Left = 292,
            Top = 42,
            Width = 430,
            Height = 300,
            MultiLine = true
        });
        Controls.Add(new Label
        {
            Left = 292,
            Top = 350,
            Width = 430,
            Height = 42,
            Text = "Enums use member names. Colors use #AARRGGBB. Durations are milliseconds."
        });
        errorLabel = Controls.Add(new Label { Left = 292, Top = 396, Width = 430, Height = 44, Text = string.Empty });

        var apply = Controls.Add(new Button { Left = 450, Top = 446, Width = 80, Height = 30, Text = "Apply" });
        var ok = Controls.Add(new Button { Left = 546, Top = 446, Width = 80, Height = 30, Text = "OK" });
        var cancel = Controls.Add(new Button { Left = 642, Top = 446, Width = 80, Height = 30, Text = "Cancel" });

        effectList.SelectedIndexChanged += (_, _) => ChangeSelection();
        addRipple.Click += (_, _) => Add(InteractionEffectDesignerRegistry.RippleTypeName);
        addPress.Click += (_, _) => Add(InteractionEffectDesignerRegistry.PressScaleTypeName);
        remove.Click += (_, _) => RemoveSelected();
        up.Click += (_, _) => MoveSelected(-1);
        down.Click += (_, _) => MoveSelected(1);
        apply.Click += (_, _) => TryApplyLoaded();
        ok.Click += (_, _) => Commit();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        RefreshList(selectIndex: entries.Count > 0 ? 0 : -1);
    }

    private void Add(string typeName)
    {
        if (!TryApplyLoaded())
            return;
        entries.Add(InteractionEffectDesignerRegistry.Create(typeName));
        RefreshList(entries.Count - 1);
    }

    private void RemoveSelected()
    {
        int index = effectList.SelectedIndex;
        if (index < 0 || index >= entries.Count)
            return;
        entries.RemoveAt(index);
        loadedIndex = -1;
        RefreshList(Math.Min(index, entries.Count - 1));
    }

    private void MoveSelected(int delta)
    {
        if (!TryApplyLoaded())
            return;
        int oldIndex = effectList.SelectedIndex;
        int newIndex = oldIndex + delta;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= entries.Count)
            return;
        DesignerInteractionEffectEntry entry = entries[oldIndex];
        entries.RemoveAt(oldIndex);
        entries.Insert(newIndex, entry);
        loadedIndex = -1;
        RefreshList(newIndex);
    }

    private void ChangeSelection()
    {
        if (changingSelection)
            return;
        int selected = effectList.SelectedIndex;
        if (selected == loadedIndex)
            return;
        if (!TryApplyLoaded())
        {
            changingSelection = true;
            effectList.SelectedIndex = loadedIndex;
            changingSelection = false;
            return;
        }
        loadedIndex = selected;
        propertyEditor.Text = selected >= 0 && selected < entries.Count
            ? InteractionEffectDesignerRegistry.FormatEditorText(entries[selected])
            : string.Empty;
        errorLabel.Text = string.Empty;
    }

    private bool TryApplyLoaded()
    {
        if (loadedIndex < 0 || loadedIndex >= entries.Count)
            return true;
        if (InteractionEffectDesignerRegistry.TryApplyEditorText(
            entries[loadedIndex],
            propertyEditor.Text,
            out string? error))
        {
            errorLabel.Text = string.Empty;
            return true;
        }
        ShowError(error ?? "The selected effect properties are invalid.");
        return false;
    }

    private void Commit()
    {
        if (!TryApplyLoaded())
            return;

        if (entries.Count == 0)
            control.Properties.Remove(InteractionEffectDesignValue.PropertyName);
        else
            control.Properties[InteractionEffectDesignValue.PropertyName] =
                InteractionEffectDesignerRegistry.WriteCollection(entries);
        DialogResult = DialogResult.OK;
    }

    private void RefreshList(int selectIndex)
    {
        changingSelection = true;
        effectList.Items.Clear();
        foreach (DesignerInteractionEffectEntry entry in entries)
            effectList.Items.Add(entry);
        effectList.SelectedIndex = entries.Count == 0 ? -1 : Math.Clamp(selectIndex, 0, entries.Count - 1);
        changingSelection = false;
        loadedIndex = -1;
        ChangeSelection();
    }

    private void ShowError(string message)
    {
        errorLabel.Text = message;
        session.Log($"Interaction effect editor: {message}");
    }
}

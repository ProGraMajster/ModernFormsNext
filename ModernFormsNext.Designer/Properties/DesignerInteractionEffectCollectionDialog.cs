using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerInteractionEffectCollectionDialog : Form
{
    private readonly DesignerSession session;
    private readonly IDictionary<string, DesignPropertyValue> properties;
    private readonly DesignerInteractionEffectCollectionEditorModel model;
    private readonly ComboBox effectType;
    private readonly ListBox effectList;
    private readonly DesignerPropertyGrid propertyGrid;
    private readonly Label unavailableLabel;
    private readonly Label errorLabel;
    private readonly Button addButton;
    private readonly Button removeButton;
    private readonly Button upButton;
    private readonly Button downButton;
    private readonly Button applyButton;
    private readonly Button okButton;
    private DesignerInteractionEffectEntry? selectedEntry;
    private bool changingSelection;

    public DesignerInteractionEffectCollectionDialog(
        DesignerSession session,
        IDictionary<string, DesignPropertyValue> properties)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.properties = properties ?? throw new ArgumentNullException(nameof(properties));

        IReadOnlyList<DesignAnimationDefinitionDescriptor> definitions = BuiltInAnimationDefinitionCatalog.Definitions
            .Concat(session.AnimationDefinitions)
            .Where(item => item.Kind == DesignAnimationDefinitionKind.InteractionEffect)
            .GroupBy(item => item.TypeName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        properties.TryGetValue(InteractionEffectDesignValue.PropertyName, out DesignPropertyValue? stored);
        model = new DesignerInteractionEffectCollectionEditorModel(stored, definitions);

        Text = "Interaction Effects Collection Editor";
        Name = "DesignerInteractionEffectCollectionDialog";
        Size = new System.Drawing.Size(900, 580);
        MinimumSize = new System.Drawing.Size(760, 500);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label
        {
            Left = 18,
            Top = 16,
            Width = 260,
            Height = 22,
            Text = "Effects (runtime order)"
        });
        effectList = Controls.Add(new ListBox
        {
            Left = 18,
            Top = 42,
            Width = 260,
            Height = 386,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        });

        effectType = Controls.Add(new ComboBox
        {
            Left = 18,
            Top = 440,
            Width = 164,
            Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });
        effectType.Items.AddRange(model.Definitions.Select(item => (object)item.DisplayName).ToArray());
        effectType.SelectedIndex = model.Definitions.Count > 0 ? 0 : -1;
        addButton = Controls.Add(new Button
        {
            Left = 190,
            Top = 440,
            Width = 88,
            Height = 30,
            Text = "Add Effect",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });
        removeButton = Controls.Add(new Button
        {
            Left = 18,
            Top = 478,
            Width = 80,
            Height = 30,
            Text = "Remove",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });
        upButton = Controls.Add(new Button
        {
            Left = 106,
            Top = 478,
            Width = 76,
            Height = 30,
            Text = "Move Up",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });
        downButton = Controls.Add(new Button
        {
            Left = 190,
            Top = 478,
            Width = 88,
            Height = 30,
            Text = "Move Down",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });

        propertyGrid = Controls.Add(new DesignerPropertyGrid(
            session,
            () => selectedEntry?.ToString() ?? "No effect selected",
            () => selectedEntry?.TypeName ?? string.Empty,
            () => DesignerInteractionEffectPropertyDescriptors.Create(selectedEntry),
            "Selected Effect Properties")
        {
            Left = 300,
            Top = 16,
            Width = 566,
            Height = 430,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        });
        unavailableLabel = Controls.Add(new Label
        {
            Left = 318,
            Top = 92,
            Width = 520,
            Height = 64,
            Text = string.Empty,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        });
        unavailableLabel.BringToFront();
        errorLabel = Controls.Add(new Label
        {
            Left = 300,
            Top = 454,
            Width = 566,
            Height = 38,
            Text = string.Empty,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        });

        applyButton = Controls.Add(new Button
        {
            Left = 594,
            Top = 500,
            Width = 80,
            Height = 30,
            Text = "Apply",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        okButton = Controls.Add(new Button
        {
            Left = 690,
            Top = 500,
            Width = 80,
            Height = 30,
            Text = "OK",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        var cancelButton = Controls.Add(new Button
        {
            Left = 786,
            Top = 500,
            Width = 80,
            Height = 30,
            Text = "Cancel",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });

        effectList.SelectedIndexChanged += (_, _) => ChangeSelection();
        addButton.Click += (_, _) => AddSelectedType();
        removeButton.Click += (_, _) => RemoveSelected();
        upButton.Click += (_, _) => MoveSelected(-1);
        downButton.Click += (_, _) => MoveSelected(1);
        applyButton.Click += (_, _) => Apply();
        okButton.Click += (_, _) => Commit();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        RefreshList(model.Entries.Count > 0 ? 0 : -1);
        if (model.LoadError is not null)
            DisableForLoadError(model.LoadError);
    }

    private void AddSelectedType()
    {
        int definitionIndex = effectType.SelectedIndex;
        if (definitionIndex < 0 || definitionIndex >= model.Definitions.Count)
            return;
        model.Add(model.Definitions[definitionIndex].TypeName);
        RefreshList(model.Entries.Count - 1);
    }

    private void RemoveSelected()
    {
        int index = effectList.SelectedIndex;
        if (!model.RemoveAt(index))
            return;
        RefreshList(Math.Min(index, model.Entries.Count - 1));
    }

    private void MoveSelected(int delta)
    {
        int oldIndex = effectList.SelectedIndex;
        if (!model.Move(oldIndex, delta))
            return;
        RefreshList(oldIndex + delta);
    }

    private void ChangeSelection()
    {
        if (changingSelection)
            return;

        int index = effectList.SelectedIndex;
        selectedEntry = index >= 0 && index < model.Entries.Count ? model.Entries[index] : null;
        bool unavailable = selectedEntry is { IsSupported: false };
        unavailableLabel.Visible = unavailable;
        unavailableLabel.Text = unavailable
            ? $"{selectedEntry} cannot be inspected because its detached source descriptor is unavailable. "
                + "The serialized definition will be preserved unless you remove it."
            : string.Empty;
        propertyGrid.Enabled = selectedEntry is { IsSupported: true };
        propertyGrid.RefreshProperties();
        UpdateButtonState();
    }

    private void Apply()
    {
        model.Apply(properties);
        errorLabel.Text = "Changes applied inside the active editor transaction.";
    }

    private void Commit()
    {
        model.Apply(properties);
        DialogResult = DialogResult.OK;
    }

    private void RefreshList(int selectIndex)
    {
        changingSelection = true;
        effectList.Items.Clear();
        foreach (DesignerInteractionEffectEntry entry in model.Entries)
            effectList.Items.Add(entry);
        effectList.SelectedIndex = model.Entries.Count == 0
            ? -1
            : Math.Clamp(selectIndex, 0, model.Entries.Count - 1);
        changingSelection = false;
        ChangeSelection();
    }

    private void UpdateButtonState()
    {
        int index = effectList.SelectedIndex;
        bool selected = index >= 0 && index < model.Entries.Count;
        removeButton.Enabled = selected;
        upButton.Enabled = selected && index > 0;
        downButton.Enabled = selected && index < model.Entries.Count - 1;
        addButton.Enabled = effectType.SelectedIndex >= 0;
    }

    private void DisableForLoadError(string error)
    {
        errorLabel.Text = error;
        effectList.Enabled = false;
        effectType.Enabled = false;
        propertyGrid.Enabled = false;
        addButton.Enabled = false;
        removeButton.Enabled = false;
        upButton.Enabled = false;
        downButton.Enabled = false;
        applyButton.Enabled = false;
        okButton.Enabled = false;
        session.Log($"Interaction effect editor: {error}");
    }
}

using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerTransitionDialog : Form
{
    private readonly DesignerSession session;
    private readonly IDictionary<string, DesignPropertyValue> properties;
    private readonly string propertyName;
    private readonly bool isLayout;
    private readonly TextBox editor;
    private readonly Label errorLabel;

    public DesignerTransitionDialog(
        DesignerSession session,
        IDictionary<string, DesignPropertyValue> properties,
        bool isLayout)
    {
        this.session = session;
        this.properties = properties;
        this.isLayout = isLayout;
        propertyName = isLayout ? LayoutTransitionDesignValue.PropertyName : VisualStateTransitionDesignValue.PropertyName;
        properties.TryGetValue(propertyName, out var stored);
        string? loadError = null;
        if (stored is not null)
        {
            bool valid = isLayout
                ? LayoutTransitionDesignValue.TryRead(stored, out _, out _, out _, out loadError)
                : VisualStateTransitionDesignValue.TryRead(stored, out _, out loadError);
            if (valid)
                loadError = null;
        }

        Text = isLayout ? "Layout Transition Editor" : "Visual State Transitions Editor";
        Name = "DesignerTransitionDialog";
        Size = new System.Drawing.Size(660, 430);
        StartPosition = FormStartPosition.CenterParent;
        Controls.Add(new Label
        {
            Left = 18,
            Top = 16,
            Width = 610,
            Height = 42,
            Text = isLayout
                ? "Edit Enabled, DurationMilliseconds and a built-in Easing identifier."
                : "One ordered transition per line: Normal->Hover; DurationMilliseconds=150; Easing=CubicOut"
        });
        editor = Controls.Add(new TextBox
        {
            Left = 18,
            Top = 66,
            Width = 610,
            Height = 240,
            MultiLine = true,
            Text = isLayout
                ? DesignerTransitionEditorModel.FormatLayout(stored)
                : DesignerTransitionEditorModel.FormatVisualStates(stored)
        });
        errorLabel = Controls.Add(new Label { Left = 18, Top = 314, Width = 610, Height = 34 });
        var reset = Controls.Add(new Button { Left = 352, Top = 350, Width = 84, Height = 30, Text = "Reset" });
        var ok = Controls.Add(new Button { Left = 448, Top = 350, Width = 84, Height = 30, Text = "OK" });
        var cancel = Controls.Add(new Button { Left = 544, Top = 350, Width = 84, Height = 30, Text = "Cancel" });
        reset.Click += (_, _) => Reset();
        ok.Click += (_, _) => Commit();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        if (loadError is not null)
        {
            editor.Enabled = false;
            ok.Enabled = false;
            errorLabel.Text = loadError + " Use Reset to remove the invalid optional value.";
            session.Log($"Transition editor: {loadError}");
        }
    }

    private void Commit()
    {
        bool success = isLayout
            ? DesignerTransitionEditorModel.TryParseLayout(editor.Text, out var value, out string? error)
            : DesignerTransitionEditorModel.TryParseVisualStates(editor.Text, out value, out error);
        if (!success)
        {
            errorLabel.Text = error ?? "The transition definition is invalid.";
            session.Log($"Transition editor: {error}");
            return;
        }

        if (!isLayout
            && VisualStateTransitionDesignValue.TryRead(value, out var transitions, out _)
            && transitions.Count == 0)
        {
            properties.Remove(propertyName);
        }
        else
        {
            properties[propertyName] = value;
        }
        DialogResult = DialogResult.OK;
    }

    private void Reset()
    {
        properties.Remove(propertyName);
        DialogResult = DialogResult.OK;
    }
}

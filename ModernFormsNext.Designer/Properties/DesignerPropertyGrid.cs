using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyGrid : DesignerPanelBase
{
    private readonly DesignerPropertyGridState state;
    private readonly DesignerPropertyGridRenderer renderer;
    private readonly DesignerPropertyGridController controller;
    private readonly DesignerFileService? fileService;
    private TextBox? textEditor;
    private ComboBox? comboEditor;
    private bool committingEdit;
    private bool preserveInitialTextSelection;
    private bool replaceInitialTextSelectionOnInput;
    private int scrollOffset;

    public DesignerPropertyGrid(DesignerSession playgroundState, string title = "Properties")
        : this(playgroundState, fileService: null, title)
    {
    }

    public DesignerPropertyGrid(
        DesignerSession playgroundState,
        DesignerFileService? fileService,
        string title = "Properties")
        : base(title)
    {
        this.fileService = fileService;
        state = new DesignerPropertyGridState(playgroundState);
        renderer = new DesignerPropertyGridRenderer();
        controller = new DesignerPropertyGridController(state, renderer);
        TabStop = true;

        state.Changed += (_, _) =>
        {
            if (!committingEdit && !state.IsEditing)
                RemoveEditors();

            ClampScrollOffset();
            Invalidate();
        };

        SizeChanged += (_, _) =>
        {
            CancelEdit();
            ClampScrollOffset();
        };
    }

    internal bool IsEditingValue => textEditor is not null || comboEditor is not null || state.IsEditing;

    public void BeginEdit(DesignerPropertyGridRow row, Rectangle bounds)
    {
        CancelEdit();
        state.SelectRow(row);
        state.BeginEditing();

        if (TryBeginListComboEdit(row, bounds))
            return;

        textEditor = new TextBox
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Text = GetRowValueText(row)
        };
        textEditor.Style.Border.Width = 1;
        textEditor.Style.Border.Color = DesignerColors.Accent;
        textEditor.Style.BackgroundColor = SKColors.White;
        textEditor.Style.ForegroundColor = SKColors.Black;
        textEditor.KeyDown += TextEditor_KeyDown;
        textEditor.KeyPress += TextEditor_KeyPress;
        textEditor.KeyUp += TextEditor_KeyUp;
        textEditor.MouseUp += TextEditor_MouseUp;
        textEditor.LostFocus += TextEditor_LostFocus;
        textEditor.TextChanged += TextEditor_TextChanged;

        Controls.Add(textEditor);
        textEditor.BringToFront();
        textEditor.Select();
        textEditor.SelectAll();
        preserveInitialTextSelection = true;
        replaceInitialTextSelectionOnInput = true;
        UpdateTextEditorSnapshot();
    }

    public async void OpenDialogEditor(DesignerPropertyGridRow row)
    {
        CancelEdit();
        state.SelectRow(row);

        if (row.Property?.DialogEditor is null)
        {
            state.Session.Log($"Property '{row.Property?.DisplayName ?? "unknown"}' does not have a dialog editor.");
            return;
        }

        var owner = FindForm();

        if (owner is null)
        {
            state.Session.Log($"Cannot edit {row.Property.DisplayName}: no owner form was found for the dialog.");
            return;
        }

        try
        {
            var changed = await row.Property.DialogEditor(new DesignerPropertyDialogContext(owner, state.Session, row.Property));

            if (!changed)
                return;

            state.Session.NotifyDocumentChanged();
            state.Session.Log($"Updated {state.HeaderName}.{row.Property.DisplayName}.");
            state.Refresh();
            Invalidate();
        }
        catch (Exception ex)
        {
            state.Session.Log($"Dialog editor for {row.Property.DisplayName} failed: {ex.Message}");
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Select();

        if (!EndEdit())
            return;

        var logicalPoint = DesignerDpiCoordinateConverter.DeviceToLogicalPoint(e.X, e.Y, Scaling);

        if (controller.HandleMouseDown(this, logicalPoint.X, logicalPoint.Y, scrollOffset))
            Invalidate();
    }

    protected override void OnDoubleClick(MouseEventArgs e)
    {
        base.OnDoubleClick(e);

        var logicalPoint = DesignerDpiCoordinateConverter.DeviceToLogicalPoint(e.X, e.Y, Scaling);

        if (controller.TryCreateDefaultEventHandler(logicalPoint.X, logicalPoint.Y, Width, scrollOffset, out var eventDescriptor, out var handlerName))
        {
            EnsureEventHandlerMethod(eventDescriptor, handlerName);
            Invalidate();
            return;
        }

        if (controller.TryBeginEdit(this, logicalPoint.X, logicalPoint.Y, scrollOffset))
            Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Enter)
        {
            if (state.IsEditing)
                EndEdit();
            else
                controller.TryBeginEditSelected(this, scrollOffset);

            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && state.IsEditing)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        var nextOffset = controller.HandleMouseWheel(e, Height, scrollOffset);

        if (nextOffset == scrollOffset)
            return;

        scrollOffset = nextOffset;
        CancelEdit();
        Invalidate();
    }

    protected override void OnPaintContent(PaintEventArgs e)
    {
        renderer.Render(e, state, Width, Height, scrollOffset);
    }

    private bool EndEdit()
    {
        if (textEditor is null && comboEditor is null)
            return true;

        committingEdit = true;
        var committed = false;

        try
        {
            if (textEditor is not null)
            {
                var editingEvent = state.EditingEvent;
                state.UpdateEditingText(textEditor.Text);
                committed = state.CommitEditing();

                if (committed && editingEvent is not null && !string.IsNullOrWhiteSpace(textEditor.Text))
                    EnsureEventHandlerMethod(editingEvent, textEditor.Text.Trim());
            }
            else if (comboEditor is not null)
            {
                committed = CommitComboEdit();
            }
        }
        finally
        {
            committingEdit = false;

            if (committed)
                RemoveEditors();
            else
                FocusActiveEditor();

            Invalidate();
        }

        return committed;
    }

    private void CancelEdit()
    {
        state.CancelEditing();
        RemoveEditors();
        Invalidate();
    }

    private bool TryBeginListComboEdit(DesignerPropertyGridRow row, Rectangle bounds)
    {
        if (row.Property is not { } property || !UsesComboEditor(property))
            return false;

        var values = property.StandardValues ?? Enum.GetNames(Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType);
        comboEditor = new ComboBox
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = Math.Max(bounds.Height, 24)
        };

        comboEditor.Style.Border.Width = 1;
        comboEditor.Style.Border.Color = DesignerColors.Accent;
        comboEditor.Items.AddRange(values.Cast<object>().ToArray());

        var valueText = GetRowValueText(row);
        var selectedIndex = values
            .Select((value, index) => new { value, index })
            .FirstOrDefault(item => string.Equals(item.value, valueText, StringComparison.Ordinal))?.index ?? -1;

        if (selectedIndex >= 0)
            comboEditor.SelectedIndex = selectedIndex;

        comboEditor.SelectedIndexChanged += ComboEditor_SelectedIndexChanged;
        comboEditor.LostFocus += ComboEditor_LostFocus;
        comboEditor.KeyDown += ComboEditor_KeyDown;

        Controls.Add(comboEditor);
        comboEditor.BringToFront();
        comboEditor.Select();
        return true;
    }

    private void RemoveEditors()
    {
        if (textEditor is not null)
        {
            textEditor.KeyDown -= TextEditor_KeyDown;
            textEditor.KeyPress -= TextEditor_KeyPress;
            textEditor.KeyUp -= TextEditor_KeyUp;
            textEditor.MouseUp -= TextEditor_MouseUp;
            textEditor.LostFocus -= TextEditor_LostFocus;
            textEditor.TextChanged -= TextEditor_TextChanged;
            Controls.Remove(textEditor);
            textEditor.Dispose();
            textEditor = null;
            preserveInitialTextSelection = false;
            replaceInitialTextSelectionOnInput = false;
        }

        if (comboEditor is not null)
        {
            comboEditor.SelectedIndexChanged -= ComboEditor_SelectedIndexChanged;
            comboEditor.LostFocus -= ComboEditor_LostFocus;
            comboEditor.KeyDown -= ComboEditor_KeyDown;
            Controls.Remove(comboEditor);
            comboEditor.Dispose();
            comboEditor = null;
        }
    }

    private void ClampScrollOffset()
    {
        var contentHeight = renderer.GetContentHeight(state);
        var viewportHeight = renderer.GetGridViewportHeight(Height);
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, contentHeight - viewportHeight));
    }

    private void FocusActiveEditor()
    {
        if (textEditor is not null)
        {
            textEditor.Select();
            textEditor.SelectAll();
        }
        else
        {
            comboEditor?.Select();
        }
    }

    private void TextEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            EndEdit();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
        else if (replaceInitialTextSelectionOnInput && e.KeyCode is Keys.Back or Keys.Delete)
        {
            textEditor?.SelectAll();
            replaceInitialTextSelectionOnInput = false;
            preserveInitialTextSelection = false;
        }
    }

    private void TextEditor_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!replaceInitialTextSelectionOnInput || textEditor is null || e.Control || e.Alt)
            return;

        if (e.KeyChar >= 32 && e.KeyChar != 127)
        {
            textEditor.SelectAll();
            replaceInitialTextSelectionOnInput = false;
            preserveInitialTextSelection = false;
        }
    }

    private void TextEditor_KeyUp(object? sender, KeyEventArgs e)
    {
        UpdateTextEditorSnapshot();
    }

    private void TextEditor_MouseUp(object? sender, MouseEventArgs e)
    {
        if (preserveInitialTextSelection && textEditor is not null)
        {
            textEditor.SelectAll();
            preserveInitialTextSelection = false;
        }

        UpdateTextEditorSnapshot();
    }

    private void TextEditor_LostFocus(object? sender, EventArgs e)
    {
        EndEditOrCancelOnFocusLoss();
    }

    private void TextEditor_TextChanged(object? sender, EventArgs e)
    {
        if (textEditor is null)
            return;

        state.UpdateEditingText(textEditor.Text);
        UpdateTextEditorSnapshot();
        textEditor.Invalidate();
        Invalidate();
    }

    private void ComboEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            EndEdit();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void ComboEditor_SelectedIndexChanged(object? sender, EventArgs e)
    {
        EndEdit();
    }

    private void ComboEditor_LostFocus(object? sender, EventArgs e)
    {
        EndEditOrCancelOnFocusLoss();
    }

    private void EndEditOrCancelOnFocusLoss()
    {
        if (textEditor is null && comboEditor is null)
            return;

        committingEdit = true;
        var editingEvent = state.EditingEvent;
        var committedText = textEditor?.Text ?? GetComboValueText();

        try
        {
            if (textEditor is not null)
                state.UpdateEditingText(textEditor.Text);
            else if (comboEditor is not null)
                state.UpdateEditingText(GetComboValueText());

            if (!state.CommitEditing())
                state.CancelEditing();
            else if (editingEvent is not null && !string.IsNullOrWhiteSpace(committedText))
                EnsureEventHandlerMethod(editingEvent, committedText.Trim());
        }
        finally
        {
            committingEdit = false;
            RemoveEditors();
            Invalidate();
        }
    }

    private bool CommitComboEdit()
    {
        state.UpdateEditingText(GetComboValueText());
        return state.CommitEditing();
    }

    private string GetComboValueText()
    {
        if (comboEditor is null)
            return string.Empty;

        return comboEditor.SelectedItem?.ToString()
            ?? (comboEditor.SelectedIndex >= 0 ? comboEditor.Items[comboEditor.SelectedIndex]?.ToString() : null)
            ?? string.Empty;
    }

    private static string GetRowValueText(DesignerPropertyGridRow row)
        => row.Property is not null ? row.Property.GetValueText() : row.Event?.GetValueText() ?? string.Empty;

    private void UpdateTextEditorSnapshot()
    {
        if (textEditor is null)
            return;

        var selectionStart = textEditor.SelectionStart;
        var selectionEnd = textEditor.SelectionEnd;
        var caret = selectionEnd >= 0
            ? selectionEnd
            : textEditor.Text.Length;

        state.UpdateEditingSelection(selectionStart, selectionEnd, caret);
    }

    private static bool UsesComboEditor(DesignerPropertyDescriptor property)
    {
        var type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;
        return type == typeof(bool) || type.IsEnum;
    }

    private void EnsureEventHandlerMethod(DesignerEventDescriptor? eventDescriptor, string handlerName)
    {
        if (fileService is null || eventDescriptor is null || string.IsNullOrWhiteSpace(handlerName))
            return;

        var result = fileService.EnsureEventHandlerMethod(state.Session.Document, handlerName.Trim(), eventDescriptor.HandlerType);
        state.Session.Log(result.Message);
    }
}

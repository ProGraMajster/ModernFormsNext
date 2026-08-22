using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.History;
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
    private CheckBox? booleanEditor;
    private NumericUpDown? numericEditor;
    private bool committingEdit;
    private bool preserveInitialTextSelection;
    private bool replaceInitialTextSelectionOnInput;
    private int scrollOffset;

    public DesignerPropertyGrid(DesignerSession playgroundState, string title = "Properties")
        : this(new DesignerPropertyGridState(playgroundState), fileService: null, title)
    {
    }

    public DesignerPropertyGrid(
        DesignerSession playgroundState,
        DesignerFileService? fileService,
        string title = "Properties")
        : this(new DesignerPropertyGridState(playgroundState), fileService, title)
    {
    }

    internal DesignerPropertyGrid(
        DesignerSession playgroundState,
        Func<string> headerName,
        Func<string> headerType,
        Func<IReadOnlyList<DesignerPropertyDescriptor>> propertyProvider,
        string title = "Properties")
        : this(
            new DesignerPropertyGridState(playgroundState, headerName, headerType, propertyProvider),
            fileService: null,
            title)
    {
    }

    private DesignerPropertyGrid(
        DesignerPropertyGridState state,
        DesignerFileService? fileService,
        string title)
        : base(title)
    {
        this.fileService = fileService;
        this.state = state;
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

    internal bool IsEditingValue
        => textEditor is not null
        || comboEditor is not null
        || booleanEditor is not null
        || numericEditor is not null
        || state.IsEditing;

    internal void RefreshProperties()
    {
        CancelEdit();
        state.Refresh();
    }

    public void BeginEdit(DesignerPropertyGridRow row, Rectangle bounds)
    {
        CancelEdit();
        state.SelectRow(row);
        state.BeginEditing();

        if (TryBeginBooleanEdit(row, bounds))
            return;

        if (TryBeginNumericEdit(row, bounds))
            return;

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

        if (!state.SupportsEvents)
        {
            try
            {
                if (await row.Property.DialogEditor(new DesignerPropertyDialogContext(owner, state.Session, row.Property)))
                {
                    state.Session.Log($"Updated {state.HeaderName}.{row.Property.DisplayName}.");
                    state.Refresh();
                    Invalidate();
                }
            }
            catch (Exception ex)
            {
                state.Session.Log($"Dialog editor for {row.Property.DisplayName} failed: {ex.Message}");
            }

            return;
        }

        using var transaction = state.Session.Transactions.Begin($"Edit {row.Property.DisplayName}");
        var snapshot = DesignerModelMutationSnapshot.CaptureSelected(state.Session);

        try
        {
            var changed = await row.Property.DialogEditor(new DesignerPropertyDialogContext(owner, state.Session, row.Property));
            snapshot.RecordChanges(state.Session.Transactions);

            if (!changed)
            {
                transaction.Rollback();
                return;
            }

            transaction.Commit();
            state.Session.Log($"Updated {state.HeaderName}.{row.Property.DisplayName}.");
            state.Refresh();
            Invalidate();
        }
        catch (Exception ex)
        {
            // A dialog failure before commit still needs its partial mutations recorded so scope
            // disposal can revert them. If only a post-commit observer failed, the transaction is
            // already complete and there is nothing left to record or roll back.
            if (state.Session.Transactions.HasActiveTransaction)
                snapshot.RecordChanges(state.Session.Transactions);
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
        if (textEditor is null && comboEditor is null && booleanEditor is null && numericEditor is null)
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
            else if (booleanEditor is not null)
            {
                state.UpdateEditingText(booleanEditor.Checked ? "True" : "False");
                committed = state.CommitEditing();
            }
            else if (numericEditor is not null)
            {
                state.UpdateEditingText(numericEditor.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                committed = state.CommitEditing();
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

    private bool TryBeginBooleanEdit(DesignerPropertyGridRow row, Rectangle bounds)
    {
        if (row.Property is not { UseBooleanCheckBox: true, ValueType: var valueType } property
            || (Nullable.GetUnderlyingType(valueType) ?? valueType) != typeof(bool))
        {
            return false;
        }

        booleanEditor = new CheckBox
        {
            Left = bounds.Left + 4,
            Top = bounds.Top,
            Width = Math.Max(1, bounds.Width - 8),
            Height = bounds.Height,
            Text = string.Empty,
            Checked = property.GetValue() is bool value && value
        };
        booleanEditor.CheckedChanged += BooleanEditor_CheckedChanged;
        booleanEditor.LostFocus += BooleanEditor_LostFocus;

        Controls.Add(booleanEditor);
        booleanEditor.BringToFront();
        booleanEditor.Select();
        return true;
    }

    private bool TryBeginNumericEdit(DesignerPropertyGridRow row, Rectangle bounds)
    {
        if (row.Property is not { NumericMinimum: { } minimum, NumericMaximum: { } maximum } property)
            return false;

        decimal current;
        try
        {
            double numericValue = Convert.ToDouble(property.GetValue(), System.Globalization.CultureInfo.InvariantCulture);
            current = numericValue <= (double)minimum ? minimum
                : numericValue >= (double)maximum ? maximum
                : (decimal)numericValue;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            current = minimum;
        }

        numericEditor = new NumericUpDown
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = Math.Max(bounds.Height, 24),
            AllowDecimalValues = property.NumericDecimalPlaces > 0,
            DecimalPlaces = property.NumericDecimalPlaces,
            Minimum = minimum,
            Maximum = maximum,
            Increment = property.NumericIncrement ?? (property.NumericDecimalPlaces > 0 ? 0.1m : 1m),
            Value = Math.Clamp(current, minimum, maximum)
        };
        numericEditor.ValueCommitted += NumericEditor_ValueCommitted;
        numericEditor.LostFocus += NumericEditor_LostFocus;

        Controls.Add(numericEditor);
        numericEditor.BringToFront();
        numericEditor.Select();
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

        if (booleanEditor is not null)
        {
            booleanEditor.CheckedChanged -= BooleanEditor_CheckedChanged;
            booleanEditor.LostFocus -= BooleanEditor_LostFocus;
            Controls.Remove(booleanEditor);
            booleanEditor.Dispose();
            booleanEditor = null;
        }

        if (numericEditor is not null)
        {
            numericEditor.ValueCommitted -= NumericEditor_ValueCommitted;
            numericEditor.LostFocus -= NumericEditor_LostFocus;
            Controls.Remove(numericEditor);
            numericEditor.Dispose();
            numericEditor = null;
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
            if (comboEditor is not null)
                comboEditor.Select();
            else if (booleanEditor is not null)
                booleanEditor.Select();
            else
                numericEditor?.Select();
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

    private void BooleanEditor_CheckedChanged(object? sender, EventArgs e)
        => EndEdit();

    private void BooleanEditor_LostFocus(object? sender, EventArgs e)
        => EndEditOrCancelOnFocusLoss();

    private void NumericEditor_ValueCommitted(object? sender, EventArgs e)
        => EndEdit();

    private void NumericEditor_LostFocus(object? sender, EventArgs e)
        => EndEditOrCancelOnFocusLoss();

    private void EndEditOrCancelOnFocusLoss()
    {
        if (textEditor is null && comboEditor is null && booleanEditor is null && numericEditor is null)
            return;

        committingEdit = true;
        var editingEvent = state.EditingEvent;
        var committedText = textEditor?.Text
            ?? (comboEditor is not null ? GetComboValueText() : null)
            ?? (booleanEditor is not null ? (booleanEditor.Checked ? "True" : "False") : null)
            ?? numericEditor?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? string.Empty;

        try
        {
            if (textEditor is not null)
                state.UpdateEditingText(textEditor.Text);
            else if (comboEditor is not null)
                state.UpdateEditingText(GetComboValueText());
            else
                state.UpdateEditingText(committedText);

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
        return property.StandardValues is not null || type.IsEnum;
    }

    private void EnsureEventHandlerMethod(DesignerEventDescriptor? eventDescriptor, string handlerName)
    {
        if (fileService is null || eventDescriptor is null || string.IsNullOrWhiteSpace(handlerName))
            return;

        var result = fileService.EnsureEventHandlerMethod(state.Session.Document, handlerName.Trim(), eventDescriptor.HandlerType);
        state.Session.Log(result.Message);
    }
}

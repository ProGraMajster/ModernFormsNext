using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using SkiaSharp;
using System.Reflection;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerSurfaceRenderer
{
    private readonly DesignerCoordinateMapper coordinateMapper = new();
    private readonly DesignerLayoutEngine layoutEngine = new();
    private readonly HashSet<string> loggedRuntimeRenderFailures = [];
    private readonly HashSet<string> loggedRuntimePropertyFailures = [];
    private readonly Dictionary<DesignControlNode, string> runtimeRenderDiagnostics = [];
    private readonly Dictionary<DesignControlNode, string> placeholderRenderDiagnostics = [];
    private string? lastFrameDiagnostics;

    public void Render(PaintEventArgs e, DesignerSession state, int width, int height)
    {
        LogFrameDiagnostics(state, width, height);

        var view = coordinateMapper.GetView(state, width, height);
        var formBounds = ToDevice(e, new System.Drawing.Rectangle(view.FormX, view.FormY, view.FormWidth, view.FormHeight));
        var clientBounds = ToDevice(e, new System.Drawing.Rectangle(view.ClientX, view.ClientY, view.ClientWidth, view.ClientHeight));

        e.Canvas.FillRectangle(new System.Drawing.Rectangle(0, 0, e.LogicalToDeviceUnits(width), e.LogicalToDeviceUnits(height)), DesignerColors.Workspace);
        var layout = layoutEngine.Layout(state.Document);

        DrawForm(e, state, formBounds, clientBounds, view);

        e.Canvas.Save();
        e.Canvas.ClipRect(clientBounds.ToSKRect());

        foreach (var node in state.Document.Controls)
            DrawNode(e, state, layout, node, view);

        e.Canvas.Restore();

        if (state.SelectedNode is null)
            DesignerSelectionAdorner.Draw(e, formBounds, showResizeHandle: false);
    }

    public bool TryMapToDocument(
        DesignerSession state,
        int width,
        int height,
        float x,
        float y,
        out DesignPoint point)
        => coordinateMapper.TryMapToDocument(state, width, height, x, y, out point);

    private static void DrawForm(
        PaintEventArgs e,
        DesignerSession state,
        System.Drawing.Rectangle formBounds,
        System.Drawing.Rectangle clientBounds,
        DesignerSurfaceView view)
    {
        e.Canvas.FillRectangle(formBounds, DesignerColors.FormBorder);
        e.Canvas.FillRectangle(
            new System.Drawing.Rectangle(formBounds.Left, formBounds.Top, formBounds.Width, e.LogicalToDeviceUnits(view.TitleHeight)),
            new SKColor(218, 232, 246));
        e.Canvas.FillRectangle(clientBounds, new SKColor(245, 245, 245));
        e.Canvas.DrawRectangle(formBounds, new SKColor(123, 183, 224), e.LogicalToDeviceUnits(1));
        DrawGrid(e, clientBounds);
        e.Canvas.DrawText(
            state.Document.FormName,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(formBounds.Left + e.LogicalToDeviceUnits(12), formBounds.Top, formBounds.Width - e.LogicalToDeviceUnits(24), e.LogicalToDeviceUnits(view.TitleHeight)),
            new SKColor(15, 32, 44),
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private static void DrawGrid(PaintEventArgs e, System.Drawing.Rectangle bounds)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(225, 225, 225),
            StrokeWidth = 1,
            IsAntialias = false
        };

        var step = e.LogicalToDeviceUnits(8);

        for (var x = bounds.Left + step; x < bounds.Right; x += step)
        {
            for (var y = bounds.Top + step; y < bounds.Bottom; y += step)
                e.Canvas.DrawPoint(x, y, paint);
        }
    }

    private void DrawNode(
        PaintEventArgs e,
        DesignerSession state,
        DesignerLayoutResult layout,
        DesignControlNode node,
        DesignerSurfaceView view)
    {
        if (!IsDesignNodeVisible(node))
            return;

        var absolute = layout.GetEffectiveBounds(node);
        var surfaceBounds = coordinateMapper.ToSurfaceBounds(absolute, view);
        var bounds = ToDevice(e, surfaceBounds);
        var selected = ReferenceEquals(state.SelectedNode, node);

        if (ShouldUseSpecialDesignerRendering(node))
        {
            DrawSpecialContainer(e, node, layout, view, bounds);
        }
        else if (state.ControlRenderMode == DesignerControlRenderMode.Runtime)
        {
            DrawRuntimeControl(e, state, node, surfaceBounds, bounds);
        }
        else
        {
            LogPlaceholderRenderDiagnostics(state, node, bounds);
            DrawPlaceholder(e, node, bounds);
        }

        if (node.Children.Count > 0)
        {
            e.Canvas.Save();
            e.Canvas.ClipRect(GetChildClipBounds(bounds).ToSKRect());

            foreach (var child in GetRenderableChildren(node))
                DrawNode(e, state, layout, child, view);

            e.Canvas.Restore();
        }

        if (selected)
            DesignerSelectionAdorner.Draw(e, bounds, DesignerLayoutProperties.GetResizeHandles(node));
    }

    private static void DrawPlaceholder(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        switch (node.TypeName)
        {
            case "Panel":
                DrawPanel(e, bounds);
                break;
            case "Label":
                DrawLabel(e, node, bounds);
                break;
            case "TextBox":
            case "RichTextBox":
                DrawTextBox(e, node, bounds);
                break;
            case "Button":
                DrawButton(e, node, bounds);
                break;
            default:
                DrawUnknown(e, node, bounds);
                break;
        }
    }

    private static bool ShouldUseSpecialDesignerRendering(DesignControlNode node)
        => DesignerSpecialContainers.IsSplitContainer(node)
        || DesignerSpecialContainers.IsTabControl(node)
        || DesignerSpecialContainers.IsFlowLayoutPanel(node)
        || DesignerSpecialContainers.IsTableLayoutPanel(node)
        || DesignerSpecialContainers.IsTabPage(node)
        || DesignerSpecialContainers.IsSplitPanel(node);

    private void DrawSpecialContainer(
        PaintEventArgs e,
        DesignControlNode node,
        DesignerLayoutResult layout,
        DesignerSurfaceView view,
        System.Drawing.Rectangle bounds)
    {
        if (DesignerSpecialContainers.IsSplitContainer(node))
        {
            DrawPanel(e, bounds);
            var splitter = coordinateMapper.ToSurfaceBounds(DesignerSpecialContainers.GetSplitterBounds(node, layout.GetEffectiveBounds(node)), view);
            var splitterBounds = ToDevice(e, splitter);
            e.Canvas.FillRectangle(splitterBounds, new SKColor(190, 190, 190));
            e.Canvas.DrawRectangle(splitterBounds, new SKColor(115, 115, 115));
            return;
        }

        if (DesignerSpecialContainers.IsTabControl(node))
        {
            DrawTabControl(e, node, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsTableLayoutPanel(node))
        {
            DrawPanel(e, bounds);
            DrawTableGrid(e, node, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsFlowLayoutPanel(node)
            || DesignerSpecialContainers.IsSplitPanel(node)
            || DesignerSpecialContainers.IsTabPage(node))
        {
            DrawPanel(e, bounds);
        }
    }

    private static IEnumerable<DesignControlNode> GetRenderableChildren(DesignControlNode node)
    {
        if (DesignerSpecialContainers.IsTabControl(node))
        {
            if (DesignerSpecialContainers.GetSelectedTabPage(node) is { } page)
                yield return page;

            yield break;
        }

        foreach (var child in node.Children)
            yield return child;
    }

    private static void DrawTabControl(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, new SKColor(245, 245, 245));
        e.Canvas.DrawRectangle(bounds, new SKColor(115, 115, 115));

        var headerHeight = e.LogicalToDeviceUnits(24);
        var x = bounds.Left + e.LogicalToDeviceUnits(4);
        var selectedIndex = DesignerSpecialContainers.GetSelectedTabIndex(node);

        for (var index = 0; index < node.Children.Count; index++)
        {
            var page = node.Children[index];

            if (!DesignerSpecialContainers.IsTabPage(page))
                continue;

            var text = GetText(page, page.Name);
            var tabWidth = e.LogicalToDeviceUnits(Math.Clamp(48 + (text.Length * 6), 64, 140));
            var tabBounds = new System.Drawing.Rectangle(x, bounds.Top + e.LogicalToDeviceUnits(2), tabWidth, headerHeight);
            var selected = index == selectedIndex;

            e.Canvas.FillRectangle(tabBounds, selected ? new SKColor(255, 255, 255) : new SKColor(230, 230, 230));
            e.Canvas.DrawRectangle(tabBounds, new SKColor(115, 115, 115));
            e.Canvas.DrawText(
                text,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(tabBounds.Left + e.LogicalToDeviceUnits(6), tabBounds.Top, Math.Max(1, tabBounds.Width - e.LogicalToDeviceUnits(12)), tabBounds.Height),
                new SKColor(20, 20, 20),
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);

            x += tabWidth;
        }
    }

    private static void DrawTableGrid(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        var columns = Math.Max(1, DesignerSpecialContainers.GetInt(node, DesignerSpecialContainers.ColumnCountPropertyName, 2));
        var rows = Math.Max(1, DesignerSpecialContainers.GetInt(node, DesignerSpecialContainers.RowCountPropertyName, 2));

        using var paint = new SKPaint
        {
            Color = new SKColor(165, 165, 165),
            StrokeWidth = e.LogicalToDeviceUnits(1),
            IsAntialias = false,
            PathEffect = SKPathEffect.CreateDash(new[] { 3f, 3f }, 0)
        };

        for (var column = 1; column < columns; column++)
        {
            var x = bounds.Left + (bounds.Width * column / columns);
            e.Canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, paint);
        }

        for (var row = 1; row < rows; row++)
        {
            var y = bounds.Top + (bounds.Height * row / rows);
            e.Canvas.DrawLine(bounds.Left, y, bounds.Right, y, paint);
        }
    }

    private void DrawRuntimeControl(
        PaintEventArgs e,
        DesignerSession state,
        DesignControlNode node,
        System.Drawing.Rectangle surfaceBounds,
        System.Drawing.Rectangle deviceBounds)
    {
        var saved = false;

        try
        {
            if (state.ResolveControlType(node) is not { } controlType
                || !typeof(Control).IsAssignableFrom(controlType)
                || Activator.CreateInstance(controlType) is not Control control)
            {
                LogRuntimeRenderFailure(state, node, $"Could not create runtime control for type '{node.TypeName}'.");
                DrawUnknown(e, node, deviceBounds);
                return;
            }

            control.Name = node.Name;
            control.Bounds = new System.Drawing.Rectangle(0, 0, Math.Max(1, surfaceBounds.Width), Math.Max(1, surfaceBounds.Height));
            var propertyErrors = new List<string>();
            var appliedPropertyCount = ApplyNodeProperties(control, node, propertyErrors);
            ApplyDesignTimePreviewState(control, node);

            e.Canvas.Save();
            saved = true;
            e.Canvas.ClipRect(deviceBounds.ToSKRect());

            if (!RuntimeControlPainter.TryPaint(e, control, deviceBounds, out var diagnostics, out var error))
            {
                LogRuntimeRenderFailure(state, node, error ?? "Unknown renderer failure.");
                DrawUnknown(e, node, deviceBounds);
            }
            else
            {
                LogRuntimeRenderDiagnostics(state, node, control, diagnostics, appliedPropertyCount, propertyErrors.Count);
            }

            foreach (var propertyError in propertyErrors)
                LogRuntimePropertyFailure(state, node, propertyError);

            e.Canvas.Restore();
            saved = false;
        }
        catch (Exception ex)
        {
            if (saved)
                e.Canvas.Restore();

            LogRuntimeRenderFailure(state, node, $"{ex.GetType().Name}: {ex.Message}");
            DrawUnknown(e, node, deviceBounds);
        }
    }

    private static int ApplyNodeProperties(Control control, DesignControlNode node, List<string> propertyErrors)
    {
        var appliedCount = 0;

        foreach (var property in node.Properties)
        {
            var propertyPath = NormalizeRuntimePropertyPath(property.Key);

            if (IsDesignerAuthoredLayoutProperty(propertyPath))
                continue;

            if (propertyPath.Contains('.', StringComparison.Ordinal))
            {
                if (ApplyNestedProperty(control, propertyPath, property.Value, out var error))
                    appliedCount++;
                else
                    propertyErrors.Add($"{property.Key}: {error}");

                continue;
            }

            if (ApplyProperty(control, propertyPath, property.Value, out var propertyError))
                appliedCount++;
            else
                propertyErrors.Add($"{property.Key}: {propertyError}");
        }

        return appliedCount;
    }

    private static bool IsDesignerAuthoredLayoutProperty(string path)
    {
        var root = path.Split('.', 2)[0];

        // The designer surface has already resolved authored bounds into a preview
        // rectangle before this runtime control is created. Reapplying X/Y/Bounds here
        // moves the temporary preview control inside its own bitmap and can make the
        // real renderer paint outside the isolated surface.
        return root is
            "Bounds" or
            "Location" or
            "Size" or
            "X" or
            "Y" or
            "Left" or
            "Top" or
            "Right" or
            "Bottom" or
            "Width" or
            "Height" or
            "Dock" or
            "Anchor" or
            "Margin" or
            "MinimumSize" or
            "MaximumSize";
    }

    private static void ApplyDesignTimePreviewState(Control control, DesignControlNode node)
    {
        if (string.IsNullOrWhiteSpace(control.Text) && UsesTextPreview(control))
            control.Text = node.Name;

        if (control is ComboBox comboBox && comboBox.Items.Count == 0)
        {
            var previewText = GetText(node, control.Text);

            if (string.IsNullOrWhiteSpace(previewText))
                previewText = node.Name;

            comboBox.Items.Add(previewText);
            comboBox.SelectedIndex = 0;
        }
        else if (control is ListBox listBox && listBox.Items.Count == 0)
        {
            var previewText = GetText(node, node.Name);

            if (!string.IsNullOrWhiteSpace(previewText))
                listBox.Items.Add(previewText);
        }
    }

    private static bool UsesTextPreview(Control control)
        => control is Button
            or CheckBox
            or ComboBox
            or Label
            or LinkLabel
            or RadioButton
            or TextBox;

    private void LogRuntimeRenderFailure(DesignerSession state, DesignControlNode node, string message)
    {
        var key = $"{node.TypeName}:{message}";

        if (loggedRuntimeRenderFailures.Add(key))
            state.Log($"Runtime rendering failed for {node.Name} ({node.TypeName}): {message}");
    }

    private void LogFrameDiagnostics(DesignerSession state, int width, int height)
    {
        var signature = string.Join(
            "|",
            state.ControlRenderMode,
            width,
            height,
            state.Document.ClassName,
            state.Document.Controls.Count,
            state.EnumerateNodes().Count());

        if (string.Equals(lastFrameDiagnostics, signature, StringComparison.Ordinal))
            return;

        lastFrameDiagnostics = signature;
        state.LogDiagnostic(
            $"Surface frame: mode={state.ControlRenderMode}, viewport={width}x{height}, " +
            $"document={state.Document.ClassName}, rootControls={state.Document.Controls.Count}, totalNodes={state.EnumerateNodes().Count()}.");
    }

    private void LogPlaceholderRenderDiagnostics(DesignerSession state, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        var signature = $"{node.TypeName}|{bounds.X}|{bounds.Y}|{bounds.Width}|{bounds.Height}";

        if (placeholderRenderDiagnostics.TryGetValue(node, out var previous)
            && string.Equals(previous, signature, StringComparison.Ordinal))
        {
            return;
        }

        placeholderRenderDiagnostics[node] = signature;
        state.LogDiagnostic($"Placeholder rendered {node.Name} ({node.TypeName}) at {bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height}.");
    }

    private void LogRuntimePropertyFailure(DesignerSession state, DesignControlNode node, string message)
    {
        var key = $"{node.Name}:{message}";

        if (loggedRuntimePropertyFailures.Add(key))
            state.Log($"Runtime property skipped for {node.Name}: {message}");
    }

    private void LogRuntimeRenderDiagnostics(
        DesignerSession state,
        DesignControlNode node,
        Control control,
        RuntimeControlPaintDiagnostics diagnostics,
        int appliedPropertyCount,
        int skippedPropertyCount)
    {
        var background = DesignerPropertyValueEditor.ToHex(control.CurrentStyle.GetBackgroundColor());
        var foreground = DesignerPropertyValueEditor.ToHex(control.CurrentStyle.GetForegroundColor());
        var signature = string.Join(
            "|",
            control.GetType().FullName,
            diagnostics.Width,
            diagnostics.Height,
            diagnostics.VisibleSampleCount,
            diagnostics.OpaqueSampleCount,
            appliedPropertyCount,
            skippedPropertyCount,
            control.Text,
            background,
            foreground,
            IsDesignNodeVisible(node),
            control.Enabled);

        if (runtimeRenderDiagnostics.TryGetValue(node, out var previous) && previous == signature)
            return;

        runtimeRenderDiagnostics[node] = signature;
        state.Log(
            $"Runtime rendered {node.Name} as {control.GetType().Name}: " +
            $"{diagnostics.Width}x{diagnostics.Height}, visible samples {diagnostics.VisibleSampleCount}/{diagnostics.SampleCount}, " +
            $"opaque {diagnostics.OpaqueSampleCount}, properties applied {appliedPropertyCount}, skipped {skippedPropertyCount}, " +
            $"Text='{control.Text}', BackColor={background}, ForeColor={foreground}.");
    }

    private static bool IsDesignNodeVisible(DesignControlNode node)
    {
        if (!node.Properties.TryGetValue("Visible", out var value))
            return true;

        return value.Kind switch
        {
            DesignPropertyValueKind.Boolean when value.Value is bool visible => visible,
            DesignPropertyValueKind.String when bool.TryParse(value.ToString(), out var visible) => visible,
            _ => true
        };
    }

    private static string NormalizeRuntimePropertyPath(string path)
        => path.StartsWith("CurrentStyle.", StringComparison.Ordinal)
            ? "Style" + path["CurrentStyle".Length..]
            : path;

    private static bool ApplyNestedProperty(Control control, string path, DesignPropertyValue value, out string? error)
    {
        error = null;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            error = "Nested property path is empty.";
            return false;
        }

        object? current = control;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current is null)
            {
                error = $"Parent value before '{parts[i]}' is null.";
                return false;
            }

            var property = current.GetType().GetProperty(parts[i], BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                error = $"Property '{parts[i]}' was not found on {current.GetType().Name}.";
                return false;
            }

            current = property?.GetValue(current);
        }

        if (current is null)
        {
            error = "Nested property parent is null.";
            return false;
        }

        return ApplyProperty(current, parts[^1], value, out error);
    }

    private static bool ApplyProperty(object target, string name, DesignPropertyValue value, out string? error)
    {
        error = null;
        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);

        if (property is null || property.SetMethod is null || !property.SetMethod.IsPublic)
        {
            error = property is null
                ? $"Property '{name}' was not found on {target.GetType().Name}."
                : $"Property '{name}' on {target.GetType().Name} is read-only.";
            return false;
        }

        try
        {
            var converted = DesignerPropertyValueEditor.FromDesignPropertyValue(value, property.PropertyType);
            property.SetValue(target, converted);
            return true;
        }
        catch (Exception ex)
        {
            // Designer documents can carry values for properties whose runtime surface is not
            // ready yet. Rendering should stay resilient and fall back to the remaining values.
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static void DrawPanel(PaintEventArgs e, System.Drawing.Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, new SKColor(238, 238, 238));

        using var paint = new SKPaint
        {
            Color = new SKColor(126, 126, 126),
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = e.LogicalToDeviceUnits(1),
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
        };

        e.Canvas.DrawRect(bounds.ToSKRect(), paint);
    }

    private static void DrawButton(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, new SKColor(239, 239, 239));
        e.Canvas.DrawRectangle(bounds, new SKColor(126, 126, 126));
        DrawCenteredText(e, GetText(node, node.Name), bounds, new SKColor(20, 20, 20));
    }

    private static void DrawLabel(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        e.Canvas.DrawText(
            GetText(node, node.Name),
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            bounds,
            new SKColor(20, 20, 20),
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private static void DrawTextBox(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, SKColors.White);
        e.Canvas.DrawRectangle(bounds, new SKColor(122, 122, 122));
        e.Canvas.DrawText(
            GetText(node, string.Empty),
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(bounds.Left + e.LogicalToDeviceUnits(4), bounds.Top, Math.Max(1, bounds.Width - e.LogicalToDeviceUnits(8)), bounds.Height),
            new SKColor(20, 20, 20),
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private static void DrawUnknown(PaintEventArgs e, DesignControlNode node, System.Drawing.Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, new SKColor(232, 237, 242));
        e.Canvas.DrawRectangle(bounds, new SKColor(121, 133, 148));
        DrawCenteredText(e, node.Name, bounds, new SKColor(32, 38, 46));
    }

    private static void DrawCenteredText(PaintEventArgs e, string text, System.Drawing.Rectangle bounds, SKColor color)
    {
        e.Canvas.DrawText(
            text,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            bounds,
            color,
            ContentAlignment.MiddleCenter,
            maxLines: 1,
            ellipsis: true);
    }

    private static string GetText(DesignControlNode node, string fallback)
        => node.Properties.TryGetValue("Text", out var value) ? value.GetString() : fallback;

    private static System.Drawing.Rectangle GetChildClipBounds(System.Drawing.Rectangle bounds)
        => bounds;

    private static System.Drawing.Rectangle ToDevice(PaintEventArgs e, System.Drawing.Rectangle bounds)
        => new(
            e.LogicalToDeviceUnits(bounds.X),
            e.LogicalToDeviceUnits(bounds.Y),
            e.LogicalToDeviceUnits(bounds.Width),
            e.LogicalToDeviceUnits(bounds.Height));
}

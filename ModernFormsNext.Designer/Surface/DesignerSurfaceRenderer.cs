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
    private readonly HashSet<string> loggedEmbeddedPreviewMessages = [];
    private readonly Dictionary<DesignControlNode, string> runtimeRenderDiagnostics = [];
    private readonly Dictionary<DesignControlNode, string> placeholderRenderDiagnostics = [];
    private DesignerEmbeddedPreviewCache? embeddedPreviewCache;
    private string? embeddedPreviewProjectPath;
    private string? lastFrameDiagnostics;

    public void Render(PaintEventArgs e, DesignerSession state, int width, int height)
    {
        LogFrameDiagnostics(state, width, height);

        var view = coordinateMapper.GetView(state, width, height);
        var formBounds = DesignerDpiCoordinateConverter.LogicalToDevice(
            new System.Drawing.Rectangle(view.FormX, view.FormY, view.FormWidth, view.FormHeight),
            e.Scaling);
        var clientBounds = DesignerDpiCoordinateConverter.LogicalToDevice(
            new System.Drawing.Rectangle(view.ClientX, view.ClientY, view.ClientWidth, view.ClientHeight),
            e.Scaling);
        var previewPaintArgs = new PaintEventArgs(
            e.Info,
            e.Canvas,
            DesignerDpiCoordinateConverter.CombineWithPreviewScale(e.Scaling, view.Scale));

        e.Canvas.FillRectangle(new System.Drawing.Rectangle(0, 0, e.LogicalToDeviceUnits(width), e.LogicalToDeviceUnits(height)), DesignerColors.Workspace);
        var layout = layoutEngine.Layout(state.Document);

        DrawRoot(e, previewPaintArgs, state, formBounds, clientBounds, view);

        e.Canvas.Save();
        e.Canvas.ClipRect(clientBounds.ToSKRect());

        DrawNodesInPaintOrder(
            e,
            previewPaintArgs,
            state,
            layout,
            state.Document.Controls,
            parentNode: null,
            view,
            offsetX: 0,
            offsetY: 0,
            previewStack: new HashSet<string>(StringComparer.Ordinal));

        e.Canvas.Restore();

        if (state.SelectedNode is null)
        {
            var rootHandles = state.Document.RootKind == DesignRootKind.UserControl
                ? DesignerHitTestService.GetRootHandles()
                : [];
            DesignerSelectionAdorner.Draw(e, formBounds, rootHandles);
        }
    }

    public bool TryMapToDocument(
        DesignerSession state,
        int width,
        int height,
        float x,
        float y,
        out DesignPoint point)
        => coordinateMapper.TryMapToDocument(state, width, height, x, y, out point);

    private static void DrawRoot(
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        System.Drawing.Rectangle formBounds,
        System.Drawing.Rectangle clientBounds,
        DesignerSurfaceView view)
    {
        surfacePaintArgs.Canvas.FillRectangle(formBounds, DesignerColors.FormBorder);

        if (state.Document.RootKind == DesignRootKind.UserControl)
        {
            surfacePaintArgs.Canvas.FillRectangle(clientBounds, new SKColor(245, 245, 245));
            surfacePaintArgs.Canvas.DrawRectangle(formBounds, new SKColor(123, 183, 224), surfacePaintArgs.LogicalToDeviceUnits(1));
            DrawGrid(previewPaintArgs, clientBounds);
            return;
        }

        surfacePaintArgs.Canvas.FillRectangle(
            new System.Drawing.Rectangle(formBounds.Left, formBounds.Top, formBounds.Width, surfacePaintArgs.LogicalToDeviceUnits(view.TitleHeight)),
            new SKColor(218, 232, 246));
        surfacePaintArgs.Canvas.FillRectangle(clientBounds, new SKColor(245, 245, 245));
        surfacePaintArgs.Canvas.DrawRectangle(formBounds, new SKColor(123, 183, 224), surfacePaintArgs.LogicalToDeviceUnits(1));
        DrawGrid(previewPaintArgs, clientBounds);
        surfacePaintArgs.Canvas.DrawText(
            state.Document.FormName,
            Theme.UIFont,
            surfacePaintArgs.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(
                formBounds.Left + surfacePaintArgs.LogicalToDeviceUnits(12),
                formBounds.Top,
                formBounds.Width - surfacePaintArgs.LogicalToDeviceUnits(24),
                surfacePaintArgs.LogicalToDeviceUnits(view.TitleHeight)),
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

        // Grid intervals are document units, so they use the composed preview+DPI scale.
        var step = Math.Max(1, e.LogicalToDeviceUnits(8));

        for (var x = bounds.Left + step; x < bounds.Right; x += step)
        {
            for (var y = bounds.Top + step; y < bounds.Bottom; y += step)
                e.Canvas.DrawPoint(x, y, paint);
        }
    }

    private void DrawNode(
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        DesignerLayoutResult layout,
        DesignControlNode node,
        DesignerSurfaceView view,
        int offsetX,
        int offsetY,
        HashSet<string> previewStack)
    {
        if (!IsDesignNodeVisible(node))
            return;

        var localAbsolute = layout.GetEffectiveBounds(node);
        var absolute = Offset(localAbsolute, offsetX, offsetY);
        var surfaceBounds = coordinateMapper.ToSurfaceBounds(absolute, view);
        var bounds = DesignerDpiCoordinateConverter.LogicalToDevice(surfaceBounds, surfacePaintArgs.Scaling);
        var selected = ReferenceEquals(state.SelectedNode, node);
        var isProjectUserControl = state.IsProjectUserControlType(node.TypeName);

        if (isProjectUserControl)
        {
            if (!TryDrawEmbeddedPreview(
                surfacePaintArgs,
                previewPaintArgs,
                state,
                node,
                absolute,
                bounds,
                view,
                previewStack))
            {
                LogPlaceholderRenderDiagnostics(state, node, bounds);
                DrawPlaceholder(previewPaintArgs, node, bounds);
            }
        }
        else if (ShouldUseSpecialDesignerRendering(node))
        {
            DrawSpecialContainer(surfacePaintArgs, previewPaintArgs, node, layout, view, bounds, offsetX, offsetY);
        }
        else if (state.ControlRenderMode == DesignerControlRenderMode.Runtime && !isProjectUserControl)
        {
            DrawRuntimeControl(surfacePaintArgs, previewPaintArgs, state, node, absolute, bounds);
        }
        else
        {
            LogPlaceholderRenderDiagnostics(state, node, bounds);
            DrawPlaceholder(previewPaintArgs, node, bounds);
        }

        if (node.Children.Count > 0 && !isProjectUserControl)
        {
            surfacePaintArgs.Canvas.Save();
            surfacePaintArgs.Canvas.ClipRect(GetChildClipBounds(bounds).ToSKRect());

            if (DesignerSpecialContainers.IsTabControl(node))
            {
                if (DesignerSpecialContainers.GetSelectedTabPage(node) is { } page)
                    DrawNode(surfacePaintArgs, previewPaintArgs, state, layout, page, view, offsetX, offsetY, previewStack);
            }
            else
            {
                DrawNodesInPaintOrder(
                    surfacePaintArgs,
                    previewPaintArgs,
                    state,
                    layout,
                    node.Children,
                    node,
                    view,
                    offsetX,
                    offsetY,
                    previewStack);
            }

            surfacePaintArgs.Canvas.Restore();
        }

        if (selected)
            DesignerSelectionAdorner.Draw(surfacePaintArgs, bounds, DesignerLayoutProperties.GetResizeHandles(node));
    }

    private bool TryDrawEmbeddedPreview(
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        DesignControlNode instanceNode,
        DesignBounds logicalBounds,
        System.Drawing.Rectangle deviceBounds,
        DesignerSurfaceView view,
        HashSet<string> previewStack)
    {
        var cache = GetEmbeddedPreviewCache(state);

        if (!cache.TryGetPreview(
            instanceNode.TypeName,
            new DesignSize(logicalBounds.Width, logicalBounds.Height),
            out var preview,
            out var error))
        {
            LogEmbeddedPreviewMessage(
                state,
                $"fallback:{instanceNode.TypeName}:{error}",
                $"Custom UserControl preview fallback for {instanceNode.Name} ({instanceNode.TypeName}): {error}");
            return false;
        }

        if (!previewStack.Add(preview!.TypeName))
        {
            LogEmbeddedPreviewMessage(
                state,
                $"cycle:{preview.TypeName}",
                $"Custom UserControl preview cycle detected at {instanceNode.Name} ({preview.TypeName}); using placeholder fallback.");
            return false;
        }

        try
        {
            DrawEmbeddedPreviewRoot(
                surfacePaintArgs,
                previewPaintArgs,
                state,
                instanceNode,
                preview.Document,
                logicalBounds,
                deviceBounds);

            surfacePaintArgs.Canvas.Save();
            try
            {
                surfacePaintArgs.Canvas.ClipRect(deviceBounds.ToSKRect());
                DrawNodesInPaintOrder(
                    surfacePaintArgs,
                    previewPaintArgs,
                    state,
                    preview.Layout,
                    preview.Document.Controls,
                    parentNode: null,
                    view,
                    logicalBounds.X,
                    logicalBounds.Y,
                    previewStack);
            }
            finally
            {
                surfacePaintArgs.Canvas.Restore();
            }

            LogEmbeddedPreviewMessage(
                state,
                $"success:{preview.TypeName}:{preview.DocumentPath}",
                $"Rendered safe custom UserControl preview for {instanceNode.Name} ({preview.TypeName}) " +
                $"from {preview.DocumentPath}; root children={preview.Document.Controls.Count}.");
            return true;
        }
        finally
        {
            previewStack.Remove(preview.TypeName);
        }
    }

    private void DrawEmbeddedPreviewRoot(
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        DesignControlNode instanceNode,
        DesignDocument previewDocument,
        DesignBounds logicalBounds,
        System.Drawing.Rectangle deviceBounds)
    {
        if (state.ControlRenderMode != DesignerControlRenderMode.Runtime)
        {
            DrawPanel(previewPaintArgs, deviceBounds);
            return;
        }

        var rootNode = new DesignControlNode
        {
            TypeName = typeof(UserControl).FullName!,
            Name = instanceNode.Name,
            Bounds = new DesignBounds(0, 0, logicalBounds.Width, logicalBounds.Height),
            Properties = new SortedDictionary<string, DesignPropertyValue>(
                previewDocument.Properties,
                StringComparer.Ordinal)
        };

        // Parent-authored instance properties run after the custom control's generated defaults at
        // runtime, so they also override root defaults in the data-only preview projection.
        foreach (var property in instanceNode.Properties)
            rootNode.Properties[property.Key] = property.Value;

        DrawRuntimeControl(
            surfacePaintArgs,
            previewPaintArgs,
            state,
            rootNode,
            logicalBounds,
            deviceBounds);
    }

    private DesignerEmbeddedPreviewCache GetEmbeddedPreviewCache(DesignerSession state)
    {
        var projectPath = state.CurrentProjectPath;

        if (embeddedPreviewCache is null
            || !string.Equals(embeddedPreviewProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
        {
            embeddedPreviewProjectPath = projectPath;
            embeddedPreviewCache = new DesignerEmbeddedPreviewCache(projectPath, state.ProjectUserControls);
            loggedEmbeddedPreviewMessages.Clear();
        }

        return embeddedPreviewCache;
    }

    private void LogEmbeddedPreviewMessage(
        DesignerSession state,
        string key,
        string message)
    {
        if (loggedEmbeddedPreviewMessages.Add(key))
            state.Log(message);
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
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignControlNode node,
        DesignerLayoutResult layout,
        DesignerSurfaceView view,
        System.Drawing.Rectangle bounds,
        int offsetX,
        int offsetY)
    {
        if (DesignerSpecialContainers.IsSplitContainer(node))
        {
            DrawPanel(previewPaintArgs, bounds);
            var logicalSplitterBounds = Offset(
                DesignerSpecialContainers.GetSplitterBounds(node, layout.GetEffectiveBounds(node)),
                offsetX,
                offsetY);
            var splitter = coordinateMapper.ToSurfaceBounds(logicalSplitterBounds, view);
            var deviceSplitterBounds = DesignerDpiCoordinateConverter.LogicalToDevice(splitter, surfacePaintArgs.Scaling);
            surfacePaintArgs.Canvas.FillRectangle(deviceSplitterBounds, new SKColor(190, 190, 190));
            surfacePaintArgs.Canvas.DrawRectangle(deviceSplitterBounds, new SKColor(115, 115, 115));
            return;
        }

        if (DesignerSpecialContainers.IsTabControl(node))
        {
            DrawTabControl(previewPaintArgs, node, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsTableLayoutPanel(node))
        {
            DrawPanel(previewPaintArgs, bounds);
            DrawTableGrid(previewPaintArgs, node, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsFlowLayoutPanel(node)
            || DesignerSpecialContainers.IsSplitPanel(node)
            || DesignerSpecialContainers.IsTabPage(node))
        {
            DrawPanel(previewPaintArgs, bounds);
        }
    }

    private void DrawNodesInPaintOrder(
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        DesignerLayoutResult layout,
        DesignControlCollection nodes,
        DesignControlNode? parentNode,
        DesignerSurfaceView view,
        int offsetX,
        int offsetY,
        HashSet<string> previewStack)
    {
        // Ordinary document collections are front-to-back, so paint them from the last element to
        // index zero. Sequential containers retain runtime collection order and therefore paint
        // forward. This is deliberately container-aware rather than a global reverse operation.
        if (parentNode is not null && PreservesSequentialChildOrder(parentNode))
        {
            for (var index = 0; index < nodes.Count; index++)
                DrawNode(surfacePaintArgs, previewPaintArgs, state, layout, nodes[index], view, offsetX, offsetY, previewStack);

            return;
        }

        for (var index = nodes.Count - 1; index >= 0; index--)
            DrawNode(surfacePaintArgs, previewPaintArgs, state, layout, nodes[index], view, offsetX, offsetY, previewStack);
    }

    private static bool PreservesSequentialChildOrder(DesignControlNode node)
        => DesignerSpecialContainers.IsFlowLayoutPanel(node)
        || DesignerSpecialContainers.IsTableLayoutPanel(node)
        || DesignerSpecialContainers.IsTabControl(node);

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
            StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1)),
            IsAntialias = false,
            PathEffect = SKPathEffect.CreateDash(
                new[] { (float)Math.Max(1, e.LogicalToDeviceUnits(3)), (float)Math.Max(1, e.LogicalToDeviceUnits(3)) },
                0)
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
        PaintEventArgs surfacePaintArgs,
        PaintEventArgs previewPaintArgs,
        DesignerSession state,
        DesignControlNode node,
        DesignBounds logicalBounds,
        System.Drawing.Rectangle deviceBounds)
    {
        var saved = false;

        try
        {
            if (ResolveFrameworkControlType(node.TypeName) is not { } controlType
                || !typeof(Control).IsAssignableFrom(controlType)
                || Activator.CreateInstance(controlType) is not Control control)
            {
                LogRuntimeRenderFailure(state, node, $"Could not safely create framework control for type '{node.TypeName}'.");
                DrawUnknown(previewPaintArgs, node, deviceBounds);
                return;
            }

            control.Name = node.Name;
            var logicalSize = new System.Drawing.Size(Math.Max(1, logicalBounds.Width), Math.Max(1, logicalBounds.Height));
            control.Bounds = new System.Drawing.Rectangle(System.Drawing.Point.Empty, logicalSize);
            var propertyErrors = new List<string>();
            var appliedPropertyCount = ApplyNodeProperties(control, node, propertyErrors);
            ApplyDesignTimePreviewState(control, node);

            surfacePaintArgs.Canvas.Save();
            saved = true;
            surfacePaintArgs.Canvas.ClipRect(deviceBounds.ToSKRect());

            if (!RuntimeControlPainter.TryPaint(surfacePaintArgs, control, logicalSize, deviceBounds, out var diagnostics, out var error))
            {
                LogRuntimeRenderFailure(state, node, error ?? "Unknown renderer failure.");
                DrawUnknown(previewPaintArgs, node, deviceBounds);
            }
            else
            {
                LogRuntimeRenderDiagnostics(state, node, control, diagnostics, appliedPropertyCount, propertyErrors.Count);
            }

            foreach (var propertyError in propertyErrors)
                LogRuntimePropertyFailure(state, node, propertyError);

            surfacePaintArgs.Canvas.Restore();
            saved = false;
        }
        catch (Exception ex)
        {
            if (saved)
                surfacePaintArgs.Canvas.Restore();

            LogRuntimeRenderFailure(state, node, $"{ex.GetType().Name}: {ex.Message}");
            DrawUnknown(previewPaintArgs, node, deviceBounds);
        }
    }

    private static Type? ResolveFrameworkControlType(string typeName)
    {
        // Preview rendering resolves types only from the already-loaded framework assembly. Using
        // Type.GetType for an assembly-qualified project type could load user binaries before the
        // custom UserControl boundary is recognized, which would violate the data-only contract.
        var normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        var frameworkAssembly = typeof(Control).Assembly;
        return frameworkAssembly.GetType(normalized, throwOnError: false)
            ?? frameworkAssembly.GetType($"ModernFormsNext.{normalized}", throwOnError: false);
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

        // The designer surface has already resolved authored bounds into the logical size used
        // by the detached preview control. Reapplying X/Y/Bounds here moves the temporary control
        // inside its isolated bitmap or bypasses the uniform preview+DPI canvas transform.
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
            StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1)),
            PathEffect = SKPathEffect.CreateDash(
                new[] { (float)Math.Max(1, e.LogicalToDeviceUnits(4)), (float)Math.Max(1, e.LogicalToDeviceUnits(3)) },
                0)
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

    private static DesignBounds Offset(DesignBounds bounds, int offsetX, int offsetY)
        => new(
            bounds.X + offsetX,
            bounds.Y + offsetY,
            bounds.Width,
            bounds.Height);

}

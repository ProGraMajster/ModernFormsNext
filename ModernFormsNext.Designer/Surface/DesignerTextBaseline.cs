using System.Drawing;
using ModernFormsNext.Designing;
using ModernFormsNext.Layout;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerTextBaseline
{
    internal const int MaximumTextLength = 4096;

    internal static double? Measure(DesignControlNode node, DesignerSnapBounds bounds)
    {
        if (!node.Properties.TryGetValue("Text", out var value) || value.Kind != DesignPropertyValueKind.String
            || value.Value is not string text || string.IsNullOrWhiteSpace(text) || text.Length > MaximumTextLength
            || text.Contains('\n') || text.Contains('\r') || node.Properties.ContainsKey("Style")
            || node.Properties.ContainsKey("Image") || node.Properties.ContainsKey("ImageList")
            || node.Properties.TryGetValue("Multiline", out var multiline) && (multiline.Value is true
                || multiline.Value is string multilineText && bool.TryParse(multilineText, out var wraps) && wraps)
            || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > int.MaxValue || bounds.Height > int.MaxValue) return null;
        var type = DesignerSurfaceRenderer.ResolveFrameworkControlType(node.TypeName);
        // These are exact trusted framework types, not subclasses or project constructors.
        using Control? control = type == typeof(Label) ? new Label() : type == typeof(Button) ? new Button() : null;
        if (control is null) return null;
        try
        {
            DesignerPreviewControl.Prepare(control);
            control.Bounds = new Rectangle(0, 0, (int)bounds.Width, (int)bounds.Height);
            var projection = new DesignControlNode { TypeName = node.TypeName };
            foreach (var property in node.Properties)
                if (IsTextLayoutProperty(property.Key)) projection.Properties.Add(property.Key, property.Value);
            var errors = new List<string>();
            DesignerSurfaceRenderer.ApplyPreviewProperties(control, projection, errors, includeLayout: true);
            if (errors.Count != 0) return null;
            return MeasureControl(control);
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null; // An unsupported inert text projection has no trustworthy baseline.
        }
    }

    // Keep access to the production internal layout/shaper in the existing Designer friend
    // assembly. Both neutral projection and independently configured control checks use it;
    // callers never construct project controls or allocate a rendering bitmap to measure ink.
    internal static double? MeasureControl(Control control)
    {
        if (control.GetType() != typeof(Label) && control.GetType() != typeof(Button)) return null;
        var alignment = control is Label label ? label.TextAlign : ((Button)control).TextAlign;
        bool ellipsis = control is Label labelControl ? labelControl.AutoEllipsis : ((Button)control).AutoEllipsis;
        var layout = TextImageLayoutEngine.Layout(control);
        var style = control.CurrentStyle;
        var block = TextMeasurer.CreateTextBlock(control.Text, style.GetFont(), control.LogicalToDeviceUnits(style.GetFontSize()),
            layout.TextBounds.Size, TextMeasurer.GetTextAlign(alignment), maxLines: 1, ellipsis: ellipsis,
            fontStyle: style.GetFontStyle(), lineHeight: style.GetLineHeight(), letterSpacing: TextMeasurer.GetDeviceLetterSpacing(control));
        var line = block.Lines.FirstOrDefault();
        if (line is null) return null;
        int top = layout.TextBounds.Top;
        var vertical = TextMeasurer.GetVerticalAlign(alignment);
        if (vertical == SKTextAlign.Right) top = layout.TextBounds.Bottom - (int)block.MeasuredHeight;
        else if (vertical == SKTextAlign.Center) top += (layout.TextBounds.Height - (int)block.MeasuredHeight) / 2;
        double baseline = top + line.BaseLine;
        return double.IsFinite(baseline) && baseline >= 0 && baseline <= control.Height ? baseline : null;
    }

    private static bool IsTextLayoutProperty(string name)
        => name is "Text" or "Font" or "Padding" or "TextAlign" or "LineHeight" or "LetterSpacing" or "AutoEllipsis"
            or "Style.Font" or "Style.TextFont" or "Style.FontSize" or "Style.FontStyle" or "Style.LineHeight" or "Style.LetterSpacing"
            || name.StartsWith("Style.Border.", StringComparison.Ordinal) && name.EndsWith("Width", StringComparison.Ordinal);
}

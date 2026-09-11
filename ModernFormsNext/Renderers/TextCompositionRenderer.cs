using System;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext.Renderers;

/// <summary>
/// Paints the provisional IME range using the editor's existing shaped text block.
/// </summary>
internal static class TextCompositionRenderer
{
    // The caller supplies its actual plain, rich, or syntax-colored block and keeps the
    // normal text viewport clip active. All geometry here remains in rendering/device units;
    // the native candidate-window contract separately converts caret geometry to logical units.
    internal static void Render(TextBox control, PaintEventArgs e, TextBlock block, Point origin)
    {
        var document = control.document;
        if (!control.Selected || control.ReadOnly || !document.HasComposition)
            return;

        int start = document.GetLayoutCodePointIndex(document.CompositionStart);
        int end = document.GetLayoutCodePointIndex(document.CompositionEnd);
        if (start >= end)
            return;

        float thickness = Math.Max(1, e.LogicalToDeviceUnits(1));
        using var paint = new SKPaint {
            Color = control.CurrentStyle.GetForegroundColor(),
            IsAntialias = false,
            StrokeWidth = thickness,
            StrokeCap = SKStrokeCap.Butt
        };

        foreach (var line in block.Lines) {
            // Use the actual line's baseline/descent, including rich font sizes and wrapping.
            // Keep the stroke inside the line box so adjacent lines cannot erase each other.
            float y = origin.Y + line.YCoord + Math.Min(
                line.Height - thickness / 2,
                line.BaseLine + Math.Max(thickness, line.MaxDescent / 2));

            foreach (var run in line.Runs) {
                int runStart = Math.Max(start, run.Start);
                int runEnd = Math.Min(end, run.End);
                if (runStart >= runEnd || run.Width <= 0)
                    continue;

                // UTF-16 document offsets have already been mapped to layout code points.
                // Font-run positions also preserve bidirectional visual ordering and shaped
                // cluster advances; joining global caret endpoints would cross unrelated text.
                float x1 = run.GetXCoordOfCodePointIndex(runStart);
                float x2 = runEnd == run.End
                    ? run.XCoord + (run.Direction == TextDirection.RTL ? 0 : run.Width)
                    : run.GetXCoordOfCodePointIndex(runEnd);
                if (x1 != x2)
                    e.Canvas.DrawLine(origin.X + Math.Min(x1, x2), y,
                        origin.X + Math.Max(x1, x2), y, paint);
            }
        }
    }
}

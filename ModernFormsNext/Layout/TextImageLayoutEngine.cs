using System.Drawing;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext.Layout;

internal static class TextImageLayoutEngine
{
    public static TextImageLayoutData Layout (Control control)
    {
        var result = new TextImageLayoutData {
            Client = control.ClientRectangle,
        };

        CalculateFace (control, result);
        CalculateInitialField (control, result);
        CalculateGlyph (control, result);
        CalculateImageAndTextBounds (control, result);

        result.Focus = Rectangle.Inflate (result.Field, control.LogicalToDeviceUnits (3), control.LogicalToDeviceUnits (3));

        return result;
    }

    private static void CalculateFace (Control control, TextImageLayoutData layout)
    {
        BorderStyle border = control.CurrentStyle.Border;
        var left_border = control.LogicalToDeviceUnits (border.Left.GetWidth ());
        var top_border = control.LogicalToDeviceUnits (border.Top.GetWidth ());
        var right_border = control.LogicalToDeviceUnits (border.Right.GetWidth ());
        var bottom_border = control.LogicalToDeviceUnits (border.Bottom.GetWidth ());

        var face = new Rectangle (
            layout.Client.X + left_border,
            layout.Client.Y + top_border,
            layout.Client.Width - (left_border + right_border),
            layout.Client.Height - (top_border + bottom_border)
        );

        layout.Face = face;
    }

    private static void CalculateInitialField (Control control, TextImageLayoutData layout)
    {
        // The initial field is Control's ClientRectangle minus the border and padding.
        Padding padding = control.LogicalToDeviceUnits (control.PresentationPadding);
        var left_padding = padding.Left;
        var top_padding = padding.Top;
        var right_padding = padding.Right;
        var bottom_padding = padding.Bottom;

        var field = new Rectangle (
            layout.Face.X + left_padding,
            layout.Face.Y + top_padding,
            layout.Face.Width - (left_padding + right_padding),
            layout.Face.Height - (top_padding + bottom_padding)
        );

        layout.Field = field;
    }

    private static void CalculateGlyph (Control control, TextImageLayoutData layout)
    {
        if (control is not IHaveGlyph glyph_control)
            return;

        var renderer = RenderManager.GetRenderer<Renderer> (control);

        if (renderer is not IRenderGlyph glyph_renderer)
            return;

        var box_size = control.LogicalToDeviceUnits (glyph_renderer.GlyphSize);
        var glyph_padding = control.LogicalToDeviceUnits (glyph_renderer.GlyphTextPadding);
        var glyph_size_full = box_size + glyph_padding;

        if (box_size <= 0)
            return;

        layout.GlyphBounds = new Rectangle (layout.Field.X, layout.Field.Y, box_size, box_size);

        var align = glyph_control.GlyphAlign;
        var field = layout.Field;

        // Handle horizontal alignment of GlyphBounds
        if ((align & LayoutUtils.AnyRight) != 0)
            layout.GlyphBounds.X = (field.X + field.Width) - layout.GlyphBounds.Width;
        else if ((align & LayoutUtils.AnyCenter) != 0)
            layout.GlyphBounds.X = field.X + (field.Width - layout.GlyphBounds.Width) / 2;

        // Handle vertical alignment of GlyphBounds
        if ((align & LayoutUtils.AnyBottom) != 0)
            layout.GlyphBounds.Y = (field.Y + field.Height) - layout.GlyphBounds.Height;
        else if ((align & LayoutUtils.AnyTop) != 0)
            layout.GlyphBounds.Y = field.Y + 2; // + 2: this needs to be aligned to the text (
        else
            layout.GlyphBounds.Y = field.Y + (field.Height - layout.GlyphBounds.Height) / 2;

        // Calculate GlyphArea and Field
        switch (align) {
            case ContentAlignment.TopLeft:
            case ContentAlignment.MiddleLeft:
            case ContentAlignment.BottomLeft:
                layout.GlyphArea.X = field.X;
                layout.GlyphArea.Width = glyph_size_full + 1;

                layout.GlyphArea.Y = field.Y;
                layout.GlyphArea.Height = field.Height;

                layout.Field.X += glyph_size_full + 1;
                layout.Field.Width -= glyph_size_full + 1;
                break;
            case ContentAlignment.TopRight:
            case ContentAlignment.MiddleRight:
            case ContentAlignment.BottomRight:
                layout.GlyphArea.X = field.X + field.Width - glyph_size_full;
                layout.GlyphArea.Width = glyph_size_full + 1;

                layout.GlyphArea.Y = field.Y;
                layout.GlyphArea.Height = field.Height;

                layout.Field.Width -= glyph_size_full + 1;
                break;
            case ContentAlignment.TopCenter:
                layout.GlyphArea.X = field.X;
                layout.GlyphArea.Width = field.Width;

                layout.GlyphArea.Y = field.Y;
                layout.GlyphArea.Height = glyph_size_full;

                layout.Field.Y += glyph_size_full;
                layout.Field.Height -= glyph_size_full;
                break;

            case ContentAlignment.BottomCenter:
                layout.GlyphArea.X = field.X;
                layout.GlyphArea.Width = field.Width;

                layout.GlyphArea.Y = field.Y + field.Height - glyph_size_full;
                layout.GlyphArea.Height = glyph_size_full;

                layout.Field.Height -= glyph_size_full;
                break;

            case ContentAlignment.MiddleCenter:
                layout.GlyphArea = layout.GlyphBounds;
                break;
        }
    }

    private static void CalculateImageAndTextBounds (Control control, TextImageLayoutData layout)
    {
        if (control is not IHaveTextAndImageAlign text_image_control)
            return;

        var image_align = text_image_control.ImageAlign;
        var text_align = text_image_control.TextAlign;
        var text_image_relation = text_image_control.TextImageRelation;
        var image_size = control.LogicalToDeviceUnits (text_image_control.GetImage ()?.GetSize () ?? Size.Empty);
        var maxBounds = layout.Field;

        var measured_text = GetTextSize (control);
        var text_size = measured_text.ToSize ();
        // A fractional glyph advance still needs the final device pixel. Rounding down
        // incorrectly enables ellipsis even when the available field can fit the entire line.
        if (text_image_control.AutoEllipsis)
            text_size.Width = (int)Math.Ceiling (measured_text.Width);
        text_size.Height++;

        // Sizes cannot be bigger than max bounds
        text_size = text_size.Clamp (maxBounds.Size);
        image_size = image_size.Clamp (maxBounds.Size);

        // We only have one thing to layout, or it's overlay so position doesn't matter
        if (text_image_relation == TextImageRelation.Overlay || image_size.IsEmpty || !control.Text.HasValue ()) {

            layout.TextBounds = text_image_control.Multiline ? maxBounds : LayoutUtils.Align(text_size, maxBounds, text_align);
            layout.ImageBounds = LayoutUtils.Align (image_size, maxBounds, image_align);

            return;
        }

        var max_combined_bounds = maxBounds;

        var renderer = RenderManager.GetRenderer<Renderer> (control);
        var margin = 0;

        if (renderer is IRenderTextAndImage text_image_renderer)
            margin = control.LogicalToDeviceUnits (text_image_renderer.ImageTextMargin);

        if (text_image_control.AutoEllipsis) {
            // Ellipsis must use the field left after reserving the image and its gap. Otherwise
            // the union below grows beyond the client field and the ellipsis is clipped away.
            var available = LayoutUtils.SubAlignedRegion (maxBounds.Size, image_size, text_image_relation);
            if (LayoutUtils.IsVerticalRelation (text_image_relation)) {
                margin = Math.Min (margin, Math.Max (0, available.Height));
                available.Height = Math.Max (0, available.Height - margin);
            } else {
                margin = Math.Min (margin, Math.Max (0, available.Width));
                available.Width = Math.Max (0, available.Width - margin);
            }
            text_size = text_size.Clamp (available);
        }

        // Combine text & image into one rectangle that we center within maxBounds.
        var combined_size = LayoutUtils.AddAlignedRegion (text_size, image_size, text_image_relation);
        if (text_image_control.AutoEllipsis) {
            if (LayoutUtils.IsVerticalRelation (text_image_relation))
                combined_size.Height += margin;
            else
                combined_size.Width += margin;
        }
        max_combined_bounds.Size = LayoutUtils.UnionSizes (max_combined_bounds.Size, combined_size);
        var combined_bounds = LayoutUtils.Align (combined_size, max_combined_bounds, ContentAlignment.MiddleCenter);

        // ImageEdge indicates whether the combination of ImageAlign and TextImageRelation place
        // the image along the edge of the control.  If so, we can increase the space for text.
        var image_edge = (AnchorStyles)(ImageAlignToRelation (image_align) & text_image_relation) != AnchorStyles.None;

        // TextEdge indicates whether the combination of TextAlign and TextImageRelation place
        // the text along the edge of the control.  If so, we can increase the space for image.
        var text_edge = (AnchorStyles)(TextAlignToRelation (text_align) & text_image_relation) != AnchorStyles.None;

        Rectangle text_bounds;
        Rectangle image_bounds;

        if (image_edge) {
            // Just split imageSize off of maxCombinedBounds.
            LayoutUtils.SplitRegion (
                max_combined_bounds,
                image_size,
                (AnchorStyles)text_image_relation,
                margin,
                out image_bounds,
                out text_bounds);
        } else if (text_edge) {
            // Just split textSize off of maxCombinedBounds.
            LayoutUtils.SplitRegion (
                max_combined_bounds,
                text_size,
                (AnchorStyles)LayoutUtils.GetOppositeTextImageRelation (text_image_relation),
                margin,
                out text_bounds,
                out image_bounds);
        } else {
            // Expand the adjacent regions to maxCombinedBounds (centered) and split the rectangle into
            // imageBounds and textBounds.
            LayoutUtils.SplitRegion (
                combined_bounds,
                image_size,
                (AnchorStyles)text_image_relation,
                0,
                out image_bounds,
                out text_bounds);
            LayoutUtils.ExpandRegionsToFillBounds (
                max_combined_bounds,
                (AnchorStyles)text_image_relation,
                ref image_bounds,
                ref text_bounds);
            // Expansion requires adjacent regions. Reserve the image/text gap afterwards,
            // on the text edge facing the image, so it is not mistaken for a layout gap.
            switch (text_image_relation) {
                case TextImageRelation.ImageBeforeText:
                    text_bounds.X += margin;
                    text_bounds.Width -= margin;
                    break;
                case TextImageRelation.TextBeforeImage:
                    text_bounds.Width -= margin;
                    break;
                case TextImageRelation.ImageAboveText:
                    text_bounds.Y += margin;
                    text_bounds.Height -= margin;
                    break;
                case TextImageRelation.TextAboveImage:
                    text_bounds.Height -= margin;
                    break;
            }
        }

        // Align text/image within their regions.
        layout.ImageBounds = LayoutUtils.Align (image_size, image_bounds, image_align);
        layout.TextBounds = text_image_control.Multiline ? text_bounds : LayoutUtils.Align(text_size, text_bounds, text_align);
    }

    // Maps an image align to the set of TextImageRelations that represent the same edge.
    // For example, imageAlign = TopLeft maps to TextImageRelations ImageAboveText (top)
    // and ImageBeforeText (left).
    private static readonly TextImageRelation[] _imageAlignToRelation =
    [
                /* TopLeft = */       TextImageRelation.ImageAboveText | TextImageRelation.ImageBeforeText,
                /* TopCenter = */     TextImageRelation.ImageAboveText,
                /* TopRight = */      TextImageRelation.ImageAboveText | TextImageRelation.TextBeforeImage,
                /* Invalid */         0,
                /* MiddleLeft = */    TextImageRelation.ImageBeforeText,
                /* MiddleCenter = */  0,
                /* MiddleRight = */   TextImageRelation.TextBeforeImage,
                /* Invalid */         0,
                /* BottomLeft = */    TextImageRelation.TextAboveImage | TextImageRelation.ImageBeforeText,
                /* BottomCenter = */  TextImageRelation.TextAboveImage,
                /* BottomRight = */   TextImageRelation.TextAboveImage | TextImageRelation.TextBeforeImage
    ];

    private static TextImageRelation ImageAlignToRelation (ContentAlignment alignment)
        => _imageAlignToRelation[LayoutUtils.ContentAlignmentToIndex (alignment)];

    private static TextImageRelation TextAlignToRelation (ContentAlignment alignment)
        => LayoutUtils.GetOppositeTextImageRelation (ImageAlignToRelation (alignment));

    private static SKSize GetTextSize (Control control)
    {
        if (control.Text is null)
            return SKSize.Empty;

        // The renderer consumes CurrentStyle, including transient text metrics. Measuring
        // the persistent style can leave a growing single line with a too-short MaxHeight,
        // causing the shaper to omit it entirely even when the control has enough space.
        // Measure the natural extent before the image/text regions are allocated. An arbitrary
        // measurement width wraps long captions early and can trigger ellipsis in a fitting field.
        return TextMeasurer.MeasureText (control.Text, control);
    }
}

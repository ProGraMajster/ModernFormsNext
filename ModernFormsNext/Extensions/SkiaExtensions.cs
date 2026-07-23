using System;
using System.Drawing;
using System.Runtime.Versioning;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// A collection of extension methods to facilitate working with Skia.
    /// </summary>
    public static class SkiaExtensions
    {
        private static readonly SKColorFilter disabled_matrix = SKColorFilter.CreateColorMatrix (new float[]
                {
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0,     0,     0,     1, 0
                });

        /// <summary>
        /// Clips a canvas to the specified rectangle.
        /// </summary>
        public static void Clip (this SKCanvas canvas, Rectangle rectangle) => canvas.ClipRect (rectangle.ToSKRect ());

        /// <summary>
        /// Draws a control's background.
        /// </summary>
        public static void DrawBackground (this SKCanvas canvas, Rectangle bounds, ControlStyle style, Drawing.Brush? brush = null)
        {
            var radius = style.Border.GetRadius ();
            var borderWidth = style.Border.GetWidth ();
            var backgroundBounds = new SKRect (0, 0, bounds.Width - borderWidth, bounds.Height - borderWidth);

            if (brush is not null) {
                canvas.Clear (SKColors.Transparent);

                if (radius > 0) {
                    using var path = new SKPath ();
                    path.AddRoundRect (backgroundBounds, radius, radius);

                    canvas.Save ();
                    canvas.ClipPath (path, SKClipOperation.Intersect, true);

                    RenderBrushBackground (canvas, backgroundBounds, brush, style.GetBackgroundColor ());

                    canvas.Restore ();
                    return;
                }

                RenderBrushBackground (canvas, backgroundBounds, brush, style.GetBackgroundColor ());
                return;
            }

            if (radius > 0) {
                canvas.Clear (SKColors.Transparent);
                canvas.FillRoundedRectangle (
                    0,
                    0,
                    bounds.Width - borderWidth,
                    bounds.Height - borderWidth,
                    style.GetBackgroundColor (),
                    radius,
                    radius,
                    borderWidth);
                return;
            }

            canvas.Clear (style.GetBackgroundColor ());
        }

        private static void RenderGradient(SKCanvas canvas, SKRect bounds, GradientBrush brush, SKBlendMode blendMode)
        {
            GradientStop[] stops = brush.GetOrderedStops();
            if (stops.Length == 0 || brush.Opacity <= 0f)
                return;

            // A one-stop gradient is a solid fill. A zero-radius radial gradient also has no
            // spatial interval, so the final stop is the only well-defined area color.
            if (stops.Length == 1 || brush is RadialGradientBrush { Radius: 0f })
            {
                using var singlePaint = new SKPaint
                {
                    Color = SkiaBrushFactory.ApplyOpacity(stops[^1].Color, brush.Opacity),
                    IsAntialias = true,
                    BlendMode = blendMode
                };

                canvas.DrawRect(bounds, singlePaint);
                return;
            }

            using SKShader? shader = SkiaBrushFactory.CreateGradientShader(brush, bounds);
            if (shader is null)
                return;

            using var paint = new SKPaint
            {
                Shader = shader,
                IsAntialias = true,
                BlendMode = blendMode
            };

            canvas.DrawRect(bounds, paint);
        }

        private static void RenderGlassBackground (SKCanvas canvas, SKRect bounds, GlassBrush brush, SKBlendMode blendMode = SKBlendMode.SrcOver)
        {
            // Base translucent fill with a very subtle vertical depth gradient
            using (var shader = SkiaBrushFactory.ApplyTransform(
                SKShader.CreateLinearGradient (
                    new SKPoint (bounds.Left, bounds.Top),
                    new SKPoint (bounds.Left, bounds.Bottom),
                    new[] {
                        SkiaBrushFactory.ApplyOpacity(brush.HighlightColor, brush.Opacity),
                        SkiaBrushFactory.ApplyOpacity(brush.TintColor, brush.Opacity),
                        SkiaBrushFactory.ApplyOpacity(brush.SecondaryTintColor, brush.Opacity)
                    },
                    new[] { 0f, 0.28f, 1f },
                    SKShaderTileMode.Clamp),
                brush.Transform))
            using (var paint = new SKPaint {
                Shader = shader,
                IsAntialias = true,
                BlendMode = blendMode
            }) {
                canvas.DrawRect (bounds, paint);
            }

            // Soft highlight band at the top
            if (brush.ShowHighlight) {
                var highlightHeight = MathF.Max (8f, bounds.Height * 0.32f);

                SKColor highlightColor = SkiaBrushFactory.ApplyOpacity(brush.HighlightColor, brush.Opacity);
                using var highlightShader = SkiaBrushFactory.ApplyTransform(
                    SKShader.CreateLinearGradient (
                        new SKPoint (bounds.Left, bounds.Top),
                        new SKPoint (bounds.Left, bounds.Top + highlightHeight),
                        new[] {
                            highlightColor,
                            new SKColor (highlightColor.Red, highlightColor.Green, highlightColor.Blue, 0)
                        },
                        new[] { 0f, 1f },
                        SKShaderTileMode.Clamp),
                    brush.Transform);

                using var highlightPaint = new SKPaint {
                    Shader = highlightShader,
                    IsAntialias = true,
                    BlendMode = blendMode
                };

                canvas.DrawRect (
                    new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Top + highlightHeight),
                    highlightPaint);
            }

            // Outer border
            using (var borderPaint = new SKPaint {
                Color = SkiaBrushFactory.ApplyOpacity(brush.BorderColor, brush.Opacity),
                IsStroke = true,
                StrokeWidth = 1f,
                IsAntialias = true,
                BlendMode = blendMode
            }) {
                canvas.DrawRect (
                    bounds.Left + 0.5f,
                    bounds.Top + 0.5f,
                    bounds.Width - 1f,
                    bounds.Height - 1f,
                    borderPaint);
            }

            // Optional inner border for a more glass-like edge
            if (brush.ShowInnerBorder) {
                using var innerBorderPaint = new SKPaint {
                    Color = SkiaBrushFactory.ApplyOpacity(new SKColor (255, 255, 255, 20), brush.Opacity),
                    IsStroke = true,
                    StrokeWidth = 1f,
                    IsAntialias = true,
                    BlendMode = blendMode
                };

                canvas.DrawRect (
                    bounds.Left + 1.5f,
                    bounds.Top + 1.5f,
                    Math.Max (0, bounds.Width - 3f),
                    Math.Max (0, bounds.Height - 3f),
                    innerBorderPaint);
            }
        }

        internal static void RenderBrushBackground (SKCanvas canvas, SKRect bounds, Drawing.Brush? brush, SKColor fallbackColor, SKBlendMode blendMode = SKBlendMode.SrcOver)
        {
            if (brush is null) {
                using var fallbackPaint = new SKPaint {
                    Color = fallbackColor,
                    IsAntialias = true,
                    BlendMode = blendMode
                };

                canvas.DrawRect (bounds, fallbackPaint);
                return;
            }

            if (brush is NoBrush || brush.Opacity <= 0f)
                return;

            switch (brush) {
                case SolidColorBrush solid:
                    using (var paint = new SKPaint {
                        Color = SkiaBrushFactory.ApplyOpacity(solid.Color, solid.Opacity),
                        IsAntialias = true,
                        BlendMode = blendMode
                    })
                        canvas.DrawRect (bounds, paint);
                    break;

                case LinearGradientBrush linear:
                    RenderGradient (canvas, bounds, linear, blendMode);
                    break;

                case RadialGradientBrush radial:
                    RenderGradient (canvas, bounds, radial, blendMode);
                    break;
                case SweepGradientBrush sweep:
                    RenderGradient (canvas, bounds, sweep, blendMode);
                    break;

                case GlassBrush glass:
                    RenderGlassBackground (canvas, bounds, glass, blendMode);
                    break;
            }
        }

        /// <summary>
        /// Draws a bitmap.
        /// </summary>
        public static void DrawBitmap (this SKCanvas canvas, SKBitmap bitmap, Rectangle rect, bool disabled = false)
        {
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };

            if (disabled)
                paint.ColorFilter = disabled_matrix;

            canvas.DrawBitmap (bitmap, rect.ToSKRect (), paint);
        }

        /// <summary>
        /// Draws a bitmap.
        /// </summary>
        public static void DrawBitmap (this SKCanvas canvas, SKBitmap bitmap, float x, float y, bool disabled = false)
        {
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };

            if (disabled)
                paint.ColorFilter = disabled_matrix;

            canvas.DrawBitmap (bitmap, x, y, paint);
        }

        /// <summary>
        /// Draws a control's border.
        /// </summary>
        public static void DrawBorder (
            this SKCanvas canvas,
            Rectangle bounds,
            ControlStyle style,
            Drawing.Brush? brush = null)
        {
            if (brush is not null) {
                DrawBrushBorder (canvas, bounds, style, brush);
                return;
            }

            // If using border radius, currently all border sides are drawn, and all are the same color
            var radius = style.Border.GetRadius ();

            if (radius > 0) {
                canvas.DrawRoundedRectangle (0, 0, bounds.Width - style.Border.GetWidth (), bounds.Height - style.Border.GetWidth (), style.Border.GetColor (), radius, radius, style.Border.GetWidth ());
                return;
            }

            // Left Border
            if (style.Border.Left.GetWidth () > 0) {
                var left_offset = style.Border.Left.GetWidth () / 2f;
                canvas.DrawLine (left_offset, 0, left_offset, bounds.Height, style.Border.Left.GetColor (), style.Border.Left.GetWidth ());
            }

            // Right Border
            if (style.Border.Right.GetWidth () > 0) {
                var right_offset = style.Border.Right.GetWidth () / 2f;
                canvas.DrawLine (bounds.Width - right_offset, 0, bounds.Width - right_offset, bounds.Height, style.Border.Right.GetColor (), style.Border.Right.GetWidth ());
            }

            // Top Border
            if (style.Border.Top.GetWidth () > 0) {
                var top_offset = style.Border.Top.GetWidth () / 2f;
                canvas.DrawLine (0, top_offset, bounds.Width, top_offset, style.Border.Top.GetColor (), style.Border.Top.GetWidth ());
            }

            // Bottom Border
            if (style.Border.Bottom.GetWidth () > 0) {
                var bottom_offset = style.Border.Bottom.GetWidth () / 2f;
                canvas.DrawLine (0, bounds.Height - bottom_offset, bounds.Width, bounds.Height - bottom_offset, style.Border.Bottom.GetColor (), style.Border.Bottom.GetWidth ());
            }
        }

        private static void DrawBrushBorder (
            SKCanvas canvas,
            Rectangle bounds,
            ControlStyle style,
            Drawing.Brush brush)
        {
            if (brush is NoBrush || brush.Opacity <= 0f)
                return;

            var paint = new SKPaint {
                IsAntialias = true,
                IsStroke = true,
                Color = style.Border.GetColor ()
            };
            SKShader? shader = null;
            try {
                if (brush is SolidColorBrush solid)
                    paint.Color = SkiaBrushFactory.ApplyOpacity (solid.Color, solid.Opacity);
                else if (brush is GradientBrush gradient) {
                    shader = SkiaBrushFactory.CreateGradientShader (
                        gradient,
                        new SKRect (0, 0, bounds.Width, bounds.Height));
                    paint.Shader = shader;
                }

                var radius = style.Border.GetRadius ();
                if (radius > 0) {
                    paint.StrokeWidth = style.Border.GetWidth ();
                    canvas.DrawRoundRect (
                        new SKRect (
                            paint.StrokeWidth / 2f,
                            paint.StrokeWidth / 2f,
                            bounds.Width - (paint.StrokeWidth / 2f),
                            bounds.Height - (paint.StrokeWidth / 2f)),
                        radius,
                        radius,
                        paint);
                    return;
                }

                DrawBrushBorderSide (canvas, paint, style.Border.Left.GetWidth (),
                    static (target, offset, _, height, value) => target.DrawLine (offset, 0, offset, height, value),
                    bounds.Width, bounds.Height);
                DrawBrushBorderSide (canvas, paint, style.Border.Right.GetWidth (),
                    static (target, offset, width, height, value) => target.DrawLine (width - offset, 0, width - offset, height, value),
                    bounds.Width, bounds.Height);
                DrawBrushBorderSide (canvas, paint, style.Border.Top.GetWidth (),
                    static (target, offset, width, _, value) => target.DrawLine (0, offset, width, offset, value),
                    bounds.Width, bounds.Height);
                DrawBrushBorderSide (canvas, paint, style.Border.Bottom.GetWidth (),
                    static (target, offset, width, height, value) => target.DrawLine (0, height - offset, width, height - offset, value),
                    bounds.Width, bounds.Height);
            } finally {
                shader?.Dispose ();
                paint.Dispose ();
            }
        }

        private static void DrawBrushBorderSide (
            SKCanvas canvas,
            SKPaint paint,
            int width,
            Action<SKCanvas, float, float, float, SKPaint> draw,
            float boundsWidth,
            float boundsHeight)
        {
            if (width <= 0)
                return;
            paint.StrokeWidth = width;
            draw (canvas, width / 2f, boundsWidth, boundsHeight, paint);
        }

        /// <summary>
        /// Draws an unfilled circle.
        /// </summary>
        public static void DrawCircle (this SKCanvas canvas, int x, int y, int radius, SKColor color, int strokeWidth = 1)
        {
            using var paint = new SKPaint { Color = color, IsStroke = true, StrokeWidth = strokeWidth, IsAntialias = true };

            canvas.DrawCircle (x, y, radius, paint);
        }

        /// <summary>
        /// Draws a focus rectangle.
        /// </summary>
        public static void DrawFocusRectangle (this SKCanvas canvas, int x, int y, int width, int height, int inset = 0)
        {
            // Draw a white rectangle
            canvas.DrawRectangle (x + inset, y + inset, width - (2 * inset) - 1, height - (2 * inset) - 1, SKColors.White);

            // Draw a black dashed rectangle on top of it
            var effect = SKPathEffect.CreateDash (new[] { 1f, 1f }, 0);
            using var paint = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1, PathEffect = effect };

            canvas.DrawRect (x + inset, y + inset, width - (2 * inset) - 1, height - (2 * inset) - 1, paint);
        }

        /// <summary>
        /// Draws an focus rectangle.
        /// </summary>
        public static void DrawFocusRectangle (this SKCanvas canvas, Rectangle rectangle, int inset = 0)
            => DrawFocusRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, inset);

        /// <summary>
        /// Draws a line.
        /// </summary>
        public static void DrawLine (this SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, int thickness = 1)
        {
            using var paint = new SKPaint { Color = color, StrokeWidth = thickness };

            canvas.DrawLine (x1, y1, x2, y2, paint);
        }

        /// <summary>
        /// Draws a path.
        /// </summary>
        public static void DrawPath(this SKCanvas canvas, SKPath path, SKColor color, int thickness = 1)
        {
            using var paint = new SKPaint { Color = color, StrokeWidth = thickness, IsStroke = true };

            canvas.DrawPath(path, paint);
        }

        /// <summary>
        /// Draws an unfilled rectangle.
        /// </summary>
        public static void DrawRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int strokeWidth = 1)
        {
            using var paint = new SKPaint { Color = color, IsStroke = true, StrokeWidth = strokeWidth };

            // canvas.DrawRect (x, y, width, height, paint);

            // Inset by half the stroke width so the stroke is fully inside the specified bounds.
            // In Skia's coordinate system, pixel (i,j) occupies [i,i+1)x[j,j+1), so a centered
            // stroke at an integer coordinate straddles a pixel edge and can bleed outside the
            // buffer at fractional DPI scales (e.g. 150%).
            var half = strokeWidth * 0.5f;

            // When the requested rectangle is thinner than the stroke width, subtracting the
            // stroke width from the dimensions can produce zero or negative sizes, which Skia
            // will not render. In those cases, approximate the rectangle as a line or point
            // so thin glyphs (e.g. text carets) still appear.
            if (width <= strokeWidth && height <= strokeWidth)
            {
                // Degenerate case: both dimensions are very small, render a single point.
                canvas.DrawPoint(x + half, y + half, paint);
            }
            else if (width <= strokeWidth)
            {
                // Very thin vertical rectangle: draw a vertical line centered in the bounds.
                canvas.DrawLine(x + half, y + half, x + half, y + height - half, paint);
            }
            else if (height <= strokeWidth)
            {
                // Very thin horizontal rectangle: draw a horizontal line centered in the bounds.
                canvas.DrawLine(x + half, y + half, x + width - half, y + half, paint);
            }
            else
            {
                // Normal rectangle: inset by the stroke width so the stroke stays inside bounds.
                canvas.DrawRect(x + half, y + half, width - strokeWidth, height - strokeWidth, paint);
            }
        }

        /// <summary>
        /// Draws an unfilled rectangle.
        /// </summary>
        public static void DrawRectangle (this SKCanvas canvas, Rectangle rectangle, SKColor color, int strokeWidth = 1)
            => DrawRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, strokeWidth);

        /// <summary>
        /// Draws an unfilled rectangle with rounded corners.
        /// </summary>
        public static void DrawRoundedRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int rx = 3, int ry = 3, float strokeWidth = 1)
        {
            using var paint = new SKPaint {
                Color = color,
                IsStroke = true,
                IsAntialias = true,
                StrokeWidth = strokeWidth
            };

            //canvas.DrawRoundRect (x + .5f, y + .5f, width, height, rx, ry, paint);
            // Inset by half the stroke width so the stroke is fully inside the specified bounds.
            // In Skia's coordinate system, pixel (i,j) occupies [i,i+1)x[j,j+1), so a centered
            // stroke at an integer coordinate straddles a pixel edge and can bleed outside the
            // buffer at fractional DPI scales (e.g. 150%).
            var half = strokeWidth * 0.5f;
            var adjustedWidth = Math.Max(0, width - strokeWidth);
            var adjustedHeight = Math.Max(0, height - strokeWidth);
            canvas.DrawRoundRect(x + half, y + half, adjustedWidth, adjustedHeight, rx, ry, paint);
        }

        /// <summary>
        /// Draws a filled circle.
        /// </summary>
        public static void FillCircle (this SKCanvas canvas, int x, int y, int radius, SKColor color)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };

            canvas.DrawCircle (x, y, radius, paint);
        }

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        public static void FillRectangle (this SKCanvas canvas, Rectangle rectangle, SKColor color)
            => FillRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        public static void FillRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color)
        {
            using var paint = new SKPaint { Color = color };

            canvas.DrawRect (x, y, width, height, paint);
        }

        /// <summary>
        /// Draws a filled rectangle with rounded corners.
        /// </summary>
        public static void FillRoundedRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int rx = 3, int ry = 3, float strokeWidth = 1)
        {
            using var paint = new SKPaint {
                Color = color,
                IsStroke = false,
                IsAntialias = true,
                StrokeWidth = strokeWidth
            };
            var r = new SKRoundRect ();

            canvas.DrawRoundRect (x + .5f, y + .5f, width, height, rx, ry, paint);
        }

        /// <summary>
        /// Gets the size of the specified bitmap.
        /// </summary>
        public static Size GetSize (this SKBitmap bitmap) => new Size (bitmap.Width, bitmap.Height);

        /// <summary>
        /// Convers an SKImage to a Bitmap.
        /// </summary>
        [SupportedOSPlatform ("windows")]
        public static Bitmap ToBitmap (this SKImage skiaImage)
        {
            // TODO: maybe keep the same color types where we can, instead of just going to the platform default
            var bitmap = new Bitmap (skiaImage.Width, skiaImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits (new Rectangle (0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);

            // copy
            using (var pixmap = new SKPixmap (new SKImageInfo (data.Width, data.Height), data.Scan0, data.Stride))
                skiaImage.ReadPixels (pixmap, 0, 0);

            bitmap.UnlockBits (data);
            return bitmap;
        }

        /// <summary>
        /// Convers an SKBitmap to a Bitmap.
        /// </summary>
        [SupportedOSPlatform ("windows")]
        public static Bitmap ToBitmap (this SKBitmap skiaBitmap)
        {
            using (var image = SKImage.FromPixels (skiaBitmap.PeekPixels ()))
                return ToBitmap (image);
        }

        /// <summary>
        /// Converts an SKRect to a Rectangle.
        /// </summary>
        public static Rectangle ToRectangle (this SKRect rect) => new Rectangle ((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);

        /// <summary>
        /// Converts an SKSize to a Size.
        /// </summary>
        public static Size ToSize (this SKSize size) => new Size ((int)size.Width, (int)size.Height);

        /// <summary>
        /// Converts a Rectangle to an SKRect.
        /// </summary>
        public static SKRect ToSKRect (this Rectangle rect) => new SKRect (rect.X, rect.Y, rect.Right, rect.Bottom);

        /// <summary>
        /// Converts a Size to an SKSize.
        /// </summary>
        public static SKSize ToSKSize (this Size size) => new SKSize (size.Width, size.Height);
    }
}

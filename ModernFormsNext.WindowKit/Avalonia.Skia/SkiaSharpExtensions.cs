using System;
using System.Diagnostics.CodeAnalysis;
//using ModernFormsNext.WindowKit.Media;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Media.Imaging;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Skia
{
    /// <summary>
    /// Provides conversions between ModernFormsNext drawing primitives and SkiaSharp primitives.
    /// </summary>
    public static class SkiaSharpExtensions
    {
        /// <summary>
        /// Converts a bitmap interpolation mode to a SkiaSharp filter quality value.
        /// </summary>
        /// <param name="interpolationMode">The interpolation mode to convert.</param>
        /// <returns>The matching SkiaSharp filter quality.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="interpolationMode"/> is not recognized.
        /// </exception>
        public static SKFilterQuality ToSKFilterQuality(this BitmapInterpolationMode interpolationMode)
        {
            switch (interpolationMode)
            {
                case BitmapInterpolationMode.Unspecified:
                case BitmapInterpolationMode.LowQuality:
                    return SKFilterQuality.Low;
                case BitmapInterpolationMode.MediumQuality:
                    return SKFilterQuality.Medium;
                case BitmapInterpolationMode.HighQuality:
                    return SKFilterQuality.High;
                case BitmapInterpolationMode.None:
                    return SKFilterQuality.None;
                default:
                    throw new ArgumentOutOfRangeException(nameof(interpolationMode), interpolationMode, null);
            }
        }

        //public static SKBlendMode ToSKBlendMode(this BitmapBlendingMode blendingMode)
        //{
        //    switch (blendingMode)
        //    {
        //        case BitmapBlendingMode.Unspecified:
        //        case BitmapBlendingMode.SourceOver:
        //            return SKBlendMode.SrcOver;
        //        case BitmapBlendingMode.Source:
        //            return SKBlendMode.Src;
        //        case BitmapBlendingMode.SourceIn:
        //            return SKBlendMode.SrcIn;
        //        case BitmapBlendingMode.SourceOut:
        //            return SKBlendMode.SrcOut;
        //        case BitmapBlendingMode.SourceAtop:
        //            return SKBlendMode.SrcATop;
        //        case BitmapBlendingMode.Destination:
        //            return SKBlendMode.Dst;
        //        case BitmapBlendingMode.DestinationIn:
        //            return SKBlendMode.DstIn;
        //        case BitmapBlendingMode.DestinationOut:
        //            return SKBlendMode.DstOut;
        //        case BitmapBlendingMode.DestinationOver:
        //            return SKBlendMode.DstOver;
        //        case BitmapBlendingMode.DestinationAtop:
        //            return SKBlendMode.DstATop;
        //        case BitmapBlendingMode.Xor:
        //            return SKBlendMode.Xor;
        //        case BitmapBlendingMode.Plus:
        //            return SKBlendMode.Plus;
        //        default:
        //            throw new ArgumentOutOfRangeException(nameof(blendingMode), blendingMode, null);
        //    }
        //}

        /// <summary>
        /// Converts a logical point to a SkiaSharp point.
        /// </summary>
        /// <param name="p">The point to convert.</param>
        /// <returns>The converted SkiaSharp point.</returns>
        public static SKPoint ToSKPoint(this Point p)
        {
            return new SKPoint((float)p.X, (float)p.Y);
        }
        
        /// <summary>
        /// Converts a vector to a SkiaSharp point.
        /// </summary>
        /// <param name="p">The vector to convert.</param>
        /// <returns>The converted SkiaSharp point.</returns>
        public static SKPoint ToSKPoint(this Vector p)
        {
            return new SKPoint((float)p.X, (float)p.Y);
        }

        /// <summary>
        /// Converts a logical rectangle to a SkiaSharp rectangle.
        /// </summary>
        /// <param name="r">The rectangle to convert.</param>
        /// <returns>The converted SkiaSharp rectangle.</returns>
        public static SKRect ToSKRect(this Rect r)
        {
            return new SKRect((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
        }

        //public static SKRoundRect ToSKRoundRect(this RoundedRect r)
        //{
        //    var rc = r.Rect.ToSKRect();
        //    var result = new SKRoundRect();

        //    result.SetRectRadii(rc,
        //           new[]
        //           {
        //                r.RadiiTopLeft.ToSKPoint(), r.RadiiTopRight.ToSKPoint(),
        //                r.RadiiBottomRight.ToSKPoint(), r.RadiiBottomLeft.ToSKPoint(),
        //           });            

        //    return result;
        //}

        /// <summary>
        /// Converts a SkiaSharp rectangle to a ModernFormsNext rectangle.
        /// </summary>
        /// <param name="r">The SkiaSharp rectangle to convert.</param>
        /// <returns>The converted ModernFormsNext rectangle.</returns>
        public static Rect ToAvaloniaRect(this SKRect r)
        {
            return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }

        //public static SKMatrix ToSKMatrix(this Matrix m)
        //{
        //    var sm = new SKMatrix
        //    {
        //        ScaleX = (float)m.M11,
        //        SkewX = (float)m.M21,
        //        TransX = (float)m.M31,
        //        SkewY = (float)m.M12,
        //        ScaleY = (float)m.M22,
        //        TransY = (float)m.M32,
        //        Persp0 = (float)m.M13,
        //        Persp1 = (float)m.M23,
        //        Persp2 = (float)m.M33
        //    };

        //    return sm;
        //}

        //public static SKColor ToSKColor(this Color c)
        //{
        //    return new SKColor(c.R, c.G, c.B, c.A);
        //}

        /// <summary>
        /// Converts a ModernFormsNext pixel format to a SkiaSharp color type.
        /// </summary>
        /// <param name="fmt">The pixel format to convert.</param>
        /// <returns>The matching SkiaSharp color type.</returns>
        /// <exception cref="ArgumentException">Thrown when the pixel format is not supported by this conversion.</exception>
        public static SKColorType ToSkColorType(this PixelFormat fmt)
        {
            if (fmt == PixelFormat.Rgb565)
                return SKColorType.Rgb565;
            if (fmt == PixelFormat.Bgra8888)
                return SKColorType.Bgra8888;
            if (fmt == PixelFormat.Rgba8888)
                return SKColorType.Rgba8888;
            throw new ArgumentException("Unknown pixel format: " + fmt);
        }

        /// <summary>
        /// Converts a SkiaSharp color type to a ModernFormsNext pixel format when supported.
        /// </summary>
        /// <param name="colorType">The SkiaSharp color type to convert.</param>
        /// <returns>The matching pixel format, or <see langword="null"/> when no mapping exists.</returns>
        public static PixelFormat? ToAvalonia(this SKColorType colorType)
        {
            if (colorType == SKColorType.Rgb565)
                return PixelFormats.Rgb565;
            if (colorType == SKColorType.Bgra8888)
                return PixelFormats.Bgra8888;
            if (colorType == SKColorType.Rgba8888)
                return PixelFormats.Rgba8888;
            return null;
        }

        /// <summary>
        /// Converts a SkiaSharp color type to a ModernFormsNext pixel format.
        /// </summary>
        /// <param name="fmt">The SkiaSharp color type to convert.</param>
        /// <returns>The matching pixel format.</returns>
        /// <exception cref="ArgumentException">Thrown when the color type is not supported by this conversion.</exception>
        public static PixelFormat ToPixelFormat(this SKColorType fmt)
        {
            if (fmt == SKColorType.Rgb565)
                return PixelFormat.Rgb565;
            if (fmt == SKColorType.Bgra8888)
                return PixelFormat.Bgra8888;
            if (fmt == SKColorType.Rgba8888)
                return PixelFormat.Rgba8888;
            throw new ArgumentException("Unknown pixel format: " + fmt);
        }

        /// <summary>
        /// Converts an alpha format to a SkiaSharp alpha type.
        /// </summary>
        /// <param name="fmt">The alpha format to convert.</param>
        /// <returns>The matching SkiaSharp alpha type.</returns>
        /// <exception cref="ArgumentException">Thrown when the alpha format is not recognized.</exception>
        public static SKAlphaType ToSkAlphaType(this AlphaFormat fmt)
        {
            return fmt switch
            {
                AlphaFormat.Premul => SKAlphaType.Premul,
                AlphaFormat.Unpremul => SKAlphaType.Unpremul,
                AlphaFormat.Opaque => SKAlphaType.Opaque,
                _ => throw new ArgumentException($"Unknown alpha format: {fmt}")
            };
        }

        //public static AlphaFormat ToAlphaFormat(this SKAlphaType fmt)
        //{
        //    return fmt switch
        //    {
        //        SKAlphaType.Premul => AlphaFormat.Premul,
        //        SKAlphaType.Unpremul => AlphaFormat.Unpremul,
        //        SKAlphaType.Opaque => AlphaFormat.Opaque,
        //        _ => throw new ArgumentException($"Unknown alpha format: {fmt}")
        //    };
        //}

        //public static SKShaderTileMode ToSKShaderTileMode(this GradientSpreadMethod m)
        //{
        //    switch (m)
        //    {
        //        default:
        //        case GradientSpreadMethod.Pad: return SKShaderTileMode.Clamp;
        //        case GradientSpreadMethod.Reflect: return SKShaderTileMode.Mirror;
        //        case GradientSpreadMethod.Repeat: return SKShaderTileMode.Repeat;
        //    }
        //}

        //public static SKTextAlign ToSKTextAlign(this TextAlignment a)
        //{
        //    switch (a)
        //    {
        //        default:
        //        case TextAlignment.Left: return SKTextAlign.Left;
        //        case TextAlignment.Center: return SKTextAlign.Center;
        //        case TextAlignment.Right: return SKTextAlign.Right;
        //    }
        //}

        //public static TextAlignment ToAvalonia(this SKTextAlign a)
        //{
        //    switch (a)
        //    {
        //        default:
        //        case SKTextAlign.Left: return TextAlignment.Left;
        //        case SKTextAlign.Center: return TextAlignment.Center;
        //        case SKTextAlign.Right: return TextAlignment.Right;
        //    }
        //}

        //public static FontStyle ToAvalonia(this SKFontStyleSlant slant)
        //{
        //    return slant switch
        //    {
        //        SKFontStyleSlant.Upright => FontStyle.Normal,
        //        SKFontStyleSlant.Italic => FontStyle.Italic,
        //        SKFontStyleSlant.Oblique => FontStyle.Oblique,
        //        _ => throw new ArgumentOutOfRangeException(nameof (slant), slant, null)
        //    };
        //}

        //[return: NotNullIfNotNull(nameof(src))]
        /// <summary>
        /// Creates a copy of a SkiaSharp path.
        /// </summary>
        /// <param name="src">The path to clone.</param>
        /// <returns>A cloned path, or <see langword="null"/> when <paramref name="src"/> is <see langword="null"/>.</returns>
        public static SKPath? Clone(this SKPath? src)
        {
            return src != null ? new SKPath(src) : null;
        }
    }
}

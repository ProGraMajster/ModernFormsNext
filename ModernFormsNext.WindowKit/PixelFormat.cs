using System;

namespace ModernFormsNext.WindowKit.Platform
{
    internal enum PixelFormatEnum
    {
        Rgb565,
        Rgba8888,
        Bgra8888,
        BlackWhite,
        Gray2,
        Gray4,
        Gray8,
        Gray16,
        Gray32Float,
        Rgba64,
        Rgb24,
        Bgr24
    }

    /// <summary>
    /// Identifies the channel layout used by bitmap and framebuffer pixel data.
    /// </summary>
    public record struct PixelFormat
    {
        internal PixelFormatEnum FormatEnum;

        /// <summary>
        /// Gets the number of bits used by one pixel in this format.
        /// </summary>
        public int BitsPerPixel
        {
            get
            {
                if (FormatEnum == PixelFormatEnum.BlackWhite)
                    return 1;
                else if (FormatEnum == PixelFormatEnum.Gray2)
                    return 2;
                else if (FormatEnum == PixelFormatEnum.Gray4)
                    return 4;
                else if (FormatEnum == PixelFormatEnum.Gray8)
                    return 8;
                else if (FormatEnum == PixelFormatEnum.Rgb565 
                         || FormatEnum == PixelFormatEnum.Gray16)
                    return 16;
                else if (FormatEnum is PixelFormatEnum.Bgr24 or PixelFormatEnum.Rgb24)
                    return 24;
                else if (FormatEnum == PixelFormatEnum.Rgba64)
                    return 64;

                return 32;
            }
        }

        internal bool HasAlpha => FormatEnum == PixelFormatEnum.Rgba8888 
                                  || FormatEnum == PixelFormatEnum.Bgra8888
                                  || FormatEnum == PixelFormatEnum.Rgba64;

        internal PixelFormat(PixelFormatEnum format)
        {
            FormatEnum = format;
        }

        /// <summary>
        /// Gets the 16-bit RGB 565 pixel format.
        /// </summary>
        public static PixelFormat Rgb565 => PixelFormats.Rgb565;

        /// <summary>
        /// Gets the 32-bit RGBA pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Rgba8888 => PixelFormats.Rgba8888;

        /// <summary>
        /// Gets the 32-bit BGRA pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Bgra8888 => PixelFormats.Bgra8888;
        
        /// <summary>
        /// Returns the platform-neutral pixel format name.
        /// </summary>
        /// <returns>The pixel format name.</returns>
        public override string ToString() => FormatEnum.ToString();
    }

    /// <summary>
    /// Provides shared instances for supported pixel formats.
    /// </summary>
    public static class PixelFormats
    {
        /// <summary>
        /// Gets the 16-bit RGB 565 pixel format.
        /// </summary>
        public static PixelFormat Rgb565 { get; } = new PixelFormat(PixelFormatEnum.Rgb565);

        /// <summary>
        /// Gets the 32-bit RGBA pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Rgba8888 { get; } = new PixelFormat(PixelFormatEnum.Rgba8888);

        /// <summary>
        /// Gets the 64-bit RGBA pixel format with 16 bits per channel.
        /// </summary>
        public static PixelFormat Rgba64 { get; } = new PixelFormat(PixelFormatEnum.Rgba64);

        /// <summary>
        /// Gets the 32-bit BGRA pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Bgra8888 { get; } = new PixelFormat(PixelFormatEnum.Bgra8888);

        /// <summary>
        /// Gets the 1-bit black-and-white pixel format.
        /// </summary>
        public static PixelFormat BlackWhite { get; } = new PixelFormat(PixelFormatEnum.BlackWhite);

        /// <summary>
        /// Gets the 2-bit grayscale pixel format.
        /// </summary>
        public static PixelFormat Gray2 { get; } = new PixelFormat(PixelFormatEnum.Gray2);

        /// <summary>
        /// Gets the 4-bit grayscale pixel format.
        /// </summary>
        public static PixelFormat Gray4 { get; } = new PixelFormat(PixelFormatEnum.Gray4);

        /// <summary>
        /// Gets the 8-bit grayscale pixel format.
        /// </summary>
        public static PixelFormat Gray8 { get; } = new PixelFormat(PixelFormatEnum.Gray8);

        /// <summary>
        /// Gets the 16-bit grayscale pixel format.
        /// </summary>
        public static PixelFormat Gray16 { get; } = new PixelFormat(PixelFormatEnum.Gray16);

        /// <summary>
        /// Gets the 32-bit floating-point grayscale pixel format.
        /// </summary>
        public static PixelFormat Gray32Float { get; } = new PixelFormat(PixelFormatEnum.Gray32Float);

        /// <summary>
        /// Gets the 24-bit RGB pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Rgb24 { get; } = new PixelFormat(PixelFormatEnum.Rgb24);

        /// <summary>
        /// Gets the 24-bit BGR pixel format with 8 bits per channel.
        /// </summary>
        public static PixelFormat Bgr24 { get; } = new PixelFormat(PixelFormatEnum.Bgr24);
    }
}

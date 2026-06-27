using System;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Describes a paper size in hundredths of an inch.
    /// </summary>
    public class PaperSize
    {
        private int width;
        private int height;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaperSize"/> class using Letter paper.
        /// </summary>
        public PaperSize()
            : this("Letter", 850, 1100)
        {
            Kind = PaperKind.Letter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaperSize"/> class.
        /// </summary>
        /// <param name="name">The display name of the paper size.</param>
        /// <param name="width">The width in hundredths of an inch.</param>
        /// <param name="height">The height in hundredths of an inch.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than one.</exception>
        public PaperSize(string name, int width, int height)
        {
            PaperName = name ?? string.Empty;
            Width = width;
            Height = height;
            Kind = PaperKind.Custom;
        }

        /// <summary>
        /// Gets or sets the standard paper kind.
        /// </summary>
        public PaperKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the paper display name.
        /// </summary>
        public string PaperName { get; set; }

        /// <summary>
        /// Gets or sets the paper width in hundredths of an inch.
        /// </summary>
        public int Width {
            get => width;
            set {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
                width = value;
            }
        }

        /// <summary>
        /// Gets or sets the paper height in hundredths of an inch.
        /// </summary>
        public int Height {
            get => height;
            set {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
                height = value;
            }
        }

        /// <summary>
        /// Creates a new <see cref="PaperSize"/> for the specified standard kind.
        /// </summary>
        /// <param name="kind">The standard paper kind.</param>
        /// <returns>A paper size instance with dimensions in hundredths of an inch.</returns>
        public static PaperSize FromKind(PaperKind kind)
        {
            return kind switch
            {
                PaperKind.Legal => new PaperSize("Legal", 850, 1400) { Kind = PaperKind.Legal },
                PaperKind.A4 => new PaperSize("A4", 827, 1169) { Kind = PaperKind.A4 },
                PaperKind.A5 => new PaperSize("A5", 583, 827) { Kind = PaperKind.A5 },
                _ => new PaperSize("Letter", 850, 1100) { Kind = PaperKind.Letter }
            };
        }

        internal Rectangle GetBounds(bool landscape)
        {
            return landscape
                ? new Rectangle(0, 0, Height, Width)
                : new Rectangle(0, 0, Width, Height);
        }

        /// <inheritdoc/>
        public override string ToString() => PaperName;
    }
}

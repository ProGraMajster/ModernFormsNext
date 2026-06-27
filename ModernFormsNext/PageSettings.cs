using System;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Contains page settings used by a <see cref="PrintDocument"/>.
    /// </summary>
    /// <remarks>
    /// Dimensions and margins are expressed in hundredths of an inch to match WinForms-style
    /// printing APIs. The current implementation is platform-neutral and can be used for
    /// preview rendering on every backend that supports ModernFormsNext rendering.
    /// </remarks>
    public class PageSettings : ICloneable
    {
        private Margins margins = new ();
        private PaperSize paperSize = new ();
        private PaperSource paperSource = new ();
        private PrinterResolution printerResolution = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="PageSettings"/> class.
        /// </summary>
        public PageSettings()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageSettings"/> class for the specified printer settings.
        /// </summary>
        /// <param name="printerSettings">The printer settings associated with the page.</param>
        public PageSettings(PrinterSettings printerSettings)
        {
            PrinterSettings = printerSettings;
        }

        /// <summary>
        /// Gets the full page bounds in hundredths of an inch.
        /// </summary>
        public Rectangle Bounds => paperSize.GetBounds(Landscape);

        /// <summary>
        /// Gets or sets a value indicating whether color output is requested.
        /// </summary>
        public bool Color { get; set; } = true;

        /// <summary>
        /// Gets the hard margin from the left edge in hundredths of an inch.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not yet query physical printer hard margins, so this value is currently <c>0</c>.
        /// </remarks>
        public float HardMarginX => 0;

        /// <summary>
        /// Gets the hard margin from the top edge in hundredths of an inch.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not yet query physical printer hard margins, so this value is currently <c>0</c>.
        /// </remarks>
        public float HardMarginY => 0;

        /// <summary>
        /// Gets or sets a value indicating whether the page is landscape.
        /// </summary>
        public bool Landscape { get; set; }

        /// <summary>
        /// Gets or sets the page margins in hundredths of an inch.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public Margins Margins {
            get => margins;
            set {
                ArgumentNullException.ThrowIfNull(value);
                margins = value;
            }
        }

        /// <summary>
        /// Gets or sets the selected paper size.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PaperSize PaperSize {
            get => paperSize;
            set {
                ArgumentNullException.ThrowIfNull(value);
                paperSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the selected paper source.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PaperSource PaperSource {
            get => paperSource;
            set {
                ArgumentNullException.ThrowIfNull(value);
                paperSource = value;
            }
        }

        /// <summary>
        /// Gets or sets the selected printer resolution.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PrinterResolution PrinterResolution {
            get => printerResolution;
            set {
                ArgumentNullException.ThrowIfNull(value);
                printerResolution = value;
            }
        }

        /// <summary>
        /// Gets or sets the printer settings associated with this page.
        /// </summary>
        public PrinterSettings? PrinterSettings { get; set; }

        /// <summary>
        /// Creates a copy of this page settings object.
        /// </summary>
        /// <returns>A new <see cref="PageSettings"/> with copied page values.</returns>
        public object Clone()
        {
            return new PageSettings(PrinterSettings ?? new PrinterSettings())
            {
                Color = Color,
                Landscape = Landscape,
                Margins = (Margins)Margins.Clone(),
                PaperSize = new PaperSize(PaperSize.PaperName, PaperSize.Width, PaperSize.Height) { Kind = PaperSize.Kind },
                PaperSource = new PaperSource { Kind = PaperSource.Kind, SourceName = PaperSource.SourceName },
                PrinterResolution = new PrinterResolution { Kind = PrinterResolution.Kind, X = PrinterResolution.X, Y = PrinterResolution.Y }
            };
        }

        /// <inheritdoc/>
        public override string ToString() => $"[PageSettings Bounds={Bounds}, Landscape={Landscape}, Margins={Margins}]";
    }
}

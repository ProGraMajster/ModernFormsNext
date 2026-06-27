namespace ModernFormsNext
{
    /// <summary>
    /// Describes the printer resolution selected for a print job.
    /// </summary>
    public class PrinterResolution
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrinterResolution"/> class using high quality.
        /// </summary>
        public PrinterResolution()
        {
            Kind = PrinterResolutionKind.High;
            X = 600;
            Y = 600;
        }

        /// <summary>
        /// Gets or sets the resolution kind.
        /// </summary>
        public PrinterResolutionKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the horizontal dots per inch for custom resolutions.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the vertical dots per inch for custom resolutions.
        /// </summary>
        public int Y { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Kind == PrinterResolutionKind.Custom ? $"{X}x{Y}" : Kind.ToString();
    }
}

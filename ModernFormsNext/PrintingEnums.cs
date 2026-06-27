namespace ModernFormsNext
{
    /// <summary>
    /// Specifies which pages of a document should be printed.
    /// </summary>
    public enum PrintRange
    {
        /// <summary>
        /// Print every page in the document.
        /// </summary>
        AllPages,

        /// <summary>
        /// Print the current selection.
        /// </summary>
        Selection,

        /// <summary>
        /// Print the page range specified by <see cref="PrinterSettings.FromPage"/> and <see cref="PrinterSettings.ToPage"/>.
        /// </summary>
        SomePages,

        /// <summary>
        /// Print the current page.
        /// </summary>
        CurrentPage
    }

    /// <summary>
    /// Specifies duplex printing behavior.
    /// </summary>
    public enum Duplex
    {
        /// <summary>
        /// Use the printer default duplex setting.
        /// </summary>
        Default = -1,

        /// <summary>
        /// Print on one side of each sheet.
        /// </summary>
        Simplex = 1,

        /// <summary>
        /// Print on both sides and flip along the short edge.
        /// </summary>
        Horizontal = 3,

        /// <summary>
        /// Print on both sides and flip along the long edge.
        /// </summary>
        Vertical = 2
    }

    /// <summary>
    /// Specifies a standard paper size.
    /// </summary>
    public enum PaperKind
    {
        /// <summary>
        /// A custom paper size.
        /// </summary>
        Custom = 0,

        /// <summary>
        /// Letter paper, 8.5 by 11 inches.
        /// </summary>
        Letter = 1,

        /// <summary>
        /// Legal paper, 8.5 by 14 inches.
        /// </summary>
        Legal = 5,

        /// <summary>
        /// A4 paper, 210 by 297 millimeters.
        /// </summary>
        A4 = 9,

        /// <summary>
        /// A5 paper, 148 by 210 millimeters.
        /// </summary>
        A5 = 11
    }

    /// <summary>
    /// Specifies the paper source tray kind.
    /// </summary>
    public enum PaperSourceKind
    {
        /// <summary>
        /// A custom paper source.
        /// </summary>
        Custom = 0,

        /// <summary>
        /// The upper paper tray.
        /// </summary>
        Upper = 1,

        /// <summary>
        /// The lower paper tray.
        /// </summary>
        Lower = 2,

        /// <summary>
        /// The middle paper tray.
        /// </summary>
        Middle = 3,

        /// <summary>
        /// Manual feed.
        /// </summary>
        Manual = 4,

        /// <summary>
        /// The default paper source.
        /// </summary>
        AutomaticFeed = 7
    }

    /// <summary>
    /// Specifies the printer resolution kind.
    /// </summary>
    public enum PrinterResolutionKind
    {
        /// <summary>
        /// A custom resolution.
        /// </summary>
        Custom = 0,

        /// <summary>
        /// Low printer resolution.
        /// </summary>
        Low = -1,

        /// <summary>
        /// Medium printer resolution.
        /// </summary>
        Medium = -2,

        /// <summary>
        /// High printer resolution.
        /// </summary>
        High = -3,

        /// <summary>
        /// Draft printer resolution.
        /// </summary>
        Draft = -4
    }
}

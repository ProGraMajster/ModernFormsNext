namespace ModernFormsNext
{
    /// <summary>
    /// Describes the paper source selected for printing.
    /// </summary>
    public class PaperSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaperSource"/> class.
        /// </summary>
        public PaperSource()
        {
            SourceName = "Automatic";
            Kind = PaperSourceKind.AutomaticFeed;
        }

        /// <summary>
        /// Gets or sets the paper source kind.
        /// </summary>
        public PaperSourceKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the paper source display name.
        /// </summary>
        public string SourceName { get; set; }

        /// <inheritdoc/>
        public override string ToString() => SourceName;
    }
}

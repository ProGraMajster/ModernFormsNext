using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="PrintDocument.QueryPageSettings"/> event.
    /// </summary>
    public class QueryPageSettingsEventArgs : PrintEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryPageSettingsEventArgs"/> class.
        /// </summary>
        /// <param name="pageSettings">The page settings that can be adjusted for the next page.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pageSettings"/> is <see langword="null"/>.</exception>
        public QueryPageSettingsEventArgs(PageSettings pageSettings)
        {
            ArgumentNullException.ThrowIfNull(pageSettings);
            PageSettings = pageSettings;
        }

        /// <summary>
        /// Gets or sets the page settings used for the next page.
        /// </summary>
        public PageSettings PageSettings { get; set; }
    }
}

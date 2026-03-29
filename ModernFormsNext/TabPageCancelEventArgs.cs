using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for events that allow cancelling a tab-related operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is typically used in events such as <c>TabControl.TabClosing</c>,
    /// where the operation can be intercepted and optionally cancelled.
    /// </para>
    /// <para>
    /// Setting <see cref="Cancel"/> to <see langword="true"/> prevents the tab from being closed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// tabControl.TabClosing += (s, e) =>
    /// {
    ///     if (e.TabPage.Text == "Important")
    ///         e.Cancel = true;
    /// };
    /// </code>
    /// </example>
    public class TabPageCancelEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabPageCancelEventArgs"/> class.
        /// </summary>
        /// <param name="page">The tab page associated with the event.</param>
        public TabPageCancelEventArgs(TabPage page)
            => TabPage = page;

        /// <summary>
        /// Gets the tab page associated with the event.
        /// </summary>
        /// <value>
        /// The <see cref="TabPage"/> that is being processed.
        /// </value>
        public TabPage TabPage { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation should be cancelled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to cancel the operation; otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// When set to <see langword="true"/>, the operation (e.g., closing a tab)
        /// will be aborted.
        /// </remarks>
        public bool Cancel { get; set; }
    }
}
using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for events related to a <see cref="TabPage"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used in events such as <c>TabControl.TabClosed</c> to provide
    /// information about the tab page involved in the operation.
    /// </para>
    /// <para>
    /// Unlike <see cref="TabPageCancelEventArgs"/>, this class does not support
    /// cancelling the operation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// tabControl.TabClosed += (s, e) =>
    /// {
    ///     Console.WriteLine($"Closed tab: {e.TabPage.Text}");
    /// };
    /// </code>
    /// </example>
    public class TabPageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabPageEventArgs"/> class.
        /// </summary>
        /// <param name="page">The tab page associated with the event.</param>
        public TabPageEventArgs(TabPage page)
            => TabPage = page;

        /// <summary>
        /// Gets the tab page associated with the event.
        /// </summary>
        /// <value>
        /// The <see cref="TabPage"/> involved in the event.
        /// </value>
        public TabPage TabPage { get; }
    }
}
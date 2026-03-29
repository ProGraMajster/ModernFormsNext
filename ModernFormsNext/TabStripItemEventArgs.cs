using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for events related to a <see cref="TabStripItem"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used in events such as <see cref="TabStrip.TabCloseButtonClicked"/>
    /// to provide information about the tab item involved in the interaction.
    /// </para>
    /// <para>
    /// Unlike cancelable event arguments, this class is intended for notification-only scenarios.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// tabStrip.TabCloseButtonClicked += (s, e) =>
    /// {
    ///     Console.WriteLine($"Close button clicked on tab: {e.Item.Text}");
    /// };
    /// </code>
    /// </example>
    public class TabStripItemEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabStripItemEventArgs"/> class.
        /// </summary>
        /// <param name="item">The tab item associated with the event.</param>
        public TabStripItemEventArgs(TabStripItem item)
            => Item = item;

        /// <summary>
        /// Gets the tab item associated with the event.
        /// </summary>
        /// <value>
        /// The <see cref="TabStripItem"/> involved in the event.
        /// </value>
        public TabStripItem Item { get; }
    }
}
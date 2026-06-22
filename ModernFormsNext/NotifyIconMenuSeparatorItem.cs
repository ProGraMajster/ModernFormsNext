namespace ModernFormsNext
{
    /// <summary>
    /// Represents a separator displayed in a <see cref="NotifyIconContextMenu"/>.
    /// </summary>
    /// <remarks>
    /// Separator items are rendered by the platform menu and cannot be selected.
    /// </remarks>
    public sealed class NotifyIconMenuSeparatorItem : NotifyIconMenuItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyIconMenuSeparatorItem"/> class.
        /// </summary>
        public NotifyIconMenuSeparatorItem ()
        {
            Enabled = false;
        }

        /// <inheritdoc/>
        internal override bool Separator => true;
    }
}

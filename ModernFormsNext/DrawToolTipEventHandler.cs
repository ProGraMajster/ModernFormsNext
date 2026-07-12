namespace ModernFormsNext
{
    /// <summary>
    /// Represents the method that handles the <see cref="ToolTip.Draw"/> event.
    /// </summary>
    /// <param name="sender">The <see cref="ToolTip"/> that owns the tooltip popup.</param>
    /// <param name="e">The event data used to render the tooltip with SkiaSharp.</param>
    public delegate void DrawToolTipEventHandler(object? sender, DrawToolTipEventArgs e);
}

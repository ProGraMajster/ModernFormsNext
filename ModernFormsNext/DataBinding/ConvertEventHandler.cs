namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Represents the method that handles <see cref="Binding.Format"/> and
    ///  <see cref="Binding.Parse"/> events.
    /// </summary>
    /// <param name="sender">The binding that raised the event.</param>
    /// <param name="e">The conversion information for the binding operation.</param>
    public delegate void ConvertEventHandler(object? sender, ConvertEventArgs e);
}

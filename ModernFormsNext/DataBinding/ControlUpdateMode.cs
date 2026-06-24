namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Determines when a bound component property is refreshed from its data source.
    /// </summary>
    /// <remarks>
    ///  This controls data-source-to-component updates. Use <see cref="Binding.DataSourceUpdateMode"/>
    ///  to control the opposite direction, where component changes are written back to the data source.
    /// </remarks>
    public enum ControlUpdateMode
    {
        /// <summary>
        ///  The component property is not updated automatically by the binding.
        /// </summary>
        Never = 0,

        /// <summary>
        ///  The component property is updated when the data source value changes.
        /// </summary>
        OnPropertyChanged = 1,
    }
}

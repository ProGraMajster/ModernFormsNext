using System;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides data for the <see cref="Binding.Format"/> and <see cref="Binding.Parse"/> events.
    /// </summary>
    /// <remarks>
    ///  Handlers can replace <see cref="Value"/> with a value of <see cref="DesiredType"/> to take
    ///  over conversion between a data-source value and a bindable component property. Conversion is
    ///  performed on the UI thread as part of a binding read or write operation.
    /// </remarks>
    /// <example>
    /// <code>
    /// binding.Format += (_, e) =>
    /// {
    ///     if (e.DesiredType == typeof(string) &amp;&amp; e.Value is decimal amount)
    ///     {
    ///         e.Value = amount.ToString("C");
    ///     }
    /// };
    /// </code>
    /// </example>
    public class ConvertEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="ConvertEventArgs"/> class.
        /// </summary>
        /// <param name="value">The value being converted.</param>
        /// <param name="desiredType">The type expected by the binding target.</param>
        public ConvertEventArgs(object? value, Type desiredType)
        {
            ArgumentNullException.ThrowIfNull(desiredType);

            Value = value;
            DesiredType = desiredType;
        }

        /// <summary>
        ///  Gets the type expected by the current conversion.
        /// </summary>
        public Type DesiredType { get; }

        /// <summary>
        ///  Gets or sets the value being converted.
        /// </summary>
        /// <remarks>
        ///  Set this property in a <see cref="Binding.Format"/> or <see cref="Binding.Parse"/>
        ///  handler to provide the converted value. If the value is left unchanged, the binding
        ///  continues with its built-in conversion pipeline.
        /// </remarks>
        public object? Value { get; set; }
    }
}

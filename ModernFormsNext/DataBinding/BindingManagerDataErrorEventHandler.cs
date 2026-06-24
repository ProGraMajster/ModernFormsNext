using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Represents the method that handles binding manager data errors.
    /// </summary>
    /// <param name="sender">The binding manager that raised the event.</param>
    /// <param name="e">The event data containing the data error exception.</param>
    public delegate void BindingManagerDataErrorEventHandler(object? sender, BindingManagerDataErrorEventArgs e);
}

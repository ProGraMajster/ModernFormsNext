using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Indicates the direction of a binding operation.
    /// </summary>
    public enum BindingCompleteContext
    {
        /// <summary>
        ///  Control value is being updated from data source value.
        /// </summary>
        ControlUpdate = 0,

        /// <summary>
        ///  Data source value is being updated from control value.
        /// </summary>
        DataSourceUpdate = 1,
    }
}

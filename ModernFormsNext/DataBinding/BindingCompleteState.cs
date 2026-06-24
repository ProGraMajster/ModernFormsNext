using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Indicates the result of a completed binding operation.
    /// </summary>
    public enum BindingCompleteState
    {
        /// <summary>
        ///  Binding operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        ///  Binding operation failed with a data error.
        /// </summary>
        DataError = 1,

        /// <summary>
        ///  Binding operation failed with an exception.
        /// </summary>
        Exception = 2,
    }
}

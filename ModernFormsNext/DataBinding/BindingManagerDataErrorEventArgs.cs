using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides data for binding manager data error events.
    /// </summary>
    public class BindingManagerDataErrorEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingManagerDataErrorEventArgs"/> class.
        /// </summary>
        /// <param name="exception">The exception raised while moving data through the binding manager.</param>
        public BindingManagerDataErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        /// <summary>
        ///  Gets the exception raised while moving data through the binding manager.
        /// </summary>
        public Exception Exception { get; }
    }
}

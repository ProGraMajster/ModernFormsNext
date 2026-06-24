using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Defines an object that can execute a command without exposing command binding details.
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        ///  Executes the command represented by this object.
        /// </summary>
        void Execute();
    }
}

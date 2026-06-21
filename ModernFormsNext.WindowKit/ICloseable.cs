using System;

namespace ModernFormsNext.WindowKit
{
    /// <summary>
    /// Represents an object that reports when it has closed.
    /// </summary>
    public interface ICloseable
    {
        /// <summary>
        /// Raised when the object is closed.
        /// </summary>
        event EventHandler? Closed;
    }
}

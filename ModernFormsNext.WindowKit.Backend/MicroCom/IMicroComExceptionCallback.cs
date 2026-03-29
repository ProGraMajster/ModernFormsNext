using System;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides a mechanism to handle exceptions that occur during native-to-managed calls.
    /// </summary>
    /// <remarks>
    /// This interface is used by the runtime to propagate exceptions that cannot be directly
    /// marshaled across the native boundary.
    /// </remarks>
    public interface IMicroComExceptionCallback
    {
        /// <summary>
        /// Called when an exception occurs during interop execution.
        /// </summary>
        /// <param name="e">The exception instance.</param>
        void RaiseException(Exception e);
    }
}
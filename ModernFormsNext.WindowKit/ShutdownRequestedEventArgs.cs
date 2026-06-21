using System.ComponentModel;

namespace ModernFormsNext.WindowKit.Controls.ApplicationLifetimes
{
    /// <summary>
    /// Provides data for application shutdown requests that can be canceled by the application.
    /// </summary>
    /// <remarks>
    /// Handlers can set <see cref="CancelEventArgs.Cancel"/> to keep the application running,
    /// for example while prompting the user to save work. The shutdown request is raised by the
    /// active application lifetime and should be handled on the UI thread.
    /// </remarks>
    public class ShutdownRequestedEventArgs : CancelEventArgs
    {

    }
}

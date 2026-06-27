using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides a base class for dialog components that show modal UI owned by a <see cref="Form"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ModernFormsNext dialog APIs are asynchronous because native and managed dialogs may run
    /// through different backend implementations. Derived dialogs should perform UI work on the
    /// dispatcher thread and return <see cref="DialogResult.OK"/> only when the user confirms
    /// the selection.
    /// </para>
    /// <para>
    /// Platform-specific dialogs should keep native handles, Win32 structures, and other backend
    /// details behind WindowKit service contracts rather than exposing them through this shared API.
    /// </para>
    /// </remarks>
    public abstract class CommonDialog : Component
    {
        /// <summary>
        /// Gets or sets application-defined data associated with the dialog.
        /// </summary>
        /// <remarks>
        /// This property is not interpreted by ModernFormsNext. It exists for callers that need
        /// to associate state with a reusable dialog instance.
        /// </remarks>
        public object? Tag { get; set; }

        /// <summary>
        /// Occurs when the user requests help from a platform dialog.
        /// </summary>
        /// <remarks>
        /// Backend support varies by platform. Windows-backed font and printing dialogs raise
        /// this event when their <c>ShowHelp</c> option is enabled and the native dialog reports
        /// a help command.
        /// </remarks>
        public event EventHandler? HelpRequest;

        /// <summary>
        /// Resets the dialog to its default option values.
        /// </summary>
        public abstract void Reset();

        /// <summary>
        /// Displays the dialog with the last open form as the owner.
        /// </summary>
        /// <returns>
        /// A task whose result indicates whether the user accepted or canceled the dialog.
        /// </returns>
        /// <remarks>
        /// Prefer <see cref="ShowDialog(Form)"/> when the owner is known. This overload is
        /// provided for WinForms-style migration convenience and requires at least one open
        /// <see cref="Form"/> to be tracked by <see cref="Application.OpenForms"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no open owner form can be found.
        /// </exception>
        public Task<DialogResult> ShowDialog()
        {
            var owner = Application.OpenForms.LastOrDefault();

            if (owner is null)
                throw new InvalidOperationException("A dialog owner is required when no forms are open.");

            return ShowDialog(owner);
        }

        /// <summary>
        /// Displays the dialog with the specified owner form.
        /// </summary>
        /// <param name="owner">The form that owns the modal dialog.</param>
        /// <returns>
        /// A task whose result indicates whether the user accepted or canceled the dialog.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="owner"/> is <see langword="null"/>.
        /// </exception>
        public Task<DialogResult> ShowDialog(Form owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return RunDialog(owner);
        }

        /// <summary>
        /// Raises the <see cref="HelpRequest"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnHelpRequest(EventArgs e) => HelpRequest?.Invoke(this, e);

        /// <summary>
        /// Runs the dialog implementation for a validated owner form.
        /// </summary>
        /// <param name="owner">The form that owns the modal dialog.</param>
        /// <returns>
        /// A task whose result indicates whether the user accepted or canceled the dialog.
        /// </returns>
        protected abstract Task<DialogResult> RunDialog(Form owner);
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides information about a completed binding operation.
    /// </summary>
    /// <remarks>
    ///  Instances are passed to <see cref="Binding.BindingComplete"/> and
    ///  <see cref="BindingSource.BindingComplete"/> after a data push or pull finishes.
    /// </remarks>
    public class BindingCompleteEventArgs : CancelEventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingCompleteEventArgs"/> class.
        /// </summary>
        /// <param name="binding">The binding that completed, or <see langword="null"/> when not available.</param>
        /// <param name="state">The final state of the binding operation.</param>
        /// <param name="context">The direction or context of the binding operation.</param>
        /// <param name="errorText">The binding or data error text, if one was reported.</param>
        /// <param name="exception">The exception raised by the binding operation, if any.</param>
        /// <param name="cancel">The initial cancellation state for the operation.</param>
        public BindingCompleteEventArgs(
            Binding? binding,
            BindingCompleteState state,
            BindingCompleteContext context,
            string? errorText,
            Exception? exception,
            bool cancel)
            : base(cancel)
        {
            Binding = binding;
            BindingCompleteState = state;
            BindingCompleteContext = context;
            ErrorText = errorText ?? string.Empty;
            Exception = exception;
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingCompleteEventArgs"/> class
        ///  with cancellation enabled.
        /// </summary>
        /// <param name="binding">The binding that completed, or <see langword="null"/> when not available.</param>
        /// <param name="state">The final state of the binding operation.</param>
        /// <param name="context">The direction or context of the binding operation.</param>
        /// <param name="errorText">The binding or data error text, if one was reported.</param>
        /// <param name="exception">The exception raised by the binding operation, if any.</param>
        public BindingCompleteEventArgs(
            Binding? binding,
            BindingCompleteState state,
            BindingCompleteContext context,
            string? errorText,
            Exception? exception)
            : this(binding, state, context, errorText, exception, true)
        {
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingCompleteEventArgs"/> class
        ///  with error text.
        /// </summary>
        /// <param name="binding">The binding that completed, or <see langword="null"/> when not available.</param>
        /// <param name="state">The final state of the binding operation.</param>
        /// <param name="context">The direction or context of the binding operation.</param>
        /// <param name="errorText">The binding or data error text, if one was reported.</param>
        public BindingCompleteEventArgs(
            Binding? binding,
            BindingCompleteState state,
            BindingCompleteContext context,
            string? errorText)
            : this(binding, state, context, errorText, null, true)
        {
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingCompleteEventArgs"/> class
        ///  for a successful operation.
        /// </summary>
        /// <param name="binding">The binding that completed, or <see langword="null"/> when not available.</param>
        /// <param name="state">The final state of the binding operation.</param>
        /// <param name="context">The direction or context of the binding operation.</param>
        public BindingCompleteEventArgs(
            Binding? binding,
            BindingCompleteState state,
            BindingCompleteContext context)
            : this(binding, state, context, string.Empty, null, false)
        {
        }

        /// <summary>
        ///  Gets the binding that completed, or <see langword="null"/> when not available.
        /// </summary>
        public Binding? Binding { get; }

        /// <summary>
        ///  Gets the final state of the binding operation.
        /// </summary>
        public BindingCompleteState BindingCompleteState { get; }

        /// <summary>
        ///  Gets the direction or context of the binding operation.
        /// </summary>
        public BindingCompleteContext BindingCompleteContext { get; }

        /// <summary>
        ///  Gets the binding or data error text, or an empty string when no error was reported.
        /// </summary>
        public string ErrorText { get; }

        /// <summary>
        ///  Gets the exception raised by the binding operation, if any.
        /// </summary>
        public Exception? Exception { get; }
    }
}

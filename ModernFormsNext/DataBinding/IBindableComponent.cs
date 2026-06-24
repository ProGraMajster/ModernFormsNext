using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Defines a component that can participate in ModernFormsNext data binding.
    /// </summary>
    /// <remarks>
    ///  Implementations expose a <see cref="ControlBindingsCollection"/> and optionally inherit a
    ///  <see cref="DataBinding.BindingContext"/> from their owning control tree. Data binding APIs
    ///  are expected to be used from the UI thread.
    /// </remarks>
    public interface IBindableComponent : IComponent
    {
        internal const string ComponentModelTrimIncompatibilityMessage = "Binding is not supported with trimming";

        /// <summary>
        ///  Gets the collection of bindings associated with this component.
        /// </summary>
        ControlBindingsCollection DataBindings { get; }

        /// <summary>
        ///  Gets or sets the binding context used to resolve currency managers for this component.
        /// </summary>
        BindingContext? BindingContext
        {
            get;
            set;
        }
    }
}

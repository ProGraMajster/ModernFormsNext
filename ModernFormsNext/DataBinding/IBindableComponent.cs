using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    public interface IBindableComponent : IComponent
    {
        internal const string ComponentModelTrimIncompatibilityMessage = "Binding is not supported with trimming";
        ControlBindingsCollection DataBindings { get; }
        BindingContext? BindingContext
        {
            get;
            set;
        }
    }
}

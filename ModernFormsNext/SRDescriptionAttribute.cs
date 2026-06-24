using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext
{
    [AttributeUsage(AttributeTargets.All)]
    internal sealed class SRDescriptionAttribute : DescriptionAttribute
    {
        public SRDescriptionAttribute(string description) : base(description)
        {
        }
    }
}

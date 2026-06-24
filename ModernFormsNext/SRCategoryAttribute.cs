using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext
{
    [AttributeUsage(AttributeTargets.All)]
    internal sealed class SRCategoryAttribute : CategoryAttribute
    {
        public SRCategoryAttribute(string category) : base(category)
        {
        }
    }
}

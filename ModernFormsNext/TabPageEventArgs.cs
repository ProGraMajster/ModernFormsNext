using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext
{
    public class TabPageEventArgs : EventArgs
    {
        public TabPageEventArgs (TabPage page) => TabPage = page;
        public TabPage TabPage { get; }
    }
}

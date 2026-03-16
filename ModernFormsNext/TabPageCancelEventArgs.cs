using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext
{
    public class TabPageCancelEventArgs : EventArgs
    {
        public TabPageCancelEventArgs (TabPage page) => TabPage = page;
        public TabPage TabPage { get; }
        public bool Cancel { get; set; }
    }
}

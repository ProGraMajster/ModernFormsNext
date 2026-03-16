using System;

namespace ModernFormsNext
{
    public class TabStripItemEventArgs : EventArgs
    {
        public TabStripItemEventArgs (TabStripItem item) => Item = item;
        public TabStripItem Item { get; }
    }
}

using System;
using SkiaSharp;
using static ModernFormsNext.TabStripItem;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a TabPage control.
    /// </summary>
    public class TabPage : Panel
    {
        /// <summary>
        /// Initializes a new instance of the TabPage class.
        /// </summary>
        public TabPage ()
        {
            Dock = DockStyle.Fill;
            TabStripItem = new TabStripItem ();
        }

        /// <summary>
        /// Initializes a new instance of the TabPage class with the specified text.
        /// </summary>
        public TabPage (string text) : this ()
        {
            TabStripItem.Text = text;
        }

        // The TabStripItem that accompanies this TabPage.
        internal TabStripItem TabStripItem { get; }

        /// <inheritdoc/>
        public override string Text { 
            get => TabStripItem.Text; 
            set => TabStripItem.Text = value;
        }

        public SKImage? Icon {
            get => TabStripItem.Icon;
            set => TabStripItem.Icon = value;
        }

        public bool Closable {
            get => TabStripItem.Closable;
            set => TabStripItem.Closable = value;
        }

        public bool Pinned {
            get => TabStripItem.Pinned;
            set => TabStripItem.Pinned = value;
        }

        public TabDisplayMode DisplayMode {
            get => TabStripItem.DisplayMode;
            set => TabStripItem.DisplayMode = value;
        }

    }
}

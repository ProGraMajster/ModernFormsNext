using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext
{
    /// <summary>
    ///  Provides properties for scrollbars.
    /// </summary>
    public class ScrollProperties
    {
        private readonly ScrollBar scrollbar;

        internal ScrollProperties (ScrollBar scrollbar)
        {
            this.scrollbar = scrollbar;
        }

        /// <summary>
        /// Gets or sets if the scrollbar is enabled.
        /// </summary>
        public bool Enabled {
            get => scrollbar.Enabled;
            set => scrollbar.Enabled = value;
        }

        /// <summary>
        /// Gets or sets the large change value of the scrollbar.
        /// </summary>
        public int LargeChange {
            get => scrollbar.LargeChange;
            set => scrollbar.LargeChange = value;
        }

        /// <summary>
        /// Gets or sets the maximum value of the scrollbar.
        /// </summary>
        public int Maximum {
            get => scrollbar.Maximum;
            set => scrollbar.Maximum = value;
        }

        /// <summary>
        /// Gets or sets the minimum value of the scrollbar.
        /// </summary>
        public int Minimum {
            get => scrollbar.Minimum;
            set => scrollbar.Minimum = value;
        }

        /// <summary>
        /// Gets or sets the small change value of the scrollbar.
        /// </summary>
        public int SmallChange {
            get => scrollbar.SmallChange;
            set => scrollbar.SmallChange = value;
        }

        /// <summary>
        /// Gets or sets the value of the scrollbar.
        /// </summary>
        /// <remarks>
        /// Set on the UI thread. An actual viewport movement synchronously raises
        /// <see cref="ScrollableControl.Scroll"/> after child displacement, with ThumbPosition.
        /// Event offsets are relative to Minimum; this property retains the raw scrollbar value.
        /// Values outside Minimum/Maximum throw; assigning the current value emits no Scroll event.
        /// </remarks>
        public int Value {
            get => scrollbar.Value;
            set => scrollbar.Value = value;
        }

        /// <summary>
        /// The parent control of this scrollbar, if any.
        /// </summary>
        public ScrollableControl? ParentControl => scrollbar.Parent as ScrollableControl;
    }
}

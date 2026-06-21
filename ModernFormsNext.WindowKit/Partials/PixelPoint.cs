using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit
{
    public readonly partial struct PixelPoint
    {
        /// <summary>
        /// Converts this point to a <see cref="System.Drawing.Point"/>.
        /// </summary>
        /// <returns>The converted drawing point.</returns>
        public System.Drawing.Point ToDrawingPoint () => new System.Drawing.Point (X, Y);
    }
}

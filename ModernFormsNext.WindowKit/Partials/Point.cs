using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit
{
    readonly partial struct Point
    {
        /// <summary>
        /// Gets the empty point at coordinates 0,0.
        /// </summary>
        public static Point Empty { get; } = new Point();
    }
}

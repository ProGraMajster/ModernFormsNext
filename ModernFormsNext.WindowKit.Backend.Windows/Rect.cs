using System;
using System.Globalization;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop
{
    /// <summary>
    /// Defines a rectangle.
    /// </summary>
    public readonly struct Rect : IEquatable<Rect>
    {
        /// <summary>
        /// An empty rectangle.
        /// </summary>
        public static readonly Rect Empty = default(Rect);

        /// <summary>
        /// The X position.
        /// </summary>
        private readonly double _x;

        /// <summary>
        /// The Y position.
        /// </summary>
        private readonly double _y;

        /// <summary>
        /// The width.
        /// </summary>
        private readonly double _width;

        /// <summary>
        /// The height.
        /// </summary>
        private readonly double _height;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> structure.
        /// </summary>
        /// <param name="x">The X position.</param>
        /// <param name="y">The Y position.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public Rect(double x, double y, double width, double height)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        /// <summary>
        /// Gets the X position.
        /// </summary>
        public double X => _x;

        /// <summary>
        /// Gets the Y position.
        /// </summary>
        public double Y => _y;

        /// <summary>
        /// Gets the width.
        /// </summary>
        public double Width => _width;

        /// <summary>
        /// Gets the height.
        /// </summary>
        public double Height => _height;

        /// <summary>
        /// Gets the right position of the rectangle.
        /// </summary>
        public double Right => _x + _width;

        /// <summary>
        /// Gets the bottom position of the rectangle.
        /// </summary>
        public double Bottom => _y + _height;
        
        /// <summary>
        /// Gets the left position.
        /// </summary>
        public double Left => _x;

        /// <summary>
        /// Gets the top position.
        /// </summary>
        public double Top => _y;

        /// <summary>
        /// Gets a value that indicates whether the rectangle is empty.
        /// </summary>
        // ReSharper disable CompareOfFloatsByEqualityOperator
        public bool IsEmpty => _width == 0 && _height == 0;
        // ReSharper restore CompareOfFloatsByEqualityOperator

        /// <summary>
        /// Checks for equality between two <see cref="Rect"/>s.
        /// </summary>
        /// <param name="left">The first rect.</param>
        /// <param name="right">The second rect.</param>
        /// <returns>True if the rects are equal; otherwise false.</returns>
        public static bool operator ==(Rect left, Rect right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks for inequality between two <see cref="Rect"/>s.
        /// </summary>
        /// <param name="left">The first rect.</param>
        /// <param name="right">The second rect.</param>
        /// <returns>True if the rects are unequal; otherwise false.</returns>
        public static bool operator !=(Rect left, Rect right)
        {
            return !(left == right);
        }
        
        /// <summary>
        /// Multiplies a rectangle by a scale.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The scaled rectangle.</returns>
        public static Rect operator *(Rect rect, double scale)
        {
            return new Rect(
                rect.X * scale,
                rect.Y * scale,
                rect.Width * scale,
                rect.Height * scale);
        }

        /// <summary>
        /// Centers another rectangle in this rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to center.</param>
        /// <returns>The centered rectangle.</returns>
        public Rect CenterRect(Rect rect)
        {
            return new Rect(
                _x + ((_width - rect._width) / 2),
                _y + ((_height - rect._height) / 2),
                rect._width,
                rect._height);
        }

        /// <summary>
        /// Returns a boolean indicating whether the rect is equal to the other given rect.
        /// </summary>
        /// <param name="other">The other rect to test equality against.</param>
        /// <returns>True if this rect is equal to other; False otherwise.</returns>
        public bool Equals(Rect other)
        {
            // ReSharper disable CompareOfFloatsByEqualityOperator
            return _x == other._x &&
                   _y == other._y &&
                   _width == other._width &&
                   _height == other._height;
            // ReSharper enable CompareOfFloatsByEqualityOperator
        }

        /// <summary>
        /// Returns a boolean indicating whether the given object is equal to this rectangle.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>True if the object is equal to this rectangle; false otherwise.</returns>
        public override bool Equals(object? obj) => obj is Rect other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + X.GetHashCode();
                hash = (hash * 23) + Y.GetHashCode();
                hash = (hash * 23) + Width.GetHashCode();
                hash = (hash * 23) + Height.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Gets the intersection of two rectangles.
        /// </summary>
        /// <param name="rect">The other rectangle.</param>
        /// <returns>The intersection.</returns>
        public Rect Intersect(Rect rect)
        {
            var newLeft = (rect.X > X) ? rect.X : X;
            var newTop = (rect.Y > Y) ? rect.Y : Y;
            var newRight = (rect.Right < Right) ? rect.Right : Right;
            var newBottom = (rect.Bottom < Bottom) ? rect.Bottom : Bottom;

            if ((newRight > newLeft) && (newBottom > newTop))
            {
                return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
            }
            else
            {
                return Empty;
            }
        }

        /// <summary>
        /// Determines whether a rectangle intersects with this rectangle.
        /// </summary>
        /// <param name="rect">The other rectangle.</param>
        /// <returns>
        /// True if the specified rectangle intersects with this one; otherwise false.
        /// </returns>
        public bool Intersects(Rect rect)
        {
            return (rect.X < Right) && (X < rect.Right) && (rect.Y < Bottom) && (Y < rect.Bottom);
        }

        /// <summary>
		/// Normalizes the rectangle so both the <see cref="Width"/> and <see 
        /// cref="Height"/> are positive, without changing the location of the rectangle
		/// </summary>
        /// <returns>Normalized Rect</returns>
		/// <remarks>
		/// Empty rect will be return when Rect contains invalid values. Like NaN.
		/// </remarks>
		public Rect Normalize()
        {
            Rect rect = this;            

            if(double.IsNaN(rect.Right) || double.IsNaN(rect.Bottom) || 
                double.IsNaN(rect.X) || double.IsNaN(rect.Y) || 
                double.IsNaN(Height) || double.IsNaN(Width))
            {
                return Rect.Empty;
            }

            if (rect.Width < 0)
            {
                var x = X + Width;
                var width = X - x;

                rect = rect.WithX(x).WithWidth(width);
            }

            if (rect.Height < 0)
            {
                var y = Y + Height;
                var height = Y - y;

                rect = rect.WithY(y).WithHeight(height);
            }

            return rect;
        }


        /// <summary>
        /// Returns a new <see cref="Rect"/> with the specified X position.
        /// </summary>
        /// <param name="x">The x position.</param>
        /// <returns>The new <see cref="Rect"/>.</returns>
        public Rect WithX(double x)
        {
            return new Rect(x, _y, _width, _height);
        }

        /// <summary>
        /// Returns a new <see cref="Rect"/> with the specified Y position.
        /// </summary>
        /// <param name="y">The y position.</param>
        /// <returns>The new <see cref="Rect"/>.</returns>
        public Rect WithY(double y)
        {
            return new Rect(_x, y, _width, _height);
        }

        /// <summary>
        /// Returns a new <see cref="Rect"/> with the specified width.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <returns>The new <see cref="Rect"/>.</returns>
        public Rect WithWidth(double width)
        {
            return new Rect(_x, _y, width, _height);
        }

        /// <summary>
        /// Returns a new <see cref="Rect"/> with the specified height.
        /// </summary>
        /// <param name="height">The height.</param>
        /// <returns>The new <see cref="Rect"/>.</returns>
        public Rect WithHeight(double height)
        {
            return new Rect(_x, _y, _width, height);
        }

        /// <summary>
        /// Returns the string representation of the rectangle.
        /// </summary>
        /// <returns>The string representation of the rectangle.</returns>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}, {1}, {2}, {3}",
                _x,
                _y,
                _width,
                _height);
        }
    }
}

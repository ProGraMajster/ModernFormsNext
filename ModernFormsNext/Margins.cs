using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents page margins in hundredths of an inch.
    /// </summary>
    /// <remarks>
    /// The printing APIs use the same logical unit convention as WinForms printing APIs:
    /// one unit is one one-hundredth of an inch. Changing margin values does not invalidate
    /// any control directly; callers should rerender previews or print again after updating them.
    /// </remarks>
    public class Margins : ICloneable
    {
        private int left;
        private int right;
        private int top;
        private int bottom;

        /// <summary>
        /// Initializes a new instance of the <see cref="Margins"/> class with one-inch margins.
        /// </summary>
        public Margins()
            : this(100, 100, 100, 100)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Margins"/> class.
        /// </summary>
        /// <param name="left">The left margin in hundredths of an inch.</param>
        /// <param name="right">The right margin in hundredths of an inch.</param>
        /// <param name="top">The top margin in hundredths of an inch.</param>
        /// <param name="bottom">The bottom margin in hundredths of an inch.</param>
        /// <exception cref="ArgumentOutOfRangeException">A margin is less than zero.</exception>
        public Margins(int left, int right, int top, int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        /// <summary>
        /// Gets or sets the left margin in hundredths of an inch.
        /// </summary>
        public int Left {
            get => left;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                left = value;
            }
        }

        /// <summary>
        /// Gets or sets the right margin in hundredths of an inch.
        /// </summary>
        public int Right {
            get => right;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                right = value;
            }
        }

        /// <summary>
        /// Gets or sets the top margin in hundredths of an inch.
        /// </summary>
        public int Top {
            get => top;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                top = value;
            }
        }

        /// <summary>
        /// Gets or sets the bottom margin in hundredths of an inch.
        /// </summary>
        public int Bottom {
            get => bottom;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                bottom = value;
            }
        }

        /// <summary>
        /// Creates a copy of this margin object.
        /// </summary>
        /// <returns>A new <see cref="Margins"/> instance with the same values.</returns>
        public object Clone() => new Margins(Left, Right, Top, Bottom);

        /// <inheritdoc/>
        public override string ToString() => $"[Margins Left={Left}, Right={Right}, Top={Top}, Bottom={Bottom}]";
    }
}

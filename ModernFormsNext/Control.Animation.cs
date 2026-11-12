using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides animation-related properties for controls.
    /// </summary>
    /// <remarks>
    /// Set render transforms on the owning UI thread. Every distinct value is retained,
    /// including small changes at animation endpoints. Changes notify native presentation
    /// observers and invalidate rendering without changing layout bounds.
    /// </remarks>
    public partial class Control
    {
        private float opacity = 1f;
        private float translationX;
        private float translationY;
        private float scaleX = 1f;
        private float scaleY = 1f;
        private float rotation;

        /// <summary>
        /// Invalidates the control after a render transform value changes.
        /// </summary>
        private void InvalidateAnimation ()
        {
            NotifyNativePresentationChanged();
            Invalidate ();
        }

        /// <summary>
        /// Gets or sets the control opacity in the range from 0 to 1.
        /// </summary>
        public float Opacity {
            get => opacity;
            set {
                var clamped = Math.Clamp (value, 0f, 1f);

                // Approximate equality can discard the final 0/1 after a near-terminal
                // frame. Keep exact state and notify observers for every distinct value.
                if (opacity == clamped)
                    return;

                opacity = clamped;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the horizontal render translation of the control, in logical pixels.
        /// </summary>
        public float TranslationX {
            get => translationX;
            set {
                if (translationX == value)
                    return;

                translationX = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the vertical render translation of the control, in logical pixels.
        /// </summary>
        public float TranslationY {
            get => translationY;
            set {
                if (translationY == value)
                    return;

                translationY = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the dimensionless horizontal render scale of the control; 1 is unchanged.
        /// </summary>
        public float ScaleX {
            get => scaleX;
            set {
                if (scaleX == value)
                    return;

                scaleX = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the dimensionless vertical render scale of the control; 1 is unchanged.
        /// </summary>
        public float ScaleY {
            get => scaleY;
            set {
                if (scaleY == value)
                    return;

                scaleY = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the render rotation of the control in degrees.
        /// </summary>
        public float Rotation {
            get => rotation;
            set {
                if (rotation == value)
                    return;

                rotation = value;
                InvalidateAnimation ();
            }
        }
    }
}

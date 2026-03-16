using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides animation-related properties for controls.
    /// </summary>
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
            Invalidate ();
        }

        /// <summary>
        /// Gets or sets the control opacity in the range from 0 to 1.
        /// </summary>
        public float Opacity {
            get => opacity;
            set {
                var clamped = Math.Clamp (value, 0f, 1f);

                if (Math.Abs (opacity - clamped) < 0.0001f)
                    return;

                opacity = clamped;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the horizontal render translation of the control.
        /// </summary>
        public float TranslationX {
            get => translationX;
            set {
                if (Math.Abs (translationX - value) < 0.0001f)
                    return;

                translationX = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the vertical render translation of the control.
        /// </summary>
        public float TranslationY {
            get => translationY;
            set {
                if (Math.Abs (translationY - value) < 0.0001f)
                    return;

                translationY = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the horizontal render scale of the control.
        /// </summary>
        public float ScaleX {
            get => scaleX;
            set {
                if (Math.Abs (scaleX - value) < 0.0001f)
                    return;

                scaleX = value;
                InvalidateAnimation ();
            }
        }

        /// <summary>
        /// Gets or sets the vertical render scale of the control.
        /// </summary>
        public float ScaleY {
            get => scaleY;
            set {
                if (Math.Abs (scaleY - value) < 0.0001f)
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
                if (Math.Abs (rotation - value) < 0.0001f)
                    return;

                rotation = value;
                InvalidateAnimation ();
            }
        }
    }
}

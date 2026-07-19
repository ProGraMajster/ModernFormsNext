using System;
using System.Numerics;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Defines a mutable, shareable value that paints an area or a text mask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Brushes are platform-neutral visual values. A single brush may be shared by multiple
    /// controls or stored in a dynamic resource. Changing the brush raises <see cref="Changed"/>
    /// so every live consumer can invalidate its rendering. Mutate brushes on the UI thread.
    /// </para>
    /// <para>
    /// The renderer resolves relative gradient coordinates against the current paint bounds. The
    /// same brush therefore adapts to control resizing and is rendered identically by the Windows
    /// and experimental Android Skia surfaces.
    /// </para>
    /// </remarks>
    public abstract class Brush
    {
        private float opacity = 1f;
        private Matrix3x2 transform = Matrix3x2.Identity;

        /// <summary>
        /// Initializes a new brush with full opacity and an identity transform.
        /// </summary>
        protected Brush()
        {
        }

        /// <summary>
        /// Occurs when a property or nested gradient stop changes the rendered result.
        /// </summary>
        /// <remarks>
        /// The event is raised synchronously on the mutation thread. Controls use weak
        /// subscriptions, so sharing a brush from a long-lived resource does not retain a control.
        /// </remarks>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets or sets the opacity multiplier applied to every color produced by this brush.
        /// </summary>
        /// <value>A finite value in the inclusive range 0 through 1. The default is 1.</value>
        /// <remarks>
        /// Changing opacity invalidates controls using this brush but does not request layout.
        /// Color alpha and brush opacity are multiplied during rendering.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is not finite or is outside the inclusive range 0..1.
        /// </exception>
        public float Opacity
        {
            get => opacity;
            set
            {
                ValidateUnitValue(value, nameof(Opacity));
                if (opacity.Equals(value))
                    return;

                opacity = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the platform-neutral transform applied to this brush's coordinate space.
        /// </summary>
        /// <value>
        /// A finite <see cref="Matrix3x2"/>. The default is <see cref="Matrix3x2.Identity"/>.
        /// Translation components use the same logical coordinate space as the resolved paint
        /// bounds; relative gradient points are resolved before this transform is applied.
        /// </value>
        /// <remarks>
        /// Use <see cref="Matrix3x2.CreateTranslation(float, float)"/>,
        /// <see cref="Matrix3x2.CreateScale(float)"/>, and
        /// <see cref="Matrix3x2.CreateRotation(float)"/> to compose transforms without depending
        /// on renderer-specific matrix types. Changing the transform invalidates rendering only.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when any matrix component is NaN or infinity.
        /// </exception>
        public Matrix3x2 Transform
        {
            get => transform;
            set
            {
                ValidateTransform(value);
                if (transform.Equals(value))
                    return;

                transform = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises <see cref="Changed"/> after a rendered value changes.
        /// </summary>
        /// <param name="e">Event data for the change.</param>
        /// <remarks>
        /// Derived classes should update their backing field before calling this method and should
        /// raise it only when the effective value changes.
        /// </remarks>
        protected virtual void OnChanged(EventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            Changed?.Invoke(this, e);
        }

        internal static void ValidateUnitValue(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite and in the inclusive range 0 through 1.");
        }

        internal static void ValidatePoint(System.Drawing.PointF value, string parameterName)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                throw new ArgumentException("Point coordinates must be finite.", parameterName);
        }

        private static void ValidateTransform(Matrix3x2 value)
        {
            if (!float.IsFinite(value.M11) || !float.IsFinite(value.M12) ||
                !float.IsFinite(value.M21) || !float.IsFinite(value.M22) ||
                !float.IsFinite(value.M31) || !float.IsFinite(value.M32))
            {
                throw new ArgumentException("Transform components must be finite.", nameof(value));
            }
        }
    }
}

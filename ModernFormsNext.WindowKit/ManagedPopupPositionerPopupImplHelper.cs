using System;
using System.Collections.Generic;
using System.Linq;
using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Controls.Primitives.PopupPositioning
{
    /// <summary>
    /// Simplifies integration between <see cref="IPopupImpl"/> implementations and the managed popup positioner.
    /// </summary>
    [PrivateApi]
    public class ManagedPopupPositionerPopupImplHelper : IManagedPopupPositionerPopup 
    {
        private readonly IWindowBaseImpl _parent;

        /// <summary>
        /// Moves and resizes a popup using platform pixel coordinates and logical size.
        /// </summary>
        /// <param name="position">The popup position in device pixels.</param>
        /// <param name="size">The popup size in logical pixels.</param>
        /// <param name="scaling">The scale factor used by the parent window.</param>
        public delegate void MoveResizeDelegate(PixelPoint position, Size size, double scaling);
        private readonly MoveResizeDelegate _moveResize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedPopupPositionerPopupImplHelper"/> class.
        /// </summary>
        /// <param name="parent">The parent window implementation used for screen and scaling information.</param>
        /// <param name="moveResize">The callback used to move and resize the popup.</param>
        public ManagedPopupPositionerPopupImplHelper(IWindowBaseImpl parent, MoveResizeDelegate moveResize)
        {
            _parent = parent;
            _moveResize = moveResize;
        }

        /// <inheritdoc />
        public IReadOnlyList<ManagedPopupPositionerScreenInfo> Screens =>

            _parent.Screen.AllScreens
                .Select(s => new ManagedPopupPositionerScreenInfo(s.Bounds.ToRect(1), s.WorkingArea.ToRect(1)))
                .ToArray();

        /// <inheritdoc />
        public Rect ParentClientAreaScreenGeometry
        {
            get
            {
                // Popup positioner operates with abstract coordinates, but in our case they are pixel ones
                var point = _parent.PointToScreen(default);
                var size = _parent.ClientSize * Scaling;
                return new Rect(point.X, point.Y, size.Width, size.Height);

        }
        }

        /// <inheritdoc />
        public void MoveAndResize(Point devicePoint, Size virtualSize)
        {
            _moveResize(new PixelPoint((int)devicePoint.X, (int)devicePoint.Y), virtualSize, _parent.RenderScaling);
        }

        /// <inheritdoc />
        public virtual double Scaling => _parent.DesktopScaling;
}
}

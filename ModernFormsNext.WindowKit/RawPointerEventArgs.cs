using System;
using System.Collections.Generic;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// Identifies the kind of raw pointer event reported by a platform backend.
    /// </summary>
    /// <remarks>
    /// Numeric values are stable across compatible releases for already compiled backends.
    /// New event kinds must use new values without renumbering existing members.
    /// </remarks>
    public enum RawPointerEventType
    {
        /// <summary>
        /// The pointer left the top-level window.
        /// </summary>
        LeaveWindow = 0,

        /// <summary>
        /// The left pointer button was pressed.
        /// </summary>
        LeftButtonDown = 1,

        /// <summary>
        /// The left pointer button was released.
        /// </summary>
        LeftButtonUp = 2,

        /// <summary>
        /// The right pointer button was pressed.
        /// </summary>
        RightButtonDown = 3,

        /// <summary>
        /// The right pointer button was released.
        /// </summary>
        RightButtonUp = 4,

        /// <summary>
        /// The middle pointer button was pressed.
        /// </summary>
        MiddleButtonDown = 5,

        /// <summary>
        /// The middle pointer button was released.
        /// </summary>
        MiddleButtonUp = 6,

        /// <summary>
        /// The first extended pointer button was pressed.
        /// </summary>
        XButton1Down = 7,

        /// <summary>
        /// The first extended pointer button was released.
        /// </summary>
        XButton1Up = 8,

        /// <summary>
        /// The second extended pointer button was pressed.
        /// </summary>
        XButton2Down = 9,

        /// <summary>
        /// The second extended pointer button was released.
        /// </summary>
        XButton2Up = 10,

        /// <summary>
        /// The pointer moved.
        /// </summary>
        Move = 11,

        /// <summary>
        /// The pointer wheel changed.
        /// </summary>
        Wheel = 12,

        /// <summary>
        /// The left button was pressed in the non-client area of a window.
        /// </summary>
        NonClientLeftButtonDown = 13,

        /// <summary>
        /// A touch contact began.
        /// </summary>
        TouchBegin = 14,

        /// <summary>
        /// A touch contact moved or changed.
        /// </summary>
        TouchUpdate = 15,

        /// <summary>
        /// A touch contact ended.
        /// </summary>
        TouchEnd = 16,

        /// <summary>
        /// A touch contact was canceled by the platform.
        /// </summary>
        TouchCancel = 17,

        /// <summary>
        /// A magnification gesture was reported.
        /// </summary>
        Magnify = 18,

        /// <summary>
        /// A rotation gesture was reported.
        /// </summary>
        Rotate = 19,

        /// <summary>
        /// A swipe gesture was reported.
        /// </summary>
        Swipe = 20,

        /// <summary>
        /// The platform revoked pointer capture before the matching button release was received.
        /// </summary>
        CaptureLost = 21
    }

    /// <summary>
    /// A raw mouse event.
    /// </summary>
    [PrivateApi]
    public class RawPointerEventArgs : RawInputEventArgs
    {
        private RawPointerPoint _point;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RawPointerEventArgs"/> class.
        /// </summary>
        /// <param name="device">The associated device.</param>
        /// <param name="timestamp">The event timestamp.</param>
        /// <param name="root">The root from which the event originates.</param>
        /// <param name="type">The type of the event.</param>
        /// <param name="position">The mouse position, in client DIPs.</param>
        /// <param name="inputModifiers">The input modifiers.</param>
        public RawPointerEventArgs(
            IInputDevice device,
            ulong timestamp,
            IInputRoot root,
            RawPointerEventType type,
            Point position, 
            RawInputModifiers inputModifiers)
            : base(device, timestamp, root)
        {
            Point = new RawPointerPoint();
            Position = position;
            Type = type;
            InputModifiers = inputModifiers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RawPointerEventArgs"/> class.
        /// </summary>
        /// <param name="device">The associated device.</param>
        /// <param name="timestamp">The event timestamp.</param>
        /// <param name="root">The root from which the event originates.</param>
        /// <param name="type">The type of the event.</param>
        /// <param name="point">The point properties and position, in client DIPs.</param>
        /// <param name="inputModifiers">The input modifiers.</param>
        public RawPointerEventArgs(
            IInputDevice device,
            ulong timestamp,
            IInputRoot root,
            RawPointerEventType type,
            RawPointerPoint point, 
            RawInputModifiers inputModifiers)
            : base(device, timestamp, root)
        {
            Point = point;
            Type = type;
            InputModifiers = inputModifiers;
        }

        /// <summary>
        /// Gets the raw pointer identifier.
        /// </summary>
        public long RawPointerId { get; set; }

        /// <summary>
        /// Gets the pointer properties and position, in client DIPs.
        /// </summary>
        public RawPointerPoint Point
        {
            get => _point;
            set => _point = value;
        }

        /// <summary>
        /// Gets the mouse position, in client DIPs.
        /// </summary>
        public Point Position
        {
            get => _point.Position;
            set => _point.Position = value;
        }

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        public RawPointerEventType Type { get; set; }

        /// <summary>
        /// Gets the input modifiers.
        /// </summary>
        public RawInputModifiers InputModifiers { get; set; }
        
        /// <summary>
        /// Points that were traversed by a pointer since the previous relevant event,
        /// only valid for Move and TouchUpdate
        /// </summary>
        public Lazy<IReadOnlyList<RawPointerPoint>?>? IntermediatePoints { get; set; }
        
        //internal IInputElement? InputHitTestResult { get; set; }
    }

    /// <summary>
    /// Describes a raw pointer sample reported by a platform backend.
    /// </summary>
    [PrivateApi]
    public record struct RawPointerPoint
    {
        /// <summary>
        /// Pointer position, in client DIPs.
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Gets the pointer twist value reported by the platform.
        /// </summary>
        public float Twist { get; set; }

        /// <summary>
        /// Gets the normalized pointer pressure value reported by the platform.
        /// </summary>
        public float Pressure { get; set; }

        /// <summary>
        /// Gets the horizontal pointer tilt value reported by the platform.
        /// </summary>
        public float XTilt { get; set; }

        /// <summary>
        /// Gets the vertical pointer tilt value reported by the platform.
        /// </summary>
        public float YTilt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RawPointerPoint"/> struct with default pressure.
        /// </summary>
        public RawPointerPoint()
        {
            this = default;
            Pressure = 0.5f;
        }
    }
}

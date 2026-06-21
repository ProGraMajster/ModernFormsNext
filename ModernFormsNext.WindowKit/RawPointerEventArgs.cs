using System;
using System.Collections.Generic;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// Identifies the kind of raw pointer event reported by a platform backend.
    /// </summary>
    public enum RawPointerEventType
    {
        /// <summary>
        /// The pointer left the top-level window.
        /// </summary>
        LeaveWindow,

        /// <summary>
        /// The left pointer button was pressed.
        /// </summary>
        LeftButtonDown,

        /// <summary>
        /// The left pointer button was released.
        /// </summary>
        LeftButtonUp,

        /// <summary>
        /// The right pointer button was pressed.
        /// </summary>
        RightButtonDown,

        /// <summary>
        /// The right pointer button was released.
        /// </summary>
        RightButtonUp,

        /// <summary>
        /// The middle pointer button was pressed.
        /// </summary>
        MiddleButtonDown,

        /// <summary>
        /// The middle pointer button was released.
        /// </summary>
        MiddleButtonUp,

        /// <summary>
        /// The first extended pointer button was pressed.
        /// </summary>
        XButton1Down,

        /// <summary>
        /// The first extended pointer button was released.
        /// </summary>
        XButton1Up,

        /// <summary>
        /// The second extended pointer button was pressed.
        /// </summary>
        XButton2Down,

        /// <summary>
        /// The second extended pointer button was released.
        /// </summary>
        XButton2Up,

        /// <summary>
        /// The pointer moved.
        /// </summary>
        Move,

        /// <summary>
        /// The pointer wheel changed.
        /// </summary>
        Wheel,

        /// <summary>
        /// The left button was pressed in the non-client area of a window.
        /// </summary>
        NonClientLeftButtonDown,

        /// <summary>
        /// A touch contact began.
        /// </summary>
        TouchBegin,

        /// <summary>
        /// A touch contact moved or changed.
        /// </summary>
        TouchUpdate,

        /// <summary>
        /// A touch contact ended.
        /// </summary>
        TouchEnd,

        /// <summary>
        /// A touch contact was canceled by the platform.
        /// </summary>
        TouchCancel,

        /// <summary>
        /// A magnification gesture was reported.
        /// </summary>
        Magnify,

        /// <summary>
        /// A rotation gesture was reported.
        /// </summary>
        Rotate,

        /// <summary>
        /// A swipe gesture was reported.
        /// </summary>
        Swipe
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

        /// <inheritdoc cref="PointerPointProperties.Twist" />
        public float Twist { get; set; }
        /// <inheritdoc cref="PointerPointProperties.Pressure" />
        public float Pressure { get; set; }
        /// <inheritdoc cref="PointerPointProperties.XTilt" />
        public float XTilt { get; set; }
        /// <inheritdoc cref="PointerPointProperties.YTilt" />
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

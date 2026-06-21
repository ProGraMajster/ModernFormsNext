using System;
using System.Collections.Generic;
using System.Linq;
//using ModernFormsNext.WindowKit.Input.GestureRecognizers;
//using ModernFormsNext.WindowKit.VisualTree;

namespace ModernFormsNext.WindowKit.Input
{
    /// <summary>
    /// Represents a pointer tracked by the input system.
    /// </summary>
    public partial class Pointer : IPointer, IDisposable
    {
        private static int s_NextFreePointerId = 1000;

        /// <summary>
        /// Gets the next framework-generated pointer identifier.
        /// </summary>
        /// <returns>A pointer identifier that has not been generated previously in this process.</returns>
        public static int GetNextFreeId() => s_NextFreePointerId++;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Pointer"/> class.
        /// </summary>
        /// <param name="id">The unique pointer identifier.</param>
        /// <param name="type">The pointer device type.</param>
        /// <param name="isPrimary">A value indicating whether this is the primary pointer.</param>
        public Pointer(int id, PointerType type, bool isPrimary)
        {
            Id = id;
            Type = type;
            IsPrimary = isPrimary;
        }

        /// <inheritdoc />
        public int Id { get; }

        //static IInputElement? FindCommonParent(IInputElement? control1, IInputElement? control2)
        //{
        //    if (control1 is not Visual c1 || control2 is not Visual c2)
        //        return null;
        //    var seen = new HashSet<IInputElement>(c1.GetSelfAndVisualAncestors().OfType<IInputElement>());
        //    return c2.GetSelfAndVisualAncestors().OfType<IInputElement>().FirstOrDefault(seen.Contains);
        //}

        //protected virtual void PlatformCapture(IInputElement? element)
        //{
            
        //}
        
        //public void Capture(IInputElement? control)
        //{
        //    if (Captured is Visual v1)
        //        v1.DetachedFromVisualTree -= OnCaptureDetached;
        //    var oldCapture = Captured;
        //    Captured = control;
        //    PlatformCapture(control);
        //    if (oldCapture is Visual v2)
        //    {
        //        var commonParent = FindCommonParent(control, oldCapture);
        //        foreach (var notifyTarget in v2.GetSelfAndVisualAncestors().OfType<IInputElement>())
        //        {
        //            if (notifyTarget == commonParent)
        //                break;
        //            notifyTarget.RaiseEvent(new PointerCaptureLostEventArgs(notifyTarget, this));
        //        }
        //    }

        //    if (Captured is Visual v3)
        //        v3.DetachedFromVisualTree += OnCaptureDetached;

        //    if (Captured != null)
        //        CaptureGestureRecognizer(null);
        //}

        //static IInputElement? GetNextCapture(Visual parent)
        //{
        //    return parent as IInputElement ?? parent.FindAncestorOfType<IInputElement>();
        //}

        //private void OnCaptureDetached(object? sender, VisualTreeAttachmentEventArgs e)
        //{
        //    Capture(GetNextCapture(e.Parent));
        //}


        //public IInputElement? Captured { get; private set; }
            
        /// <inheritdoc />
        public PointerType Type { get; }

        /// <inheritdoc />
        public bool IsPrimary { get; }

        ///// <summary>
        ///// Gets the gesture recognizer that is currently capturing by the pointer, if any.
        ///// </summary>
        //internal GestureRecognizer? CapturedGestureRecognizer { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            //Capture(null);
        }

        ///// <summary>
        ///// Captures pointer input to the specified gesture recognizer.
        ///// </summary>
        ///// <param name="gestureRecognizer">The gesture recognizer.</param>
        ///// </remarks>
        //internal void CaptureGestureRecognizer(GestureRecognizer? gestureRecognizer)
        //{
        //    if (CapturedGestureRecognizer != gestureRecognizer)
        //        CapturedGestureRecognizer?.PointerCaptureLostInternal(this);

        //    if (gestureRecognizer != null)
        //        Capture(null);

        //    CapturedGestureRecognizer = gestureRecognizer;
        //}
    }
}

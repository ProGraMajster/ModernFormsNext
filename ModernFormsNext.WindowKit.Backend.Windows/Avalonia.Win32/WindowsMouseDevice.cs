using System;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.Input
{
    internal class WindowsMouseDevice : MouseDevice
    {
        private readonly IPointer _pointer;

        public WindowsMouseDevice() : base(WindowsMousePointer.CreatePointer(out var pointer))
        {
            _pointer = pointer;
        }

        // Normally user should use IPointer.Capture instead of MouseDevice.Capture,
        // But on Windows we need to handle WM_MOUSE capture manually without having access to the Pointer. 
        //internal void Capture(IInputElement? control)
        //{
        //    _pointer.Capture(control);
        //}
        
        internal class WindowsMousePointer : Pointer
        {
            private WindowsMousePointer() : base(GetNextFreeId(),PointerType.Mouse, true)
            {
            }
            
            public static WindowsMousePointer CreatePointer(out WindowsMousePointer pointer)
            {
                return pointer = new WindowsMousePointer();
            }

            //protected override void PlatformCapture(IInputElement? element)
            //{
            //    var hwnd = (TopLevel.GetTopLevel(element as Visual)?.PlatformImpl as WindowImpl)
            //        ?.Handle.Handle;

            //    if (hwnd.HasValue && hwnd != IntPtr.Zero)
            //        UnmanagedMethods.SetCapture(hwnd.Value);
            //    else
            //        UnmanagedMethods.ReleaseCapture();
            //}
        }
    }
}

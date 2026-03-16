using System;
using ModernFormsNext.WindowKit.Mac.Interop;

namespace ModernFormsNext.WindowKit.Native
{
    partial class WindowBaseImpl
    {
        protected unsafe partial class WindowBaseEvents
        {
            public AvnDragDropEffects DragEvent(AvnDragEventType type, AvnPoint position,
                AvnInputModifiers modifiers,
                AvnDragDropEffects effects,
                IAvnClipboard clipboard, IntPtr dataObjectHandle)
            {
                return AvnDragDropEffects.None;
            }
        }
    }
}

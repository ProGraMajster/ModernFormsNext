using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32
{
    internal partial class WindowImpl
    {
        public IEnumerable<object> Surfaces => new object[] { Handle, /* _gl, */ _framebuffer };

        public void SetIcon(SkiaSharp.SKBitmap? icon)
        {
            if (icon == null)
            {
                UnmanagedMethods.PostMessage(_hwnd, (int)UnmanagedMethods.WindowsMessage.WM_SETICON,
                    new IntPtr((int)UnmanagedMethods.Icons.ICON_BIG), IntPtr.Zero);

                return;
            }

            using var icon2 = icon.ToBitmap();

            UnmanagedMethods.PostMessage(_hwnd, (int)UnmanagedMethods.WindowsMessage.WM_SETICON,
                new IntPtr((int)UnmanagedMethods.Icons.ICON_BIG), icon2.GetHicon());
        }
    }
}

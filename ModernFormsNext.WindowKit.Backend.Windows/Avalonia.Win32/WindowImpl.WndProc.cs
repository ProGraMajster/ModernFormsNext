// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Controls.Platform;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32
{
    internal partial class WindowImpl
    {
        protected virtual unsafe IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            IntPtr lRet = IntPtr.Zero;
            bool callDwp = true;

            if (_isClientAreaExtended)
            {
                //lRet = CustomCaptionProc(hWnd, msg, wParam, lParam, ref callDwp);
            }

            if (callDwp)
            {
                lRet = AppWndProc(hWnd, msg, wParam, lParam);
            }

            return lRet;
        }
        
        //public INativeControlHostImpl NativeControlHost => _nativeControlHost;

        protected virtual bool ShouldTakeFocusOnClick => true;
    }
}

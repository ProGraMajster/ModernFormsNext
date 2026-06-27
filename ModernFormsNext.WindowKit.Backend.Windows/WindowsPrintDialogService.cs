using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Windows
{
    /// <summary>
    /// Implements <see cref="IPlatformPrintDialogService"/> by using Win32 common printing dialogs.
    /// </summary>
    /// <remarks>
    /// This type intentionally keeps <c>PRINTDLGW</c>, <c>PAGESETUPDLGW</c>, global memory handles,
    /// hook procedures, and common-dialog flags inside the Windows backend so shared ModernFormsNext
    /// code stays platform-neutral.
    /// </remarks>
    internal sealed class WindowsPrintDialogService : IPlatformPrintDialogService
    {
        private const int HelpButtonId = 0x040E;
        private const int WmCommand = 0x0111;
        private const int PaperKindLetter = 1;
        private const int PaperKindLegal = 5;
        private const int PaperKindA4 = 9;
        private const int PaperKindA5 = 11;

        /// <inheritdoc/>
        public Task<PlatformPrintDialogResult?> ShowPrintDialogAsync(IWindowBaseImpl owner, PlatformPrintDialogRequest request)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(request);

            PrintHookProc? hookProc = null;
            var printerSettings = ClonePrinterSettings(request.PrinterSettings);
            var flags = BuildPrintFlags(request);

            if (request.ShowHelp && request.HelpRequest is not null)
            {
                hookProc = (_, message, wParam, _) =>
                {
                    if (message == WmCommand && LowWord(wParam) == HelpButtonId)
                        request.HelpRequest?.Invoke();

                    return IntPtr.Zero;
                };
                flags |= PrintDialogFlags.EnablePrintHook;
            }

            var printDialog = new PrintDialogData
            {
                lStructSize = Marshal.SizeOf<PrintDialogData>(),
                hwndOwner = owner.Handle.Handle,
                Flags = flags,
                nFromPage = ToDialogPage(printerSettings.FromPage, printerSettings.MinimumPage),
                nToPage = ToDialogPage(printerSettings.ToPage, printerSettings.MinimumPage),
                nMinPage = ToDialogPage(printerSettings.MinimumPage, 1),
                nMaxPage = ToDialogPage(Math.Max(printerSettings.MaximumPage, printerSettings.MinimumPage), 1),
                nCopies = ToDialogCopies(printerSettings.Copies),
                lpfnPrintHook = hookProc is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(hookProc)
            };

            try
            {
                if (!PrintDlg(ref printDialog))
                {
                    var extendedError = CommDlgExtendedError();

                    if (extendedError != 0)
                        throw new InvalidOperationException($"The Windows print dialog failed with common dialog error 0x{extendedError:X}.");

                    return Task.FromResult<PlatformPrintDialogResult?>(null);
                }

                UpdatePrinterSettingsFromPrintDialog(printerSettings, printDialog);
                return Task.FromResult<PlatformPrintDialogResult?>(new PlatformPrintDialogResult(printerSettings));
            }
            finally
            {
                FreeGlobal(printDialog.hDevMode);
                FreeGlobal(printDialog.hDevNames);

                if (printDialog.hDC != IntPtr.Zero)
                    DeleteDC(printDialog.hDC);

                // Keep the delegate alive until the native dialog has fully returned and can no
                // longer call the hook pointer.
                GC.KeepAlive(hookProc);
            }
        }

        /// <inheritdoc/>
        public Task<PlatformPageSetupDialogResult?> ShowPageSetupDialogAsync(IWindowBaseImpl owner, PlatformPageSetupDialogRequest request)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(request);

            PageSetupHookProc? hookProc = null;
            var pageSettings = ClonePageSettings(request.PageSettings);
            var printerSettings = ClonePrinterSettings(request.PrinterSettings);
            var useMetric = request.EnableMetric;
            var flags = BuildPageSetupFlags(request);

            if (request.ShowHelp && request.HelpRequest is not null)
            {
                hookProc = (_, message, wParam, _) =>
                {
                    if (message == WmCommand && LowWord(wParam) == HelpButtonId)
                        request.HelpRequest?.Invoke();

                    return IntPtr.Zero;
                };
                flags |= PageSetupDialogFlags.EnablePageSetupHook;
            }

            var pageSetupDialog = new PageSetupDialogData
            {
                lStructSize = Marshal.SizeOf<PageSetupDialogData>(),
                hwndOwner = owner.Handle.Handle,
                Flags = flags,
                ptPaperSize = new NativePoint
                {
                    X = ToNativeUnits(pageSettings.PaperSize.Width, useMetric),
                    Y = ToNativeUnits(pageSettings.PaperSize.Height, useMetric)
                },
                rtMinMargin = ToNativeRect(request.MinMargins, useMetric),
                rtMargin = ToNativeRect(pageSettings.Margins, useMetric),
                lpfnPageSetupHook = hookProc is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(hookProc)
            };

            try
            {
                if (!PageSetupDlg(ref pageSetupDialog))
                {
                    var extendedError = CommDlgExtendedError();

                    if (extendedError != 0)
                        throw new InvalidOperationException($"The Windows page setup dialog failed with common dialog error 0x{extendedError:X}.");

                    return Task.FromResult<PlatformPageSetupDialogResult?>(null);
                }

                UpdatePageSettingsFromPageSetupDialog(pageSettings, pageSetupDialog, useMetric);
                UpdatePrinterNameFromDevNames(printerSettings, pageSetupDialog.hDevNames);

                return Task.FromResult<PlatformPageSetupDialogResult?>(
                    new PlatformPageSetupDialogResult(pageSettings, printerSettings));
            }
            finally
            {
                FreeGlobal(pageSetupDialog.hDevMode);
                FreeGlobal(pageSetupDialog.hDevNames);
                GC.KeepAlive(hookProc);
            }
        }

        private static PrintDialogFlags BuildPrintFlags(PlatformPrintDialogRequest request)
        {
            var settings = request.PrinterSettings;
            var flags = PrintDialogFlags.None;

            flags |= settings.PrintRange switch
            {
                PlatformPrintRange.Selection => PrintDialogFlags.Selection,
                PlatformPrintRange.SomePages => PrintDialogFlags.PageNums,
                PlatformPrintRange.CurrentPage => PrintDialogFlags.CurrentPage,
                _ => PrintDialogFlags.AllPages
            };

            if (!request.AllowSelection)
                flags |= PrintDialogFlags.NoSelection;

            if (!request.AllowSomePages)
                flags |= PrintDialogFlags.NoPageNums;

            if (!request.AllowCurrentPage)
                flags |= PrintDialogFlags.NoCurrentPage;

            if (!request.AllowPrintToFile)
                flags |= PrintDialogFlags.DisablePrintToFile;

            if (!request.ShowNetwork)
                flags |= PrintDialogFlags.NoNetworkButton;

            if (settings.Collate)
                flags |= PrintDialogFlags.Collate;

            if (settings.PrintToFile)
                flags |= PrintDialogFlags.PrintToFile;

            if (request.ShowHelp)
                flags |= PrintDialogFlags.ShowHelp;

            return flags;
        }

        private static PageSetupDialogFlags BuildPageSetupFlags(PlatformPageSetupDialogRequest request)
        {
            var flags = request.EnableMetric
                ? PageSetupDialogFlags.InHundredthsOfMillimeters
                : PageSetupDialogFlags.InThousandthsOfInches;

            flags |= PageSetupDialogFlags.Margins | PageSetupDialogFlags.MinMargins;

            if (!request.AllowMargins)
                flags |= PageSetupDialogFlags.DisableMargins;

            if (!request.AllowOrientation)
                flags |= PageSetupDialogFlags.DisableOrientation;

            if (!request.AllowPaper)
                flags |= PageSetupDialogFlags.DisablePaper;

            if (!request.AllowPrinter)
                flags |= PageSetupDialogFlags.DisablePrinter;

            if (!request.ShowNetwork)
                flags |= PageSetupDialogFlags.NoNetworkButton;

            if (request.ShowHelp)
                flags |= PageSetupDialogFlags.ShowHelp;

            return flags;
        }

        private static void UpdatePrinterSettingsFromPrintDialog(PlatformPrinterSettings settings, PrintDialogData printDialog)
        {
            settings.Copies = (short)Math.Max(1, (int)printDialog.nCopies);
            settings.Collate = printDialog.Flags.HasFlag(PrintDialogFlags.Collate);
            settings.PrintToFile = printDialog.Flags.HasFlag(PrintDialogFlags.PrintToFile);
            settings.FromPage = Math.Max(0, (int)printDialog.nFromPage);
            settings.ToPage = Math.Max(settings.FromPage, (int)printDialog.nToPage);

            if (printDialog.Flags.HasFlag(PrintDialogFlags.Selection))
                settings.PrintRange = PlatformPrintRange.Selection;
            else if (printDialog.Flags.HasFlag(PrintDialogFlags.PageNums))
                settings.PrintRange = PlatformPrintRange.SomePages;
            else if (printDialog.Flags.HasFlag(PrintDialogFlags.CurrentPage))
                settings.PrintRange = PlatformPrintRange.CurrentPage;
            else
                settings.PrintRange = PlatformPrintRange.AllPages;

            UpdatePrinterNameFromDevNames(settings, printDialog.hDevNames);
        }

        private static void UpdatePageSettingsFromPageSetupDialog(PlatformPageSettings settings, PageSetupDialogData dialog, bool useMetric)
        {
            var width = Math.Max(1, FromNativeUnits(dialog.ptPaperSize.X, useMetric));
            var height = Math.Max(1, FromNativeUnits(dialog.ptPaperSize.Y, useMetric));
            var landscape = width > height;
            var portraitWidth = landscape ? height : width;
            var portraitHeight = landscape ? width : height;

            settings.Landscape = landscape;
            settings.Margins = FromNativeRect(dialog.rtMargin, useMetric);
            settings.PaperSize = new PlatformPaperSize
            {
                Kind = GetPaperKind(portraitWidth, portraitHeight),
                Name = GetPaperName(portraitWidth, portraitHeight),
                Width = portraitWidth,
                Height = portraitHeight
            };
        }

        private static void UpdatePrinterNameFromDevNames(PlatformPrinterSettings settings, IntPtr hDevNames)
        {
            var printerName = TryGetPrinterName(hDevNames);

            if (!string.IsNullOrWhiteSpace(printerName))
                settings.PrinterName = printerName;
        }

        private static PlatformPrinterSettings ClonePrinterSettings(PlatformPrinterSettings source)
        {
            return new PlatformPrinterSettings
            {
                CanDuplex = source.CanDuplex,
                Collate = source.Collate,
                Copies = source.Copies,
                Duplex = source.Duplex,
                FromPage = source.FromPage,
                IsPlotter = source.IsPlotter,
                LandscapeAngle = source.LandscapeAngle,
                MaximumCopies = source.MaximumCopies,
                MaximumPage = source.MaximumPage,
                MinimumPage = source.MinimumPage,
                PrintFileName = source.PrintFileName,
                PrintRange = source.PrintRange,
                PrintToFile = source.PrintToFile,
                PrinterName = source.PrinterName,
                SupportsColor = source.SupportsColor,
                ToPage = source.ToPage
            };
        }

        private static PlatformPageSettings ClonePageSettings(PlatformPageSettings source)
        {
            return new PlatformPageSettings
            {
                Color = source.Color,
                Landscape = source.Landscape,
                Margins = CloneMargins(source.Margins),
                PaperSize = new PlatformPaperSize
                {
                    Kind = source.PaperSize.Kind,
                    Name = source.PaperSize.Name,
                    Width = source.PaperSize.Width,
                    Height = source.PaperSize.Height
                },
                PaperSource = new PlatformPaperSource
                {
                    Kind = source.PaperSource.Kind,
                    Name = source.PaperSource.Name
                }
            };
        }

        private static PlatformMargins CloneMargins(PlatformMargins source)
        {
            return new PlatformMargins
            {
                Left = source.Left,
                Right = source.Right,
                Top = source.Top,
                Bottom = source.Bottom
            };
        }

        private static NativeRect ToNativeRect(PlatformMargins margins, bool useMetric)
        {
            return new NativeRect
            {
                Left = ToNativeUnits(margins.Left, useMetric),
                Top = ToNativeUnits(margins.Top, useMetric),
                Right = ToNativeUnits(margins.Right, useMetric),
                Bottom = ToNativeUnits(margins.Bottom, useMetric)
            };
        }

        private static PlatformMargins FromNativeRect(NativeRect rect, bool useMetric)
        {
            return new PlatformMargins
            {
                Left = FromNativeUnits(rect.Left, useMetric),
                Right = FromNativeUnits(rect.Right, useMetric),
                Top = FromNativeUnits(rect.Top, useMetric),
                Bottom = FromNativeUnits(rect.Bottom, useMetric)
            };
        }

        private static int ToNativeUnits(int hundredthsOfInch, bool useMetric)
        {
            return useMetric
                ? (int)Math.Round(hundredthsOfInch * 25.4d)
                : hundredthsOfInch * 10;
        }

        private static int FromNativeUnits(int value, bool useMetric)
        {
            return useMetric
                ? Math.Max(0, (int)Math.Round(value / 25.4d))
                : Math.Max(0, (int)Math.Round(value / 10d));
        }

        private static ushort ToDialogPage(int value, int fallback)
        {
            var normalized = value <= 0 ? fallback : value;
            return (ushort)Math.Clamp(normalized, 1, ushort.MaxValue);
        }

        private static ushort ToDialogCopies(short copies)
        {
            return (ushort)Math.Clamp(copies, (short)1, (short)short.MaxValue);
        }

        private static int GetPaperKind(int width, int height)
        {
            return (width, height) switch
            {
                (850, 1100) => PaperKindLetter,
                (850, 1400) => PaperKindLegal,
                (827, 1169) => PaperKindA4,
                (583, 827) => PaperKindA5,
                _ => 0
            };
        }

        private static string GetPaperName(int width, int height)
        {
            return GetPaperKind(width, height) switch
            {
                PaperKindLetter => "Letter",
                PaperKindLegal => "Legal",
                PaperKindA4 => "A4",
                PaperKindA5 => "A5",
                _ => "Custom"
            };
        }

        private static string? TryGetPrinterName(IntPtr hDevNames)
        {
            if (hDevNames == IntPtr.Zero)
                return null;

            var pointer = GlobalLock(hDevNames);

            if (pointer == IntPtr.Zero)
                return null;

            try
            {
                var devNames = Marshal.PtrToStructure<DevNames>(pointer);
                var deviceNamePointer = IntPtr.Add(pointer, devNames.wDeviceOffset * 2);
                return Marshal.PtrToStringUni(deviceNamePointer);
            }
            finally
            {
                GlobalUnlock(hDevNames);
            }
        }

        private static int LowWord(IntPtr value)
        {
            return IntPtr.Size == 8
                ? unchecked((int)(value.ToInt64() & 0xffff))
                : value.ToInt32() & 0xffff;
        }

        private static void FreeGlobal(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                GlobalFree(handle);
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "PrintDlgW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintDlg(ref PrintDialogData printDialog);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "PageSetupDlgW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PageSetupDlg(ref PageSetupDialogData pageSetupDialog);

        [DllImport("comdlg32.dll")]
        private static extern uint CommDlgExtendedError();

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr handle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalFree(IntPtr handle);

        private delegate IntPtr PrintHookProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr PageSetupHookProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [Flags]
        private enum PrintDialogFlags : uint
        {
            None = 0x00000000,
            AllPages = 0x00000000,
            Selection = 0x00000001,
            PageNums = 0x00000002,
            NoSelection = 0x00000004,
            NoPageNums = 0x00000008,
            Collate = 0x00000010,
            PrintToFile = 0x00000020,
            ReturnDC = 0x00000100,
            ShowHelp = 0x00000800,
            EnablePrintHook = 0x00001000,
            DisablePrintToFile = 0x00080000,
            NoNetworkButton = 0x00200000,
            CurrentPage = 0x00400000,
            NoCurrentPage = 0x00800000
        }

        [Flags]
        private enum PageSetupDialogFlags : uint
        {
            Margins = 0x00000002,
            MinMargins = 0x00000001,
            InThousandthsOfInches = 0x00000004,
            InHundredthsOfMillimeters = 0x00000008,
            DisableMargins = 0x00000010,
            DisablePrinter = 0x00000020,
            NoNetworkButton = 0x00200000,
            DisableOrientation = 0x00000100,
            DisablePaper = 0x00000200,
            ShowHelp = 0x00000800,
            EnablePageSetupHook = 0x00002000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PrintDialogData
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDevMode;
            public IntPtr hDevNames;
            public IntPtr hDC;
            public PrintDialogFlags Flags;
            public ushort nFromPage;
            public ushort nToPage;
            public ushort nMinPage;
            public ushort nMaxPage;
            public ushort nCopies;
            public IntPtr hInstance;
            public IntPtr lCustData;
            public IntPtr lpfnPrintHook;
            public IntPtr lpfnSetupHook;
            public IntPtr lpPrintTemplateName;
            public IntPtr lpSetupTemplateName;
            public IntPtr hPrintTemplate;
            public IntPtr hSetupTemplate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PageSetupDialogData
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDevMode;
            public IntPtr hDevNames;
            public PageSetupDialogFlags Flags;
            public NativePoint ptPaperSize;
            public NativeRect rtMinMargin;
            public NativeRect rtMargin;
            public IntPtr hInstance;
            public IntPtr lCustData;
            public IntPtr lpfnPageSetupHook;
            public IntPtr lpfnPagePaintHook;
            public IntPtr lpPageSetupTemplateName;
            public IntPtr hPageSetupTemplate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DevNames
        {
            public ushort wDriverOffset;
            public ushort wDeviceOffset;
            public ushort wOutputOffset;
            public ushort wDefault;
        }
    }
}

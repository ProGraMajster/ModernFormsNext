using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Backend.Windows
{
    /// <summary>
    /// Implements <see cref="IPlatformFontDialogService"/> by using the Win32 common font dialog.
    /// </summary>
    /// <remarks>
    /// This type intentionally keeps <c>CHOOSEFONTW</c>, <c>LOGFONTW</c>, hook procedures,
    /// and COLORREF conversion inside the Windows backend so shared ModernFormsNext code stays
    /// platform-neutral.
    /// </remarks>
    internal sealed class WindowsFontDialogService : IPlatformFontDialogService
    {
        private const int ApplyButtonId = 0x0402;
        private const int ColorComboId = 0x0473;
        private const int ColorLabelId = 0x0443;
        private const int DefaultCharSet = 1;
        private const int DefaultPitch = 0;
        private const int FontWeightBold = 700;
        private const int FontWeightNormal = 400;
        private const int LogPixelsY = 90;
        private const int ShowWindowHide = 0;
        private const int HelpButtonId = 0x040E;
        private const int CbErr = -1;
        private const uint CbGetCurSel = 0x0147;
        private const uint CbGetItemData = 0x0150;
        private const uint WmChooseFontGetLogFont = WmUser + 1;
        private const uint WmCommand = 0x0111;
        private const uint WmInitDialog = 0x0110;
        private const uint WmUser = 0x0400;

        /// <inheritdoc/>
        public Task<PlatformFontDialogResult?> ShowFontDialogAsync(IWindowBaseImpl owner, PlatformFontDialogRequest request)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(request);

            var logFont = CreateLogFont(request.Font);
            var logFontPointer = Marshal.AllocHGlobal(Marshal.SizeOf<LogFont>());
            Exception? callbackException = null;

            ChooseFontHookProc? hookProc = null;

            try
            {
                Marshal.StructureToPtr(logFont, logFontPointer, false);
                var sizeBounds = GetNormalizedSizeBounds(request);

                hookProc = (hWnd, message, wParam, lParam) =>
                {
                    try
                    {
                        return FontHookProc(hWnd, message, wParam, request);
                    }
                    catch (Exception ex)
                    {
                        callbackException ??= ex;
                        return IntPtr.Zero;
                    }
                };

                var chooseFont = new ChooseFontData
                {
                    lStructSize = Marshal.SizeOf<ChooseFontData>(),
                    hwndOwner = owner.Handle.Handle,
                    lpLogFont = logFontPointer,
                    Flags = BuildFlags(request),
                    rgbColors = ToColorRef(request.Color),
                    lpfnHook = Marshal.GetFunctionPointerForDelegate(hookProc),
                    nSizeMin = sizeBounds.Min,
                    nSizeMax = sizeBounds.Max == 0 ? int.MaxValue : sizeBounds.Max
                };

                if (!ChooseFont(ref chooseFont))
                {
                    ThrowIfCallbackFailed(callbackException);

                    var extendedError = CommDlgExtendedError();

                    if (extendedError != 0)
                        throw new InvalidOperationException($"The Windows font dialog failed with common dialog error 0x{extendedError:X}.");

                    return Task.FromResult<PlatformFontDialogResult?>(null);
                }

                logFont = Marshal.PtrToStructure<LogFont>(logFontPointer);
                ThrowIfCallbackFailed(callbackException);

                var result = CreateResult(logFont, chooseFont.iPointSize / 10f, chooseFont.rgbColors, request);
                return Task.FromResult<PlatformFontDialogResult?>(result);
            }
            finally
            {
                Marshal.FreeHGlobal(logFontPointer);

                // Keep the delegate alive until ChooseFont has fully returned and the hook pointer
                // can no longer be called by the native dialog.
                GC.KeepAlive(hookProc);
            }
        }

        private static ChooseFontFlags BuildFlags(PlatformFontDialogRequest request)
        {
            var flags = ChooseFontFlags.ScreenFonts
                | ChooseFontFlags.EnableHook
                | ChooseFontFlags.InitToLogFontStruct;

            if (request.ShowEffects)
                flags |= ChooseFontFlags.Effects;

            if (request.ShowApply)
                flags |= ChooseFontFlags.Apply;

            if (request.ShowHelp)
                flags |= ChooseFontFlags.ShowHelp;

            if (!request.AllowSimulations)
                flags |= ChooseFontFlags.NoSimulations;

            if (!request.AllowVectorFonts)
                flags |= ChooseFontFlags.NoVectorFonts;

            if (!request.AllowVerticalFonts)
                flags |= ChooseFontFlags.NoVerticalFonts;

            if (!request.AllowScriptChange)
                flags |= ChooseFontFlags.SelectScript;

            if (request.FixedPitchOnly)
                flags |= ChooseFontFlags.FixedPitchOnly;

            if (request.FontMustExist)
                flags |= ChooseFontFlags.ForceFontExist;

            if (request.ScriptsOnly)
                flags |= ChooseFontFlags.ScriptsOnly;

            if (request.TrueTypeOnly)
                flags |= ChooseFontFlags.TrueTypeOnly;

            var sizeBounds = GetNormalizedSizeBounds(request);

            if (sizeBounds.Min > 0 || sizeBounds.Max > 0)
                flags |= ChooseFontFlags.LimitSize;

            return flags;
        }

        private static PlatformFontDialogResult CreateResult(
            LogFont logFont,
            float sizeInPoints,
            int colorRef,
            PlatformFontDialogRequest request)
        {
            var familyName = string.IsNullOrWhiteSpace(logFont.lfFaceName)
                ? request.Font.FamilyName
                : logFont.lfFaceName;

            if (sizeInPoints <= 0)
                sizeInPoints = SizeInPointsFromLogFontHeight(logFont.lfHeight);

            var sizeBounds = GetNormalizedSizeBounds(request);

            if (sizeInPoints <= 0)
                sizeInPoints = sizeBounds.Min > 0 ? sizeBounds.Min : Math.Max(1, request.Font.SizeInPoints);

            if (sizeBounds.Min > 0 && sizeInPoints < sizeBounds.Min)
                sizeInPoints = sizeBounds.Min;

            if (sizeBounds.Max > 0 && sizeInPoints > sizeBounds.Max)
                sizeInPoints = sizeBounds.Max;

            var style = PlatformFontStyle.Regular;

            if (logFont.lfWeight >= FontWeightBold)
                style |= PlatformFontStyle.Bold;

            if (logFont.lfItalic != 0)
                style |= PlatformFontStyle.Italic;

            if (logFont.lfUnderline != 0)
                style |= PlatformFontStyle.Underline;

            if (logFont.lfStrikeOut != 0)
                style |= PlatformFontStyle.Strikeout;

            return new PlatformFontDialogResult(
                new PlatformFontSelection(familyName, sizeInPoints, style),
                FromColorRef(colorRef));
        }

        private static (int Min, int Max) GetNormalizedSizeBounds(PlatformFontDialogRequest request)
        {
            var min = Math.Max(0, request.MinSize);
            var max = Math.Max(0, request.MaxSize);

            if (max > 0 && max < min)
                max = min;

            return (min, max);
        }

        private static LogFont CreateLogFont(PlatformFontSelection font)
        {
            var familyName = string.IsNullOrWhiteSpace(font.FamilyName) ? "Segoe UI" : font.FamilyName;

            if (familyName.Length > 31)
                familyName = familyName[..31];

            return new LogFont
            {
                lfHeight = -LogicalHeightFromSizeInPoints(font.SizeInPoints <= 0 ? 9 : font.SizeInPoints),
                lfWeight = font.Style.HasFlag(PlatformFontStyle.Bold) ? FontWeightBold : FontWeightNormal,
                lfItalic = font.Style.HasFlag(PlatformFontStyle.Italic) ? (byte)1 : (byte)0,
                lfUnderline = font.Style.HasFlag(PlatformFontStyle.Underline) ? (byte)1 : (byte)0,
                lfStrikeOut = font.Style.HasFlag(PlatformFontStyle.Strikeout) ? (byte)1 : (byte)0,
                lfCharSet = DefaultCharSet,
                lfPitchAndFamily = DefaultPitch,
                lfFaceName = familyName
            };
        }

        private static IntPtr FontHookProc(
            IntPtr hWnd,
            uint message,
            IntPtr wParam,
            PlatformFontDialogRequest request)
        {
            if (message == WmInitDialog)
            {
                if (!request.ShowColor)
                {
                    HideDialogItem(hWnd, ColorComboId);
                    HideDialogItem(hWnd, ColorLabelId);
                }

                return IntPtr.Zero;
            }

            if (message != WmCommand)
                return IntPtr.Zero;

            var commandId = LowWord(wParam);

            if (commandId == ApplyButtonId)
            {
                var logFont = new LogFont();
                SendMessage(hWnd, WmChooseFontGetLogFont, IntPtr.Zero, ref logFont);

                var color = GetSelectedColorRef(hWnd, request.Color);
                request.Apply?.Invoke(CreateResult(logFont, 0, color, request));
                return IntPtr.Zero;
            }

            if (commandId == HelpButtonId)
            {
                request.HelpRequest?.Invoke();
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private static int GetSelectedColorRef(IntPtr hWnd, SKColor fallback)
        {
            var selectedIndex = ToInt32(SendDlgItemMessage(hWnd, ColorComboId, CbGetCurSel, IntPtr.Zero, IntPtr.Zero));

            if (selectedIndex == CbErr)
                return ToColorRef(fallback);

            return ToInt32(SendDlgItemMessage(hWnd, ColorComboId, CbGetItemData, (IntPtr)selectedIndex, IntPtr.Zero));
        }

        private static int GetDpiY()
        {
            var dc = GetDC(IntPtr.Zero);

            if (dc == IntPtr.Zero)
                return 96;

            try
            {
                var dpi = GetDeviceCaps(dc, LogPixelsY);
                return dpi <= 0 ? 96 : dpi;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }
        }

        private static void HideDialogItem(IntPtr hWnd, int itemId)
        {
            var item = GetDlgItem(hWnd, itemId);

            if (item != IntPtr.Zero)
                ShowWindow(item, ShowWindowHide);
        }

        private static int LogicalHeightFromSizeInPoints(float sizeInPoints)
        {
            return Math.Max(1, (int)Math.Round(sizeInPoints * GetDpiY() / 72f));
        }

        private static float SizeInPointsFromLogFontHeight(int logicalHeight)
        {
            return Math.Abs(logicalHeight) * 72f / GetDpiY();
        }

        private static int LowWord(IntPtr value) => ToInt32(value) & 0xffff;

        private static int ToColorRef(SKColor color)
        {
            return color.Red | (color.Green << 8) | (color.Blue << 16);
        }

        private static SKColor FromColorRef(int colorRef)
        {
            return new SKColor(
                (byte)(colorRef & 0xff),
                (byte)((colorRef >> 8) & 0xff),
                (byte)((colorRef >> 16) & 0xff));
        }

        private static int ToInt32(IntPtr value)
        {
            return IntPtr.Size == 8
                ? unchecked((int)(value.ToInt64() & 0xffffffff))
                : value.ToInt32();
        }

        private static void ThrowIfCallbackFailed(Exception? exception)
        {
            if (exception is not null)
                throw new InvalidOperationException("A font dialog callback failed.", exception);
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChooseFont(ref ChooseFontData chooseFont);

        [DllImport("comdlg32.dll")]
        private static extern uint CommDlgExtendedError();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendDlgItemMessage(IntPtr hDlg, int nIDDlgItem, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, ref LogFont lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private delegate IntPtr ChooseFontHookProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [Flags]
        private enum ChooseFontFlags
        {
            ScreenFonts = 0x00000001,
            ShowHelp = 0x00000004,
            EnableHook = 0x00000008,
            InitToLogFontStruct = 0x00000040,
            Effects = 0x00000100,
            Apply = 0x00000200,
            ScriptsOnly = 0x00000400,
            NoVectorFonts = 0x00000800,
            NoSimulations = 0x00001000,
            LimitSize = 0x00002000,
            FixedPitchOnly = 0x00004000,
            ForceFontExist = 0x00010000,
            TrueTypeOnly = 0x00040000,
            SelectScript = 0x00400000,
            NoVerticalFonts = 0x01000000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ChooseFontData
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDC;
            public IntPtr lpLogFont;
            public int iPointSize;
            public ChooseFontFlags Flags;
            public int rgbColors;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr hInstance;
            public IntPtr lpszStyle;
            public short nFontType;
            public short alignment;
            public int nSizeMin;
            public int nSizeMax;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LogFont
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }
    }
}

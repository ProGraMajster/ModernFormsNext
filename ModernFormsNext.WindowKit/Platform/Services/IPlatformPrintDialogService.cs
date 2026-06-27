using System;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Platform.Services
{
    /// <summary>
    /// Provides platform-specific print and page setup dialog support for shared ModernFormsNext dialog APIs.
    /// </summary>
    /// <remarks>
    /// Implementations should show native or backend-appropriate printing UI for the current
    /// platform. The service receives and returns portable settings data so that shared framework
    /// code does not depend on Win32 handles, native structures, or backend types.
    /// </remarks>
    public interface IPlatformPrintDialogService
    {
        /// <summary>
        /// Shows a modal print dialog owned by the specified window.
        /// </summary>
        /// <param name="owner">The platform window that owns the dialog.</param>
        /// <param name="request">The initial printer settings and option state for the dialog.</param>
        /// <returns>
        /// A task whose result is the selected printer settings, or <see langword="null"/> when the user cancels.
        /// </returns>
        Task<PlatformPrintDialogResult?> ShowPrintDialogAsync(IWindowBaseImpl owner, PlatformPrintDialogRequest request);

        /// <summary>
        /// Shows a modal page setup dialog owned by the specified window.
        /// </summary>
        /// <param name="owner">The platform window that owns the dialog.</param>
        /// <param name="request">The initial page, printer, margin, and option state for the dialog.</param>
        /// <returns>
        /// A task whose result is the selected page and printer settings, or <see langword="null"/> when the user cancels.
        /// </returns>
        Task<PlatformPageSetupDialogResult?> ShowPageSetupDialogAsync(IWindowBaseImpl owner, PlatformPageSetupDialogRequest request);
    }

    /// <summary>
    /// Specifies which pages a platform print dialog should print.
    /// </summary>
    public enum PlatformPrintRange
    {
        /// <summary>
        /// Print every page.
        /// </summary>
        AllPages,

        /// <summary>
        /// Print the current selection.
        /// </summary>
        Selection,

        /// <summary>
        /// Print a user-specified page range.
        /// </summary>
        SomePages,

        /// <summary>
        /// Print the current page.
        /// </summary>
        CurrentPage
    }

    /// <summary>
    /// Contains portable printer settings for platform dialog services.
    /// </summary>
    public sealed class PlatformPrinterSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the selected printer can duplex.
        /// </summary>
        public bool CanDuplex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether copies should be collated.
        /// </summary>
        public bool Collate { get; set; }

        /// <summary>
        /// Gets or sets the number of copies.
        /// </summary>
        public short Copies { get; set; } = 1;

        /// <summary>
        /// Gets or sets the WinForms-compatible duplex value.
        /// </summary>
        public int Duplex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the first page in a page range.
        /// </summary>
        public int FromPage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the selected printer is a plotter.
        /// </summary>
        public bool IsPlotter { get; set; }

        /// <summary>
        /// Gets or sets the landscape rotation angle in degrees.
        /// </summary>
        public int LandscapeAngle { get; set; } = 90;

        /// <summary>
        /// Gets or sets the maximum number of copies.
        /// </summary>
        public int MaximumCopies { get; set; } = 999;

        /// <summary>
        /// Gets or sets the highest page number allowed by the dialog.
        /// </summary>
        public int MaximumPage { get; set; } = 9999;

        /// <summary>
        /// Gets or sets the lowest page number allowed by the dialog.
        /// </summary>
        public int MinimumPage { get; set; }

        /// <summary>
        /// Gets or sets the file name requested for print-to-file output.
        /// </summary>
        public string PrintFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested print range.
        /// </summary>
        public PlatformPrintRange PrintRange { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether print-to-file output is selected.
        /// </summary>
        public bool PrintToFile { get; set; }

        /// <summary>
        /// Gets or sets the selected printer name.
        /// </summary>
        public string PrinterName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the selected printer supports color.
        /// </summary>
        public bool SupportsColor { get; set; } = true;

        /// <summary>
        /// Gets or sets the last page in a page range.
        /// </summary>
        public int ToPage { get; set; }
    }

    /// <summary>
    /// Contains portable page settings for platform page setup dialog services.
    /// </summary>
    public sealed class PlatformPageSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether color output is requested.
        /// </summary>
        public bool Color { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the page is landscape.
        /// </summary>
        public bool Landscape { get; set; }

        /// <summary>
        /// Gets or sets the page margins in hundredths of an inch.
        /// </summary>
        public PlatformMargins Margins { get; set; } = new();

        /// <summary>
        /// Gets or sets the selected paper size.
        /// </summary>
        public PlatformPaperSize PaperSize { get; set; } = new();

        /// <summary>
        /// Gets or sets the selected paper source.
        /// </summary>
        public PlatformPaperSource PaperSource { get; set; } = new();
    }

    /// <summary>
    /// Represents page margins in hundredths of an inch.
    /// </summary>
    public sealed class PlatformMargins
    {
        /// <summary>
        /// Gets or sets the left margin.
        /// </summary>
        public int Left { get; set; } = 100;

        /// <summary>
        /// Gets or sets the right margin.
        /// </summary>
        public int Right { get; set; } = 100;

        /// <summary>
        /// Gets or sets the top margin.
        /// </summary>
        public int Top { get; set; } = 100;

        /// <summary>
        /// Gets or sets the bottom margin.
        /// </summary>
        public int Bottom { get; set; } = 100;
    }

    /// <summary>
    /// Describes a platform-neutral paper size in hundredths of an inch.
    /// </summary>
    public sealed class PlatformPaperSize
    {
        /// <summary>
        /// Gets or sets the WinForms-compatible paper kind value.
        /// </summary>
        public int Kind { get; set; } = 1;

        /// <summary>
        /// Gets or sets the paper display name.
        /// </summary>
        public string Name { get; set; } = "Letter";

        /// <summary>
        /// Gets or sets the paper width in hundredths of an inch.
        /// </summary>
        public int Width { get; set; } = 850;

        /// <summary>
        /// Gets or sets the paper height in hundredths of an inch.
        /// </summary>
        public int Height { get; set; } = 1100;
    }

    /// <summary>
    /// Describes a platform-neutral paper source.
    /// </summary>
    public sealed class PlatformPaperSource
    {
        /// <summary>
        /// Gets or sets the WinForms-compatible paper source kind value.
        /// </summary>
        public int Kind { get; set; } = 7;

        /// <summary>
        /// Gets or sets the paper source display name.
        /// </summary>
        public string Name { get; set; } = "Automatic";
    }

    /// <summary>
    /// Contains option data for a platform print dialog request.
    /// </summary>
    public sealed class PlatformPrintDialogRequest
    {
        /// <summary>
        /// Gets or sets a value indicating whether the Current Page option is enabled.
        /// </summary>
        public bool AllowCurrentPage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether print-to-file selection is enabled.
        /// </summary>
        public bool AllowPrintToFile { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Selection option is enabled.
        /// </summary>
        public bool AllowSelection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Pages option is enabled.
        /// </summary>
        public bool AllowSomePages { get; set; }

        /// <summary>
        /// Gets or sets the document name shown by the platform dialog when supported.
        /// </summary>
        public string DocumentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the callback invoked when the platform dialog reports a help request.
        /// </summary>
        public Action? HelpRequest { get; set; }

        /// <summary>
        /// Gets or sets the printer settings edited by the dialog.
        /// </summary>
        public PlatformPrinterSettings PrinterSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the dialog should display a Help button.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether network printer UI should be shown.
        /// </summary>
        public bool ShowNetwork { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether a Windows extended print dialog is preferred.
        /// </summary>
        public bool UseExtendedDialog { get; set; }
    }

    /// <summary>
    /// Contains option data for a platform page setup dialog request.
    /// </summary>
    public sealed class PlatformPageSetupDialogRequest
    {
        /// <summary>
        /// Gets or sets a value indicating whether the margin fields are enabled.
        /// </summary>
        public bool AllowMargins { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether orientation selection is enabled.
        /// </summary>
        public bool AllowOrientation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether paper selection is enabled.
        /// </summary>
        public bool AllowPaper { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether printer selection is enabled.
        /// </summary>
        public bool AllowPrinter { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether metric units should be preferred by the platform dialog.
        /// </summary>
        public bool EnableMetric { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the platform dialog reports a help request.
        /// </summary>
        public Action? HelpRequest { get; set; }

        /// <summary>
        /// Gets or sets the minimum margins in hundredths of an inch.
        /// </summary>
        public PlatformMargins MinMargins { get; set; } = new() { Left = 0, Right = 0, Top = 0, Bottom = 0 };

        /// <summary>
        /// Gets or sets the page settings edited by the dialog.
        /// </summary>
        public PlatformPageSettings PageSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the printer settings edited by the dialog.
        /// </summary>
        public PlatformPrinterSettings PrinterSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the dialog should display a Help button.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether network printer UI should be shown.
        /// </summary>
        public bool ShowNetwork { get; set; } = true;
    }

    /// <summary>
    /// Contains the result returned by a platform print dialog.
    /// </summary>
    /// <param name="PrinterSettings">The selected printer settings.</param>
    public sealed record PlatformPrintDialogResult(PlatformPrinterSettings PrinterSettings);

    /// <summary>
    /// Contains the result returned by a platform page setup dialog.
    /// </summary>
    /// <param name="PageSettings">The selected page settings.</param>
    /// <param name="PrinterSettings">The selected printer settings.</param>
    public sealed record PlatformPageSetupDialogResult(
        PlatformPageSettings PageSettings,
        PlatformPrinterSettings PrinterSettings);
}

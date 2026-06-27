using System;
using System.Collections.ObjectModel;

namespace ModernFormsNext
{
    /// <summary>
    /// Contains printer and page-range settings used by print dialogs and print documents.
    /// </summary>
    /// <remarks>
    /// This type intentionally mirrors the most commonly used WinForms <c>PrinterSettings</c>
    /// members while remaining platform-neutral. It does not enumerate physical printers yet;
    /// backend-specific printer discovery and spooling can be added behind WindowKit services later.
    /// </remarks>
    public class PrinterSettings : ICloneable
    {
        private const string DefaultPrinterName = "Default printer";

        private short copies = 1;
        private int fromPage;
        private int toPage;
        private int minimumPage;
        private int maximumPage = 9999;
        private string printerName = DefaultPrinterName;

        /// <summary>
        /// Gets a read-only list of known printer names.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not yet expose platform printer enumeration, so this collection
        /// currently contains a single logical default printer entry.
        /// </remarks>
        public static ReadOnlyCollection<string> InstalledPrinters { get; } = Array.AsReadOnly(new[] { DefaultPrinterName });

        /// <summary>
        /// Gets a value indicating whether the selected printer can duplex.
        /// </summary>
        public bool CanDuplex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether copies should be collated.
        /// </summary>
        public bool Collate { get; set; }

        /// <summary>
        /// Gets or sets the number of copies to print.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than one.</exception>
        public short Copies {
            get => copies;
            set {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, (short)1);
                copies = value;
            }
        }

        /// <summary>
        /// Gets a default page settings object associated with these printer settings.
        /// </summary>
        public PageSettings DefaultPageSettings => new PageSettings(this);

        /// <summary>
        /// Gets or sets duplex printing behavior.
        /// </summary>
        public Duplex Duplex { get; set; } = Duplex.Default;

        /// <summary>
        /// Gets or sets the first page in a page range.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than zero.</exception>
        public int FromPage {
            get => fromPage;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                fromPage = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the selected printer is the default printer.
        /// </summary>
        public bool IsDefaultPrinter => string.Equals(PrinterName, DefaultPrinterName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a value indicating whether the selected printer is a plotter.
        /// </summary>
        public bool IsPlotter { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current logical printer settings are valid.
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(PrinterName);

        /// <summary>
        /// Gets the landscape rotation angle in degrees.
        /// </summary>
        public int LandscapeAngle { get; set; } = 90;

        /// <summary>
        /// Gets or sets the highest page number accepted by the dialog.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than zero.</exception>
        public int MaximumPage {
            get => maximumPage;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                maximumPage = value;
            }
        }

        /// <summary>
        /// Gets the maximum number of copies accepted by the settings object.
        /// </summary>
        public int MaximumCopies { get; set; } = 999;

        /// <summary>
        /// Gets or sets the lowest page number accepted by the dialog.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than zero.</exception>
        public int MinimumPage {
            get => minimumPage;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                minimumPage = value;
            }
        }

        /// <summary>
        /// Gets or sets the file name requested when printing to a file.
        /// </summary>
        public string PrintFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested print range.
        /// </summary>
        public PrintRange PrintRange { get; set; } = PrintRange.AllPages;

        /// <summary>
        /// Gets or sets a value indicating whether the user requested printing to a file.
        /// </summary>
        public bool PrintToFile { get; set; }

        /// <summary>
        /// Gets or sets the selected printer name.
        /// </summary>
        public string PrinterName {
            get => printerName;
            set => printerName = value ?? string.Empty;
        }

        /// <summary>
        /// Gets a value indicating whether the selected printer supports color.
        /// </summary>
        public bool SupportsColor { get; set; } = true;

        /// <summary>
        /// Gets or sets the last page in a page range.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than zero.</exception>
        public int ToPage {
            get => toPage;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                toPage = value;
            }
        }

        /// <summary>
        /// Creates a copy of this printer settings object.
        /// </summary>
        /// <returns>A new <see cref="PrinterSettings"/> with the same values.</returns>
        public object Clone()
        {
            return new PrinterSettings
            {
                CanDuplex = CanDuplex,
                Collate = Collate,
                Copies = Copies,
                Duplex = Duplex,
                FromPage = FromPage,
                IsPlotter = IsPlotter,
                LandscapeAngle = LandscapeAngle,
                MaximumCopies = MaximumCopies,
                MaximumPage = MaximumPage,
                MinimumPage = MinimumPage,
                PrintFileName = PrintFileName,
                PrintRange = PrintRange,
                PrintToFile = PrintToFile,
                PrinterName = PrinterName,
                SupportsColor = SupportsColor,
                ToPage = ToPage
            };
        }

        /// <inheritdoc/>
        public override string ToString() => $"[PrinterSettings PrinterName={PrinterName}, Copies={Copies}, PrintRange={PrintRange}]";
    }
}

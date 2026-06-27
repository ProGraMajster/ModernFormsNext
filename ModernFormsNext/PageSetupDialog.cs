using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a dialog that edits page layout, paper, margin, and logical printer settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog edits the platform-neutral <see cref="PageSettings"/> and
    /// <see cref="PrinterSettings"/> models. By default it asks the active backend for a system
    /// page setup dialog, matching WinForms migration expectations on platforms that provide one.
    /// Set <see cref="RenderingMode"/> to <see cref="PrintingDialogRenderingMode.ModernFormsNext"/>
    /// to force the framework-rendered dialog composed from ModernFormsNext controls.
    /// </para>
    /// <para>
    /// Values are stored in hundredths of an inch, matching WinForms printing APIs. When
    /// <see cref="EnableMetric"/> is enabled, the dialog displays margins in millimeters and
    /// converts them back to hundredths of an inch when the user accepts the dialog.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var document = new PrintDocument();
    /// var dialog = new PageSetupDialog { Document = document };
    ///
    /// if (await dialog.ShowDialog(this) == DialogResult.OK)
    /// {
    ///     document.DefaultPageSettings = dialog.PageSettings!;
    /// }
    /// </code>
    /// </example>
    public class PageSetupDialog : CommonDialog
    {
        private PrintDocument? document;
        private Margins minMargins = new (0, 0, 0, 0);
        private PageSettings? pageSettings;
        private PrinterSettings? printerSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageSetupDialog"/> class.
        /// </summary>
        public PageSetupDialog()
        {
            Reset();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the margin fields are enabled.
        /// </summary>
        [DefaultValue(true)]
        public bool AllowMargins { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether portrait and landscape orientation can be changed.
        /// </summary>
        [DefaultValue(true)]
        public bool AllowOrientation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether paper size and paper source fields are enabled.
        /// </summary>
        [DefaultValue(true)]
        public bool AllowPaper { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether printer settings can be changed.
        /// </summary>
        [DefaultValue(true)]
        public bool AllowPrinter { get; set; }

        /// <summary>
        /// Gets or sets the document whose page and printer settings should be edited.
        /// </summary>
        /// <remarks>
        /// Assigning a document also selects <see cref="PrintDocument.DefaultPageSettings"/> and
        /// <see cref="PrintDocument.PrinterSettings"/> as the settings edited by the dialog.
        /// Assigning <see cref="PageSettings"/> or <see cref="PrinterSettings"/> later clears
        /// this association, matching WinForms-style ownership semantics.
        /// </remarks>
        public PrintDocument? Document {
            get => document;
            set {
                document = value;

                if (document is not null) {
                    pageSettings = document.DefaultPageSettings;
                    printerSettings = document.PrinterSettings;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays margin values in millimeters.
        /// </summary>
        /// <remarks>
        /// The underlying <see cref="Margins"/> values remain stored in hundredths of an inch.
        /// This property only changes the units shown to the user in the managed dialog.
        /// </remarks>
        [DefaultValue(false)]
        public bool EnableMetric { get; set; }

        /// <summary>
        /// Gets or sets the minimum margins the user is allowed to choose.
        /// </summary>
        /// <remarks>
        /// The values are stored in hundredths of an inch. Assigning <see langword="null"/>
        /// resets the minimum margins to zero on every side.
        /// </remarks>
        public Margins? MinMargins {
            get => minMargins;
            set => minMargins = value ?? new Margins(0, 0, 0, 0);
        }

        /// <summary>
        /// Gets or sets the page settings edited by the dialog.
        /// </summary>
        /// <remarks>
        /// Assigning this property clears <see cref="Document"/> so the dialog edits the supplied
        /// standalone settings object instead of a document-owned settings object.
        /// </remarks>
        public PageSettings? PageSettings {
            get => pageSettings;
            set {
                pageSettings = value;
                document = null;
            }
        }

        /// <summary>
        /// Gets or sets the printer settings edited by the dialog.
        /// </summary>
        /// <remarks>
        /// Assigning this property clears <see cref="Document"/> so the dialog edits the supplied
        /// standalone settings object instead of a document-owned settings object.
        /// </remarks>
        public PrinterSettings? PrinterSettings {
            get => printerSettings;
            set {
                printerSettings = value;
                document = null;
            }
        }

        /// <summary>
        /// Gets or sets how the page setup dialog user interface is rendered.
        /// </summary>
        /// <value>
        /// One of the <see cref="PrintingDialogRenderingMode"/> values. The default is
        /// <see cref="PrintingDialogRenderingMode.System"/> to preserve native-dialog behavior
        /// for WinForms migration scenarios.
        /// </value>
        /// <remarks>
        /// <para>
        /// <see cref="PrintingDialogRenderingMode.System"/> uses the active platform backend and
        /// may expose platform-specific behavior such as the Windows common page setup dialog.
        /// If the backend does not provide a system print dialog service, this mode throws
        /// <see cref="PlatformNotSupportedException"/>.
        /// </para>
        /// <para>
        /// <see cref="PrintingDialogRenderingMode.ModernFormsNext"/> uses a dialog composed from
        /// ModernFormsNext controls. It visually matches the framework and is available on every
        /// backend that can show ModernFormsNext forms.
        /// </para>
        /// </remarks>
        [DefaultValue(PrintingDialogRenderingMode.System)]
        public PrintingDialogRenderingMode RenderingMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays a Help button.
        /// </summary>
        [DefaultValue(false)]
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether network printer UI should be shown.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not yet enumerate platform printers, so this property is currently
        /// stored for WinForms migration compatibility and has no visual effect.
        /// </remarks>
        [DefaultValue(true)]
        public bool ShowNetwork { get; set; }

        /// <inheritdoc/>
        public override void Reset()
        {
            AllowMargins = true;
            AllowOrientation = true;
            AllowPaper = true;
            AllowPrinter = true;
            EnableMetric = false;
            minMargins = new Margins(0, 0, 0, 0);
            pageSettings = null;
            document = null;
            printerSettings = null;
            RenderingMode = PrintingDialogRenderingMode.System;
            ShowHelp = false;
            ShowNetwork = true;
        }

        /// <inheritdoc/>
        protected override async Task<DialogResult> RunDialog(Form owner)
        {
            var targetPrinterSettings = PrinterSettings ?? Document?.PrinterSettings ?? PageSettings?.PrinterSettings ?? new PrinterSettings();
            var targetPageSettings = PageSettings ?? Document?.DefaultPageSettings ?? new PageSettings(targetPrinterSettings);
            targetPageSettings.PrinterSettings ??= targetPrinterSettings;

            return RenderingMode switch
            {
                PrintingDialogRenderingMode.Auto => await RunAutoDialog(owner, targetPageSettings, targetPrinterSettings),
                PrintingDialogRenderingMode.System => await RunSystemDialog(owner, targetPageSettings, targetPrinterSettings),
                PrintingDialogRenderingMode.ModernFormsNext => await RunModernFormsDialog(owner, targetPageSettings, targetPrinterSettings),
                _ => throw new InvalidEnumArgumentException(nameof(RenderingMode), (int)RenderingMode, typeof(PrintingDialogRenderingMode))
            };
        }

        private async Task<DialogResult> RunAutoDialog(Form owner, PageSettings targetPageSettings, PrinterSettings targetPrinterSettings)
        {
            FrameworkBootstrap.EnsureInitialized();

            if (WindowKit.AvaloniaGlobals.GetService<IPlatformPrintDialogService>() is null)
                return await RunModernFormsDialog(owner, targetPageSettings, targetPrinterSettings);

            return await RunSystemDialog(owner, targetPageSettings, targetPrinterSettings);
        }

        private async Task<DialogResult> RunSystemDialog(Form owner, PageSettings targetPageSettings, PrinterSettings targetPrinterSettings)
        {
            FrameworkBootstrap.EnsureInitialized();

            var service = WindowKit.AvaloniaGlobals.GetService<IPlatformPrintDialogService>();

            if (service is null)
                throw new PlatformNotSupportedException("The current platform backend does not provide a page setup dialog service.");

            var result = await service.ShowPageSetupDialogAsync(owner.window, CreatePlatformRequest(targetPageSettings, targetPrinterSettings));

            if (result is null)
                return DialogResult.Cancel;

            PrintingPlatformConversions.ApplyPlatformPageSettings(result.PageSettings, targetPageSettings);
            PrintingPlatformConversions.ApplyPlatformPrinterSettings(result.PrinterSettings, targetPrinterSettings);
            SaveAcceptedSettings(targetPageSettings, targetPrinterSettings);

            return DialogResult.OK;
        }

        private async Task<DialogResult> RunModernFormsDialog(Form owner, PageSettings targetPageSettings, PrinterSettings targetPrinterSettings)
        {
            var form = new PageSetupDialogForm(
                targetPageSettings,
                targetPrinterSettings,
                MinMargins ?? new Margins(0, 0, 0, 0),
                AllowMargins,
                AllowOrientation,
                AllowPaper,
                AllowPrinter,
                EnableMetric,
                ShowHelp,
                () => OnHelpRequest(EventArgs.Empty));

            var result = await form.ShowDialog(owner);

            if (result == DialogResult.OK) {
                CopyPageSettings(form.PageSettings, targetPageSettings);
                CopyPrinterSettings(form.PrinterSettings, targetPrinterSettings);
                SaveAcceptedSettings(targetPageSettings, targetPrinterSettings);
            }

            return result;
        }

        private PlatformPageSetupDialogRequest CreatePlatformRequest(PageSettings targetPageSettings, PrinterSettings targetPrinterSettings)
        {
            return new PlatformPageSetupDialogRequest
            {
                AllowMargins = AllowMargins,
                AllowOrientation = AllowOrientation,
                AllowPaper = AllowPaper,
                AllowPrinter = AllowPrinter,
                EnableMetric = EnableMetric,
                HelpRequest = () => OnHelpRequest(EventArgs.Empty),
                MinMargins = PrintingPlatformConversions.ToPlatformMargins(MinMargins ?? new Margins(0, 0, 0, 0)),
                PageSettings = PrintingPlatformConversions.ToPlatformPageSettings(targetPageSettings),
                PrinterSettings = PrintingPlatformConversions.ToPlatformPrinterSettings(targetPrinterSettings),
                ShowHelp = ShowHelp,
                ShowNetwork = ShowNetwork
            };
        }

        private void SaveAcceptedSettings(PageSettings acceptedPageSettings, PrinterSettings acceptedPrinterSettings)
        {
            acceptedPageSettings.PrinterSettings = acceptedPrinterSettings;

            if (document is not null) {
                document.DefaultPageSettings = acceptedPageSettings;
                document.PrinterSettings = acceptedPrinterSettings;
            }

            pageSettings = acceptedPageSettings;
            printerSettings = acceptedPrinterSettings;
        }

        private static void CopyPageSettings(PageSettings source, PageSettings target)
        {
            target.Color = source.Color;
            target.Landscape = source.Landscape;
            target.Margins = (Margins)source.Margins.Clone();
            target.PaperSize = new PaperSize(source.PaperSize.PaperName, source.PaperSize.Width, source.PaperSize.Height) { Kind = source.PaperSize.Kind };
            target.PaperSource = new PaperSource { Kind = source.PaperSource.Kind, SourceName = source.PaperSource.SourceName };
            target.PrinterResolution = new PrinterResolution { Kind = source.PrinterResolution.Kind, X = source.PrinterResolution.X, Y = source.PrinterResolution.Y };
        }

        private static void CopyPrinterSettings(PrinterSettings source, PrinterSettings target)
        {
            target.CanDuplex = source.CanDuplex;
            target.Collate = source.Collate;
            target.Copies = source.Copies;
            target.Duplex = source.Duplex;
            target.FromPage = source.FromPage;
            target.IsPlotter = source.IsPlotter;
            target.LandscapeAngle = source.LandscapeAngle;
            target.MaximumCopies = source.MaximumCopies;
            target.MaximumPage = source.MaximumPage;
            target.MinimumPage = source.MinimumPage;
            target.PrintFileName = source.PrintFileName;
            target.PrintRange = source.PrintRange;
            target.PrintToFile = source.PrintToFile;
            target.PrinterName = source.PrinterName;
            target.SupportsColor = source.SupportsColor;
            target.ToPage = source.ToPage;
        }
    }

    internal sealed class PageSetupDialogForm : Form
    {
        private static readonly PaperSize[] StandardPaperSizes =
        {
            PaperSize.FromKind(PaperKind.Letter),
            PaperSize.FromKind(PaperKind.Legal),
            PaperSize.FromKind(PaperKind.A4),
            PaperSize.FromKind(PaperKind.A5)
        };

        private readonly Action? helpRequest;
        private readonly bool metricUnits;
        private readonly RadioButton portraitRadio;
        private readonly RadioButton landscapeRadio;
        private readonly NumericUpDown leftMarginBox;
        private readonly NumericUpDown rightMarginBox;
        private readonly NumericUpDown topMarginBox;
        private readonly NumericUpDown bottomMarginBox;
        private readonly ComboBox paperSizeBox;
        private readonly NumericUpDown paperWidthBox;
        private readonly NumericUpDown paperHeightBox;
        private readonly TextBox paperSourceBox;
        private readonly CheckBox colorBox;
        private readonly TextBox printerNameBox;
        private readonly NumericUpDown copiesBox;

        public PageSetupDialogForm(
            PageSettings initialPageSettings,
            PrinterSettings initialPrinterSettings,
            Margins minMargins,
            bool allowMargins,
            bool allowOrientation,
            bool allowPaper,
            bool allowPrinter,
            bool enableMetric,
            bool showHelp,
            Action? helpRequest)
        {
            ArgumentNullException.ThrowIfNull(initialPageSettings);
            ArgumentNullException.ThrowIfNull(initialPrinterSettings);
            ArgumentNullException.ThrowIfNull(minMargins);

            this.helpRequest = helpRequest;
            metricUnits = enableMetric;
            PageSettings = (PageSettings)initialPageSettings.Clone();
            PrinterSettings = (PrinterSettings)initialPrinterSettings.Clone();
            PageSettings.PrinterSettings = PrinterSettings;

            Text = "Page Setup";
            Size = new Size(600, 475);
            StartPosition = FormStartPosition.CenterParent;
            Resizeable = false;
            AllowMaximize = false;
            AllowMinimize = false;

            var orientationPanel = Controls.Add(new Panel
            {
                Location = new Point(20, 38),
                Size = new Size(260, 80),
                Enabled = allowOrientation
            });
            orientationPanel.Style.Border.Width = 1;
            orientationPanel.Style.Border.Color = Theme.BorderLowColor;
            orientationPanel.Controls.Add(CreateLabel("Orientation", 12, 8, 120));

            portraitRadio = orientationPanel.Controls.Add(new RadioButton
            {
                Location = new Point(16, 42),
                Size = new Size(90, 24),
                Text = "Portrait",
                Checked = !PageSettings.Landscape
            });

            landscapeRadio = orientationPanel.Controls.Add(new RadioButton
            {
                Location = new Point(128, 42),
                Size = new Size(110, 24),
                Text = "Landscape",
                Checked = PageSettings.Landscape
            });

            var printerPanel = Controls.Add(new Panel
            {
                Location = new Point(300, 38),
                Size = new Size(260, 80),
                Enabled = allowPrinter
            });
            printerPanel.Style.Border.Width = 1;
            printerPanel.Style.Border.Color = Theme.BorderLowColor;
            printerPanel.Controls.Add(CreateLabel("Printer", 12, 8, 120));
            printerPanel.Controls.Add(CreateLabel("Name", 16, 38, 45));
            printerNameBox = printerPanel.Controls.Add(new TextBox
            {
                Location = new Point(66, 34),
                Size = new Size(174, 28),
                Text = PrinterSettings.PrinterName
            });

            var marginsPanel = Controls.Add(new Panel
            {
                Location = new Point(20, 135),
                Size = new Size(260, 180),
                Enabled = allowMargins
            });
            marginsPanel.Style.Border.Width = 1;
            marginsPanel.Style.Border.Color = Theme.BorderLowColor;
            marginsPanel.Controls.Add(CreateLabel(metricUnits ? "Margins (mm)" : "Margins (1/100 in)", 12, 8, 180));

            leftMarginBox = CreateMarginBox(marginsPanel, "Left", 16, 38, PageSettings.Margins.Left, minMargins.Left);
            rightMarginBox = CreateMarginBox(marginsPanel, "Right", 16, 72, PageSettings.Margins.Right, minMargins.Right);
            topMarginBox = CreateMarginBox(marginsPanel, "Top", 16, 106, PageSettings.Margins.Top, minMargins.Top);
            bottomMarginBox = CreateMarginBox(marginsPanel, "Bottom", 16, 140, PageSettings.Margins.Bottom, minMargins.Bottom);

            var paperPanel = Controls.Add(new Panel
            {
                Location = new Point(300, 135),
                Size = new Size(260, 180),
                Enabled = allowPaper
            });
            paperPanel.Style.Border.Width = 1;
            paperPanel.Style.Border.Color = Theme.BorderLowColor;
            paperPanel.Controls.Add(CreateLabel("Paper", 12, 8, 120));
            paperPanel.Controls.Add(CreateLabel("Size", 16, 40, 50));

            paperSizeBox = paperPanel.Controls.Add(new ComboBox
            {
                Location = new Point(78, 36),
                Size = new Size(160, 28)
            });
            foreach (var paperSize in StandardPaperSizes)
                paperSizeBox.Items.Add(paperSize.PaperName);

            paperSizeBox.Items.Add("Custom");
            paperSizeBox.SelectedIndex = GetPaperSizeIndex(PageSettings.PaperSize);
            paperSizeBox.SelectedIndexChanged += (_, _) => UpdateCustomPaperFields();

            paperPanel.Controls.Add(CreateLabel("Width", 16, 74, 50));
            paperWidthBox = paperPanel.Controls.Add(new NumericUpDown
            {
                Location = new Point(78, 70),
                Size = new Size(84, 28),
                Minimum = 1,
                Maximum = 10000,
                Value = PageSettings.PaperSize.Width
            });

            paperPanel.Controls.Add(CreateLabel("Height", 16, 108, 50));
            paperHeightBox = paperPanel.Controls.Add(new NumericUpDown
            {
                Location = new Point(78, 104),
                Size = new Size(84, 28),
                Minimum = 1,
                Maximum = 10000,
                Value = PageSettings.PaperSize.Height
            });

            paperPanel.Controls.Add(CreateLabel("Source", 16, 142, 56));
            paperSourceBox = paperPanel.Controls.Add(new TextBox
            {
                Location = new Point(78, 138),
                Size = new Size(160, 28),
                Text = PageSettings.PaperSource.SourceName
            });

            Controls.Add(CreateLabel("Copies", 300, 332, 75));
            copiesBox = Controls.Add(new NumericUpDown
            {
                Location = new Point(375, 328),
                Size = new Size(85, 28),
                Minimum = 1,
                Maximum = Math.Max(1, PrinterSettings.MaximumCopies),
                Value = PrinterSettings.Copies,
                Enabled = allowPrinter
            });

            colorBox = Controls.Add(new CheckBox
            {
                Location = new Point(20, 330),
                Size = new Size(130, 24),
                Text = "Color",
                Checked = PageSettings.Color
            });

            var okButton = Controls.Add(new Button
            {
                Location = new Point(300, 395),
                Size = new Size(80, 30),
                Text = "OK"
            });

            var cancelButton = Controls.Add(new Button
            {
                Location = new Point(390, 395),
                Size = new Size(80, 30),
                Text = "Cancel"
            });

            var helpButton = Controls.Add(new Button
            {
                Location = new Point(480, 395),
                Size = new Size(80, 30),
                Text = "Help",
                Visible = showHelp
            });

            okButton.Click += (_, _) =>
            {
                ApplySettings();
                DialogResult = DialogResult.OK;
                Close();
            };
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            helpButton.Click += (_, _) => this.helpRequest?.Invoke();

            UpdateCustomPaperFields();
        }

        public PageSettings PageSettings { get; }

        public PrinterSettings PrinterSettings { get; }

        private void ApplySettings()
        {
            PageSettings.Landscape = landscapeRadio.Checked;
            PageSettings.Color = colorBox.Checked;
            PageSettings.Margins = new Margins(
                FromDisplayUnits(leftMarginBox.Value),
                FromDisplayUnits(rightMarginBox.Value),
                FromDisplayUnits(topMarginBox.Value),
                FromDisplayUnits(bottomMarginBox.Value));

            PageSettings.PaperSize = GetSelectedPaperSize();
            PageSettings.PaperSource = new PaperSource
            {
                Kind = string.Equals(paperSourceBox.Text, "Automatic", StringComparison.OrdinalIgnoreCase)
                    ? PaperSourceKind.AutomaticFeed
                    : PaperSourceKind.Custom,
                SourceName = paperSourceBox.Text
            };

            PrinterSettings.PrinterName = printerNameBox.Text;
            PrinterSettings.Copies = (short)Math.Round(copiesBox.Value);
        }

        private NumericUpDown CreateMarginBox(Control parent, string label, int x, int y, int value, int minimum)
        {
            parent.Controls.Add(CreateLabel(label, x, y + 4, 70));

            return parent.Controls.Add(new NumericUpDown
            {
                Location = new Point(x + 90, y),
                Size = new Size(90, 28),
                AllowDecimalValues = metricUnits,
                DecimalPlaces = metricUnits ? 1 : 0,
                Increment = metricUnits ? 1m : 10m,
                Minimum = ToDisplayUnits(minimum),
                Maximum = metricUnits ? 2500m : 10000m,
                Value = ToDisplayUnits(Math.Max(value, minimum))
            });
        }

        private PaperSize GetSelectedPaperSize()
        {
            if (paperSizeBox.SelectedIndex >= 0 && paperSizeBox.SelectedIndex < StandardPaperSizes.Length) {
                var selected = StandardPaperSizes[paperSizeBox.SelectedIndex];
                return new PaperSize(selected.PaperName, selected.Width, selected.Height) { Kind = selected.Kind };
            }

            return new PaperSize("Custom", (int)Math.Round(paperWidthBox.Value), (int)Math.Round(paperHeightBox.Value));
        }

        private static int GetPaperSizeIndex(PaperSize paperSize)
        {
            for (var i = 0; i < StandardPaperSizes.Length; i++) {
                if (StandardPaperSizes[i].Kind == paperSize.Kind)
                    return i;
            }

            return StandardPaperSizes.Length;
        }

        private void UpdateCustomPaperFields()
        {
            var custom = paperSizeBox.SelectedIndex == StandardPaperSizes.Length;
            paperWidthBox.Enabled = custom;
            paperHeightBox.Enabled = custom;

            if (!custom && paperSizeBox.SelectedIndex >= 0 && paperSizeBox.SelectedIndex < StandardPaperSizes.Length) {
                paperWidthBox.Value = StandardPaperSizes[paperSizeBox.SelectedIndex].Width;
                paperHeightBox.Value = StandardPaperSizes[paperSizeBox.SelectedIndex].Height;
            }
        }

        private decimal ToDisplayUnits(int hundredthsOfInch)
        {
            if (!metricUnits)
                return hundredthsOfInch;

            return Math.Round(hundredthsOfInch * 0.254m, 1);
        }

        private int FromDisplayUnits(decimal value)
        {
            if (!metricUnits)
                return (int)Math.Round(value);

            return (int)Math.Round(value / 0.254m);
        }

        private static Label CreateLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Location = new Point(x, y),
                Size = new Size(width, 22),
                Text = text
            };
        }
    }
}

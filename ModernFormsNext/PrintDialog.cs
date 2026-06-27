using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a dialog that lets the user choose print settings for a <see cref="PrintDocument"/>.
    /// </summary>
    /// <remarks>
    /// The dialog edits the platform-neutral <see cref="PrinterSettings"/> model. By default it
    /// asks the active backend for a system print dialog, matching WinForms migration expectations
    /// on platforms that provide one. Set <see cref="RenderingMode"/> to
    /// <see cref="PrintingDialogRenderingMode.ModernFormsNext"/> to force the framework-rendered
    /// dialog composed from ModernFormsNext controls.
    /// </remarks>
    public class PrintDialog : CommonDialog
    {
        private PrinterSettings printerSettings = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="PrintDialog"/> class.
        /// </summary>
        public PrintDialog()
        {
            Reset();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the Current Page option is enabled.
        /// </summary>
        [DefaultValue(false)]
        public bool AllowCurrentPage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether printing to a file can be selected.
        /// </summary>
        [DefaultValue(true)]
        public bool AllowPrintToFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Selection option is enabled.
        /// </summary>
        [DefaultValue(false)]
        public bool AllowSelection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Pages range option is enabled.
        /// </summary>
        [DefaultValue(false)]
        public bool AllowSomePages { get; set; }

        /// <summary>
        /// Gets or sets the document associated with the dialog.
        /// </summary>
        public PrintDocument? Document { get; set; }

        /// <summary>
        /// Gets or sets how the print dialog user interface is rendered.
        /// </summary>
        /// <value>
        /// One of the <see cref="PrintingDialogRenderingMode"/> values. The default is
        /// <see cref="PrintingDialogRenderingMode.System"/> to preserve native-dialog behavior
        /// for WinForms migration scenarios.
        /// </value>
        /// <remarks>
        /// <para>
        /// <see cref="PrintingDialogRenderingMode.System"/> uses the active platform backend and
        /// may expose platform-specific behavior such as the Windows common print dialog. If the
        /// backend does not provide a system print dialog, this mode throws
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
        /// Gets or sets a value indicating whether printing to a file is selected.
        /// </summary>
        [DefaultValue(false)]
        public bool PrintToFile {
            get => PrinterSettings.PrintToFile;
            set => PrinterSettings.PrintToFile = value;
        }

        /// <summary>
        /// Gets or sets the printer settings edited by the dialog.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PrinterSettings PrinterSettings {
            get => Document?.PrinterSettings ?? printerSettings;
            set {
                ArgumentNullException.ThrowIfNull(value);
                printerSettings = value;

                if (Document is not null)
                    Document.PrinterSettings = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays a Help button.
        /// </summary>
        [DefaultValue(false)]
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether network printer selection should be shown.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not yet enumerate platform printers, so this property is currently
        /// stored for WinForms migration compatibility and has no visual effect.
        /// </remarks>
        [DefaultValue(true)]
        public bool ShowNetwork { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Windows extended dialog should be preferred.
        /// </summary>
        /// <remarks>
        /// This property is included for WinForms migration compatibility. System backends may use
        /// it as a hint when choosing between native dialog variants; the ModernFormsNext-rendered
        /// dialog stores the value but does not change its layout.
        /// </remarks>
        [DefaultValue(false)]
        public bool UseEXDialog { get; set; }

        /// <inheritdoc/>
        public override void Reset()
        {
            AllowCurrentPage = false;
            AllowPrintToFile = true;
            AllowSelection = false;
            AllowSomePages = false;
            ShowHelp = false;
            ShowNetwork = true;
            RenderingMode = PrintingDialogRenderingMode.System;
            UseEXDialog = false;
            printerSettings = new PrinterSettings();

            if (Document is not null)
                Document.PrinterSettings = printerSettings;
        }

        /// <inheritdoc/>
        protected override async Task<DialogResult> RunDialog(Form owner)
        {
            return RenderingMode switch
            {
                PrintingDialogRenderingMode.Auto => await RunAutoDialog(owner),
                PrintingDialogRenderingMode.System => await RunSystemDialog(owner),
                PrintingDialogRenderingMode.ModernFormsNext => await RunModernFormsDialog(owner),
                _ => throw new InvalidEnumArgumentException(nameof(RenderingMode), (int)RenderingMode, typeof(PrintingDialogRenderingMode))
            };
        }

        private async Task<DialogResult> RunAutoDialog(Form owner)
        {
            FrameworkBootstrap.EnsureInitialized();

            if (WindowKit.AvaloniaGlobals.GetService<IPlatformPrintDialogService>() is null)
                return await RunModernFormsDialog(owner);

            return await RunSystemDialog(owner);
        }

        private async Task<DialogResult> RunSystemDialog(Form owner)
        {
            FrameworkBootstrap.EnsureInitialized();

            var service = WindowKit.AvaloniaGlobals.GetService<IPlatformPrintDialogService>();

            if (service is null)
                throw new PlatformNotSupportedException("The current platform backend does not provide a print dialog service.");

            var result = await service.ShowPrintDialogAsync(owner.window, CreatePlatformRequest());

            if (result is null)
                return DialogResult.Cancel;

            PrintingPlatformConversions.ApplyPlatformPrinterSettings(result.PrinterSettings, PrinterSettings);
            return DialogResult.OK;
        }

        private async Task<DialogResult> RunModernFormsDialog(Form owner)
        {
            var form = new PrintDialogForm(
                PrinterSettings,
                AllowCurrentPage,
                AllowPrintToFile,
                AllowSelection,
                AllowSomePages,
                ShowHelp,
                () => OnHelpRequest(EventArgs.Empty));

            var result = await form.ShowDialog(owner);

            if (result == DialogResult.OK)
                PrinterSettings = form.PrinterSettings;

            return result;
        }

        private PlatformPrintDialogRequest CreatePlatformRequest()
        {
            return new PlatformPrintDialogRequest
            {
                AllowCurrentPage = AllowCurrentPage,
                AllowPrintToFile = AllowPrintToFile,
                AllowSelection = AllowSelection,
                AllowSomePages = AllowSomePages,
                DocumentName = Document?.DocumentName ?? string.Empty,
                HelpRequest = () => OnHelpRequest(EventArgs.Empty),
                PrinterSettings = PrintingPlatformConversions.ToPlatformPrinterSettings(PrinterSettings),
                ShowHelp = ShowHelp,
                ShowNetwork = ShowNetwork,
                UseExtendedDialog = UseEXDialog
            };
        }
    }

    internal sealed class PrintDialogForm : Form
    {
        private readonly TextBox printerNameBox;
        private readonly NumericUpDown copiesBox;
        private readonly CheckBox collateBox;
        private readonly CheckBox printToFileBox;
        private readonly TextBox printFileNameBox;
        private readonly RadioButton allPagesRadio;
        private readonly RadioButton selectionRadio;
        private readonly RadioButton currentPageRadio;
        private readonly RadioButton somePagesRadio;
        private readonly NumericUpDown fromPageBox;
        private readonly NumericUpDown toPageBox;
        private readonly Action? helpRequest;

        public PrintDialogForm(
            PrinterSettings initialSettings,
            bool allowCurrentPage,
            bool allowPrintToFile,
            bool allowSelection,
            bool allowSomePages,
            bool showHelp,
            Action? helpRequest)
        {
            ArgumentNullException.ThrowIfNull(initialSettings);

            this.helpRequest = helpRequest;

            Text = "Print";
            Size = new Size(520, 390);
            StartPosition = FormStartPosition.CenterParent;
            Resizeable = false;
            AllowMaximize = false;
            AllowMinimize = false;

            PrinterSettings = (PrinterSettings)initialSettings.Clone();

            Controls.Add(CreateLabel("Printer", 20, 40, 120));
            printerNameBox = Controls.Add(new TextBox
            {
                Location = new Point(140, 36),
                Size = new Size(330, 28),
                Text = PrinterSettings.PrinterName
            });

            Controls.Add(CreateLabel("Copies", 20, 82, 120));
            copiesBox = Controls.Add(new NumericUpDown
            {
                Location = new Point(140, 78),
                Size = new Size(90, 28),
                Minimum = 1,
                Maximum = Math.Max(1, PrinterSettings.MaximumCopies),
                Value = PrinterSettings.Copies
            });

            collateBox = Controls.Add(new CheckBox
            {
                Location = new Point(250, 80),
                Size = new Size(120, 24),
                Text = "Collate",
                Checked = PrinterSettings.Collate
            });

            printToFileBox = Controls.Add(new CheckBox
            {
                Location = new Point(20, 122),
                Size = new Size(130, 24),
                Text = "Print to file",
                Checked = PrinterSettings.PrintToFile,
                Enabled = allowPrintToFile
            });

            printFileNameBox = Controls.Add(new TextBox
            {
                Location = new Point(140, 118),
                Size = new Size(330, 28),
                Text = PrinterSettings.PrintFileName,
                Enabled = allowPrintToFile && PrinterSettings.PrintToFile
            });

            var rangePanel = Controls.Add(new Panel
            {
                Location = new Point(20, 165),
                Size = new Size(450, 110)
            });
            rangePanel.Style.Border.Width = 1;
            rangePanel.Style.Border.Color = Theme.BorderLowColor;

            rangePanel.Controls.Add(CreateLabel("Print range", 12, 8, 140));

            allPagesRadio = rangePanel.Controls.Add(new RadioButton
            {
                Location = new Point(16, 36),
                Size = new Size(95, 24),
                Text = "All pages"
            });

            selectionRadio = rangePanel.Controls.Add(new RadioButton
            {
                Location = new Point(125, 36),
                Size = new Size(95, 24),
                Text = "Selection",
                Enabled = allowSelection
            });

            currentPageRadio = rangePanel.Controls.Add(new RadioButton
            {
                Location = new Point(235, 36),
                Size = new Size(115, 24),
                Text = "Current page",
                Enabled = allowCurrentPage
            });

            somePagesRadio = rangePanel.Controls.Add(new RadioButton
            {
                Location = new Point(16, 72),
                Size = new Size(80, 24),
                Text = "Pages",
                Enabled = allowSomePages
            });

            rangePanel.Controls.Add(CreateLabel("From", 110, 72, 45));
            fromPageBox = rangePanel.Controls.Add(new NumericUpDown
            {
                Location = new Point(155, 69),
                Size = new Size(70, 26),
                Minimum = Math.Max(0, PrinterSettings.MinimumPage),
                Maximum = Math.Max(1, PrinterSettings.MaximumPage),
                Value = Math.Max(PrinterSettings.MinimumPage, PrinterSettings.FromPage),
                Enabled = allowSomePages
            });

            rangePanel.Controls.Add(CreateLabel("To", 240, 72, 30));
            toPageBox = rangePanel.Controls.Add(new NumericUpDown
            {
                Location = new Point(270, 69),
                Size = new Size(70, 26),
                Minimum = Math.Max(0, PrinterSettings.MinimumPage),
                Maximum = Math.Max(1, PrinterSettings.MaximumPage),
                Value = Math.Max(PrinterSettings.MinimumPage, PrinterSettings.ToPage),
                Enabled = allowSomePages
            });

            var okButton = Controls.Add(new Button
            {
                Location = new Point(220, 320),
                Size = new Size(80, 30),
                Text = "OK"
            });

            var cancelButton = Controls.Add(new Button
            {
                Location = new Point(310, 320),
                Size = new Size(80, 30),
                Text = "Cancel"
            });

            var helpButton = Controls.Add(new Button
            {
                Location = new Point(400, 320),
                Size = new Size(80, 30),
                Text = "Help",
                Visible = showHelp
            });

            SelectPrintRange(PrinterSettings.PrintRange, allowSelection, allowCurrentPage, allowSomePages);
            printToFileBox.CheckedChanged += (_, _) => printFileNameBox.Enabled = printToFileBox.Checked && printToFileBox.Enabled;
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
        }

        public PrinterSettings PrinterSettings { get; private set; }

        private void ApplySettings()
        {
            PrinterSettings.PrinterName = printerNameBox.Text;
            PrinterSettings.Copies = (short)Math.Round(copiesBox.Value);
            PrinterSettings.Collate = collateBox.Checked;
            PrinterSettings.PrintToFile = printToFileBox.Enabled && printToFileBox.Checked;
            PrinterSettings.PrintFileName = printFileNameBox.Text;
            PrinterSettings.FromPage = (int)Math.Round(fromPageBox.Value);
            PrinterSettings.ToPage = Math.Max(PrinterSettings.FromPage, (int)Math.Round(toPageBox.Value));

            if (selectionRadio.Checked)
                PrinterSettings.PrintRange = PrintRange.Selection;
            else if (currentPageRadio.Checked)
                PrinterSettings.PrintRange = PrintRange.CurrentPage;
            else if (somePagesRadio.Checked)
                PrinterSettings.PrintRange = PrintRange.SomePages;
            else
                PrinterSettings.PrintRange = PrintRange.AllPages;
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

        private void SelectPrintRange(PrintRange range, bool allowSelection, bool allowCurrentPage, bool allowSomePages)
        {
            if (range == PrintRange.Selection && allowSelection)
                selectionRadio.Checked = true;
            else if (range == PrintRange.CurrentPage && allowCurrentPage)
                currentPageRadio.Checked = true;
            else if (range == PrintRange.SomePages && allowSomePages)
                somePagesRadio.Checked = true;
            else
                allPagesRadio.Checked = true;
        }
    }
}

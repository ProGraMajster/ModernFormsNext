using System;
using System.ComponentModel;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a form that displays a print preview for a <see cref="PrintDocument"/>.
    /// </summary>
    /// <remarks>
    /// The dialog is built from ModernFormsNext controls and hosts a <see cref="PrintPreviewControl"/>.
    /// The Print command currently runs the document event pipeline through <see cref="PrintDocument.Print"/>;
    /// physical printer spooling will require a future WindowKit print backend.
    /// </remarks>
    /// <example>
    /// <code>
    /// var preview = new PrintPreviewDialog { Document = document };
    /// await preview.ShowDialog(this);
    /// </code>
    /// </example>
    [DefaultProperty(nameof(Document))]
    public class PrintPreviewDialog : Form
    {
        private readonly PrintPreviewControl previewControl;
        private readonly Button printButton;
        private readonly Button pageSetupButton;
        private readonly Button previousButton;
        private readonly Button nextButton;
        private readonly NumericUpDown pageBox;
        private readonly ComboBox zoomBox;
        private bool updatingPageBox;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrintPreviewDialog"/> class.
        /// </summary>
        public PrintPreviewDialog()
        {
            Text = "Print Preview";
            Size = new Size(900, 680);
            StartPosition = FormStartPosition.CenterParent;

            previewControl = Controls.Add(new PrintPreviewControl
            {
                Dock = DockStyle.Fill
            });

            var toolbar = Controls.Add(new Panel
            {
                Dock = DockStyle.Top,
                Height = 46
            });
            toolbar.Style.Border.Bottom.Width = 1;
            toolbar.Style.Border.Bottom.Color = Theme.BorderLowColor;

            printButton = toolbar.Controls.Add(new Button
            {
                Location = new Point(8, 8),
                Size = new Size(70, 30),
                Text = "Print"
            });

            pageSetupButton = toolbar.Controls.Add(new Button
            {
                Location = new Point(86, 8),
                Size = new Size(92, 30),
                Text = "Page setup"
            });

            previousButton = toolbar.Controls.Add(new Button
            {
                Location = new Point(196, 8),
                Size = new Size(34, 30),
                Text = "<"
            });

            pageBox = toolbar.Controls.Add(new NumericUpDown
            {
                Location = new Point(238, 8),
                Size = new Size(70, 30),
                Minimum = 1,
                Maximum = 1,
                Value = 1
            });

            nextButton = toolbar.Controls.Add(new Button
            {
                Location = new Point(316, 8),
                Size = new Size(34, 30),
                Text = ">"
            });

            zoomBox = toolbar.Controls.Add(new ComboBox
            {
                Location = new Point(370, 8),
                Size = new Size(100, 30)
            });
            zoomBox.Items.Add("Auto");
            zoomBox.Items.Add("25%");
            zoomBox.Items.Add("50%");
            zoomBox.Items.Add("75%");
            zoomBox.Items.Add("100%");
            zoomBox.Items.Add("150%");
            zoomBox.Items.Add("200%");
            zoomBox.SelectedIndex = 0;

            var closeButton = toolbar.Controls.Add(new Button
            {
                Location = new Point(800, 8),
                Size = new Size(80, 30),
                Text = "Close"
            });

            printButton.Click += (_, _) => Document?.Print();
            pageSetupButton.Click += PageSetupButton_Click;
            previousButton.Click += (_, _) => MovePreview(-previewControl.Rows * previewControl.Columns);
            nextButton.Click += (_, _) => MovePreview(previewControl.Rows * previewControl.Columns);
            pageBox.ValueChanged += PageBox_ValueChanged;
            zoomBox.SelectedIndexChanged += (_, _) => ApplyZoomSelection();
            closeButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            previewControl.StartPageChanged += (_, _) => UpdatePageControls();

            UpdatePageControls();
        }

        /// <summary>
        /// Gets or sets the document displayed by the preview dialog.
        /// </summary>
        public PrintDocument? Document {
            get => previewControl.Document;
            set {
                previewControl.Document = value;
                UpdatePageControls();
            }
        }

        /// <summary>
        /// Gets the preview control hosted by the dialog.
        /// </summary>
        [Browsable(false)]
        public PrintPreviewControl PrintPreviewControl => previewControl;

        /// <summary>
        /// Gets or sets how the page setup dialog opened from the preview toolbar is rendered.
        /// </summary>
        /// <value>
        /// One of the <see cref="PrintingDialogRenderingMode"/> values. The default is
        /// <see cref="PrintingDialogRenderingMode.System"/> to match the default behavior of
        /// <see cref="PageSetupDialog"/>.
        /// </value>
        /// <remarks>
        /// This property only affects the Page setup command hosted by this preview dialog. The
        /// preview window itself is always rendered with ModernFormsNext controls because it hosts
        /// a framework <see cref="PrintPreviewControl"/>.
        /// </remarks>
        [DefaultValue(PrintingDialogRenderingMode.System)]
        public PrintingDialogRenderingMode PageSetupDialogRenderingMode { get; set; } = PrintingDialogRenderingMode.System;

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing is used when drawing preview pages.
        /// </summary>
        [DefaultValue(false)]
        public bool UseAntiAlias {
            get => previewControl.UseAntiAlias;
            set => previewControl.UseAntiAlias = value;
        }

        private async void PageSetupButton_Click(object? sender, MouseEventArgs e)
        {
            if (Document is null)
                return;

            var dialog = new PageSetupDialog
            {
                Document = Document,
                RenderingMode = PageSetupDialogRenderingMode
            };

            if (await dialog.ShowDialog(this) == DialogResult.OK) {
                previewControl.InvalidatePreview();
                UpdatePageControls();
            }
        }

        private void PageBox_ValueChanged(object? sender, EventArgs e)
        {
            if (updatingPageBox)
                return;

            previewControl.StartPage = Math.Max(0, (int)Math.Round(pageBox.Value) - 1);
            UpdatePageControls();
        }

        private void MovePreview(int pageDelta)
        {
            previewControl.StartPage = Math.Max(0, previewControl.StartPage + pageDelta);
            UpdatePageControls();
        }

        private void ApplyZoomSelection()
        {
            switch (zoomBox.SelectedIndex) {
                case 0:
                    previewControl.AutoZoom = true;
                    break;
                case 1:
                    previewControl.Zoom = 0.25d;
                    break;
                case 2:
                    previewControl.Zoom = 0.5d;
                    break;
                case 3:
                    previewControl.Zoom = 0.75d;
                    break;
                case 4:
                    previewControl.Zoom = 1.0d;
                    break;
                case 5:
                    previewControl.Zoom = 1.5d;
                    break;
                case 6:
                    previewControl.Zoom = 2.0d;
                    break;
            }
        }

        private void UpdatePageControls()
        {
            var pageCount = Math.Max(1, previewControl.PageCount);
            updatingPageBox = true;
            pageBox.Maximum = pageCount;
            pageBox.Value = Math.Min(pageCount, previewControl.StartPage + 1);
            updatingPageBox = false;

            printButton.Enabled = Document is not null;
            pageSetupButton.Enabled = Document is not null;
            previousButton.Enabled = previewControl.StartPage > 0;
            nextButton.Enabled = previewControl.StartPage + 1 < previewControl.PageCount;
        }
    }
}

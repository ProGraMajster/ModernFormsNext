using System;
using System.Drawing;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class PrintingPanel : Panel
    {
        private readonly PrintDocument document;
        private readonly PrintPreviewControl previewControl;
        private readonly Label statusLabel;
        private readonly CheckBox systemDialogsBox;
        private int pageNumber;

        public PrintingPanel()
        {
            document = new PrintDocument
            {
                DocumentName = "ControlGallery print sample"
            };
            document.DefaultPageSettings.Margins = new Margins(75, 75, 75, 75);
            document.PrinterSettings.MinimumPage = 1;
            document.PrinterSettings.MaximumPage = 2;
            document.PrinterSettings.FromPage = 1;
            document.PrinterSettings.ToPage = 2;
            document.BeginPrint += (_, _) => pageNumber = 1;
            document.PrintPage += Document_PrintPage;

            statusLabel = Controls.Add(new Label
            {
                Location = new Point(565, 170),
                Size = new Size(340, 120),
                Multiline = true,
                Text = "Ready."
            });

            var pageSetupButton = Controls.Add(new Button
            {
                Location = new Point(20, 20),
                Size = new Size(120, 30),
                Text = "Page Setup"
            });
            pageSetupButton.Click += PageSetupButton_Click;

            var printDialogButton = Controls.Add(new Button
            {
                Location = new Point(150, 20),
                Size = new Size(120, 30),
                Text = "Print Dialog"
            });
            printDialogButton.Click += PrintDialogButton_Click;

            var previewDialogButton = Controls.Add(new Button
            {
                Location = new Point(280, 20),
                Size = new Size(135, 30),
                Text = "Preview Dialog"
            });
            previewDialogButton.Click += PreviewDialogButton_Click;

            var runPrintButton = Controls.Add(new Button
            {
                Location = new Point(425, 20),
                Size = new Size(105, 30),
                Text = "Run Print"
            });
            runPrintButton.Click += (_, _) =>
            {
                document.Print();
                statusLabel.Text = "PrintDocument.Print ran the managed print pipeline.";
            };

            var antiAliasBox = Controls.Add(new CheckBox
            {
                Location = new Point(550, 24),
                Size = new Size(130, 24),
                Text = "Antialias",
                Checked = true
            });

            systemDialogsBox = Controls.Add(new CheckBox
            {
                Location = new Point(680, 24),
                Size = new Size(150, 24),
                Text = "System dialogs"
            });

            previewControl = Controls.Add(new PrintPreviewControl
            {
                Location = new Point(20, 70),
                Size = new Size(520, 520),
                Document = document,
                UseAntiAlias = true
            });

            antiAliasBox.CheckedChanged += (_, _) =>
            {
                previewControl.UseAntiAlias = antiAliasBox.Checked;
                previewControl.Invalidate();
            };

            Controls.Add(new Label
            {
                Location = new Point(565, 80),
                Size = new Size(75, 22),
                Text = "Rows"
            });

            var rowsBox = Controls.Add(new NumericUpDown
            {
                Location = new Point(650, 76),
                Size = new Size(70, 28),
                Minimum = 1,
                Maximum = 3,
                Value = 1
            });
            rowsBox.ValueChanged += (_, _) => previewControl.Rows = (int)Math.Round(rowsBox.Value);

            Controls.Add(new Label
            {
                Location = new Point(565, 116),
                Size = new Size(75, 22),
                Text = "Columns"
            });

            var columnsBox = Controls.Add(new NumericUpDown
            {
                Location = new Point(650, 112),
                Size = new Size(70, 28),
                Minimum = 1,
                Maximum = 3,
                Value = 1
            });
            columnsBox.ValueChanged += (_, _) => previewControl.Columns = (int)Math.Round(columnsBox.Value);

        }

        private async void PageSetupButton_Click(object? sender, MouseEventArgs e)
        {
            var dialog = new PageSetupDialog
            {
                Document = document,
                EnableMetric = false,
                RenderingMode = GetDialogRenderingMode(),
                ShowHelp = true
            };
            dialog.HelpRequest += (_, _) => statusLabel.Text = "Page setup help requested.";

            var owner = FindForm();
            if (owner is null) {
                statusLabel.Text = "Page setup requires an owning form.";
                return;
            }

            if (await dialog.ShowDialog(owner) == DialogResult.OK) {
                previewControl.InvalidatePreview();
                statusLabel.Text = $"Page setup accepted: {document.DefaultPageSettings.PaperSize.PaperName}, margins {document.DefaultPageSettings.Margins}.";
            } else {
                statusLabel.Text = "Page setup canceled.";
            }
        }

        private async void PrintDialogButton_Click(object? sender, MouseEventArgs e)
        {
            var dialog = new PrintDialog
            {
                Document = document,
                AllowCurrentPage = true,
                AllowPrintToFile = true,
                AllowSelection = true,
                AllowSomePages = true,
                RenderingMode = GetDialogRenderingMode(),
                ShowHelp = true
            };
            dialog.HelpRequest += (_, _) => statusLabel.Text = "Print dialog help requested.";

            var owner = FindForm();
            if (owner is null) {
                statusLabel.Text = "Print dialog requires an owning form.";
                return;
            }

            if (await dialog.ShowDialog(owner) == DialogResult.OK)
                statusLabel.Text = $"Print dialog accepted: {document.PrinterSettings.PrinterName}, copies {document.PrinterSettings.Copies}.";
            else
                statusLabel.Text = "Print dialog canceled.";
        }

        private async void PreviewDialogButton_Click(object? sender, MouseEventArgs e)
        {
            using var dialog = new PrintPreviewDialog
            {
                Document = document,
                PageSetupDialogRenderingMode = GetDialogRenderingMode(),
                UseAntiAlias = true
            };

            var owner = FindForm();
            if (owner is null) {
                statusLabel.Text = "Print preview requires an owning form.";
                return;
            }

            await dialog.ShowDialog(owner);
            statusLabel.Text = "Preview dialog closed.";
        }

        private void Document_PrintPage(object? sender, PrintPageEventArgs e)
        {
            using var titleFont = new SKFont(Theme.UIFont, 34);
            using var bodyFont = new SKFont(Theme.UIFont, 18);
            using var smallFont = new SKFont(Theme.UIFont, 14);
            using var titlePaint = new SKPaint { Color = new SKColor(30, 96, 145), IsAntialias = true };
            using var bodyPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var accentPaint = new SKPaint { Color = new SKColor(30, 96, 145), IsAntialias = true, StrokeWidth = 3 };
            using var lightPaint = new SKPaint { Color = new SKColor(230, 236, 241), IsAntialias = true };

            var margin = e.MarginBounds;
            e.Canvas.DrawText("ModernFormsNext", margin.Left, margin.Top + 40, SKTextAlign.Left, titleFont, titlePaint);
            e.Canvas.DrawText("Printing compatibility sample", margin.Left, margin.Top + 78, SKTextAlign.Left, bodyFont, bodyPaint);
            e.Canvas.DrawLine(margin.Left, margin.Top + 100, margin.Right, margin.Top + 100, accentPaint);

            var y = margin.Top + 145;

            for (var i = 0; i < 7; i++) {
                var rowTop = y + (i * 55);
                var rowRect = new SKRect(margin.Left, rowTop, margin.Right, rowTop + 42);

                if (i % 2 == 0)
                    e.Canvas.DrawRect(rowRect, lightPaint);

                e.Canvas.DrawText($"Line item {i + 1}", margin.Left + 16, rowTop + 28, SKTextAlign.Left, bodyFont, bodyPaint);
                e.Canvas.DrawText($"Page {pageNumber}", margin.Right - 110, rowTop + 28, SKTextAlign.Left, bodyFont, bodyPaint);
            }

            e.Canvas.DrawText($"Rendered page {pageNumber}", margin.Left, margin.Bottom - 25, SKTextAlign.Left, smallFont, bodyPaint);

            e.HasMorePages = pageNumber < 2;
            pageNumber++;
        }

        private PrintingDialogRenderingMode GetDialogRenderingMode()
        {
            return systemDialogsBox.Checked
                ? PrintingDialogRenderingMode.System
                : PrintingDialogRenderingMode.ModernFormsNext;
        }
    }
}

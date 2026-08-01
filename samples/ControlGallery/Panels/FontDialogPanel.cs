using System;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class FontDialogPanel : Panel
    {
        private readonly Label previewLabel;
        private readonly Label statusLabel;
        private readonly CheckBox modernRenderingCheckBox;
        private Font selectedFont = new Font("Segoe UI", 18);
        private SKColor selectedColor = Theme.ForegroundColor;

        public FontDialogPanel()
        {
            var button = Controls.Add(new Button
            {
                Left = 10,
                Top = 10,
                Width = 160,
                Text = "Show Font Dialog"
            });
            button.Click += Button_Click;

            modernRenderingCheckBox = Controls.Add(new CheckBox
            {
                Left = 190,
                Top = 13,
                Width = 230,
                Text = "ModernFormsNext rendering",
                Checked = true
            });

            previewLabel = Controls.Add(new Label
            {
                Left = 10,
                Top = 60,
                Width = 520,
                Height = 80,
                Text = "The quick brown fox jumps over the lazy dog.",
                TextAlign = ContentAlignment.MiddleLeft
            });

            statusLabel = Controls.Add(new Label
            {
                Left = 10,
                Top = 155,
                Width = 650,
                Height = 60,
                Multiline = true
            });

            ApplySelection();
        }

        private async void Button_Click(object? sender, MouseEventArgs e)
        {
            var dialog = new FontDialog
            {
                Font = selectedFont,
                Color = selectedColor,
                MinSize = 6,
                MaxSize = 72,
                ShowApply = true,
                ShowColor = true,
                ShowEffects = true,
                ShowHelp = true,
                RenderingMode = modernRenderingCheckBox.Checked
                    ? FontDialogRenderingMode.ModernFormsNext
                    : FontDialogRenderingMode.System
            };

            dialog.Apply += (_, _) =>
            {
                selectedFont = dialog.Font;
                selectedColor = dialog.Color;
                ApplySelection("Apply");
            };

            dialog.HelpRequest += (_, _) =>
            {
                statusLabel.Text = "Help requested from the native font dialog.";
            };

            if (await dialog.ShowDialog(FindForm()!) == DialogResult.OK)
            {
                selectedFont = dialog.Font;
                selectedColor = dialog.Color;
                ApplySelection("OK");
            }
            else
            {
                statusLabel.Text = "Font dialog canceled.";
            }
        }

        private void ApplySelection(string source = "Initial")
        {
            previewLabel.Font = selectedFont;
            previewLabel.Style.ForegroundColor = selectedColor;
            previewLabel.Invalidate();

            statusLabel.Text = $"{source}: {selectedFont.Name}, {selectedFont.SizeInPoints:g}pt, {selectedFont.Style}, {selectedColor}";
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace ModernFormsNext
{
    internal sealed class FontDialogForm : Form
    {
        private static readonly int[] StandardSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];

        private readonly Action<Font, SKColor>? applyCallback;
        private readonly Action? helpRequestCallback;
        private readonly ListBox familyList;
        private readonly ListBox styleList;
        private readonly ListBox sizeList;
        private readonly NumericUpDown sizePicker;
        private readonly CheckBox underlineCheckBox;
        private readonly CheckBox strikeoutCheckBox;
        private readonly Button colorButton;
        private readonly Panel colorPreview;
        private readonly Label previewLabel;
        private readonly bool showColor;
        private readonly bool showEffects;
        private bool updating;

        public FontDialogForm(
            Font initialFont,
            SKColor initialColor,
            int minSize,
            int maxSize,
            bool showApply,
            bool showColor,
            bool showEffects,
            bool showHelp,
            Action<Font, SKColor>? applyCallback,
            Action? helpRequestCallback)
        {
            ArgumentNullException.ThrowIfNull(initialFont);

            this.showColor = showColor;
            this.showEffects = showEffects;
            this.applyCallback = applyCallback;
            this.helpRequestCallback = helpRequestCallback;

            Text = "Font";
            Size = new Size(740, 500);
            StartPosition = FormStartPosition.CenterParent;
            Resizeable = false;
            AllowMaximize = false;
            AllowMinimize = false;

            SelectedFont = initialFont;
            SelectedColor = initialColor;

            Controls.Add(CreateCaptionLabel("Font", 20, 40, 220));
            Controls.Add(CreateCaptionLabel("Font style", 300, 40, 140));
            Controls.Add(CreateCaptionLabel("Size", 470, 40, 90));

            familyList = Controls.Add(new ListBox
            {
                Location = new Point(20, 65),
                Size = new Size(260, 240),
                ShowHover = true
            });

            styleList = Controls.Add(new ListBox
            {
                Location = new Point(300, 65),
                Size = new Size(150, 120),
                ShowHover = true
            });

            sizeList = Controls.Add(new ListBox
            {
                Location = new Point(470, 65),
                Size = new Size(80, 150),
                ShowHover = true
            });

            sizePicker = Controls.Add(new NumericUpDown
            {
                Location = new Point(570, 65),
                Size = new Size(80, 28),
                Minimum = GetEffectiveMinSize(minSize),
                Maximum = GetEffectiveMaxSize(minSize, maxSize),
                Value = ClampSize(initialFont.SizeInPoints, minSize, maxSize)
            });

            underlineCheckBox = Controls.Add(new CheckBox
            {
                Location = new Point(300, 205),
                Size = new Size(120, 24),
                Text = "Underline",
                Checked = showEffects && initialFont.Underline
            });

            strikeoutCheckBox = Controls.Add(new CheckBox
            {
                Location = new Point(300, 235),
                Size = new Size(120, 24),
                Text = "Strikeout",
                Checked = showEffects && initialFont.Strikeout
            });

            colorButton = Controls.Add(new Button
            {
                Location = new Point(300, 275),
                Size = new Size(90, 30),
                Text = "Color..."
            });

            colorPreview = Controls.Add(new Panel
            {
                Location = new Point(400, 275),
                Size = new Size(50, 30)
            });
            colorPreview.Style.Border.Width = 1;
            colorPreview.Style.Border.Color = Theme.BorderLowColor;

            var previewPanel = Controls.Add(new Panel
            {
                Location = new Point(20, 335),
                Size = new Size(690, 90)
            });
            previewPanel.Style.Border.Width = 1;
            previewPanel.Style.Border.Color = Theme.BorderLowColor;
            previewPanel.Style.BackgroundColor = Theme.ControlLowColor;

            previewLabel = previewPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "The quick brown fox jumps over the lazy dog.",
                TextAlign = ContentAlignment.MiddleCenter
            });

            var okButton = Controls.Add(new Button
            {
                Location = new Point(450, 450),
                Size = new Size(80, 30),
                Text = "OK"
            });

            var cancelButton = Controls.Add(new Button
            {
                Location = new Point(540, 450),
                Size = new Size(80, 30),
                Text = "Cancel"
            });

            var applyButton = Controls.Add(new Button
            {
                Location = new Point(630, 450),
                Size = new Size(80, 30),
                Text = "Apply"
            });

            var helpButton = Controls.Add(new Button
            {
                Location = new Point(20, 450),
                Size = new Size(80, 30),
                Text = "Help"
            });

            applyButton.Visible = showApply;
            helpButton.Visible = showHelp;
            underlineCheckBox.Visible = showEffects;
            strikeoutCheckBox.Visible = showEffects;
            colorButton.Visible = showColor;
            colorPreview.Visible = showColor;

            PopulateFamilies(initialFont);
            PopulateStyles(initialFont);
            PopulateSizes(minSize, maxSize);
            SelectInitialValues(initialFont);

            familyList.SelectedIndexChanged += (_, _) => UpdateSelectionFromControls();
            styleList.SelectedIndexChanged += (_, _) => UpdateSelectionFromControls();
            sizeList.SelectedIndexChanged += SizeList_SelectedIndexChanged;
            sizePicker.ValueChanged += SizePicker_ValueChanged;
            underlineCheckBox.CheckedChanged += (_, _) => UpdateSelectionFromControls();
            strikeoutCheckBox.CheckedChanged += (_, _) => UpdateSelectionFromControls();
            colorButton.Click += ColorButton_Click;
            okButton.Click += (_, _) =>
            {
                UpdateSelectionFromControls();
                DialogResult = DialogResult.OK;
                Close();
            };
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            applyButton.Click += (_, _) =>
            {
                UpdateSelectionFromControls();
                applyCallback?.Invoke(SelectedFont, SelectedColor);
            };
            helpButton.Click += (_, _) => helpRequestCallback?.Invoke();

            UpdateSelectionFromControls();
        }

        public SKColor SelectedColor { get; private set; }

        public Font SelectedFont { get; private set; }

        private async void ColorButton_Click(object? sender, MouseEventArgs e)
        {
            var dialog = new ColorDialog
            {
                Color = SelectedColor
            };

            if (await dialog.ShowDialog(this) == DialogResult.OK)
            {
                SelectedColor = dialog.Color;
                UpdatePreview();
            }
        }

        private static Label CreateCaptionLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 22)
            };
        }

        private static decimal GetEffectiveMaxSize(int minSize, int maxSize)
        {
            if (maxSize > 0)
                return Math.Max(GetEffectiveMinSize(minSize), maxSize);

            return 200;
        }

        private static decimal GetEffectiveMinSize(int minSize) => Math.Max(1, minSize);

        private static decimal ClampSize(float size, int minSize, int maxSize)
        {
            var value = (decimal)Math.Round(size);
            var min = GetEffectiveMinSize(minSize);
            var max = GetEffectiveMaxSize(minSize, maxSize);

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private void PopulateFamilies(Font initialFont)
        {
            var families = SKFontManager.Default.FontFamilies
                .Where(family => !string.IsNullOrWhiteSpace(family))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(family => family, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            if (families.Length == 0)
            {
                familyList.Items.Add(initialFont.FamilyName);
                return;
            }

            if (!families.Contains(initialFont.FamilyName, StringComparer.CurrentCultureIgnoreCase))
                familyList.Items.Add(initialFont.FamilyName);

            familyList.Items.AddRange(families.Cast<object>().ToArray());
        }

        private void PopulateSizes(int minSize, int maxSize)
        {
            foreach (var size in StandardSizes)
            {
                if (minSize > 0 && size < minSize)
                    continue;

                if (maxSize > 0 && size > maxSize)
                    continue;

                sizeList.Items.Add(size);
            }
        }

        private void PopulateStyles(Font initialFont)
        {
            styleList.Items.AddRange(
                new FontStyleChoice("Regular", FontStyle.Regular),
                new FontStyleChoice("Bold", FontStyle.Bold),
                new FontStyleChoice("Italic", FontStyle.Italic),
                new FontStyleChoice("Bold Italic", FontStyle.Bold | FontStyle.Italic));

            var baseStyle = initialFont.Style & (FontStyle.Bold | FontStyle.Italic);
            var selected = styleList.Items.OfType<FontStyleChoice>().FirstOrDefault(choice => choice.Style == baseStyle);

            styleList.SelectedItem = selected ?? styleList.Items[0];
        }

        private void SelectInitialValues(Font initialFont)
        {
            updating = true;

            try
            {
                var familyIndex = familyList.Items
                    .Select((item, index) => new { item, index })
                    .FirstOrDefault(x => string.Equals(x.item.ToString(), initialFont.FamilyName, StringComparison.CurrentCultureIgnoreCase))
                    ?.index ?? 0;

                familyList.SelectedIndex = familyIndex;

                var roundedSize = (int)Math.Round(sizePicker.Value);
                var sizeIndex = sizeList.Items.IndexOf(roundedSize);

                if (sizeIndex >= 0)
                    sizeList.SelectedIndex = sizeIndex;
            }
            finally
            {
                updating = false;
            }
        }

        private void SizeList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (updating)
                return;

            if (sizeList.SelectedItem is int size)
            {
                updating = true;
                sizePicker.Value = size;
                updating = false;
            }

            UpdateSelectionFromControls();
        }

        private void SizePicker_ValueChanged(object? sender, EventArgs e)
        {
            if (updating)
                return;

            updating = true;

            try
            {
                var size = (int)Math.Round(sizePicker.Value);
                var sizeIndex = sizeList.Items.IndexOf(size);
                sizeList.SelectedIndex = sizeIndex;
            }
            finally
            {
                updating = false;
            }

            UpdateSelectionFromControls();
        }

        private void UpdatePreview()
        {
            colorPreview.Style.BackgroundColor = SelectedColor;
            colorPreview.Invalidate();

            previewLabel.Font = SelectedFont;
            previewLabel.Style.ForegroundColor = SelectedColor;
            previewLabel.Invalidate();
        }

        private void UpdateSelectionFromControls()
        {
            if (updating)
                return;

            var familyName = familyList.SelectedItem?.ToString() ?? SelectedFont.FamilyName;
            var style = (styleList.SelectedItem as FontStyleChoice)?.Style ?? FontStyle.Regular;

            if (showEffects)
            {
                if (underlineCheckBox.Checked)
                    style |= FontStyle.Underline;

                if (strikeoutCheckBox.Checked)
                    style |= FontStyle.Strikeout;
            }

            var size = Math.Max(1, (float)sizePicker.Value);
            SelectedFont = new Font(familyName, size, style);

            UpdatePreview();
        }

        private sealed class FontStyleChoice
        {
            public FontStyleChoice(string text, FontStyle style)
            {
                Text = text;
                Style = style;
            }

            public FontStyle Style { get; }

            public string Text { get; }

            public override string ToString() => Text;
        }
    }
}

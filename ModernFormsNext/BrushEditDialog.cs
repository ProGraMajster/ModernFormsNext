using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ModernFormsNext.Drawing;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext
{
    /// <summary>
    /// Presents a ModernFormsNext dialog for editing a brush value.
    /// </summary>
    /// <remarks>
    /// The dialog is implemented with ModernFormsNext controls and SkiaSharp rendering so it
    /// can be reused by the standalone designer and by Visual Studio-hosted designer tools.
    /// It supports null brushes, solid color brushes, and gradient brushes with editable
    /// color stops, offsets, alpha values, and a live preview.
    /// </remarks>
    /// <example>
    /// <code>
    /// var dialog = new BrushEditDialog
    /// {
    ///     Brush = new SolidColorBrush(SKColors.SteelBlue)
    /// };
    ///
    /// if (await dialog.ShowDialog(this) == DialogResult.OK)
    /// {
    ///     control.BackgroundBrush = dialog.Brush;
    /// }
    /// </code>
    /// </example>
    public sealed class BrushEditDialog
    {
        /// <summary>
        /// Gets or sets the brush being edited. Set this value to <see langword="null"/>
        /// to clear the brush.
        /// </summary>
        public MfnBrush? Brush { get; set; }

        /// <summary>
        /// Shows the brush editor dialog.
        /// </summary>
        /// <param name="owner">The owner form for modal display.</param>
        /// <returns>A task that resolves to the selected dialog result.</returns>
        public async Task<DialogResult> ShowDialog(Form owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            var form = new BrushEditDialogForm(Brush);
            var result = await form.ShowDialog(owner);

            if (result == DialogResult.OK)
                Brush = form.SelectedBrush;

            return result;
        }
    }

    internal sealed class BrushEditDialogForm : Form
    {
        private readonly ComboBox brushType;
        private readonly CheckBox noBrushCheckBox;
        private readonly ListBox stopsList;
        private readonly TextBox colorText;
        private readonly TextBox offsetText;
        private readonly TextBox alphaText;
        private readonly BrushPreviewPanel preview;
        private readonly List<EditableGradientStop> stops = [];
        private bool updatingFields;

        public BrushEditDialogForm(MfnBrush? initialBrush)
        {
            Text = "Brush Editor";
            Name = "BrushEditDialog";
            Size = new Size(560, 500);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Left = 20,
                Top = 52,
                Width = 92,
                Height = 22,
                Text = "Type"
            });

            brushType = Controls.Add(new ComboBox
            {
                Left = 128,
                Top = 48,
                Width = 190,
                Height = 26
            });
            brushType.Items.AddRange(new object[]
            {
                "Solid",
                "LinearGradient",
                "RadialGradient",
                "SweepGradient"
            });

            noBrushCheckBox = Controls.Add(new CheckBox
            {
                Left = 336,
                Top = 49,
                Width = 170,
                Height = 24,
                Text = "No brush (null)"
            });

            Controls.Add(new Label
            {
                Left = 20,
                Top = 88,
                Width = 220,
                Height = 22,
                Text = "Gradient stops"
            });

            stopsList = Controls.Add(new ListBox
            {
                Left = 20,
                Top = 112,
                Width = 250,
                Height = 150
            });

            var addStop = Controls.Add(new Button
            {
                Left = 20,
                Top = 270,
                Width = 76,
                Height = 28,
                Text = "Add"
            });

            var removeStop = Controls.Add(new Button
            {
                Left = 104,
                Top = 270,
                Width = 76,
                Height = 28,
                Text = "Remove"
            });

            var pickColor = Controls.Add(new Button
            {
                Left = 188,
                Top = 270,
                Width = 82,
                Height = 28,
                Text = "Color..."
            });

            Controls.Add(new Label
            {
                Left = 292,
                Top = 112,
                Width = 90,
                Height = 22,
                Text = "Color"
            });

            colorText = Controls.Add(new TextBox
            {
                Left = 388,
                Top = 108,
                Width = 130,
                Height = 26
            });

            Controls.Add(new Label
            {
                Left = 292,
                Top = 148,
                Width = 90,
                Height = 22,
                Text = "Offset"
            });

            offsetText = Controls.Add(new TextBox
            {
                Left = 388,
                Top = 144,
                Width = 130,
                Height = 26
            });

            Controls.Add(new Label
            {
                Left = 292,
                Top = 184,
                Width = 90,
                Height = 22,
                Text = "Alpha"
            });

            alphaText = Controls.Add(new TextBox
            {
                Left = 388,
                Top = 180,
                Width = 130,
                Height = 26
            });

            Controls.Add(new Label
            {
                Left = 292,
                Top = 212,
                Width = 230,
                Height = 40,
                Text = "Offset is 0..1. Alpha is 0..255 and controls color strength."
            });

            preview = Controls.Add(new BrushPreviewPanel
            {
                Left = 20,
                Top = 318,
                Width = 500,
                Height = 90
            });

            var ok = Controls.Add(new Button
            {
                Left = 340,
                Top = 426,
                Width = 86,
                Height = 30,
                Text = "OK"
            });

            var cancel = Controls.Add(new Button
            {
                Left = 434,
                Top = 426,
                Width = 86,
                Height = 30,
                Text = "Cancel"
            });

            InitializeFromBrush(initialBrush);

            brushType.SelectedIndexChanged += (_, _) => RefreshPreview();
            noBrushCheckBox.CheckedChanged += (_, _) =>
            {
                UpdateEditorEnabledState();
                RefreshPreview();
            };
            stopsList.SelectedIndexChanged += (_, _) => LoadSelectedStop();
            colorText.TextChanged += (_, _) => UpdateSelectedStopFromFields();
            offsetText.TextChanged += (_, _) => UpdateSelectedStopFromFields();
            alphaText.TextChanged += (_, _) => UpdateSelectedStopFromFields();
            addStop.Click += (_, _) => AddStopFromSelection();
            removeStop.Click += (_, _) => RemoveSelectedStop();
            pickColor.Click += async (_, _) => await PickSelectedStopColor();
            ok.Click += (_, _) =>
            {
                SelectedBrush = CreateBrushFromInputs();
                DialogResult = DialogResult.OK;
            };
            cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        }

        public MfnBrush? SelectedBrush { get; private set; }

        private void InitializeFromBrush(MfnBrush? brush)
        {
            stops.Clear();
            noBrushCheckBox.Checked = brush is null;

            switch (brush)
            {
                case SolidColorBrush solid:
                    brushType.SelectedIndex = 0;
                    stops.Add(new EditableGradientStop(solid.Color, 0f));
                    break;
                case LinearGradientBrush linear:
                    brushType.SelectedIndex = 1;
                    CopyStops(linear);
                    break;
                case RadialGradientBrush radial:
                    brushType.SelectedIndex = 2;
                    CopyStops(radial);
                    break;
                case SweepGradientBrush sweep:
                    brushType.SelectedIndex = 3;
                    CopyStops(sweep);
                    break;
                default:
                    brushType.SelectedIndex = 0;
                    stops.Add(new EditableGradientStop(SKColors.White, 0f));
                    stops.Add(new EditableGradientStop(SKColors.SteelBlue, 1f));
                    break;
            }

            if (stops.Count == 0)
            {
                stops.Add(new EditableGradientStop(SKColors.White, 0f));
                stops.Add(new EditableGradientStop(SKColors.SteelBlue, 1f));
            }

            RefreshStopList(selectIndex: 0);
            UpdateEditorEnabledState();
            RefreshPreview();
        }

        private void CopyStops(GradientBrush brush)
        {
            foreach (var stop in brush.GradientStops.OrderBy(stop => stop.Offset))
                stops.Add(new EditableGradientStop(stop.Color, stop.Offset));
        }

        private async Task PickSelectedStopColor()
        {
            if (GetSelectedStop() is not { } stop)
                return;

            var dialog = new ColorDialog
            {
                Color = stop.Color
            };

            if (await dialog.ShowDialog(this) != DialogResult.OK)
                return;

            stop.Color = dialog.Color;
            LoadSelectedStop();
            RefreshStopList(stopsList.SelectedIndex);
            RefreshPreview();
        }

        private void AddStopFromSelection()
        {
            var selected = GetSelectedStop();
            var offset = selected is null
                ? stops.Count == 0 ? 0f : Math.Clamp(stops[^1].Offset + 0.1f, 0f, 1f)
                : Math.Clamp(selected.Offset + 0.1f, 0f, 1f);
            var color = selected?.Color ?? SKColors.White;

            var newStop = new EditableGradientStop(color, offset);
            stops.Add(newStop);
            stops.Sort((first, second) => first.Offset.CompareTo(second.Offset));
            RefreshStopList(stops.FindIndex(stop => ReferenceEquals(stop, newStop)));
            RefreshPreview();
        }

        private void RemoveSelectedStop()
        {
            var index = stopsList.SelectedIndex;

            if (index < 0 || index >= stops.Count)
                return;

            if (stops.Count <= 1)
            {
                stops[index] = new EditableGradientStop(SKColors.Transparent, 0f);
                RefreshStopList(index);
                RefreshPreview();
                return;
            }

            stops.RemoveAt(index);
            RefreshStopList(Math.Min(index, stops.Count - 1));
            RefreshPreview();
        }

        private void LoadSelectedStop()
        {
            if (updatingFields)
                return;

            updatingFields = true;

            try
            {
                if (GetSelectedStop() is not { } stop)
                {
                    colorText.Text = string.Empty;
                    offsetText.Text = string.Empty;
                    alphaText.Text = string.Empty;
                    return;
                }

                colorText.Text = ToHexWithoutAlpha(stop.Color);
                offsetText.Text = stop.Offset.ToString("0.###", CultureInfo.InvariantCulture);
                alphaText.Text = stop.Color.Alpha.ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                updatingFields = false;
            }
        }

        private void UpdateSelectedStopFromFields()
        {
            if (updatingFields || GetSelectedStop() is not { } stop)
                return;

            var changed = false;

            if (TryParseColor(colorText.Text, out var color))
            {
                var alpha = stop.Color.Alpha;

                if (byte.TryParse(alphaText.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAlpha))
                    alpha = parsedAlpha;

                color = color.WithAlpha(alpha);

                if (stop.Color != color)
                {
                    stop.Color = color;
                    changed = true;
                }
            }

            if (float.TryParse(offsetText.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
            {
                offset = Math.Clamp(offset, 0f, 1f);

                if (Math.Abs(stop.Offset - offset) > 0.0001f)
                {
                    stop.Offset = offset;
                    changed = true;
                }
            }

            if (!changed)
                return;

            RefreshStopList(stopsList.SelectedIndex);
            RefreshPreview();
        }

        private void RefreshStopList(int selectIndex)
        {
            updatingFields = true;

            try
            {
                stopsList.Items.Clear();

                foreach (var stop in stops)
                    stopsList.Items.Add(stop);

                if (stops.Count > 0)
                    stopsList.SelectedIndex = Math.Clamp(selectIndex, 0, stops.Count - 1);
            }
            finally
            {
                updatingFields = false;
            }

            LoadSelectedStop();
        }

        private void UpdateEditorEnabledState()
        {
            var enabled = !noBrushCheckBox.Checked;
            brushType.Enabled = enabled;
            stopsList.Enabled = enabled;
            colorText.Enabled = enabled;
            offsetText.Enabled = enabled;
            alphaText.Enabled = enabled;
        }

        private EditableGradientStop? GetSelectedStop()
        {
            var index = stopsList.SelectedIndex;
            return index >= 0 && index < stops.Count ? stops[index] : null;
        }

        private void RefreshPreview()
        {
            SelectedBrush = CreateBrushFromInputs();
            preview.Brush = SelectedBrush;
            preview.Invalidate();
        }

        private MfnBrush? CreateBrushFromInputs()
        {
            if (noBrushCheckBox.Checked)
                return null;

            var orderedStops = stops
                .OrderBy(stop => stop.Offset)
                .Select(stop => new GradientStop(stop.Color, stop.Offset))
                .ToArray();
            var primary = orderedStops.Length > 0 ? orderedStops[0].Color : SKColors.Transparent;

            if (brushType.SelectedIndex == 0)
                return new SolidColorBrush(primary);

            var brush = brushType.SelectedIndex switch
            {
                2 => (GradientBrush)new RadialGradientBrush(),
                3 => new SweepGradientBrush(),
                _ => new LinearGradientBrush()
            };

            if (orderedStops.Length == 0)
                brush.GradientStops.Add(new GradientStop(SKColors.Transparent, 0f));
            else
            {
                foreach (var stop in orderedStops)
                    brush.GradientStops.Add(stop);
            }

            return brush;
        }

        private static string ToHex(SKColor color)
            => color.Alpha == byte.MaxValue
                ? FormattableString.Invariant($"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}")
                : FormattableString.Invariant($"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

        private static string ToHexWithoutAlpha(SKColor color)
            => FormattableString.Invariant($"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

        private static bool TryParseColor(string text, out SKColor color)
        {
            var value = text.Trim().TrimStart('#');

            if (value.Length is not (6 or 8)
                || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            {
                color = SKColors.Transparent;
                return false;
            }

            color = value.Length == 6
                ? new SKColor((byte)((number >> 16) & 0xFF), (byte)((number >> 8) & 0xFF), (byte)(number & 0xFF))
                : new SKColor((byte)((number >> 16) & 0xFF), (byte)((number >> 8) & 0xFF), (byte)(number & 0xFF), (byte)((number >> 24) & 0xFF));
            return true;
        }

        private sealed class EditableGradientStop
        {
            public EditableGradientStop(SKColor color, float offset)
            {
                Color = color;
                Offset = Math.Clamp(offset, 0f, 1f);
            }

            public SKColor Color { get; set; }

            public float Offset { get; set; }

            public override string ToString()
                => FormattableString.Invariant($"{Offset:0.###}  {ToHex(Color)}");
        }
    }

    internal sealed class BrushPreviewPanel : Panel
    {
        public MfnBrush? Brush { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var bounds = new SKRect(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            e.Canvas.FillRectangle(ClientRectangle, SKColors.White);
            SkiaExtensions.RenderBrushBackground(e.Canvas, bounds, Brush, SKColors.White);
            e.Canvas.DrawRectangle(0, 0, Width - 1, Height - 1, SKColors.Gray);
        }
    }
}

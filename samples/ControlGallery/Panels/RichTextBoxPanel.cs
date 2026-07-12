using System;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class RichTextBoxPanel : Panel
    {
        private readonly RichTextBox editor;

        public RichTextBoxPanel()
        {
            editor = Controls.Add(new RichTextBox {
                Left = 10,
                Top = 10,
                Width = 560,
                Height = 240,
                Text = "ModernFormsNext RichTextBox\nSelect text and apply formatting.\nRTF is stored in the shared control model."
            });

            editor.Select(0, "ModernFormsNext".Length);
            editor.SelectionFont = new Font("Segoe UI", 15, FontStyle.Bold);
            editor.SelectionColor = Theme.AccentColor2;

            var markerStart = editor.Text.IndexOf("RTF");
            editor.Select(markerStart, 3);
            editor.SelectionBackColor = new SKColor(255, 244, 180);
            editor.SelectionFont = new Font("Segoe UI", 11, FontStyle.Bold | FontStyle.Underline);
            editor.DeselectAll();

            var bold = CreateToolButton(10, 265, 80, "Bold");
            bold.Click += (_, _) => ApplyFontStyle(FontStyle.Bold);

            var italic = CreateToolButton(100, 265, 80, "Italic");
            italic.Click += (_, _) => ApplyFontStyle(FontStyle.Italic);

            var blue = CreateToolButton(190, 265, 80, "Blue");
            blue.Click += (_, _) => ToggleSelectionColor(Theme.AccentColor2);

            var yellow = CreateToolButton(280, 265, 90, "Highlight");
            yellow.Click += (_, _) => ToggleSelectionBackColor(new SKColor(255, 244, 180));

            var zoomIn = CreateToolButton(380, 265, 80, "Zoom +");
            zoomIn.Click += (_, _) => RunEditorCommand(() => editor.ZoomFactor = Math.Min(3f, editor.ZoomFactor + 0.1f));

            var zoomOut = CreateToolButton(470, 265, 80, "Zoom -");
            zoomOut.Click += (_, _) => RunEditorCommand(() => editor.ZoomFactor = Math.Max(0.5f, editor.ZoomFactor - 0.1f));
        }

        private void ApplyFontStyle(FontStyle style)
        {
            RunEditorCommand(() => {
                var current = editor.SelectionFont ?? editor.Font;
                var nextStyle = current.Style.HasFlag(style)
                    ? current.Style & ~style
                    : current.Style | style;

                editor.SelectionFont = new Font(current.FamilyName, current.SizeInPoints, nextStyle);
            });
        }

        private ToolbarButton CreateToolButton(int left, int top, int width, string text)
        {
            return Controls.Add(new ToolbarButton {
                Left = left,
                Top = top,
                Width = width,
                Text = text
            });
        }

        private void RunEditorCommand(Action command)
        {
            command();
            editor.Select();
        }

        private void ToggleSelectionBackColor(SKColor color)
        {
            RunEditorCommand(() => editor.SelectionBackColor = editor.SelectionBackColor == color ? SKColor.Empty : color);
        }

        private void ToggleSelectionColor(SKColor color)
        {
            RunEditorCommand(() => editor.SelectionColor = editor.SelectionColor == color ? SKColor.Empty : color);
        }

        private sealed class ToolbarButton : Button
        {
            public ToolbarButton()
            {
                TabStop = false;
                SetControlBehavior(ControlBehaviors.Selectable, false);
            }
        }
    }
}

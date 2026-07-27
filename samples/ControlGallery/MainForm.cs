using ControlGallery.Panels;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery
{
    public class MainForm : Form
    {
        private Panel? current_panel;
        private readonly TreeView tree;

        public MainForm ()
        {
            Theme.UIFont = SKTypeface.FromFamilyName ("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            tree = new TreeView {
                Dock = DockStyle.Left,
                ShowDropdownGlyph = false
            };
            tree.Style.Border.Width = 0;
            tree.Style.Border.Right.Width = 1;

            tree.Items.Add ("Button", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Animations", ImageLoader.Get ("swatches.png"));
            tree.Items.Add ("Animations and Interaction Effects", ImageLoader.Get ("swatches.png"));
            tree.Items.Add ("CheckBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("CheckedListBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ComboBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Dialogs", ImageLoader.Get ("button.png"));
            tree.Items.Add("DataGridView", ImageLoader.Get("button.png"));
            tree.Items.Add ("FileDialogs", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FontDialog", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FlowLayoutPanel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FormPaint", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FormShortcuts", ImageLoader.Get ("button.png"));
            tree.Items.Add ("GroupBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ImageList", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Label", ImageLoader.Get ("button.png"));
            tree.Items.Add("LinkLabel", ImageLoader.Get("button.png"));
            tree.Items.Add ("ListBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ListView", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MarkdownEditor", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MarkdownViewer", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Menu", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MaskedTextBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MessageBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("NavigationPane", ImageLoader.Get ("button.png"));
            tree.Items.Add ("NotifyIcon", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Panel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Paint & Gradients", ImageLoader.Get ("swatches.png"));
            tree.Items.Add ("PictureBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Printing", ImageLoader.Get ("print.png"));
            tree.Items.Add ("ProgressBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("RadioButton", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Ribbon", ImageLoader.Get ("button.png"));
            tree.Items.Add ("RichTextBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ScrollableControl", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ScrollBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("SplitContainer", ImageLoader.Get ("button.png"));
            tree.Items.Add ("StatusBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Switch", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TabControl", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TableLayoutPanel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TabStrip", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TextBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TitleBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ToolBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ToolTip", ImageLoader.Get ("button.png"));
            tree.Items.Add("TrackBar", ImageLoader.Get("button.png"));
            tree.Items.Add("ThemeManager", ImageLoader.Get("swatches.png"));
            tree.Items.Add ("TreeView", ImageLoader.Get ("button.png"));
            tree.Items.Add("ColorDialog", ImageLoader.Get("button.png"));
            tree.Items.Add("DateTimePicker" , ImageLoader.Get("button.png"));
            tree.Items.Add("NumericUpDown", ImageLoader.Get("button.png"));

            tree.ItemSelected += Tree_ItemSelected;
            Controls.Add (tree);

            Text = "Control Gallery";
            Image = ImageLoader.Get ("button.png");
        }

        private void Tree_ItemSelected (object? sender, EventArgs<TreeViewItem> e)
        {
            if (current_panel != null) {
                Controls.Remove (current_panel);

                if (current_panel is BasePanel bp)
                    bp.UnloadPanel ();

                current_panel.Dispose ();
                current_panel = null;
            }

            var new_panel = CreatePanel (e.Value.Text);

            if (new_panel != null) {
                current_panel = new_panel;
                new_panel.Dock = DockStyle.Fill;
                Controls.Insert (0, new_panel);
            }
        }

        private Panel? CreatePanel (string text)
        {
            switch (text) {
                case "Animations":
                    return new AnimationSchedulerPanel ();
                case "Animations and Interaction Effects":
                    return new AnimationsAndInteractionEffectsPanel ();
                case "Button":
                    return new ButtonPanel ();
                case "CheckBox":
                    return new CheckBoxPanel ();
                case "CheckedListBox":
                    return new CheckedListBoxPanel ();
                case "ComboBox":
                    return new ComboBoxPanel ();
                case "DataGridView":
                    return new DataGridViewPanel();
                case "Dialogs":
                    return new DialogPanel ();
                case "FileDialogs":
                    return new FileDialogPanel ();
                case "FontDialog":
                    return new FontDialogPanel ();
                case "FlowLayoutPanel":
                    return new FlowLayoutPanelPanel ();
                case "FormShortcuts":
                    return new FormShortcutsPanel (this);
                case "GroupBox":
                    return new GroupBoxPanel ();
                case "ImageList":
                    return new ImageListPanel ();
                case "Label":
                    return new LabelPanel ();
                case "LinkLabel":
                    return new LinkLabelPanel();
                case "ListBox":
                    return new ListBoxPanel ();
                case "ListView":
                    return new ListViewPanel ();
                case "MarkdownEditor":
                    return new MarkdownEditorPanel ();
                case "MarkdownViewer":
                    return new MarkdownViewerPanel ();
                case "Menu":
                    return new MenuPanel ();
                case "MaskedTextBox":
                    return new MaskedTextBoxPanel ();
                case "MessageBox":
                    return new MessageBoxPanel ();
                case "NavigationPane":
                    return new NavigationPanePanel ();
                case "NotifyIcon":
                    return new NotifyIconPanel ();
                case "Panel":
                    return new PanelPanel ();
                case "Paint & Gradients":
                    return new PaintAndGradientsPanel ();
                case "PictureBox":
                    return new PictureBoxPanel ();
                case "Printing":
                    return new PrintingPanel ();
                case "ProgressBar":
                    return new ProgressBarPanel ();
                case "RadioButton":
                    return new RadioButtonPanel ();
                case "Ribbon":
                    return new RibbonPanel ();
                case "RichTextBox":
                    return new RichTextBoxPanel();
                case "ScrollableControl":
                    return new ScrollableControlPanel ();
                case "ScrollBar":
                    return new ScrollBarPanel ();
                case "SplitContainer":
                    return new SplitContainerPanel ();
                case "StatusBar":
                    return new StatusBarPanel ();
                case "Switch":
                    return new SwitchPanel ();
                case "TabControl":
                    return new TabControlPanel ();
                case "TableLayoutPanel":
                    return new TableLayoutPanelPanel ();
                case "TabStrip":
                    return new TabStripPanel ();
                case "TextBox":
                    return new TextBoxPanel ();
                case "TitleBar":
                    return new TitleBarPanel ();
                case "ToolBar":
                    return new ToolBarPanel ();
                case "ToolTip":
                    return new ToolTipPanel ();
                case "TrackBar":
                    return new TrackBarPanel();
                case "ThemeManager":
                    return new ThemeManagerPanel();
                case "TreeView":
                    return new TreeViewPanel ();
                case "ColorDialog":
                    return new ColorDialogPanel();
                case "DateTimePicker":
                    return new DateTimePickerPanel();
                case "NumericUpDown":
                    return new NumericUpDownPanel();
            }

            return null;
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (tree.SelectedItem.Text == "FormPaint") {
                e.Canvas.FillRectangle (Scale (300), Scale (50), Scale (100), Scale (100), SKColors.Red);

                DrawThemeColor (e.Canvas, Scale (450), Scale (50), Scale (150), Scale (40), Theme.BackgroundColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (90), Scale (150), Scale (40), Theme.ControlLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (130), Scale (150), Scale (40), Theme.ControlMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (170), Scale (150), Scale (40), Theme.ControlMidHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (210), Scale (150), Scale (40), Theme.ControlHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (250), Scale (150), Scale (40), Theme.ControlVeryHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (290), Scale (150), Scale (40), Theme.ControlHighlightLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (330), Scale (150), Scale (40), Theme.ControlHighlightMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (370), Scale (150), Scale (40), Theme.ControlHighlightHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (410), Scale (150), Scale (40), Theme.BorderLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (450), Scale (150), Scale (40), Theme.BorderMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (490), Scale (150), Scale (40), Theme.BorderHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (530), Scale (150), Scale (40), Theme.ForegroundColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (570), Scale (150), Scale (40), Theme.ForegroundDisabledColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (610), Scale (150), Scale (40), Theme.ForegroundColorOnAccent);
                DrawThemeColor (e.Canvas, Scale (450), Scale (650), Scale (150), Scale (40), Theme.AccentColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (690), Scale (150), Scale (40), Theme.AccentColor2);
                DrawThemeColor (e.Canvas, Scale (450), Scale (730), Scale (150), Scale (40), Theme.WarningHighlightColor);
            }
        }

        private static void DrawThemeColor (SKCanvas canvas, int x, int y, int width, int height, SKColor color)
        {
            canvas.FillRectangle (x, y, width, height, color);
            canvas.DrawText (color.ToString (), x + 10, y + 20, new SKPaint { Typeface = Theme.UIFont, Color = Theme.ForegroundColor });
        }

        protected override void OnPaintBackground (PaintEventArgs e)
        {
            base.OnPaintBackground (e);

            if (tree.SelectedItem.Text == "FormPaint")
                e.Canvas.Clear (SKColors.Green);
        }

        private int Scale (int value) => (int)(value * Scaling);
    }
}

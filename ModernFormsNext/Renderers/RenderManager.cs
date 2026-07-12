using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class to manage rendering.
    /// </summary>
    public static class RenderManager
    {
        private static readonly Dictionary<Type, Renderer> renderers = new Dictionary<Type, Renderer> ();

        static RenderManager ()
        {
            SetRenderer<Button> (new ButtonRenderer ());
            SetRenderer<CheckBox> (new CheckBoxRenderer ());
            SetRenderer<CheckedListBox> (new CheckedListBoxRenderer ());
            SetRenderer<ComboBox> (new ComboBoxRenderer ());
            SetRenderer<DataGridView>(new DataGridViewRenderer());
            SetRenderer<FormTitleBar> (new FormTitleBarRenderer ());
            SetRenderer<GroupBox> (new GroupBoxRenderer ());
            SetRenderer<Label> (new LabelRenderer ());
            SetRenderer<LinkLabel>(new LinkLabelRenderer());
            SetRenderer<ListBox> (new ListBoxRenderer ());
            SetRenderer<ListView> (new ListViewRenderer ());
            SetRenderer<Menu> (new MenuRenderer ());
            SetRenderer<MenuDropDown> (new MenuDropDownRenderer ());
            SetRenderer<NavigationPane> (new NavigationPaneRenderer ());
            SetRenderer<Panel> (new PanelRenderer ());
            SetRenderer<PictureBox> (new PictureBoxRenderer ());
            SetRenderer<ProgressBar> (new ProgressBarRenderer ());
            SetRenderer<RadioButton> (new RadioButtonRenderer ());
            SetRenderer<Ribbon> (new RibbonRenderer ());
            SetRenderer<RichTextBox> (new RichTextBoxRenderer ());
            SetRenderer<MarkdownEditorTextBox> (new MarkdownEditorTextBoxRenderer ());
            SetRenderer<ScrollableControl> (new ScrollableControlRenderer ());
            SetRenderer<ScrollBar> (new ScrollBarRenderer ());
            SetRenderer<SplitContainer> (new SplitContainerRenderer ());
            SetRenderer<Splitter> (new SplitterRenderer ());
            SetRenderer<StatusBar> (new StatusBarRenderer ());
            SetRenderer<Switch> (new SwitchRenderer ());
            SetRenderer<TabControl> (new TabControlRenderer ());
            SetRenderer<TabStrip> (new TabStripRenderer ());
            SetRenderer<TextBox> (new TextBoxRenderer ());
            SetRenderer<ToolTipPopupControl> (new ToolTipPopupControlRenderer ());
            SetRenderer<ToolBar> (new ToolBarRenderer ());
            SetRenderer<TreeView> (new TreeViewRenderer ());
            SetRenderer<TrackBar> (new TrackBarRenderer ());
            SetRenderer<ColorBox> (new ColorBoxRenderer ());
            SetRenderer<HueSlider> (new HueSliderRenderer ());
            SetRenderer<DateTimePicker> (new DateTimePickerRenderer ());
            SetRenderer<DateTimePickerCalendar> (new DateTimePickerCalendarRenderer ());
            SetRenderer<DocumentViewer> (new DocumentViewerRenderer ());
            SetRenderer<NumericUpDown> (new NumericUpDownRenderer ());
        }

        /// <summary>
        /// Gets a renderer of the requested type.
        /// </summary>
        public static T? GetRenderer<T> () where T : Renderer
        {
            return renderers.Values.OfType<T> ().FirstOrDefault ();
        }

        /// <summary>
        /// Gets a renderer for the requested control.
        /// </summary>
        public static T? GetRenderer<T> (Control control) where T : Renderer
        {
            var type = (Type?)control.GetType ();

            while (type != null && type != typeof (object)) {
                if (renderers.TryGetValue (type, out var renderer)) {
                    return renderer as T;
                }

                type = type.BaseType;
            }

            throw new InvalidOperationException ($"No renderer found for type {typeof (T).FullName}");
        }

        /// <summary>
        /// Renders the specified control.
        /// </summary>
        public static void Render<T> (T control, PaintEventArgs e) where T : Control
        {
            var renderer = GetRenderer<Renderer> (control);
            renderer?.Render (control, e);
        }

        /// <summary>
        /// Registers a renderer for a control class.
        /// </summary>
        public static void SetRenderer<T> (Renderer renderer) where T : Control
        {
            if (renderer.Type != typeof (T))
                throw new InvalidOperationException ($"Invalid renderer for type {typeof (T).FullName}");

            renderers[typeof (T)] = renderer;
        }
    }
}

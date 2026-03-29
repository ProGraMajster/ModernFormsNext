using System.Threading.Tasks;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a dialog that allows the user to select a color.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog provides a user interface for selecting a color, typically using
    /// HSV-based controls such as <see cref="HueSlider"/> and <see cref="ColorBox"/>.
    /// </para>
    /// <para>
    /// The selected color is stored in the <see cref="Color"/> property and can be
    /// accessed after the dialog is closed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var dialog = new ColorDialog();
    /// var result = await dialog.ShowDialog(this);
    ///
    /// if (result == DialogResult.OK)
    /// {
    ///     var color = dialog.Color;
    /// }
    /// </code>
    /// </example>
    public class ColorDialog
    {
        private SKColor selectedColor = SKColors.White;

        /// <summary>
        /// Gets or sets the currently selected color.
        /// </summary>
        /// <value>
        /// A <see cref="SKColor"/> representing the selected color.
        /// </value>
        /// <remarks>
        /// This value is used as the initial color when the dialog is opened and is
        /// updated when the user confirms the selection.
        /// </remarks>
        public SKColor Color {
            get => selectedColor;
            set => selectedColor = value;
        }

        /// <summary>
        /// Displays the color selection dialog asynchronously.
        /// </summary>
        /// <param name="owner">
        /// The parent <see cref="Form"/> that owns this dialog.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a <see cref="DialogResult"/> indicating how the dialog was closed.
        /// </returns>
        /// <remarks>
        /// <para>
        /// If the user confirms the selection (OK), the <see cref="Color"/> property
        /// is updated with the selected value.
        /// </para>
        /// <para>
        /// If the dialog is canceled, the <see cref="Color"/> property remains unchanged.
        /// </para>
        /// </remarks>
        public async Task<DialogResult> ShowDialog (Form owner)
        {
            var form = new ColorDialogForm (selectedColor);

            var result = await form.ShowDialog (owner);

            if (result == DialogResult.OK)
                selectedColor = form.SelectedColor;

            return result;
        }
    }
}

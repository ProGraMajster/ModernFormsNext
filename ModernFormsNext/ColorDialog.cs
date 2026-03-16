using System.Threading.Tasks;
using SkiaSharp;

namespace ModernFormsNext
{
    public class ColorDialog
    {
        private SKColor selectedColor = SKColors.White;

        public SKColor Color {
            get => selectedColor;
            set => selectedColor = value;
        }

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

using System.Drawing;

namespace ModernFormsNext
{
    internal sealed class ToolTipInfo
    {
        public ToolTipInfo(string? caption, ToolTipDisplayMode displayMode)
        {
            Caption = caption;
            DisplayMode = displayMode;
        }

        public string? Caption { get; set; }

        public ToolTipDisplayMode DisplayMode { get; }

        public Point? ScreenLocation { get; set; }
    }
}

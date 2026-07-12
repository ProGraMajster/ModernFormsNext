using System;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Stores rich formatting for a contiguous range of RichTextBox text.
    /// </summary>
    /// <remarks>
    /// The range is tracked separately from <see cref="TextBoxDocument"/> because the base
    /// text document owns caret movement, selection, and editing, while <see cref="RichTextBox"/>
    /// adds character formatting on top. All offsets are UTF-16 string indexes to match the
    /// existing text box model.
    /// </remarks>
    internal sealed class RichTextBoxTextRun
    {
        public RichTextBoxTextRun(int start, int length, RichTextBoxTextStyle style)
        {
            Start = start;
            Length = length;
            Style = style.Clone();
        }

        public int End => Start + Length;

        public int Length { get; set; }

        public int Start { get; set; }

        public RichTextBoxTextStyle Style { get; set; }

        public RichTextBoxTextRun Clone()
            => new RichTextBoxTextRun(Start, Length, Style);
    }

    /// <summary>
    /// Describes the formatting applied to a rich text run.
    /// </summary>
    internal sealed class RichTextBoxTextStyle : IEquatable<RichTextBoxTextStyle>
    {
        public SKColor? BackColor { get; set; }

        public Font? Font { get; set; }

        public SKColor? ForeColor { get; set; }

        public RichTextBoxTextStyle Clone()
            => new RichTextBoxTextStyle {
                BackColor = BackColor,
                Font = Font,
                ForeColor = ForeColor
            };

        public bool Equals(RichTextBoxTextStyle? other)
            => other is not null
                && Nullable.Equals(BackColor, other.BackColor)
                && Equals(Font, other.Font)
                && Nullable.Equals(ForeColor, other.ForeColor);

        public override bool Equals(object? obj)
            => Equals(obj as RichTextBoxTextStyle);

        public override int GetHashCode()
            => HashCode.Combine(BackColor, Font, ForeColor);
    }
}

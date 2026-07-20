using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Centralizes the isolation policy for mutable theme values.
/// </summary>
internal static class ThemeValueCloner
{
    public static object CloneValue(object value)
        => value is MfnBrush brush ? CloneBrush(brush) : value;

    public static MfnBrush CloneBrush(MfnBrush source)
    {
        ArgumentNullException.ThrowIfNull(source);

        MfnBrush clone = source switch
        {
            SolidColorBrush solid when solid.GetType() == typeof(SolidColorBrush) =>
                new SolidColorBrush(solid.PaintColor),
            LinearGradientBrush linear when linear.GetType() == typeof(LinearGradientBrush) =>
                CloneLinear(linear),
            RadialGradientBrush radial when radial.GetType() == typeof(RadialGradientBrush) =>
                CloneRadial(radial),
            SweepGradientBrush sweep when sweep.GetType() == typeof(SweepGradientBrush) =>
                CloneSweep(sweep),
            GlassBrush glass when glass.GetType() == typeof(GlassBrush) =>
                new GlassBrush
                {
                    Tint = glass.Tint,
                    SecondaryTint = glass.SecondaryTint,
                    Highlight = glass.Highlight,
                    Border = glass.Border,
                    ShowHighlight = glass.ShowHighlight,
                    ShowInnerBorder = glass.ShowInnerBorder
                },
            NoBrush when source.GetType() == typeof(NoBrush) => new NoBrush(),
            _ => throw new NotSupportedException(
                $"Theme brushes do not support the concrete type '{source.GetType().FullName}'.")
        };

        clone.Opacity = source.Opacity;
        clone.Transform = source.Transform;
        return clone;
    }

    private static LinearGradientBrush CloneLinear(LinearGradientBrush source)
    {
        var clone = new LinearGradientBrush
        {
            Start = source.Start,
            End = source.End,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static RadialGradientBrush CloneRadial(RadialGradientBrush source)
    {
        var clone = new RadialGradientBrush
        {
            CenterPoint = source.CenterPoint,
            GradientOrigin = source.GradientOrigin,
            Radius = source.Radius,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static SweepGradientBrush CloneSweep(SweepGradientBrush source)
    {
        var clone = new SweepGradientBrush
        {
            CenterPoint = source.CenterPoint,
            StartAngle = source.StartAngle,
            EndAngle = source.EndAngle,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static void CopyStops(GradientBrush source, GradientBrush destination)
    {
        foreach (GradientStop stop in source.GradientStops)
            destination.GradientStops.Add(new GradientStop(stop.PaintColor, stop.Offset));
    }
}

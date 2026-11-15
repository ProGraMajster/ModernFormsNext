using ModernFormsNext;
using SkiaSharp;

namespace ProjectFixture;

// Deliberate failure modes for a private Designer qualification session.
public sealed class PreviewBadge : UserControl
{
    private const string PrivateError = "PRIVATE_EXCEPTION_MESSAGE";

    private string caption = "default";

    public string Caption
    {
        get => caption;
        set
        {
            if (!DesignMode || value == "setter")
                throw new InvalidOperationException(PrivateError);
            caption = value;
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (Caption == "layout")
            throw new InvalidOperationException(PrivateError);
        base.OnLayout(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        switch (Caption)
        {
            case "paint":
                throw new InvalidOperationException(PrivateError);
            case "hang":
                Thread.Sleep(Timeout.Infinite);
                break;
            case "crash":
                Environment.Exit(71);
                break;
        }

        e.Canvas.Clear(Caption == "approved" ? SKColors.MediumSeaGreen : SKColors.OrangeRed);
        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint { Color = SKColors.White };
        e.Canvas.DrawText("custom C#", 6, 18, font, paint);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Caption == "dispose")
            throw new InvalidOperationException(PrivateError);
    }
}

public sealed class ThrowingBadge : UserControl
{
    public ThrowingBadge() => throw new InvalidOperationException("PRIVATE_EXCEPTION_MESSAGE");
}

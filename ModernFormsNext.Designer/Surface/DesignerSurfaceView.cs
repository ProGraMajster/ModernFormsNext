namespace ModernFormsNext.Designer.Surface;

internal readonly record struct DesignerSurfaceView(
    float Scale,
    int FormX,
    int FormY,
    int TitleHeight,
    int Border,
    int ClientWidth,
    int ClientHeight)
{
    public int ClientX => FormX + Border;

    public int ClientY => FormY + TitleHeight + Border;

    public int FormWidth => ClientWidth + (Border * 2);

    public int FormHeight => TitleHeight + ClientHeight + (Border * 2);
}

using ModernFormsNext.Designing;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;
using MfnFont = ModernFormsNext.Font;

namespace ModernFormsNext.Designer.Properties;

internal static class DesignerPropertyDialogEditors
{
    public static Func<DesignerPropertyDialogContext, Task<bool>> Color(
        Func<SKColor?> getColor,
        Func<SKColor?, (bool Success, string? Error)> setColor)
        => async context =>
        {
            var dialog = new ColorDialog
            {
                Color = getColor() ?? SKColors.White
            };

            if (await dialog.ShowDialog(context.Owner) != DialogResult.OK)
                return false;

            var result = setColor(dialog.Color);

            if (!result.Success)
            {
                context.Session.Log($"Color editor failed: {result.Error}");
                return false;
            }

            return true;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> Font(
        Func<MfnFont?> getFont,
        Func<MfnFont?, (bool Success, string? Error)> setFont)
        => async context =>
        {
            var dialog = new FontDialog
            {
                Font = getFont() ?? new MfnFont("Segoe UI", 9),
                RenderingMode = FontDialogRenderingMode.Auto
            };

            if (await dialog.ShowDialog(context.Owner) != DialogResult.OK)
                return false;

            var result = setFont(dialog.Font);

            if (!result.Success)
            {
                context.Session.Log($"Font editor failed: {result.Error}");
                return false;
            }

            return true;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> Brush(
        Func<MfnBrush?> getBrush,
        Func<MfnBrush?, (bool Success, string? Error)> setBrush)
        => async context =>
        {
            var dialog = new BrushEditDialog
            {
                Brush = getBrush()
            };

            if (await dialog.ShowDialog(context.Owner) != DialogResult.OK)
                return false;

            var result = setBrush(dialog.Brush);

            if (!result.Success)
            {
                context.Session.Log($"Brush editor failed: {result.Error}");
                return false;
            }

            return true;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> ImageLocation(DesignControlNode node)
        => async context =>
        {
            var currentPath = GetStoredString(node, "ImageLocation");
            var dialog = new DesignerImagePickerDialog(context.Session.CurrentDocumentPath, currentPath);

            if (await dialog.ShowDialog(context.Owner) != DialogResult.OK
                || string.IsNullOrWhiteSpace(dialog.SelectedImagePath))
            {
                return false;
            }

            node.Properties["ImageLocation"] = DesignPropertyValue.FromString(dialog.SelectedImagePath);
            return true;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> TabPages(DesignControlNode tabControl)
        => async context =>
        {
            var dialog = new DesignerTabPageCollectionDialog(context.Session, tabControl);

            return await dialog.ShowDialog(context.Owner) == DialogResult.OK;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> InteractionEffects(
        IDictionary<string, DesignPropertyValue> properties)
        => async context =>
        {
            var dialog = new DesignerInteractionEffectCollectionDialog(context.Session, properties);
            return await dialog.ShowDialog(context.Owner) == DialogResult.OK;
        };

    public static Func<DesignerPropertyDialogContext, Task<bool>> Transition(
        IDictionary<string, DesignPropertyValue> properties,
        bool isLayout)
        => async context =>
        {
            var dialog = new DesignerTransitionDialog(context.Session, properties, isLayout);
            return await dialog.ShowDialog(context.Owner) == DialogResult.OK;
        };

    private static string? GetStoredString(DesignControlNode node, string propertyName)
        => node.Properties.TryGetValue(propertyName, out var value)
            ? value.Value?.ToString()
            : null;
}

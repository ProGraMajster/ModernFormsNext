using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.VisualStudio.Shell;

namespace ModernFormsNext.VisualStudioExtension.Options;

/// <summary>
/// Provides per-user Visual Studio options for the ModernFormsNext Designer.
/// </summary>
public sealed class DesignerOptionsPage : DialogPage
{
    /// <summary>
    /// Gets or sets how newly opened Designer documents are hosted.
    /// </summary>
    /// <remarks>
    /// Visual Studio persists this option in its user settings store. Changing it does not move
    /// an already running Designer window; the new value is captured when a document is opened.
    /// </remarks>
    [Category("Designer")]
    [DisplayName("Designer hosting")]
    [Description("Choose whether newly opened Designer documents are integrated in Visual Studio or shown in a separate window. Changes apply after reopening a Designer document.")]
    [DefaultValue(DesignerHostingMode.Integrated)]
    [TypeConverter(typeof(DesignerHostingModeTypeConverter))]
    public DesignerHostingMode HostingMode { get; set; } = DesignerHostingMode.Integrated;
}

/// <summary>
/// Supplies user-facing labels for Designer hosting modes in the Visual Studio property grid.
/// </summary>
public sealed class DesignerHostingModeTypeConverter : EnumConverter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerHostingModeTypeConverter"/> class.
    /// </summary>
    public DesignerHostingModeTypeConverter()
        : base(typeof(DesignerHostingMode))
    {
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string text)
        {
            if (string.Equals(text, "Integrated in Visual Studio", StringComparison.OrdinalIgnoreCase))
                return DesignerHostingMode.Integrated;
            if (string.Equals(text, "Separate window", StringComparison.OrdinalIgnoreCase))
                return DesignerHostingMode.Standalone;
        }

        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc/>
    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is DesignerHostingMode mode)
        {
            return mode == DesignerHostingMode.Standalone
                ? "Separate window"
                : "Integrated in Visual Studio";
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

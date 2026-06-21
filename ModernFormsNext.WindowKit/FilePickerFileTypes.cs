namespace ModernFormsNext.WindowKit.Platform.Storage;

/// <summary>
/// Dictionary of well known file types.
/// </summary>
public static class FilePickerFileTypes
{
    /// <summary>
    /// Matches any file that the platform picker can expose.
    /// </summary>
    public static FilePickerFileType All { get; } = new("All")
    {
        Patterns = new[] { "*.*" },
        MimeTypes = new[] { "*/*" }
    };

    /// <summary>
    /// Matches plain text files.
    /// </summary>
    public static FilePickerFileType TextPlain { get; } = new("Plain Text")
    {
        Patterns = new[] { "*.txt" },
        AppleUniformTypeIdentifiers = new[] { "public.plain-text" },
        MimeTypes = new[] { "text/plain" }
    };

    /// <summary>
    /// Matches common raster image file types.
    /// </summary>
    public static FilePickerFileType ImageAll { get; } = new("All Images")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" },
        AppleUniformTypeIdentifiers = new[] { "public.image" },
        MimeTypes = new[] { "image/*" }
    };

    /// <summary>
    /// Matches JPEG image files.
    /// </summary>
    public static FilePickerFileType ImageJpg { get; } = new("JPEG image")
    {
        Patterns = new[] { "*.jpg", "*.jpeg" },
        AppleUniformTypeIdentifiers = new[] { "public.jpeg" },
        MimeTypes = new[] { "image/jpeg" }
    };

    /// <summary>
    /// Matches PNG image files.
    /// </summary>
    public static FilePickerFileType ImagePng { get; } = new("PNG image")
    {
        Patterns = new[] { "*.png" },
        AppleUniformTypeIdentifiers = new[] { "public.png" },
        MimeTypes = new[] { "image/png" }
    };

    /// <summary>
    /// Matches PDF document files.
    /// </summary>
    public static FilePickerFileType Pdf { get; } = new("PDF document")
    {
        Patterns = new[] { "*.pdf" },
        AppleUniformTypeIdentifiers = new[] { "com.adobe.pdf" },
        MimeTypes = new[] { "application/pdf" }
    };
}

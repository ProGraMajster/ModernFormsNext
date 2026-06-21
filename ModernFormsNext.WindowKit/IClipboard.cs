using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Platform
{
    /// <summary>
    /// Provides platform clipboard access.
    /// </summary>
    /// <remarks>
    /// Clipboard operations are platform-specific and should be called from the UI thread unless
    /// a backend explicitly documents broader thread-safety.
    /// </remarks>
    [NotClientImplementable]
    public interface IClipboard
    {
        /// <summary>
        /// Gets text from the clipboard.
        /// </summary>
        /// <returns>The clipboard text, or <see langword="null"/> when text is not available.</returns>
        Task<string?> GetTextAsync();

        /// <summary>
        /// Sets clipboard text.
        /// </summary>
        /// <param name="text">The text to place on the clipboard, or <see langword="null"/> to clear text content.</param>
        /// <returns>A task that completes when the platform clipboard has been updated.</returns>
        Task SetTextAsync(string? text);

        /// <summary>
        /// Clears clipboard content.
        /// </summary>
        /// <returns>A task that completes when the clipboard has been cleared.</returns>
        Task ClearAsync();
        
        /// <summary>
        /// Sets clipboard content from a data object.
        /// </summary>
        /// <param name="data">The data object to place on the clipboard.</param>
        /// <returns>A task that completes when the platform clipboard has been updated.</returns>
        Task SetDataObjectAsync(IDataObject data);
        
        /// <summary>
        /// Gets the data formats currently available on the clipboard.
        /// </summary>
        /// <returns>The available clipboard format identifiers.</returns>
        Task<string[]> GetFormatsAsync();
        
        /// <summary>
        /// Gets clipboard data for a specific format.
        /// </summary>
        /// <param name="format">The clipboard format identifier to retrieve.</param>
        /// <returns>The clipboard data, or <see langword="null"/> when the format is not available.</returns>
        Task<object?> GetDataAsync(string format);
    }
}

using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Platform
{
    [NotClientImplementable]
    public interface IClipboard
    {
        Task<string?> GetTextAsync();

        Task SetTextAsync(string? text);

        Task ClearAsync();
        
        Task SetDataObjectAsync(IDataObject data);
        
        Task<string[]> GetFormatsAsync();
        
        Task<object?> GetDataAsync(string format);
    }
}

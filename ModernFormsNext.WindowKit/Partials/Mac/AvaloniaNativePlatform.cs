using ModernFormsNext.WindowKit.Mac.Interop;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Native
{
    partial class AvaloniaNativePlatform
    {
        public IWindowImpl CreateWindow()
        {
            return new WindowImpl(_factory, _options);
        }

        public IAvaloniaNativeFactory Factory => _factory;

        public static AvaloniaNativePlatform Initialize()
        {
            var options = new AvaloniaNativePlatformOptions();
            return Initialize(CreateAvaloniaNative(), options);
        }

    }
}

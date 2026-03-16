namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.WinRT
{
    internal enum BlurEffect
    {
        None,
        Acrylic,
        Mica
    }
    
    internal interface IBlurHost
    {
        void SetBlur(BlurEffect enable);
    }
}

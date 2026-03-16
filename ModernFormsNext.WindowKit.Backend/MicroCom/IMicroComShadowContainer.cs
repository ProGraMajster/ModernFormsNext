namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    public interface IMicroComShadowContainer
    {
        MicroComShadow Shadow { get; set; }
        void OnReferencedFromNative();
        void OnUnreferencedFromNative();
    }
}

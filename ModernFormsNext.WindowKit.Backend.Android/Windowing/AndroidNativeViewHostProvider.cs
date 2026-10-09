using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

// Stable window feature; replacement native presentations never replace the shared Form.
internal sealed class AndroidNativeViewHostProvider : INativeViewHostProvider
{
    private INativeViewHostProvider? presentation;
    public NativeViewCapabilities Capabilities => NativeViewCapabilities.Baseline;
    public bool IsAvailable => presentation?.IsAvailable == true;
    public bool IsVisible => presentation?.IsVisible == true;
    public bool IsEnabled => presentation?.IsEnabled == true;
    public event Action? Changed;
    internal void NotifyState() => Changed?.Invoke();
    internal void Attach(INativeViewHostProvider? next)
    {
        presentation = next;
        Changed?.Invoke();
    }
    internal void ConfirmGeometry() => Changed?.Invoke();
    internal void Detach(INativeViewHostProvider previous)
    {
        // Epoch revocation can precede presentation disposal. Compare the concrete lease,
        // releasing this root without clearing a newer reentrant presentation.
        if (ReferenceEquals(presentation, previous)) Attach(null);
    }
    public INativeViewSession CreateSession(INativeViewFactory factory, INativeViewHostCallbacks callbacks)
        => presentation?.CreateSession(factory, callbacks) ?? throw new InvalidOperationException("No Android native presentation.");
    public void FocusFramework() => presentation?.FocusFramework();
}

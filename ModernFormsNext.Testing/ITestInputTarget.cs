namespace ModernFormsNext.Testing;

// Both top-level test windows and production PopupWindow instances use the same raw input
// adapter. This contract carries access/lifetime information, never a second control tree.
internal interface ITestInputTarget
{
    WindowBase HostedWindow { get; }
    HeadlessWindowImpl Backend { get; }
    TestViewport Viewport { get; }
    Control? FocusedControl { get; }
    ulong InputTimestamp { get; }
    bool IsClosed { get; }
    bool CanReceiveInput { get; }
    void VerifyInputAccess();
    bool Contains(Control candidate);
}

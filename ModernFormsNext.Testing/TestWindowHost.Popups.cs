namespace ModernFormsNext.Testing;

public sealed partial class TestWindowHost : ITestInputTarget
{
    WindowBase ITestInputTarget.HostedWindow => hostedForm;
    HeadlessWindowImpl ITestInputTarget.Backend => backend;
    ulong ITestInputTarget.InputTimestamp => InputTimestamp;
    bool ITestInputTarget.CanReceiveInput => CanReceiveInput;
    void ITestInputTarget.VerifyInputAccess() => VerifyInputAccess();
    bool ITestInputTarget.Contains(Control candidate) => Contains(candidate);

    /// <summary>Gets a snapshot list of handles to this window's live production popups, including hidden popups.</summary>
    /// <remarks>
    /// Call on the host UI thread. Popup controls create these windows through the normal backend
    /// contract. Each handle exposes the real popup tree, input and rendering. Parent close owns
    /// cleanup; a popup hidden for later reuse remains in this list until destroyed.
    /// </remarks>
    public IReadOnlyList<TestPopupHost> Popups
    {
        get
        {
            VerifyInputAccess();
            return Array.AsReadOnly(backend.LivePopups
                .Select(popup => popup.GetTestHost(this)).ToArray());
        }
    }

    /// <summary>Gets this window's currently active production popup, or null when it has none.</summary>
    /// <remarks>Call on the host UI thread. This reflects framework popup state, not OS activation.</remarks>
    public TestPopupHost? ActivePopup
        => Popups.FirstOrDefault(popup => popup.IsVisible && ReferenceEquals(popup.Window, Application.ActivePopupWindow));
}

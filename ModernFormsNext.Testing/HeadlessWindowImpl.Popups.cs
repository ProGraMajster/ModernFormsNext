using System.Runtime.ExceptionServices;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Testing;

internal sealed partial class HeadlessWindowImpl
{
    private readonly List<HeadlessPopupImpl> ownedPopups = [];

    internal IEnumerable<HeadlessPopupImpl> LivePopups
        => ownedPopups.Where(popup => !popup.State.IsDisposed && popup.HostedPopup is not null);

    private IPopupImpl CreateHeadlessPopup()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var popup = new HeadlessPopupImpl(this);
        ownedPopups.Add(popup);
        return popup;
    }

    private void DisposeOwnedPopupsAndNotifyClosed()
    {
        var failures = new List<Exception>();
        // Popups are owned even when application code never asks for a TestPopupHost. Finish
        // every child before publishing parent closure, including when a callback throws.
        foreach (HeadlessPopupImpl popup in ownedPopups.ToArray())
        {
            try { popup.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        ownedPopups.Clear();
        try { Closed?.Invoke(); }
        catch (Exception exception) { failures.Add(exception); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Headless popup/window cleanup failed.", failures);
    }
}

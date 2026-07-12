using System;
using System.Drawing;

namespace ModernFormsNext.Documents;

/// <summary>
/// Tracks one primary-pointer link press independently from text selection and rendering.
/// </summary>
internal sealed class DocumentLinkInteractionState
{
    internal const int DragThresholdLogicalPixels = 3;

    private LinkInline? activationLink;
    private Point pointerDownLocation;

    public bool DragStarted { get; private set; }

    public bool IsPointerDown { get; private set; }

    public LinkInline? PressedLink { get; private set; }

    public bool Begin(LinkInline? link, Point location)
    {
        var changed = !ReferenceEquals(PressedLink, link);

        activationLink = link;
        PressedLink = link;
        pointerDownLocation = location;
        DragStarted = false;
        IsPointerDown = true;

        return changed;
    }

    public DocumentLinkMoveResult Move(LinkInline? linkUnderPointer, Point location, int dragThreshold)
    {
        if (!IsPointerDown)
            return default;

        var dragStartedNow = false;
        if (!DragStarted && IsOutsideDragThreshold(location, Math.Max(1, dragThreshold)))
        {
            DragStarted = true;
            dragStartedNow = true;
            activationLink = null;
        }

        var visualLink = !DragStarted && ReferenceEquals(activationLink, linkUnderPointer)
            ? activationLink
            : null;
        var visualStateChanged = !ReferenceEquals(PressedLink, visualLink);
        PressedLink = visualLink;

        return new DocumentLinkMoveResult(visualStateChanged, dragStartedNow);
    }

    public bool LeaveLink()
    {
        if (!IsPointerDown || PressedLink is null)
            return false;

        PressedLink = null;
        return true;
    }

    public LinkInline? Complete(LinkInline? linkUnderPointer)
    {
        var activatedLink = IsPointerDown
            && !DragStarted
            && activationLink is not null
            && ReferenceEquals(activationLink, linkUnderPointer)
                ? activationLink
                : null;

        Reset();
        return activatedLink;
    }

    public bool Cancel()
    {
        var changed = PressedLink is not null || IsPointerDown || DragStarted;
        Reset();
        return changed;
    }

    private bool IsOutsideDragThreshold(Point location, int dragThreshold)
    {
        var deltaX = Math.Abs((long)location.X - pointerDownLocation.X);
        var deltaY = Math.Abs((long)location.Y - pointerDownLocation.Y);
        return deltaX >= dragThreshold || deltaY >= dragThreshold;
    }

    private void Reset()
    {
        activationLink = null;
        PressedLink = null;
        pointerDownLocation = Point.Empty;
        DragStarted = false;
        IsPointerDown = false;
    }
}

internal readonly record struct DocumentLinkMoveResult(bool VisualStateChanged, bool DragStartedNow);

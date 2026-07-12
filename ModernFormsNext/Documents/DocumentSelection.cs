using System;

namespace ModernFormsNext.Documents;

internal sealed class DocumentSelection
{
    private int anchor;
    private int active;

    public int End => Math.Max(anchor, active);

    public int Length => End - Start;

    public int Start => Math.Min(anchor, active);

    public bool Clear()
        => Set(0, 0, int.MaxValue);

    public bool Select(int start, int length, int textLength)
    {
        start = Math.Clamp(start, 0, textLength);
        length = Math.Clamp(length, 0, textLength - start);
        return Set(start, start + length, textLength);
    }

    public bool SelectFromAnchor(int selectionAnchor, int selectionActive, int textLength)
        => Set(selectionAnchor, selectionActive, textLength);

    private bool Set(int newAnchor, int newActive, int textLength)
    {
        newAnchor = Math.Clamp(newAnchor, 0, textLength);
        newActive = Math.Clamp(newActive, 0, textLength);

        if (anchor == newAnchor && active == newActive)
            return false;

        anchor = newAnchor;
        active = newActive;
        return true;
    }
}

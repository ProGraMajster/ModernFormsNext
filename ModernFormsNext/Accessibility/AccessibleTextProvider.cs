using System;
using System.Collections.Generic;
using System.Drawing;

namespace ModernFormsNext.Accessibility;

/// <summary>Describes a logical or rendered unit used to navigate accessible text.</summary>
public enum AccessibleTextUnit
{
    /// <summary>A complete caret cluster, without splitting a Unicode text element.</summary>
    Character,
    /// <summary>A contiguous span with the same effective supported formatting.</summary>
    Format,
    /// <summary>A word boundary supplied by the editor's layout engine.</summary>
    Word,
    /// <summary>A rendered, possibly wrapped, line.</summary>
    Line,
    /// <summary>A paragraph delimited by a document line break.</summary>
    Paragraph,
    /// <summary>A page, promoted to Document by editors without pagination.</summary>
    Page,
    /// <summary>The complete document.</summary>
    Document
}

/// <summary>Identifies one end of an accessible text range.</summary>
public enum AccessibleTextEndpoint
{
    /// <summary>The inclusive logical start.</summary>
    Start,
    /// <summary>The exclusive logical end.</summary>
    End
}

/// <summary>Identifies supported platform-neutral text formatting and state metadata.</summary>
public enum AccessibleTextAttribute
{
    /// <summary>The effective font family, as a string.</summary>
    FontName,
    /// <summary>The effective rendered font size in logical pixels, as a double.</summary>
    /// <remarks>Native adapters convert this layout measurement to their required units, such as UIA points.</remarks>
    FontSize,
    /// <summary>The effective font weight, as an integer from 1 through 1000.</summary>
    FontWeight,
    /// <summary>Whether the text uses italic styling.</summary>
    Italic,
    /// <summary>Whether the text is underlined.</summary>
    Underline,
    /// <summary>Whether the text is struck through.</summary>
    Strikethrough,
    /// <summary>The effective foreground color, encoded as ARGB in an integer.</summary>
    ForegroundColor,
    /// <summary>The effective background color, encoded as ARGB in an integer.</summary>
    BackgroundColor,
    /// <summary>Whether the editor prohibits text modification.</summary>
    IsReadOnly,
    /// <summary>Whether the represented text is hidden from view.</summary>
    IsHidden
}

/// <summary>Provides identity values for mixed and unsupported accessible text attributes.</summary>
public static class AccessibleTextAttributeValues
{
    /// <summary>A requested attribute has different values within the range.</summary>
    public static object Mixed { get; } = new Marker("Mixed");
    /// <summary>The text provider does not implement the requested attribute.</summary>
    public static object NotSupported { get; } = new Marker("NotSupported");
    private sealed record Marker(string Name);
}

/// <summary>Exposes one existing editor's text and viewport through its canonical accessible object.</summary>
/// <remarks>
/// Every operation requires the owning UI thread. The provider does not own or focus the editor.
/// Ranges remain independent of native IME focus sessions. Sensitive content must not be exposed,
/// including through lengths, selection or formatting. No member writes document text.
/// </remarks>
public abstract class AccessibleTextProvider
{
    /// <summary>Gets the existing accessible object that encloses this text stream.</summary>
    public abstract AccessibleObject Owner { get; }
    /// <summary>Gets a fresh range spanning the entire live document.</summary>
    public abstract AccessibleTextRange DocumentRange { get; }

    /// <summary>Creates a range from absolute UTF-16 offsets in the current document.</summary>
    /// <param name="start">Inclusive start between zero and the document length.</param>
    /// <param name="end">Exclusive end between zero and the document length.</param>
    /// <remarks>Call on the UI thread. Surrogate boundaries are normalized without splitting a scalar.</remarks>
    public abstract AccessibleTextRange RangeFromOffsets(int start, int end);

    /// <summary>Sets the document's single selection while preserving its anchor and caret direction.</summary>
    /// <param name="anchor">Absolute UTF-16 selection anchor.</param>
    /// <param name="caret">Absolute UTF-16 active caret endpoint.</param>
    /// <returns>Whether the live, available document accepted the selection.</returns>
    /// <remarks>Call on the UI thread. Accepts visible preedit before moving selection. Read-only text may be selected.</remarks>
    public abstract bool SetSelection(int anchor, int caret);
    /// <summary>Gets whether a single contiguous text selection can be made.</summary>
    public virtual bool SupportsSelection => true;
    /// <summary>Gets the current selection, or one degenerate range at the insertion point.</summary>
    public abstract IReadOnlyList<AccessibleTextRange> GetSelection();
    /// <summary>Gets the portions of the document currently visible in its viewport.</summary>
    public abstract IReadOnlyList<AccessibleTextRange> GetVisibleRanges();
    /// <summary>Finds the insertion point that ordinary pointer hit testing would choose.</summary>
    /// <param name="screenPoint">A point in the same screen coordinates as the owner's Bounds.</param>
    public abstract AccessibleTextRange RangeFromPoint(PointF screenPoint);
    /// <summary>Gets the text enclosing an existing embedded accessible child.</summary>
    /// <param name="child">An embedded child of this text stream.</param>
    /// <returns>The enclosing range; editors without embedded objects reject the argument.</returns>
    public virtual AccessibleTextRange RangeFromChild(AccessibleObject child)
        => throw new ArgumentException("The object is not embedded in this text stream.", nameof(child));
    /// <summary>Gets a degenerate range at the insertion point without changing focus.</summary>
    /// <param name="isActive">True when this editor owns keyboard focus in its currently active input host.</param>
    public abstract AccessibleTextRange GetCaretRange(out bool isActive);
}

/// <summary>Represents persistent UTF-16 endpoints in one existing accessible text stream.</summary>
/// <remarks>
/// Call on the owning UI thread. Navigation changes only this range, never editor selection.
/// TextBox ranges retain offsets rather than text snapshots and rebase through at most 1024
/// recorded edits. An older range throws InvalidOperationException; obtain a fresh range.
/// A nonempty start has left insertion affinity and its end has right affinity. A collapsed
/// range uses one right-affinity anchor. Disposal or a TextBox password-mode transition invalidates
/// old ranges. Sensitive ancestor protection also blocks every read while that protection applies.
/// </remarks>
public abstract class AccessibleTextRange
{
    /// <summary>Gets the current inclusive UTF-16 offset, after rebasing retained edits.</summary>
    public abstract int Start { get; }
    /// <summary>Gets the current exclusive UTF-16 offset, after rebasing retained edits.</summary>
    public abstract int End { get; }
    /// <summary>Gets the enclosing canonical text provider.</summary>
    public abstract AccessibleTextProvider Provider { get; }
    /// <summary>Creates an independently movable range at the same current endpoints.</summary>
    public abstract AccessibleTextRange Clone();
    /// <summary>Compares current endpoints of ranges from the same provider.</summary>
    /// <param name="other">A range from this provider.</param>
    public abstract bool Compare(AccessibleTextRange other);
    /// <summary>Compares two endpoints; only the sign of the result is significant.</summary>
    /// <param name="endpoint">This range's endpoint.</param>
    /// <param name="other">A range from this provider.</param>
    /// <param name="otherEndpoint">The other range's endpoint.</param>
    public abstract int CompareEndpoints(AccessibleTextEndpoint endpoint, AccessibleTextRange other, AccessibleTextEndpoint otherEndpoint);
    /// <summary>Normalizes this range to the enclosing unit at its start.</summary>
    /// <param name="unit">The unit to enclose.</param>
    public abstract void ExpandToEnclosingUnit(AccessibleTextUnit unit);
    /// <summary>Finds matching effective formatting within this range.</summary>
    /// <param name="attribute">The requested attribute.</param>
    /// <param name="value">The typed value to match.</param>
    /// <param name="backward">Search from the range's end.</param>
    /// <remarks>TextBox font-size searches allow four binary floating-point units of roundoff from native unit conversion; other attributes use exact typed equality.</remarks>
    public abstract AccessibleTextRange? FindAttribute(AccessibleTextAttribute attribute, object value, bool backward = false);
    /// <summary>Finds text within this range without changing the document or selection.</summary>
    /// <param name="text">Nonempty text to find.</param>
    /// <param name="backward">Search from the range's end.</param>
    /// <param name="ignoreCase">Use ordinal case-insensitive comparison.</param>
    public abstract AccessibleTextRange? FindText(string text, bool backward = false, bool ignoreCase = false);
    /// <summary>Gets a typed attribute value or a Mixed/NotSupported identity value.</summary>
    /// <param name="attribute">The requested attribute.</param>
    public abstract object GetAttributeValue(AccessibleTextAttribute attribute);
    /// <summary>Gets visible text fragments in the same screen coordinates as the owner's Bounds.</summary>
    public abstract IReadOnlyList<RectangleF> GetBoundingRectangles();
    /// <summary>Gets actual embedded accessible children enclosed by this range.</summary>
    public virtual IReadOnlyList<AccessibleObject> GetChildren() => Array.Empty<AccessibleObject>();
    /// <summary>Gets the existing accessible object enclosing this range.</summary>
    public virtual AccessibleObject GetEnclosingElement() => Provider.Owner;
    /// <summary>Gets the current plain document text between these endpoints.</summary>
    /// <param name="maximumLength">Maximum UTF-16 units, or -1 for the complete range.</param>
    /// <remarks>Placeholders and rendering markers are excluded. This does not use IME snapshot limits.</remarks>
    public abstract string GetText(int maximumLength = -1);
    /// <summary>Normalizes and moves the range by logical or rendered units.</summary>
    /// <param name="unit">The navigation unit.</param>
    /// <param name="count">Signed number of units.</param>
    /// <returns>The signed number of units actually traversed.</returns>
    public abstract int Move(AccessibleTextUnit unit, int count);
    /// <summary>Moves one endpoint, collapsing the other when the endpoints cross.</summary>
    /// <param name="endpoint">The endpoint to move.</param>
    /// <param name="unit">The navigation unit.</param>
    /// <param name="count">Signed number of units.</param>
    public abstract int MoveEndpointByUnit(AccessibleTextEndpoint endpoint, AccessibleTextUnit unit, int count);
    /// <summary>Moves one endpoint to an endpoint of another range from the same provider.</summary>
    /// <param name="endpoint">The endpoint to move.</param>
    /// <param name="other">A range from this provider.</param>
    /// <param name="otherEndpoint">The endpoint to copy.</param>
    public abstract void MoveEndpointByRange(AccessibleTextEndpoint endpoint, AccessibleTextRange other, AccessibleTextEndpoint otherEndpoint);
    /// <summary>Replaces the editor's selection using its normal selection and composition policy.</summary>
    /// <returns>Whether the available editor accepted selection.</returns>
    public abstract bool Select();
    /// <summary>Adds a touching or overlapping range to the single selection; rejects disjoint ranges.</summary>
    public abstract bool AddToSelection();
    /// <summary>Removes a selected edge; rejects removal that would create disjoint selections.</summary>
    public abstract bool RemoveFromSelection();
    /// <summary>Scrolls this text into view without moving selection or focus.</summary>
    /// <param name="alignToTop">Align the beginning to the top, otherwise the end to the bottom.</param>
    /// <returns>Whether the available editor accepted the request.</returns>
    public abstract bool ScrollIntoView(bool alignToTop = true);
}

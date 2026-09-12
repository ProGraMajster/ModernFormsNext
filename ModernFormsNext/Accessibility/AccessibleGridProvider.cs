namespace ModernFormsNext.Accessibility;

/// <summary>Describes the traversal order of cells in an accessible table.</summary>
public enum AccessibleTableTraversal
{
    /// <summary>Cells are presented by row, then column.</summary>
    RowMajor,
    /// <summary>Cells are presented by column, then row.</summary>
    ColumnMajor,
    /// <summary>No primary traversal order is defined.</summary>
    Indeterminate
}

/// <summary>Provides coordinates and header associations over an existing semantic hierarchy.</summary>
/// <remarks>
/// Read on the owning UI thread. This capability must return the same peers as normal tree
/// enumeration and must not create controls, edit cells, change selection or scroll while reading.
/// Counts describe exposed rows and columns; hidden columns are omitted. A sparse coordinate may
/// return null. Retained providers must become empty when their represented owner is removed.
/// </remarks>
public abstract class AccessibleGridProvider
{
    /// <summary>Gets the current number of semantic rows.</summary>
    public abstract int RowCount { get; }
    /// <summary>Gets the current number of semantic columns.</summary>
    public abstract int ColumnCount { get; }
    /// <summary>Gets a cell at zero-based coordinates, or null for an absent or invalid coordinate.</summary>
    /// <param name="row">The semantic row index.</param>
    /// <param name="column">The semantic column index.</param>
    /// <returns>The existing logical cell peer, or null.</returns>
    public abstract AccessibleObject? GetItem(int row, int column);
    /// <summary>Gets whether this grid also supplies table/header semantics.</summary>
    public virtual bool IsTable => false;
    /// <summary>Gets the primary traversal order when <see cref="IsTable"/> is true.</summary>
    public virtual AccessibleTableTraversal Traversal => AccessibleTableTraversal.RowMajor;
    /// <summary>Gets row headers without changing the viewport or realizing visual controls.</summary>
    /// <returns>The live logical header peers.</returns>
    public virtual IReadOnlyList<AccessibleObject> GetRowHeaders() => [];
    /// <summary>Gets column headers without changing the viewport or realizing visual controls.</summary>
    /// <returns>The live logical header peers.</returns>
    public virtual IReadOnlyList<AccessibleObject> GetColumnHeaders() => [];
}

/// <summary>Describes a cell's current coordinates and header associations in a semantic grid.</summary>
/// <remarks>
/// This immutable metadata is a point-in-time view. Re-read it after reordering or mutation.
/// Associated peers retain their normal lifetime rules. Coordinates count only semantic rows and
/// columns, so they can differ from model indices for hidden columns. Construct and read on the UI thread.
/// </remarks>
public sealed class AccessibleGridCellInfo
{
    /// <summary>Creates validated cell metadata with optional immutable header associations.</summary>
    /// <param name="grid">The containing grid's canonical peer.</param>
    /// <param name="row">The zero-based row.</param>
    /// <param name="column">The zero-based column.</param>
    /// <param name="rowSpan">The positive number of rows occupied.</param>
    /// <param name="columnSpan">The positive number of columns occupied.</param>
    /// <param name="rowHeaders">At most 64 associated row headers, or null for none.</param>
    /// <param name="columnHeaders">At most 64 associated column headers, or null for none.</param>
    /// <exception cref="ArgumentException">A header collection contains null or more than 64 associations.</exception>
    public AccessibleGridCellInfo(AccessibleObject grid, int row, int column, int rowSpan = 1,
        int columnSpan = 1, IEnumerable<AccessibleObject>? rowHeaders = null,
        IEnumerable<AccessibleObject>? columnHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnSpan, 1);
        if ((long)row + rowSpan > int.MaxValue || (long)column + columnSpan > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(rowSpan), "Cell endpoints must fit signed coordinates.");
        Grid = grid; Row = row; Column = column; RowSpan = rowSpan; ColumnSpan = columnSpan;
        RowHeaders = CopyHeaders(rowHeaders);
        ColumnHeaders = CopyHeaders(columnHeaders);
    }

    /// <summary>Gets the containing grid peer.</summary>
    public AccessibleObject Grid { get; }
    /// <summary>Gets the zero-based semantic row.</summary>
    public int Row { get; }
    /// <summary>Gets the zero-based semantic column.</summary>
    public int Column { get; }
    /// <summary>Gets the positive row span.</summary>
    public int RowSpan { get; }
    /// <summary>Gets the positive column span.</summary>
    public int ColumnSpan { get; }
    /// <summary>Gets the immutable associated row headers.</summary>
    public IReadOnlyList<AccessibleObject> RowHeaders { get; }
    /// <summary>Gets the immutable associated column headers.</summary>
    public IReadOnlyList<AccessibleObject> ColumnHeaders { get; }

    private static IReadOnlyList<AccessibleObject> CopyHeaders(IEnumerable<AccessibleObject>? headers)
    {
        if (headers is null) return Array.Empty<AccessibleObject>();
        List<AccessibleObject> copy = [];
        foreach (var header in headers)
        {
            if (header is null || copy.Count == 64)
                throw new ArgumentException("Headers must contain at most 64 non-null associations.", nameof(headers));
            copy.Add(header);
        }
        return copy.AsReadOnly();
    }
}

public partial class AccessibleObject
{
    /// <summary>Gets optional grid/table access over this object's existing semantic children.</summary>
    /// <remarks>UI-thread-affine. Reads do not select, scroll, edit or realize visual controls.</remarks>
    public virtual AccessibleGridProvider? GridProvider => null;

    /// <summary>Gets optional current cell coordinates and header associations.</summary>
    /// <remarks>UI-thread-affine. Re-read after structure changes; sensitive peers return null.</remarks>
    public virtual AccessibleGridCellInfo? GridCell => null;
}

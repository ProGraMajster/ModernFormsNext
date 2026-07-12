using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

/// <summary>
/// Specifies horizontal alignment for text in document table columns and cells.
/// </summary>
public enum DocumentTextAlignment
{
    /// <summary>
    /// Aligns content to the leading edge of the available width.
    /// </summary>
    Left,

    /// <summary>
    /// Centers content within the available width.
    /// </summary>
    Center,

    /// <summary>
    /// Aligns content to the trailing edge of the available width.
    /// </summary>
    Right
}

/// <summary>
/// Represents a table made of rows and cells.
/// </summary>
/// <remarks>
/// <see cref="TableBlock"/> is a native document node and is not tied to Markdown table syntax.
/// The renderer uses content-aware column widths within the available document width, wraps cell
/// text, preserves column alignment, and draws SkiaSharp borders and header backgrounds.
/// </remarks>
public sealed class TableBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="TableBlock"/> instance.
    /// </summary>
    /// <param name="columns">The table columns and their default alignment.</param>
    /// <param name="rows">The table rows.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="columns"/> or <paramref name="rows"/> is <see langword="null"/>.</exception>
    public TableBlock(IEnumerable<DocumentTableColumn> columns, IEnumerable<DocumentTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        Columns = columns.ToArray();
        Rows = rows.ToArray();
    }

    /// <summary>
    /// Gets the table columns in display order.
    /// </summary>
    public IReadOnlyList<DocumentTableColumn> Columns { get; }

    /// <summary>
    /// Gets the table rows in display order.
    /// </summary>
    public IReadOnlyList<DocumentTableRow> Rows { get; }
}

/// <summary>
/// Describes a table column.
/// </summary>
public sealed class DocumentTableColumn
{
    /// <summary>
    /// Initializes a new <see cref="DocumentTableColumn"/> instance.
    /// </summary>
    /// <param name="alignment">The default horizontal alignment for cells in the column.</param>
    public DocumentTableColumn(DocumentTextAlignment alignment = DocumentTextAlignment.Left)
    {
        Alignment = alignment;
    }

    /// <summary>
    /// Gets the default horizontal alignment for cells in the column.
    /// </summary>
    public DocumentTextAlignment Alignment { get; }
}

/// <summary>
/// Represents a row in a <see cref="TableBlock"/>.
/// </summary>
public sealed class DocumentTableRow
{
    /// <summary>
    /// Initializes a new <see cref="DocumentTableRow"/> instance.
    /// </summary>
    /// <param name="cells">The cells contained by the row.</param>
    /// <param name="isHeader">A value indicating whether the row is a header row.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is <see langword="null"/>.</exception>
    public DocumentTableRow(IEnumerable<DocumentTableCell> cells, bool isHeader = false)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Cells = cells.ToArray();
        IsHeader = isHeader;
    }

    /// <summary>
    /// Gets the cells contained by the row.
    /// </summary>
    public IReadOnlyList<DocumentTableCell> Cells { get; }

    /// <summary>
    /// Gets a value indicating whether the row is a header row.
    /// </summary>
    public bool IsHeader { get; }
}

/// <summary>
/// Represents a single table cell.
/// </summary>
public sealed class DocumentTableCell
{
    /// <summary>
    /// Initializes a new <see cref="DocumentTableCell"/> instance.
    /// </summary>
    /// <param name="blocks">The document blocks displayed inside the cell.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public DocumentTableCell(IEnumerable<DocumentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Blocks = blocks.ToArray();
    }

    /// <summary>
    /// Gets the document blocks displayed inside the cell.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }
}

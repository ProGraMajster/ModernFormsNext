using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

/// <summary>
/// Represents a block-level element in a <see cref="Document"/>.
/// </summary>
/// <remarks>
/// Blocks participate in vertical document layout. Inline formatting, such as emphasis or links,
/// is represented by <see cref="DocumentInline"/> descendants inside text-bearing blocks.
/// </remarks>
public abstract class DocumentBlock
{
    private protected DocumentBlock()
    {
    }
}

/// <summary>
/// Represents a paragraph containing inline document content.
/// </summary>
public sealed class ParagraphBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="ParagraphBlock"/> instance.
    /// </summary>
    /// <param name="inlines">The inline content displayed in the paragraph.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public ParagraphBlock(IEnumerable<DocumentInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        Inlines = inlines.ToArray();
    }

    /// <summary>
    /// Gets the inline content displayed in the paragraph.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }
}

/// <summary>
/// Represents a heading block.
/// </summary>
/// <remarks>
/// Heading levels follow the common document convention where level 1 is the largest top-level
/// heading and level 6 is the smallest heading. Assigning the block to a viewer affects layout
/// because heading levels use different font metrics and spacing.
/// </remarks>
public sealed class HeadingBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="HeadingBlock"/> instance.
    /// </summary>
    /// <param name="level">The heading level from 1 through 6.</param>
    /// <param name="inlines">The inline content displayed inside the heading.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is outside the range 1 through 6.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public HeadingBlock(int level, IEnumerable<DocumentInline> inlines)
    {
        if (level is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(level), "Heading level must be between 1 and 6.");

        ArgumentNullException.ThrowIfNull(inlines);
        Level = level;
        Inlines = inlines.ToArray();
    }

    /// <summary>
    /// Gets the heading level from 1 through 6.
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Gets the inline content displayed inside the heading.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }
}

/// <summary>
/// Represents a preformatted code block.
/// </summary>
/// <remarks>
/// Code block text is rendered using the document code style and preserves line breaks. The
/// optional <see cref="Language"/> value is metadata for future syntax highlighting and is not
/// interpreted by the current renderer.
/// </remarks>
public sealed class CodeBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="CodeBlock"/> instance.
    /// </summary>
    /// <param name="text">The preformatted code text.</param>
    /// <param name="language">An optional language identifier such as <c>csharp</c>.</param>
    public CodeBlock(string? text, string? language = null)
    {
        Text = text ?? string.Empty;
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
    }

    /// <summary>
    /// Gets the optional language identifier associated with the code block.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets the preformatted code text.
    /// </summary>
    public string Text { get; }
}

/// <summary>
/// Represents quoted block content.
/// </summary>
public sealed class QuoteBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="QuoteBlock"/> instance.
    /// </summary>
    /// <param name="blocks">The nested blocks displayed as quoted content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public QuoteBlock(IEnumerable<DocumentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Blocks = blocks.ToArray();
    }

    /// <summary>
    /// Gets the nested blocks displayed as quoted content.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }
}

/// <summary>
/// Represents an ordered or unordered list.
/// </summary>
/// <remarks>
/// List items may contain nested blocks, including nested <see cref="ListBlock"/> instances.
/// The renderer supports reasonable nested list indentation without requiring a table
/// or HTML layout engine.
/// </remarks>
public sealed class ListBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="ListBlock"/> instance.
    /// </summary>
    /// <param name="ordered">A value indicating whether this is an ordered list.</param>
    /// <param name="items">The list items.</param>
    /// <param name="startNumber">The starting number for ordered lists.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public ListBlock(bool ordered, IEnumerable<DocumentListItem> items, int startNumber = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        Ordered = ordered;
        Items = items.ToArray();
        StartNumber = Math.Max(1, startNumber);
    }

    /// <summary>
    /// Gets the list items.
    /// </summary>
    public IReadOnlyList<DocumentListItem> Items { get; }

    /// <summary>
    /// Gets a value indicating whether this is an ordered list.
    /// </summary>
    public bool Ordered { get; }

    /// <summary>
    /// Gets the starting number for ordered lists.
    /// </summary>
    public int StartNumber { get; }
}

/// <summary>
/// Represents a single list item.
/// </summary>
public sealed class DocumentListItem
{
    /// <summary>
    /// Initializes a new <see cref="DocumentListItem"/> instance.
    /// </summary>
    /// <param name="blocks">The blocks displayed inside the list item.</param>
    /// <param name="isChecked">The checked state for task-list items, or <see langword="null"/> for a normal item.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public DocumentListItem(IEnumerable<DocumentBlock> blocks, bool? isChecked = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Blocks = blocks.ToArray();
        IsChecked = isChecked;
    }

    /// <summary>
    /// Gets the blocks displayed inside the list item.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    /// <summary>
    /// Gets the checked state for task-list items, or <see langword="null"/> for a normal item.
    /// </summary>
    public bool? IsChecked { get; }
}

/// <summary>
/// Represents a horizontal rule separator.
/// </summary>
public sealed class HorizontalRuleBlock : DocumentBlock
{
}

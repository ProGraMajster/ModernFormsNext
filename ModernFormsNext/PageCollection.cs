using System.Collections;

namespace ModernFormsNext;

/// <summary>Provides the ordered, UI-thread-owned pages retained by a <see cref="TabbedPage"/>.</summary>
/// <remarks>
/// Entries are borrowed references. Add admits detached pages; removal transfers them without disposal.
/// A false mutation result means validation or a newer application callback superseded the request.
/// Enumeration is live and follows normal List invalidation rules; use ToArray for a borrowed snapshot.
/// The indexer is read-only: replace a page explicitly through removal and insertion.
/// </remarks>
public sealed class PageCollection : IReadOnlyList<Page>
{
    private readonly TabbedPage owner;
    internal readonly List<Page> Entries = [];
    internal PageCollection(TabbedPage owner) => this.owner = owner;

    /// <summary>Gets the number of retained pages.</summary>
    public int Count => Entries.Count;
    /// <summary>Gets the borrowed page at a logical collection index.</summary>
    /// <param name="index">The zero-based index.</param>
    public Page this[int index] => Entries[index];
    /// <summary>Attempts to append one detached page.</summary>
    /// <param name="page">The non-null page; duplicates, parenting cycles and disposed pages are rejected.</param>
    /// <returns>Whether this operation retained the page through its callbacks.</returns>
    public bool Add(Page page) => Insert(Count, page);
    /// <summary>Attempts to insert a detached page at a logical index.</summary>
    /// <param name="index">An index from zero through Count, inclusive.</param>
    /// <param name="page">The non-null detached page.</param>
    /// <returns>Whether this operation remained current through ownership and collection callbacks.</returns>
    public bool Insert(int index, Page page) => owner.InsertPage(index, page);
    /// <summary>Attempts to remove and transfer a page without disposing it.</summary>
    /// <param name="page">The retained page, or an unrelated page which produces false.</param>
    /// <returns>True when this request removed the retained entry; false on absence, veto or supersession.</returns>
    public bool Remove(Page page) => owner.RemovePage(page);
    /// <summary>Attempts to transfer all entries without disposal after admitting departure from the current page.</summary>
    /// <returns>Whether the collection was cleared; a newer callback mutation is preserved.</returns>
    /// <remarks>A callback may interrupt an already committed prefix of removals. Keep an earlier borrowed snapshot when recovering transferred references.</remarks>
    public bool Clear() => owner.ClearPages();
    /// <summary>Moves a retained entry without changing its identity, parent, lifetime or logical selection.</summary>
    /// <param name="oldIndex">The current logical index.</param>
    /// <param name="newIndex">The final logical index, within the existing collection.</param>
    public void Move(int oldIndex, int newIndex) => owner.MovePage(oldIndex, newIndex);
    /// <summary>Gets whether the exact page instance is retained.</summary>
    /// <param name="page">The page to find.</param>
    public bool Contains(Page page) => Entries.Contains(page);
    /// <summary>Gets the logical index of a page, or minus one when absent.</summary>
    /// <param name="page">The page to find.</param>
    public int IndexOf(Page page) => Entries.IndexOf(page);
    /// <summary>Enumerates borrowed retained pages in logical order on the UI thread.</summary>
    public IEnumerator<Page> GetEnumerator() => Entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

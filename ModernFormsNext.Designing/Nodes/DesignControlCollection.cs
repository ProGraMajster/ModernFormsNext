using System.Collections.ObjectModel;

namespace ModernFormsNext.Designing;

/// <summary>
/// Represents an ordered collection of designer control nodes.
/// </summary>
/// <remarks>
/// The order is significant. For ordinary containers it represents front-to-back Z-order, with
/// index zero as the front-most sibling. For flow, table, and tab containers it represents their
/// sequential content order; when such children overlap, the last item is visually front-most,
/// matching runtime. The same container policy drives deterministic serialization, generation,
/// docking, painting, hit testing, reverse synchronization, and document-outline moves.
/// Existing documents already persist children as an ordered JSON array, so no separate
/// child-order field or format migration is required.
/// </remarks>
public sealed class DesignControlCollection : Collection<DesignControlNode>
{
}

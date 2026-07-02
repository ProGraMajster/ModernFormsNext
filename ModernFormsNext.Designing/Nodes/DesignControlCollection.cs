using System.Collections.ObjectModel;

namespace ModernFormsNext.Designing;

/// <summary>
/// Represents an ordered collection of designer control nodes.
/// </summary>
/// <remarks>
/// The order is significant. It is used for deterministic serialization, generated
/// field order, and hit-testing from the topmost added node back to earlier nodes.
/// </remarks>
public sealed class DesignControlCollection : Collection<DesignControlNode>
{
}

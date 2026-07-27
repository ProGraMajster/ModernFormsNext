using System.Collections;
using System.ComponentModel;

namespace ModernFormsNext.Animations;

/// <summary>Stores directional visual-state transition settings for one control.</summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class VisualStateTransitionCollection : IEnumerable<VisualStateTransition>
{
    private readonly Dictionary<(VisualState From, VisualState To), VisualStateTransition> transitions = [];

    /// <summary>Gets the number of configured directional transitions.</summary>
    public int Count => transitions.Count;

    /// <summary>Adds or replaces a directional transition.</summary>
    public void Add(VisualState from, VisualState to, VisualStateTransition transition)
    {
        ValidateState(from, nameof(from));
        ValidateState(to, nameof(to));
        ArgumentNullException.ThrowIfNull(transition);
        transitions[(from, to)] = transition;
    }

    /// <summary>Removes a directional transition.</summary>
    public bool Remove(VisualState from, VisualState to)
        => transitions.Remove((from, to));

    /// <summary>Removes every configured transition.</summary>
    public void Clear() => transitions.Clear();

    /// <summary>Returns transitions in deterministic from/to order.</summary>
    public IEnumerator<VisualStateTransition> GetEnumerator()
        => transitions
            .OrderBy(static pair => pair.Key.From)
            .ThenBy(static pair => pair.Key.To)
            .Select(static pair => pair.Value)
            .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal bool TryGet(VisualState from, VisualState to, out VisualStateTransition? transition)
        => transitions.TryGetValue((from, to), out transition);

    private static void ValidateState(VisualState value, string parameterName)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "The visual state is not defined.");
    }
}

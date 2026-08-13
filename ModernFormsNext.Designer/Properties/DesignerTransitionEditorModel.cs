using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerLayoutTransitionEditorModel
{
    public DesignerLayoutTransitionEditorModel(DesignPropertyValue? stored)
    {
        IsExplicit = stored is not null;
        if (!LayoutTransitionDesignValue.TryRead(
            stored,
            out bool enabled,
            out double duration,
            out string easing,
            out string? error))
        {
            LoadError = error ?? "The stored layout transition is malformed.";
            enabled = true;
            duration = 250d;
            easing = "EaseOut";
        }

        Enabled = enabled;
        DurationMilliseconds = duration;
        Easing = easing;
    }

    public bool Enabled { get; private set; }

    public double DurationMilliseconds { get; private set; }

    public string Easing { get; private set; }

    public bool IsExplicit { get; private set; }

    public string? LoadError { get; private set; }

    public bool TrySet(bool enabled, double durationMilliseconds, string easing, out string? error)
    {
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds < 0d)
        {
            error = "Duration must be a finite non-negative number of milliseconds.";
            return false;
        }
        if (!KnownEasingDesignValue.IsKnown(easing))
        {
            error = $"Easing '{easing}' is not supported by the Designer.";
            return false;
        }

        Enabled = enabled;
        DurationMilliseconds = durationMilliseconds;
        Easing = easing;
        IsExplicit = true;
        LoadError = null;
        error = null;
        return true;
    }

    public void Reset()
    {
        Enabled = true;
        DurationMilliseconds = 250d;
        Easing = "EaseOut";
        IsExplicit = false;
        LoadError = null;
    }

    public bool TryCreateValue(out DesignPropertyValue? value, out string? error)
    {
        if (LoadError is not null)
        {
            value = null;
            error = LoadError;
            return false;
        }
        if (!IsExplicit)
        {
            value = null;
            error = null;
            return true;
        }

        try
        {
            value = LayoutTransitionDesignValue.Create(Enabled, DurationMilliseconds, Easing);
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            value = null;
            error = exception.Message;
            return false;
        }
    }
}

internal sealed class DesignerVisualStateTransitionEditorModel
{
    internal static IReadOnlyList<string> States { get; } =
        ["Normal", "Hover", "Pressed", "Disabled", "Focused"];

    private readonly List<DesignVisualStateTransition> entries = [];

    public DesignerVisualStateTransitionEditorModel(DesignPropertyValue? stored)
    {
        if (!VisualStateTransitionDesignValue.TryRead(stored, out var loaded, out string? error))
        {
            LoadError = error ?? "The stored visual-state transition collection is malformed.";
            return;
        }
        entries.AddRange(loaded);
    }

    public IReadOnlyList<DesignVisualStateTransition> Entries => entries;

    public string? LoadError { get; private set; }

    public bool TryAddDefault(out int index, out string? error)
    {
        foreach (string from in States)
        {
            foreach (string to in States)
            {
                if (from == to || entries.Any(item => item.From == from && item.To == to))
                    continue;

                entries.Add(new DesignVisualStateTransition(from, to, 150d, "CubicOut"));
                index = entries.Count - 1;
                error = null;
                return true;
            }
        }

        index = -1;
        error = "Every supported visual-state pair is already configured.";
        return false;
    }

    public bool TryAdd(
        string from,
        string to,
        double durationMilliseconds,
        string easing,
        out string? error)
    {
        if (!TryValidate(from, to, durationMilliseconds, easing, ignoredIndex: -1, out error))
            return false;
        entries.Add(new DesignVisualStateTransition(from, to, durationMilliseconds, easing));
        return true;
    }

    public bool TryUpdate(
        int index,
        string from,
        string to,
        double durationMilliseconds,
        string easing,
        out string? error)
    {
        if (index < 0 || index >= entries.Count)
        {
            error = "Select a transition to edit.";
            return false;
        }
        if (!TryValidate(from, to, durationMilliseconds, easing, index, out error))
            return false;
        entries[index] = new DesignVisualStateTransition(from, to, durationMilliseconds, easing);
        return true;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= entries.Count)
            return false;
        entries.RemoveAt(index);
        return true;
    }

    public void Reset()
    {
        entries.Clear();
        LoadError = null;
    }

    public bool TryCreateValue(out DesignPropertyValue value, out string? error)
    {
        if (LoadError is not null)
        {
            value = null!;
            error = LoadError;
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            DesignVisualStateTransition entry = entries[index];
            if (!TryValidate(
                entry.From,
                entry.To,
                entry.DurationMilliseconds,
                entry.Easing,
                index,
                out error))
            {
                value = null!;
                return false;
            }
        }

        try
        {
            value = VisualStateTransitionDesignValue.Create(entries);
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            value = null!;
            error = exception.Message;
            return false;
        }
    }

    private bool TryValidate(
        string from,
        string to,
        double durationMilliseconds,
        string easing,
        int ignoredIndex,
        out string? error)
    {
        if (!States.Contains(from, StringComparer.Ordinal) || !States.Contains(to, StringComparer.Ordinal))
        {
            error = "From and To must be supported visual states.";
            return false;
        }
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            error = "From and To must identify different visual states.";
            return false;
        }
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds < 0d)
        {
            error = "Duration must be a finite non-negative number of milliseconds.";
            return false;
        }
        if (!KnownEasingDesignValue.IsKnown(easing))
        {
            error = $"Easing '{easing}' is not supported by the Designer.";
            return false;
        }
        if (entries.Where((_, index) => index != ignoredIndex)
            .Any(item => item.From == from && item.To == to))
        {
            error = $"The {from} -> {to} transition is already configured.";
            return false;
        }

        error = null;
        return true;
    }
}

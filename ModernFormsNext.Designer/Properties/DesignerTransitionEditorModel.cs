using System.Globalization;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal static class DesignerTransitionEditorModel
{
    private static readonly string[] States = ["Normal", "Hover", "Pressed", "Disabled", "Focused"];

    public static string FormatLayout(DesignPropertyValue? value)
    {
        if (!LayoutTransitionDesignValue.TryRead(value, out bool enabled, out double duration, out string easing, out _))
            return string.Empty;
        return $"Enabled={enabled.ToString().ToLowerInvariant()}{Environment.NewLine}DurationMilliseconds={duration.ToString("R", CultureInfo.InvariantCulture)}{Environment.NewLine}Easing={easing}";
    }

    public static bool TryParseLayout(string text, out DesignPropertyValue value, out string? error)
    {
        bool enabled = true;
        double duration = 250d;
        string easing = "EaseOut";
        if (!TryReadAssignments(text, out var assignments, out error))
        {
            value = null!;
            return false;
        }

        foreach ((string name, string input) in assignments)
        {
            switch (name)
            {
                case "Enabled" when bool.TryParse(input, out bool parsed):
                    enabled = parsed;
                    break;
                case "DurationMilliseconds" when double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    && double.IsFinite(parsed) && parsed >= 0d:
                    duration = parsed;
                    break;
                case "Easing" when KnownEasingDesignValue.IsKnown(input):
                    easing = input;
                    break;
                default:
                    value = null!;
                    error = $"Layout transition value '{name}={input}' is not supported.";
                    return false;
            }
        }
        value = LayoutTransitionDesignValue.Create(enabled, duration, easing);
        error = null;
        return true;
    }

    public static string FormatVisualStates(DesignPropertyValue? value)
    {
        if (!VisualStateTransitionDesignValue.TryRead(value, out var transitions, out _))
            return string.Empty;
        return string.Join(Environment.NewLine, transitions.Select(item =>
            $"{item.From}->{item.To}; DurationMilliseconds={item.DurationMilliseconds.ToString("R", CultureInfo.InvariantCulture)}; Easing={item.Easing}"));
    }

    public static bool TryParseVisualStates(string text, out DesignPropertyValue value, out string? error)
    {
        var transitions = new List<DesignVisualStateTransition>();
        foreach (string rawLine in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            string[] segments = line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string[] endpoints = segments[0].Split("->", StringSplitOptions.TrimEntries);
            if (endpoints.Length != 2
                || !States.Contains(endpoints[0], StringComparer.Ordinal)
                || !States.Contains(endpoints[1], StringComparer.Ordinal))
            {
                value = null!;
                error = $"'{segments[0]}' must be a known From->To visual-state pair.";
                return false;
            }

            double duration = 150d;
            string easing = "CubicOut";
            if (!TryReadAssignments(string.Join(Environment.NewLine, segments.Skip(1)), out var assignments, out error, separator: '='))
            {
                value = null!;
                return false;
            }
            foreach ((string name, string input) in assignments)
            {
                if (name == "DurationMilliseconds"
                    && double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    && double.IsFinite(parsed) && parsed >= 0d)
                {
                    duration = parsed;
                }
                else if (name == "Easing" && KnownEasingDesignValue.IsKnown(input))
                {
                    easing = input;
                }
                else
                {
                    value = null!;
                    error = $"Visual-state transition value '{name}={input}' is not supported.";
                    return false;
                }
            }
            transitions.Add(new DesignVisualStateTransition(endpoints[0], endpoints[1], duration, easing));
        }

        try
        {
            value = VisualStateTransitionDesignValue.Create(transitions);
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

    private static bool TryReadAssignments(
        string text,
        out Dictionary<string, string> assignments,
        out string? error,
        char separator = '=')
    {
        assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string rawLine in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            int index = line.IndexOf(separator);
            if (index <= 0 || !assignments.TryAdd(line[..index].Trim(), line[(index + 1)..].Trim()))
            {
                error = $"'{line}' must be a unique Property=Value entry.";
                return false;
            }
        }
        error = null;
        return true;
    }
}

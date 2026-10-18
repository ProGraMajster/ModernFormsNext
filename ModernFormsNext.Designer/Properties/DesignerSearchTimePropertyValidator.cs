using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

// Exact built-in source-free controls validate the complete authored scalar/value state.
// No project constructor, source, provider, factory, command or TypeConverter is executed.
internal static class DesignerSearchTimePropertyValidator
{
    internal static bool TryValidate(DesignControlNode node, Type type, out string? error)
    {
        error = null;
        try
        {
            if (type == typeof(TimePicker))
            {
                using var time = new TimePicker();
                time.SuspendLayout();
                time.MaximumTime = Read(node, nameof(TimePicker.MaximumTime), time.MaximumTime);
                time.MinimumTime = Read(node, nameof(TimePicker.MinimumTime), time.MinimumTime);
                time.CultureName = Read<string?>(node, nameof(TimePicker.CultureName), null);
                time.Format = Read<string?>(node, nameof(TimePicker.Format), null);
                time.StepSeconds = Read(node, nameof(TimePicker.StepSeconds), time.StepSeconds);
                time.Time = Read<TimeOnly?>(node, nameof(TimePicker.Time), null);
            }
            else if (type == typeof(SearchBar))
            {
                using var search = new SearchBar { SuppressSuggestionRequests = true };
                search.SuspendLayout();
                search.HistoryCapacity = Read(node, nameof(SearchBar.HistoryCapacity), search.HistoryCapacity);
                search.SuggestionRowHeight = Read(node, nameof(SearchBar.SuggestionRowHeight), search.SuggestionRowHeight);
                search.MaximumVisibleSuggestions = Read(node, nameof(SearchBar.MaximumVisibleSuggestions), search.MaximumVisibleSuggestions);
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        { error = exception.Message; return false; }
    }

    private static T Read<T>(DesignControlNode node, string property, T fallback)
        => node.Properties.TryGetValue(property, out var stored)
            ? (T)DesignerPropertyValueEditor.FromDesignPropertyValue(stored, typeof(T))!
            : fallback;
}

using System.Globalization;

namespace ModernFormsNext.AnimationRecipes;

// Compiled into the runtime and neutral Designing assembly. This is a closed data grammar:
// tokens select framework primitives and never name types, members, factories or callbacks.
internal static class AnimationRecipeSyntax
{
    internal const int MaximumLength = 2048;
    internal const int MaximumSteps = 8;
    internal const int MaximumDurationMilliseconds = 2000;

    internal static bool TryRead(string? text, out RecipeStep[] steps, out string? error)
    {
        steps = [];
        if (text is null || text.Length > MaximumLength || string.IsNullOrWhiteSpace(text))
        {
            error = $"Enter a recipe of at most {MaximumLength} characters.";
            return false;
        }

        string[] lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || lines.Length > MaximumSteps + 1 || lines[0] != "Click")
        {
            error = $"Start with Click followed by 1 to {MaximumSteps} animation steps.";
            return false;
        }

        var result = new RecipeStep[lines.Length - 1];
        int totalDuration = 0;
        for (int i = 0; i < result.Length; i++)
        {
            if (!TryReadStep(lines[i + 1], out RecipeStep step))
            {
                error = $"Step {i + 1} needs a supported property, a value in range, a duration in ms, and a registered easing.";
                return false;
            }
            totalDuration += step.DurationMilliseconds;
            if (totalDuration > MaximumDurationMilliseconds)
            {
                error = $"The sequence may last at most {MaximumDurationMilliseconds} ms.";
                return false;
            }
            result[i] = step;
        }

        steps = result;
        error = null;
        return true;
    }

    private static bool TryReadStep(string line, out RecipeStep step)
    {
        step = default;
        string[] fields = line.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is < 4 or > 5 ||
            !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            !float.IsFinite(value) ||
            !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int duration) ||
            duration is < 1 or > MaximumDurationMilliseconds)
        {
            return false;
        }

        RecipeProperty? property = fields[0] switch
        {
            "Opacity" when value is >= 0f and <= 1f => RecipeProperty.Opacity,
            "Scale" when value is >= 0.5f and <= 2f => RecipeProperty.Scale,
            "Rotation" when value is >= -180f and <= 180f => RecipeProperty.Rotation,
            _ => null
        };
        RecipeEasing? easing = fields[3] switch
        {
            "Linear" => RecipeEasing.Linear,
            "EaseIn" => RecipeEasing.EaseIn,
            "EaseOut" => RecipeEasing.EaseOut,
            "EaseInOut" => RecipeEasing.EaseInOut,
            "BlendEaseInOut" => RecipeEasing.BlendEaseInOut,
            _ => null
        };
        if (property is null || easing is null)
            return false;

        float strength = 0f;
        if (easing == RecipeEasing.BlendEaseInOut)
        {
            if (fields.Length != 5 ||
                !float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out strength) ||
                !float.IsFinite(strength) || strength is < 0f or > 1f)
            {
                return false;
            }
        }
        else if (fields.Length != 4)
        {
            return false;
        }

        step = new RecipeStep(property.Value, value, duration, easing.Value, strength);
        return true;
    }
}

internal enum RecipeProperty { Opacity, Scale, Rotation }
internal enum RecipeEasing { Linear, EaseIn, EaseOut, EaseInOut, BlendEaseInOut }
internal readonly record struct RecipeStep(RecipeProperty Property, float Value,
    int DurationMilliseconds, RecipeEasing Easing, float Strength);

using ModernFormsNext.AnimationRecipes;

namespace ModernFormsNext.Designing;

internal static class AnimationRecipeDesignValue
{
    // A reserved metadata key cannot collide with a project control's CLR property name.
    internal const string PropertyName = "$animationRecipe";
    internal const string EffectTypeName = "global::ModernFormsNext.Animations.AnimationRecipeEffect";
    internal const int MaximumLength = AnimationRecipeSyntax.MaximumLength;

    internal static bool TryRead(DesignPropertyValue? value, out string recipe, out string? error)
    {
        recipe = value is { Kind: DesignPropertyValueKind.String, Value: string text } ? text : string.Empty;
        return TryValidate(recipe, out error);
    }

    internal static bool TryValidate(string? recipe, out string? error)
        => AnimationRecipeSyntax.TryRead(recipe, out _, out error);
}

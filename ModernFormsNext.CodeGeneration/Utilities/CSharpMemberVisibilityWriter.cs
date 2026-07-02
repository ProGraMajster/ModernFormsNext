using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Utilities;

/// <summary>
/// Converts designer member visibility values to C# field modifiers.
/// </summary>
public static class CSharpMemberVisibilityWriter
{
    /// <summary>
    /// Writes a C# access modifier for a generated field.
    /// </summary>
    /// <param name="visibility">The designer member visibility.</param>
    /// <returns>The C# access modifier, or an empty string for <see cref="DesignerMemberVisibility.None"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="visibility"/> is not a defined value.</exception>
    public static string WriteFieldModifier(DesignerMemberVisibility visibility)
        => visibility switch
        {
            DesignerMemberVisibility.None => string.Empty,
            DesignerMemberVisibility.Private => "private",
            DesignerMemberVisibility.Protected => "protected",
            DesignerMemberVisibility.Internal => "internal",
            DesignerMemberVisibility.Public => "public",
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unsupported designer member visibility.")
        };
}

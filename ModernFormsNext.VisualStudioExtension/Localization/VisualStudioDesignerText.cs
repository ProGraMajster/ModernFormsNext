using System.Globalization;

namespace ModernFormsNext.VisualStudioExtension.Localization;

internal static class VisualStudioDesignerText
{
    public static string ViewDesignerCommand
        => IsPolish
            ? "Otworz projektant ModernFormsNext"
            : "View ModernFormsNext Designer";

    private static bool IsPolish
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "pl", StringComparison.OrdinalIgnoreCase);
}

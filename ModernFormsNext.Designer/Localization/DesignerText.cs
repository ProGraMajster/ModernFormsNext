using System.Globalization;

namespace ModernFormsNext.Designer.Localization;

/// <summary>
/// Provides localized text used by the reusable ModernFormsNext designer shell.
/// </summary>
/// <remarks>
/// The designer currently ships with English and Polish strings. Hosts can select a language
/// through <see cref="ModernFormsDesignerOptions.Language"/> or leave it on
/// <see cref="DesignerLanguage.Auto"/> to follow the current UI culture.
/// </remarks>
public static class DesignerText
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Toolbox"] = "Toolbox",
        ["DocumentOutline"] = "Document Outline",
        ["SolutionExplorer"] = "Solution Explorer",
        ["Properties"] = "Properties",
        ["Output"] = "Output",
        ["New"] = "New",
        ["Open"] = "Open",
        ["AddPanel"] = "Add Panel",
        ["AddButton"] = "Add Button",
        ["AddLabel"] = "Add Label",
        ["AddTextBox"] = "Add TextBox",
        ["SaveDesign"] = "Save .mfdesign",
        ["GenerateDesignerCode"] = "Generate .Designer.cs",
        ["Settings"] = "Settings",
        ["SearchToolbox"] = "Search Toolbox",
        ["Delete"] = "Delete",
        ["DesignerSettings"] = "Designer Settings",
        ["Rendering"] = "Rendering",
        ["Language"] = "Language",
        ["ShowToolbar"] = "Show toolbar",
        ["AutoSave"] = "Auto-save .mfdesign",
        ["AutoGenerate"] = "Generate .Designer.cs on save",
        ["ToolWindows"] = "Tool windows",
        ["Panel"] = "Panel",
        ["Side"] = "Side",
        ["Mode"] = "Mode",
        ["Size"] = "Size",
        ["Ok"] = "OK",
        ["Cancel"] = "Cancel",
        ["Design"] = "Design",
        ["Saved"] = "Saved",
        ["Modified"] = "Modified",
        ["Render"] = "Render",
        ["Selection"] = "Selection",
        ["Position"] = "Position",
        ["Pointer"] = "Pointer",
        ["NoProjectPath"] = "No project path is available."
    };

    private static readonly IReadOnlyDictionary<string, string> Polish = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Toolbox"] = "Przybornik",
        ["DocumentOutline"] = "Konspekt dokumentu",
        ["SolutionExplorer"] = "Eksplorator rozwiazan",
        ["Properties"] = "Wlasciwosci",
        ["Output"] = "Dane wyjsciowe",
        ["New"] = "Nowy",
        ["Open"] = "Otworz",
        ["AddPanel"] = "Dodaj panel",
        ["AddButton"] = "Dodaj przycisk",
        ["AddLabel"] = "Dodaj etykiete",
        ["AddTextBox"] = "Dodaj TextBox",
        ["SaveDesign"] = "Zapisz .mfdesign",
        ["GenerateDesignerCode"] = "Generuj .Designer.cs",
        ["Settings"] = "Ustawienia",
        ["SearchToolbox"] = "Szukaj w przyborniku",
        ["Delete"] = "Usun",
        ["DesignerSettings"] = "Ustawienia projektanta",
        ["Rendering"] = "Renderowanie",
        ["Language"] = "Jezyk",
        ["ShowToolbar"] = "Pokaz pasek narzedzi",
        ["AutoSave"] = "Auto-zapis .mfdesign",
        ["AutoGenerate"] = "Generuj .Designer.cs przy zapisie",
        ["ToolWindows"] = "Panele narzedzi",
        ["Panel"] = "Panel",
        ["Side"] = "Strona",
        ["Mode"] = "Tryb",
        ["Size"] = "Rozmiar",
        ["Ok"] = "OK",
        ["Cancel"] = "Anuluj",
        ["Design"] = "Projekt",
        ["Saved"] = "Zapisano",
        ["Modified"] = "Zmodyfikowano",
        ["Render"] = "Render",
        ["Selection"] = "Zaznaczenie",
        ["Position"] = "Pozycja",
        ["Pointer"] = "Wskaznik",
        ["NoProjectPath"] = "Sciezka projektu jest niedostepna."
    };

    /// <summary>
    /// Gets the localized text for the specified key.
    /// </summary>
    /// <param name="key">The stable text key.</param>
    /// <param name="language">The requested designer language.</param>
    /// <returns>The localized text, or <paramref name="key"/> when no text has been defined.</returns>
    public static string Get(string key, DesignerLanguage language)
    {
        var texts = ResolveLanguage(language) == DesignerLanguage.Polish ? Polish : English;
        return texts.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Resolves <see cref="DesignerLanguage.Auto"/> to a concrete designer language.
    /// </summary>
    /// <param name="language">The configured language.</param>
    /// <returns>The concrete language used for lookup.</returns>
    public static DesignerLanguage ResolveLanguage(DesignerLanguage language)
    {
        if (language != DesignerLanguage.Auto)
            return language;

        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "pl", StringComparison.OrdinalIgnoreCase)
            ? DesignerLanguage.Polish
            : DesignerLanguage.English;
    }
}

using System.Globalization;

namespace ModernFormsNext.Designing;

/// <summary>
/// Validates designer documents without throwing for ordinary document errors.
/// </summary>
/// <remarks>
/// This validator focuses on model integrity needed by the MVP host and C# generator:
/// non-empty names, C# identifier validity, duplicate control names, non-negative
/// sizes, and non-empty control type names.
/// </remarks>
public sealed class DesignDocumentValidator
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while"
    };

    /// <summary>
    /// Validates a designer document.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>A validation result containing errors and warnings.</returns>
    public DesignDocumentValidationResult Validate(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var result = new DesignDocumentValidationResult();
        var names = new HashSet<string>(StringComparer.Ordinal);

        ValidateDocument(document, result);
        ValidateControls(document.Controls, result, names, parentPath: "form");

        return result;
    }

    /// <summary>
    /// Determines whether a value is a valid C# identifier for generated code.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <returns><see langword="true"/> when the identifier is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidCSharpIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        var value = identifier;

        if (value[0] == '@')
        {
            if (value.Length == 1)
                return false;

            value = value[1..];
        }
        else if (Keywords.Contains(value))
        {
            return false;
        }

        if (!IsIdentifierStart(value[0]))
            return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]))
                return false;
        }

        return true;
    }

    private static void ValidateDocument(DesignDocument document, DesignDocumentValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.ClassName))
        {
            result.AddError("The form class name cannot be empty.");
        }
        else if (!IsValidCSharpIdentifier(document.ClassName))
        {
            result.AddError($"The form class name '{document.ClassName}' is not a valid C# identifier.");
        }

        if (!string.IsNullOrWhiteSpace(document.Namespace) && !IsValidNamespace(document.Namespace))
            result.AddError($"The namespace '{document.Namespace}' is not a valid C# namespace.");

        if (document.Size.Width < 0 || document.Size.Height < 0)
            result.AddError("The form size cannot contain negative width or height.");

        if (string.IsNullOrWhiteSpace(document.FormName))
            result.AddWarning("The form name is empty; generated code will fall back to the class name for display text.");
    }

    private static void ValidateControls(
        IEnumerable<DesignControlNode> controls,
        DesignDocumentValidationResult result,
        HashSet<string> names,
        string parentPath)
    {
        var index = 0;

        foreach (var control in controls)
        {
            var path = $"{parentPath}.controls[{index}]";

            if (string.IsNullOrWhiteSpace(control.TypeName))
                result.AddError($"Control at '{path}' has an empty type name.");

            if (string.IsNullOrWhiteSpace(control.Name))
            {
                result.AddError($"Control at '{path}' has an empty name.");
            }
            else
            {
                if (!IsValidCSharpIdentifier(control.Name))
                    result.AddError($"Control name '{control.Name}' at '{path}' is not a valid C# identifier.");

                if (!names.Add(control.Name))
                    result.AddError($"Control name '{control.Name}' is duplicated.");
            }

            if (control.Bounds.Width < 0 || control.Bounds.Height < 0)
                result.AddError($"Control '{control.Name}' has negative width or height.");

            ValidateControls(control.Children, result, names, $"{path}.{control.Name}");
            index++;
        }
    }

    private static bool IsValidNamespace(string namespaceName)
        => namespaceName
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Length == namespaceName.Split('.').Length
        && namespaceName.Split('.').All(IsValidCSharpIdentifier);

    private static bool IsIdentifierStart(char value)
        => value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value)
    {
        if (value == '_' || char.IsLetterOrDigit(value))
            return true;

        var category = CharUnicodeInfo.GetUnicodeCategory(value);

        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.Format;
    }
}

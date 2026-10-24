using System.Globalization;

namespace ModernFormsNext.Designing;

/// <summary>
/// Validates designer documents without throwing for ordinary document errors.
/// </summary>
/// <remarks>
/// This validator focuses on model integrity needed by the designer host and C# generator:
/// non-empty names, C# identifier validity, duplicate control names, non-negative
/// sizes, and non-empty control type names.
/// </remarks>
public sealed partial class DesignDocumentValidator
{
    /// <summary>Gets the maximum number of authored nodes in one bounded designer graph.</summary>
    /// <remarks>This matches the established Designer clipboard node budget; item data is not a control node.</remarks>
    public const int MaximumNodeCount = 10_000;

    /// <summary>Gets the maximum authored control nesting depth, with a root control at depth zero.</summary>
    /// <remarks>This matches the established Designer clipboard depth budget.</remarks>
    public const int MaximumTreeDepth = 96;

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

        var result = ValidateStructure(document);
        if (!result.IsValid) return result;
        var names = new HashSet<string>(StringComparer.Ordinal);

        ValidateDocument(document, result);
        ValidateControls(document, document.Controls, result, names, parentPath: "root");

        return result;
    }

    /// <summary>Checks bounded graph ownership before recursive normalization, generation or hosting.</summary>
    /// <param name="document">The borrowed document, never modified by validation.</param>
    /// <returns>Structural errors for cycles, shared node identities, malformed collections, limits or known page slots.</returns>
    /// <remarks>
    /// This iterative preflight invokes no runtime control, application constructor, factory or converter.
    /// Primitive value shapes, finite numbers, collection cardinality and data budgets are checked
    /// without invoking runtime setters. Other editable member semantics are checked by Validate or hosts.
    /// Hosts can reject malformed graphs before changing the active document or normalizing its children.
    /// </remarks>
    public DesignDocumentValidationResult ValidateStructure(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new DesignDocumentValidationResult();
        if (document.Controls is null || document.Properties is null || document.Events is null)
        {
            result.AddError("Designer root collections must not be null.");
            return result;
        }
        if (document.Size.Width < 0 || document.Size.Height < 0)
        { result.AddError("The design root size cannot contain negative dimensions."); return result; }
        var values = new ValueValidationContext(result, MaximumPropertyValueCount, MaximumTextLength);
        values.AddText(document.Namespace, MaximumTypeNameLength);
        values.AddText(document.ClassName, MaximumTypeNameLength);
        values.AddText(document.FormName, MaximumTypeNameLength);
        values.AddText(document.Metadata?.ToolName, MaximumTypeNameLength);
        values.ValidateProperties(document.Properties);
        values.ValidateEvents(document.Events);
        if (!result.IsValid) return result;
        if (document.Controls.Count > MaximumNodeCount)
        {
            result.AddError($"Designer graphs support at most {MaximumNodeCount} control nodes.");
            return result;
        }
        var seen = new HashSet<DesignControlNode>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(DesignControlNode? Node, int Depth)>();
        for (int index = document.Controls.Count - 1; index >= 0; index--) pending.Push((document.Controls[index], 0));
        int count = 0;
        while (pending.TryPop(out var item))
        {
            if (++count > MaximumNodeCount)
            { result.AddError($"Designer graphs support at most {MaximumNodeCount} control nodes."); break; }
            if (item.Node is not { } node)
            { result.AddError("An authored control node must not be null."); continue; }
            if (!seen.Add(node))
            { result.AddError($"Control '{node.Name}' is cyclic or shared by more than one authored owner."); continue; }
            if (item.Depth > MaximumTreeDepth)
            { result.AddError($"Designer control depth must not exceed {MaximumTreeDepth}."); continue; }
            if (node.Children is null || node.Properties is null || node.Events is null)
            { result.AddError($"Control '{node.Name}' collections must not be null."); continue; }
            if (node.Bounds.Width < 0 || node.Bounds.Height < 0
                || (long)node.Bounds.X + node.Bounds.Width > int.MaxValue
                || (long)node.Bounds.Y + node.Bounds.Height > int.MaxValue)
            { result.AddError("Authored bounds require non-negative dimensions and representable right/bottom edges."); continue; }
            values.AddText(node.Name, MaximumTypeNameLength);
            values.AddText(node.TypeName, MaximumTypeNameLength);
            values.ValidateProperties(node.Properties);
            values.ValidateEvents(node.Events);
            if (!result.IsValid) break;
            if (node.Children.Any(child => child is null))
            { result.AddError($"Control '{node.Name}' contains a null authored child."); continue; }
            if (DesignPageContainers.GetChildError(node) is { } pageError)
                result.AddError($"Control '{node.Name}': {pageError}");
            if (pending.Count + node.Children.Count > MaximumNodeCount - count)
            { result.AddError($"Designer graphs support at most {MaximumNodeCount} control nodes."); break; }
            for (int index = node.Children.Count - 1; index >= 0; index--) pending.Push((node.Children[index], item.Depth + 1));
        }
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
        if (!Enum.IsDefined(document.RootKind))
            result.AddError($"The design root kind '{document.RootKind}' is not supported.");

        if (string.IsNullOrWhiteSpace(document.ClassName))
        {
            result.AddError("The design root class name cannot be empty.");
        }
        else if (!IsValidCSharpIdentifier(document.ClassName))
        {
            result.AddError($"The design root class name '{document.ClassName}' is not a valid C# identifier.");
        }

        if (!string.IsNullOrWhiteSpace(document.Namespace) && !IsValidNamespace(document.Namespace))
            result.AddError($"The namespace '{document.Namespace}' is not a valid C# namespace.");

        if (document.Size.Width < 0 || document.Size.Height < 0)
            result.AddError("The design root size cannot contain negative width or height.");

        if (string.IsNullOrWhiteSpace(document.FormName))
            result.AddWarning("The design root name is empty; generated code will fall back to the class name.");
    }

    private static void ValidateControls(
        DesignDocument document,
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
            else if (IsRootTypeReference(document, control.TypeName))
                result.AddError($"Control '{control.Name}' cannot contain the design root type '{control.TypeName}'.");

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

            ValidateControls(document, control.Children, result, names, $"{path}.{control.Name}");
            index++;
        }
    }

    private static bool IsRootTypeReference(DesignDocument document, string typeName)
    {
        var normalized = typeName.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
        var assemblySeparator = normalized.IndexOf(',');

        if (assemblySeparator >= 0)
            normalized = normalized[..assemblySeparator].Trim();

        if (string.Equals(normalized, document.ClassName, StringComparison.Ordinal))
            return true;

        return !string.IsNullOrWhiteSpace(document.Namespace)
            && string.Equals(normalized, $"{document.Namespace}.{document.ClassName}", StringComparison.Ordinal);
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

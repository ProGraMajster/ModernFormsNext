using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Discovers explicitly opted-in project animation definitions from C# source without loading assemblies.
/// </summary>
internal static class DesignerProjectAnimationDefinitionDiscovery
{
    private const string AnimationNamespace = "ModernFormsNext.Animations";
    private const string DefinitionAttributeName = "DesignableAnimationDefinition";
    private const string PropertyAttributeName = "DesignableAnimationProperty";

    /// <summary>Reads explicitly attributed animation descriptors from project C# source.</summary>
    /// <param name="projectPath">A project file or project directory path.</param>
    /// <returns>Detached descriptors in deterministic display order.</returns>
    public static IReadOnlyList<DesignAnimationDefinitionDescriptor> Discover(string? projectPath)
    {
        string? directory = DesignerProjectUserControlDiscovery.GetProjectDirectory(projectPath);
        if (directory is null)
            return [];

        try
        {
            ProjectAnimationDeclaration[] declarations = DesignerProjectUserControlDiscovery
                .EnumerateProjectFiles(directory, "*.cs")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadDeclarations)
                .GroupBy(item => item.FullName, StringComparer.Ordinal)
                .Select(MergePartialDeclarations)
                .ToArray();
            IReadOnlyDictionary<string, DesignAnimationDefinitionKind> kinds = ResolveKinds(declarations);

            return declarations
                .Where(item => item.DisplayName is not null
                    && item.IsPublic
                    && !item.IsAbstract
                    && !item.IsGeneric
                    && (!item.HasAnyConstructor || item.HasPublicParameterlessConstructor)
                    && kinds.ContainsKey(item.FullName))
                .Select(item => new DesignAnimationDefinitionDescriptor(
                    item.FullName,
                    item.DisplayName!,
                    kinds[item.FullName],
                    ReadInheritedProperties(item, declarations)))
                .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                .ThenBy(item => item.TypeName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<ProjectAnimationDeclaration> ReadDeclarations(string path)
    {
        CompilationUnitSyntax root;
        try
        {
            root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (ClassDeclarationSyntax type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            // Nested runtime types require '+' metadata names and are intentionally outside the
            // current document/generator contract. Top-level abstract and generic declarations stay
            // in the graph so concrete derived definitions can inherit their metadata safely.
            if (type.Ancestors().OfType<TypeDeclarationSyntax>().Any())
                continue;

            string name = type.Identifier.ValueText;
            string namespaceName = ReadNamespace(type);
            string fullName = string.IsNullOrWhiteSpace(namespaceName) ? name : $"{namespaceName}.{name}";
            UsingDirectiveSyntax[] usings = ReadUsings(type).ToArray();
            bool hasAnimationUsing = HasNamespaceUsing(usings, AnimationNamespace);
            AttributeSyntax? marker = FindAttribute(
                type.AttributeLists,
                DefinitionAttributeName,
                usings,
                hasAnimationUsing);
            ConstructorDeclarationSyntax[] constructors = type.Members
                .OfType<ConstructorDeclarationSyntax>()
                .ToArray();

            yield return new ProjectAnimationDeclaration(
                name,
                namespaceName,
                fullName,
                ResolveAliases(type.BaseList?.Types.FirstOrDefault()?.Type.ToString(), usings),
                marker is null ? null : ReadStringArgument(marker, 0) ?? name,
                ReadProperties(type, usings, hasAnimationUsing).ToArray(),
                type.Modifiers.Any(SyntaxKind.PublicKeyword),
                type.Modifiers.Any(SyntaxKind.AbstractKeyword),
                type.TypeParameterList is not null,
                constructors.Length > 0,
                constructors.Any(constructor => constructor.ParameterList.Parameters.Count == 0
                    && constructor.Modifiers.Any(SyntaxKind.PublicKeyword)),
                hasAnimationUsing);
        }
    }

    private static IReadOnlyDictionary<string, DesignAnimationDefinitionKind> ResolveKinds(
        IReadOnlyList<ProjectAnimationDeclaration> declarations)
    {
        var kinds = new Dictionary<string, DesignAnimationDefinitionKind>(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (ProjectAnimationDeclaration declaration in declarations)
            {
                if (kinds.ContainsKey(declaration.FullName)
                    || !TryReadKind(declaration, declarations, kinds, out DesignAnimationDefinitionKind kind))
                {
                    continue;
                }

                kinds.Add(declaration.FullName, kind);
                changed = true;
            }
        }
        while (changed);

        return kinds;
    }

    private static bool TryReadKind(
        ProjectAnimationDeclaration declaration,
        IReadOnlyList<ProjectAnimationDeclaration> declarations,
        IReadOnlyDictionary<string, DesignAnimationDefinitionKind> knownKinds,
        out DesignAnimationDefinitionKind kind)
    {
        string baseTypeName = RemoveGenericArguments(
            DesignerProjectUserControlDiscovery.NormalizeTypeName(declaration.BaseTypeName ?? string.Empty));
        ProjectAnimationDeclaration? projectBase = FindProjectBase(declaration, baseTypeName, declarations);
        if (projectBase is not null)
            return knownKinds.TryGetValue(projectBase.FullName, out kind);

        if (string.Equals(baseTypeName, $"{AnimationNamespace}.InteractionEffect", StringComparison.Ordinal)
            || declaration.HasAnimationNamespaceUsing
                && string.Equals(baseTypeName, "InteractionEffect", StringComparison.Ordinal))
        {
            kind = DesignAnimationDefinitionKind.InteractionEffect;
            return true;
        }

        if (string.Equals(baseTypeName, $"{AnimationNamespace}.AnimationDefinition", StringComparison.Ordinal)
            || declaration.HasAnimationNamespaceUsing
                && string.Equals(baseTypeName, "AnimationDefinition", StringComparison.Ordinal))
        {
            kind = DesignAnimationDefinitionKind.AnimationDefinition;
            return true;
        }

        kind = default;
        return false;
    }

    private static IReadOnlyList<DesignAnimationPropertyDescriptor> ReadInheritedProperties(
        ProjectAnimationDeclaration declaration,
        IReadOnlyList<ProjectAnimationDeclaration> declarations)
    {
        var properties = new List<DesignAnimationPropertyDescriptor>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        Append(declaration);
        return properties;

        void Append(ProjectAnimationDeclaration current)
        {
            if (!visiting.Add(current.FullName))
                return;

            string baseTypeName = RemoveGenericArguments(
                DesignerProjectUserControlDiscovery.NormalizeTypeName(current.BaseTypeName ?? string.Empty));
            ProjectAnimationDeclaration? projectBase = FindProjectBase(current, baseTypeName, declarations);
            if (projectBase is not null)
                Append(projectBase);

            foreach (DesignAnimationPropertyDescriptor property in current.DeclaredProperties)
            {
                if (indexes.TryGetValue(property.Name, out int index))
                    properties[index] = property;
                else
                {
                    indexes.Add(property.Name, properties.Count);
                    properties.Add(property);
                }
            }
        }
    }

    private static IEnumerable<DesignAnimationPropertyDescriptor> ReadProperties(
        ClassDeclarationSyntax type,
        IReadOnlyList<UsingDirectiveSyntax> usings,
        bool hasAnimationUsing)
    {
        foreach (PropertyDeclarationSyntax property in type.Members.OfType<PropertyDeclarationSyntax>())
        {
            AttributeSyntax? marker = FindAttribute(
                property.AttributeLists,
                PropertyAttributeName,
                usings,
                hasAnimationUsing);
            if (marker is null
                || !property.Modifiers.Any(SyntaxKind.PublicKeyword)
                || property.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    && !accessor.Modifiers.Any(SyntaxKind.PrivateKeyword)) != true)
            {
                continue;
            }

            DesignAnimationPropertyKind? kind = ReadPropertyKind(marker);
            if (kind is null)
                continue;

            string name = property.Identifier.ValueText;
            string? defaultText = ReadNamedString(marker, "DefaultValue");
            string? enumTypeName = ReadNamedString(marker, "EnumTypeName");
            string[] enumMembers = ReadNamedStringArray(marker, "EnumMembers");
            double declaredMinimum = ReadNamedDouble(marker, "Minimum") ?? double.MinValue;
            double minimum = kind == DesignAnimationPropertyKind.TimeSpan
                ? Math.Max(0d, declaredMinimum)
                : declaredMinimum;
            double maximum = ReadNamedDouble(marker, "Maximum") ?? double.MaxValue;
            string runtimeTypeName = property.Type.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
            if (minimum > maximum
                || !IsCompatiblePropertyType(kind.Value, runtimeTypeName, enumTypeName)
                || kind == DesignAnimationPropertyKind.Enum
                    && (!IsQualifiedIdentifier(enumTypeName)
                        || enumMembers.Length == 0
                        || enumMembers.Any(member => !DesignDocumentValidator.IsValidCSharpIdentifier(member)))
                || !TryCreateDefault(
                    kind.Value,
                    defaultText,
                    enumTypeName,
                    enumMembers,
                    minimum,
                    maximum,
                    out DesignPropertyValue defaultValue))
            {
                continue;
            }

            yield return new DesignAnimationPropertyDescriptor(name, name, kind.Value, defaultValue)
            {
                Minimum = minimum,
                Maximum = maximum,
                EnumTypeName = enumTypeName,
                EnumMembers = enumMembers,
                RuntimeTypeName = runtimeTypeName
            };
        }
    }

    private static DesignAnimationPropertyKind? ReadPropertyKind(AttributeSyntax marker)
    {
        string text = marker.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? string.Empty;
        string member = text.Split('.').Last();
        return Enum.TryParse(member, ignoreCase: false, out DesignAnimationPropertyKind result) ? result : null;
    }

    private static bool TryCreateDefault(
        DesignAnimationPropertyKind kind,
        string? text,
        string? enumTypeName,
        IReadOnlyList<string> enumMembers,
        double minimum,
        double maximum,
        out DesignPropertyValue value)
    {
        text ??= kind switch
        {
            DesignAnimationPropertyKind.Boolean => "false",
            DesignAnimationPropertyKind.Easing => "Linear",
            DesignAnimationPropertyKind.Enum => enumMembers.FirstOrDefault() ?? string.Empty,
            DesignAnimationPropertyKind.String => string.Empty,
            DesignAnimationPropertyKind.ColorArgb => "#00000000",
            _ => "0"
        };

        switch (kind)
        {
            case DesignAnimationPropertyKind.Boolean when bool.TryParse(text, out bool boolean):
                value = DesignPropertyValue.FromBoolean(boolean);
                return true;
            case DesignAnimationPropertyKind.Int32 when int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int integer)
                && integer >= minimum && integer <= maximum:
                value = DesignPropertyValue.FromInt32(integer);
                return true;
            case DesignAnimationPropertyKind.Number or DesignAnimationPropertyKind.TimeSpan
                when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && double.IsFinite(number) && number >= minimum && number <= maximum:
                value = DesignPropertyValue.FromDouble(number);
                return true;
            case DesignAnimationPropertyKind.Easing when KnownEasingDesignValue.IsKnown(text):
                value = DesignPropertyValue.FromString(text);
                return true;
            case DesignAnimationPropertyKind.Enum when enumMembers.Contains(text, StringComparer.Ordinal):
                value = DesignPropertyValue.FromEnum(enumTypeName!, text);
                return true;
            case DesignAnimationPropertyKind.ColorArgb when TryReadArgb(text, out int argb):
                value = DesignPropertyValue.FromInt32(argb);
                return true;
            case DesignAnimationPropertyKind.String:
                value = DesignPropertyValue.FromString(text);
                return true;
            default:
                value = DesignPropertyValue.FromNull();
                return false;
        }
    }

    private static AttributeSyntax? FindAttribute(
        SyntaxList<AttributeListSyntax> lists,
        string shortName,
        IReadOnlyList<UsingDirectiveSyntax> usings,
        bool hasAnimationUsing)
        => lists.SelectMany(list => list.Attributes).FirstOrDefault(attribute =>
        {
            string name = ResolveAliases(attribute.Name.ToString(), usings) ?? string.Empty;
            return string.Equals(name, $"{AnimationNamespace}.{shortName}", StringComparison.Ordinal)
                || string.Equals(name, $"{AnimationNamespace}.{shortName}Attribute", StringComparison.Ordinal)
                || hasAnimationUsing && (string.Equals(name, shortName, StringComparison.Ordinal)
                    || string.Equals(name, shortName + "Attribute", StringComparison.Ordinal));
        });

    private static string? ReadStringArgument(AttributeSyntax attribute, int index)
        => attribute.ArgumentList?.Arguments.ElementAtOrDefault(index)?.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.Token.ValueText
                : null;

    private static string? ReadNamedString(AttributeSyntax attribute, string name)
        => attribute.ArgumentList?.Arguments.FirstOrDefault(argument =>
                string.Equals(argument.NameEquals?.Name.Identifier.ValueText, name, StringComparison.Ordinal))
            ?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.Token.ValueText
                : null;

    private static double? ReadNamedDouble(AttributeSyntax attribute, string name)
    {
        ExpressionSyntax? expression = attribute.ArgumentList?.Arguments.FirstOrDefault(argument =>
            string.Equals(argument.NameEquals?.Name.Identifier.ValueText, name, StringComparison.Ordinal))?.Expression;
        if (expression is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.UnaryMinusExpression))
        {
            return double.TryParse(
                prefix.Operand.ToString().TrimEnd('d', 'D', 'f', 'F'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double negative)
                    ? -negative
                    : null;
        }

        return expression is not null && double.TryParse(
            expression.ToString().TrimEnd('d', 'D', 'f', 'F'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double value)
                ? value
                : null;
    }

    private static string[] ReadNamedStringArray(AttributeSyntax attribute, string name)
    {
        ExpressionSyntax? expression = attribute.ArgumentList?.Arguments.FirstOrDefault(argument =>
            string.Equals(argument.NameEquals?.Name.Identifier.ValueText, name, StringComparison.Ordinal))?.Expression;
        InitializerExpressionSyntax? initializer = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer,
            CollectionExpressionSyntax collection => SyntaxFactory.InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SyntaxFactory.SeparatedList(collection.Elements.OfType<ExpressionElementSyntax>().Select(item => item.Expression))),
            _ => null
        };
        return initializer?.Expressions.OfType<LiteralExpressionSyntax>()
            .Where(item => item.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(item => item.Token.ValueText)
            .ToArray() ?? [];
    }

    private static bool TryReadArgb(string text, out int value)
    {
        value = 0;
        return text.Length == 9 && text[0] == '#'
            && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb)
            && (value = unchecked((int)argb)) == unchecked((int)argb);
    }

    private static bool IsCompatiblePropertyType(
        DesignAnimationPropertyKind kind,
        string typeName,
        string? enumTypeName)
    {
        string normalized = typeName.Replace(" ", string.Empty, StringComparison.Ordinal);
        string simpleName = normalized.Split('.').Last();
        return kind switch
        {
            DesignAnimationPropertyKind.Boolean => simpleName is "bool" or "Boolean",
            DesignAnimationPropertyKind.Int32 => simpleName is "int" or "Int32",
            DesignAnimationPropertyKind.Number => simpleName is "float" or "Single" or "double" or "Double",
            DesignAnimationPropertyKind.TimeSpan => simpleName == "TimeSpan",
            DesignAnimationPropertyKind.Easing => normalized
                is "Func<float,float>" or "System.Func<float,float>" or "Func<System.Single,System.Single>" or "System.Func<System.Single,System.Single>",
            DesignAnimationPropertyKind.Enum => !string.IsNullOrWhiteSpace(enumTypeName)
                && string.Equals(simpleName, enumTypeName.Split('.').Last(), StringComparison.Ordinal),
            DesignAnimationPropertyKind.ColorArgb => simpleName == "Color",
            DesignAnimationPropertyKind.String => simpleName is "string" or "String",
            _ => false
        };
    }

    private static bool IsQualifiedIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Split('.').All(DesignDocumentValidator.IsValidCSharpIdentifier);

    private static ProjectAnimationDeclaration MergePartialDeclarations(
        IGrouping<string, ProjectAnimationDeclaration> declarations)
    {
        ProjectAnimationDeclaration declaration = declarations
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.BaseTypeName))
            ?? declarations.First();
        return declaration with
        {
            BaseTypeName = declarations.Select(candidate => candidate.BaseTypeName)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            DisplayName = declarations.Select(candidate => candidate.DisplayName)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            DeclaredProperties = declarations.SelectMany(candidate => candidate.DeclaredProperties).ToArray(),
            IsPublic = declarations.Any(candidate => candidate.IsPublic),
            IsAbstract = declarations.Any(candidate => candidate.IsAbstract),
            IsGeneric = declarations.Any(candidate => candidate.IsGeneric),
            HasAnyConstructor = declarations.Any(candidate => candidate.HasAnyConstructor),
            HasPublicParameterlessConstructor = declarations.Any(candidate => candidate.HasPublicParameterlessConstructor),
            HasAnimationNamespaceUsing = declarations.Any(candidate => candidate.HasAnimationNamespaceUsing)
        };
    }

    private static ProjectAnimationDeclaration? FindProjectBase(
        ProjectAnimationDeclaration declaration,
        string typeName,
        IReadOnlyList<ProjectAnimationDeclaration> declarations)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (!typeName.Contains('.'))
        {
            string relativeName = string.IsNullOrWhiteSpace(declaration.Namespace)
                ? typeName
                : $"{declaration.Namespace}.{typeName}";
            return declarations.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, relativeName, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(declaration.Namespace))
        {
            string relativeName = $"{declaration.Namespace}.{typeName}";
            ProjectAnimationDeclaration? relative = declarations.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, relativeName, StringComparison.Ordinal));
            if (relative is not null)
                return relative;
        }

        return declarations.FirstOrDefault(candidate =>
            string.Equals(candidate.FullName, typeName, StringComparison.Ordinal));
    }

    private static IEnumerable<UsingDirectiveSyntax> ReadUsings(ClassDeclarationSyntax declaration)
        => declaration.Ancestors().SelectMany(node => node switch
        {
            CompilationUnitSyntax compilationUnit => compilationUnit.Usings,
            BaseNamespaceDeclarationSyntax namespaceDeclaration => namespaceDeclaration.Usings,
            _ => []
        });

    private static bool HasNamespaceUsing(IEnumerable<UsingDirectiveSyntax> usings, string namespaceName)
        => usings.Any(usingDirective => usingDirective.Alias is null
            && string.Equals(
                DesignerProjectUserControlDiscovery.NormalizeTypeName(usingDirective.Name?.ToString() ?? string.Empty),
                namespaceName,
                StringComparison.Ordinal));

    private static string? ResolveAliases(
        string? typeName,
        IEnumerable<UsingDirectiveSyntax> usings)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return typeName;

        string normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        foreach (UsingDirectiveSyntax usingDirective in usings)
        {
            string? alias = usingDirective.Alias?.Name.Identifier.ValueText;
            string? target = usingDirective.Name?.ToString();
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(target))
                continue;

            target = DesignerProjectUserControlDiscovery.NormalizeTypeName(target);
            if (string.Equals(normalized, alias, StringComparison.Ordinal))
                return target;
            if (normalized.StartsWith(alias + ".", StringComparison.Ordinal))
                return target + normalized[alias.Length..];
        }

        return normalized;
    }

    private static string RemoveGenericArguments(string typeName)
    {
        var result = new StringBuilder(typeName.Length);
        int depth = 0;
        foreach (char character in typeName)
        {
            if (character == '<')
            {
                depth++;
                continue;
            }
            if (character == '>')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }
            if (depth == 0 && !char.IsWhiteSpace(character))
                result.Append(character);
        }
        return result.ToString();
    }

    private static string ReadNamespace(ClassDeclarationSyntax declaration)
    {
        var segments = new Stack<string>();
        for (SyntaxNode? node = declaration.Parent; node is not null; node = node.Parent)
        {
            if (node is BaseNamespaceDeclarationSyntax namespaceDeclaration)
                segments.Push(namespaceDeclaration.Name.ToString());
        }
        return string.Join(".", segments);
    }

    private sealed record ProjectAnimationDeclaration(
        string Name,
        string Namespace,
        string FullName,
        string? BaseTypeName,
        string? DisplayName,
        IReadOnlyList<DesignAnimationPropertyDescriptor> DeclaredProperties,
        bool IsPublic,
        bool IsAbstract,
        bool IsGeneric,
        bool HasAnyConstructor,
        bool HasPublicParameterlessConstructor,
        bool HasAnimationNamespaceUsing);
}

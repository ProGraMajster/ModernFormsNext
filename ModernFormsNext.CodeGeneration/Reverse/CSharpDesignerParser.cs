using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Parses the deterministic C# designer code emitted by ModernFormsNext back into a design document.
/// </summary>
/// <remarks>
/// This parser is intentionally conservative. It recognizes the generated
/// <c>InitializeComponent</c> shape and a small set of literal assignments, but it does
/// not execute code, evaluate arbitrary expressions, or attempt to understand dynamic
/// C# constructs such as loops and conditionals.
/// </remarks>
public sealed class CSharpDesignerParser
{
    private readonly CSharpDesignerSyntaxReader syntaxReader = new();
    private readonly List<CSharpDesignerDiagnostic> diagnostics = [];
    private readonly Dictionary<string, FieldDefinition> fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DesignControlNode> nodes = new(StringComparer.Ordinal);
    private readonly List<string> nodeOrder = [];
    private readonly List<ControlAddOperation> addOperations = [];
    private IReadOnlyList<DesignAnimationDefinitionDescriptor> animationDefinitions = [];

    /// <summary>
    /// Parses generated C# designer source into a ModernFormsNext design document.
    /// </summary>
    /// <param name="sourceText">The C# source text to parse.</param>
    /// <param name="options">Optional parser settings.</param>
    /// <returns>The parse result, including a document when parsing produced usable model state.</returns>
    public CSharpDesignerParseResult Parse(
        string sourceText,
        CSharpDesignerParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        options ??= new CSharpDesignerParseOptions();
        animationDefinitions = options.AnimationDefinitions ?? [];
        diagnostics.Clear();
        fields.Clear();
        nodes.Clear();
        nodeOrder.Clear();
        addOperations.Clear();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var root = syntaxTree.GetCompilationUnitRoot();

        foreach (var diagnostic in syntaxTree.GetDiagnostics())
        {
            var severity = diagnostic.Severity == DiagnosticSeverity.Error
                ? CSharpDesignerDiagnosticSeverity.Error
                : CSharpDesignerDiagnosticSeverity.Warning;
            var position = diagnostic.Location.GetLineSpan().StartLinePosition;
            diagnostics.Add(new CSharpDesignerDiagnostic(
                severity,
                diagnostic.GetMessage(CultureInfo.InvariantCulture),
                position.Line + 1,
                position.Character + 1));
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == CSharpDesignerDiagnosticSeverity.Error))
            return CreateResult(null, options);

        var classDeclaration = FindDesignerClass(root, options);
        if (classDeclaration is null)
        {
            AddDiagnostic(CSharpDesignerDiagnosticSeverity.Error, "No class declaration was found in the designer source.", root);
            return CreateResult(null, options);
        }

        var initializeComponent = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method => string.Equals(method.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal));

        if (initializeComponent?.Body is null)
        {
            AddDiagnostic(CSharpDesignerDiagnosticSeverity.Error, "The designer class does not contain a block-bodied InitializeComponent method.", classDeclaration);
            return CreateResult(null, options);
        }

        ReadFields(classDeclaration);

        var document = new DesignDocument
        {
            Namespace = options.NamespaceOverride ?? ReadNamespace(classDeclaration),
            ClassName = options.ClassNameOverride ?? classDeclaration.Identifier.ValueText,
            FormName = options.FormNameOverride ?? classDeclaration.Identifier.ValueText,
            RootKind = options.RootKind,
            Size = options.DefaultFormSize
        };

        var formNameAssigned = !string.IsNullOrWhiteSpace(options.FormNameOverride);

        foreach (var statement in initializeComponent.Body.Statements)
            ProcessStatement(statement, document, ref formNameAssigned);

        BuildHierarchy(document);

        if (string.IsNullOrWhiteSpace(document.FormName))
            document.FormName = document.ClassName;

        return CreateResult(document, options);
    }

    private static ClassDeclarationSyntax? FindDesignerClass(
        CompilationUnitSyntax root,
        CSharpDesignerParseOptions options)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

        if (!string.IsNullOrWhiteSpace(options.ClassNameOverride))
        {
            return classes.FirstOrDefault(type => string.Equals(type.Identifier.ValueText, options.ClassNameOverride, StringComparison.Ordinal))
                ?? classes.FirstOrDefault();
        }

        return classes.FirstOrDefault(type => type.Modifiers.Any(SyntaxKind.PartialKeyword))
            ?? classes.FirstOrDefault();
    }

    private static string ReadNamespace(ClassDeclarationSyntax classDeclaration)
    {
        for (SyntaxNode? node = classDeclaration.Parent; node is not null; node = node.Parent)
        {
            if (node is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                return fileScopedNamespace.Name.ToString();

            if (node is NamespaceDeclarationSyntax namespaceDeclaration)
                return namespaceDeclaration.Name.ToString();
        }

        return string.Empty;
    }

    private void ReadFields(ClassDeclarationSyntax classDeclaration)
    {
        foreach (var field in classDeclaration.Members.OfType<FieldDeclarationSyntax>())
        {
            var typeName = field.Declaration.Type.ToString();
            var visibility = ReadMemberVisibility(field.Modifiers);

            foreach (var variable in field.Declaration.Variables)
            {
                fields[variable.Identifier.ValueText] = new FieldDefinition(typeName, visibility);
            }
        }
    }

    private static DesignerMemberVisibility ReadMemberVisibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
            return DesignerMemberVisibility.Public;
        if (modifiers.Any(SyntaxKind.InternalKeyword))
            return DesignerMemberVisibility.Internal;
        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
            return DesignerMemberVisibility.Protected;

        return DesignerMemberVisibility.Private;
    }

    private void ProcessStatement(StatementSyntax statement, DesignDocument document, ref bool formNameAssigned)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax localDeclaration:
                ProcessLocalDeclaration(localDeclaration);
                break;

            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }:
                ProcessAssignment(assignment, document, ref formNameAssigned);
                break;

            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                ProcessInvocation(invocation, document);
                break;

            case EmptyStatementSyntax:
                break;

            default:
                AddUnsupported(
                    statement,
                    "Unsupported statement in InitializeComponent. Reverse sync only understands literal generated initialization statements.");
                break;
        }
    }

    private void ProcessLocalDeclaration(LocalDeclarationStatementSyntax localDeclaration)
    {
        foreach (var variable in localDeclaration.Declaration.Variables)
        {
            if (TryReadObjectCreationType(variable.Initializer?.Value, out var typeName))
            {
                var declaredType = localDeclaration.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" }
                    ? typeName
                    : localDeclaration.Declaration.Type.ToString();

                EnsureNode(variable.Identifier.ValueText, declaredType, DesignerMemberVisibility.None, variable);
                continue;
            }

            AddUnsupported(localDeclaration, "Unsupported local declaration in InitializeComponent.");
        }
    }

    private void ProcessAssignment(
        AssignmentExpressionSyntax assignment,
        DesignDocument document,
        ref bool formNameAssigned)
    {
        if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            if (TryReadObjectCreationType(assignment.Right, out var createdType)
                && syntaxReader.GetObjectReferenceName(assignment.Left) is { } createdName
                && fields.TryGetValue(createdName, out var field))
            {
                EnsureNode(createdName, createdType, field.Visibility, assignment);
                return;
            }

            // Object-valued properties such as this.Size and control.Bounds also use a `new ...`
            // expression. Only declared designer fields represent control construction; all other
            // targets must continue to property parsing so form geometry survives reverse sync.

            if (TryReadPropertyAccess(assignment.Left, out var ownerName, out var propertyPath))
            {
                ProcessPropertyAssignment(ownerName, propertyPath, assignment.Right, document, assignment, ref formNameAssigned);
                return;
            }
        }

        if (assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            && TryReadPropertyAccess(assignment.Left, out var eventOwnerName, out var eventName))
        {
            ProcessEventSubscription(eventOwnerName, eventName, assignment.Right, document, assignment);
            return;
        }

        AddUnsupported(assignment, "Unsupported assignment in InitializeComponent.");
    }

    private void ProcessInvocation(InvocationExpressionSyntax invocation, DesignDocument document)
    {
        if (TryProcessInteractionEffectAdd(invocation, document) || TryProcessVisualStateTransitionAdd(invocation, document))
            return;

        if (TryReadControlsAdd(invocation, out var parentName, out var childName))
        {
            addOperations.Add(new ControlAddOperation(parentName, childName, invocation));
            return;
        }

        AddUnsupported(invocation, "Unsupported method call in InitializeComponent.");
    }

    private bool TryProcessInteractionEffectAdd(InvocationExpressionSyntax invocation, DesignDocument document)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax addAccess
            || !string.Equals(addAccess.Name.Identifier.ValueText, "Add", StringComparison.Ordinal)
            || addAccess.Expression is not MemberAccessExpressionSyntax effectsAccess
            || !string.Equals(effectsAccess.Name.Identifier.ValueText, InteractionEffectDesignValue.PropertyName, StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        string? ownerName = effectsAccess.Expression is ThisExpressionSyntax
            ? "this"
            : syntaxReader.GetObjectReferenceName(effectsAccess.Expression);
        if (string.IsNullOrWhiteSpace(ownerName))
            return true;

        IDictionary<string, DesignPropertyValue> properties;
        if (string.Equals(ownerName, "this", StringComparison.Ordinal))
            properties = document.Properties;
        else
        {
            if (!TryGetNode(ownerName, invocation, out DesignControlNode node))
                return true;
            properties = node.Properties;
        }

        if (!TryReadInteractionEffect(invocation.ArgumentList.Arguments[0].Expression, out DesignPropertyValue effect))
        {
            AddUnsupported(invocation, $"Unsupported interaction effect initializer for '{ownerName}'.");
            return true;
        }

        properties.TryGetValue(InteractionEffectDesignValue.PropertyName, out DesignPropertyValue? existing);
        if (!InteractionEffectDesignValue.TryRead(existing, out IReadOnlyList<DesignPropertyValue> effects, out string? error))
        {
            AddUnsupported(invocation, $"Cannot append interaction effect for '{ownerName}': {error}");
            return true;
        }

        properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create(effects.Append(effect));
        return true;
    }

    private bool TryReadInteractionEffect(ExpressionSyntax expression, out DesignPropertyValue effect)
    {
        effect = null!;
        if (expression is not ObjectCreationExpressionSyntax objectCreation
            || objectCreation.Initializer is null)
        {
            return false;
        }

        string typeName = objectCreation.Type.ToString();
        DesignAnimationDefinitionDescriptor? descriptor = FindEffectDescriptor(typeName);
        if (descriptor is null)
            return false;
        var byRuntimeName = descriptor.Properties.ToDictionary(item => item.RuntimePropertyName, StringComparer.Ordinal);

        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        foreach (ExpressionSyntax initializer in objectCreation.Initializer.Expressions)
        {
            if (initializer is not AssignmentExpressionSyntax assignment
                || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                return false;
            }

            string propertyName = assignment.Left switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(propertyName)
                || !byRuntimeName.TryGetValue(propertyName, out DesignAnimationPropertyDescriptor? property))
                return false;
            if (property.Kind == DesignAnimationPropertyKind.ColorArgb)
            {
                if (!TryReadColorArgb(assignment.Right, out int argb))
                    return false;
                if (!properties.TryAdd(property.Name, DesignPropertyValue.FromInt32(argb)))
                    return false;
                continue;
            }
            if (property.Kind == DesignAnimationPropertyKind.TimeSpan)
            {
                if (!TryReadTimeSpanMilliseconds(assignment.Right, out double milliseconds)
                    || !IsAnimationNumberInRange(property, milliseconds))
                    return false;
                if (!properties.TryAdd(property.Name, DesignPropertyValue.FromDouble(milliseconds)))
                    return false;
                continue;
            }
            if (property.Kind == DesignAnimationPropertyKind.Easing)
            {
                if (!TryReadKnownEasing(assignment.Right, out string easing)
                    || !properties.TryAdd(property.Name, DesignPropertyValue.FromString(easing)))
                    return false;
                continue;
            }

            if (!TryReadDesignerValue(assignment.Right, out DesignPropertyValue value)
                || !IsAnimationValueCompatible(property, value))
                return false;
            if (!properties.TryAdd(property.Name, value))
                return false;
        }

        effect = DesignPropertyValue.FromStructuredObject(descriptor.TypeName, properties);
        return true;
    }

    private static bool IsAnimationValueCompatible(
        DesignAnimationPropertyDescriptor property,
        DesignPropertyValue value)
        => property.Kind switch
        {
            DesignAnimationPropertyKind.Boolean => value.Kind == DesignPropertyValueKind.Boolean,
            DesignAnimationPropertyKind.Int32 => value.Kind == DesignPropertyValueKind.Int32
                && IsAnimationNumberInRange(property, Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)),
            DesignAnimationPropertyKind.Number => value.Kind is DesignPropertyValueKind.Int32 or DesignPropertyValueKind.Double
                && IsAnimationNumberInRange(property, Convert.ToDouble(value.Value, CultureInfo.InvariantCulture))
                && IsRepresentableRuntimeNumber(property, Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)),
            DesignAnimationPropertyKind.Enum => value.Kind == DesignPropertyValueKind.Enum
                && IsExpectedEnumType(value.EnumTypeName, property.EnumTypeName)
                && property.EnumMembers.Contains(value.GetString(), StringComparer.Ordinal),
            DesignAnimationPropertyKind.String => value.Kind == DesignPropertyValueKind.String,
            _ => false
        };

    private static bool IsAnimationNumberInRange(
        DesignAnimationPropertyDescriptor property,
        double number)
        => double.IsFinite(number) && number >= property.Minimum && number <= property.Maximum;

    private static bool IsRepresentableRuntimeNumber(
        DesignAnimationPropertyDescriptor property,
        double number)
    {
        if (property.RuntimeTypeName?.Split('.').Last() is not ("float" or "Single"))
            return true;
        float single = (float)number;
        return float.IsFinite(single) && (single != 0f || number == 0d);
    }

    private static bool IsExpectedEnumType(string? actual, string? expected)
    {
        string normalizedActual = (actual ?? string.Empty).Replace("global::", string.Empty, StringComparison.Ordinal);
        string normalizedExpected = (expected ?? string.Empty).Replace("global::", string.Empty, StringComparison.Ordinal);
        return string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal)
            || string.Equals(normalizedActual, normalizedExpected.Split('.').Last(), StringComparison.Ordinal);
    }

    private DesignAnimationDefinitionDescriptor? FindEffectDescriptor(string typeName)
    {
        string normalized = typeName.Replace("global::", string.Empty, StringComparison.Ordinal);
        return BuiltInAnimationDefinitionCatalog.Definitions.Concat(animationDefinitions)
            .FirstOrDefault(item => item.Kind == DesignAnimationDefinitionKind.InteractionEffect
                && (string.Equals(item.TypeName, normalized, StringComparison.Ordinal)
                    || item.IsBuiltIn && string.Equals(item.TypeName.Split('.').Last(), normalized, StringComparison.Ordinal)));
    }

    private static bool TryReadKnownEasing(ExpressionSyntax expression, out string easing)
    {
        easing = expression is MemberAccessExpressionSyntax member
            ? member.Name.Identifier.ValueText
            : string.Empty;
        return expression is MemberAccessExpressionSyntax easingMember
            && IsTypeReference(easingMember.Expression, "Easings", "ModernFormsNext.Animations.Easings")
            && KnownEasingDesignValue.IsKnown(easing);
    }

    private bool TryProcessVisualStateTransitionAdd(InvocationExpressionSyntax invocation, DesignDocument document)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax addAccess
            || !string.Equals(addAccess.Name.Identifier.ValueText, "Add", StringComparison.Ordinal)
            || addAccess.Expression is not MemberAccessExpressionSyntax collectionAccess
            || !string.Equals(collectionAccess.Name.Identifier.ValueText, VisualStateTransitionDesignValue.PropertyName, StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 3)
            return false;

        string? ownerName = collectionAccess.Expression is ThisExpressionSyntax
            ? "this"
            : syntaxReader.GetObjectReferenceName(collectionAccess.Expression);
        if (string.IsNullOrWhiteSpace(ownerName))
            return true;
        if (!TryReadVisualState(invocation.ArgumentList.Arguments[0].Expression, out string from)
            || !TryReadVisualState(invocation.ArgumentList.Arguments[1].Expression, out string to)
            || !TryReadVisualStateTransition(invocation.ArgumentList.Arguments[2].Expression, out double duration, out string easing))
        {
            AddUnsupported(invocation, $"Unsupported visual-state transition initializer for '{ownerName}'.");
            return true;
        }

        IDictionary<string, DesignPropertyValue> properties;
        if (string.Equals(ownerName, "this", StringComparison.Ordinal))
            properties = document.Properties;
        else
        {
            if (!TryGetNode(ownerName, invocation, out DesignControlNode node))
                return true;
            properties = node.Properties;
        }
        properties.TryGetValue(VisualStateTransitionDesignValue.PropertyName, out var existing);
        if (!VisualStateTransitionDesignValue.TryRead(existing, out var transitions, out string? error))
        {
            AddUnsupported(invocation, $"Cannot append visual-state transition for '{ownerName}': {error}");
            return true;
        }
        try
        {
            properties[VisualStateTransitionDesignValue.PropertyName] = VisualStateTransitionDesignValue.Create(
                transitions.Append(new DesignVisualStateTransition(from, to, duration, easing)));
        }
        catch (ArgumentException exception)
        {
            AddUnsupported(invocation, exception.Message);
        }
        return true;
    }

    private static bool TryReadVisualState(ExpressionSyntax expression, out string state)
    {
        state = expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.ValueText : string.Empty;
        return expression is MemberAccessExpressionSyntax stateMember
            && IsTypeReference(stateMember.Expression, "VisualState", "ModernFormsNext.Animations.VisualState")
            && state is "Normal" or "Hover" or "Pressed" or "Disabled" or "Focused";
    }

    private static bool TryReadVisualStateTransition(
        ExpressionSyntax expression,
        out double duration,
        out string easing)
    {
        duration = 150d;
        easing = "CubicOut";
        if (expression is not ObjectCreationExpressionSyntax creation
            || !IsTypeReference(creation.Type, "VisualStateTransition", "ModernFormsNext.Animations.VisualStateTransition")
            || creation.Initializer is null)
            return false;
        foreach (ExpressionSyntax item in creation.Initializer.Expressions)
        {
            if (item is not AssignmentExpressionSyntax assignment)
                return false;
            string name = assignment.Left.ToString();
            if (name == "Duration" && !TryReadTimeSpanMilliseconds(assignment.Right, out duration))
                return false;
            if (name == "Easing" && !TryReadKnownEasing(assignment.Right, out easing))
                return false;
            if (name is not ("Duration" or "Easing"))
                return false;
        }
        return true;
    }

    private static bool TryReadTimeSpanMilliseconds(ExpressionSyntax expression, out double milliseconds)
    {
        milliseconds = 0d;
        return expression is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax member
            && string.Equals(member.Name.Identifier.ValueText, "FromMilliseconds", StringComparison.Ordinal)
            && IsTypeReference(member.Expression, "TimeSpan", "System.TimeSpan")
            && invocation.ArgumentList.Arguments.Count == 1
            && TryReadDouble(invocation.ArgumentList.Arguments[0].Expression, out milliseconds)
            && double.IsFinite(milliseconds)
            && milliseconds >= 0d;
    }

    private static bool TryReadColorArgb(ExpressionSyntax expression, out int argb)
    {
        argb = 0;
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member
            || !string.Equals(member.Name.Identifier.ValueText, "FromArgb", StringComparison.Ordinal)
            || !IsTypeReference(member.Expression, "Color", "System.Drawing.Color")
            || invocation.ArgumentList.Arguments.Count != 4)
        {
            return false;
        }

        var channels = new int[4];
        for (int index = 0; index < channels.Length; index++)
        {
            if (!TryReadInt32(invocation.ArgumentList.Arguments[index].Expression, out channels[index])
                || channels[index] is < 0 or > 255)
            {
                return false;
            }
        }

        argb = unchecked((int)(((uint)channels[0] << 24)
            | ((uint)channels[1] << 16)
            | ((uint)channels[2] << 8)
            | (uint)channels[3]));
        return true;
    }

    private void ProcessPropertyAssignment(
        string? ownerName,
        string propertyPath,
        ExpressionSyntax valueExpression,
        DesignDocument document,
        SyntaxNode syntax,
        ref bool formNameAssigned)
    {
        if (ownerName is null)
        {
            ProcessFormPropertyAssignment(propertyPath, valueExpression, document, syntax, ref formNameAssigned);
            return;
        }

        if (!TryGetNode(ownerName, syntax, out var node))
            return;

        switch (propertyPath)
        {
            case "Name":
                if (TryReadString(valueExpression, out var name) && !string.IsNullOrWhiteSpace(name))
                    node.Name = name;
                else
                    AddUnsupported(valueExpression, $"Unsupported Name assignment for '{ownerName}'.");
                break;

            case "Bounds":
                if (TryReadBounds(valueExpression, out var bounds))
                    node.Bounds = bounds;
                else
                    AddUnsupported(valueExpression, $"Unsupported Bounds assignment for '{ownerName}'.");
                break;

            case "Location":
                if (TryReadPoint(valueExpression, out var x, out var y))
                    node.Bounds = node.Bounds with { X = x, Y = y };
                else
                    AddUnsupported(valueExpression, $"Unsupported Location assignment for '{ownerName}'.");
                break;

            case "Size":
                if (TryReadSize(valueExpression, out var width, out var height))
                    node.Bounds = node.Bounds with { Width = width, Height = height };
                else
                    AddUnsupported(valueExpression, $"Unsupported Size assignment for '{ownerName}'.");
                break;

            default:
                if (propertyPath == LayoutTransitionDesignValue.PropertyName
                    && TryReadLayoutTransition(valueExpression, out var layoutTransition))
                    node.Properties[propertyPath] = layoutTransition;
                else if (TryReadDesignerValue(valueExpression, out var value))
                    node.Properties[propertyPath] = value;
                else
                    AddUnsupported(valueExpression, $"Unsupported value assigned to '{ownerName}.{propertyPath}'.");
                break;
        }
    }

    private void ProcessFormPropertyAssignment(
        string propertyPath,
        ExpressionSyntax valueExpression,
        DesignDocument document,
        SyntaxNode syntax,
        ref bool formNameAssigned)
    {
        switch (propertyPath)
        {
            case "Name":
                if (TryReadString(valueExpression, out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    document.FormName = name;
                    formNameAssigned = true;
                }
                else
                {
                    AddUnsupported(valueExpression, "Unsupported form Name assignment.");
                }

                break;

            case "Text":
                if (document.RootKind == DesignRootKind.Form
                    && !formNameAssigned
                    && TryReadString(valueExpression, out var text)
                    && !string.IsNullOrWhiteSpace(text))
                {
                    document.FormName = text;
                }
                else if (document.RootKind == DesignRootKind.UserControl
                    && TryReadDesignerValue(valueExpression, out var userControlText))
                {
                    document.Properties[propertyPath] = userControlText;
                }

                break;

            // Size is the canonical generated contract. Keep accepting ClientSize from earlier
            // designer builds so existing .Designer.cs files can still be imported and rewritten
            // in the current canonical form.
            case "Size":
            case "ClientSize":
                if (TryReadSize(valueExpression, out var width, out var height))
                    document.Size = new DesignSize(width, height);
                else
                    AddUnsupported(valueExpression, $"Unsupported form {propertyPath} assignment.");
                break;

            default:
                if (propertyPath == LayoutTransitionDesignValue.PropertyName
                    && TryReadLayoutTransition(valueExpression, out var layoutTransition))
                    document.Properties[propertyPath] = layoutTransition;
                else if (TryReadDesignerValue(valueExpression, out var value))
                    document.Properties[propertyPath] = value;
                else
                    AddUnsupported(syntax, $"Unsupported design root property assignment '{propertyPath}'.");
                break;
        }
    }

    private static bool TryReadLayoutTransition(ExpressionSyntax expression, out DesignPropertyValue value)
    {
        bool enabled = true;
        double duration = 250d;
        string easing = "EaseOut";
        value = null!;
        if (expression is not ObjectCreationExpressionSyntax creation
            || !IsTypeReference(creation.Type, "LayoutTransition", "ModernFormsNext.Animations.LayoutTransition")
            || creation.Initializer is null)
            return false;
        foreach (ExpressionSyntax item in creation.Initializer.Expressions)
        {
            if (item is not AssignmentExpressionSyntax assignment)
                return false;
            string name = assignment.Left.ToString();
            if (name == "Enabled" && !TryReadBoolean(assignment.Right, out enabled))
                return false;
            if (name == "Duration" && !TryReadTimeSpanMilliseconds(assignment.Right, out duration))
                return false;
            if (name == "Easing" && !TryReadKnownEasing(assignment.Right, out easing))
                return false;
            if (name is not ("Enabled" or "Duration" or "Easing"))
                return false;
        }
        value = LayoutTransitionDesignValue.Create(enabled, duration, easing);
        return true;
    }

    private static bool IsTypeReference(SyntaxNode syntax, string shortName, string fullName)
    {
        string value = syntax.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
        return string.Equals(value, shortName, StringComparison.Ordinal)
            || string.Equals(value, fullName, StringComparison.Ordinal);
    }

    private void ProcessEventSubscription(
        string? ownerName,
        string eventName,
        ExpressionSyntax handlerExpression,
        DesignDocument document,
        SyntaxNode syntax)
    {
        if (syntaxReader.GetObjectReferenceName(handlerExpression) is not { } handlerName)
        {
            AddUnsupported(handlerExpression, $"Unsupported event handler expression for '{eventName}'.");
            return;
        }

        if (ownerName is null)
        {
            document.Events[eventName] = handlerName;
            return;
        }

        if (TryGetNode(ownerName, syntax, out var node))
            node.Events[eventName] = handlerName;
    }

    private DesignControlNode EnsureNode(
        string variableName,
        string typeName,
        DesignerMemberVisibility visibility,
        SyntaxNode syntax)
    {
        if (nodes.TryGetValue(variableName, out var existing))
        {
            if (!string.Equals(existing.TypeName, typeName, StringComparison.Ordinal))
            {
                AddDiagnostic(
                    CSharpDesignerDiagnosticSeverity.Warning,
                    $"Control '{variableName}' was created more than once with different types. Keeping '{existing.TypeName}'.",
                    syntax);
            }

            return existing;
        }

        var node = new DesignControlNode
        {
            TypeName = typeName,
            Name = variableName,
            Bounds = new DesignBounds(0, 0, 75, 23),
            MemberVisibility = visibility
        };

        nodes.Add(variableName, node);
        nodeOrder.Add(variableName);
        return node;
    }

    private bool TryGetNode(string variableName, SyntaxNode syntax, out DesignControlNode node)
    {
        if (nodes.TryGetValue(variableName, out node!))
            return true;

        if (fields.TryGetValue(variableName, out var field))
        {
            node = EnsureNode(variableName, field.TypeName, field.Visibility, syntax);
            return true;
        }

        AddDiagnostic(
            CSharpDesignerDiagnosticSeverity.Warning,
            $"Assignment references unknown control '{variableName}'.",
            syntax);
        node = null!;
        return false;
    }

    private void BuildHierarchy(DesignDocument document)
    {
        foreach (var node in nodes.Values)
            node.Children.Clear();

        document.Controls.Clear();
        var added = new HashSet<string>(StringComparer.Ordinal);

        // Runtime Controls.Add appends at the visual front. Ordinary generated containers therefore
        // emit back-to-front and are inserted at document index zero to recover canonical
        // front-to-back order. Sequential flow/table/tab collections preserve invocation order.
        foreach (var operation in addOperations)
        {
            if (!nodes.TryGetValue(operation.ChildName, out var child))
            {
                AddDiagnostic(
                    CSharpDesignerDiagnosticSeverity.Warning,
                    $"Controls.Add references unknown child '{operation.ChildName}'.",
                    operation.Syntax);
                continue;
            }

            if (!added.Add(operation.ChildName))
            {
                AddDiagnostic(
                    CSharpDesignerDiagnosticSeverity.Warning,
                    $"Control '{operation.ChildName}' is added to more than one parent. The first add is kept.",
                    operation.Syntax);
                continue;
            }

            if (operation.ParentName is null)
            {
                document.Controls.Insert(0, child);
                continue;
            }

            if (!nodes.TryGetValue(operation.ParentName, out var parent))
            {
                AddDiagnostic(
                    CSharpDesignerDiagnosticSeverity.Warning,
                    $"Controls.Add references unknown parent '{operation.ParentName}'.",
                    operation.Syntax);
                document.Controls.Add(child);
                continue;
            }

            if (ReferenceEquals(parent, child) || ContainsDescendant(child, parent))
            {
                AddDiagnostic(
                    CSharpDesignerDiagnosticSeverity.Warning,
                    $"Controls.Add would create a cycle between '{operation.ParentName}' and '{operation.ChildName}'.",
                    operation.Syntax);
                document.Controls.Add(child);
                continue;
            }

            if (PreservesSequentialChildOrder(parent))
                parent.Children.Add(child);
            else
                parent.Children.Insert(0, child);
        }

        foreach (var name in nodeOrder)
        {
            if (added.Contains(name))
                continue;

            AddDiagnostic(
                CSharpDesignerDiagnosticSeverity.Warning,
                $"Control '{name}' was created but not added to a Controls collection. It was placed on the design root.",
                null);
            document.Controls.Add(nodes[name]);
        }
    }

    private static bool PreservesSequentialChildOrder(DesignControlNode node)
        => IsType(node.TypeName, "FlowLayoutPanel")
        || IsType(node.TypeName, "TableLayoutPanel")
        || IsType(node.TypeName, "TabControl");

    private static bool ContainsDescendant(DesignControlNode root, DesignControlNode candidate)
    {
        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, candidate) || ContainsDescendant(child, candidate))
                return true;
        }

        return false;
    }

    private bool TryReadControlsAdd(
        InvocationExpressionSyntax invocation,
        out string? parentName,
        out string childName)
    {
        parentName = null;
        childName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax addAccess
            || !string.Equals(addAccess.Name.Identifier.ValueText, "Add", StringComparison.Ordinal)
            || addAccess.Expression is not MemberAccessExpressionSyntax controlsAccess
            || !string.Equals(controlsAccess.Name.Identifier.ValueText, "Controls", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        if (controlsAccess.Expression is not ThisExpressionSyntax)
            parentName = syntaxReader.GetObjectReferenceName(controlsAccess.Expression);

        childName = syntaxReader.GetObjectReferenceName(invocation.ArgumentList.Arguments[0].Expression) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(childName)
            && (controlsAccess.Expression is ThisExpressionSyntax || !string.IsNullOrWhiteSpace(parentName));
    }

    private static bool TryReadPropertyAccess(
        ExpressionSyntax expression,
        out string? ownerName,
        out string propertyPath)
    {
        ownerName = null;
        propertyPath = string.Empty;

        if (!TryReadAccessParts(expression, out var startsWithThis, out var parts)
            || parts.Count == 0)
        {
            return false;
        }

        if (startsWithThis && parts.Count == 1)
        {
            propertyPath = parts[0];
            return true;
        }

        if (parts.Count < 2)
            return false;

        ownerName = parts[0];
        propertyPath = string.Join(".", parts.Skip(1));
        return !string.IsNullOrWhiteSpace(propertyPath);
    }

    private static bool TryReadAccessParts(
        ExpressionSyntax expression,
        out bool startsWithThis,
        out List<string> parts)
    {
        startsWithThis = false;
        parts = [];

        switch (expression)
        {
            case ThisExpressionSyntax:
                startsWithThis = true;
                return true;

            case IdentifierNameSyntax identifier:
                parts.Add(identifier.Identifier.ValueText);
                return true;

            case MemberAccessExpressionSyntax memberAccess:
                if (!TryReadAccessParts(memberAccess.Expression, out startsWithThis, out parts))
                    return false;

                parts.Add(memberAccess.Name.Identifier.ValueText);
                return true;

            default:
                return false;
        }
    }

    private static bool TryReadObjectCreationType(ExpressionSyntax? expression, out string typeName)
    {
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            typeName = objectCreation.Type.ToString();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    private static bool TryReadString(ExpressionSyntax expression, out string? value)
    {
        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            value = null;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadBoolean(ExpressionSyntax expression, out bool value)
    {
        if (expression.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            value = true;
            return true;
        }

        if (expression.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryReadInt32(ExpressionSyntax expression, out int value)
    {
        if (expression is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.UnaryMinusExpression)
            && TryReadInt32(unary.Operand, out var operand))
        {
            value = -operand;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal
            && literal.Token.Value is int intValue)
        {
            value = intValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadDouble(ExpressionSyntax expression, out double value)
    {
        if (expression is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.UnaryMinusExpression)
            && TryReadDouble(unary.Operand, out var operand))
        {
            value = -operand;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal
            && literal.Token.Value is IConvertible convertible)
        {
            value = convertible.ToDouble(CultureInfo.InvariantCulture);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadEnum(ExpressionSyntax expression, out string enumTypeName, out string memberName)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            enumTypeName = memberAccess.Expression.ToString();
            memberName = memberAccess.Name.Identifier.ValueText;
            return !string.IsNullOrWhiteSpace(enumTypeName)
                && !string.IsNullOrWhiteSpace(memberName);
        }

        enumTypeName = string.Empty;
        memberName = string.Empty;
        return false;
    }

    private static bool TryReadDesignerValue(ExpressionSyntax expression, out DesignPropertyValue value)
    {
        if (TryReadString(expression, out var stringValue))
        {
            value = stringValue is null
                ? DesignPropertyValue.FromNull()
                : DesignPropertyValue.FromString(stringValue);
            return true;
        }

        if (TryReadBoolean(expression, out var boolValue))
        {
            value = DesignPropertyValue.FromBoolean(boolValue);
            return true;
        }

        if (TryReadInt32(expression, out var intValue))
        {
            value = DesignPropertyValue.FromInt32(intValue);
            return true;
        }

        if (TryReadDouble(expression, out var doubleValue))
        {
            value = DesignPropertyValue.FromDouble(doubleValue);
            return true;
        }

        if (TryReadEnum(expression, out var enumTypeName, out var memberName))
        {
            value = DesignPropertyValue.FromEnum(enumTypeName, memberName);
            return true;
        }

        if (TryReadStructuredObject(expression, out value!))
            return true;

        value = null!;
        return false;
    }

    private static bool TryReadBounds(ExpressionSyntax expression, out DesignBounds bounds)
    {
        if (TryReadObjectArguments(expression, 4, out var arguments))
        {
            bounds = new DesignBounds(arguments[0], arguments[1], arguments[2], arguments[3]);
            return true;
        }

        bounds = default;
        return false;
    }

    private static bool TryReadPoint(ExpressionSyntax expression, out int x, out int y)
    {
        if (TryReadObjectArguments(expression, 2, out var arguments))
        {
            x = arguments[0];
            y = arguments[1];
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool TryReadSize(ExpressionSyntax expression, out int width, out int height)
    {
        if (TryReadObjectArguments(expression, 2, out var arguments))
        {
            width = arguments[0];
            height = arguments[1];
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool TryReadObjectArguments(ExpressionSyntax expression, int count, out int[] values)
    {
        values = [];

        if (expression is not ObjectCreationExpressionSyntax objectCreation
            || objectCreation.ArgumentList?.Arguments.Count != count)
        {
            return false;
        }

        var arguments = new int[count];
        for (var index = 0; index < count; index++)
        {
            if (!TryReadInt32(objectCreation.ArgumentList.Arguments[index].Expression, out arguments[index]))
                return false;
        }

        values = arguments;
        return true;
    }

    private static bool TryReadStructuredObject(ExpressionSyntax expression, out DesignPropertyValue value)
    {
        value = null!;

        if (expression is not ObjectCreationExpressionSyntax objectCreation
            || objectCreation.ArgumentList is null)
        {
            return false;
        }

        var typeName = objectCreation.Type.ToString();
        var propertyValues = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);

        if (IsType(typeName, "Size") && TryReadObjectArguments(expression, 2, out var size))
        {
            propertyValues["Width"] = DesignPropertyValue.FromInt32(size[0]);
            propertyValues["Height"] = DesignPropertyValue.FromInt32(size[1]);
            value = DesignPropertyValue.FromStructuredObject(typeName, propertyValues);
            return true;
        }

        if (IsType(typeName, "Point") && TryReadObjectArguments(expression, 2, out var point))
        {
            propertyValues["X"] = DesignPropertyValue.FromInt32(point[0]);
            propertyValues["Y"] = DesignPropertyValue.FromInt32(point[1]);
            value = DesignPropertyValue.FromStructuredObject(typeName, propertyValues);
            return true;
        }

        if ((IsType(typeName, "Rectangle") || IsType(typeName, "Bounds"))
            && TryReadObjectArguments(expression, 4, out var rectangle))
        {
            propertyValues["X"] = DesignPropertyValue.FromInt32(rectangle[0]);
            propertyValues["Y"] = DesignPropertyValue.FromInt32(rectangle[1]);
            propertyValues["Width"] = DesignPropertyValue.FromInt32(rectangle[2]);
            propertyValues["Height"] = DesignPropertyValue.FromInt32(rectangle[3]);
            value = DesignPropertyValue.FromStructuredObject(typeName, propertyValues);
            return true;
        }

        if (IsType(typeName, "SKColor")
            && objectCreation.ArgumentList.Arguments.Count is 3 or 4)
        {
            var arguments = objectCreation.ArgumentList.Arguments;
            if (!TryReadInt32(arguments[0].Expression, out var r)
                || !TryReadInt32(arguments[1].Expression, out var g)
                || !TryReadInt32(arguments[2].Expression, out var b))
            {
                return false;
            }

            var a = 255;
            if (arguments.Count == 4
                && !TryReadInt32(arguments[3].Expression, out a))
            {
                return false;
            }

            propertyValues["R"] = DesignPropertyValue.FromInt32(r);
            propertyValues["G"] = DesignPropertyValue.FromInt32(g);
            propertyValues["B"] = DesignPropertyValue.FromInt32(b);
            propertyValues["A"] = DesignPropertyValue.FromInt32(a);
            value = DesignPropertyValue.FromStructuredObject(typeName, propertyValues);
            return true;
        }

        return false;
    }

    private static bool IsType(string typeName, string expectedSuffix)
        => string.Equals(typeName, expectedSuffix, StringComparison.Ordinal)
        || typeName.EndsWith("." + expectedSuffix, StringComparison.Ordinal)
        || typeName.EndsWith(expectedSuffix, StringComparison.Ordinal);

    private void AddUnsupported(SyntaxNode syntax, string message)
        => AddDiagnostic(CSharpDesignerDiagnosticSeverity.Warning, message, syntax, syntax.ToString());

    private void AddDiagnostic(
        CSharpDesignerDiagnosticSeverity severity,
        string message,
        SyntaxNode? syntax,
        string? unsupportedSyntax = null)
    {
        int? line = null;
        int? column = null;

        if (syntax is not null)
        {
            var position = syntax.GetLocation().GetLineSpan().StartLinePosition;
            line = position.Line + 1;
            column = position.Character + 1;
        }

        diagnostics.Add(new CSharpDesignerDiagnostic(severity, message, line, column, unsupportedSyntax));
    }

    private CSharpDesignerParseResult CreateResult(
        DesignDocument? document,
        CSharpDesignerParseOptions options)
    {
        var resultDiagnostics = options.TreatWarningsAsErrors
            ? diagnostics
                .Select(diagnostic => diagnostic.Severity == CSharpDesignerDiagnosticSeverity.Warning
                    ? new CSharpDesignerDiagnostic(
                        CSharpDesignerDiagnosticSeverity.Error,
                        diagnostic.Message,
                        diagnostic.Line,
                        diagnostic.Column,
                        diagnostic.Syntax)
                    : diagnostic)
                .ToArray()
            : diagnostics.ToArray();

        return new CSharpDesignerParseResult(document, resultDiagnostics);
    }

    private readonly record struct FieldDefinition(
        string TypeName,
        DesignerMemberVisibility Visibility);

    private readonly record struct ControlAddOperation(
        string? ParentName,
        string ChildName,
        SyntaxNode Syntax);
}

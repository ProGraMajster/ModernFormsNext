using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

// Plans source changes without writing, compiling to an assembly or executing project code.
// Both disk and live-buffer hosts use the same semantic checks and UTF-16 insertion coordinates.
internal static class DesignerEventSource
{
    internal const int MaximumSourceCharacters = 512 * 1024;
    internal const int MaximumProjectCharacters = 8 * 1024 * 1024;
    internal const int MaximumProjectFiles = 512;
    internal const int MaximumSyntaxDepth = 128;
    internal const int MaximumMembers = 2048;

    internal static bool TryParse(string text, string path, out SyntaxTree tree, out string? error, CancellationToken cancellationToken = default)
    {
        tree = null!;
        error = null;
        if (text.Length > MaximumSourceCharacters)
        { error = "Event source exceeds the 512 Ki UTF-16 character safety limit; edit it in code."; return false; }
        if (!WithinSyntaxBudget(text, out error, cancellationToken)) return false;
        tree = CSharpSyntaxTree.ParseText(text, path: path, cancellationToken: cancellationToken);
        if (tree.GetDiagnostics(cancellationToken).Any(d => d.Severity == DiagnosticSeverity.Error))
        { error = "Fix C# syntax errors before editing Designer event handlers."; return false; }
        return true;
    }

    private static bool WithinSyntaxBudget(string text, out string? error, CancellationToken cancellationToken = default)
    {
        error = null;
        // Bound nesting before the recursive parser sees the input. Tokens inside strings/comments
        // do not contribute. The token count is also bounded by the source length.
        var depth = 0;
        var angleDepth = 0;
        var tokenRun = 0;
        foreach (var token in SyntaxFactory.ParseTokens(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++tokenRun > 1024)
            { error = "Event source expression complexity exceeds its safety budget; edit it in code."; return false; }
            if (token.Kind() is SyntaxKind.SemicolonToken or SyntaxKind.OpenBraceToken or SyntaxKind.CloseBraceToken)
            {
                tokenRun = 0;
                angleDepth = 0;
            }
            if (token.Kind() is SyntaxKind.OpenBraceToken or SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken)
            {
                if (++depth > MaximumSyntaxDepth)
                { error = "Event source nesting exceeds the supported safety limit; edit it in code."; return false; }
            }
            else if (token.Kind() is SyntaxKind.CloseBraceToken or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken)
                depth = Math.Max(0, depth - 1);
            else if (token.IsKind(SyntaxKind.LessThanToken) && ++angleDepth > MaximumSyntaxDepth)
            { error = "Event source type/expression nesting exceeds its safety budget; edit it in code."; return false; }
            else if (token.IsKind(SyntaxKind.GreaterThanToken)) angleDepth = Math.Max(0, angleDepth - 1);
            else if (token.IsKind(SyntaxKind.GreaterThanGreaterThanToken)) angleDepth = Math.Max(0, angleDepth - 2);
        }
        return true;
    }

    internal static DesignerEventSourcePlan Plan(DesignDocument document, string path, string text,
        string handlerName, string parameters, string? projectPath, bool create, CSharpCompilation? sourceCompilation = null,
        CancellationToken cancellationToken = default)
    {
        if (handlerName.Length > 256 || !DesignDocumentValidator.IsValidCSharpIdentifier(handlerName))
            return DesignerEventSourcePlan.Failed("The handler name must be a valid C# identifier of at most 256 characters.");
        if (!TryReadParameters(parameters, out var parameterList))
            return DesignerEventSourcePlan.Failed("This event signature is unsupported; edit the handler in code.");
        if (!TryParse(text, path, out var tree, out var error, cancellationToken))
            return DesignerEventSourcePlan.Failed(error!);
        var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == document.ClassName.TrimStart('@')
                && !c.Ancestors().OfType<TypeDeclarationSyntax>().Any()
                && ReadNamespace(c) == NormalizeNamespace(document.Namespace)).ToArray();
        if (classes.Length != 1 || classes[0].TypeParameterList is not null)
            return DesignerEventSourcePlan.Failed("The paired source must contain exactly one matching, non-generic root class and namespace.");
        var target = classes[0];
        if (target.Members.Count > MaximumMembers || target.ContainsDirectives)
            return DesignerEventSourcePlan.Failed("The root class is too complex or contains conditional directives; edit handlers in code.");
        try
        {
            var compilation = sourceCompilation is null
                ? DesignerProjectUserControlDiscovery.CreateEventCompilation(tree, projectPath, cancellationToken)
                : sourceCompilation.ReplaceSyntaxTree(sourceCompilation.SyntaxTrees.Single(t => t.FilePath == path), tree);
            var symbol = compilation.GetSemanticModel(tree).GetDeclaredSymbol(target, cancellationToken);
            if (symbol is null || symbol.GetMembers().Length > MaximumMembers)
                return DesignerEventSourcePlan.Failed("The root class could not be resolved within the event source budget.");
            var named = symbol.GetMembers(handlerName.TrimStart('@'));
            if (named.Length != 0)
            {
                // Probe the same instance method-group conversion emitted by the canonical
                // generator. Roslyn handles aliases, overloads and reference variance; name or
                // parameter-count comparisons alone incorrectly accept incompatible handlers.
                var probeName = "__mfEventCheck";
                while (symbol.GetMembers(probeName).Length != 0) probeName += "_";
                var delegateName = probeName + "Delegate";
                while (symbol.GetMembers(delegateName).Length != 0) delegateName += "_";
                var probe = SyntaxFactory.ParseMemberDeclaration(
                    $"private {delegateName} {probeName}() => this.{handlerName};")!;
                var expected = SyntaxFactory.ParseMemberDeclaration(
                    $"private delegate void {delegateName}{parameterList};")!;
                var updatedTarget = target.AddMembers(expected, probe);
                var probeTree = tree.WithRootAndOptions(tree.GetRoot().ReplaceNode(target, updatedTarget), tree.Options);
                var probeCompilation = compilation.ReplaceSyntaxTree(tree, probeTree);
                var probeRoot = probeTree.GetRoot();
                var insertedProbe = probeRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Single(m => m.Identifier.ValueText == probeName);
                var insertedDelegate = probeRoot.DescendantNodes().OfType<DelegateDeclarationSyntax>()
                    .Single(n => n.Identifier.ValueText == delegateName);
                var model = probeCompilation.GetSemanticModel(probeTree);
                var conversion = model.GetSymbolInfo(
                    ((MemberAccessExpressionSyntax)insertedProbe.ExpressionBody!.Expression), cancellationToken);
                var method = conversion.Symbol as IMethodSymbol;
                if (method is null || method.IsStatic || method.IsGenericMethod
                    || model.GetDiagnostics(insertedProbe.Span, cancellationToken).Concat(model.GetDiagnostics(insertedDelegate.Span, cancellationToken))
                        .Any(d => d.Severity == DiagnosticSeverity.Error))
                    return DesignerEventSourcePlan.Failed($"Handler '{handlerName}' exists but is incompatible or ambiguous for this event.");
                var declaration = method.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellationToken))
                    .OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.SyntaxTree == probeTree
                        && (m.Body is not null || m.ExpressionBody is not null));
                if (declaration is null)
                    return DesignerEventSourcePlan.Failed($"Handler '{handlerName}' is outside the paired editable source or has no method body. Edit it in code.");
                var location = declaration.Body?.OpenBraceToken.Span.End ?? declaration.ExpressionBody!.Expression.SpanStart;
                return new(true, string.Empty, location, string.Empty, $"Event handler {handlerName} already exists.")
                { NavigationOffset = location };
            }
            if (!create)
                return DesignerEventSourcePlan.Failed($"Handler '{handlerName}' is missing or was renamed. Reassign or clear the event binding; user methods are preserved.");
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var close = target.CloseBraceToken.SpanStart;
            var lineStart = text.LastIndexOf('\n', Math.Max(0, close - 1)) + 1;
            var prefix = text[lineStart..close];
            var indentation = prefix.All(c => c is ' ' or '\t') ? prefix : string.Empty;
            var memberIndent = indentation + "    ";
            var insertion = $"{newline}{memberIndent}private void {handlerName}{parameterList}{newline}"
                + $"{memberIndent}{{{newline}{memberIndent}    // TODO: Add event handling logic.{newline}"
                + $"{memberIndent}}}{newline}{indentation}";
            if (insertion.Length > 16 * 1024)
                return DesignerEventSourcePlan.Failed("The handler insertion exceeds the 16 Ki character safety budget.");
            var updatedText = text.Insert(close, insertion);
            if (updatedText.Length > MaximumSourceCharacters)
                return DesignerEventSourcePlan.Failed("The handler would exceed the event source safety budget.");
            // Revalidate the new method against its own signature before allowing any writer.
            var validation = Plan(document, path, updatedText, handlerName, parameters, projectPath, create: false, cancellationToken: cancellationToken);
            return validation.Succeeded
                ? new(true, insertion, close, updatedText, $"Added event handler {handlerName}.") { NavigationOffset = validation.Offset }
                : validation;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or System.Xml.XmlException)
        { return DesignerEventSourcePlan.Failed($"Cannot inspect event source: {ex.Message}"); }
    }

    internal static bool TryReadParameters(string parameters, out ParameterListSyntax list)
    {
        list = null!;
        if (parameters.Length > 8192 || !WithinSyntaxBudget(parameters, out _)) return false;
        list = SyntaxFactory.ParseParameterList("(" + parameters + ")");
        return list.Parameters.Count <= 32 && !list.ContainsDiagnostics && !list.ContainsDirectives
            && list.Parameters.All(p => p.Type is not null && p.Default is null && p.AttributeLists.Count == 0
                && p.Modifiers.Count == 0 && p.Type is not PointerTypeSyntax and not FunctionPointerTypeSyntax
                && p.Identifier.IsKind(SyntaxKind.IdentifierToken) && !p.Identifier.IsMissing);
    }

    internal static IReadOnlyList<string> CompatibleMethods(DesignDocument document, string path, string source,
        string parameters, string? projectPath, CancellationToken cancellationToken = default)
    {
        if (!TryParse(source, path, out var tree, out var error, cancellationToken)) throw new InvalidDataException(error);
        var compilation = DesignerProjectUserControlDiscovery.CreateEventCompilation(tree, projectPath, cancellationToken);
        var names = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == document.ClassName.TrimStart('@') && ReadNamespace(c) == NormalizeNamespace(document.Namespace)
                && !c.Ancestors().OfType<TypeDeclarationSyntax>().Any())
            .SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>()).Select(m =>
                SyntaxFacts.GetKeywordKind(m.Identifier.ValueText) == SyntaxKind.None ? m.Identifier.ValueText : "@" + m.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length > 128) throw new InvalidDataException("More than 128 method names in the paired class; assign handlers by name in code.");
        return names.Where(name => Plan(document, path, source, name, parameters, projectPath, false, compilation, cancellationToken).Succeeded)
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static string ReadNamespace(ClassDeclarationSyntax target)
        => string.Join(".", target.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
            .SelectMany(n => n.Name.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText)));

    private static string NormalizeNamespace(string value) => string.Join(".", value.Split('.').Select(p => p.TrimStart('@')));
}

internal sealed record DesignerEventSourcePlan(bool Succeeded, string Insertion, int Offset, string UpdatedText, string Message)
{
    internal int NavigationOffset { get; init; }
    internal static DesignerEventSourcePlan Failed(string message) => new(false, string.Empty, 0, string.Empty, message);
}

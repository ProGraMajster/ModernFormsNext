using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModernFormsNext.Designer.Services;

// An exact previous generation can be replaced as a whole. For edited files the only
// region this writer owns is the verified InitializeComponent body. All other source is
// copied from the stable read, including comments, members, directives and formatting.
internal static class DesignerGeneratedSourceRewrite
{
    internal static bool TryPreserve(string existing, string baseline, string generated,
        bool reviewed, out string result)
    {
        result = string.Empty;
        if (existing.Length > 4 * 1024 * 1024 ||
            FindRegion(existing) is not { } old || FindRegion(baseline) is not { } expected ||
            FindRegion(generated) is not { } next)
            return false;

        if (old.Type != expected.Type || old.Type != next.Type || old.Body.ContainsDirectives ||
            !reviewed && !SyntaxFactory.AreEquivalent(old.Body, expected.Body) ||
            !expected.Fields.SequenceEqual(next.Fields, StringComparer.Ordinal))
            return false;
        var fieldCounts = old.Fields.CountBy(field => field, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (!expected.Fields.All(field => fieldCounts.GetValueOrDefault(field) == 1)) return false;

        result = string.Concat(existing.AsSpan(0, old.Body.SpanStart), next.Body.ToString(),
            existing.AsSpan(old.Body.Span.End));
        return true;
    }

    private static Region? FindRegion(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        if (root.ContainsDiagnostics) return null;
        var candidates = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == "InitializeComponent")
                .Select(method => (Type: type, Method: method)))
            .Take(2).ToArray();
        if (candidates is not [{ Type: var type, Method: { Body: { } body, ParameterList.Parameters.Count: 0 } }] ||
            type.Parent is TypeDeclarationSyntax)
            return null;
        var typeName = string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse().Select(ns => ns.Name.ToString()).Append(type.Identifier.ValueText));
        var fields = type.Members.OfType<FieldDeclarationSyntax>()
            .Select(field => field.WithoutTrivia().NormalizeWhitespace().ToFullString())
            .Order(StringComparer.Ordinal).ToArray();
        return new Region(typeName, body, fields);
    }

    private sealed record Region(string Type, BlockSyntax Body, string[] Fields);
}

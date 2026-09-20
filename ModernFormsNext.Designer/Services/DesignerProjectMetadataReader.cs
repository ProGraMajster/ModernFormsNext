using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

internal static partial class DesignerProjectUserControlDiscovery
{
    // Roslyn reads PE/source data only. In particular, do not replace this with Assembly.Load,
    // MetadataLoadContext + instantiated attributes, TypeDescriptor or reflection over user types.
    internal static DesignerCustomControlCatalog DiscoverCatalog(string? projectPath)
    {
        var diagnostics = new List<string>();
        var directory = GetProjectDirectory(projectPath);
        if (directory is null)
            return new([], diagnostics);

        var candidates = DiscoverSourceCandidates(projectPath).ToDictionary(c => c.FullName, StringComparer.Ordinal);
        try
        {
            var trees = new List<SyntaxTree>();
            foreach (var path in EnumerateProjectFiles(directory, "*.cs"))
            {
                try { trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { diagnostics.Add($"Cannot read source '{path}': {exception.Message}"); }
            }

            // SDK implicit usings are source semantics, not a reason to evaluate arbitrary project
            // targets. Unresolved project-specific globals remain diagnostics instead of execution.
            trees.Add(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic;"));
            var references = new List<MetadataReference>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var trustedPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(IOPath.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Concat([typeof(Control).Assembly.Location, typeof(DesignDocument).Assembly.Location]);
            foreach (var path in trustedPaths)
                if (paths.Add(path)) references.Add(MetadataReference.CreateFromFile(path));

            var customReferences = new List<PortableExecutableReference>();
            foreach (var path in ReadReferencePaths(projectPath, directory, diagnostics))
            {
                if (!paths.Add(path)) continue;
                try
                {
                    // Copy bytes, then release the file before publishing a catalog. Rebuilds can
                    // replace DLLs; refresh never reuses an assembly loaded into the Designer process.
                    var reference = MetadataReference.CreateFromImage(File.ReadAllBytes(path), filePath: path);
                    references.Add(reference);
                    customReferences.Add(reference);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
                { diagnostics.Add($"Cannot inspect reference '{path}': {exception.Message} Rebuild/restore, then Refresh Toolbox."); }
            }

            var compilation = CSharpCompilation.Create("DesignerMetadata", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var frameworkControl = compilation.GetTypeByMetadataName("ModernFormsNext.Control");
            var assemblies = new List<(IAssemblySymbol Assembly, string Path)> { (compilation.Assembly, string.Empty) };
            foreach (var reference in customReferences)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    var incompatible = assembly.Modules.SelectMany(module => module.ReferencedAssemblies)
                        .FirstOrDefault(identity => identity.Name == frameworkControl?.ContainingAssembly.Name
                            && identity.Version > frameworkControl.ContainingAssembly.Identity.Version);
                    if (incompatible is not null)
                        diagnostics.Add($"Incompatible reference '{reference.FilePath}' requires {incompatible}; rebuild against this Designer's ModernFormsNext version.");
                    else
                        assemblies.Add((assembly, reference.FilePath!));
                }
                else
                    diagnostics.Add($"Incompatible managed reference '{reference.FilePath}'. Rebuild for the host's framework version.");
            }

            var ambiguousTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (assembly, path) in assemblies)
            foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
            {
                if (type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public && type.BaseType?.TypeKind == TypeKind.Error)
                    diagnostics.Add($"'{type}' has an unresolved base type. Restore missing dependencies and Refresh Toolbox; no runtime probing is attempted.");
                if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.Arity != 0
                    || type.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public || type.ContainingType is not null
                    || !Inherits(type, frameworkControl)) continue;

                var name = type.ToDisplayString();
                if (ambiguousTypes.Contains(name)) continue;
                if (!name.Split('.').All(DesignDocumentValidator.IsValidCSharpIdentifier))
                {
                    candidates.Remove(name);
                    diagnostics.Add($"'{name}' requires unsupported C# type-name syntax. Existing nodes remain placeholders.");
                    continue;
                }
                if (!type.InstanceConstructors.Any(c => c.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public && c.Parameters.Length == 0))
                {
                    candidates.Remove(name);
                    diagnostics.Add($"'{name}' needs a public parameterless constructor for generated initialization. Existing nodes remain placeholders.");
                    continue;
                }
                if (path.Length > 0 && candidates.ContainsKey(name))
                {
                    if (candidates[name].SourceFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add($"Ignored duplicate binary type '{name}' from '{path}'; current source takes precedence.");
                    else
                    {
                        candidates.Remove(name);
                        ambiguousTypes.Add(name);
                        diagnostics.Add($"Ambiguous binary type '{name}' occurs in multiple references. Remove the conflicting reference and Refresh Toolbox; existing nodes remain placeholders.");
                    }
                    continue;
                }

                var controlMetadata = Attribute(type, "ModernFormsNext.Designing.DesignableControlAttribute");
                var properties = new List<DesignerCustomProperty>();
                var events = new List<DesignerCustomEvent>();
                var hiddenProperties = new List<string>();
                var hiddenEvents = new List<string>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                for (var current = type; current is not null && visited.Add(current) && !SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, frameworkControl?.ContainingAssembly); current = current.BaseType)
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic || member.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public || !names.Add(member.Name)) continue;
                    if (!DesignDocumentValidator.IsValidCSharpIdentifier(member.Name)) continue;
                    if (member is IPropertySymbol property && !property.IsIndexer)
                    {
                        var metadata = ReadProperty(property, diagnostics);
                        if (metadata is not null) properties.Add(metadata);
                        else hiddenProperties.Add(property.Name);
                    }
                    else if (member is IEventSymbol eventSymbol)
                    {
                        var metadata = ReadEvent(eventSymbol, diagnostics);
                        if (metadata is not null) events.Add(metadata);
                        else hiddenEvents.Add(eventSymbol.Name);
                    }
                }
                // Keep the syntax discovery's base-declaring file for partial UserControls. A
                // generated partial may be enumerated first but must not change .mfdesign identity.
                var sourcePath = candidates.TryGetValue(name, out var sourceCandidate)
                    ? sourceCandidate.SourceFilePath
                    : type.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath ?? directory;
                candidates[name] = new(type.Name, name, path.Length == 0 ? sourcePath : path)
                {
                    DisplayName = Argument(controlMetadata, 0) as string ?? Text(type, controlMetadata, "DisplayName") ?? type.Name,
                    Category = Text(type, controlMetadata, "Category") ?? (path.Length == 0 ? "My Project" : "References"),
                    Description = Text(type, controlMetadata, "Description"),
                    FrameworkBaseTypeName = FindFrameworkBase(type, frameworkControl)?.ToDisplayString() ?? "ModernFormsNext.Control",
                    VisibleInToolbox = Named(controlMetadata, "VisibleInToolbox") as bool?
                        ?? Argument(Attribute(type, "System.ComponentModel.BrowsableAttribute"), 0) as bool? ?? true,
                    Properties = properties.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray(),
                    Events = events.OrderBy(e => e.Name, StringComparer.Ordinal).ToArray(),
                    HiddenPropertyNames = hiddenProperties,
                    HiddenEventNames = hiddenEvents
                };
            }

            foreach (var diagnostic in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(10))
                diagnostics.Add($"Source/reference metadata is incomplete: {diagnostic}. Restore/rebuild and Refresh Toolbox; unresolved members remain unavailable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
        { diagnostics.Add($"Metadata refresh incomplete: {exception.Message} Existing document values are preserved; rebuild/restore and Refresh Toolbox."); }

        return new(candidates.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.FullName, StringComparer.Ordinal).ToArray(), diagnostics);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol scope)
    {
        foreach (var type in scope.GetTypeMembers()) yield return type;
        foreach (var child in scope.GetNamespaceMembers())
            foreach (var type in EnumerateTypes(child)) yield return type;
    }

    private static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol? frameworkControl)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type.BaseType; current is not null && seen.Add(current); current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, frameworkControl)) return true;
        return false;
    }

    private static INamedTypeSymbol? FindFrameworkBase(INamedTypeSymbol type, INamedTypeSymbol? frameworkControl)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type.BaseType; current is not null && seen.Add(current); current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, frameworkControl?.ContainingAssembly)) return current;
        return null;
    }

    private static DesignerCustomProperty? ReadProperty(IPropertySymbol property, List<string> diagnostics)
    {
        var designable = Attribute(property, "ModernFormsNext.Designing.DesignablePropertyAttribute");
        if (Attribute(property, "ModernFormsNext.Designing.DesignerHiddenAttribute") is not null
            || Named(designable, "Visible") is false || Named(designable, "Visibility") is 0
            || designable is null && Argument(Attribute(property, "System.ComponentModel.BrowsableAttribute"), 0) is false) return null;

        var type = property.Type;
        var nullable = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        if (nullable) type = ((INamedTypeSymbol)type).TypeArguments[0];
        Type? editor = type.SpecialType switch
        {
            SpecialType.System_String => typeof(string), SpecialType.System_Boolean => typeof(bool),
            // The canonical primitive document model emits Double literals. A Single editor
            // would lose its suffix on round-trip; keep it read-only until that model supports it.
            SpecialType.System_Int32 => typeof(int),
            SpecialType.System_Double => typeof(double), _ => null
        };
        var enumType = type.TypeKind == TypeKind.Enum && type.ToDisplayString().Split('.').All(DesignDocumentValidator.IsValidCSharpIdentifier)
            ? type.ToDisplayString() : null;
        var enumFields = type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue && DesignDocumentValidator.IsValidCSharpIdentifier(f.Name)).ToArray();
        var members = enumType is null ? null : enumFields.Select(f => f.Name).ToArray();
        var readOnly = property.SetMethod?.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public || property.SetMethod.IsInitOnly
            || property.GetMethod?.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public
            || Named(designable, "ReadOnly") is true || Named(designable, "Serialize") is false
            || designable is null && (Argument(Attribute(property, "System.ComponentModel.ReadOnlyAttribute"), 0) is true
                || Argument(Attribute(property, "System.ComponentModel.DesignerSerializationVisibilityAttribute"), 0) is 0)
            || editor is null && enumType is null;
        if (editor is null && enumType is null)
            diagnostics.Add($"'{property.ContainingType}.{property.Name}' has unsupported type '{type}': read-only placeholder; no getter or converter is executed.");
        if (Attribute(property, "System.ComponentModel.TypeConverterAttribute") is not null
            || Attribute(type, "System.ComponentModel.TypeConverterAttribute") is not null)
            diagnostics.Add($"'{property.ContainingType}.{property.Name}': TypeConverter code is not executed. Only the built-in primitive/enum editor is available.");

        DesignPropertyValue? defaultValue = null;
        var defaultAttribute = Attribute(property, "System.ComponentModel.DefaultValueAttribute");
        if (defaultAttribute is not null)
        {
            var constant = Argument(defaultAttribute, 0);
            if (defaultAttribute.ConstructorArguments.Length == 1)
            {
                if (enumType is not null && enumFields.FirstOrDefault(f => Equals(f.ConstantValue, constant)) is { } field)
                    defaultValue = DesignPropertyValue.FromEnum(enumType, field.Name);
                else if (constant is null || constant is string or bool or int or float or double)
                    defaultValue = DesignPropertyValue.FromObject(constant);
            }
            // The (Type, string) overload normally invokes TypeDescriptor. Do not instantiate it.
            // Convert only through the same allow-listed editor as an explicit property edit.
            else if (defaultAttribute.ConstructorArguments.Length == 2 && Argument(defaultAttribute, 1) is string text)
            {
                var adapter = new DesignerCustomProperty(property.Name, property.Name, "Misc", "", editor, enumType, members, nullable, false, false, null);
                adapter.TryConvert(text, out defaultValue, out _);
            }
            if (defaultValue is null) diagnostics.Add($"'{property.ContainingType}.{property.Name}': unsupported DefaultValue metadata; no executable default is evaluated.");
        }
        return new(property.Name, Text(property, designable, "DisplayName") ?? property.Name,
            Text(property, designable, "Category") ?? "Misc",
            Text(property, designable, "Description") ?? "Safe source/binary metadata. Constructor and getter defaults are not evaluated.",
            editor, enumType, members, nullable, readOnly,
            Named(designable, "Visibility") is 1 || editor is null && enumType is null, defaultValue);
    }

    private static DesignerCustomEvent? ReadEvent(IEventSymbol member, List<string> diagnostics)
    {
        var metadata = Attribute(member, "ModernFormsNext.Designing.DesignableEventAttribute");
        if (Named(metadata, "Visible") is false
            || metadata is null && Argument(Attribute(member, "System.ComponentModel.BrowsableAttribute"), 0) is false) return null;
        var invoke = (member.Type as INamedTypeSymbol)?.DelegateInvokeMethod;
        var supported = invoke is { ReturnsVoid: true, ReturnsByRef: false }
            && invoke.Parameters.All(p => p.RefKind == RefKind.None && IsSafeParameterType(p.Type));
        if (!supported) diagnostics.Add($"'{member.ContainingType}.{member.Name}': unsupported delegate signature; edit/bind in source. No delegate is executed.");
        var parameters = supported ? string.Join(", ", invoke!.Parameters.Select((p, i) =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} arg{i}")) : null;
        return new(member.Name, Text(member, metadata, "DisplayName") ?? member.Name,
            Text(member, metadata, "Category") ?? "Action", Text(member, metadata, "Description") ?? "Safe delegate signature metadata.", parameters);
    }

    private static bool IsSafeParameterType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsSafeParameterType(array.ElementType),
        INamedTypeSymbol named => named.TypeKind != TypeKind.Error && !named.IsRefLikeType
            && named.TypeArguments.All(IsSafeParameterType),
        _ => false
    };

    private static AttributeData? Attribute(ISymbol symbol, string name)
    {
        // AttributeData exposes encoded constants only; it never calls attribute constructors.
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (ISymbol? current = symbol; current is not null && visited.Add(current); current = current switch
        {
            INamedTypeSymbol type => type.BaseType,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol eventSymbol => eventSymbol.OverriddenEvent,
            _ => null
        })
        {
            var attribute = current.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);
            if (attribute is not null) return attribute;
        }
        return null;
    }

    private static object? Argument(AttributeData? attribute, int index)
        => attribute is not null && attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value : null;
    private static object? Named(AttributeData? attribute, string name)
        => attribute?.NamedArguments.FirstOrDefault(p => p.Key == name).Value.Value;
    private static string? Text(ISymbol symbol, AttributeData? designable, string name)
        => Named(designable, name) as string ?? Argument(Attribute(symbol, $"System.ComponentModel.{name}Attribute"), 0) as string;
}

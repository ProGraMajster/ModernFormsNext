using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designing;
using ModernFormsNext.Designer.Surface;

namespace ModernFormsNext.Designer.Services;

// Composes companions into the one active design tree. All project types remain
// Roslyn symbols. No base constructor, property getter, attribute or event is run.
internal static class DesignerRootInheritance
{
    private const int MaximumLevels = 16;
    private const int MaximumInheritedNodes = 2048;

    internal static bool TryHydrate(DesignDocument document, string? projectPath, string? designPath,
        out string? diagnostic)
    {
        diagnostic = null;
        if (document.InheritanceState is not null) return true;
        try
        {
            var rootDirectory = DesignerProjectAssets.GetRoot(projectPath, designPath);
            if (rootDirectory is null)
            {
                if (document.BaseTypeName is not null)
                {
                    diagnostic = "A project context is required to resolve the inherited design.";
                    document.InheritanceError = diagnostic;
                }
                return document.BaseTypeName is null;
            }
            var compilation = DesignerProjectUserControlDiscovery.CreateEventCompilation(
                CSharpSyntaxTree.ParseText(string.Empty, path: IOPath.Combine(rootDirectory, ".inheritance-metadata.cs")),
                projectPath ?? rootDirectory);
            var identity = string.IsNullOrEmpty(document.Namespace) ? document.ClassName : $"{document.Namespace}.{document.ClassName}";
            var rootType = compilation.GetTypeByMetadataName(identity);
            if (rootType is null || rootType.DeclaringSyntaxReferences.Length == 0)
            {
                if (document.BaseTypeName is not null)
                {
                    diagnostic = $"Inherited design source '{identity}' is unavailable or ambiguous.";
                    document.InheritanceError = diagnostic;
                }
                return document.BaseTypeName is null;
            }

            var layers = new List<(INamedTypeSymbol Type, DesignDocument Document)>();
            var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var current = rootType.BaseType;
            DesignRootKind? kind = null;
            while (current is not null && visited.Add(current))
            {
                if (current.Arity != 0 || current.ContainingType is not null || current.TypeKind == TypeKind.Error)
                    throw new InvalidDataException("The inherited base must be a resolved, nongeneric top-level type.");
                if (SymbolEqualityComparer.Default.Equals(current, compilation.GetTypeByMetadataName("ModernFormsNext.Form")))
                { kind = DesignRootKind.Form; break; }
                if (SymbolEqualityComparer.Default.Equals(current, compilation.GetTypeByMetadataName("ModernFormsNext.UserControl")))
                { kind = DesignRootKind.UserControl; break; }
                if (layers.Count >= MaximumLevels)
                    throw new InvalidDataException("The inherited design exceeds its base-chain budget.");
                if (current.IsSealed)
                    throw new InvalidDataException($"Sealed type '{current}' cannot serve as an inherited design base.");
                if (!current.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0
                    && compilation.IsSymbolAccessibleWithin(constructor, rootType)))
                    throw new InvalidDataException($"Base '{current}' has no accessible parameterless constructor.");
                layers.Add((current, LoadCompanion(current, rootDirectory)));
                current = current.BaseType;
            }
            if (kind is null)
                throw new InvalidDataException("The source root does not inherit a compatible ModernFormsNext Form or UserControl.");
            if (layers.Count == 0)
            {
                if (document.BaseTypeName is not null || Enumerate(document.Controls).Any(node => node.IsInherited))
                    throw new InvalidDataException("The stored inherited design no longer matches the source base.");
                document.RootKind = kind.Value;
                return true;
            }
            var baseName = layers[0].Type.ToDisplayString();
            if (document.BaseTypeName is not null && Normalize(document.BaseTypeName) != baseName)
                throw new InvalidDataException("The stored custom base differs from the source declaration. Resolve the base change before saving.");

            var composed = new DesignDocument
            {
                RootKind = kind.Value, BaseTypeName = baseName,
                Namespace = document.Namespace, ClassName = document.ClassName, FormName = document.FormName
            };
            for (int index = layers.Count - 1; index >= 0; index--)
            {
                var layer = layers[index];
                DesignerSpecialContainers.NormalizeDocument(layer.Document);
                if (layer.Document.RootKind != kind.Value)
                    throw new InvalidDataException($"Companion for '{layer.Type}' has an incompatible root kind.");
                var expectedBase = index + 1 < layers.Count ? layers[index + 1].Type.ToDisplayString() : null;
                if (layer.Document.BaseTypeName is not null && Normalize(layer.Document.BaseTypeName) != expectedBase)
                    throw new InvalidDataException($"Companion for '{layer.Type}' does not describe its source base. Open and save the declaring design first.");
                if (index == layers.Count - 1 || layer.Document.BaseTypeName is null)
                {
                    composed.Size = layer.Document.Size;
                    composed.Properties["Name"] = DesignPropertyValue.FromString(layer.Document.FormName);
                    if (kind == DesignRootKind.Form)
                        composed.Properties["Text"] = DesignPropertyValue.FromString(layer.Document.FormName);
                }
                else if (layer.Document.InheritedSize is { } inheritedSize)
                    new DesignerLayoutEngine().ResizeRoot(composed, inheritedSize);
                CopyProperties(layer.Document.Properties, composed.Properties);
                MergeChildren(composed.Controls, layer.Document.Controls, layer.Type, rootType, compilation, markInherited: true);
                EnsureCompositionBudget(composed.Controls);
                RequireValidComposition(composed);
            }

            var baseSize = composed.Size;
            bool legacyRoot = document.BaseTypeName is null;
            new DesignerLayoutEngine().ResizeRoot(composed, legacyRoot ? document.Size : document.InheritedSize ?? baseSize);
            // Every subscription in the composed base layers is installed by a base
            // constructor. Only subscriptions authored in the current layer are emitted.
            foreach (var node in Enumerate(composed.Controls)) node.Events.Clear();
            Snapshot(composed.Controls, parent: null, rootType, compilation);
            var rootState = new DesignRootInheritanceState(baseName, document.Namespace, document.ClassName, kind.Value, baseSize,
                DesignInheritanceProjection.CloneProperties(composed.Properties),
                new SortedDictionary<string, string?>(StringComparer.Ordinal),
                composed.Controls.Select(node => node.Name).ToArray());
            CopyProperties(document.Properties, composed.Properties);
            if (legacyRoot)
            {
                composed.Properties["Name"] = DesignPropertyValue.FromString(document.FormName);
                if (kind == DesignRootKind.Form && !document.Properties.ContainsKey("Text"))
                    composed.Properties["Text"] = DesignPropertyValue.FromString(document.FormName);
            }
            MergeChildren(composed.Controls, document.Controls, rootType, rootType, compilation, markInherited: false);
            EnsureCompositionBudget(composed.Controls);
            RequireValidComposition(composed);

            // Publish only after all companion, ownership and override checks pass.
            document.RootKind = kind.Value;
            document.BaseTypeName = baseName;
            document.Size = legacyRoot ? document.Size : document.InheritedSize ?? rootState.Size;
            document.Properties = composed.Properties;
            var originalLocals = Enumerate(document.Controls).Where(node => !node.IsInherited)
                .ToDictionary(node => node.Name, StringComparer.Ordinal);
            RestoreLocalIdentities(composed.Controls, originalLocals);
            document.Controls = composed.Controls;
            document.InheritanceState = rootState;
            document.InheritanceError = null;
            var messages = new List<string>();
            if (layers.Any(layer => layer.Type.IsAbstract))
                messages.Add("Abstract base layout was resolved from metadata and design companions.");
            foreach (var node in Enumerate(document.Controls))
                if (node.IsInherited && node.IsInheritanceLocked
                    && node.MemberVisibility is DesignerMemberVisibility.Protected or DesignerMemberVisibility.Public or DesignerMemberVisibility.Internal)
                    messages.Add($"Inherited member '{node.Name}' is missing, inaccessible or incompatible with its declaring metadata and remains locked.");
            diagnostic = messages.Count == 0 ? null : string.Join(" ", messages);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or NotSupportedException
            or BadImageFormatException or OverflowException)
        {
            diagnostic = $"Inherited design unavailable: {exception.Message}";
            document.InheritanceError = diagnostic;
            return false;
        }
    }

    private static void RequireValidComposition(DesignDocument document)
    {
        var validation = new DesignDocumentValidator().ValidateStructure(document);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(" ", validation.Errors));
        DesignInheritanceProjection.RequireOwnershipOrder(document.Controls, parent: null);
    }

    private static void RestoreLocalIdentities(DesignControlCollection nodes, IReadOnlyDictionary<string, DesignControlNode> originals)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (!nodes[index].IsInherited && originals.TryGetValue(nodes[index].Name, out var original))
                nodes[index] = original;
            else
                RestoreLocalIdentities(nodes[index].Children, originals);
        }
    }

    private static DesignDocument LoadCompanion(INamedTypeSymbol type, string root)
    {
        var sourcePaths = type.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree.FilePath)
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        if (sourcePaths.Length == 1)
        {
            var path = IOPath.ChangeExtension(sourcePaths[0], ".mfdesign");
            try
            {
                var document = DesignerProjectDocumentReader.Load(root, path);
                RequireIdentity(document, type);
                return document;
            }
            catch (FileNotFoundException) { }
        }
        DesignDocument? match = null;
        int count = 0;
        long remaining = 16L * 1024 * 1024;
        foreach (var path in DesignerProjectUserControlDiscovery.EnumerateProjectFiles(root, "*.mfdesign", 8192))
        {
            long bytes = new FileInfo(path).Length;
            if (++count > 64 || (remaining -= bytes) < 0)
                throw new InvalidDataException("Inherited companion discovery exceeds its scan budget.");
            var candidate = DesignerProjectDocumentReader.Load(root, path);
            var name = string.IsNullOrEmpty(candidate.Namespace) ? candidate.ClassName : $"{candidate.Namespace}.{candidate.ClassName}";
            if (name != type.ToDisplayString()) continue;
            if (match is not null) throw new InvalidDataException($"Companion identity '{type}' is ambiguous.");
            match = candidate;
        }
        return match ?? throw new FileNotFoundException($"No project-owned .mfdesign companion exists for base '{type}'.");
    }

    private static void RequireIdentity(DesignDocument document, INamedTypeSymbol type)
    {
        var identity = string.IsNullOrEmpty(document.Namespace) ? document.ClassName : $"{document.Namespace}.{document.ClassName}";
        if (identity != type.ToDisplayString())
            throw new InvalidDataException($"The design companion does not declare '{type}'.");
    }

    private static void MergeChildren(DesignControlCollection target, IEnumerable<DesignControlNode> authored,
        INamedTypeSymbol owner, INamedTypeSymbol derived, CSharpCompilation compilation, bool markInherited,
        DesignControlNode? parent = null)
    {
        var inputs = authored.ToArray();
        foreach (var patch in inputs.Where(node => node.IsInherited))
        {
            var matches = target.Where(node => node.Name == patch.Name && Normalize(node.InheritedFrom!) == Normalize(patch.InheritedFrom!)).Take(2).ToArray();
            if (matches.Length != 1 || Normalize(matches[0].TypeName) != Normalize(patch.TypeName))
                throw new InvalidDataException($"Inherited member '{patch.Name}' is missing, renamed or incompatible.");
            var node = matches[0];
            if (!CanOverride(node, derived, compilation))
                throw new InvalidDataException($"Inherited member '{node.Name}' is not accessible for a derived override.");
            if (patch.InheritedBounds is { } bounds) node.Bounds = bounds;
            CopyProperties(patch.Properties, node.Properties);
            foreach (var binding in patch.Events) node.Events[binding.Key] = binding.Value;
            MergeChildren(node.Children, patch.Children, owner, derived, compilation, markInherited, node);
        }
        bool sequential = parent is not null && DesignInheritanceProjection.PreservesSequence(parent);
        var locals = inputs.Where(node => !node.IsInherited);
        foreach (var local in sequential ? locals : locals.Reverse())
        {
            if (Enumerate(local.Children).Any(child => child.IsInherited))
                throw new InvalidDataException("An inherited member cannot be reparented into a locally declared control.");
            if (Enumerate(target).Any(node => node.Name == local.Name))
                throw new InvalidDataException($"Local member '{local.Name}' conflicts with an inherited member.");
            // Local inputs are detached through bounded canonical data so failed
            // composition cannot mutate the caller's editable nodes.
            var clone = CloneNode(local);
            if (markInherited) Mark(clone, owner.ToDisplayString());
            if (sequential) target.Add(clone);
            else target.Insert(0, clone);
        }
    }

    private static void EnsureCompositionBudget(IEnumerable<DesignControlNode> nodes)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<(DesignControlNode Node, int Depth)>();
        foreach (var node in nodes) pending.Push((node, 0));
        int count = 0;
        while (pending.TryPop(out var item))
        {
            if (++count > MaximumInheritedNodes || item.Depth > DesignDocumentValidator.MaximumTreeDepth)
                throw new InvalidDataException("The inherited design exceeds its combined graph budget.");
            if (!names.Add(item.Node.Name))
                throw new InvalidDataException($"Member '{item.Node.Name}' is declared more than once across the inherited design.");
            foreach (var child in item.Node.Children) pending.Push((child, item.Depth + 1));
        }
    }

    private static DesignControlNode CloneNode(DesignControlNode node)
        => new()
        {
            Name = node.Name, TypeName = node.TypeName, Bounds = node.Bounds,
            MemberVisibility = node.MemberVisibility,
            Properties = new(node.Properties, StringComparer.Ordinal),
            Events = new(node.Events, StringComparer.Ordinal),
            Children = [.. node.Children.Select(CloneNode)]
        };

    private static void Mark(DesignControlNode node, string declaringType)
    {
        node.InheritedFrom = declaringType;
        node.Events.Clear(); // Base subscriptions are installed by the base InitializeComponent.
        foreach (var child in node.Children) Mark(child, declaringType);
    }

    private static void Snapshot(IEnumerable<DesignControlNode> nodes, string? parent,
        INamedTypeSymbol derived, CSharpCompilation compilation)
    {
        foreach (var node in nodes)
        {
            node.InheritanceState = new(node.InheritedFrom!, node.Name, node.TypeName, parent, node.Bounds,
                node.MemberVisibility, CanOverride(node, derived, compilation),
                DesignInheritanceProjection.CloneProperties(node.Properties),
                new SortedDictionary<string, string?>(StringComparer.Ordinal),
                node.Children.Select(child => child.Name).ToArray());
            Snapshot(node.Children, node.Name, derived, compilation);
        }
    }

    private static bool CanOverride(DesignControlNode node, INamedTypeSymbol derived, CSharpCompilation compilation)
    {
        if (node.MemberVisibility is DesignerMemberVisibility.Private or DesignerMemberVisibility.None)
            return false;
        var declaring = compilation.GetTypeByMetadataName(node.InheritedFrom!);
        // Generated references use this.member. A nearer handwritten declaration
        // must not silently redirect an override to a different control.
        for (var current = derived; current is not null && !SymbolEqualityComparer.Default.Equals(current, declaring); current = current.BaseType)
            if (current.GetMembers(node.Name).Length != 0) return false;
        var members = declaring?.GetMembers(node.Name).Where(member => !member.IsStatic
            && member is IFieldSymbol or IPropertySymbol).ToArray();
        if (members is not { Length: 1 } || !compilation.IsSymbolAccessibleWithin(members[0], derived))
            return false;
        if (members[0] is IPropertySymbol reference && (reference.IsIndexer || reference.GetMethod is null
            || !compilation.IsSymbolAccessibleWithin(reference.GetMethod, derived)))
            return false;
        var type = members[0] switch { IFieldSymbol field => field.Type, IPropertySymbol property => property.Type, _ => null };
        var expected = DesignerProjectUserControlDiscovery.NormalizeTypeName(node.TypeName);
        return type is not null && (expected == type.ToDisplayString()
            || !expected.Contains('.', StringComparison.Ordinal) && expected == type.Name);
    }

    private static string Normalize(string typeName)
        => DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);

    private static void CopyProperties(IReadOnlyDictionary<string, DesignPropertyValue> source,
        SortedDictionary<string, DesignPropertyValue> target)
    {
        foreach (var property in source) target[property.Key] = property.Value;
    }

    private static IEnumerable<DesignControlNode> Enumerate(IEnumerable<DesignControlNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Enumerate(node.Children)) yield return child;
        }
    }
}

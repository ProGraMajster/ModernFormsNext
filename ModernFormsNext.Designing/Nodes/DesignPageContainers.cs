namespace ModernFormsNext.Designing;

/// <summary>Describes authored children of the known framework page containers using document data only.</summary>
/// <remarks>
/// The existing Children collection remains the sole authored graph. These rules name actual
/// runtime ownership members; constructor-owned hosts and navigation history are never nodes.
/// Type classification accepts only short framework names or their ModernFormsNext qualification,
/// optionally prefixed with global::. It never resolves assemblies, constructors or converters.
/// </remarks>
public static class DesignPageContainers
{
    /// <summary>Tests an exact short or framework-qualified name without resolving a runtime type.</summary>
    /// <param name="typeName">The document type name; null or empty is never a match.</param>
    /// <param name="frameworkName">The unqualified framework type name to match.</param>
    /// <returns>Whether the names match ordinally, without treating arbitrary namespaces as framework types.</returns>
    public static bool IsType(string? typeName, string frameworkName)
    {
        ArgumentNullException.ThrowIfNull(frameworkName);
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        var normalized = typeName.Trim();
        if (normalized.StartsWith("global::", StringComparison.Ordinal))
            return normalized[8..] == "ModernFormsNext." + frameworkName;
        return normalized == frameworkName || normalized == "ModernFormsNext." + frameworkName;
    }

    /// <summary>Tests whether a name denotes one of the concrete, stabilized framework page types.</summary>
    /// <param name="typeName">The short or qualified document type name; null is unknown.</param>
    /// <returns>True for ContentPage, NavigationPage, TabbedPage, FlyoutPage or AppShell.</returns>
    /// <remarks>Custom page subclasses require separate detached metadata and are not inferred from a suffix.</remarks>
    public static bool IsPage(string? typeName)
        => IsType(typeName, "ContentPage") || IsType(typeName, "NavigationPage") ||
            IsType(typeName, "TabbedPage") || IsType(typeName, "FlyoutPage") || IsType(typeName, "AppShell");

    /// <summary>Gets the canonical ownership member for an authored child, or null for an ordinary container.</summary>
    /// <param name="parent">The parent node, whose actual Children collection owns the child.</param>
    /// <param name="child">The authored child, not a constructor-owned implicit control.</param>
    /// <returns>Content, Page, RootPage, Pages, Flyout or Detail; null when no known page slot applies.</returns>
    /// <remarks>
    /// RootPage denotes the NavigationPage constructor argument, not a writable runtime property.
    /// Pages is an ordered collection. Flyout and Detail are identified by the existing designer role field.
    /// Call GetChildError before generation or hosting; this method alone does not validate cardinality.
    /// </remarks>
    public static string? GetChildMember(DesignControlNode parent, DesignControlNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        if (IsType(parent.TypeName, "ContentPage")) return "Content";
        if (IsType(parent.TypeName, "PageHost")) return "Page";
        if (IsType(parent.TypeName, "NavigationPage")) return "RootPage";
        if (IsType(parent.TypeName, "TabbedPage")) return "Pages";
        if (IsType(parent.TypeName, "FlyoutPage") && child.Properties is not null &&
            child.Properties.TryGetValue(DesignNodeRoleNames.RolePropertyName, out var role) &&
            role is not null && role.Kind == DesignPropertyValueKind.String && role.Value is string text)
        {
            if (text == DesignNodeRoleNames.FlyoutPageFlyout) return "Flyout";
            if (text == DesignNodeRoleNames.FlyoutPageDetail) return "Detail";
        }
        return null;
    }

    /// <summary>Validates this node's immediate authored page children without constructing controls.</summary>
    /// <param name="node">The node to inspect; descendants remain the canonical validator's responsibility.</param>
    /// <returns>The first ownership error, or null when the known page rules are satisfied.</returns>
    /// <remarks>AppShell history and factories are application runtime state; its authored child list must be empty.</remarks>
    public static string? GetChildError(DesignControlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        bool content = IsType(node.TypeName, "ContentPage");
        bool host = IsType(node.TypeName, "PageHost");
        bool navigation = IsType(node.TypeName, "NavigationPage");
        bool tabs = IsType(node.TypeName, "TabbedPage");
        bool flyout = IsType(node.TypeName, "FlyoutPage");
        if (node.Children is null) return "Authored children must be a non-null collection.";
        if (node.Children.Any(child => child is null)) return "Authored page children must not contain null nodes.";
        if (IsType(node.TypeName, "AppShell") && node.Children.Count != 0)
            return "AppShell has no authored children; register route factories explicitly in application code.";
        if ((content || host || navigation) && node.Children.Count > 1)
            return $"{node.TypeName} accepts at most one authored {(content ? "content control" : "page")}.";
        if (tabs && node.Properties is not null && node.Properties.TryGetValue("SelectedIndex", out var selected) &&
            (selected is null || selected.Kind != DesignPropertyValueKind.Int32 || selected.Value is not int index || index < -1 || index >= node.Children.Count))
            return "TabbedPage.SelectedIndex must be -1 or an authored page index.";
        if ((navigation || flyout || IsType(node.TypeName, "AppShell")) && node.Properties is not null &&
            node.Properties.TryGetValue("TransitionDuration", out var storedDuration) &&
            (storedDuration is null || !TimeSpanDesignValue.TryRead(storedDuration, out var duration, out _) || duration < TimeSpan.Zero))
            return "TransitionDuration requires a non-negative canonical System.TimeSpan value.";
        if (host || navigation || tabs || flyout)
            foreach (var child in node.Children)
                if (!IsPage(child.TypeName)) return $"{node.TypeName} requires known framework pages, not '{child.TypeName}'.";
        if (flyout)
        {
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in node.Children)
            {
                var member = GetChildMember(node, child);
                if (member is null) return "FlyoutPage children require an explicit Flyout or Detail designer role.";
                if (!members.Add(member)) return $"FlyoutPage contains more than one authored {member} page.";
            }
        }
        return null;
    }
}

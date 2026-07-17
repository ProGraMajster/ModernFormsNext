using System.Collections.Concurrent;
using System.Reflection;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_resourcesProperty = PropertyStore.CreateKey();
    private static readonly int s_resourceReferencesProperty = PropertyStore.CreateKey();
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo> ResourceProperties = [];

    /// <summary>
    /// Gets the resources defined directly on this control.
    /// </summary>
    /// <remarks>
    /// Resource lookup starts here, then walks parent controls, the owning window, and finally
    /// <see cref="Application.Resources"/>. The dictionary is created lazily so controls that do
    /// not define resources keep the normal lightweight storage profile.
    /// </remarks>
    public ResourceDictionary Resources
    {
        get
        {
            if (!Properties.TryGetValue(s_resourcesProperty, out ResourceDictionary? resources))
                resources = Properties.AddValue(s_resourcesProperty, new ResourceDictionary());

            return resources!;
        }
    }

    /// <summary>
    /// Occurs when a runtime resource value cannot be assigned to its referenced CLR property.
    /// </summary>
    /// <remarks>
    /// Initial type mismatches are rejected by <see cref="SetResourceReference(string, object)"/>.
    /// This event reports later incompatible resource replacements or exceptions raised by a
    /// property setter. The control restores the value captured before the reference was created.
    /// </remarks>
    public event EventHandler<ResourceReferenceErrorEventArgs>? ResourceReferenceFailed;

    /// <summary>
    /// Creates or replaces a dynamic reference from a public writable CLR property to a resource key.
    /// </summary>
    /// <param name="propertyName">
    /// The case-sensitive name of a public writable instance property on this control.
    /// </param>
    /// <param name="resourceKey">The non-null resource key to resolve.</param>
    /// <remarks>
    /// <para>
    /// The current property value is captured as the final fallback. When the resource changes, the
    /// normal property setter is invoked; the setter remains responsible for the appropriate repaint
    /// or layout request. If no scope contains the key, the captured value is used.
    /// </para>
    /// <para>
    /// Assigning the property directly does not remove the reference. Call
    /// <see cref="ClearResourceReference(string)"/> before taking manual ownership of its value.
    /// Resource updates for live controls must occur on the owning UI thread.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;
    /// button.SetResourceReference(nameof(Control.BackColor), "Button.Primary.Background");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when the property name is empty, missing, indexed, static, or not publicly writable.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="propertyName"/> or <paramref name="resourceKey"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initially resolved resource value is incompatible with the property type or
    /// the property setter rejects it.
    /// </exception>
    public void SetResourceReference(string propertyName, object resourceKey)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(resourceKey);
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("A resource target property name cannot be empty.", nameof(propertyName));

        PropertyInfo property = GetResourceProperty(GetType(), propertyName);
        Dictionary<string, ResourceReferenceBinding> references = GetOrCreateResourceReferences();

        object? fallbackValue;
        if (references.Remove(property.Name, out var previous))
        {
            fallbackValue = previous.Fallback;
            previous.Dispose();
        }
        else
        {
            fallbackValue = property.GetValue(this);
        }

        var reference = new ResourceReferenceBinding(this, property, resourceKey, fallbackValue);
        references.Add(property.Name, reference);

        try
        {
            reference.ApplyInitialValue();
        }
        catch
        {
            references.Remove(property.Name);
            reference.RestoreFallback();
            reference.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Removes a dynamic resource reference and restores the property value captured when the
    /// reference was first created.
    /// </summary>
    /// <param name="propertyName">The case-sensitive target property name.</param>
    /// <returns><see langword="true"/> when a reference was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyName"/> is <see langword="null"/>.</exception>
    public bool ClearResourceReference(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references) ||
            !references!.Remove(propertyName, out var reference))
        {
            return false;
        }

        reference.RestoreFallback();
        reference.Dispose();
        if (references.Count == 0)
            Properties.RemoveValue(s_resourceReferencesProperty);

        return true;
    }

    /// <summary>
    /// Searches this control, its parent controls, its owning window, and application resources for
    /// the specified key.
    /// </summary>
    /// <param name="resourceKey">The non-null resource key to find.</param>
    /// <param name="value">Receives the nearest resource value when found.</param>
    /// <returns><see langword="true"/> when a resource was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceKey"/> is <see langword="null"/>.</exception>
    public bool TryFindResource(object resourceKey, out object? value)
    {
        ArgumentNullException.ThrowIfNull(resourceKey);

        for (Control? current = this; current is not null; current = current.Parent)
        {
            if (current.ResourcesInternal?.TryGetValue(resourceKey, out value) == true)
                return true;
        }

        if (FindWindow()?.ResourcesInternal?.TryGetValue(resourceKey, out value) == true)
            return true;

        return Application.Resources.TryGetValue(resourceKey, out value);
    }

    /// <summary>
    /// Raises <see cref="ResourceReferenceFailed"/>.
    /// </summary>
    /// <param name="e">Information about the rejected resource value.</param>
    /// <remarks>
    /// Derived controls that override this method should call the base implementation so registered
    /// handlers receive the diagnostic.
    /// </remarks>
    protected internal virtual void OnResourceReferenceFailed(ResourceReferenceErrorEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ResourceReferenceFailed?.Invoke(this, e);
    }

    internal ResourceDictionary? ResourcesInternal
        => Properties.TryGetValue(s_resourcesProperty, out ResourceDictionary? resources) ? resources : null;

    internal bool IsResourceScopeInHierarchy(ResourceDictionary source)
    {
        for (Control? current = this; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current.ResourcesInternal, source))
                return true;
        }

        if (ReferenceEquals(FindWindow()?.ResourcesInternal, source))
            return true;

        return ReferenceEquals(Application.Resources, source);
    }

    internal void RefreshResourceBindingsForSubtree()
    {
        if (Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            foreach (var reference in references!.Values.ToArray())
                reference.Refresh();
        }

        if (Properties.GetObject(s_controlsCollectionProperty) is ControlCollection children)
        {
            foreach (Control child in children)
                child.RefreshResourceBindingsForSubtree();
        }
    }

    private static PropertyInfo GetResourceProperty(Type controlType, string propertyName)
    {
        return ResourceProperties.GetOrAdd((controlType, propertyName), static key =>
        {
            PropertyInfo? property = key.Type.GetProperty(
                key.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property is null)
            {
                throw new ArgumentException(
                    $"Public property '{key.PropertyName}' was not found on control type '{key.Type.FullName}'.",
                    "propertyName");
            }

            if (property.GetIndexParameters().Length != 0 || property.SetMethod?.IsPublic != true)
            {
                throw new ArgumentException(
                    $"Property '{key.Type.FullName}.{key.PropertyName}' must be a public writable non-indexed instance property.",
                    "propertyName");
            }

            return property;
        });
    }

    private Dictionary<string, ResourceReferenceBinding> GetOrCreateResourceReferences()
    {
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            references = Properties.AddValue(
                s_resourceReferencesProperty,
                new Dictionary<string, ResourceReferenceBinding>(StringComparer.Ordinal));
        }

        return references!;
    }

    private void DisposeResourceReferences()
    {
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            return;
        }

        foreach (var reference in references!.Values)
            reference.Dispose();

        references.Clear();
        Properties.RemoveValue(s_resourceReferencesProperty);
    }
}
